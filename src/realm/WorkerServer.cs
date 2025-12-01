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
using WaadShared.AuthCodes;

using static DBCStores;
using static WaadShared.CharacterHandler;
using static WaadShared.WorkerServer;

namespace WaadRealmServer
{
    public class WorkerServer(uint id, object socket) : IDisposable
    {
        public WorkerServerSocket ServerSocket => _socket;
        public const int MaxSessionsPerServer = 1000;
        public uint LastPing { get; set; } = 0;
        public uint LastPong { get; set; } = 0;
        public uint PingTime { get; set; } = 0;
        public uint Latency { get; set; } = 0;

        private static DBCStorage<AreaTable> DbcArea { get; } = new DBCStorage<AreaTable>();
        private static readonly Dictionary<WorkerServerOpcodes, Action<WorkerServer, WorldPacket>> PHandlers = [];        
        private readonly uint _id = id;
        private readonly WorkerServerSocket _socket = (WorkerServerSocket)socket;
        private readonly Queue<WorldPacket> _recvQueue = new();
        private readonly List<Instance> _instances = []; // Use actual Instance type
        private bool _disposed = false;

        public static void InitHandlers()
        {
            PHandlers.Clear();
            PHandlers[WorkerServerOpcodes.ICMSG_REGISTER_WORKER]                  = (ws, p) => ws.HandleRegisterWorker(p);
            PHandlers[WorkerServerOpcodes.ICMSG_WOW_PACKET]                       = (ws, p) => HandleWoWPacket(p);
            PHandlers[WorkerServerOpcodes.ICMSG_PLAYER_LOGIN_RESULT]              = (ws, p) => ws.HandlePlayerLoginResult(p);
            PHandlers[WorkerServerOpcodes.ICMSG_PLAYER_LOGOUT]                    = (ws, p) => ws.HandlePlayerLogout(p);
            PHandlers[WorkerServerOpcodes.ICMSG_TELEPORT_REQUEST]                 = (ws, p) => ws.HandleTeleportRequest(p);
            PHandlers[WorkerServerOpcodes.ICMSG_ERROR_HANDLER]                    = (ws, p) => HandleError(p);
            PHandlers[WorkerServerOpcodes.ICMSG_SWITCH_SERVER]                    = (ws, p) => HandleSwitchServer(p);
            PHandlers[WorkerServerOpcodes.ICMSG_SAVE_ALL_PLAYERS]                 = (ws, p) => HandleSaveAllPlayers(p);
            PHandlers[WorkerServerOpcodes.ICMSG_TRANSPORTER_MAP_CHANGE]           = (ws, p) => HandleTransporterMapChange(p);
            PHandlers[WorkerServerOpcodes.ICMSG_PLAYER_TELEPORT]                  = (ws, p) => HandlePlayerTeleport(p);
            PHandlers[WorkerServerOpcodes.ICMSG_CREATE_PLAYER]                    = (ws, p) => HandleCreatePlayerResult(p);
            PHandlers[WorkerServerOpcodes.ICMSG_PLAYER_INFO]                      = (ws, p) => HandlePlayerInfo(p);
            PHandlers[WorkerServerOpcodes.ICMSG_CHANNEL_ACTION]                   = (ws, p) => HandleChannelAction(p);
            PHandlers[WorkerServerOpcodes.ICMSG_CHANNEL_UPDATE]                   = (ws, p) => HandleChannelUpdate(p);
            PHandlers[WorkerServerOpcodes.ICMSG_CHANNEL_LFG_DUNGEON_STATUS_REPLY] = (ws, p) => HandleChannelLFGDungeonStatusReply(p);
        }

