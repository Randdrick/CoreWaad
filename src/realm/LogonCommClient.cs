/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2025 WAAD Team <https://arbonne.games-rpg.net/>
 *
 * From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;
using System.IO.Compression;
using System.Threading;
using WaadShared;

using static WaadShared.LogonCommClient;
using static WaadShared.LogonCommServer;
using static WaadShared.RealmListOpcode;

namespace WaadRealmServer;

public class LogonCommClientSocket : WaadShared.Network.Socket, IDisposable
{
    private uint remaining;
    private ushort opcode;
    private readonly RC4Engine _sendCrypto = new();
    private readonly RC4Engine _recvCrypto = new();
    public long last_ping_ms;
    public long last_pong_ms;
    public long last_server_ping_ms;
    public long pingtime_ms;
    public uint latency;
    public uint _id;
    public uint authenticated;
    public bool use_crypto;
    public HashSet<uint> realm_ids = [];
    public Action<bool> AuthCompleted { get; set; }
    private bool _disposed = false;
    

    // Constructeur sans paramètre pour compatibilité avec ConnectTCPSocket<T>
    public LogonCommClientSocket() : base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, 724288, 262444)
    {
        long now = Environment.TickCount64;
        last_ping_ms = last_pong_ms = last_server_ping_ms = pingtime_ms = now;
        remaining = opcode = 0;
        _id = 0;
        latency = 0;
        use_crypto = false;
        authenticated = 0;
    }
    public LogonCommClientSocket(Socket fd)
        : base(fd, 724288, 262444)
    {
        long now = Environment.TickCount64;
        last_ping_ms = last_pong_ms = last_server_ping_ms = pingtime_ms = now;
        remaining = opcode = 0;
        _id = 0;
        latency = 0;
        use_crypto = false;
        authenticated = 0;
    }

    // IDisposable implementation
    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            // Clear collections
            if (disposing)
            {
                realm_ids.Clear();
            }
            _disposed = true;
        }
    }

    public override void OnRead()
    {
        // read header then payload, decrypt if needed, and dispatch
        CLog.Debug("[LogonCommClient]", R_D_LOGCOMCLT_ONREAD, use_crypto, GetReadBuffer().GetSize());

        while (true)
        {
            if (remaining == 0)
            {
                if (GetReadBuffer().GetSize() < 6)
                {
                    remaining = 0;
                    opcode = 0;
                    return; // not even a header yet
                }

                // read 6-byte header first and check size field
                byte[] encryptedHeader = new byte[6];
                GetReadBuffer().Read(encryptedHeader, 6);

                byte[] headerBytes = encryptedHeader;
                if (use_crypto)
                {
                    headerBytes = new byte[6];
                    _recvCrypto.Process(encryptedHeader, headerBytes);
                }

                CLog.Debug("[LogonCommClient]", R_D_LOGCOMCLT_HEADERBYTES, BitConverter.ToString(headerBytes));

                // parse opcode and payload size from header
                uint opcodeValue = BitConverter.ToUInt16(headerBytes, 0);  // bytes 0-1 = opcode
                uint payloadSize = BitConverter.ToUInt32(headerBytes, 2);  // bytes 2-5 = size

                CLog.Debug("[LogonCommClient]", R_D_LOGCOMCLT_PARSED, payloadSize, opcodeValue);

                // Nettoyer le buffer temporaire après utilisation
                Array.Clear(encryptedHeader, 0, encryptedHeader.Length);

                // sanity check
                if (payloadSize > 65535)
                {
                    CLog.Error("[LogonCommClient]", R_E_LOGCOMCLT_PAYLOAD_TOO_LARGE, payloadSize);
                    remaining = 0;
                    opcode = 0;
                    OnDisconnect();
                    return;
                }

                if (payloadSize == 0)
                {
                    // just opcode, no payload
                    opcode = (ushort)opcodeValue;
                    remaining = 0;

                    var emptyPacket = new WorldPacket(opcode, 0);
                    HandlePacket(emptyPacket);
                    opcode = 0;
                }
                else
                {
                    // wait for payload
                    if (GetReadBuffer().GetSize() < payloadSize)
                    {
                        // payload not ready yet - need to decrypt and parse it later
                        // for now, store what we know
                        opcode = (ushort)opcodeValue;
                        remaining = payloadSize;
                        return;
                    }

                    // read payload
                    byte[] encryptedPayload = new byte[payloadSize];
                    GetReadBuffer().Read(encryptedPayload, (int)payloadSize);

                    byte[] payloadBytes = encryptedPayload;
                    if (use_crypto)
                    {
                        payloadBytes = new byte[payloadSize];
                        _recvCrypto.Process(encryptedPayload, payloadBytes);
                    }

                    opcode = (ushort)opcodeValue;
                    var packet = new WorldPacket(opcode, (int)payloadSize);
                    packet.Append(payloadBytes, (int)payloadSize);
                    HandlePacket(packet);

                    // Nettoyer le buffer temporaire
                    Array.Clear(encryptedPayload, 0, encryptedPayload.Length);
                    encryptedPayload = null;

                    remaining = 0;
                    opcode = 0;
                }
            }
            else
            {
                // remaining > 0: payload arrived in a subsequent read
                if (GetReadBuffer().GetSize() < remaining)
                {
                    // Partial payload: keep state (opcode + remaining) and wait for more data
                    return; // still waiting for the rest of the payload
                }

                byte[] encryptedPayload = new byte[remaining];
                GetReadBuffer().Read(encryptedPayload, (int)remaining);

                byte[] payloadBytes = encryptedPayload;
                if (use_crypto)
                {
                    payloadBytes = new byte[remaining];
                    _recvCrypto.Process(encryptedPayload, payloadBytes);
                }

                var packet = new WorldPacket(opcode, (int)remaining);
                packet.Append(payloadBytes, (int)remaining);
                HandlePacket(packet);

                // Nettoyer le buffer temporaire
                Array.Clear(encryptedPayload, 0, encryptedPayload.Length);
                encryptedPayload = null;

                remaining = 0;
                opcode = 0;
            }
        }
    }

    // Structure interne pour l'en-tête du paquet logon
    private struct LogonPacket
    {
        public ushort Opcode;
        public uint Size;
    }

    // Envoi d'un WorldPacket sur la socket, avec ou sans chiffrement
    public void SendPacket(WorldPacket data, bool noCrypto = false)
    {
        if (!IsConnected() || IsDeleted())
            return;

        BurstBegin();

        LogonPacket header = new()
        {
            Opcode = data.GetOpcode(),
            Size = (uint)data.Size
        };

        // Conversion en bytes (endianness)
        byte[] headerBytes = new byte[6];
        Array.Copy(BitConverter.GetBytes(header.Opcode), 0, headerBytes, 0, 2);
        uint sizeNet = header.Size;

        Array.Copy(BitConverter.GetBytes(sizeNet), 0, headerBytes, 2, 4);

        if (use_crypto && !noCrypto)
        {
            byte[] tmp = new byte[6];
            _sendCrypto.Process(headerBytes, tmp);
            Array.Copy(tmp, headerBytes, 6);
        }

        bool rv = BurstSend(headerBytes, 6);

        if (data.Size > 0 && rv)
        {
            byte[] payload = data.Contents;
            if (use_crypto && !noCrypto)
            {
                byte[] tmp = new byte[payload.Length];
                _sendCrypto.Process(payload, tmp);
                Array.Copy(tmp, payload, payload.Length);
            }
            rv = BurstSend(payload, data.Size);
        }

        if (rv)
            BurstPush();
        BurstEnd();
    }

    // Send packet directly without buffering (for PING/PONG packets)
    public bool SendPacketDirect(WorldPacket data, bool noCrypto = false)
    {
        if (!IsConnected() || IsDeleted())
            return false;

        LogonPacket header = new()
        {
            Opcode = data.GetOpcode(),
            Size = (uint)data.Size
        };

        // Conversion en bytes (endianness)
        byte[] headerBytes = new byte[6];
        Array.Copy(BitConverter.GetBytes(header.Opcode), 0, headerBytes, 0, 2);
        uint sizeNet = header.Size;

        Array.Copy(BitConverter.GetBytes(sizeNet), 0, headerBytes, 2, 4);

        // Combine header and payload into a single buffer to send atomically
        byte[] packetData;
        if (use_crypto && !noCrypto)
        {
            byte[] tmp = new byte[6];
            _sendCrypto.Process(headerBytes, tmp);
            
            if (data.Size > 0)
            {
                byte[] payload = data.Contents;
                byte[] encPayload = new byte[payload.Length];
                _sendCrypto.Process(payload, encPayload);
                
                // Combine encrypted header + encrypted payload
                packetData = new byte[6 + payload.Length];
                Array.Copy(tmp, 0, packetData, 0, 6);
                Array.Copy(encPayload, 0, packetData, 6, payload.Length);
            }
            else
            {
                packetData = tmp;
            }
        }
        else
        {
            // No encryption: combine header + payload directly
            if (data.Size > 0)
            {
                byte[] payload = data.Contents;
                packetData = new byte[6 + payload.Length];
                Array.Copy(headerBytes, 0, packetData, 0, 6);
                Array.Copy(payload, 0, packetData, 6, payload.Length);
            }
            else
            {
                packetData = headerBytes;
            }
        }

        // Send the complete packet (header + payload) in a single atomic operation
        return SendPacketDirect(packetData, packetData.Length);
    }

    // Utilitaire pour swap32 (endianness)
    private static uint Swap32(uint v)
    {
        return ((v & 0xFF) << 24) | ((v & 0xFF00) << 8) | ((v & 0xFF0000) >> 8) | ((v & 0xFF000000) >> 24);
    }

    public void HandlePacket(WorldPacket recvData)
    {
        // Tout paquet entrant (sauf AUTH_CHALLENGE) prouve que la connexion est vivante
        if (recvData.GetOpcode() != (ushort)RCMSG_AUTH_CHALLENGE)
        {
            long now = Environment.TickCount64;
            Interlocked.Exchange(ref last_pong_ms, now);
            Interlocked.Exchange(ref last_ping_ms, now);
        }

        // Tableau des handlers, indexé par opcode (voir enum RMSG_*)
        // Attention : l'ordre doit correspondre à l'enum côté client/serveur
        // Les opcodes non gérés sont à null
        // Note: Le tableau doit avoir au moins 20 éléments pour couvrir opcode 19 (RCMSG_SERVER_PONG)
        var handlers = new Action<WorldPacket>[]
        {
            null,                        // 0 - RMSG_NULL
            null,                        // 1 - RCMSG_REGISTER_REALM
            HandleRegister,              // 2 - RSMSG_REALM_REGISTERED
            null,                        // 3 - RCMSG_REQUEST_SESSION
            HandleSessionInfo,           // 4 - RSMSG_SESSION_RESULT
            null,                        // 5 - RCMSG_PING (le RealmServer ENVOIE ce PING, ne le reçoit pas)
            HandlePong,                  // 6 - RSMSG_PONG (PONG reçu du LogonServer, réponse à notre PING opcode 5)
            null,                        // 7 - RCMSG_SQL_EXECUTE
            null,                        // 8 - RCMSG_RELOAD_ACCOUNTS
            null,                        // 9 - RCMSG_AUTH_CHALLENGE
            HandleAuthResponse,          // 10 - RSMSG_AUTH_RESPONSE
            HandleRequestAccountMapping, // 11 - RSMSG_REQUEST_ACCOUNT_CHARACTER_MAPPING
            null,                        // 12 - RCMSG_ACCOUNT_CHARACTER_MAPPING_REPLY
            null,                        // 13 - RCMSG_UPDATE_CHARACTER_MAPPING_COUNT
            HandleDisconnectAccount,     // 14 - RSMSG_DISCONNECT_ACCOUNT
            null,                        // 15 - RCMSG_TEST_CONSOLE_LOGIN
            HandleConsoleAuthResult,     // 16 - RSMSG_CONSOLE_LOGIN_RESULT
            null,                        // 17 - RCMSG_MODIFY_DATABASE
            HandleServerPing,            // 18 - RSMSG_SERVER_PING (PING reçu du LogonServer)
            null,                        // 19 - RCMSG_SERVER_PONG (le RealmServer ENVOIE ce PONG, ne le reçoit pas)
        };

        ushort op = recvData.GetOpcode();
        if (op >= handlers.Length || handlers[op] == null)
        {
            CLog.Error("[LogonCommClient]", R_E_LOGCOMCLT_1, $"{op}");
            return;
        }
        handlers[op](recvData);

        recvData.Clear(); // Nettoyer le buffer après traitement
    }

    public void SendPing()
    {
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref pingtime_ms, now);
        // Rate-limit retries when a pong is delayed or lost. The timeout decision
        // still uses last_pong_ms, so this does not make a dead link look alive.
        Interlocked.Exchange(ref last_ping_ms, now);
        var packet = new WorldPacket((ushort)RCMSG_PING, 4);
        packet.WriteUInt32((uint)(now & 0xFFFFFFFF));
        
        // PING packets must be sent directly without buffering to prevent timeout issues
        if (!SendPacketDirect(packet, false))
        {
            // Fallback: try normal buffered send if direct send fails
            SendPacket(packet);
        }
        else
        {
            CLog.Debug("[LogonCommClient]", "PING sent directly (no buffer).");
        }
    }

    public void SendChallenge()
    {
        byte[] key = new byte[20];
        byte[] sqlPassHash = LogonCommHandler.Instance.SqlPassHash;

        // Copier la clé pour éviter de modifier l'original
        Array.Copy(sqlPassHash, key, 20);

        try
        {
            Logger.OutColor(LogColor.TNORMAL, L_N_LOGCOMSE_6);

            for (int i = 0; i < 20; ++i)
                Logger.OutColor(LogColor.TGREEN, $"{key[i]:X2} ");

            Logger.OutColor(LogColor.TNORMAL, "\n");

            /* initialize rc4 keys */
            _recvCrypto.Setup(key, 20);
            _sendCrypto.Setup(key, 20);

            /* packets are encrypted from now on */
            use_crypto = true;

            var packet = new WorldPacket((ushort)RCMSG_AUTH_CHALLENGE, 20);
            packet.Append(key, 20);
            SendPacket(packet, true); // true = pas de chiffrement sur le challenge
        }
        finally
        {
            // Nettoyer la clé de la mémoire
            Array.Clear(key, 0, key.Length);
            key = null;
        }
    }

    public void HandleAuthResponse(WorldPacket recvData)
    {
        try
        {
            // Lecture du résultat d'authentification
            byte result = recvData.Contents[0];
            CLog.Debug("[LogonCommClient]", R_D_LOGCOMCLT_AUTH_RESULT, result);
            if (result != 1)
            {
                authenticated = 0xFFFFFFFF;
                CLog.Error("[LogonCommClient]", R_E_LOGCOMCLT_3);
                AuthCompleted?.Invoke(false);
            }
            else
            {
                authenticated = 1;
                LogonCommHandler.Instance.RequestAddition(this);
                AuthCompleted?.Invoke(true);
            }
            use_crypto = true;
        }
        finally
        {
            recvData.Clear(); // Nettoyer le buffer
        }
    }

    public void HandleRegister(WorldPacket recvData)
    {
        // Extraction des champs : error, realmlid, realmname
        uint error = recvData.ReadUInt32();
        uint realmlid = recvData.ReadUInt32();
        string realmname = recvData.ReadString();

        if (error != 0)
        {
            // Affichage d'une erreur et retour immédiat
            CLog.Error("[LogonCommClient]", R_E_LOGCOMCLT_4, realmname, realmlid, error);
            return;
        }

        // Affichage du nom du realm
        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMCLT, realmname);
        Logger.OutColor(LogColor.TGREEN, $"{realmlid} \n");

        // Ack d'ajout
        LogonCommHandler.Instance.AdditionAck(_id, realmlid);
        // Ajout au set
        realm_ids.Add(realmlid);
    }

    public void HandlePong(WorldPacket recvData)
    {
        if (recvData.Size >= 4)
        {
            _ = recvData.ReadUInt32();
        }

        // Gestion du pong reçu : calcul de la latence et mise à jour des timestamps
        long now = Environment.TickCount64;
        long pingtime = Interlocked.Read(ref pingtime_ms);
        
        if (latency != 0)
        {
            CLog.Debug("[LogonCommClient]", R_D_LOGCOMCLT, $"{now - pingtime}");
        }
        
        latency = (uint)(now - pingtime);
        Interlocked.Exchange(ref last_pong_ms, now);
    }

    public void HandleServerPing(WorldPacket recvData)
    {
        // Gestion du ping serveur : lit un uint32, renvoie un pong
        uint r = 0;
        if (recvData.Size >= 4)
        {
            r = recvData.ReadUInt32();
        }

        SendServerPong(r);

        // Un SERVER_PING reçu prouve que la connexion est vivante : réinitialiser le timer de pong.
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref last_server_ping_ms, now);
        Interlocked.Exchange(ref last_pong_ms, now);
    }

    public void SendServerPong(uint echo)
    {
        var packet = new WorldPacket((ushort)RCMSG_SERVER_PONG, 4);
        packet.WriteUInt32(echo);
        
        // PONG packets must be sent directly without buffering to prevent timeout issues
        if (!SendPacketDirect(packet, false))
        {
            // Fallback: try normal buffered send if direct send fails
            SendPacket(packet, false);
        }
        else
        {
            CLog.Debug("[LogonCommClient]", string.Format("SERVER_PONG sent directly (no buffer)."));
        }
    }

    public void HandleServerPong(WorldPacket recvData)
    {
        if (recvData.Size >= 4)
        {
            _ = recvData.ReadUInt32();
        }
        // Mise à jour du timestamp pour éviter le timeout
        long now = Environment.TickCount64;
        long pingtime = Interlocked.Read(ref pingtime_ms);
        latency = (uint)(now - pingtime);
        Interlocked.Exchange(ref last_pong_ms, now);
    }

    public static void HandleSessionInfo(WorldPacket recvData)
    {
        // Read requestId first; recvData.Rpos now points to the error field.
        // Pass recvData directly — InformationRetreiveCallback reads from current Rpos.
        uint requestId = recvData.ReadUInt32();
        LogonCommHandler.Instance.OnSessionInfo(recvData, requestId);
    }

    public void HandleRequestAccountMapping(WorldPacket recvData)
    {            
        uint realmId = recvData.ReadUInt32();
        var mappingToSend = new Dictionary<uint, byte>();

        var db = RealmDatabaseManager.GetDatabase();
        if (db == null || !db.IsInitialized)
            return;

        var result = db.Query("SELECT acct FROM characters");
        if (result != null)
        {
            do
            {
                uint accountId = (uint)Convert.ToInt32(result.GetValue(0));
                if (mappingToSend.TryGetValue(accountId, out byte value))
                    mappingToSend[accountId] = ++value;
                else
                    mappingToSend[accountId] = 1;
            } while (result.NextRow());
        }

        if (mappingToSend.Count == 0)
            return; // Rien à envoyer

        // Batchs de 40 000 comptes max
        const int BATCH_SIZE = 40000;
        int remaining = mappingToSend.Count;
        var enumerator = mappingToSend.GetEnumerator();
        while (remaining > 0)
        {
            var uncompressed = new ByteBuffer(BATCH_SIZE * 5 + 8);
            uncompressed.WriteUInt32(realmId);
            int batchCount = Math.Min(remaining, BATCH_SIZE);
            uncompressed.WriteUInt32((uint)batchCount);
            for (int i = 0; i < batchCount; ++i)
            {
                if (!enumerator.MoveNext()) break;
                uncompressed.WriteUInt32(enumerator.Current.Key);
                uncompressed.Write(enumerator.Current.Value);
            }
            remaining -= batchCount;
            CompressAndSend(uncompressed);
        }
    }

    public void UpdateAccountCount(uint accountId, byte add)
    {
        // Envoie à tous les realms connus la mise à jour du nombre de personnages pour un compte
        foreach (var realmId in realm_ids)
        {
            var packet = new WorldPacket((ushort)RCMSG_UPDATE_CHARACTER_MAPPING_COUNT, 9);
            packet.WriteUInt32(realmId);
            packet.WriteUInt32(accountId);
            packet.Write(add);
            SendPacket(packet, false);
        }
    }

    public static void HandleDisconnectAccount(WorldPacket recvData)
    {
        // Déconnexion d'un compte par son ID
        uint accountId = recvData.ReadUInt32();
        var session = ClientMgr.Instance.GetSession(accountId);
        session?.Disconnect();
    }

    public static void HandleConsoleAuthResult(WorldPacket recvData) 
    {
       
        uint requestId = recvData.ReadUInt32();
        uint result = recvData.ReadUInt32();
        ConsoleListener.ConsoleAuthCallback(requestId, result);
    }

    public override void OnDisconnect()
    {
        uint droppedId = Interlocked.Exchange(ref _id, 0);

        if (droppedId != 0)
        {
            if (!LogonCommHandler.Instance.IsShuttingDown)
            {
                CLog.Error("[LogonCommClient]", R_E_LOGCOMCLT_2);
                LogonCommHandler.Instance.ConnectionDropped(droppedId);
            }
        }
    }

    public void CompressAndSend(ByteBuffer uncompressed)
    {
        // Portage fidèle du C++ : compression Deflate, header, envoi
        int srcLen = uncompressed.Size;
        int destLen = srcLen + srcLen / 10 + 16;
        var packet = new WorldPacket((ushort)RCMSG_ACCOUNT_CHARACTER_MAPPING_REPLY, destLen + 4);

        // Réserver 4 octets pour la taille non compressée
        // (sera écrite en little endian)
        byte[] compressed = new byte[destLen];
        int compressedSize = 0;
        using (var ms = new MemoryStream(compressed))
        using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, true))
        {
            ds.Write(uncompressed.ToArray(), 0, srcLen);
            ds.Flush();
            compressedSize = (int)ms.Position;
        }

        // Écrire la taille non compressée (4 octets, little endian)
        Array.Copy(BitConverter.GetBytes(srcLen), 0, packet.Contents, 0, 4);
        // Copier les données compressées après les 4 octets
        Array.Copy(compressed, 0, packet.Contents, 4, compressedSize);
        packet.Size = compressedSize + 4;

        SendPacket(packet, false);
    }
}
