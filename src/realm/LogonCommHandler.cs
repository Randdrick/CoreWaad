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
using System.Threading;

using WaadShared;
using WaadShared.Auth;
using WaadShared.Config;
using WaadShared.Network;

using static WaadShared.Network.Socket;
using static WaadShared.RealmListOpcode;

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
    private uint realmType;
    public byte[] SqlPassHash = new byte[20];
    public byte[] Key = new byte[20];
    private bool _disposed = false;

    public LogonCommHandler()
    {
        var configPath = System.IO.Path.Combine(AppContext.BaseDirectory, "waad-logonserver.ini");
        var configMgr = new ConfigMgr();
        if (!configMgr.MainConfig.SetSource(configPath))
        {
            CLog.Error("[ConsoleListener]", $"Config file not found: {configPath}");
            return;
        }
        idHigh = 1;
        nextRequest = 1;
        realmType = 0;
        
        string logonPass = configMgr.MainConfig.GetString("LogonServer:RemotePassword", "");
        pings = !configMgr.MainConfig.GetBoolean("LogonServer:DisablePings", "false");

        // SHA3 hash
        var hash = new Sha3Hash();
        hash.UpdateData(logonPass);
        hash.FinalizeHash();
        SqlPassHash = hash.GetDigest();
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
        LogonCommClientSocket conn = ConnectTCPSocket<LogonCommClientSocket>(address, (ushort)port);
        return conn;
    }
    public void RequestAddition(LogonCommClientSocket socket)
    {
        foreach (var realm in realms)
        {
            var data = new WorldPacket((ushort)RCMSG_REGISTER_REALM, 100);
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
        server.RetryTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        server.Registered = false;
        var conn = ConnectToLogon(server.Address, server.Port);
        logons[server] = conn;
        if (conn == null)
        {
            return;
        }
        conn.SendChallenge();
        var tt = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        while (conn.authenticated == 0)
        {
            if ((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= tt)
            {
                conn.Disconnect();
                logons[server] = null;
                return;
            }
            Thread.Sleep(10);
        }
        if (conn.authenticated == 0)
        {
            logons[server] = null;
            conn.Disconnect();
            return;
        }
        conn.SendPing();
        conn._id = server.ID;
        RequestAddition(conn);
        var st = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10;
        while (!server.Registered)
        {
            if ((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= st)
            {
                logons[server] = null;
                conn.Disconnect();
                break;
            }
            Thread.Sleep(50);
        }
        if (!server.Registered)
            return;
        Thread.Sleep(200);
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
            pendingLogons.Remove(id);
        }
    }

    public void RemoveUnauthedSocket(uint id)
    {
        pendingLogons.Remove(id);
    }
    public void LoadRealmConfiguration()
    {
        var configPath = System.IO.Path.Combine(AppContext.BaseDirectory, "waad-realms.ini");
        var configMgr = new ConfigMgr();
        if (!configMgr.RealmConfig.SetSource(configPath))
        {
            CLog.Error("[ConsoleListener]", $"Config file not found: {configPath}");
            return;
        }
        var ls = new LogonServer
        {
            ID = idHigh++,
            Address = configMgr.RealmConfig.GetString("LogonServer:IpOrHost", "127.0.0.1"),
            Port    = configMgr.RealmConfig.GetInt("LogonServer:Port", "8093"),
            Name    = configMgr.RealmConfig.GetString("LogonServer:Name", "UnkLogon")
        };
        servers.Add(ls);
        uint realmCount = (uint)configMgr.RealmConfig.GetInt32("LogonServer:RealmCount", "1");
        for (uint i = 1; i <= realmCount; ++i)
        {
            var realm = new Realm
            {
                Name       = configMgr.RealmConfig.GetString($"Realm{i}:Name", "SomeRealm"),
                Address    = configMgr.RealmConfig.GetString($"Realm{i}:Address", "127.0.0.1:8129"),
                Colour     = configMgr.RealmConfig.GetInt($"Realm{i}:Colour", "1"),
                TimeZone   = configMgr.RealmConfig.GetInt($"Realm{i}:TimeZone", "10"),
                Population = configMgr.RealmConfig.GetFloat($"Realm{i}:Population", "0"),
            };
            string rt = configMgr.RealmConfig.GetString($"Realm{i}:Icon", "Normal");
            uint type = rt.ToLowerInvariant() switch
            {
                "pvp" => (uint)RealmType.Pvp,
                "rp" => (uint)RealmType.Rp,
                "rppvp" => (uint)RealmType.RpPvp,
                _ => (uint)RealmType.Normal
            };
            realm.Icon = type;
            realmType = type;
            realms.Add(realm);
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
        forcedPermissions.TryGetValue(username, out var perm);
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
