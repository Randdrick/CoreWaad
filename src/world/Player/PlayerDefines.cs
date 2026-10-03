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

using System.Collections.Generic;

namespace WaadWorldServer;

public static class PlayerConstants
{
    public const int PLAYER_ACTION_BUTTON_COUNT = 144;
    public const int TAXIMASK_SIZE = 14;
    public const int TUTORIALS_COUNT = 8;
    public const int MAX_SPEC_COUNT = 2;
    public const int GLYPHS_COUNT = EPlayerFields.PLAYER_GLYPHS_ENABLED - EPlayerFields.PLAYER_FIELD_GLYPHS_1;
    public const int NUM_COOLDOWN_TYPES = 3;
    public const int SKILL_TYPE_LANGUAGE = 10;
    public const int SKILL_RIDING = 762;
}

public enum PlayerTeam : byte
{
    FACTION_ALLY = 0,
    FACTION_HORDE = 1,
}

public enum DeathState : uint
{
    ALIVE = 0,
    JUST_DIED = 1,
    CORPSE = 2,
    DEAD = 3,
}

public enum PlayerCooldownType : uint
{
    COOLDOWN_TYPE_SPELL = 0,
    COOLDOWN_TYPE_CATEGORY = 1,
    COOLDOWN_TYPE_ITEM = 2,
}

public struct PlayerSkill
{
    public uint Type;
    public uint CurrentValue;
    public uint MaximumValue;
}

public struct ActionButton
{
    public ushort Action;
    public byte Type;
    public byte Misc;
}

public struct LoginAura
{
    public uint Id;
    public uint Duration;
    public uint Flags;
}

public sealed class FactionReputation
{
    public int BaseStanding;
    public int Standing;
    public uint Flag;
}

public struct PlayerCooldown
{
    // Stocké en timestamp Unix (secondes), comme en base.
    public uint ExpireTime;
    public uint ItemId;
    public uint SpellId;
}

public sealed class PlayerSpec
{
    public Dictionary<uint, byte> Talents { get; } = [];
    public ushort[] Glyphs { get; } = new ushort[PlayerConstants.GLYPHS_COUNT];
}