        public int GetInstanceCount() => _instances.Count;
        public uint GetID() => _id;
        public void AddInstance(Instance instance) => _instances.Add(instance); // Use real Instance type
        public void QueuePacket(WorldPacket packet) => _recvQueue.Enqueue(packet);
        public void Update()
        {
            while (_recvQueue.Count > 0)
            {
                var packet = _recvQueue.Dequeue();
                var opcode = (WorkerServerOpcodes)packet.GetOpcode();
                if (PHandlers.TryGetValue(opcode, out var handler))
                {
                    handler(this, packet);
                }
                else
                    CLog.Error("[WServer]", R_E_WORKMGR_2, opcode);
            }

            uint currentTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Check for server disconnection (no pong for 60 seconds)
            if (LastPong > 0 && currentTime > LastPong && (currentTime - LastPong) > 60)
            {
                CLog.Warning("[WServer]", $"Worker server {_id} disconnected due to timeout");
                ClusterMgr.Instance.OnServerDisconnect(this);
                return;
            }

            // Send ping every 15 seconds and update player info
            if (LastPing == 0 || (currentTime - LastPing) > 15)
            {
                SendPing();
                ClientMgr.Instance.SendPackedClientInfo(_socket);
            }
        }

        public void SendPing()
        {
            PingTime = (uint)Environment.TickCount;
            var packet = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_REALM_PING_STATUS, 4);
            packet.WriteUInt32(PingTime);
            _socket.SendPacket(packet);
            LastPing = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public void SendPacket(WorldPacket packet)
        {
            _socket.SendPacket(packet);
        }

        // Handler stubs (to be implemented)
        protected void HandleRegisterWorker(WorldPacket p)
        {
            // Read: p >> build >> maps >> instancedmaps;
            _ = p.ReadUInt32(); // build (not used here)

            // Read maps list (count + elements)
            var maps = new List<uint>();
            uint mapsCount = p.ReadUInt32();
            for (uint i = 0; i < mapsCount; i++)
            {
                maps.Add(p.ReadUInt32());
            }
            
            // Read instancedmaps list (count + elements)
            var instancedmaps = new List<uint>();
            uint instancedmapsCount = p.ReadUInt32();
            for (uint i = 0; i < instancedmapsCount; i++)
            {
                instancedmaps.Add(p.ReadUInt32());
            }
            
            // Send packed client info to this worker server
            ClientMgr.Instance.SendPackedClientInfo(_socket);
            
            // Allocate initial instances for this worker
            ClusterMgr.Instance.AllocateInitialInstances(this, maps);
            
            // For each instanced map, create instance if needed
            if (instancedmaps.Count > 0)
            {
                foreach (var mapId in instancedmaps)
                {
                        if (!ClusterMgr.IsMainMap(mapId) && ClusterMgr.Instance.GetPrototypeInstanceByMapId(mapId) == null)
                    {
                        var instance = ClusterMgr.Instance.CreateInstance(mapId, this);
                        ClusterMgr.InstancedMaps[mapId] = instance;
                        CLog.Debug("ClusterMgr", R_D_WORKMGR, mapId, GetID());
                        Log.SLog.OutDetail(R_D_WORKMGR, mapId, GetID());
                    }
                }
            }
        }
        protected static void HandleWoWPacket(WorldPacket p)
        {
            uint sessionid = p.ReadUInt32();
            ushort opcode = p.ReadUInt16();
            uint size = p.ReadUInt32();
            var session = ClientMgr.Instance.GetSession(sessionid);
            if (session == null) return;
            var socket = session.GetSocket();
            if (socket != null)
            {
                // Extract data from packet payload
                // In C++: pck.contents() + 10 skips the header (sessionid(4) + opcode(2) + size(4) = 10 bytes)
                byte[] data = new byte[size];
                if (size > 0)
                {
                    p.Read(data, 0, (int)size);
                }
                socket.OutPacket(opcode, (ushort)size, data);
            }
        }
        protected void HandlePlayerLoginResult(WorldPacket p)
        {
            // 1. Lecture des données du paquet
            uint playerGuid = p.ReadUInt32();
            uint sessionId = p.ReadUInt32();
            byte resultCode = p.ReadByte();

            // 2. Traitement selon le résultat
            if (resultCode == (byte)LoginErrorCode.CHAR_LOGIN_SUCCESS)
            {
                HandleSuccessfulPlayerLogin(sessionId, playerGuid);
            }
            else
            {
                HandleFailedPlayerLogin(sessionId, playerGuid, resultCode);
            }
        }

