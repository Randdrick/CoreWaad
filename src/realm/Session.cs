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
using System.Collections.Concurrent;
using WaadShared;
using static WaadShared.Session;

namespace WaadRealmServer
{
    public partial class Session(uint accountId, uint sessionId) 
    {
        // --- Constants and Enums ---
        public const int NUM_ACCOUNT_DATA_TYPES = 8;
        public const byte GLOBAL_CACHE_MASK = 0x15;
        public const byte PER_CHARACTER_CACHE_MASK = 0xEA;

        public struct AccountDataEntry
        {
            public long Time; // Unix timestamp
            public byte[] Data;
            public uint Size;
            public bool IsDirty;
        }

        // --- Fields (adapted from C++ Session) ---
        protected readonly ConcurrentQueue<WorldPacket> m_readQueue = new();
        protected WorldSocket m_socket;
        protected WorkerServer m_server;
        protected WorkerServer m_nextServer;
        protected uint m_sessionId = sessionId;
        protected uint m_accountId = accountId;
        protected uint m_ClientBuild;
        protected RPlayerInfo m_currentPlayer;
        protected uint m_latency;
        protected uint m_accountFlags;
        protected string m_GMPermissions = string.Empty;
        protected string m_accountName = string.Empty;
        protected uint m_build;
        protected uint language;
        protected bool m_loadedPlayerData;
        protected AccountDataEntry[] sAccountData = new AccountDataEntry[NUM_ACCOUNT_DATA_TYPES];

        public bool Deleted = false;
        public byte UpdateCount;
        public uint Muted;
        public uint LastPing;

        // --- Extra fields for ConsoleCommands and ClientMgr compatibility ---
        public string Race { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
        public int Level { get; set; }
        public int References { get; set; } = 1;

        // --- Properties and Inline Methods ---
        public string Name => m_currentPlayer?.Name ?? string.Empty;
        public string GMPermissionsDisplay => m_GMPermissions;
        public RPlayerInfo CurrentPlayer { get => m_currentPlayer; set => m_currentPlayer = value; }
        public uint SessionId { get => m_sessionId; set => m_sessionId = value; }
        public uint AccountId { get => m_accountId; set => m_accountId = value; }
        public string GMPermissions { get => m_GMPermissions; set => m_GMPermissions = value; }
        public uint AccountFlags { get => m_accountFlags; set => m_accountFlags = value; }
        public uint ClientBuild { get => m_ClientBuild; set => m_ClientBuild = value; }
        public uint Latency { get => m_latency; set => m_latency = value; }

        public string AccountName { get => m_accountName; set => m_accountName = value; }
        public uint Language { get => language; set => language = value; }

        public string GetRemoteIP()
        {
            // TODO: Return real IP if m_socket is implemented
            return "0.0.0.0";
        }
        public string GetAccountName() => m_accountName;
        public Session GetSession() => this;

        public uint GetAccountId() => m_accountId;
        public uint GetSessionId() => m_sessionId;
        public string GetAccountPermissions() => m_GMPermissions;
        public uint GetAccountFlags() => m_accountFlags;
        public uint GetClientBuild() => m_ClientBuild;

        // --- Packet Queue ---
        public void QueuePacket(WorldPacket packet)
        {
            m_readQueue.Enqueue(packet);
        }

        // (Removed duplicate SendPacket/QueuePacket methods)

        public RPlayerInfo GetPlayer() => m_currentPlayer;
        public void ClearCurrentPlayer() => m_currentPlayer = null;
        public void ClearServers() { m_nextServer = m_server = null; }
        public void SetNextServer() { m_server = m_nextServer; }
        public object GetNextServer() => m_nextServer;
        public WorkerServer GetServer() => m_server;
        public void SetServer(WorkerServer s) { m_server = s; }
        public void SetNextServer(WorkerServer s) { m_nextServer = s; }
        public WorldSocket GetSocket() => m_socket;
        public void SetSocket(WorldSocket sock) { m_socket = sock; }

        // --- AccountDataEntry logic ---
        public void SetAccountData(uint index, byte[] data, bool initial, uint sz)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual<uint>(index, NUM_ACCOUNT_DATA_TYPES);
            sAccountData[index].Data = data;
            sAccountData[index].Size = sz;
            sAccountData[index].Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!initial && !sAccountData[index].IsDirty)
                sAccountData[index].IsDirty = true;
            else if (initial)
                sAccountData[index].IsDirty = false;
        }
        
