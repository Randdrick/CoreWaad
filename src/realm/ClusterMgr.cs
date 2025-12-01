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
using WaadShared;

using static WaadShared.ClusterManager;
using static WaadShared.Master;

namespace WaadRealmServer;

public class ClusterMgr : IDisposable
{
    private const int MAX_SINGLE_MAPID = 800;
    private const int MAX_WORKER_SERVERS = 100;
    private static Lazy<ClusterMgr> _instance = new(() => new ClusterMgr());
    public static ClusterMgr Instance => _instance.Value;
    private bool _disposed = false;

    // Stockage des instances et serveurs
    public static Dictionary<uint, Instance> InstancedMaps { get; } = [];
    private readonly Dictionary<uint, Instance> _instances = []; // InstanceId -> Instance
    private readonly Instance[] _singleInstanceMaps = new Instance[MAX_SINGLE_MAPID]; // MapId -> Instance (main maps)
    private readonly Dictionary<uint, List<Instance>> _instancedMaps = []; // MapId -> List<Instance>
    private readonly WorkerServer[] _workerServers = new WorkerServer[MAX_WORKER_SERVERS];
    private uint _maxInstanceId = 0;
    private uint _maxWorkerServer = 0;
    private readonly object _lock = new();  
    
    private ClusterMgr()
    {
        WorkerServer.InitHandlers();
    }
    public static void ResetInstance()
    {
        _instance = null;
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
                CLog.Notice("[~ClusterMg]", R_N_MASTER_18);
            }
            _disposed = true;
        }
    }

    ~ClusterMgr()
    {
        Dispose(false);
    }

    private void AddInstanceToMap(uint mapId, Instance inst)
    {
        lock (_lock)
        {
            if (!_instancedMaps.TryGetValue(mapId, out var list))
            {
                list = [];
                _instancedMaps[mapId] = list;
            }
            else
            {
                list.Clear(); // Vider la liste existante
            }
            list.Add(inst);
        }
    }

    public WorkerServer GetServerByInstanceId(uint instanceId)
    {
        lock (_lock)
        {
            return _instances.TryGetValue(instanceId, out var inst) ? inst.Server : null;
        }
    }

    public WorkerServer GetServerByMapId(uint mapId)
    {
        lock (_lock)
        {
            if (!IsMainMap(mapId))
                return null;
            return _singleInstanceMaps[mapId]?.Server;
        }
    }

    public Instance GetInstanceByInstanceId(uint instanceId)
    {
        lock (_lock)
        {
            return _instances.TryGetValue(instanceId, out var inst) ? inst : null;
        }
    }

    public Instance GetInstanceByMapId(uint mapId)
    {
        lock (_lock)
        {
            return _singleInstanceMaps[mapId];
        }
    }

    public Instance GetAnyInstance()
    {
        lock (_lock)
        {
            for (uint i = 0; i < MAX_SINGLE_MAPID; ++i)
            {
                if (_singleInstanceMaps[i] != null)
                    return _singleInstanceMaps[i];
            }
            return null;
        }
    }

    public Instance GetPrototypeInstanceByMapId(uint mapId)
    {
        lock (_lock)
        {
            if (!_instancedMaps.TryGetValue(mapId, out var list) || list.Count == 0)
                return null;

            Instance min = null;
            uint minCount = uint.MaxValue;
            foreach (var inst in list)
            {
                if (inst.MapCount < minCount)
                {
                    minCount = inst.MapCount;
                    min = inst;
                }
            }
            return min;
        }
    }

    public WorkerServer CreateWorkerServer(object socket)
    {
        lock (_lock)
        {
            uint i;
            for (i = 1; i < MAX_WORKER_SERVERS; ++i)
            {
                if (_workerServers[i] == null)
                    break;
            }
            if (i == MAX_WORKER_SERVERS)
                return null;

            var ws = new WorkerServer(i, socket);
            _workerServers[i] = ws;
            if (_maxWorkerServer <= i)
                _maxWorkerServer = i + 1;

            CLog.Success("ClusterMgr", R_S_CLUSMGR, i, ((WorkerServerSocket)socket).GetRemoteIP(), ((WorkerServerSocket)socket).GetRemotePort());
            return ws;
        }
    }

    public void AllocateInitialInstances(WorkerServer server, List<uint> preferred)
    {
        var result = new List<uint>();
        lock (_lock)
        {
            foreach (var mapId in preferred)
            {
                if (_singleInstanceMaps[mapId] == null)
                    result.Add(mapId);
            }
        }
        foreach (var mapId in result)
        {
            CreateInstance(mapId, server);
        }
    }

    public Instance CreateInstance(uint mapId, WorkerServer server)
    {
        var mapInfo = Storage.WorldMapInfoStorage.LookupEntry(mapId);
        if (mapInfo == null)
            return null;

        var inst = new Instance
        {
            InstanceId = ++_maxInstanceId,
            MapId = mapId,
            Server = server
        };

        lock (_lock)
        {
            _instances[inst.InstanceId] = inst;
            if (IsMainMap(mapId))
                _singleInstanceMaps[mapId] = inst;
            AddInstanceToMap(mapId, inst); // Utilise la méthode helper
        }

        var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_CREATE_INSTANCE);
        data.WriteUInt32(mapId);
        data.WriteUInt32(inst.InstanceId);
        server.SendPacket(data);
        server.AddInstance(inst);

        CLog.Success("ClusterMgr", R_S_CLUSMGR_1, inst.InstanceId, mapId, server.GetID());
        return inst;
    }


    public WorkerServer GetWorkerServerForNewInstance()
    {
        WorkerServer lowest = null;
        int lowestLoad = int.MaxValue;
        lock (_lock)
        {
            for (uint i = 0; i < MAX_WORKER_SERVERS; ++i)
            {
                var ws = _workerServers[i];
                if (ws != null)
                {
                    int count = ws.GetInstanceCount();
                    if (count < lowestLoad)
                    {
                        lowest = ws;
                        lowestLoad = count;
                    }
                }
            }
        }
        return lowest;
    }

    public Instance CreateInstance(uint instanceId, uint mapId)
    {
        var mapInfo = Storage.WorldMapInfoStorage.LookupEntry(mapId);
        if (mapInfo == null)
            return null;

        var server = GetWorkerServerForNewInstance();
        if (server == null)
            return null;

        lock (_lock)
        {
            if (_instances.ContainsKey(instanceId))
                return null;
            if (_maxInstanceId <= instanceId)
                _maxInstanceId = instanceId + 1;
        }

        var inst = new Instance
        {
            InstanceId = instanceId,
            MapId = mapId,
            Server = server
        };

        lock (_lock)
        {
            _instances[instanceId] = inst;
            if (IsMainMap(mapId))
                _singleInstanceMaps[mapId] = inst;
            AddInstanceToMap(mapId, inst); // Utilise la méthode helper
        }

        var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_CREATE_INSTANCE);
        data.WriteUInt32(mapId);
        data.WriteUInt32(instanceId);
        server.SendPacket(data);
        server.AddInstance(inst);

        CLog.Success("ClusterMgr", R_S_CLUSMGR_1, inst.InstanceId, mapId, server.GetID());
        return inst;
    }

    public void Update()
    {
        lock (_lock)
        {
            for (uint i = 1; i < _maxWorkerServer; i++)
            {
                _workerServers[i]?.Update();
            }
        }
    }

    public WorkerServer GetWorkerServer(uint id)
    {
        return (id < MAX_WORKER_SERVERS) ? _workerServers[id] : null;
    }

    public static bool IsInstance(uint mapId)
    {
        return mapId > 1 && mapId != 530 && mapId != 571 && mapId != 609;
    }

    public static bool IsMainMap(uint mapId)
    {
        return mapId < 2 || mapId == 530 || mapId == 571 || mapId == 609;
    }

    public void DistributePacketToAll(WorldPacket data)
    {
        DistributePacketToAll(data, null);
    }

    public void DistributePacketToAll(WorldPacket data, WorkerServer exclude)
    {
        lock (_lock)
        {
            for (uint i = 0; i < _maxWorkerServer; i++)
            {
                var ws = _workerServers[i];
                if (ws != null && ws != exclude && ws.ServerSocket != null)
                    ws.ServerSocket.SendPacket(data);
            }
        }
    }

    public void OnServerDisconnect(WorkerServer s)
    {
        lock (_lock)
        {
            // Nettoyage des instances
            var toRemove = new List<uint>();
            foreach (var kv in _instances)
            {
                if (kv.Value.Server == s)
                    toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove)
            {
                _instances.Remove(id);
                CLog.Warning("ClusterMgr", R_W_CLUSMGR_1, id);
            }

            // Nettoyage des instances instanciées
            var instancedToRemove = new List<uint>();
            foreach (var kv in _instancedMaps)
            {
                kv.Value.RemoveAll(inst => inst.Server == s);
                if (kv.Value.Count == 0)
                    instancedToRemove.Add(kv.Key);
            }
            foreach (var id in instancedToRemove)
            {
                _instancedMaps.Remove(id);
            }

            // Nettoyage des single instance maps
            for (uint i = 0; i < MAX_SINGLE_MAPID; ++i)
            {
                if (_singleInstanceMaps[i]?.Server == s)
                {
                    CLog.Warning("ClusterMgr", R_W_CLUSMGR_2, i);
                    _singleInstanceMaps[i] = null;
                }
            }

            // Nettoyage des worker servers
            for (uint i = 0; i < _maxWorkerServer; i++)
            {
                if (_workerServers[i] == s)
                {
                    CLog.Warning("ClusterMgr", R_W_CLUSMGR_3, i);
                    _workerServers[i] = null;
                }
            }
        }
        s.Dispose();
    }
}
