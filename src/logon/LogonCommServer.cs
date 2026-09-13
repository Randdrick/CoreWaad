/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2021 WAAD Team <https://arbonne.games-rpg.net/>
 *
 * From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore.
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
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using WaadShared;

using static System.Buffer;
using static WaadShared.LogonCommClient;
using static WaadShared.LogonCommServer;
using static WaadShared.RealmListOpcode;

namespace LogonServer;

public class LogonPacket
{
    public ushort Opcode;
    public uint Size;
}

public class LogonCommServerSocket : WaadShared.Network.Socket, IDisposable
{
    public long lastPing_ms;
    private Timer pingTimer;
    private uint remaining;
    private ushort opcode;
    // private bool removed;
    private bool useCrypto;
    private uint authenticated;
    private readonly RC4Engine sendCrypto = new();
    private readonly RC4Engine recvCrypto = new();
    public readonly HashSet<uint> serverIds = [];
    private readonly InformationCore sInfoCore = InformationCore.Instance;
    private static readonly List<AllowedIP> m_allowedIps = [];
    private static readonly object m_allowedIpLock = new();
    private static readonly bool ServerTrustMe = true;
    private bool _disposed = false;
    private const long ConnectionTimeout_ms = 60_000; // 60 secondes sans PONG = timeout