        public AccountDataEntry? GetAccountData(uint index)
        {
            if (index >= NUM_ACCOUNT_DATA_TYPES)
                return null;
            return sAccountData[index];
        }
        public void SendPacket(WorldPacket packet)
        {
            if (m_socket != null && m_socket.IsConnected())
            {
                m_socket.OutPacket(packet.GetOpcode(), packet.Size, packet.Contents);
            }
        }

        public void Disconnect()
        {
            if (m_socket != null && m_socket.IsConnected())
            {
                m_socket.OnDisconnect();
            }
        }

        public bool CanUseCommand(string cmdstr)
        {
            if (m_GMPermissions.Contains('a'))
                return true;
            if (cmdstr.Length <= 1)
                return m_GMPermissions.Contains(cmdstr);
            foreach (char c in cmdstr)
                if (!m_GMPermissions.Contains(c))
                    return false;
            return true;
        }

        public bool HasFlag(AccountFlagsEnum flag) => (m_accountFlags & (uint)flag) != 0;

        public static readonly Dictionary<ushort, Action<Session, WorldPacket>> Handlers = [];

        public static void InitHandlers()
        {
            if (Handlers.Count > 0)
            {
                CLog.Warning("[Session]", "Session handlers already initialized, skipping re-initialization.");
                return;
            }

            // Login
            Handlers[(ushort)Opcodes.CMSG_CHAR_ENUM]                = (s, p) => s.HandleCharEnumOpcode(p);
            Handlers[(ushort)Opcodes.CMSG_CHAR_CREATE]              = (s, p) => s.HandleCharacterCreate(p);
            Handlers[(ushort)Opcodes.CMSG_CHAR_DELETE]              = (s, p) => s.HandleCharacterDelete(p);
            Handlers[(ushort)Opcodes.CMSG_CHAR_RENAME]              = (s, p) => s.HandleCharacterRename(p);
            Handlers[(ushort)Opcodes.CMSG_PLAYER_LOGIN]             = (s, p) => s.HandlePlayerLogin(p);
            // Account Data
            Handlers[(ushort)Opcodes.CMSG_UPDATE_ACCOUNT_DATA]      = (s, p) => s.HandleUpdateAccountData(p);
            Handlers[(ushort)Opcodes.CMSG_REQUEST_ACCOUNT_DATA]     = (s, p) => s.HandleRequestAccountData(p);
            // Queries
            Handlers[(ushort)Opcodes.CMSG_CREATURE_QUERY]           = (s, p) => s.HandleCreatureQueryOpcode(p);
            Handlers[(ushort)Opcodes.CMSG_ITEM_QUERY_SINGLE]        = (s, p) => s.HandleItemQuerySingleOpcode(p);
            Handlers[(ushort)Opcodes.CMSG_ITEM_NAME_QUERY]	        = (s, p) => s.HandleItemNameQueryOpcode(p);
            Handlers[(ushort)Opcodes.CMSG_GAMEOBJECT_QUERY]         = (s, p) => s.HandleGameObjectQueryOpcode(p);
            Handlers[(ushort)Opcodes.CMSG_PAGE_TEXT_QUERY] 	        = (s, p) => s.HandlePageTextQueryOpcode(p);
            Handlers[(ushort)Opcodes.CMSG_NAME_QUERY]               = (s, p) => s.HandleNameQueryOpcode(p);
            Handlers[(ushort)Opcodes.CMSG_REALM_SPLIT]              = (s, p) => s.HandleRealmSplitQuery(p);
            Handlers[(ushort)Opcodes.CMSG_QUERY_TIME]               = (s, p) => s.HandleQueryTimeOpcode(p);
            // Channels
            Handlers[(ushort)Opcodes.CMSG_JOIN_CHANNEL]             = (s, p) => s.HandleChannelJoin(p);
            Handlers[(ushort)Opcodes.CMSG_LEAVE_CHANNEL]            = (s, p) => s.HandleChannelLeave(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_LIST]             = (s, p) => s.HandleChannelList(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_PASSWORD]         = (s, p) => s.HandleChannelPassword(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_SET_OWNER]        = (s, p) => s.HandleChannelSetOwner(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_OWNER]            = (s, p) => s.HandleChannelOwner(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_MODERATOR]        = (s, p) => s.HandleChannelModerator(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_UNMODERATOR]      = (s, p) => s.HandleChannelUnmoderator(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_MUTE]             = (s, p) => s.HandleChannelMute(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_UNMUTE]           = (s, p) => s.HandleChannelUnmute(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_INVITE]           = (s, p) => s.HandleChannelInvite(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_KICK]             = (s, p) => s.HandleChannelKick(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_BAN]              = (s, p) => s.HandleChannelBan(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_UNBAN]            = (s, p) => s.HandleChannelUnban(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_ANNOUNCEMENTS]    = (s, p) => s.HandleChannelAnnounce(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_MODERATE]         = (s, p) => s.HandleChannelModerate(p);
            Handlers[(ushort)Opcodes.CMSG_GET_CHANNEL_MEMBER_COUNT] = (s, p) => s.HandleChannelNumMembersQuery(p);
            Handlers[(ushort)Opcodes.CMSG_CHANNEL_DISPLAY_LIST]     = (s, p) => s.HandleChannelRosterQuery(p);
            Handlers[(ushort)Opcodes.CMSG_MESSAGECHAT]              = (s, p) => s.HandleMessagechatOpcode(p);

            CLog.Success("[Session]", $"Session handlers initialized ({Handlers.Count} opcodes).");
        }

