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
using System.Linq;
using System.IO;
using System.Threading;
using System.Net;

using WaadShared;
using WaadShared.Auth;
using WaadShared.Config;
using WaadShared.Network;

using static WaadShared.RealmListOpcode;
using static WaadShared.LogonCommHandler;
using System.Data.SQLite;

namespace WaadRealmServer;

public class LogonServer
{
    public uint ID { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public uint Port { get; set; }
    public uint ServerID { get; set; }
    public uint RetryTime { get; set; }
    public bool Registered { get; set; } = false;
}

public class Realm
{
    public string Name { get; set; }
    public string Address { get; set; }
    public uint Colour { get; set; }
    public uint Icon { get; set; }
    public uint TimeZone { get; set; }
    public float Population { get; set; }
}

public enum RealmType
{
    Normal = 0,
    Pvp = 3,
    Rp = 6,
    RpPvp = 8,
}

public class LogonCommHandler : IDisposable
{
    private readonly Dictionary<string, string> forcedPermissions = [];
    private readonly Dictionary<LogonServer, LogonCommClientSocket> logons = [];
    private readonly Dictionary<uint, WorldSocket> pendingLogons = [];
    private readonly HashSet<Realm> realms = [];
    private readonly HashSet<LogonServer> servers = [];
    private uint idHigh;
    private uint nextRequest;
    private readonly object mapLock = new();
    private readonly object pendingLock = new();
    private readonly bool pings;
    public byte[] SqlPassHash = new byte[20];
    public byte[] Key = new byte[20];
    private bool _disposed = false;

    public LogonCommHandler()
    {
        idHigh = 1;
        nextRequest = 1;
        var configMgr = new ConfigMgr();

        string logonPass = configMgr.MainConfig.GetString("LogonServer", "RemotePassword", "r3m0t3b4d");
        pings = !configMgr.MainConfig.GetBoolean("LogonServer","DisablePings", false);

        var sLog = new Logger();
        sLog.OutDebug("[LogonCommHandler]", "logonPass: {0}", logonPass);

        // SHA3 hash
        var hash = new Sha3Hash();
        hash.UpdateData(logonPass);
        hash.FinalizeHash();
        SqlPassHash = hash.GetDigest();

        sLog.OutDebug("[LogonCommHandler]", "SqlPassHash first 20: {0}", BitConverter.ToString(SqlPassHash, 0, 20).Replace("-", " "));
    }

    public void OnSessionInfo(WorldPacket recvData, uint requestId)
    {
        lock (pendingLock)
        {
            WorldSocket sock = GetSocketByRequest(requestId);
            if (sock == null || sock.Authed || !sock.IsConnected())
            {
                // Socket expirée ou client déconnecté
                return;
            }

            // Extraction des infos de session (fait côté WorldSocket)
            sock.Authed = true;
            RemoveUnauthedSocket(requestId);
            sock.InformationRetreiveCallback(recvData, requestId);
        }
    }

    public static LogonCommClientSocket ConnectToLogon(string address, uint port)
    {
        LogonCommClientSocket conn = Socket.ConnectTCPSocket<LogonCommClientSocket>(address, (ushort)port);
        return conn;
    }
    public void RequestAddition(LogonCommClientSocket socket)
    {
        CLog.Notice("[LogonCommHandler]", $"RequestAddition: sending register for {realms.Count} realms");
        var data = new WorldPacket((ushort)RCMSG_REGISTER_REALM, 100);
        foreach (var realm in realms)
        {
            data.WriteString(realm.Name);
            data.WriteString(realm.Address);
            data.WriteUInt32(realm.Colour);
            data.WriteUInt32(realm.Icon);
            data.WriteUInt32(realm.TimeZone);
            data.WriteFloat(realm.Population);
            socket.SendPacket(data, false);
        }
    }
    public void Startup()
    {
        // Connect to all logons
        LoadRealmConfiguration();
        foreach (var server in servers)
            Connect(server);
    }

    public void ReloadForcedPermissions()
    {
        // TODO: Query DB for forced permissions
        forcedPermissions.Clear();
        var result = CharacterDatabase.Query("SELECT * FROM account_forced_permissions");
        if (result != null)
        {
            do
            {
                string acct = result.GetValue(0).ToString().ToUpperInvariant();
                string perm = result.GetValue(1).ToString();
                forcedPermissions[acct] = perm;
            } while (result.NextRow());
        }
    }

