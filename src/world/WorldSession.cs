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
using WaadShared;

using static WaadShared.WorldSessionMessages;

namespace WaadWorldServer;

public static partial class WorldSession
{
    public const uint PlayerLogoutDelayMs = 20000;

    public static bool LogoutPlayer(ClusterPlayerSession session, bool save = true, bool notifyRealm = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (session.SyncRoot)
        {
            if (session.IsLoggingOut)
                return false;
            Player player = session.Player;
            if (player == null)
                return true;

            session.IsLoggingOut = true;
            try
            {
                if (save && player.IsLoaded && !player.SaveToDB())
                {
                    session.SetLogoutTimer(10000);
                    CLog.Error("[WorldSession]", string.Format(W_E_WORLDSESS_DELAYED_DISCONNECT_SAVE_FAILED, player.Guid));
                    return false;
                }

                player.RemoveFromWorld();
                session.Player = null;
                session.RecentLogout = true;
                session.RemovalRequested = true;
                session.SetLogoutTimer(0);
                if (notifyRealm && !session.IsDisconnected)
                {
                    using var complete = new WorldPacket((ushort)Opcodes.SMSG_LOGOUT_COMPLETE, 0);
                    ClusterInterface.Instance.ForwardWoWPacket(session.SessionId, complete);
                    ClusterInterface.Instance.NotifyPlayerLogout(session.SessionId, player.Guid);
                }
                return true;
            }
            finally
            {
                session.IsLoggingOut = false;
            }
        }
    }

    public static int Update(ClusterPlayerSession session, uint instanceId)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (session.SyncRoot)
        {
            if (session.IsDisposed || session.RemovalRequested || (session.IsDisconnected && session.Player == null))
                return 1;
            if (session.InstanceId != instanceId)
                return 2;

            long previousTimeMs = session.CurrentTimeMs;
            session.CurrentTimeMs = Environment.TickCount64;
            while (session.IncomingPackets.TryDequeue(out var packet))
            {
                try
                {
                    if (!session.IsDisconnected)
                        DispatchPacket(session, packet);
                }
                finally
                {
                    packet.Dispose();
                }

                if (session.IsDisposed || session.RemovalRequested)
                    return 1;
                if (session.InstanceId != instanceId)
                    return 2;
            }

            if (session.LogoutDeadlineMs != 0 && session.CurrentTimeMs >= session.LogoutDeadlineMs)
            {
                bool beingPushed = session.Player?.IsBeingPushed == true;
                bool pushTimedOut = session.IsDisconnected && session.CurrentTimeMs - session.DisconnectedAtMs >= 120000;
                if ((!beingPushed || pushTimedOut) && LogoutPlayer(session))
                    return 1;
            }

            uint elapsedMs = previousTimeMs == 0 ? 0 : (uint)Math.Clamp(session.CurrentTimeMs - previousTimeMs, 0, uint.MaxValue);
            session.Player?.Update(elapsedMs);
            return 0;
        }
    }

    public static void DispatchPacket(ClusterPlayerSession session, WorldPacket packet)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(packet);

        lock (session.SyncRoot)
        {
            if (!session.IsDisposed && !session.IsDisconnected && !session.RemovalRequested)
                DispatchPacketCore(session, packet);
        }
    }

    private static void DispatchPacketCore(ClusterPlayerSession session, WorldPacket packet)
    {
        ushort opcode = packet.GetOpcode();
        if (opcode >= (ushort)Opcodes.NUM_MSG_TYPES)
        {
            CLog.Error("[WorldSession]", string.Format(W_E_WORLDSESS_OUT_OF_RANGE_OPCODE, opcode, session.SessionId));
            return;
        }

        string opcodeName = NameTables.LookupName(opcode, NameTables.OpcodeSharedNames);

        if (!PacketHandlers.TryGetValue(opcode, out var handler))
        {
            CLog.Warning("[WorldSession]", string.Format(W_W_WORLDSESS_NO_HANDLER, opcodeName, opcode, session.SessionId));
            return;
        }

        if (handler.Status == SessionStatus.STATUS_IGNORED)
            return;

        if (session.Player == null &&
            (handler.Status == SessionStatus.STATUS_LOGGEDIN ||
             (handler.Status == SessionStatus.STATUS_IN_OR_LOGGINGOUT && !session.RecentLogout)))
        {
            CLog.Warning("[WorldSession]", "Paquet {0} (0x{1:X4}) ignoré: joueur non connecté, session {2}.",
                opcodeName, opcode, session.SessionId);
            return;
        }

        packet.Opcodename = opcodeName;
        try
        {
            CLog.Debug("[WorldSession]", string.Format(W_D_WORLDSESS_PROCESSING_PACKET, opcodeName, opcode, session.SessionId));
            handler.Callback(session, packet);
            if (handler.Status == SessionStatus.STATUS_AUTHED && opcode != (ushort)Opcodes.CMSG_SET_ACTIVE_VOICE_CHANNEL)
                session.RecentLogout = false;
        }
        catch (ByteBufferException ex)
        {
            CLog.Error("[WorldSession]", string.Format(W_E_WORLDSESS_MALFORMED_PACKET, opcodeName, opcode, session.SessionId, ex.Message));
        }
        catch (Exception ex)
        {
            CLog.Error("[WorldSession]", string.Format(W_E_WORLDSESS_EXCEPTION_IN_HANDLER, opcodeName, opcode, session.SessionId, ex));
        }
    }
}
