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
using System.Text;
using System.Threading;

using WaadShared;
using WaadShared.Network;

using static System.Buffer;
using static LogonServer.RealmListOpcode;



namespace LogonServer;

public class LogonPacket
{
    public ushort Opcode;
    public uint Size;
}

public class LogonCommServerSocket
{
    private readonly LogonCommServerSocket socket;
    public readonly Socket Socket = new();
    private readonly AccountMgr AccountMgr = new();
    public uint lastPing;
    private uint nextServerPing;
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
    private static readonly bool ServerTrustMe = false;

    // Constructeur sans paramètre public
    public LogonCommServerSocket()
    {
        socket = null;
        lastPing = (uint)DateTime.UtcNow.Ticks;
        nextServerPing = lastPing + 30;
        remaining = opcode = 0;
        useCrypto = false;
        authenticated = 0;
        InitializeHandlers(); // Appel de la méthode pour initialiser les gestionnaires
    }

    public LogonCommServerSocket(Socket fd)
    {
        socket = fd;
        lastPing = (uint)DateTime.UtcNow.Ticks;
        nextServerPing = lastPing + 30;
        remaining = opcode = 0;
        removed = true;
        useCrypto = false;
        authenticated = 0;
        InitializeHandlers(); // Appel de la méthode pour initialiser les gestionnaires
    }

    public void OnConnect()
    {
        if (!IsServerAllowed(Socket.GetRemoteAddress(Socket)))
        {
            Console.WriteLine($"Server {Socket.GetRemoteIP()} is not allowed.");
            Socket.Disconnect();
            return;
        }

        sInfoCore.AddServerSocket(socket);
        removed = false;
    }

    public void OnDisconnect()
    {
        if (!removed)
        {
            foreach (var id in serverIds)
            {
                sInfoCore.RemoveRealm(id);
            }
            sInfoCore.RemoveServerSocket(socket);
        }
    }

    public void OnRead(AuthSocket authSocket)
    {
        while (true)
        {
            if (remaining == 0)
            {
                if (Socket.GetReadBuffer().Length < 6)
                    return;

                byte[] buffer = new byte[6];
                authSocket.socket.Receive(buffer);

                opcode = BitConverter.ToUInt16(buffer, 0);
                remaining = BitConverter.ToUInt32(buffer, 2);

                if (useCrypto)
                {
                    recvCrypto.Process(buffer, new byte[6]);
                }

                if (!BitConverter.IsLittleEndian)
                {
                    opcode = Swap16(opcode);
                }
                else
                {
                    remaining = Swap32(remaining);
                }

                if (remaining > 65535) // Prevent overly large packets
                {
                    Console.WriteLine("Packet size exceeds maximum allowed size.");
                    Socket.Disconnect();
                    return;
                }
            }

            if (Socket.GetReadBuffer().Length < remaining)
                return;

            byte[] packetBuffer = new byte[remaining];
            authSocket.socket.Receive(packetBuffer);

            WorldPacket buff = new(opcode, (int)remaining);
            if (remaining > 0)
            {
                buff.Resize((int)remaining);
                BlockCopy(packetBuffer, 0, buff.Contents, 0, (int)remaining);
            }

            if (useCrypto && remaining > 0)
            {
                recvCrypto.Process(buff.Contents, packetBuffer);
            }

            HandlePacket(buff);
            remaining = 0;
            opcode = 0;
        }
    }

    public void HandlePacket(WorldPacket recvData)
    {
        if (authenticated == 0 && recvData.Opcode != (ushort)RCMSG_AUTH_CHALLENGE)
        {
            Socket.Disconnect();
            return;
        }

        logonpacket_handler[] Handlers = new logonpacket_handler[(int)RMSG_COUNT];

        if (recvData.Opcode >= (ushort)RMSG_COUNT || Handlers[recvData.Opcode] == null)
        {
            Console.WriteLine($"Invalid opcode: {recvData.Opcode}");
            return;
        }

        Handlers[recvData.Opcode](recvData);
    }

    public void HandleRegister(WorldPacket recvData)
    {
        Realm realm = new();
        sInfoCore.AddServerSocket(socket);
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

        Console.WriteLine($"Realm {realm.Name} registered with ID {myId}");

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

    public void SendPacket(WorldPacket data)
    {
        bool rv;
        Socket.BurstBegin();

        LogonPacket header = new()
        {
            Opcode = data.Opcode,
            Size = (uint)data.Size
        };

        if (useCrypto)
        {
            sendCrypto.Process(BitConverter.GetBytes(header.Opcode), new byte[2]);
            sendCrypto.Process(BitConverter.GetBytes(header.Size), new byte[4]);
        }

        rv = Socket.BurstSend(BitConverter.GetBytes(header.Opcode), 2);
        rv = Socket.BurstSend(BitConverter.GetBytes(header.Size), 4);

        if (data.Size > 0 && rv)
        {
            if (useCrypto)
            {
                sendCrypto.Process(data.Contents, new byte[data.Size]);
            }

            rv = Socket.BurstSend(data.Contents, data.Size);
        }

        if (rv) AuthSocket.BurstPush();
        Socket.BurstEnd();
    }

    public void HandleAuthChallenge(WorldPacket recvData)
    {
        byte[] key = new byte[20];

        uint result = 1;
        recvData.Read(key, 0, 20);

        if (!key.SequenceEqual(LogonServer.sql_hash))
        {
            result = 0;
        }

        Console.WriteLine($"Auth challenge from {Socket.GetRemoteIP()} {(result == 1 ? "OK" : "FAILED")}");

        recvCrypto.Setup(key);
        sendCrypto.Setup(key);

        useCrypto = true;

        WorldPacket data = new((ushort)RSMSG_AUTH_RESPONSE, 1);
        data.WriteUInt32(result);
        SendPacket(data);

        authenticated = (byte)result;
    }

    public void SendPing()
    {
        nextServerPing = (uint)DateTime.UtcNow.Ticks + 20;
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

    public static implicit operator LogonCommServerSocket(Socket v)
    {
        return new LogonCommServerSocket(v);
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
                HandleServerPong,                           // RCMSG_SERVER_PONG
        ];
    }

    // Dummy methods for the missing handlers
    public void HandleMappingReply(WorldPacket recvData)
    {
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
            Console.WriteLine($"Realm ID: {realmId}, Count: {count}");
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
        WorldPacket data = new((ushort)RSMSG_CONSOLE_LOGIN_RESULT, 8);
        uint request = recvData.ReadUInt32();
        string accountName = recvData.ReadString();
        byte[] key = new byte[20];
        recvData.Read(key, 0, 20);
        Console.WriteLine($"Account: {accountName}");

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
            Console.WriteLine($"Account: {account.UsernamePtr}, GMFlags: {account.GMFlags}");
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

        if (!IsServerAllowed(Socket.GetRemoteAddress(Socket)))
        {
            Console.WriteLine($"Method: {method}, IP: {Socket.GetRemoteIP()}");
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