    public void ConnectionDropped(uint id)
    {
        lock (mapLock)
        {
            foreach (var kvp in logons)
            {
                if (kvp.Key.ID == id && kvp.Value != null)
                {
                    kvp.Value.Disconnect();
                    logons[kvp.Key] = null;
                    break;
                }
            }
        }
    }

    public void AdditionAck(uint id, uint servId)
    {
        foreach (var kvp in logons)
        {
            if (kvp.Key.ID == id)
            {
                kvp.Key.ServerID = servId;
                kvp.Key.Registered = true;
                return;
            }
        }
    }

    public void UpdateSockets()
    {
        lock (mapLock)
        {
            uint t = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var kvp in logons)
            {
                var cs = kvp.Value;
                if (cs != null)
                {
                    if (!pings) continue;
                    if (cs.IsDeleted() || !cs.IsConnected())
                    {
                        cs._id = 0;
                        logons[kvp.Key] = null;
                        continue;
                    }
                    if (cs.last_pong < t && ((t - cs.last_pong) > 60))
                    {
                        cs._id = 0;
                        cs.Disconnect();
                        logons[kvp.Key] = null;
                        continue;
                    }
                    if ((t - cs.last_ping) > 15)
                    {
                        cs.SendPing();
                    }
                }
                else
                {
                    if (t >= kvp.Key.RetryTime)
                    {
                        Connect(kvp.Key);
                    }
                }
            }
        }
    }
    public void Connect(LogonServer server)
    {
        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_2, server.Name, server.Address, server.Port);
        server.RetryTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        server.Registered = false;
        var conn = ConnectToLogon(server.Address, server.Port);
        logons[server] = conn;
        if (conn == null)
        {
            Logger.OutColor(LogColor.TRED, R_E_LOGCOMHAN, server.Address, server.Port);
            Logger.OutColor(LogColor.TNORMAL, "\n");
            return;
        }

