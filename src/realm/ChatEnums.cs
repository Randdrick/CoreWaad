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

namespace WaadRealmServer;

public enum ChatMsg
{
    CHAT_MSG_ADDON = -1,
    CHAT_MSG_SYSTEM = 0,
    CHAT_MSG_SAY = 1,
    CHAT_MSG_PARTY = 2,
    CHAT_MSG_RAID = 3,
    CHAT_MSG_GUILD = 4,
    CHAT_MSG_OFFICER = 5,
    CHAT_MSG_YELL = 6,
    CHAT_MSG_WHISPER = 7,
    CHAT_MSG_WHISPER_MOB = 8,
    CHAT_MSG_WHISPER_INFORM = 9,
    CHAT_MSG_EMOTE = 10,
    CHAT_MSG_TEXT_EMOTE = 11,
    CHAT_MSG_MONSTER_SAY = 12,
    CHAT_MSG_MONSTER_PARTY = 13,
    CHAT_MSG_MONSTER_YELL = 14,
    CHAT_MSG_MONSTER_WHISPER = 15,
    CHAT_MSG_MONSTER_EMOTE = 16,
    CHAT_MSG_CHANNEL = 17,
    CHAT_MSG_CHANNEL_JOIN = 18,
    CHAT_MSG_CHANNEL_LEAVE = 19,
    CHAT_MSG_CHANNEL_LIST = 20,
    CHAT_MSG_CHANNEL_NOTICE = 21,
    CHAT_MSG_CHANNEL_NOTICE_USER = 22,
    CHAT_MSG_AFK = 23,
    CHAT_MSG_DND = 24,
    CHAT_MSG_IGNORED = 25,
    CHAT_MSG_SKILL = 26,
    CHAT_MSG_LOOT = 27,
    CHAT_MSG_MONEY = 28,
    CHAT_MSG_OPENING = 29,
    CHAT_MSG_TRADESKILLS = 30,
    CHAT_MSG_PET_INFO = 31,
    CHAT_MSG_COMBAT_MISC_INFO = 32,
    CHAT_MSG_XP_GAIN = 33,
    CHAT_MSG_HONOR_GAIN = 34,
    CHAT_MSG_COMBAT_FACTION_CHANGE = 35,
    CHAT_MSG_BG_SYSTEM_NEUTRAL = 36,
    CHAT_MSG_BG_SYSTEM_ALLIANCE = 37,
    CHAT_MSG_BG_SYSTEM_HORDE = 38,
    CHAT_MSG_RAID_LEADER = 39,
    CHAT_MSG_RAID_WARNING = 40,
    CHAT_MSG_RAID_BOSS_EMOTE = 41,
    CHAT_MSG_RAID_BOSS_WHISPER = 42,
    CHAT_MSG_FILTERED = 43,
    CHAT_MSG_BATTLEGROUND = 44,
    CHAT_MSG_BATTLEGROUND_LEADER = 45,
    CHAT_MSG_RESTRICTED = 46,
    CHAT_MSG_ACHIEVEMENT = 47,
    CHAT_MSG_GUILD_ACHIEVEMENT = 48
}

public enum Languages
{
    LANG_ADDON = -1,
    LANG_UNIVERSAL = 0x00,
    LANG_ORCISH = 0x01,
    LANG_DARNASSIAN = 0x02,
    LANG_TAURAHE = 0x03,
    LANG_DWARVISH = 0x06,
    LANG_COMMON = 0x07,
    LANG_DEMONIC = 0x08,
    LANG_TITAN = 0x09,
    LANG_THELASSIAN = 0x0A,
    LANG_DRACONIC = 0x0B,
    LANG_KALIMAG = 0x0C,
    LANG_GNOMISH = 0x0D,
    LANG_TROLL = 0x0E,
    LANG_GUTTERSPEAK = 0x21,
    LANG_DRAENEI = 0x23,
    LANG_ZOMBIE = 0x24,
    LANG_GNOMISHBINARY = 0x25,
    LANG_GOBLINBINARY = 0x26,
    NUM_LANGUAGES = 0x27
}

public static class ChatColors
{
    public const string MSG_COLOR_LIGHTRED = "|cffff6060";
    public const string MSG_COLOR_LIGHTBLUE = "|cff00ccff";
    public const string MSG_COLOR_BLUE = "|cff0000ff";
    public const string MSG_COLOR_GREEN = "|cff00ff00";
    public const string MSG_COLOR_RED = "|cffff0000";
    public const string MSG_COLOR_GOLD = "|cffffcc00";
    public const string MSG_COLOR_GREY = "|cff888888";
    public const string MSG_COLOR_WHITE = "|cffffffff";
    public const string MSG_COLOR_SUBWHITE = "|cffbbbbbb";
    public const string MSG_COLOR_MAGENTA = "|cffff00ff";
    public const string MSG_COLOR_YELLOW = "|cffffff00";
    public const string MSG_COLOR_CYAN = "|cff00ffff";
}
