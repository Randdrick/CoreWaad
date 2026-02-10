/*
 * Ascent MMORPG Server (C# port)
 * Copyright (C) 2005-2008 Ascent Team <http://www.ascentcommunity.com/>
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

namespace WaadShared
{
    public class NameTableEntry(uint id, string name)
    {
        public uint Id { get; set; } = id;
        public string Name { get; set; } = name;
    }

    public static class NameTables
    {
        public static string LookupName(uint id, NameTableEntry[] table)
        {
            foreach (var entry in table)
            {
                if (entry != null && entry.Id == id)
                    return entry.Name;
            }
            return $"UNKNOWN_{id}";
        }

        // To be filled with actual opcode names elsewhere
        public static readonly NameTableEntry[] OpcodeSharedNames =
        [
            // Login
            new((uint)Opcodes.CMSG_CHAR_ENUM, "CMSG_CHAR_ENUM"),
            new((uint)Opcodes.CMSG_CHAR_CREATE, "CMSG_CHAR_CREATE"),
            new((uint)Opcodes.CMSG_CHAR_DELETE, "CMSG_CHAR_DELETE"),
            new((uint)Opcodes.CMSG_CHAR_RENAME, "CMSG_CHAR_RENAME"),
            new((uint)Opcodes.CMSG_PLAYER_LOGIN, "CMSG_PLAYER_LOGIN"),
            // Account Data
            new((uint)Opcodes.CMSG_UPDATE_ACCOUNT_DATA, "CMSG_UPDATE_ACCOUNT_DATA"),
            new((uint)Opcodes.CMSG_REQUEST_ACCOUNT_DATA, "CMSG_REQUEST_ACCOUNT_DATA"),
            // Queries
            new((uint)Opcodes.CMSG_CREATURE_QUERY, "CMSG_CREATURE_QUERY"),
            new((uint)Opcodes.CMSG_ITEM_QUERY_SINGLE, "CMSG_ITEM_QUERY_SINGLE"),
            new((uint)Opcodes.CMSG_ITEM_NAME_QUERY, "CMSG_ITEM_NAME_QUERY"),
            new((uint)Opcodes.CMSG_GAMEOBJECT_QUERY, "CMSG_GAMEOBJECT_QUERY"),
            new((uint)Opcodes.CMSG_PAGE_TEXT_QUERY, "CMSG_PAGE_TEXT_QUERY"),
            new((uint)Opcodes.CMSG_NAME_QUERY, "CMSG_NAME_QUERY"),
            new((uint)Opcodes.CMSG_REALM_SPLIT, "CMSG_REALM_SPLIT"),
            new((uint)Opcodes.CMSG_QUERY_TIME, "CMSG_QUERY_TIME"),
            // Channels
            new((uint)Opcodes.CMSG_JOIN_CHANNEL, "CMSG_JOIN_CHANNEL"),
            new((uint)Opcodes.CMSG_LEAVE_CHANNEL, "CMSG_LEAVE_CHANNEL"),
            new((uint)Opcodes.CMSG_CHANNEL_LIST, "CMSG_CHANNEL_LIST"),
            new((uint)Opcodes.CMSG_CHANNEL_PASSWORD, "CMSG_CHANNEL_PASSWORD"),
            new((uint)Opcodes.CMSG_CHANNEL_SET_OWNER, "CMSG_CHANNEL_SET_OWNER"),
            new((uint)Opcodes.CMSG_CHANNEL_OWNER, "CMSG_CHANNEL_OWNER"),
            new((uint)Opcodes.CMSG_CHANNEL_MODERATOR, "CMSG_CHANNEL_MODERATOR"),
            new((uint)Opcodes.CMSG_CHANNEL_UNMODERATOR, "CMSG_CHANNEL_UNMODERATOR"),
            new((uint)Opcodes.CMSG_CHANNEL_MUTE, "CMSG_CHANNEL_MUTE"),
            new((uint)Opcodes.CMSG_CHANNEL_UNMUTE, "CMSG_CHANNEL_UNMUTE"),
            new((uint)Opcodes.CMSG_CHANNEL_INVITE, "CMSG_CHANNEL_INVITE"),
            new((uint)Opcodes.CMSG_CHANNEL_KICK, "CMSG_CHANNEL_KICK"),
            new((uint)Opcodes.CMSG_CHANNEL_BAN, "CMSG_CHANNEL_BAN"),
            new((uint)Opcodes.CMSG_CHANNEL_UNBAN, "CMSG_CHANNEL_UNBAN"),
            new((uint)Opcodes.CMSG_CHANNEL_ANNOUNCEMENTS, "CMSG_CHANNEL_ANNOUNCEMENTS"),
            new((uint)Opcodes.CMSG_CHANNEL_MODERATE, "CMSG_CHANNEL_MODERATE"),
            new((uint)Opcodes.CMSG_GET_CHANNEL_MEMBER_COUNT, "CMSG_GET_CHANNEL_MEMBER_COUNT"),
            new((uint)Opcodes.CMSG_CHANNEL_DISPLAY_LIST, "CMSG_CHANNEL_DISPLAY_LIST"),
            new((uint)Opcodes.CMSG_MESSAGECHAT, "CMSG_MESSAGECHAT"),
        ];
        public static NameTableEntry[] LogonOpcodeNames = [];
        public static NameTableEntry[] PluginOpcodeNames = [];
    }
}
