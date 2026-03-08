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

public class LogonCommServerSocket : WaadShared.Network.Socket
{
    private readonly AccountMgr AccountMgr = new();
    public uint lastPing;
    private uint nextServerPing;
    private Timer pingTimer;
    private uint remaining;
    private ushort opcode;
    private bool removed;
    private bool useCrypto;
    private uint authenticated;
    private readonly RC4Engine sendCrypto = new();
    private readonly RC4Engine recvCrypto = new();
    public readonly HashSet<uint> serverIds = [];
    private readonly InformationCore sInfoCore = new();
    private static readonly List<AllowedIP> m_allowedIps = [];
    private static readonly object m_allowedIpLock = new();
    private static readonly bool ServerTrustMe = true;

    // Constructeur sans paramètre public
    public LogonCommServerSocket() : base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    {
        lastPing = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        nextServerPing = lastPing + 20;
        remaining = opcode = 0;
        useCrypto = false;
        authenticated = 0;
        InitializeHandlers();
        pingTimer = new Timer(PingTimerCallback, null, 20000, 20000); // 20s interval
    }

    public LogonCommServerSocket(Socket socket) : base(socket, 1024, 1024)
    {
        lastPing = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        nextServerPing = lastPing + 20;
        remaining = opcode = 0;
        removed = true;
        useCrypto = false;
        authenticated = 0;
        InitializeHandlers();
        pingTimer = new Timer(PingTimerCallback, null, 20000, 20000); // 20s interval
        // NOTE: DO NOT call OnConnect() here - ListenSocket.SetConnected() will handle it
    }

