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
using MySql.Data.MySqlClient;
using Npgsql;
using System.Data.SQLite;
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
                    string ip = new Field(row[0]).GetString();
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
                    ipb.Expire = new Field(row[1]).GetUInt32();
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
    private static readonly Dictionary<string, Account> AccountDatabase = [];
    private readonly object setBusy = new();

    public void AddAccount(Field[] field)
    {
        var sLog = new Logger();

        // Vérifiez que le tableau 'field' n'est pas nul et contient des éléments
        if (field == null || field.Length < 9)
        {
            sLog.OutError("[AddAccount] Field array is null or does not contain enough elements.");
            return;
        }

        string username = field[1].GetString();
        if (string.IsNullOrEmpty(username))
        {
            sLog.OutError("[AddAccount] Account name is null or empty.");
            return;
        }

        username = username.ToUpper(); // Normalize the username to uppercase

        var acct = new Account
        {
            AccountId = field[0].GetUInt32(),
            AccountFlags = field[3].GetUInt8(),
            Banned = field[4].GetUInt32(),
            UsernamePtr = username // Assign the username to the account object
        };

        string GMFlags = field[2].GetString();
        string Salt = field[7].GetString() ?? "";
        string Verifier = field[8].GetString() ?? "";

        if (!string.IsNullOrEmpty(Salt))
        {
            acct.Salt = new BigNumber();
            acct.Salt.SetHexStr(Salt);
        }
        else
        {
            sLog.OutError("[AddAccount] Missing salt for account: {0}", username);
            return;
        }

        if (!string.IsNullOrEmpty(Verifier))
        {
            acct.Verifier = new BigNumber();
            acct.Verifier.SetHexStr(Verifier);
        }
        else
        {
            sLog.OutError("[AddAccount] Missing verifier for account: {0}", username);
            return;
        }

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

        acct.SetGMFlags(GMFlags);
        acct.Locale = "enUS".ToCharArray();

        if (field[5].GetString() != "enUS")
        {
            acct.Locale = field[5].GetString().ToCharArray();
            acct.ForcedLocale = true;
        }
        else
        {
            acct.ForcedLocale = false;
        }

        acct.Muted = field[6].GetUInt32();
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

        lock (setBusy)
        {
            AccountDatabase[username] = acct; // Add the account object to the dictionary
        }
    }

    public int GetAccountCount()
    {
        lock (setBusy)
        {
            return AccountDatabase.Count;
        }
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
        uint id = new Field(field[0]).GetUInt32();
        string username = new Field(field[1]).GetString();
        string gmFlags = new Field(field[2]).GetString();
        string salt = new Field(field[7]).GetString() ?? "";
        string verifier = new Field(field[8]).GetString() ?? "";
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

        acct.AccountId = new Field(field[0]).GetUInt32();
        acct.AccountFlags = new Field(field[3]).GetUInt8();
        acct.Banned = new Field(field[4]).GetUInt32();

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
        if (new Field(field[5]).GetString() != "enUS")
        {
            acct.Locale = new Field(field[5]).GetString().ToCharArray();
            acct.ForcedLocale = true;
        }
        else
        {
            acct.ForcedLocale = false;
        }

        acct.Muted = new Field(field[6]).GetUInt32();
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

        _ = username.ToUpper();
    }

    public void ReloadAccounts(bool silent)
    {
        var sLog = new Logger();
        lock (setBusy)
        {
            if (!silent) sLog.OutString(L_N_ACCOUNT);

            var result = SLogonSQL.Query("SELECT a.acct, a.login, a.gm, a.flags, a.banned, a.forceLanguage, a.muted, ad.salt, ad.verifier FROM accounts a LEFT JOIN account_data ad ON a.acct = ad.acct");
            var accountList = new HashSet<string>();

            if (result != null)
            {
                foreach (var field in result)
                {
                    // Vérifiez que le champ du nom de compte est valide
                    if (field.Length < 2 || field[1] == null)
                    {
                        sLog.OutError("[ReloadAccounts] Invalid account name field.");
                        continue;
                    }

                    string accountName = field[1].GetString()?.ToUpper(); // Normalisez le nom en majuscules
                    if (string.IsNullOrEmpty(accountName))
                    {
                        CLog.Error("[ACCOUNTMGR]", L_E_ACCOUNT_S_2);
                        continue;
                    }

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
            else
            {
                sLog.OutError("[ReloadAccounts] Failed to retrieve accounts from the database.");
            }

            // Supprimez les comptes qui ne sont plus dans la base de données
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

            if (!silent) sLog.OutDetail($"[ACCOUNTMGR] {AccountDatabase.Count} comptes trouvés.");
        }

        IPBanner.Reload();
    }

    public void ReloadAccountsCallback()
    {
        ReloadAccounts(true);
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
                var remoteAddress = WaadShared.Network.Socket.GetRemoteAddress();
                if (remoteAddress != null && !LogonCommServerSocket.IsServerAllowed(remoteAddress))
                {
                    sLog.OutError(L_E_ACCOUNT_S_1, WaadShared.Network.Socket.GetRemoteIP());
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
    private static int databaseType; // 1 = MySQL, 2 = PostgreSQL, 3 = SQLite

    public static void SetConnectionString(string connString, int dbType)
    {
        connectionString = connString;
        databaseType = dbType;
    }

    public static void Execute(string query, Dictionary<string, object> parameters = null)
    {
        switch (databaseType)
        {
            case 1: // MySQL
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    using var command = new MySqlCommand(query, connection);
                    AddParameters(command, parameters);
                    command.ExecuteNonQuery();
                }
                break;

            case 2: // PostgreSQL
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using var command = new NpgsqlCommand(query, connection);
                    AddParameters(command, parameters);
                    command.ExecuteNonQuery();
                }
                break;

            case 3: // SQLite
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    using var command = new SQLiteCommand(query, connection);
                    AddParameters(command, parameters);
                    command.ExecuteNonQuery();
                }
                break;

            default:
                throw new InvalidOperationException("Unsupported database type.");
        }
    }

    public static List<Field[]> Query(string query)
    {
        var result = new List<Field[]>();

        switch (databaseType)
        {
            case 1: // MySQL
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    using var command = new MySqlCommand(query, connection);
                    using var reader = command.ExecuteReader();
                    result = ReadFields(reader);
                }
                break;

            case 2: // PostgreSQL
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using var command = new NpgsqlCommand(query, connection);
                    using var reader = command.ExecuteReader();
                    result = ReadFields(reader);
                }
                break;

            case 3: // SQLite
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    using var command = new SQLiteCommand(query, connection);
                    using var reader = command.ExecuteReader();
                    result = ReadFields(reader);
                }
                break;

            default:
                throw new InvalidOperationException("Unsupported database type.");
        }

        return result;
    }

    private static void AddParameters(dynamic command, Dictionary<string, object> parameters)
    {
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value);
            }
        }
    }

    private static List<Field[]> ReadFields(dynamic reader)
    {
        var result = new List<Field[]>();
        var fieldCount = reader.FieldCount;

        while (reader.Read())
        {
            var fields = new Field[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                // Ensure the value is not DBNull
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                fields[i] = new Field(value);
            }
            result.Add(fields);
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
