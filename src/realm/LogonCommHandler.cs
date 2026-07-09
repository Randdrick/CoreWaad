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
using System.Collections.Concurrent;
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

namespace WaadRealmServer;

public class LogonServer
{
    public uint ID { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public uint Port { get; set; }
    public uint ServerID { get; set; }
    public long RetryTime { get; set; }
    public bool Registered { get; set; } = false;
    public bool IsConnecting { get; set; } = false;
    public long RegistrationTimeout { get; set; }
    public Action RegistrationCompleted { get; set; }
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
    private readonly ConcurrentDictionary<string, string> forcedPermissions = new();
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
    private volatile bool _shuttingDown = false;

    public LogonCommHandler()
    {
        idHigh = 1;
        nextRequest = 1;
        var configMgr = new ConfigMgr();
        
        // Load realm configuration to get the RemotePassword
        string realmConfigFile = Path.Combine(AppContext.BaseDirectory, "waad-realms.ini");
        configMgr.MainConfig.SetSource(realmConfigFile);

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
        CLog.Notice("[LogonCommHandler]", R_D_LOGCOMHAN_REQUEST_ADDITION, realms.Count);
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
        // Build the new table in a temporary dictionary, then swap atomically
        // so that concurrent GetForcedPermissions calls never observe a half-cleared state.
        var newPerms = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = CharacterDatabase.Query("SELECT * FROM account_forced_permissions");
        if (result != null)
        {
            do
            {
                string acct = result.GetValue(0)?.ToString()?.ToUpperInvariant();
                string perm = result.GetValue(1)?.ToString();
                if (!string.IsNullOrEmpty(acct))
                    newPerms[acct] = perm ?? string.Empty;
            } while (result.NextRow());
        }

        // Replace all entries atomically: remove keys gone, add/update new ones.
        foreach (var key in forcedPermissions.Keys)
        {
            if (!newPerms.ContainsKey(key))
                forcedPermissions.TryRemove(key, out _);
        }
        foreach (var kvp in newPerms)
            forcedPermissions[kvp.Key] = kvp.Value;
    }

    public void ConnectionDropped(uint id)
    {
        lock (mapLock)
        {
            foreach (var kvp in logons)
            {
                if (kvp.Key.ID == id && kvp.Value != null)
                {
                    kvp.Key.RetryTime = Environment.TickCount64 + 10_000;
                    kvp.Key.IsConnecting = false;  // Mark as no longer connecting so retry can work
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
                kvp.Key.RegistrationCompleted?.Invoke();
                return;
            }
        }
    }

    public bool IsShuttingDown => _shuttingDown;

    public void UpdateSockets()
    {
        if (_shuttingDown)
            return;

        // Collect mutations outside the foreach to avoid InvalidOperationException
        // (Dictionary version changes when a value is set during enumeration).
        var toDisconnect = new List<(LogonServer server, LogonCommClientSocket socket, string reason)>();
        var toConnect   = new List<LogonServer>();
        var toPing      = new List<LogonCommClientSocket>();

        lock (mapLock)
        {
            long now_ms = Environment.TickCount64;
            foreach (var kvp in logons)
            {
                var cs = kvp.Value;
                if (cs != null)
                {
                    if (!pings) continue;
                    if (cs.IsDeleted() || !cs.IsConnected())
                    {
                        toDisconnect.Add((kvp.Key, cs, null));
                        continue;
                    }
                    // Lecture atomique : last_pong_ms / last_ping_ms sont écrits par le thread IOCP.
                    // Interlocked.Read garantit la visibilité mémoire cross-thread sans lock.
                    long lastPong = Interlocked.Read(ref cs.last_pong_ms);
                    long lastPing = Interlocked.Read(ref cs.last_ping_ms);
                    long pongAge_ms = lastPong < now_ms ? now_ms - lastPong : 0;
                    if (pongAge_ms > 60_000)
                    {
                        // Stage 2 : aucun pong depuis 60 s — déconnexion définitive.
                        string reason = $"Disconnecting logon link due to heartbeat timeout (no pong for {pongAge_ms}ms). now={now_ms} last_ping_ms={lastPing} last_pong_ms={lastPong} id={kvp.Key.ID} addr={kvp.Key.Address}:{kvp.Key.Port}";
                        toDisconnect.Add((kvp.Key, cs, reason));
                        continue;
                    }
                    if ((now_ms - lastPing) > 15_000)
                    {
                        toPing.Add(cs);
                    }
                }
                else
                {
                    // Check if connection is in progress and timed out
                    if (kvp.Key.IsConnecting)
                    {
                        long timeLeft = kvp.Key.RegistrationTimeout - now_ms;
                        if (timeLeft <= 0)
                        {
                            // Connection attempt timed out
                            string reason = string.Format("Connection timeout for server {0} ({1}:{2})", 
                                kvp.Key.Name, kvp.Key.Address, kvp.Key.Port);
                            var connToCleanup = logons[kvp.Key];
                            toDisconnect.Add((kvp.Key, connToCleanup, reason));
                            kvp.Key.IsConnecting = false;
                            continue;
                        }
                        else
                        {
                            // Connection still in progress, timeout not yet reached
                            CLog.Debug("[LogonCommHandler]", 
                                string.Format("Waiting for connection to {0} ({1}:{2}), {3:F1}s remaining",
                                kvp.Key.Name, kvp.Key.Address, kvp.Key.Port, timeLeft / 1000.0));
                        }
                    }
                    
                    if (now_ms >= kvp.Key.RetryTime && !kvp.Key.IsConnecting)
                        toConnect.Add(kvp.Key);
                }
            }

            // Apply mutations now that enumeration is complete.
            foreach (var (server, cs, reason) in toDisconnect)
            {
                if (reason != null)
                    CLog.Warning("[LogonCommHandler]", reason);
                
                // Safely cleanup connection if it exists
                if (cs != null)
                {
                    cs._id = 0;
                    if (reason != null) // only full-disconnect on timeout; dead sockets just nulled
                        cs.Disconnect();
                }
                logons[server] = null;
            }
        }

        // Pings and reconnections are done outside the lock to avoid holding
        // mapLock during potentially blocking network I/O.
        foreach (var cs in toPing)
            cs.SendPing();

        foreach (var server in toConnect)
            Connect(server);
    }
    public void Connect(LogonServer server)
    {
        if (_shuttingDown)
            return;

        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_2, server.Name, server.Address, server.Port);
        server.RetryTime = Environment.TickCount64 + 10_000;
        server.Registered = false;
        
        // Mark as connecting to prevent duplicate attempts
        server.IsConnecting = true;
        
        // Set global timeout for the entire connection process (auth + registration)
        // This will be checked in UpdateSockets() if callbacks fail to trigger
        server.RegistrationTimeout = Environment.TickCount64 + 20_000; // 20 seconds total timeout
        
        var conn = ConnectToLogon(server.Address, server.Port);
        if (conn == null)
        {
            Logger.OutColor(LogColor.TRED, R_E_LOGCOMHAN, server.Address, server.Port);
            Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_NEWLINE);
            server.IsConnecting = false;
            return;
        }

        Logger.OutColor(LogColor.TGREEN, R_N_LOGCOMHAN_OK);
        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_3);
        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_PROMPT);

        logons[server] = conn;
        
        // Set _id BEFORE SendChallenge so that AdditionAck (called asynchronously
        // on the IOCP worker thread during HandleAuthResponse -> RequestAddition ->
        // HandleRegister) can match kvp.Key.ID and set server.Registered = true.
        conn._id = server.ID;

        // Setup callbacks for async completion - DO NOT BLOCK THE MAIN THREAD
        conn.AuthCompleted = (success) => 
        {
            if (_shuttingDown) return;
            conn.authenticated = success ? 1 : 0xFFFFFFFF;
            HandleAuthCompletion(server, conn, success);
        };
        
        server.RegistrationCompleted = () => 
        {
            if (_shuttingDown) return;
            HandleRegistrationCompletion(server, conn);
        };

        // Start connection process - this will trigger callbacks asynchronously
        conn.SendChallenge();

        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_4);
    }
    
    private void HandleAuthCompletion(LogonServer server, LogonCommClientSocket conn, bool success)
    {
        if (_shuttingDown)
        {
            CleanupConnection(server, conn);
            return;
        }
        
        if (!success)
        {
            Logger.OutColor(LogColor.TRED, R_E_LOGCOMHAN_1);
            CleanupConnection(server, conn);
            return;
        }
        
        Logger.OutColor(LogColor.TGREEN, " Ok !\n");
        conn.SendPing();
        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_5);
        
        // Start timeout timer for registration (10 seconds)
        server.RegistrationTimeout = Environment.TickCount64 + 10_000;
    }
    
    private void HandleRegistrationCompletion(LogonServer server, LogonCommClientSocket conn)
    {
        if (_shuttingDown)
        {
            CleanupConnection(server, conn);
            return;
        }
        
        if (!server.Registered)
        {
            // Registration failed or timeout
            CleanupConnection(server, conn);
            return;
        }
        
        // Connection successful - mark as no longer connecting
        server.IsConnecting = false;
        
        // Give some time for initial data to sync - use async delay to not block main thread
        _ = System.Threading.Tasks.Task.Run(async () => 
        {
            await System.Threading.Tasks.Task.Delay(200);
            if (!_shuttingDown && server.Registered)
            {
                Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_6);
                Logger.OutColor(LogColor.TYELLOW, R_N_LOGCOMHAN_LATENCE, conn.latency);
                Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_NEWLINE);
            }
        });
    }
    
    private void CleanupConnection(LogonServer server, LogonCommClientSocket conn)
    {
        server.IsConnecting = false;
        server.Registered = false;
        if (conn != null)
        {
            conn._id = 0;
            conn.Disconnect();
        }
        logons[server] = null;
    }
    //public void LogonDatabaseSQLExecute(string str, params object[] args) { /* ... */ }
    //public void LogonDatabaseReloadAccounts() { /* ... */ }


    // Worldsocket stuff

    public uint ClientConnected(string accountName, WorldSocket socket)
    {
        uint requestId = nextRequest++;
        Logger.OutColor(LogColor.TNORMAL, R_N_LOGCOMHAN_8, accountName, requestId);
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
            CLog.Error("[ConsoleListener]", R_E_LOGCOMHAN_CONFIG_NOT_FOUND, configPath);
            return;
        }
        CLog.Debug("[LogonCommHandler]", R_D_LOGCOMHAN_CHARGEMENT_CONF, configPath);
        // Normalize LogonServer address to IPv4 when possible
        string rawLogonAddr = configMgr.RealmConfig.GetString("LogonServer", "IpOrHost", "127.0.0.1");
        CLog.Debug("[LogonCommHandler]", R_D_LOGCOMHAN_ADRESSE_BRUTE, rawLogonAddr);
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
        CLog.Debug("[LogonCommHandler]", R_D_LOGCOMHAN_ADRESSE_NORMALISEE, normalizedLogonAddr);

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
            _shuttingDown = true;
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
                                var fd = kvp.Value.GetFd();
                                fd?.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                            }
                            catch { }
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