    private void PingTimerCallback(object state)
    {
        uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now >= nextServerPing && IsConnected())
        {
            SendPing();
            nextServerPing = now + 20;
        }
    }

    public override void OnDisconnect()
    {
        // Arrêt du timer de ping pour éviter les fuites de ressources
        pingTimer?.Dispose();
        pingTimer = null;
        if (!removed)
        {
            foreach (var id in serverIds)
            {
                sInfoCore.RemoveRealm(id);
            }
            sInfoCore.RemoveServerSocket(this);
        }

        // Call base to ensure proper cleanup in parent class
        base.OnDisconnect();
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
        removed = false;
    }

    public override void OnRead()
    {
        while (true)
        {
            if (remaining == 0)
            {
                if (GetReadBuffer().GetSize() < 6)
                    return;

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

                // Handle endianness
                if (!BitConverter.IsLittleEndian)
                {
                    opcode = Swap16(opcode);
                }

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
            }

            WorldPacket buff = new(opcode, (int)remaining);
            if (remaining > 0)
            {
                buff.Append(packetData, (int)remaining);
            }

            // Handle the packet
            HandlePacket(buff);

            remaining = 0;
            opcode = 0;
        }
    }

    public void HandlePacket(WorldPacket recvData)
    {
        CLog.Debug("[LogonCommServer]", $"HandlePacket called with opcode: {recvData.Opcode}");
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
    }

    public void HandleRegister(WorldPacket recvData)
    {
        var sLog = new Logger();
        sLog.OutString("HandleRegister called");
        Realm realm = new();
        sInfoCore.AddServerSocket(this);
        removed = false;

        realm.Name = recvData.ReadString();
        realm.Address = recvData.ReadString();
        realm.Colour = recvData.ReadUInt32();
        realm.Icon = recvData.ReadUInt32();
        realm.TimeZone = recvData.ReadUInt32();
        realm.Population = recvData.ReadUInt32();

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
        var AccountMgr = new AccountMgr();
        uint requestId = recvData.ReadUInt32();
        string accountName = recvData.ReadString();
        Account acct = AccountMgr.GetAccount(accountName);
        byte[] localeBytes = Encoding.UTF8.GetBytes(acct.Locale);

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
            data.WriteUInt32(acct.AccountId);
            data.WriteString(acct.UsernamePtr);
            data.WriteByte(Convert.ToByte(acct.GMFlags));
            data.WriteUInt32(acct.AccountFlags);
            data.Write(acct.SessionKey, 0, 40);
            data.Write(localeBytes, 0, localeBytes.Length);
            data.WriteUInt32(acct.Muted);
        }

        SendPacket(data);
    }

    public void HandlePing(WorldPacket recvData)
    {
        recvData = new((ushort)RSMSG_PONG, 4);
        SendPacket(recvData);
        lastPing = (uint)DateTime.UtcNow.Ticks;
    }

    public void SendPacket(WorldPacket data, bool noCrypto = false)
    {
        bool rv;
        BurstBegin();

        // Build 6-byte header: 2 bytes opcode, 4 bytes size
        byte[] header = new byte[6];

        ushort op = (ushort)data.GetOpcode();
        uint size = (uint)data.Size;

        if (!BitConverter.IsLittleEndian)
        {
            // On big-endian machines, swap opcode to network order
            op = Swap16(op);
            // leave size as-is
        }
        else
        {
            // On little-endian machines, send size in network order
            size = Swap32(size);
        }

        BlockCopy(BitConverter.GetBytes(op), 0, header, 0, 2);
        BlockCopy(BitConverter.GetBytes(size), 0, header, 2, 4);

        // Encrypt header into temporary buffer if needed (avoid in-place)
        if (useCrypto && !noCrypto)
        {
            var encHeader = new byte[6];
            sendCrypto.Process(header, encHeader);
            rv = BurstSend(encHeader, 6);
        }
        else
        {
            rv = BurstSend(header, 6);
        }

        if (data.Size > 0 && rv)
        {
            var payload = data.Contents; // get contents once

            if (useCrypto && !noCrypto)
            {
                var encPayload = new byte[payload.Length];
                sendCrypto.Process(payload, encPayload);
                rv = BurstSend(encPayload, data.Size);
            }
            else
            {
                rv = BurstSend(payload, data.Size);
            }
        }

        if (rv) BurstPush();
        BurstEnd();
    }

    public void HandleAuthChallenge(WorldPacket recvData)
    {
        var sLog = new Logger();
        byte[] key = new byte[20];

        uint result = 1;
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

    public void SendPing()
    {
    WorldPacket data = new((ushort)RSMSG_SERVER_PING, 4);
    data.WriteUInt32(0);
    SendPacket(data);
    }

    public static void HandleServerPong(WorldPacket recvData)
    {
        // Nothing to do
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
        uint realSize = recvData.ReadUInt32();
        byte[] buffer = new byte[realSize];
        Array.Resize(ref buffer, (int)realSize);

        using (var ms = new MemoryStream(recvData.Contents, 4, recvData.Size - 4))
        using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
        {
            ds.Read(buffer, 0, buffer.Length);
        }

        uint accountId;
        byte numberOfCharacters;
        uint count;
        uint realmId = BitConverter.ToUInt32(buffer, 0);
        Realm realm = sInfoCore.GetRealm(realmId);
        if (realm == null)
            return;

        lock (sInfoCore)
        {
            count = BitConverter.ToUInt32(buffer, 4);
            sLog.OutString(L_N_LOGCOMSE_5, realmId, count);
            for (uint i = 0; i < count; ++i)
            {
                accountId = BitConverter.ToUInt32(buffer, (int)(8 + (i * 5)));
                numberOfCharacters = buffer[12 + (i * 5)];
                if (realm.CharacterMap.ContainsKey(accountId))
                {
                    realm.CharacterMap[accountId] = numberOfCharacters;
                }
                else
                {
                    realm.CharacterMap[accountId] = numberOfCharacters;
                }
            }
        }
    }

    public void HandleUpdateMapping(WorldPacket recvData)
    {
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

public delegate void logonpacket_handler(WorldPacket packet);