        private void HandleSuccessfulPlayerLogin(uint sessionId, uint playerGuid)
        {
            CLog.Success("[WServer]", R_S_WORKMGR, _id, playerGuid);

            // 3. Récupération de la session
            var session = ClientMgr.Instance.GetSession(sessionId);
            if (session == null)
            {
                CLog.Warning("[WServer]", R_W_WORKMGR, sessionId);
                return;
            }

            // 4. Mise à jour de la session et du serveur
            session.SetServer(this);
            session.SetNextServer();

            // 5. Récupération du joueur et diffusion des informations
            var player = session.GetPlayer();
            if (player != null)
            {
                var playerInfoPacket = CreatePlayerInfoPacket(player);
                ClusterMgr.Instance.DistributePacketToAll(playerInfoPacket, this);
            }
            else
            {
                CLog.Warning("[WServer]", R_W_WORKMGR_1, playerGuid, _id);
            }
        }

        private void HandleFailedPlayerLogin(uint sessionId, uint playerGuid, byte resultCode)
        {
            // 6. Traitement explicite des différents codes d'erreur de connexion du personnage
            var session = ClientMgr.Instance.GetSession(sessionId);
            var code = (LoginErrorCode)resultCode;

            switch (code)
            {
                case LoginErrorCode.CHAR_LOGIN_FAILED:
                    CLog.Error("[WServer]", R_E_WORKMGR, _id, playerGuid);
                    break;

                case LoginErrorCode.CHAR_LOGIN_NO_WORLD:
                    CLog.Error("[WServer]", R_E_WORKMGR_4, playerGuid);
                    break;

                case LoginErrorCode.CHAR_LOGIN_DUPLICATE_CHARACTER:
                    CLog.Warning("[WServer]", R_W_CHARHAN_COM, playerGuid);
                    break;

                case LoginErrorCode.CHAR_LOGIN_NO_INSTANCES:
                    CLog.Warning("[WServer]", R_E_WORKMGR_5, playerGuid);
                    break;

                case LoginErrorCode.CHAR_LOGIN_DISABLED:
                case LoginErrorCode.CHAR_LOGIN_LOCKED_FOR_TRANSFER:
                case LoginErrorCode.CHAR_LOGIN_LOCKED_BY_BILLING:
                    CLog.Warning("[WServer]", R_W_WORKMGR_2, playerGuid);
                    break;

                case LoginErrorCode.CHAR_LOGIN_NO_CHARACTER:
                    CLog.Warning("[WServer]", R_W_CHARHAN_CD, playerGuid, session.GetAccountId());
                    break;

                default:
                    CLog.Error("[WServer]", R_E_WORKMGR_6, resultCode, playerGuid);
                    break;
            }

            // 7. Traitement et nettoyage communs pour tous les cas d'échec            
            if (session != null)
            {
                session.ClearCurrentPlayer();
                session.ClearServers();
            }

            ClientMgr.Instance.DestroyRPlayerInfo(playerGuid);
        }

        private static WorldPacket CreatePlayerInfoPacket(RPlayerInfo player)
        {
            // 8. Création du paquet avec les informations du joueur
            var packet = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_PLAYER_INFO, 200);
            packet.WriteUInt64(player.Guid);
            player.Pack(packet);
            return packet;
        }

