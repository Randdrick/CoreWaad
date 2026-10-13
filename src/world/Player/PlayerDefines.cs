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
    public const int MAX_DAILY_QUESTS = EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1_24 - EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1 + 1;
    public const int MAX_SPEC_COUNT = 2;
    public const int GLYPHS_COUNT = EPlayerFields.PLAYER_GLYPHS_ENABLED - EPlayerFields.PLAYER_FIELD_GLYPHS_1;
    public const int NUM_COOLDOWN_TYPES = 3;
    public const int SKILL_TYPE_SECONDARY = 9;
    public const int SKILL_TYPE_LANGUAGE = 10;
    public const int SKILL_TYPE_PROFESSION = 11;
    public const int SKILL_RIDING = 762;
    public const uint SKILL_AXES = 44;
    public const uint SKILL_BOWS = 45;
    public const uint SKILL_GUNS = 46;
    public const uint SKILL_MACES = 54;
    public const uint SKILL_SWORDS = 43;
    public const uint SKILL_2H_SWORDS = 55;
    public const uint SKILL_STAVES = 136;
    public const uint SKILL_2H_MACES = 160;
    public const uint SKILL_2H_AXES = 172;
    public const uint SKILL_DAGGERS = 173;
    public const uint SKILL_THROWN = 176;
    public const uint SKILL_CROSSBOWS = 226;
    public const uint SKILL_SPEARS = 227;
    public const uint SKILL_POLEARMS = 229;
    public const uint SKILL_PLATE_MAIL = 293;
    public const uint SKILL_FISHING = 356;
    public const uint SKILL_MAIL = 413;
    public const uint SKILL_LEATHER = 414;
    public const uint SKILL_CLOTH = 415;
    public const uint SKILL_SHIELD = 433;
    public const uint SKILL_FIST_WEAPONS = 473;
    public const uint SKILL_WANDS = 228;

    public static bool TryGetProficiencyForSkill(uint skillId, out byte itemClass, out uint subclassMask)
    {
        (itemClass, subclassMask) = skillId switch
        {
            SKILL_CLOTH => ((byte)4, 2u),
            SKILL_LEATHER => ((byte)4, 4u),
            SKILL_MAIL => ((byte)4, 8u),
            SKILL_PLATE_MAIL => ((byte)4, 16u),
            SKILL_SHIELD => ((byte)4, 64u),
            SKILL_AXES => ((byte)2, 1u),
            SKILL_2H_AXES => ((byte)2, 2u),
            SKILL_BOWS => ((byte)2, 4u),
            SKILL_GUNS => ((byte)2, 8u),
            SKILL_MACES => ((byte)2, 16u),
            SKILL_2H_MACES => ((byte)2, 32u),
            SKILL_POLEARMS => ((byte)2, 64u),
            SKILL_SWORDS => ((byte)2, 128u),
            SKILL_2H_SWORDS => ((byte)2, 256u),
            SKILL_STAVES => ((byte)2, 1024u),
            SKILL_FIST_WEAPONS => ((byte)2, 8192u),
            SKILL_DAGGERS => ((byte)2, 32768u),
            SKILL_THROWN => ((byte)2, 65536u),
            SKILL_SPEARS => ((byte)2, 131072u),
            SKILL_CROSSBOWS => ((byte)2, 262144u),
            SKILL_WANDS => ((byte)2, 524288u),
            SKILL_FISHING => ((byte)2, 1048576u),
            _ => ((byte)0, 0u)
        };

        return itemClass != 0;
    }
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
