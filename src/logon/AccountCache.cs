/*
 * Ascent MMORPG Server
 * Copyright (C) 2005-2008 Ascent Team <http://www.ascentcommunity.com/>
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
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using WaadShared;
using WaadShared.Auth;
using WaadShared.Config;
using WaadShared.Network;

using static WaadShared.Common;
using static WaadShared.AccountCache;

namespace LogonServer;

public enum RealmColours
{
    REALMCOLOUR_GREEN = 0,
    REALMCOLOUR_RED = 1,
    REALMCOLOUR_OFFLINE = 2,
    REALMCOLOUR_BLUE = 3,
}

public class Account
{
    public uint AccountId { get; set; }
    public string GMFlags { get; set; }
    public byte AccountFlags { get; set; }
    public uint Banned { get; set; }
    public byte[] SessionKey { get; set; }
    public string UsernamePtr { get; set; }
    public uint Muted { get; set; }
    public uint OneDkCreated { get; set; }
    public BigNumber Salt { get; set; }
    public BigNumber Verifier { get; set; }
    public char[] Locale { get; set; }
    public bool ForcedLocale { get; set; }

    public Account()
    {
        AccountId = 0;
        ForcedLocale = false;
        Locale = new char[4];
        UsernamePtr = null;
        GMFlags = null;
        AccountFlags = 0;
        Banned = 0;
        Muted = 0;
        OneDkCreated = 0;
        SessionKey = null;
    }

    ~Account()
    {
        if (GMFlags != null)
            GMFlags = null;
        if (SessionKey != null)
            SessionKey = null;
    }

    public void SetGMFlags(string flags)
    {
        if (GMFlags != null)
            GMFlags = null;

        if (string.IsNullOrEmpty(flags) || (flags.Length == 1 && flags[0] == '0'))
        {
            GMFlags = null;
            return;
        }

        GMFlags = flags;
    }

    public void SetSessionKey(byte[] key)
    {
        SessionKey ??= new byte[40];
        Array.Copy(key, SessionKey, 40);
    }
}

public class IPBan
{
    public uint Mask { get; set; }
    public byte Bytes { get; set; }
    public uint Expire { get; set; }
    public string DbIp { get; set; }
}

public enum BAN_STATUS
{
    BAN_STATUS_NOT_BANNED = 0,
    BAN_STATUS_TIME_LEFT_ON_BAN = 1,
    BAN_STATUS_PERMANENT_BAN = 2,
}

public class IPBanner
{
    private static IPBanner _instance;
    public static IPBanner Instance => _instance ??= new IPBanner();

    private readonly List<IPBan> banList = [];
    private readonly object listBusy = new();

    public static void Reload()
    {
        var Logger = new Logger();
        var banList = new List<IPBan>();

        lock (Instance.listBusy)
        {
            Instance.banList.Clear();
            var result = SLogonSQL.Query("SELECT ip, expire FROM ipbans");
            if (result != null)
            {
                foreach (var row in result)
                {
                    var ipb = new IPBan();
                    string smask = "32";
                    string ip = Field.GetString(row[0]);
                    int i = ip.IndexOf('/');
                    string stmp = ip[..i];
                    if (i == -1)
                    {
                        Logger.OutString(L_P_ACCOUNT_I, ip);
                    }
                    else
                    {
                        smask = ip[(i + 1)..];
                    }

                    uint ipraw = MakeIP(stmp);
                    uint ipmask = uint.Parse(smask);
                    if (ipraw == 0 || ipmask == 0)
                    {
                        Logger.OutString(L_P_ACCOUNT_I_1, ip);
                        continue;
                    }

                    ipb.Bytes = (byte)ipmask;
                    ipb.Mask = ipraw;
                    ipb.Expire = Field.GetUInt32(row[1]);
                    ipb.DbIp = ip;
                    banList.Add(ipb);
                }
            }
        }
    }

    private static uint MakeIP(string ip)
    {
        var segments = ip.Split('.');
        if (segments.Length != 4)
            return 0;

        return (uint)(int.Parse(segments[0]) << 24 |
                      int.Parse(segments[1]) << 16 |
                      int.Parse(segments[2]) << 8 |
                      int.Parse(segments[3]));
    }

    public bool Add(string ip, uint dur)
    {
        var sip = ip;
        int i = sip.IndexOf('/');
        if (i == -1)
            return false;

        var stmp = sip[..i];
        var smask = sip[(i + 1)..];

        uint ipraw = MakeIP(stmp);
        uint ipmask = uint.Parse(smask);
        if (ipraw == 0 || ipmask == 0)
            return false;

        var ipb = new IPBan
        {
            DbIp = sip,
            Bytes = (byte)ipmask,
            Mask = ipraw,
            Expire = dur
        };

        lock (listBusy)
        {
            banList.Add(ipb);
        }

        return true;
    }

    public bool Remove(string ip)
    {
        lock (listBusy)
        {
            var ipBan = banList.FirstOrDefault(b => b.DbIp == ip);
            if (ipBan != null)
            {
                banList.Remove(ipBan);
                return true;
            }
        }
        return false;
    }

    public BAN_STATUS CalculateBanStatus(IPAddress ipAddress)
    {
        lock (listBusy)
        {
            foreach (var ipBan in banList.ToList())
            {
                if (ParseCIDRBan(ipAddress, ipBan.Mask, ipBan.Bytes))
                {
                    if (ipBan.Expire == 0)
                        return BAN_STATUS.BAN_STATUS_PERMANENT_BAN;

                    if (UNIXTIME.Value >= ipBan.Expire)
                    {
                        string deleteQuery = "DELETE FROM ipbans WHERE expire = @Expire AND ip = @Ip";
                        var parameters = new Dictionary<string, object>
                    {
                        { "@Expire", ipBan.Expire },
                        { "@Ip", SLogonSQL.EscapeString(ipBan.DbIp) }
                    };

                        SLogonSQL.Execute(deleteQuery, parameters);
                        banList.Remove(ipBan);
                    }
                    else
                    {
                        return BAN_STATUS.BAN_STATUS_TIME_LEFT_ON_BAN;
                    }
                }
            }
        }

        return BAN_STATUS.BAN_STATUS_NOT_BANNED;
    }

    private static bool ParseCIDRBan(IPAddress ipAddress, uint mask, byte bytes)
    {
        byte[] addressBytes = ipAddress.GetAddressBytes();
        uint ip = BitConverter.ToUInt32(addressBytes, 0);
        uint subnetMask = uint.MaxValue << (32 - bytes);
        return (ip & subnetMask) == (mask & subnetMask);
    }
}

public class AccountMgr
{
    private static AccountMgr _instance;
    public static AccountMgr Instance => _instance ??= new AccountMgr();
    public static AccountMgr GetSingleton() => new();
    private readonly Dictionary<string, Account> AccountDatabase = [];
    private readonly object setBusy = new();

    public void AddAccount(Field[] field)
    {
        var sLog = new Logger();
        var acct = new Account
        {
            AccountId = Field.GetUInt32(field[0]),
            AccountFlags = Field.GetUInt8(field[3]),
            Banned = Field.GetUInt32(field[4]),
            GMFlags = Field.GetString(field[2]),
            Salt = new BigNumber(Field.GetString(field[7]) ?? ""),
            Verifier = new BigNumber(Field.GetString(field[8]) ?? "")
        };

        if (UNIXTIME.Value > acct.Banned && acct.Banned != 0 && acct.Banned != 1)
        {
            acct.Banned = 0;
            sLog.OutString(L_D_ACCOUNT_B, acct.UsernamePtr);

            string updateQuery = "UPDATE accounts SET banned = 0 WHERE acct = @AccountId";
            var parameters = new Dictionary<string, object>
            {
                { "@AccountId", acct.AccountId }
            };

            SLogonSQL.Execute(updateQuery, parameters);
        }

        acct.SetGMFlags(acct.GMFlags);
        acct.Locale = "enUS".ToCharArray();

        if (Field.GetString(field[5]) != "enUS")
        {
            acct.Locale = Field.GetString(field[5]).ToCharArray();
            acct.ForcedLocale = true;
        }
        else
        {
            acct.ForcedLocale = false;
        }

        acct.Muted = Field.GetUInt32(field[6]);
        if (UNIXTIME.Value > acct.Muted && acct.Muted != 0 && acct.Muted != 1)
        {
            acct.Muted = 0;
            string updateMutedQuery = "UPDATE accounts SET muted = 0 WHERE acct = @AccountId";
            var mutedParameters = new Dictionary<string, object>
            {
                { "@AccountId", acct.AccountId }
            };

            SLogonSQL.Execute(updateMutedQuery, mutedParameters);
        }

        AccountDatabase[acct.UsernamePtr] = acct;
    }

    public Account GetAccount(string name)
    {
        lock (setBusy)
        {
            AccountDatabase.TryGetValue(name.ToUpper(), out var account);
            return account;
        }
    }

    public static void UpdateAccount(Account acct, Field[] field)
    {
        uint id = Field.GetUInt32(field[0]);
        string username = Field.GetString(field[1]);
        string gmFlags = Field.GetString(field[2]);
        string salt = Field.GetString(field[7]) ?? "";
        string verifier = Field.GetString(field[8]) ?? "";
        var Logger = new Logger();

        if (id != acct.AccountId)
        {
            Logger.OutColor(LogColor.TYELLOW, L_W_ACCOUNT_F, id, username);
            Logger.OutColor(LogColor.TNORMAL, "\n");

            string deleteQuery = "DELETE FROM accounts WHERE acct = @AccountId";
            var deleteParameters = new Dictionary<string, object>
            {
                { "@AccountId", id }
            };

            SLogonSQL.Execute(deleteQuery, deleteParameters);
            return;
        }

        acct.AccountId = Field.GetUInt32(field[0]);
        acct.AccountFlags = Field.GetUInt8(field[3]);
        acct.Banned = Field.GetUInt32(field[4]);

        if (!string.IsNullOrEmpty(salt))
        {
            acct.Salt = new BigNumber(salt);
        }

        if (!string.IsNullOrEmpty(verifier))
        {
            acct.Verifier = new BigNumber(verifier);
        }

        if (UNIXTIME.Value > acct.Banned && acct.Banned != 0 && acct.Banned != 1)
        {
            acct.Banned = 0;
            Logger.OutString(L_D_ACCOUNT_B, acct.UsernamePtr);

            string updateQuery = "UPDATE accounts SET banned = 0 WHERE acct = @AccountId";
            var parameters = new Dictionary<string, object>
            {
                { "@AccountId", acct.AccountId }
            };

            SLogonSQL.Execute(updateQuery, parameters);
        }

        acct.SetGMFlags(gmFlags);
        if (Field.GetString(field[5]) != "enUS")
        {
            acct.Locale = Field.GetString(field[5]).ToCharArray();
            acct.ForcedLocale = true;
        }
        else
        {
            acct.ForcedLocale = false;
        }

        acct.Muted = Field.GetUInt32(field[6]);
        if (UNIXTIME.Value > acct.Muted && acct.Muted != 0 && acct.Muted != 1)
        {
            acct.Muted = 0;
            Logger.OutString(L_D_ACCOUNT_M, acct.UsernamePtr);

            string updateMutedQuery = "UPDATE accounts SET muted = 0 WHERE acct = @AccountId";
            var mutedParameters = new Dictionary<string, object>
            {
                { "@AccountId", acct.AccountId }
            };

            SLogonSQL.Execute(updateMutedQuery, mutedParameters);
        }

        username = username.ToUpper();
    }

    public void ReloadAccounts(bool silent)
    {
        var Logger = new Logger();
        lock (setBusy)
        {
            if (!silent) Logger.OutString(L_N_ACCOUNT);

            var result = SLogonSQL.Query("SELECT a.acct, a.login, a.gm, a.flags, a.banned, a.forceLanguage, a.muted, ad.salt, ad.verifier FROM accounts a LEFT JOIN account_data ad ON a.acct = ad.acct");
            var accountList = new HashSet<string>();

            if (result != null)
            {
                foreach (var field in result)
                {
                    string accountName = Field.GetString(field[1]).ToUpper();
                    var acct = GetAccount(accountName);

                    if (acct == null)
                    {
                        AddAccount(field);
                    }
                    else
                    {
                        UpdateAccount(acct, field);
                    }

                    accountList.Add(accountName);
                }
            }

            foreach (var kvp in AccountDatabase.ToList())
            {
                if (!accountList.Contains(kvp.Key))
                {
                    AccountDatabase.Remove(kvp.Key);
                }
                else
                {
                    kvp.Value.UsernamePtr = kvp.Key;
                }
            }

            if (!silent) Logger.OutString(L_N_ACCOUNT_F, AccountDatabase.Count);
        }

        IPBanner.Reload();
    }

    public void ReloadAccountsCallback()
    {
        ReloadAccounts(true);
    }

    public int GetCount()
    {
        return AccountDatabase.Count;
    }

    internal static void CloseSocket()
    {
        var Socket = new Socket();
        Socket.Disconnect();
    }
}

public class Realm
{
    public string Name { get; set; }
    public string Address { get; set; }
    public uint Colour { get; set; }
    public uint Icon { get; set; }
    public uint TimeZone { get; set; }
    public float Population { get; set; }
    public Dictionary<uint, byte> CharacterMap { get; set; } = [];
}

public class InformationCore
{
    public void AddServerSocket(LogonCommServerSocket socket)
    {
        lock (serverSocketLock)
        {
            serverSockets.Add(socket);
        }
    }

    public void RemoveServerSocket(LogonCommServerSocket socket)
    {
        lock (serverSocketLock)
        {
            serverSockets.Remove(socket);
        }
    }

    private static InformationCore _instance;
    public static InformationCore Instance => _instance ??= new InformationCore();

    private readonly Dictionary<uint, Realm> realms = [];
    private readonly HashSet<LogonCommServerSocket> serverSockets = [];
    private readonly object serverSocketLock = new();
    private readonly object realmLock = new();

    private uint realmhigh;
    private readonly bool usepings;

    public InformationCore()
    {
        realmhigh = 0;
        var mainConfig = new ConfigMgr();
        usepings = !mainConfig.MainConfig.GetBoolean("LogonServer", "DisablePings");
    }

    public Realm GetRealm(uint realmId)
    {
        lock (realmLock)
        {
            realms.TryGetValue(realmId, out var realm);
            return realm;
        }
    }

    public void RemoveRealm(uint realmId)
    {
        var sLog = new Logger();
        lock (realmLock)
        {
            if (realms.TryGetValue(realmId, out var realm))
            {
                sLog.OutString("Removing realm `{0}` ({1}) due to socket close.", realm.Name, realmId);
                realms.Remove(realmId);
            }
        }
    }

    public void SendRealms()
    {
        lock (realmLock)
        {
            var data = new ByteBuffer(realms.Count * 150 + 20);
            var Socket = new Socket();

            data.Write(0x10);
            data.Write(0); // Size Placeholder

            data.Write(0); // Unknown value

            data.Write((ushort)realms.Count);

            foreach (var realm in realms.Values)
            {
                data.Write((byte)realm.Icon);
                data.Write(0); // Locked Flag
                data.Write((byte)realm.Colour);

                data.Write(realm.Name);
                data.Write(realm.Address);
                data.Write(realm.Population);

                if (realm.CharacterMap.TryGetValue(AuthSocket.GetAccountID(), out var characterCount))
                {
                    data.Write(characterCount);
                }
                else
                {
                    data.Write(0);
                }

                data.Write((byte)realm.TimeZone);
                data.Write(6);
            }

            data.Write(0x17);
            data.Write(0);

            var size = (ushort)(data.Size - 3);
            data.SetUInt16(1, size);

            Socket.Send(data.ToArray());
        }
    }

    public void TimeoutSockets()
    {
        var Socket = new Socket();
        if (!usepings)
            return;

        lock (serverSocketLock)
        {
            var currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            foreach (var socket in serverSockets.ToList())
            {
                if (socket.lastPing < currentTime && (currentTime - socket.lastPing) > 60)
                {
                    serverSockets.Remove(socket);

                    foreach (var serverId in socket.serverIds)
                    {
                        RemoveRealm(serverId);
                    }

                    serverSockets.Remove(socket);
                    Socket.Disconnect();
                }
            }
        }
    }

    public void CheckServers()
    {
        var sLog = new Logger();
        var Socket = new Socket();

        lock (serverSocketLock)
        {
            var socketsToRemove = new List<LogonCommServerSocket>();

            foreach (var socket in serverSockets)
            {
                var remoteAddress = Socket.GetRemoteAddress();
                if (remoteAddress != null && !LogonCommServerSocket.IsServerAllowed(remoteAddress))
                {
                    sLog.OutError(L_E_ACCOUNT_S_1, Socket.GetRemoteIP());
                    socketsToRemove.Add(socket);
                }
            }

            foreach (var socket in socketsToRemove)
            {
                Socket.Disconnect();
                serverSockets.Remove(socket);
            }
        }
    }

    internal uint GenerateRealmID()
    {
        return ++realmhigh;
    }

    internal void AddRealm(uint realmId, Realm realm)
    {
        lock (realmLock)
        {
            realms[realmId] = realm;
        }
    }
}

public static class UNIXTIME
{
    public static uint Value => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public static class SLogonSQL
{
    private static string connectionString;

    public static void SetConnectionString(string connString)
    {
        connectionString = connString;
    }

    public static void Execute(string query, Dictionary<string, object> parameters = null)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(query, connection);
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }
        }
        command.ExecuteNonQuery();
    }

    public static List<Field[]> Query(string query)
    {
        var result = new List<Field[]>();

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();
            var fieldCount = reader.FieldCount;
            while (reader.Read())
            {
                var fields = new Field[fieldCount];
                for (int i = 0; i < fieldCount; i++)
                {
                    fields[i] = new Field(reader.GetValue(i));
                }
                result.Add(fields);
            }
        }

        return result;
    }

    internal static string EscapeString(string value)
    {
        if (value == null)
        {
            return null;
        }

        return value.Replace("'", "''");
    }
}