        protected void HandlePlayerLogout(WorldPacket p)
        {
            uint sessionid = p.ReadUInt32();
            uint guid = p.ReadUInt32();
            var pi = ClientMgr.Instance.GetRPlayer(guid);
            var session = ClientMgr.Instance.GetSession(sessionid);
            if (pi != null && session != null)
            {
                var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_DESTROY_PLAYER_INFO, 4);
                data.WriteUInt32(sessionid);
                data.WriteUInt32(guid);
                ClusterMgr.Instance.DistributePacketToAll(data, this);
                session.ClearCurrentPlayer();
                session.ClearServers();
                ClientMgr.Instance.DestroyRPlayerInfo(guid);
            }
        }
        protected void HandleTeleportRequest(WorldPacket p)
        {
            var sLog = new Logger();
            uint sessionid = p.ReadUInt32();
            uint mapid = p.ReadUInt32();
            uint instanceid = p.ReadUInt32();

            CLog.Debug("TeleportRequest", $"session {sessionid}, mapid {mapid}, instanceid {instanceid}");
            var session = ClientMgr.Instance.GetSession(sessionid);
            if (session != null)
            {
                var pi = session.GetPlayer();
                if (pi == null) return;
                var dest = (instanceid == 0 || ClusterMgr.IsMainMap(mapid)) ? ClusterMgr.Instance.GetInstanceByMapId(mapid) : ClusterMgr.Instance.GetInstanceByInstanceId(instanceid);
                if (dest == null)
                {
                    // Try to find a prototype instance for this map (C++ fallback behavior)          
                    var proto = ClusterMgr.Instance.GetPrototypeInstanceByMapId(mapid);
                    if (proto != null)
                    {
                        // Use prototype instance as fallback
                        dest = proto;
                        CLog.Debug("TeleportRequest", R_D_WORKMGR_1);
                    }
                    else
                    {
                        // No instance and no prototype: abort transfer
                        CLog.Debug("TeleportRequest", R_D_WORKMGR_1_1);
                        var abort = new WorldPacket((ushort)Opcodes.SMSG_TRANSFER_ABORTED, 8);
                        abort.WriteUInt32(0x02); // INSTANCE_ABORT_NOT_FOUND
                        // send transfer aborted to the client's socket
                        session.GetSocket()?.OutPacket((ushort)Opcodes.SMSG_TRANSFER_ABORTED, (ushort)abort.Size, abort.Contents);
                        return;
                    }
                }
                // Read LocationVector (x,y,z) and orientation (o)
                float x = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
                float y = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
                float z = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
                float o = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;

                // Update player position and instance
                pi.MapId = mapid;
                pi.InstanceId = dest.InstanceId;
                pi.PositionX = x;
                pi.PositionY = y;
                pi.PositionZ = z;

                // Prepare teleport result packet
                var teleData = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_TELEPORT_RESULT, 100);
                if (dest.Server == session.GetServer())
                {
                    // Same server: immediate success (flag 1)
                    sLog.OutDetail(R_N_WORKMGR);
                    teleData.WriteUInt32(sessionid);
                    teleData.WriteByte(1);
                    teleData.WriteUInt32(mapid);
                    teleData.WriteUInt32(instanceid);
                    teleData.WriteFloat(x);
                    teleData.WriteFloat(y);
                    teleData.WriteFloat(z);
                    teleData.WriteFloat(o);
                    // send to this worker server socket
                    SendPacket(teleData);
                }
                else
                {
                    // Different server: notify old server to prepare transfer (flag 0)
                    sLog.OutDetail(R_N_WORKMGR_1);
                    teleData.WriteUInt32(sessionid);
                    teleData.WriteByte(0);
                    teleData.WriteUInt32(mapid);
                    teleData.WriteUInt32(instanceid);
                    teleData.WriteFloat(x);
                    teleData.WriteFloat(y);
                    teleData.WriteFloat(z);
                    teleData.WriteFloat(o);
                    // cache next server and send packet
                    session.SetNextServer(dest.Server);
                    SendPacket(teleData);
                }

                // Inform other worker servers about the player's new info
                var info = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_PLAYER_INFO, 200);
                info.WriteUInt64(pi.Guid);
                pi.Pack(info);
                ClusterMgr.Instance.DistributePacketToAll(info, this);

                // Notify client of new world
                var newWorld = new WorldPacket((ushort)Opcodes.SMSG_NEW_WORLD, 20);
                newWorld.WriteUInt32(mapid);
                newWorld.WriteFloat(x);
                newWorld.WriteFloat(y);
                newWorld.WriteFloat(z);
                newWorld.WriteFloat(o);
                session.GetSocket()?.OutPacket((ushort)Opcodes.SMSG_NEW_WORLD, (ushort)newWorld.Size, newWorld.Contents);
            }
        }
        protected static void HandleError(WorldPacket p)
        {
            uint sessionid = p.ReadUInt32();
            byte errorcode = p.ReadByte();
            if (errorcode == 1)
            {
                ClientMgr.Instance.DestroySession(sessionid);
            }
        }
        protected static void HandleSwitchServer(WorldPacket p)
        {
            uint sessionid = p.ReadUInt32();
            uint guid = p.ReadUInt32();
            uint mapid = p.ReadUInt32();
            uint instanceid = p.ReadUInt32();
            
            // Read LocationVector (x, y, z) and orientation (o)
            float x = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            float y = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            float z = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            float o = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            
            var session = ClientMgr.Instance.GetSession(sessionid);
            if (session == null) return;
            
            session.SetNextServer();
            
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_PLAYER_LOGIN, 64);
            data.WriteUInt32(guid);
            data.WriteUInt32(mapid);
            data.WriteUInt32(instanceid);
            data.WriteFloat(x);
            data.WriteFloat(y);
            data.WriteFloat(z);
            data.WriteFloat(o);
            data.WriteUInt32(session.AccountId);
            data.WriteUInt32(session.AccountFlags);
            data.WriteUInt32(session.SessionId);
            
            // Write permissions, name, build
            data.WriteString(session.GetAccountPermissions());
            data.WriteString(session.GetAccountName());
            data.WriteUInt32(session.GetClientBuild());
            
            // Write account data (8 entries, each with optional size and data)
            for (uint i = 0; i < 8; i++)
            {
                var acd = session.GetAccountData(i);
                if (acd.HasValue && acd.Value.Size > 0)
                {
                    data.WriteUInt32(acd.Value.Size);
                    data.WriteBytes(acd.Value.Data);
                }
                else
                {
                    data.WriteUInt32(0);
                }
            }
            
            session.GetServer()?.SendPacket(data);
        }
        protected static void HandleSaveAllPlayers(WorldPacket p)
        {
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_SAVE_ALL_PLAYERS, 1);
            data.WriteByte(0);
            ClusterMgr.Instance.DistributePacketToAll(data);
        }
        protected static void HandleTransporterMapChange(WorldPacket p)
        {
            CLog.Debug("[WServer]", "Recieved ICMSG_TRANSPORTER_MAP_CHANGE");
            uint transporterentry = p.ReadUInt32();
            uint mapid = p.ReadUInt32();
            _ = p.ReadUInt32(); // oldmapid
            float x = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            float y = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            float z = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;

            var dest = ClusterMgr.Instance.GetInstanceByMapId(mapid);
            if (dest == null) return;
            
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_TRANSPORTER_MAP_CHANGE, 20);
            data.WriteUInt32(transporterentry);
            data.WriteUInt32(mapid);
            data.WriteFloat(x);
            data.WriteFloat(y);
            data.WriteFloat(z);
            dest.Server.SendPacket(data);
        }
        protected static void HandlePlayerTeleport(WorldPacket p)
        {
            byte result = p.ReadByte();
            byte method = p.ReadByte();
            uint sessionid = p.ReadUInt32();
            uint mapid = p.ReadUInt32();
            uint instanceid = p.ReadUInt32();
            // Read LocationVector (x,y,z)
            float lx = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            float ly = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            float lz = BitConverter.ToSingle(p.Contents, p.Size); p.Size += 4;
            var session = ClientMgr.Instance.GetSession(sessionid);
            if (session == null)
            {
                return;
            }

            RPlayerInfo rp = null;
            switch (result)
            {
                case 2:
                    /* this is a callback, reserved */
                    break;
                case 1:
                    {
                        string player_name = p.ReadString();
                        rp = ClientMgr.Instance.GetRPlayer(player_name);
                        break;
                    }

                case 0:
                    {
                        uint player_guid = p.ReadUInt32();
                        rp = ClientMgr.Instance.GetRPlayer(player_guid);
                        break;
                    }
            }
            if (rp == null)
            {
                return;
            }

            var s2 = ClientMgr.Instance.GetSessionByRPInfo(rp);
            if (s2 == null)
            {
                return;
            }

            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_PLAYER_TELEPORT, 64);
            data.WriteByte(2);
            data.WriteByte(method);
            data.WriteUInt32(sessionid);
            data.WriteUInt32(mapid);
            data.WriteUInt32(instanceid);
            // Write location and target session id
            data.WriteFloat(lx);
            data.WriteFloat(ly);
            data.WriteFloat(lz);
            data.WriteUInt32(s2.SessionId);
            session.GetServer()?.SendPacket(data);
        }
        protected static void HandleCreatePlayerResult(WorldPacket p)
        {
            uint accountid = p.ReadUInt32();
            byte result = p.ReadByte();
            CLog.Debug("[WServer]", $"Received ICMSG_CREATE_PLAYER, result {result}");
            var session = ClientMgr.Instance.GetSessionByAccountId(accountid);
            if (session == null)
            {
                CLog.Error("[WServer]", R_E_WORKMGR_1);
                return;
            }
            session.GetSocket()?.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [result]);
        }
        protected static void HandlePlayerInfo(WorldPacket p)
        {
            uint guid = p.ReadUInt32();
            var player = ClientMgr.Instance.GetRPlayer(guid);
            if (player == null)
            {
                CLog.Warning("[WServer]", R_W_WORKMGR_3, guid);
                return;
            }

            player.Unpack(p);
        }
        protected static void HandleChannelAction(WorldPacket p)
        {
            byte action = p.ReadByte();
            switch ((MsgChannelAction)action)
            {
                case MsgChannelAction.CHANNEL_JOIN:
                    {
                        uint guid = p.ReadUInt32();
                        uint cid = p.ReadUInt32();
                        var player = ClientMgr.Instance.GetRPlayer(guid);
                        if (player == null)
                        {
                            CLog.Warning("[WServer]", "CHANNEL_JOIN :", R_W_WORKMGR_3, guid);
                            return;
                        }

                        var channel = ChannelMgr.GetChannel(cid);
                        channel?.AttemptJoin(player, "");
                        break;
                    }
                case MsgChannelAction.CHANNEL_PART:
                    {
                        uint guid = p.ReadUInt32();
                        uint cid = p.ReadUInt32();
                        var player = ClientMgr.Instance.GetRPlayer(guid);
                        if (player == null)
                        {
                            CLog.Warning("[WServer]", "CHANNEL_PART :", R_W_WORKMGR_3, guid);
                            return;
                        }

                        var channel = ChannelMgr.GetChannel(cid);
                        channel?.Part(player, true);
                        break;
                    }
                case MsgChannelAction.CHANNEL_SAY:
                    {
                        string channelname = p.ReadString();
                        uint guid = p.ReadUInt32();
                        string message = p.ReadString();
                        uint for_gm_guid = p.ReadUInt32();
                        bool forced = p.ReadByte() != 0;
                        var player = ClientMgr.Instance.GetRPlayer(guid);
                        if (player == null)
                        {
                            CLog.Warning("[WServer]", "CHANNEL_SAY :", R_W_WORKMGR_3, guid);
                            return;
                        }

                        var gmPlayer = for_gm_guid != 0 ? ClientMgr.Instance.GetRPlayer(for_gm_guid) : null;
                        var channel = ChannelMgr.GetChannel(channelname, player);
                        channel?.Say(player, message, gmPlayer, forced);
                        break;
                    }
                default:
                    CLog.Debug("[WServer]", R_D_WORKMGR_2, action);
                    break;
            }
        }
        protected static void HandleChannelUpdate(WorldPacket p)
        {
            byte updatetype = p.ReadByte();
            CLog.Debug("[WServer]", $"ChannelUpdate type: {updatetype}");
            uint guid = p.ReadUInt32();
            CLog.Debug("[WServer]", R_D_WORKMGR_4, guid);
            
            // Read channels list (count + uint32 elements)
            var channels = new List<uint>();
            uint channelsCount = p.ReadUInt32();
            for (uint i = 0; i < channelsCount; i++)
            {
                channels.Add(p.ReadUInt32());
            }
            
            // Read chnUpdtWrld flag
            bool chnUpdtWrld = p.ReadByte() != 0;
            
            var player = ClientMgr.Instance.GetRPlayer(guid);
            if (player == null)
            {
                CLog.Warning("[WServer]", R_W_WORKMGR_3, guid);
                return;
            }
            
            // Implement channel update logic based on updatetype
            switch ((CmsgChannelUpdate)updatetype)
            {
                case CmsgChannelUpdate.UPDATE_CHANNELS_ON_ZONE_CHANGE:
                    {
                        CLog.Debug("[WServer]", R_D_WORKMGR_8);
                        uint areaid = p.ReadUInt32();
                        uint zoneid = p.ReadUInt32();
                        uint mapid = p.ReadUInt32();

                        // Get AreaTable entries and map info from DBC storage
                        var areaEntry = DbcArea.LookupEntry(areaid);
                        var zoneEntry = DbcArea.LookupEntry(zoneid);
                        var mapInfo = Storage.WorldMapInfoStorage.LookupEntry(mapid);

                        // Handle special zone mappings
                        if (mapid == 450) zoneid = 2917;
                        if (mapid == 449) zoneid = 2918;

                        // Determine the area/zone name
                        string areaName = string.Empty;
                        bool shouldProcessChannels = true;

                        if (zoneid == 0 || zoneid == 0xFFFF)
                        {
                            // Check for instances: use map name if not main continents
                            if (mapid != 1 && mapid != 0 && mapid != 530 && mapInfo != null)
                            {
                                areaName = mapInfo.Name;
                            }
                            else
                            {
                                shouldProcessChannels = false; // Invalid zone, skip processing
                            }
                        }
                        else
                        {
                            // Use zone name if available
                            if (zoneEntry.name != null && zoneEntry.name.Length > 1)
                            {
                                areaName = zoneEntry.name;
                            }
                            else if (mapInfo != null)
                            {
                                // Fallback to map name
                                areaName = mapInfo.Name;
                            }
                        }

                        // Process each channel if we have valid zone info
                        if (shouldProcessChannels)
                        {
                            foreach (var cid in channels)
                            {
                                var pChannel = ChannelMgr.GetChannel(cid);
                                if (pChannel == null) continue;

                                // Skip non-updatable channels (with UPDATABLE flag)
                                if ((pChannel.Flags & 0x01) != 0) continue;
                                bool shouldUpdate = true;

                                // Build new channel name based on channel type
                                // Use Master singleton configuration for channel names
                                string newChannelName;
                                // Check channel type using Master configuration
                                if (pChannel.Name.Contains(Master.TradeChannelName, StringComparison.OrdinalIgnoreCase))
                                {
                                    newChannelName = Master.TradeChannelName;
                                }
                                else if (pChannel.Name.Contains(Master.DefenseChannelName, StringComparison.OrdinalIgnoreCase))
                                {
                                    newChannelName = Master.DefenseChannelName;
                                }
                                else if (pChannel.Name.Contains(Master.MainChannelName, StringComparison.OrdinalIgnoreCase))
                                {
                                    newChannelName = Master.MainChannelName;
                                }
                                else if (pChannel.Name.Contains(Master.LookingForChannelName, StringComparison.OrdinalIgnoreCase))
                                {
                                    newChannelName = Master.LookingForChannelName;
                                    shouldUpdate = false; // LFG is constant across zones
                                }
                                else if (pChannel.Name.Contains(Master.DefUniChannelName, StringComparison.OrdinalIgnoreCase))
                                {
                                    newChannelName = Master.DefUniChannelName;
                                    shouldUpdate = false; // UniversalDefense is constant across zones
                                }
                                else if (pChannel.Name.Contains(Master.GuildChannelName, StringComparison.OrdinalIgnoreCase))
                                {
                                    newChannelName = Master.GuildChannelName;
                                }
                                else
                                {
                                    // Unknown channel type, skip
                                    continue;
                                }

                                // Append zone name for zone-specific channels
                                if (shouldUpdate && !string.IsNullOrEmpty(areaName))
                                {
                                    // Check if this is a city channel (Trade/Guild in capital cities)
                                    bool isCity = false;
                                    
                                    // Check area entry
                                    if (areaEntry.AreaId > 0 && 
                                        ((areaEntry.AreaFlags & 0x00000020) != 0 || // AREA_CITY_AREA
                                         (areaEntry.AreaFlags & 0x00000100) != 0 || // AREA_CAPITAL
                                         (areaEntry.AreaFlags & 0x00000200) != 0))   // AREA_CITY
                                    {
                                        isCity = true;
                                    }
                                    // Check zone entry
                                    else if (zoneEntry.AreaId > 0 &&
                                             ((zoneEntry.AreaFlags & 0x00000020) != 0 || // AREA_CITY_AREA
                                              (zoneEntry.AreaFlags & 0x00000100) != 0 || // AREA_CAPITAL
                                              (zoneEntry.AreaFlags & 0x00000200) != 0))   // AREA_CITY
                                    {
                                        isCity = true;
                                    }

                                    if (isCity && (newChannelName == Master.TradeChannelName || newChannelName == Master.GuildChannelName))
                                    {
                                        newChannelName += " - " + Master.CityChannelName;
                                    }
                                    else
                                    {
                                        newChannelName += " - " + areaName;
                                    }
                                }

                                // Get or create the new channel
                                var newChannel = ChannelMgr.GetOrCreateChannel(newChannelName, player, pChannel.ChannelId);
                                if (newChannel != null)
                                {
                                    // Leave the old channel if different
                                    if (shouldUpdate && pChannel != newChannel)
                                    {
                                        CLog.Debug("[WServer]", R_D_WORKMGR_6, pChannel.ChannelId, pChannel.Name);
                                        pChannel.Part(player, true);
                                    }

                                    // Join the new channel if not already a member
                                    if (!newChannel.HasMember(player))
                                    {
                                        CLog.Debug("[WServer]", R_D_WORKMGR_7, newChannel.ChannelId, newChannel.Name);
                                        newChannel.AttemptJoin(player, "");
                                    }
                                }
                                else
                                {
                                    CLog.Error("[WServer]", R_E_WORKMGR_3, newChannelName);
                                }
                            }
                        }
                        break;
                    }

                case CmsgChannelUpdate.PART_ALL_CHANNELS:
                    {
                        CLog.Debug("[WServer]", "PART_ALL_CHANNELS");
                        foreach (var cid in channels)
                        {
                            var pChannel = ChannelMgr.GetChannel(cid);
                            pChannel?.Part(player, true);
                        }
                        break;
                    }

                case CmsgChannelUpdate.JOIN_ALL_CHANNELS:
                    {
                        CLog.Debug("[WServer]", "JOIN_ALL_CHANNELS");
                        foreach (var cid in channels)
                        {
                            var pChannel = ChannelMgr.GetChannel(cid);
                            pChannel?.AttemptJoin(player, "");
                        }
                        break;
                    }

                default:
                    {
                        CLog.Debug("[WServer]", R_D_WORKMGR_5, updatetype);
                        break;
                    }
            }
        }
        protected static void HandleChannelLFGDungeonStatusReply(WorldPacket p)
        {
            byte i = p.ReadByte();
            if (i == 3) return;
            uint guid = p.ReadUInt32();
            var player = ClientMgr.Instance.GetRPlayer(guid);
            if (player == null) return;
            uint dbc_id = p.ReadUInt32();
            ushort unk = p.ReadUInt16();
            string channelname = p.ReadString();
            string pass = p.ReadString();
            var chn = ChannelMgr.GetOrCreateChannel(channelname, player, dbc_id);
            if (chn == null) return;
            chn.AttemptJoin(player, pass);
            CLog.Debug("LfgChannelJoin", $"{channelname}, unk {unk}");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            try
            {
                CLog.Debug("[WServer]", $"Disposing WorkerServer {_id}");

                if (disposing)
                {
                    // Managed cleanup: clear receive queue and detach instances
                    try
                    {
                        lock (_recvQueue)
                        {
                            _recvQueue.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        CLog.Error("[WServer]", "Error clearing recv queue: {0}", ex.Message);
                    }

                    try
                    {
                        foreach (var inst in _instances)
                        {
                            try
                            {
                                if (inst != null)
                                    inst.Server = null;
                            }
                            catch { }
                        }
                        _instances.Clear();
                    }
                    catch (Exception ex)
                    {
                        CLog.Error("[WServer]", "Error detaching instances: {0}", ex.Message);
                    }

                    // IMPORTANT: Do NOT close the underlying socket here.
                    // The socket lifecycle and OnDisconnect flow is managed by
                    // `WorkerServerSocket` -> `ClusterMgr.OnServerDisconnect`.
                    // Closing the native socket here can cause re-entrancy and
                    // double-disconnect problems. Leave socket teardown to the
                    // socket/cluster manager.
                }
            }
            catch (Exception ex)
            {
                CLog.Error("[WServer]", "Dispose failed: {0}", ex.Message);
            }

            _disposed = true;
        }
    }
}


