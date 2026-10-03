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
using System.Collections.Concurrent;
using System.Threading;
using WaadShared;

namespace WaadWorldServer;

public enum SessionStatus
{
    STATUS_AUTHED,
    STATUS_LOGGEDIN,
    STATUS_IN_OR_LOGGINGOUT,
    STATUS_IGNORED
}

public static partial class WorldSession
{
    private sealed record OpcodeHandler(SessionStatus Status, Action<ClusterPlayerSession, WorldPacket> Callback);

    private static readonly ConcurrentDictionary<ushort, OpcodeHandler> PacketHandlers = CreatePacketHandlers();
    private static int s_initialized;

    private static ConcurrentDictionary<ushort, OpcodeHandler> CreatePacketHandlers()
    {
        return new ConcurrentDictionary<ushort, OpcodeHandler>
        {
            [(ushort)Opcodes.CMSG_PING] = new(SessionStatus.STATUS_AUTHED, HandlePingOpcode),
            [(ushort)Opcodes.CMSG_REALM_SPLIT] = new(SessionStatus.STATUS_AUTHED, HandleRealmSplitOpcode),
            [(ushort)Opcodes.CMSG_LOGOUT_REQUEST] = new(SessionStatus.STATUS_LOGGEDIN, HandleLogoutRequestOpcode),
            [(ushort)Opcodes.CMSG_LOGOUT_CANCEL] = new(SessionStatus.STATUS_LOGGEDIN, HandleLogoutCancelOpcode)
        };
    }

    public static void RegisterPacketHandler(Opcodes opcode, Action<ClusterPlayerSession, WorldPacket> handler)
    {
        RegisterPacketHandler(opcode, handler, SessionStatus.STATUS_LOGGEDIN);
    }

    public static void RegisterPacketHandler(Opcodes opcode, Action<ClusterPlayerSession, WorldPacket> handler, SessionStatus status)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ushort opcodeValue = (ushort)opcode;
        if (opcodeValue >= (ushort)Opcodes.NUM_MSG_TYPES)
            throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Opcode client invalide.");
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (!PacketHandlers.TryAdd(opcodeValue, new OpcodeHandler(status, handler)))
            throw new InvalidOperationException($"Un gestionnaire est déjà enregistré pour l'opcode {opcode} (0x{opcodeValue:X4}).");
    }

    public static void InitPacketHandlerTable()
    {
        if (Interlocked.Exchange(ref s_initialized, 1) == 0)
            CLog.Success("[WorldSession]", "Table des gestionnaires de paquets initialisée ({0} gestionnaires).", PacketHandlers.Count);
    }

    private static void HandlePingOpcode(ClusterPlayerSession session, WorldPacket packet)
    {
        uint sequence = packet.ReadUInt32();
        using var response = new WorldPacket((ushort)Opcodes.SMSG_PONG, 4);
        response.WriteUInt32(sequence);
        ClusterInterface.Instance.ForwardWoWPacket(session.SessionId, response);
    }

    private static void HandleLogoutRequestOpcode(ClusterPlayerSession session, WorldPacket packet)
    {
        Player player = session.Player;
        using var response = new WorldPacket((ushort)Opcodes.SMSG_LOGOUT_RESPONSE, 5);
        bool instant = player.IsResting || player.TaxiPath != 0;
        bool denied = !instant && (player.IsInCombat || player.IsDueling);
        response.WriteUInt32(denied ? 1u : 0u);
        response.WriteByte(instant ? (byte)1 : (byte)0);
        ClusterInterface.Instance.ForwardWoWPacket(session.SessionId, response);
        if (denied || session.IsDisconnected || session.LogoutDeadlineMs != 0)
            return;
        session.SetLogoutTimer(instant ? 1000u : PlayerLogoutDelayMs);
        if (!instant)
            SetLogoutRoot(session, true);
    }

    private static void HandleLogoutCancelOpcode(ClusterPlayerSession session, WorldPacket packet)
    {
        if (session.IsDisconnected || session.LogoutDeadlineMs == 0)
            return;
        session.SetLogoutTimer(0);
        using var response = new WorldPacket((ushort)Opcodes.SMSG_LOGOUT_CANCEL_ACK, 0);
        ClusterInterface.Instance.ForwardWoWPacket(session.SessionId, response);
        SetLogoutRoot(session, false);
    }

    private static void SetLogoutRoot(ClusterPlayerSession session, bool rooted)
    {
        if (!rooted && !session.Player.IsLogoutRooted)
            return;
        session.Player.IsLogoutRooted = rooted;
        var guid = new WoWGuid((ulong)session.Guid);
        using var packet = new WorldPacket((ushort)(rooted ? Opcodes.SMSG_FORCE_MOVE_ROOT : Opcodes.SMSG_FORCE_MOVE_UNROOT), 13);
        packet.WriteByte(guid.GetNewGuidMask());
        packet.Append(guid.GetNewGuid(), guid.GetNewGuidLen());
        packet.WriteUInt32(rooted ? 1u : 5u);
        ClusterInterface.Instance.ForwardWoWPacket(session.SessionId, packet);
    }

    private static void HandleRealmSplitOpcode(ClusterPlayerSession session, WorldPacket packet)
    {
        uint value = packet.ReadUInt32();
        using var response = new WorldPacket((ushort)Opcodes.SMSG_REALM_SPLIT, 17);
        response.WriteUInt32(value);
        response.WriteUInt32(0);
        response.WriteString("01/01/01");
        ClusterInterface.Instance.ForwardWoWPacket(session.SessionId, response);
    }
}