    // Constructeur sans paramètre public
    public LogonCommServerSocket() : base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp, 262144, 262144)
    {
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref lastPing_ms, now);
        remaining = opcode = 0;
        useCrypto = false;
        authenticated = 0;
        InitializeHandlers();
        pingTimer = new Timer(PingTimerCallback, null, 30_000, 30_000); // 30s interval
        SetKeepAlive(true, 30_000, 1_000); // Activer TCP Keep-Alive (30s idle, 1s interval)
    }

    public LogonCommServerSocket(Socket socket) : base(socket, 262144, 262144)
    {
        long now = Environment.TickCount64;
        Interlocked.Exchange(ref lastPing_ms, now);
        remaining = opcode = 0;
        // removed = false;
        useCrypto = false;
        authenticated = 0;
        InitializeHandlers();
        pingTimer = new Timer(PingTimerCallback, null, 30_000, 30_000); // 30s interval
        SetKeepAlive(true, 30_000, 1_000); // Activer TCP Keep-Alive
        // NOTE: DO NOT call OnConnect() here - ListenSocket.SetConnected() will handle it
    }

    ~LogonCommServerSocket()
    {
        Dispose(false);
    }

    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        if (disposing)
        {
            // Stop the timer BEFORE disposing it to prevent callbacks after dispose
            if (pingTimer != null)
            {
                try
                {
                    // Disable the timer (no more callbacks will be scheduled)
                    pingTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    pingTimer.Dispose();
                }
                catch { }
                pingTimer = null;
            }

            // Clear collections
            serverIds.Clear();
        }

        base.Dispose(disposing);
    }

    private void PingTimerCallback(object state)
    {
        // Exit immediately if disposed
        if (_disposed || pingTimer == null)
            return;
            
        try
        {
            long now_ms = Environment.TickCount64;
            long lastPing = Interlocked.Read(ref lastPing_ms);
            
            // Vérifier si la connexion est toujours active (timeout après 60s sans PONG)
            if (now_ms - lastPing > ConnectionTimeout_ms)
            {
                CLog.Error("[LogonCommServer]", 
                    string.Format("Connection timeout: No PONG received for {0}ms from {1}. Disconnecting.", 
                    now_ms - lastPing, GetRemoteIP()));
                OnDisconnect();
                return;
            }
            
            // Envoyer un PING toutes les 30 secondes
            // Note: lastPing_ms sera mis à jour quand le PONG sera reçu (dans HandleServerPong)
            // ou dans HandlePacket pour tout autre paquet
            SendPing();
        }
        catch (Exception ex)
        {
            CLog.Error("[LogonCommServer]", string.Format("Ping timer error: {0}", ex.Message));
        }
    }

    public override void OnDisconnect()
    {
        // Prevent double-cleanup
        if (_disposed)
            return;
            
        // Remove realms and cleanup
        try
        {
            foreach (var id in serverIds)
            {
                sInfoCore.RemoveRealm(id);
            }
            serverIds.Clear(); // Clear the collection to release memory
            sInfoCore.RemoveServerSocket(this);
        }
        catch { }

        // Dispose resources (this will also stop the timer safely)
        Dispose();
    }

    protected override void OnConnect()
    {
        // First, call base to set m_connected = true and setup IOCP read event
        base.OnConnect();

        CLog.Notice("[LogonCommServer]", L_N_LOGCOMSE_0, GetRemoteIP());
        if (!IsServerAllowed(GetRemoteAddress(this)))
        {
            CLog.Error("[LogonCommServer]", L_N_LOGCOMSE, GetRemoteIP(), GetRemotePort());
            OnDisconnect();
            return;
        }

        sInfoCore.AddServerSocket(this);
    }

    public override void OnRead()
    {
        try
        {
            while (true)
            {
                if (remaining == 0)
                {
                    if (GetReadBuffer().GetSize() < 6)
                    {
                        // Réinitialiser pour éviter un état bloquant
                        remaining = 0;
                        opcode = 0;
                        return;
                    }

                    // Read header (2 bytes opcode, 4 bytes size)
                    byte[] opcodeBytes = new byte[2];
                    byte[] sizeBytes = new byte[4];
                    GetReadBuffer().Read(opcodeBytes, 2);
                    GetReadBuffer().Read(sizeBytes, 4);

                    if (useCrypto)
                    {
                        // Decrypt in separate calls (matches C++ original: Process(2), then Process(4))
                        recvCrypto.Process(opcodeBytes, opcodeBytes);
                        recvCrypto.Process(sizeBytes, sizeBytes);
                    }

                    // Parse opcode and size
                    opcode = BitConverter.ToUInt16(opcodeBytes, 0);
                    remaining = BitConverter.ToUInt32(sizeBytes, 0);

                    // Prevent overly large packets
                    if (remaining > 65535)
                    {
                        CLog.Error("[LogonCommServer]", L_E_LOGCOMSE);
                        OnDisconnect();
                        return;
                    }
                }

                // Do we have a full packet?
                if (GetReadBuffer().GetSize() < remaining)
                {
                    CLog.Error("[LogonCommServer]", R_E_LOGCOMCLT);
                    // Réinitialiser pour éviter un état bloquant
                    remaining = 0;
                    opcode = 0;
                    return;
                }
                    

                // Create the buffer
                byte[] packetData = new byte[remaining];
                if (remaining > 0)
                {
                    GetReadBuffer().Read(packetData, (int)remaining);

                    if (useCrypto)
                    {
                        recvCrypto.Process(packetData, packetData);
                    }

                    WorldPacket buff = new(opcode, (int)remaining);
                    buff.Append(packetData, (int)remaining);

                    // Nettoyer le buffer temporaire après utilisation
                    Array.Clear(packetData, 0, packetData.Length);
                    packetData = null;

                    // Handle the packet
                    HandlePacket(buff);
                }
                else
                {
                    WorldPacket buff = new(opcode, (int)remaining);
                    // Handle the packet
                    HandlePacket(buff);
                }

                remaining = 0;
                opcode = 0;
            }
        }
        catch (Exception ex)
        {
            CLog.Error("[LogonCommServer]", string.Format("OnRead error: {0}", ex.Message));
            OnDisconnect();
        }
    }

    public void HandlePacket(WorldPacket recvData)
    {
        CLog.Debug("[LogonCommServer]", $"HandlePacket called with opcode: {recvData.Opcode}");
        
        // Only update lastPing_ms for actual responses to OUR pings (opcode 19 = RCMSG_SERVER_PONG)
        // or other "alive" packets, but NOT for incoming PINGS (opcode 5 = RCMSG_PING)
        // This ensures we detect when the RealmServer stops responding to OUR pings
        if (recvData.Opcode == (ushort)RCMSG_SERVER_PONG)
        {
            Interlocked.Exchange(ref lastPing_ms, Environment.TickCount64);
        }
        else if (recvData.Opcode != (ushort)RCMSG_AUTH_CHALLENGE && 
                 recvData.Opcode != (ushort)RCMSG_PING)
        {
            // Other packets (like session requests, etc.) also prove the link is alive
            Interlocked.Exchange(ref lastPing_ms, Environment.TickCount64);
        }
        
        if (authenticated == 0 && recvData.Opcode != (ushort)RCMSG_AUTH_CHALLENGE)
        {
            OnDisconnect();
            return;
        }
        if (recvData.Opcode >= (ushort)RMSG_COUNT || Handlers == null || Handlers[recvData.Opcode] == null)
        {
            CLog.Error("[LogonCommServer]", L_N_LOGCOMSE_1, recvData.Opcode);
            return;
        }

        Handlers[recvData.Opcode](recvData);

        recvData.Clear(); // Nettoyer le buffer du paquet après traitement
    }

    public void HandleRegister(WorldPacket recvData)
    {
        var sLog = new Logger();
        sLog.OutString("HandleRegister called");
        Realm realm = new();
        sInfoCore.AddServerSocket(this);

        realm.Name = recvData.ReadString();
        realm.Address = recvData.ReadString();
        realm.Colour = recvData.ReadUInt32();
        realm.Icon = recvData.ReadUInt32();
        realm.TimeZone = recvData.ReadUInt32();
        realm.Population = recvData.Read<float>();

        uint myId = sInfoCore.GenerateRealmID();

        lock (sInfoCore)
        {
            for (uint i = 0; i < myId; ++i)
            {
                Realm r = sInfoCore.GetRealm(i);
                if (r != null && r.Name == realm.Name && r.Address == realm.Address)
                {
                    sInfoCore.RemoveRealm(i);
                }
            }
        }

        sLog.OutString(L_N_LOGCOMSE_2, realm.Name, myId);

        sInfoCore.AddRealm(myId, realm);

        WorldPacket data = new((ushort)RSMSG_REALM_REGISTERED, 4);
        data.WriteUInt32(0);
        data.WriteUInt32(myId);
        data.WriteString(realm.Name);
        SendPacket(data);
        serverIds.Add(myId);

        data.Initialize((ushort)RSMSG_REQUEST_ACCOUNT_CHARACTER_MAPPING);
        data.WriteUInt32(myId);
        SendPacket(data);
    }

    public void HandleSessionRequest(WorldPacket recvData)
    {
        if (recvData.Size < 5) // at least 4 bytes for requestId + 1 byte for string length
        {
            CLog.Error("[LogonCommServer]", $"HandleSessionRequest: packet too small (expected at least 5 bytes, got {recvData.Size})");
            return;
        }
        uint requestId = recvData.ReadUInt32();
        string accountName = recvData.ReadString();
        Account acct = AccountMgr.GetAccount(accountName);

        uint error = 0;
        if (acct == null || acct.SessionKey == null)
        {
            error = 1;
        }

        WorldPacket data = new((ushort)RSMSG_SESSION_RESULT, 150);
        data.WriteUInt32(requestId);
        data.WriteUInt32(error);

        if (error == 0)
        {
            CLog.Debug("[LogonCommServer]", $"HandleSessionRequest: sending accountId={acct.AccountId} name={acct.UsernamePtr}");
            // Order must match InformationRetreiveCallback reads:
            // 1. accountName (string)  2. gmFlags (string)  3. accountFlags (byte)  4. accountId (uint32)  5. sessionKey (40 bytes)
            data.WriteString(acct.UsernamePtr);
            data.WriteString(acct.GMFlags ?? string.Empty);
            data.WriteByte(acct.AccountFlags);
            data.WriteUInt32(acct.AccountId);
            data.Write(acct.SessionKey, 0, 40);
            byte[] localeBytes = Encoding.UTF8.GetBytes(acct.Locale);
            data.Write(localeBytes, 0, localeBytes.Length);
            data.WriteUInt32(acct.Muted);
        }

        SendPacket(data);
    }

    public void HandlePing(WorldPacket recvData)
    {
        uint pingValue = 0;
        if (recvData.Size >= 4)
        {
            pingValue = recvData.ReadUInt32();
        }

        var pong = new WorldPacket((ushort)RSMSG_PONG, 4);
        pong.WriteUInt32(pingValue);
        
        // PONG packets must be sent directly without buffering to prevent timeout issues
        if (!SendPacketDirect(pong, false))
        {
            // Fallback: try normal buffered send if direct send fails
            SendPacket(pong);
        }
        else
        {
            CLog.Debug("[LogonCommServer]", "PONG sent directly (no buffer).");
        }
        
        Interlocked.Exchange(ref lastPing_ms, Environment.TickCount64);
    }

    public bool SendPacket(WorldPacket data, bool noCrypto = false)
    {
        // Vérifier que le socket est toujours connecté
        if (!IsConnected())
        {
            CLog.Error("[LogonCommServer]", string.Format("Cannot send packet: socket to {0} is disconnected.", GetRemoteIP()));
            return false;
        }

        bool allSent = true;
        BurstBegin();

        try
        {
            // Build 6-byte header: 2 bytes opcode, 4 bytes size
            byte[] header = new byte[6];

            ushort op = (ushort)data.GetOpcode();
            uint size = (uint)data.Size;

            BlockCopy(BitConverter.GetBytes(op), 0, header, 0, 2);
            BlockCopy(BitConverter.GetBytes(size), 0, header, 2, 4);

            // Encrypt header into temporary buffer if needed (avoid in-place)
            bool headerSent;
            if (useCrypto && !noCrypto)
            {
                var encHeader = new byte[6];
                sendCrypto.Process(header, encHeader);
                headerSent = BurstSend(encHeader, 6);
            }
            else
            {
                headerSent = BurstSend(header, 6);
            }

            if (data.Size > 0 && headerSent)
            {
                var payload = data.Contents; // get contents once

                if (useCrypto && !noCrypto)
                {
                    var encPayload = new byte[payload.Length];
                    sendCrypto.Process(payload, encPayload);
                    allSent = BurstSend(encPayload, data.Size);
                }
                else
                {
                    allSent = BurstSend(payload, data.Size);
                }
            }
            else if (data.Size > 0)
            {
                // Header failed to send, so payload cannot be sent
                allSent = false;
            }

            // Toujours essayer de vider le buffer, même si ce paquet n'a pas pu être ajouté
            // BurstPush gère l'envoi asynchrone des données bufferisées
            BurstPush();

            if (!allSent)
            {
                // BurstSend peut échouer si le buffer est plein - ce n'est pas une erreur fatale
                // Le paquet sera réessayé plus tard
                CLog.Debug("[LogonCommServer]", string.Format("Packet (opcode: {0}) queued for later send to {1} (buffer full).", op, GetRemoteIP()));
                return false;
            }
        }
        catch (Exception ex)
        {
            CLog.Error("[LogonCommServer]", string.Format("SendPacket error: {0}", ex.Message));
            // Ne pas appeler OnDisconnect() ici - laisser le timeout global gérer la déconnexion
            // Les exceptions peuvent être temporaires (ex: socket bloqué momentanément)
            return false;
        }
        finally
        {
            BurstEnd();
        }

        return true;
    }

    // Send packet directly without buffering (for PING/PONG packets)
    public bool SendPacketDirect(WorldPacket data, bool noCrypto = false)
    {
        // Vérifier que le socket est toujours connecté
        if (!IsConnected())
        {
            CLog.Error("[LogonCommServer]", string.Format("Cannot send packet: socket to {0} is disconnected.", GetRemoteIP()));
            return false;
        }

        try
        {
            // Build 6-byte header: 2 bytes opcode, 4 bytes size
            byte[] header = new byte[6];

            ushort op = (ushort)data.GetOpcode();
            uint size = (uint)data.Size;

            BlockCopy(BitConverter.GetBytes(op), 0, header, 0, 2);
            BlockCopy(BitConverter.GetBytes(size), 0, header, 2, 4);

            // Combine header and payload into a single buffer to send atomically
            byte[] packetData;
            if (useCrypto && !noCrypto)
            {
                // Need to encrypt header and payload separately, then combine
                var encHeader = new byte[6];
                sendCrypto.Process(header, encHeader);
                
                if (data.Size > 0)
                {
                    var payload = data.Contents;
                    var encPayload = new byte[payload.Length];
                    sendCrypto.Process(payload, encPayload);
                    
                    // Combine encrypted header + encrypted payload
                    packetData = new byte[6 + payload.Length];
                    BlockCopy(encHeader, 0, packetData, 0, 6);
                    BlockCopy(encPayload, 0, packetData, 6, payload.Length);
                }
                else
                {
                    packetData = encHeader;
                }
            }
            else
            {
                // No encryption: combine header + payload directly
                if (data.Size > 0)
                {
                    var payload = data.Contents;
                    packetData = new byte[6 + payload.Length];
                    BlockCopy(header, 0, packetData, 0, 6);
                    BlockCopy(payload, 0, packetData, 6, payload.Length);
                }
                else
                {
                    packetData = header;
                }
            }

            // Send the complete packet (header + payload) in a single atomic operation
            return SendPacketDirect(packetData, packetData.Length);
        }
        catch (Exception ex)
        {
            CLog.Error("[LogonCommServer]", string.Format("SendPacketDirect error: {0}", ex.Message));
            return false;
        }
    }

    public void HandleAuthChallenge(WorldPacket recvData)
    {
        var sLog = new Logger();
        byte[] key = new byte[20];

        try
        {
            uint result = 1;
            if (recvData.Size < 20)
            {
                sLog.OutError("[LogonCommServer]", "AuthChallenge: received packet too small (expected 20 bytes, got {0})", recvData.Size);
                OnDisconnect();
                return;
            }
            recvData.Read(key, 0, 20);

            // Debug: log received key      
            if (!key.SequenceEqual(SocketManager.sql_hash))
            {
                sLog.OutError(L_N_LOGCOMSE_2, GetRemoteIP(), "ECHEC");
                result = 0;
            }

            sLog.OutString(L_N_LOGCOMSE_3, GetRemoteIP(), result == 1 ? "OK" : "ECHEC");

            Logger.OutColor(LogColor.TNORMAL, L_N_LOGCOMSE_6);

            for (int i = 0; i < 20; ++i)
                Logger.OutColor(LogColor.TGREEN, $"{key[i]:X2} ");

            Logger.OutColor(LogColor.TNORMAL, "\n");

            Logger.OutColor(LogColor.TNORMAL, "Expected: ");
            for (int i = 0; i < 20; ++i)
                Logger.OutColor(LogColor.TGREEN, $"{SocketManager.sql_hash[i]:X2} ");

            Logger.OutColor(LogColor.TNORMAL, "\n");

            recvCrypto.Setup(key, 20);
            sendCrypto.Setup(key, 20);

            /* packets are encrypted from now on */
            useCrypto = true;

            WorldPacket data = new((ushort)RSMSG_AUTH_RESPONSE, 1);
            data.WriteByte((byte)result);

            SendPacket(data);

            authenticated = result;
        }
        finally
        {
            // ⚡ Nettoyage SÉCURISÉ de la clé en mémoire
            Array.Clear(key, 0, key.Length);
        }
    }

    public void SendPing()
    {
        CLog.Debug("[LogonCommServer]", string.Format("Sending PING to {0}", GetRemoteIP()));
        
        // PING packets must be sent directly without buffering to prevent timeout issues
        // when the buffer is full
        WorldPacket data = new((ushort)RSMSG_SERVER_PING, 4);
        data.WriteUInt32(0);
        
        if (!SendPacketDirect(data, false))
        {
            // Fallback: try normal buffered send if direct send fails
            bool success = SendPacket(data);
            if (!success)
            {
                CLog.Debug("[LogonCommServer]", string.Format("PING queued for later send to {0} (buffer full).", GetRemoteIP()));
            }
        }
        else
        {
            CLog.Debug("[LogonCommServer]", string.Format("PING sent directly to {0} (no buffer).", GetRemoteIP()));
        }
    }

    public void HandleServerPong(WorldPacket recvData)
    {
        // Accept and consume optional pong payload, then refresh liveness.
        if (recvData.Size >= 4)
        {
            _ = recvData.ReadUInt32();
        }

        CLog.Debug("[LogonCommServer]", string.Format("Received PONG from {0}. Connection is alive.", GetRemoteIP()));
        Interlocked.Exchange(ref lastPing_ms, Environment.TickCount64);
    }

    public static bool IsServerAllowed(IPAddress address)
    {
        if (!ServerTrustMe)
        {
            Monitor.Enter(m_allowedIpLock);
            try
            {
                foreach (var allowedIp in m_allowedIps)
                {
                    if (ParseCIDRBan(address, allowedIp.IP, (byte)allowedIp.Bytes))
                    {
                        return true;
                    }
                }
            }
            finally
            {
                Monitor.Exit(m_allowedIpLock);
            }
            return false;
        }
        else
        {
            return true;
        }
    }

    private static bool ParseCIDRBan(IPAddress ipAddress, uint mask, byte bytes)
    {
        byte[] addressBytes = ipAddress.GetAddressBytes();
        uint ip = BitConverter.ToUInt32(addressBytes, 0);
        uint subnetMask = uint.MaxValue << (32 - bytes);
        return (ip & subnetMask) == (mask & subnetMask);
    }

    private static ushort Swap16(ushort value)
    {
        return (ushort)((value >> 8) | (value << 8));
    }

    private static uint Swap32(uint value)
    {
        return ((value >> 24) & 0x000000FF) | ((value >> 8) & 0x0000FF00) | ((value << 8) & 0x00FF0000) | ((value << 24) & 0xFF000000);
    }

    // Activer les Keep-Alive TCP pour détecter les connexions mortes au niveau OS
    public void SetKeepAlive(bool enable, int timeMs, int intervalMs)
    {
        try
        {
            // Structure pour les valeurs Keep-Alive (Windows)
            uint on = enable ? 1u : 0u;
            byte[] inOptionValues = new byte[12];
            
            // Activer/désactiver Keep-Alive
            BitConverter.GetBytes(on).CopyTo(inOptionValues, 0);
            // Temps d'inactivité avant le premier Keep-Alive (ms)
            BitConverter.GetBytes((uint)timeMs).CopyTo(inOptionValues, 4);
            // Intervalle entre les Keep-Alive (ms)
            BitConverter.GetBytes((uint)intervalMs).CopyTo(inOptionValues, 8);
            
            // Appliquer les paramètres Keep-Alive via le socket sous-jacent
#pragma warning disable CA1416 // 'IOControlCode.KeepAliveValues' est spécifique à Windows
            GetSocket().IOControl(IOControlCode.KeepAliveValues, inOptionValues, null);
#pragma warning restore CA1416
            CLog.Debug("[LogonCommServer]", string.Format("TCP Keep-Alive enabled: {0}, time={1}ms, interval={2}ms", enable, timeMs, intervalMs));
        }
        catch (Exception ex)
        {
            CLog.Error("[LogonCommServer]", string.Format("Failed to set TCP Keep-Alive: {0}", ex.Message));
        }
    }

    // Initialize the handlers array
    private static logonpacket_handler[] Handlers;

    private void InitializeHandlers()
    {
        Handlers =
        [
            null,                                       // RMSG_NULL
            HandleRegister,                             // RCMSG_REGISTER_REALM
            null,                                       // RSMSG_REALM_REGISTERED
            HandleSessionRequest,                       // RCMSG_REQUEST_SESSION
            null,                                       // RSMSG_SESSION_RESULT
            HandlePing,                                 // RCMSG_PING
            null,                                       // RSMSG_PONG
            null,                                       // RCMSG_SQL_EXECUTE (Deprecated)
            null,                                       // RCMSG_RELOAD_ACCOUNTS (Deprecated)
            HandleAuthChallenge,                        // RCMSG_AUTH_CHALLENGE
            null,                                       // RSMSG_AUTH_RESPONSE
            null,                                       // RSMSG_REQUEST_ACCOUNT_CHARACTER_MAPPING
            HandleMappingReply,                         // RCMSG_ACCOUNT_CHARACTER_MAPPING_REPLY
            HandleUpdateMapping,                        // RCMSG_UPDATE_CHARACTER_MAPPING_COUNT
            null,                                       // RSMSG_DISCONNECT_ACCOUNT
            HandleTestConsoleLogin,                     // RCMSG_TEST_CONSOLE_LOGIN
            null,                                       // RSMSG_CONSOLE_LOGIN_RESULT
            HandleDatabaseModify,                       // RCMSG_MODIFY_DATABASE
            null,                                       // RSMSG_SERVER_PING
            HandleServerPong                            // RCMSG_SERVER_PONG
        ];
    }

    // Dummy methods for the missing handlers
    public void HandleMappingReply(WorldPacket recvData)
    {
        var sLog = new Logger();
        
        // Validation de la taille du paquet
        if (recvData.Size < 4)
        {
            CLog.Error("[LogonCommServer]", "Mapping reply too short");
            return;
        }
        
        uint realSize = recvData.ReadUInt32();
        
        // Limite de sécurité pour éviter les allocations excessives
        const uint MAX_MAPPING_SIZE = 10 * 1024 * 1024; // 10 Mo
        if (realSize > MAX_MAPPING_SIZE)
        {
            CLog.Error("[LogonCommServer]", 
                string.Format("Mapping reply too large: {0} bytes (max: {1})", realSize, MAX_MAPPING_SIZE));
            return;
        }
        
        // Extraire les données compressées
        int compressedSize = recvData.Size - 4;
        if (compressedSize <= 0)
        {
            CLog.Error("[LogonCommServer]", "No compressed data in mapping reply");
            return;
        }
        
        byte[] compressedData = new byte[compressedSize];
        recvData.Read(compressedData, 0, compressedSize);
        
        byte[] buffer;
        try
        {
            // Décompresser les données
            buffer = DecompressData(compressedData);
            
            // Vérifier que la taille décompressée correspond
            if (buffer.Length != realSize)
            {
                CLog.Error("[LogonCommServer]", 
                    string.Format("Decompressed size mismatch: expected {0}, got {1}", realSize, buffer.Length));
                return;
            }
        }
        catch (Exception ex)
        {
            CLog.Error("[LogonCommServer]", 
                string.Format("Decompression failed: {0}", ex.Message));
            return;
        }
        finally
        {
            Array.Clear(compressedData, 0, compressedData.Length);
        }

        if (buffer.Length < 8)
        {
            CLog.Error("[LogonCommServer]", "Mapping reply decompressed payload is too short");
            return;
        }

        uint accountId;
        byte numberOfCharacters;
        uint count;
        uint realmId = BitConverter.ToUInt32(buffer, 0);
        Realm realm = sInfoCore.GetRealm(realmId);
        if (realm == null)
        {
            CLog.Warning("[LogonCommServer]", 
                string.Format("Received mapping for unknown realm {0}", realmId));
            return;
        }

        lock (sInfoCore)
        {
            count = BitConverter.ToUInt32(buffer, 4);
            if (count > (buffer.Length - 8) / 5)
            {
                CLog.Error("[LogonCommServer]", 
                    string.Format("Mapping count is invalid: {0} entries for {1} bytes", count, buffer.Length));
                return;
            }

            sLog.OutString(L_N_LOGCOMSE_5, realmId, count);
            for (uint i = 0; i < count; ++i)
            {
                accountId = BitConverter.ToUInt32(buffer, (int)(8 + (i * 5)));
                numberOfCharacters = buffer[12 + (i * 5)];
                realm.CharacterMap[accountId] = numberOfCharacters;
            }
        }
    }
    
    private static byte[] DecompressData(byte[] compressedData)
    {
        using var ms = new MemoryStream(compressedData);
        using var ds = new DeflateStream(ms, CompressionMode.Decompress);
        using var output = new MemoryStream();
        
        ds.CopyTo(output);
        return output.ToArray();
    }

    public void HandleUpdateMapping(WorldPacket recvData)
    {
        if (recvData.Size < 9) // 4 + 4 + 1
        {
            CLog.Error("[LogonCommServer]"," HandleUpdateMapping: packet too small (expected 9 bytes, got {0})", recvData.Size);
            return;
        }
        uint realmId = recvData.ReadUInt32();
        uint accountId = recvData.ReadUInt32();
        byte charsToAdd = recvData.ReadByte();

        Realm realm = sInfoCore.GetRealm(realmId);
        if (realm == null)
            return;

        lock (sInfoCore)
        {
            if (realm.CharacterMap.ContainsKey(accountId))
            {
                realm.CharacterMap[accountId] += charsToAdd;
            }
            else
            {
                realm.CharacterMap[accountId] = charsToAdd;
            }
        }
    }

    public void HandleTestConsoleLogin(WorldPacket recvData)
    {
        var sLog = new Logger();
        WorldPacket data = new((ushort)RSMSG_CONSOLE_LOGIN_RESULT, 8);
        uint request = recvData.ReadUInt32();
        string accountName = recvData.ReadString();
        byte[] key = new byte[20];
        recvData.Read(key, 0, 20);
        sLog.OutDebug(L_D_LOGCOMSE_L, accountName);


        data.WriteUInt32(request);

        Account account = AccountMgr.GetAccount(accountName);
        if (account == null)
        {
            data.WriteUInt32(0);
            SendPacket(data);
            return;
        }

        if (account.GMFlags == null || !account.GMFlags.Contains("255:"))
        {
            sLog.OutError(L_E_LOGCOMSE_R, account.UsernamePtr, account.GMFlags);
            data.WriteUInt32(0);
            SendPacket(data);
            return;
        }

        data.WriteUInt32(1);
        SendPacket(data);
    }

    public void HandleDatabaseModify(WorldPacket recvData)
    {
        uint method = recvData.ReadUInt32();
        var IPBanner = new IPBanner();

        if (!IsServerAllowed(GetRemoteAddress(this)))
        {
            CLog.Error("[LogonCommServer]", L_E_LOGCOMSE_L_1, method, GetRemoteIP());
            return;
        }

        switch (method)
        {
            case 1:
                {
                    string account = recvData.ReadString();
                    uint duration = recvData.ReadUInt32();
                    account = account.ToUpper();

                    Account acct = AccountMgr.GetAccount(account);
                    if (acct == null)
                        return;

                    acct.Banned = duration;
                    SLogonSQL.Execute($"UPDATE accounts SET banned = {duration} WHERE login = '{SLogonSQL.EscapeString(account)}'");
                }
                break;

            case 2:
                {
                    string account = recvData.ReadString();
                    string gm = recvData.ReadString();
                    account = account.ToUpper();

                    Account acct = AccountMgr.GetAccount(account);
                    if (acct == null)
                        return;

                    acct.SetGMFlags(account);
                    SLogonSQL.Execute($"UPDATE accounts SET gm = '{SLogonSQL.EscapeString(gm)}' WHERE login = '{SLogonSQL.EscapeString(account)}'");
                }
                break;

            case 3:
                {
                    string account = recvData.ReadString();
                    uint duration = recvData.ReadUInt32();
                    account = account.ToUpper();

                    Account acct = AccountMgr.GetAccount(account);
                    if (acct == null)
                        return;

                    acct.Muted = duration;
                    SLogonSQL.Execute($"UPDATE accounts SET muted = {duration} WHERE login = '{SLogonSQL.EscapeString(account)}'");
                }
                break;

            case 4:
                {
                    string ip = recvData.ReadString();
                    uint duration = recvData.ReadUInt32();

                    if (IPBanner.Add(ip, duration))
                        SLogonSQL.Execute($"INSERT INTO ipbans VALUES('{SLogonSQL.EscapeString(ip)}', {duration})");
                }
                break;

            case 5:
                {
                    string ip = recvData.ReadString();

                    if (IPBanner.Remove(ip))
                        SLogonSQL.Execute($"DELETE FROM ipbans WHERE ip = '{SLogonSQL.EscapeString(ip)}'");
                }
                break;

            case 6:
                {
                    uint guid = recvData.ReadUInt32();
                    uint oneDKCreated = recvData.ReadUInt32();

                    if (oneDKCreated == 1)
                        SLogonSQL.Execute($"UPDATE `accounts`,`characters` SET `AlreadyDK` = {guid} WHERE `accounts`.`acct` = `characters`.`acct` AND `characters`.`guid` = {guid}");
                    else
                        SLogonSQL.Execute($"UPDATE `accounts` SET `AlreadyDK` = 0 WHERE `AlreadyDK` = {guid}");
                }
                break;
        }
    }
}

