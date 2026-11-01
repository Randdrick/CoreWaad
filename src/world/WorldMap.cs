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
using System.Linq;

namespace WaadWorldServer;

// Carte/instance : grille de cellules et visibilité entre joueurs (seuls les joueurs sont portés pour l'instant).
public sealed class WorldMap(uint mapId, uint instanceId)
{
    public const float CellSize = 533.33333f / 8;
    public const float VisibilityRange = 100.0f;

    // Ordre des verrous : Session.SyncRoot, puis _lock, puis le verrou de tampon du Player.
    private readonly object _lock = new();
    private readonly Dictionary<uint, Player> _players = [];
    private readonly Dictionary<(int X, int Y), List<Player>> _cells = [];

    public uint MapId { get; } = mapId;
    public uint InstanceId { get; } = instanceId;

    public int PlayerCount
    {
        get
        {
            lock (_lock)
                return _players.Count;
        }
    }

    public bool AddPlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        lock (_lock)
        {
            if (!_players.TryAdd(player.Guid, player))
                return false;

            AddToCell(player);
            UpdateVisibility(player);
            return true;
        }
    }

    public void RemovePlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        lock (_lock)
        {
            if (!_players.TryGetValue(player.Guid, out Player registered) || !ReferenceEquals(registered, player))
                return;

            _players.Remove(player.Guid);
            RemoveFromCell(player);
            foreach (uint guid in player.VisibleGuids)
            {
                if (!_players.TryGetValue(guid, out Player other))
                    continue;
                other.VisibleGuids.Remove(player.Guid);
                player.HideFrom(other);
            }
            player.VisibleGuids.Clear();
        }
    }

    public void MovePlayer(Player player, float x, float y, float z, float o)
    {
        ArgumentNullException.ThrowIfNull(player);
        lock (_lock)
        {
            if (!_players.TryGetValue(player.Guid, out Player registered) || !ReferenceEquals(registered, player))
                return;

            (int, int) oldCell = CellOf(player.PositionX, player.PositionY);
            player.SetPosition(x, y, z, o);
            if (oldCell != CellOf(x, y))
            {
                RemoveFromCell(player, oldCell);
                AddToCell(player);
            }
            UpdateVisibility(player);
        }
    }

    // Les paquets sont mis en file ; chaque destinataire les envoie depuis son propre thread de session.
    public void BroadcastValues(Player source, byte[] block)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_lock)
        {
            foreach (uint guid in source.VisibleGuids)
                if (_players.TryGetValue(guid, out Player other))
                    other.PushUpdateData(block, 1);
        }
    }

    // Les paquets sont mis en file ; chaque destinataire les envoie depuis son propre thread de session.
    public void BroadcastToVisible(Player source, ushort opcode, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_lock)
        {
            foreach (uint guid in source.VisibleGuids)
                if (_players.TryGetValue(guid, out Player other))
                    other.QueueDelayedPacket(opcode, data);
        }
    }

    private static (int X, int Y) CellOf(float x, float y) =>
        ((int)MathF.Floor(x / CellSize), (int)MathF.Floor(y / CellSize));

    private void AddToCell(Player player)
    {
        var key = CellOf(player.PositionX, player.PositionY);
        if (!_cells.TryGetValue(key, out List<Player> list))
            _cells[key] = list = [];
        list.Add(player);
    }

    private void RemoveFromCell(Player player) => RemoveFromCell(player, CellOf(player.PositionX, player.PositionY));

    private void RemoveFromCell(Player player, (int X, int Y) key)
    {
        if (!_cells.TryGetValue(key, out List<Player> list))
            return;
        list.Remove(player);
        if (list.Count == 0)
            _cells.Remove(key);
    }

    private static bool InRange(Player a, Player b)
    {
        float dx = a.PositionX - b.PositionX;
        float dy = a.PositionY - b.PositionY;
        return dx * dx + dy * dy <= VisibilityRange * VisibilityRange;
    }

    private void UpdateVisibility(Player player)
    {
        foreach (uint guid in player.VisibleGuids.ToArray())
        {
            if (!_players.TryGetValue(guid, out Player other))
            {
                player.VisibleGuids.Remove(guid);
                continue;
            }

            if (InRange(player, other))
                continue;

            player.VisibleGuids.Remove(guid);
            other.VisibleGuids.Remove(player.Guid);
            other.HideFrom(player);
            player.HideFrom(other);
        }

        var (cellX, cellY) = CellOf(player.PositionX, player.PositionY);
        int radius = (int)MathF.Ceiling(VisibilityRange / CellSize);
        for (int x = cellX - radius; x <= cellX + radius; x++)
        {
            for (int y = cellY - radius; y <= cellY + radius; y++)
            {
                if (!_cells.TryGetValue((x, y), out List<Player> list))
                    continue;

                foreach (Player other in list)
                {
                    if (ReferenceEquals(other, player) || player.VisibleGuids.Contains(other.Guid) || !InRange(player, other))
                        continue;

                    player.VisibleGuids.Add(other.Guid);
                    other.VisibleGuids.Add(player.Guid);
                    other.ShowTo(player);
                    player.ShowTo(other);
                }
            }
        }
    }
}
