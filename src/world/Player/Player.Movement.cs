/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2027 WAAD Team <https://arbonne.games-rpg.net/>
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
using System.Buffers.Binary;
using System.Collections.Generic;
using WaadShared;

namespace WaadWorldServer;

// Portage minimal du traitement des paquets MSG_MOVE_* (sans anti-cheat ni collision).
public sealed partial class Player
{
    private const int MovementPositionOffset = 4 + 2 + 4; // flags + flags2 + time
    private const int MovementMinSize = MovementPositionOffset + 4 * sizeof(float);
    private const int MovementMaxSize = 128;
    private const uint MoveFlagOnTransport = 0x00000200;
    private const float MaxCoordinate = 17066.0f;

    // Modifié uniquement sous le verrou de WorldMap.
    internal HashSet<uint> VisibleGuids { get; } = [];
    internal WorldMap CurrentMap { get; private set; }

    internal void SetPosition(float x, float y, float z, float o)
    {
        PositionX = x;
        PositionY = y;
        PositionZ = z;
        Orientation = o;
    }

    internal bool HandleMovement(WorldPacket packet)
    {
        if (!IsInWorld || CurrentMap == null || packet.Size < MovementMinSize || packet.Size > MovementMaxSize)
            return false;

        byte[] data = packet.Contents;
        uint moveFlags = BinaryPrimitives.ReadUInt32LittleEndian(data);
        float x = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(MovementPositionOffset));
        float y = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(MovementPositionOffset + 4));
        float z = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(MovementPositionOffset + 8));
        float o = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(MovementPositionOffset + 12));
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(o)
            || MathF.Abs(x) > MaxCoordinate || MathF.Abs(y) > MaxCoordinate)
            return false;

        // Sur un transport, les coordonnées sont relatives : on ne met pas la position à jour.
        if ((moveFlags & MoveFlagOnTransport) == 0)
            CurrentMap.MovePlayer(this, x, y, z, o);

        using var relay = new WorldPacket(packet.GetOpcode(), data.Length + 9);
        WritePackedGuid(relay, Guid);
        relay.WriteBytes(data);
        CurrentMap.BroadcastToVisible(this, packet.GetOpcode(), relay.Contents);
        return true;
    }

    internal void EnterMap()
    {
        CurrentMap = MapMgr.Instance.AddPlayer(this);
    }

    internal void LeaveMap()
    {
        CurrentMap?.RemovePlayer(this);
        CurrentMap = null;
        ClearPendingUpdates();
    }
}