        // Main packet dispatch loop (Update)
        public void Update()
        {
            bool errorPacket = false;
            while (m_readQueue.TryDequeue(out var packet))
            {
                if (packet == null)
                {
                    CLog.Error("Session::Update", R_E_SESSION);                    
                    break;
                }

                ushort opcode = packet.Opcode;
                string opcodeName = NameTables.LookupName(opcode, NameTables.OpcodeSharedNames);

                if (Handlers.TryGetValue(opcode, out var handler))
                {
                    try
                    {
                        CLog.Debug("[Session]", R_D_SESSION, opcodeName, opcode);
                        handler(this, packet);
                    }
                    catch (Exception ex)
                    {
                        CLog.Error("[Session]", $"Exception in handler for opcode {opcodeName} (0x{opcode:X}): {ex}");
                        errorPacket = true;
                    }
                }
                else
                {
                    CLog.Debug("[Session]", R_D_SESSION_1);
                    // Forward to world/cluster server if possible
                    bool forwarded = false;
                    if (m_server != null)
                    {
                        try
                        {
                            dynamic dynServer = m_server;
                            dynServer.SendWoWPacket(this, packet);
                            forwarded = true;
                        }
                        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                        {
                            var sendMethod = m_server.GetType().GetMethod("SendWoWPacket");
                            if (sendMethod != null)
                            {
                                sendMethod.Invoke(m_server, [this, packet]);
                                forwarded = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            CLog.Error("[Session]", $"Exception forwarding packet to world/cluster server: {ex}");
                        }
                    }
                    if (!forwarded)
                    {
                        CLog.Debug("[Session]", R_D_SESSION_2, opcodeName, opcode);
                        errorPacket = true;
                    }
                }

                if (opcode >= (ushort)Opcodes.NUM_MSG_TYPES)
                {
                    CLog.Error("[Session]", R_E_SESSION_1, opcodeName, opcode);
                    errorPacket = true;
                }

                if (errorPacket)
                {
                    if (packet is IDisposable disposable)
                        disposable.Dispose();
                    break; 
                }
            }
        }
    }
}
