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
using WaadShared;

using static WaadShared.LogonCommClient;
using static WaadShared.LogonCommServer;
using static WaadShared.RealmListOpcode;

namespace WaadRealmServer;

public class LogonCommClientSocket : WaadShared.Network.Socket
{
    private uint remaining;
    private ushort opcode;
    private readonly RC4Engine _sendCrypto = new();
    private readonly RC4Engine _recvCrypto = new();

    public uint last_ping;
    public uint last_pong;
    public uint pingtime;
    public uint latency;
    public uint _id;
    public uint authenticated;
    public bool use_crypto;
    public HashSet<uint> realm_ids = [];
    

    // Constructeur sans paramètre pour compatibilité avec ConnectTCPSocket<T>
    public LogonCommClientSocket() : base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, 724288, 262444)
    {
        var now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        last_ping = last_pong = pingtime = now;
        remaining = opcode = 0;
        _id = 0;
        latency = 0;
        use_crypto = false;
        authenticated = 0;
    }
    public LogonCommClientSocket(Socket fd)
        : base(fd, 724288, 262444)
    {
        last_ping = last_pong = pingtime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        remaining = opcode = 0;
        _id = 0;
        latency = 0;
        use_crypto = false;
        authenticated = 0;
    }

    public override void OnRead()
    {
        // Lecture du header puis du payload, déchiffrement si besoin, dispatch
        while (true)
        {
            if (remaining == 0)
            {
                // Pas assez de données pour le header
                if (GetReadBuffer().GetSize() < 6)
                    return;

                // Lecture du header (opcode 2 octets, taille 4 octets)
                byte[] header = new byte[6];
                GetReadBuffer().Read(header, 6);

                ushort op = BitConverter.ToUInt16(header, 0);
                uint size = BitConverter.ToUInt32(header, 2);

                if (use_crypto)
                {
                    byte[] opBytes = new byte[2];
                    byte[] sizeBytes = new byte[4];
                    Array.Copy(header, 0, opBytes, 0, 2);
                    Array.Copy(header, 2, sizeBytes, 0, 4);
                    _recvCrypto.Process(opBytes, opBytes);
                    _recvCrypto.Process(sizeBytes, sizeBytes);
                    op = BitConverter.ToUInt16(opBytes, 0);
                    size = BitConverter.ToUInt32(sizeBytes, 0);
                }

                // Endianness
                size = Swap32(size);
                opcode = op;
                remaining = size;
            }

            // Pas assez de données pour le payload
            if (GetReadBuffer().GetSize() < remaining)
                return;

            // Lecture du payload
            byte[] payload = new byte[remaining];
            if (remaining > 0)
                GetReadBuffer().Read(payload, (int)remaining);

            if (use_crypto && remaining > 0)
                _recvCrypto.Process(payload, payload);

            // Construction du WorldPacket et dispatch
            var packet = new WorldPacket(opcode, (int)remaining);
            if (remaining > 0)
                Array.Copy(payload, 0, packet.Contents, 0, (int)remaining);
            packet.Size = (int)remaining;
            HandlePacket(packet);

            remaining = 0;
            opcode = 0;
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
        uint sizeNet = Swap32(header.Size);
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

        if (rv) BurstPush();
        BurstEnd();
    }

    // Utilitaire pour swap32 (endianness)
    private static uint Swap32(uint v)
    {
        return ((v & 0xFF) << 24) | ((v & 0xFF00) << 8) | ((v & 0xFF0000) >> 8) | ((v & 0xFF000000) >> 24);
    }

    public void HandlePacket(WorldPacket recvData)
    {
        // Tableau des handlers, indexé par opcode (voir enum RMSG_*)
        // Attention : l'ordre doit correspondre à l'enum côté client/serveur
        // Les opcodes non gérés sont à null
        var handlers = new Action<WorldPacket>[]
        {
            null,                        // RMSG_NULL
            null,                        // RCMSG_REGISTER_REALM
            HandleRegister,              // RSMSG_REALM_REGISTERED
            null,                        // RCMSG_REQUEST_SESSION
            HandleSessionInfo,           // RSMSG_SESSION_RESULT
            null,                        // RCMSG_PING
            HandlePong,                  // RSMSG_PONG
            null,                        // RCMSG_SQL_EXECUTE
            null,                        // RCMSG_RELOAD_ACCOUNTS
            null,                        // RCMSG_AUTH_CHALLENGE
            HandleAuthResponse,          // RSMSG_AUTH_RESPONSE
            HandleRequestAccountMapping, // RSMSG_REQUEST_ACCOUNT_CHARACTER_MAPPING
            null,                        // RCMSG_ACCOUNT_CHARACTER_MAPPING_REPLY
            null,                        // RCMSG_UPDATE_CHARACTER_MAPPING_COUNT
            HandleDisconnectAccount,     // RSMSG_DISCONNECT_ACCOUNT
            null,                        // RCMSG_TEST_CONSOLE_LOGIN
            HandleConsoleAuthResult,     // RSMSG_CONSOLE_LOGIN_RESULT
            null,                        // RCMSG_MODIFY_DATABASE
            HandleServerPing,            // RCMSG_SERVER_PING
            HandlePong,                  // RSMSG_SERVER_PONG
        };

        ushort op = recvData.GetOpcode();
        if (op >= handlers.Length || handlers[op] == null)
        {
            CLog.Error("[LogonCommClient]", R_E_LOGCOMCLT_1, $"{op}");
            return;
        }
        handlers[op](recvData);
    }

    public void SendPing()
    {
        pingtime = (uint)Environment.TickCount;
        var packet = new WorldPacket((ushort)RCMSG_PING, 4);
        SendPacket(packet);
        last_ping = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public void SendChallenge()
    {
        byte[] key = LogonCommHandler.Instance.SqlPassHash;

        Logger.OutColor(LogColor.TNORMAL, L_N_LOGCOMSE_6);

        for (int i = 0; i < 20; ++i)
            Logger.OutColor(LogColor.TGREEN, $"{key[i]:X2} ");

        Logger.OutColor(LogColor.TNORMAL, "\n");

        /* initialize rc4 keys */
        _recvCrypto.Setup(key);
        _sendCrypto.Setup(key);

        /* packets are encrypted from now on */
        use_crypto = true;

        var packet = new WorldPacket((ushort)RCMSG_AUTH_CHALLENGE, 20);
        packet.Append(key, 20);
        SendPacket(packet, true); // true = pas de chiffrement sur le challenge
    }

    public void HandleAuthResponse(WorldPacket recvData)
    {
        // Lecture du résultat d'authentification
        byte result = recvData.Contents[0];
        CLog.Notice("[LogonCommClient]", $"Auth response result: {result}");
        if (result != 1)
        {
            authenticated = 0xFFFFFFFF;
            CLog.Error("[LogonCommClient]", R_E_LOGCOMCLT_3);
        }
        else
        {
            authenticated = 1;
            LogonCommHandler.Instance.RequestAddition(this);
        }
        use_crypto = true;
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
            CLog.Error("[LogonCommClient]", $"Erreur d'enregistrement du realm '{realmname}' (id={realmlid}) : code erreur {error}");
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
        // Gestion du pong reçu : calcul de la latence et mise à jour des timestamps
        if (latency != 0)
        {
            uint now = (uint)Environment.TickCount;
            CLog.Debug("[LogonCommClient]", R_D_LOGCOMCLT, $"{now - pingtime}");
        }
        latency = (uint)Environment.TickCount - pingtime;
        last_pong = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public void HandleServerPing(WorldPacket recvData)
    {
        // Gestion du ping serveur : lit un uint32, renvoie un pong
        uint r = recvData.ReadUInt32();
        var packet = new WorldPacket((ushort)RCMSG_SERVER_PONG, 4);
        packet.WriteUInt32(r);
        SendPacket(packet, false);
    }

    public static void HandleSessionInfo(WorldPacket recvData)
    {
        // Gestion des infos de session : lit l'identifiant de requête et transmet le paquet au handler
        // Le C++ lit l'ID puis passe le WorldPacket (qui contient le reste des données) au handler
        uint requestId = recvData.ReadUInt32();
        // Le WorldPacket passé à OnSessionInfo doit contenir le reste du payload (après l'ID)
        // On crée un nouveau WorldPacket avec le reste des données si besoin
        var restSize = recvData.Size - 4; // 4 octets pour l'uint32
        WorldPacket restPacket;
        if (restSize > 0)
        {
            restPacket = new WorldPacket(recvData.GetOpcode(), (int)restSize);
            Array.Copy(recvData.Contents, 4, restPacket.Contents, 0, restSize);
            restPacket.Size = (int)restSize;
        }
        else
        {
            restPacket = new WorldPacket(recvData.GetOpcode(), 0);
        }
        LogonCommHandler.Instance.OnSessionInfo(restPacket, requestId);
    }

    public void HandleRequestAccountMapping(WorldPacket recvData)
    {            
        uint realmId = recvData.ReadUInt32();
        var mappingToSend = new Dictionary<uint, byte>();

        var db = RealmDatabaseManager.GetDatabase();
        if (db == null)
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

    public static void HandleConsoleAuthResult(WorldPacket recvData)        {
       
        uint requestId = recvData.ReadUInt32();
        uint result = recvData.ReadUInt32();
        ConsoleListener.ConsoleAuthCallback(requestId, result);
    }

    public override void OnDisconnect()
    {
        if (_id != 0)
        {
            CLog.Error("[LogonCommClient]", R_E_LOGCOMCLT_2);
            LogonCommHandler.Instance.ConnectionDropped(_id);
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