// Classe statique pour surveiller toutes les connexions RealmServer
public static class ConnectionWatchdog
{
    private static Timer watchdogTimer;
    private static readonly object startLock = new object();
    private static bool isRunning = false;

    // Démarrer le watchdog (appelé une seule fois)
    public static void Start()
    {
        lock (startLock)
        {
            if (isRunning) return;
            isRunning = true;
            watchdogTimer = new Timer(CheckAllConnections, null, 10_000, 10_000); // Toutes les 10 secondes
            CLog.Notice("[ConnectionWatchdog]", "Started. Monitoring all RealmServer connections every 10 seconds.");
        }
    }

    // Arrêter le watchdog
    public static void Stop()
    {
        lock (startLock)
        {
            if (!isRunning) return;
            isRunning = false;
            if (watchdogTimer != null)
            {
                try
                {
                    watchdogTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    watchdogTimer.Dispose();
                }
                catch { }
                watchdogTimer = null;
            }
            CLog.Notice("[ConnectionWatchdog]", "Stopped.");
        }
    }

    // Vérifier toutes les connexions actives
    private static void CheckAllConnections(object state)
    {
        try
        {
            var sInfoCore = InformationCore.Instance;
            var sockets = sInfoCore.GetAllServerSockets();
            long now_ms = Environment.TickCount64;
            
            foreach (var socket in sockets)
            {
                if (socket == null) continue;
                
                // Vérifier si le socket est encore valide
                if (!socket.IsConnected())
                {
                    CLog.Warning("[ConnectionWatchdog]",
                        string.Format("Socket to {0} is disconnected. Removing.", socket.GetRemoteIP()));
                    socket.OnDisconnect();
                    continue;
                }
                
                long lastPing = Interlocked.Read(ref socket.lastPing_ms);
                long timeSinceLastPong = now_ms - lastPing;
                
                // Si pas de PONG reçu depuis 60 secondes, forcer la déconnexion
                if (timeSinceLastPong > 60_000)
                {
                    CLog.Error("[ConnectionWatchdog]",
                        string.Format("Socket to {0} is dead (last PONG: {1}ms ago). Forcing disconnect.",
                        socket.GetRemoteIP(), timeSinceLastPong));
                    socket.OnDisconnect();
                }
            }
        }
        catch (Exception ex)
        {
            CLog.Error("[ConnectionWatchdog]", string.Format("Error in connection check: {0}", ex.Message));
        }
    }
}

public delegate void logonpacket_handler(WorldPacket packet);
