/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2025 WAAD Team <https://arbonne.games-rpg.net/>
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
using System.IO.Compression;
using System.Threading;
using WaadShared;

using static WaadShared.Master;

namespace WaadRealmServer;

public class ClientMgr : IDisposable
{
    public IEnumerable<RPlayerInfo> GetAllPlayers()
    {
        _lock.EnterReadLock();
        try { return [.. _clients.Values]; }
        finally { _lock.ExitReadLock(); }
    }

    private bool _disposed = false;
    private static readonly Lazy<ClientMgr> _instance = new(() => new ClientMgr());
    public static ClientMgr Instance => _instance.Value;

    // Reset the singleton instance (used during shutdown)
    public static void ResetInstance()
    {
        // Note: Lazy<T> is readonly, so we cannot directly reset it.
        // This is intentional - the Lazy instance persists for the application lifetime.
        // The instance's Dispose() should be called manually before shutdown.
    }

    // Types
    private readonly Dictionary<string, RPlayerInfo> _stringClients = [];
    private readonly Dictionary<uint, RPlayerInfo> _clients = [];
    private readonly Dictionary<uint, Session> _sessions = [];
    private readonly Dictionary<RPlayerInfo, Session> _sessionsByInfo = [];
    private readonly List<uint> _reusableSessions = [];
    private readonly List<uint> _pendingDeleteSessionIds = [];
    private uint _maxSessionId = 0;
    private readonly ReaderWriterLockSlim _lock = new();