        Logger.OutColor(LogColor.TGREEN, " Ok !\n");
        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_3);
        Logger.OutColor(LogColor.TNORMAL, "        >> ");

        uint tt = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        conn.SendChallenge();
        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_4);

        while (conn.authenticated == 0)
        {
            if ((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= tt)
            {
                Logger.OutColor(LogColor.TYELLOW, R_Y_LOGCOMHAN);
                conn.Disconnect();
                logons[server] = null;
                return;
            }
            Thread.Sleep(10);
        }

        if (conn.authenticated != 1)
        {
            Logger.OutColor(LogColor.TRED, R_E_LOGCOMHAN_1);
            logons[server] = null;
            conn.Disconnect();
            return;
        }
        else
            Logger.OutColor(LogColor.TGREEN, " Ok !\n");

        conn.SendPing();

        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_5);
        conn._id = server.ID;
        // RequestAddition(conn); removed, now called in HandleAuthResponse
        var st = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        while (!server.Registered)
        {
            if ((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= st)
            {
                CLog.Warning("[LogonCommHandler]", R_Y_LOGCOMHAN_1);
                logons[server] = null;
                conn.Disconnect();
                break;
            }
            Thread.Sleep(50);
        }
        if (!server.Registered)
            return;
        Thread.Sleep(200);

        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_6);
        Logger.OutColor(LogColor.TYELLOW, "%ums", conn.latency);
        Logger.OutColor(LogColor.TNORMAL, "\n");
    }
    //public void LogonDatabaseSQLExecute(string str, params object[] args) { /* ... */ }
    //public void LogonDatabaseReloadAccounts() { /* ... */ }


    // Worldsocket stuff

    public uint ClientConnected(string accountName, WorldSocket socket)
    {
        uint requestId = nextRequest++;
        if (logons.Count == 0)
            return uint.MaxValue;
        var s = logons.Values.FirstOrDefault();
        if (s == null)
            return uint.MaxValue;
        lock (pendingLock)
        {
            var data = new WorldPacket((ushort)RCMSG_REQUEST_SESSION, 100);
            data.WriteUInt32(requestId);
            var acct = accountName.Split('#')[0];
            data.WriteString(acct);
            data.WriteByte(0);
            s.SendPacket(data, false);
            pendingLogons[requestId] = socket;
        }
        return requestId;
    }

    public void UnauthedSocketClose(uint id)
    {
        lock (pendingLock)
        {
            _ = pendingLogons.Remove(id);
        }
    }

    public void RemoveUnauthedSocket(uint id)
    {
        _ = pendingLogons.Remove(id);
    }
    public void LoadRealmConfiguration()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "waad-realms.ini");
        var configMgr = new ConfigMgr();
        if (!configMgr.RealmConfig.SetSource(configPath))
        {
            CLog.Error("[ConsoleListener]", $"Config file not found: {configPath}");
            return;
        }
        CLog.Debug("[LogonCommHandler]", $"Loading config from: {configPath}");
        // Normalize LogonServer address to IPv4 when possible
        string rawLogonAddr = configMgr.RealmConfig.GetString("LogonServer", "IpOrHost", "127.0.0.1");
        CLog.Debug("[LogonCommHandler]", $"Raw logon address: {rawLogonAddr}");
        string normalizedLogonAddr = rawLogonAddr;
        if (!IPAddress.TryParse(rawLogonAddr, out var parsedIp) || parsedIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            try
            {
                // Try to resolve and pick an IPv4 address
                var he = Dns.GetHostEntry(rawLogonAddr);
                var ipv4 = he.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ipv4 != null)
                    normalizedLogonAddr = ipv4.ToString();
            }
            catch { /* keep raw if resolution fails */ }
        }
        CLog.Debug("[LogonCommHandler]", $"Normalized logon address: {normalizedLogonAddr}");

        var ls = new LogonServer
        {
            ID = idHigh++,
            Address = normalizedLogonAddr,
            Port    = (uint)configMgr.RealmConfig.GetInt32("LogonServer", "Port", 8093),
            Name    = configMgr.RealmConfig.GetString("LogonServer", "Name", "UnkLogon")
        };
        _ = servers.Add(ls);
        uint realmCount = (uint)configMgr.RealmConfig.GetInt32("LogonServer", "RealmCount", 1);
        for (uint i = 1; i <= realmCount; ++i)
        {
            // Normalize realm address (may be host:port or literal IP). Prefer IPv4.
            string rawRealmAddr = configMgr.RealmConfig.GetString($"Realm{i}", "Address", "127.0.0.1:8129");
            string realmHost = rawRealmAddr;
            int realmPort = 8129;

            // Parse host[:port], handling bracketed IPv6 if present
            if (rawRealmAddr.StartsWith('['))
            {
                int end = rawRealmAddr.IndexOf(']');
                if (end > 1)
                {
                    realmHost = rawRealmAddr[1..end];
                    if (rawRealmAddr.Length > end + 1 && rawRealmAddr[end + 1] == ':')
                        _ = int.TryParse(rawRealmAddr.AsSpan(end + 2), out realmPort);
                }
            }
            else
            {
                int lastColon = rawRealmAddr.LastIndexOf(':');
                if (lastColon > 0 && rawRealmAddr.Count(c => c == ':') == 1)
                {
                    realmHost = rawRealmAddr[..lastColon];
                    _ = int.TryParse(rawRealmAddr.AsSpan(lastColon + 1), out realmPort);
                }
                else if (lastColon > 0 && rawRealmAddr.Count(c => c == ':') > 1)
                {
                    // IPv6 literal without brackets; treat host as full literal
                    realmHost = rawRealmAddr;
                }
            }

            // Resolve and prefer IPv4
            try
            {
                var he = Dns.GetHostEntry(realmHost);
                var ipv4 = he.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ipv4 != null)
                    realmHost = ipv4.ToString();
            }
            catch { /* keep original host if resolution fails */ }

            var realm = new Realm
            {
                Name       = configMgr.RealmConfig.GetString($"Realm{i}", "Name", "SomeRealm"),
                Address    = realmHost + ":" + realmPort,
                Colour     = (uint)configMgr.RealmConfig.GetInt32($"Realm{i}", "Colour", 1),
                TimeZone   = (uint)configMgr.RealmConfig.GetInt32($"Realm{i}", "TimeZone", 10),
                Population = configMgr.RealmConfig.GetFloat($"Realm{i}", "Population", 0),
            };
            string rt = configMgr.RealmConfig.GetString($"Realm{i}", "Icon", "Normal");
            uint type = rt.ToLowerInvariant() switch
            {
                "pvp" => (uint)RealmType.Pvp,
                "rp" => (uint)RealmType.Rp,
                "rppvp" => (uint)RealmType.RpPvp,
                _ => (uint)RealmType.Normal
            };
            realm.Icon = type;
            _ = realms.Add(realm);
        }
    }
    public void UpdateAccountCount(uint accountId, byte add)
    {
        if (logons.Count == 0 || logons.Values.FirstOrDefault() == null)
            return;
        logons.Values.First().UpdateAccountCount(accountId, add);
    }

    public WorldSocket GetSocketByRequest(uint id)
    {
        lock (pendingLock)
        {
            return pendingLogons.TryGetValue(id, out var sock) ? sock : null;
        }
    }
    public object GetPendingLock() => pendingLock;
    public string GetForcedPermissions(string username)
    {
        _ = forcedPermissions.TryGetValue(username, out var perm);
        return perm;
    }

    public void Account_SetBanned(string account, uint banned)
    {
        if (logons.Count == 0 || logons.Values.FirstOrDefault() == null)
            return;
        var data = new WorldPacket((ushort)RCMSG_MODIFY_DATABASE, 50);
        data.WriteUInt32(1);
        data.WriteString(account);
        data.WriteUInt32(banned);
        logons.Values.First().SendPacket(data, false);
    }
    public void Account_SetGM(string account, string flags)
    {
        if (logons.Count == 0 || logons.Values.FirstOrDefault() == null)
            return;
        var data = new WorldPacket((ushort)RCMSG_MODIFY_DATABASE, 50);
        data.WriteUInt32(2);
        data.WriteString(account);
        data.WriteString(flags);
        logons.Values.First().SendPacket(data, false);
    }
    public void Account_SetMute(string account, uint muted)
    {
        if (logons.Count == 0 || logons.Values.FirstOrDefault() == null)
            return;
        var data = new WorldPacket((ushort)RCMSG_MODIFY_DATABASE, 50);
        data.WriteUInt32(3);
        data.WriteString(account);
        data.WriteUInt32(muted);
        logons.Values.First().SendPacket(data, false);
    }
    public void IPBan_Add(string ip, uint duration)
    {
        if (logons.Count == 0 || logons.Values.FirstOrDefault() == null)
            return;
        var data = new WorldPacket((ushort)RCMSG_MODIFY_DATABASE, 50);
        data.WriteUInt32(4);
        data.WriteString(ip);
        data.WriteUInt32(duration);
        logons.Values.First().SendPacket(data, false);
    }
    public void IPBan_Remove(string ip)
    {
        if (logons.Count == 0 || logons.Values.FirstOrDefault() == null)
            return;
        var data = new WorldPacket((ushort)RCMSG_MODIFY_DATABASE, 50);
        data.WriteUInt32(5);
        data.WriteString(ip);
        logons.Values.First().SendPacket(data, false);
    }
    public void Account_SetOneDK(uint guidPlayer, bool oneDKCreated)
    {
        if (logons.Count == 0 || logons.Values.FirstOrDefault() == null)
            return;
        var data = new WorldPacket((ushort)RCMSG_MODIFY_DATABASE, 50);
        data.WriteUInt32(6);
        data.WriteUInt32(guidPlayer);
        data.WriteUInt32(oneDKCreated ? 1u : 0u);
        logons.Values.First().SendPacket(data, false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Nettoyer les ressources managées : déconnecter tous les sockets actifs
                lock (mapLock)
                {
                    foreach (var kvp in logons)
                    {
                        if (kvp.Value != null && kvp.Value.IsConnected())
                        {
                            try
                            {
                                kvp.Value.Disconnect();
                            }
                            catch { }
                        }
                    }
                    logons.Clear();
                }

                lock (pendingLock)
                {
                    pendingLogons.Clear();
                }
            }

            _disposed = true;
        }
    }

    // Singleton pattern
    private static LogonCommHandler _instance;
    public static LogonCommHandler Instance => _instance ??= new LogonCommHandler();
}