    private ClientMgr()
    {
        // Note: Session.InitHandlers() est appelé après l'initialisation de tous les singletons
        // pour éviter les dépendances circulaires
        CLog.Success("ClientMgr", ClientManager.R_S_CLTMGR);
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
                // Libérer les ressources managées ici
                CLog.Notice("[~ClientMgr]", R_N_MASTER_18);
            }
            _disposed = true;
        }
    }

    ~ClientMgr()
    {
        Dispose(false);
    }

    // Crée ou récupère un RPlayerInfo, gère les références (fidèle au C++)
    public RPlayerInfo CreateRPlayer(uint guid)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_clients.TryGetValue(guid, out var player))
            {
                player.References++;
                return player;
            }
            var rp = new RPlayerInfo { Guid = guid, References = 1 };
            _clients[guid] = rp;
            return rp;
        }
        finally { _lock.ExitWriteLock(); }
    }

    // Décrémente la référence et supprime si plus utilisé (fidèle au C++)
    public void DestroyRPlayerInfo(uint guid)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_clients.TryGetValue(guid, out var player))
            {
                player.References--;
                if (player.References <= 0)
                {
                    _clients.Remove(guid);
                    if (!string.IsNullOrEmpty(player.Name))
                        _stringClients.Remove(player.Name);
                    _sessionsByInfo.Remove(player);
                }
            }
        }
        finally { _lock.ExitWriteLock(); }
    }

    // Get session by RPlayerInfo
    public Session GetSessionByRPInfo(RPlayerInfo p)
    {
        _lock.EnterReadLock();
        try
        {
            return _sessionsByInfo.TryGetValue(p, out var s) ? s : null;
        }
        finally { _lock.ExitReadLock(); }
    }

    // Add session by RPlayerInfo
    public void AddSessionRPInfo(Session s, RPlayerInfo p)
    {
        _lock.EnterWriteLock();
        try { _sessionsByInfo[p] = s; }
        finally { _lock.ExitWriteLock(); }
    }

    // Add string player info
    public void AddStringPlayerInfo(RPlayerInfo p)
    {
        if (string.IsNullOrEmpty(p.Name)) return;
        _lock.EnterWriteLock();
        try { _stringClients[p.Name] = p; }
        finally { _lock.ExitWriteLock(); }
    }

    // Get RPlayer by guid
    public RPlayerInfo GetRPlayer(uint guid)
    {
        _lock.EnterReadLock();
        try { return _clients.TryGetValue(guid, out var r) ? r : null; }
        finally { _lock.ExitReadLock(); }
    }

    // Get RPlayer by name
    public RPlayerInfo GetRPlayer(string name)
    {
        _lock.EnterReadLock();
        try { return _stringClients.TryGetValue(name, out var r) ? r : null; }
        finally { _lock.ExitReadLock(); }
    }

    // Get session by id
    public Session GetSession(uint id)
    {
        _lock.EnterReadLock();
        try
        {
            if (_sessions.TryGetValue(id, out var s) && s != null && !s.Deleted)
                return s;
            return null;
        }
        finally { _lock.ExitReadLock(); }
    }

    // Get session by account id
    public Session GetSessionByAccountId(uint accountId)
    {
        _lock.EnterReadLock();
        try
        {
            foreach (var s in _sessions.Values)
            {
                if (!s.Deleted && s.GetAccountId() == accountId)
                    return s;
            }
            return null;
        }
        finally { _lock.ExitReadLock(); }
    }

    // Crée une nouvelle session, réutilise les ID si possible
    public Session CreateSession(uint accountId)
    {
        _lock.EnterWriteLock();
        try
        {
            // Vérifie si une session existe déjà pour ce compte
            foreach (var s in _sessions.Values)
            {
                if (!s.Deleted && s.GetAccountId() == accountId)
                {
                    // Ajoute à la liste des sessions à supprimer (pending delete)
                    _pendingDeleteSessionIds.Add(s.SessionId);
                }
            }
            // Génère un nouvel ID de session (réutilise si possible)
            uint sessionId;
            if (_reusableSessions.Count > 0)
            {
                sessionId = _reusableSessions[^1];
                _reusableSessions.RemoveAt(_reusableSessions.Count - 1);
            }
            else
            {
                sessionId = ++_maxSessionId;
            }
            if (sessionId == 0)
                return null;
            var session = new Session(accountId, sessionId);
            _sessions[session.SessionId] = session;
            return session;
        }
        finally { _lock.ExitWriteLock(); }
    }

    // Marque la session pour suppression (pending delete)
    public void DestroySession(uint sessionId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_sessions.TryGetValue(sessionId, out var s))
            {
                s.Deleted = true;
                _pendingDeleteSessionIds.Add(sessionId);
            }
        }
        finally { _lock.ExitWriteLock(); }
    }

    // Nettoyage des sessions à supprimer et update
    public void Update()
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var sessionId in _pendingDeleteSessionIds)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    var player = session.GetPlayer();
                    if (player != null && --player.References <= 0)
                    {
                        _sessionsByInfo.Remove(player);
                        _clients.Remove(player.Guid);
                        if (!string.IsNullOrEmpty(player.Name))
                            _stringClients.Remove(player.Name);
                    }
                    _reusableSessions.Add(sessionId);
                    _sessions.Remove(sessionId);
                }
            }
            _pendingDeleteSessionIds.Clear();
            foreach (var s in _sessions.Values)
            {
                if (!s.Deleted)
                    s.Update();
            }
        }
        finally { _lock.ExitWriteLock(); }
    }

    public void SendPackedClientInfo(WorkerServerSocket server)
    {
        lock (_lock)
        {
            if (_clients.Count == 0)
                return;

            var uncompressed = new ByteBuffer(40000 * 19 + 8);
            _ = uncompressed.WriteUInt32((uint)_clients.Count);
            foreach (var player in _clients.Values)
            {
                player.Pack(uncompressed);
            }

            int destSize = uncompressed.Size + uncompressed.Size / 10 + 16;
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_PACKED_PLAYER_INFO, destSize + 4);
            data.Resize(destSize + 4);

            // .NET compression, faithful to C++ logic
            byte[] compressed;
            int compressedSize;
            using (var ms = new System.IO.MemoryStream(destSize + 4))
            {
                ms.Write(new byte[4], 0, 4); // Reserve 4 bytes for size
                using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest, true))
                {
                    deflate.Write(uncompressed.Contents, 0, uncompressed.Size);
                }
                compressedSize = (int)ms.Position;
                compressed = ms.GetBuffer(); // This may be larger than compressedSize, so we must copy
            }
            // Copy only the real compressed data
            byte[] finalPacket = new byte[compressedSize];
            Array.Copy(compressed, 0, finalPacket, 0, compressedSize);
            // Write the uncompressed size in the first 4 bytes
            Array.Copy(BitConverter.GetBytes(uncompressed.Size), 0, finalPacket, 0, 4);
            data.Contents = finalPacket;
            data.Size = finalPacket.Length;

            server.SendPacket(data);
        }
    }
}
