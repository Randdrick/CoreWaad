/*
 * Ascent MMORPG Server
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

using System.Runtime.InteropServices;

public static partial class DBCStores
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AreaTable // 335.12340
    {
        public uint AreaId;         // 1
        public uint mapId;          // 2
        public uint ZoneId;         // 3
        public uint explorationFlag; // 4
        public uint AreaFlags;      // 5
        // uint32 unk2; // 6
        // uint32 unk3; // 7
        // uint32 unk4; // 8
        public uint EXP;            // 9 not XP
        // uint32 unk5; // 10
        public uint level;          // 11
        // public string name; // 12 enGB
        // uint32 nameAlt1; // 13
        public string name;         // 14 name en dbc Fr
        // uint32 nameAlt3; // 15
        // uint32 nameAlt4; // 16
        // uint32 nameAlt5; // 17
        // uint32 nameAlt6; // 18
        // uint32 nameAlt7; // 19
        // uint32 nameAlt8; // 20
        // uint32 nameAlt9; // 21
        // uint32 nameAlt10; // 22
        // uint32 nameAlt11; // 23
        // uint32 nameAlt12; // 24
        // uint32 nameAlt13; // 25
        // uint32 nameAlt14; // 26
        // uint32 nameAlt15; // 27
        // uint32 nameFlags; // 28
        public uint category;       // 29
        // uint32 unk7; // 30
        // uint32 unk8; // 31
        // uint32 unk9; // 32
        // uint32 unk10; // 33
        // int32 unk11; // 34
        // float unk12; // 35
        // uint32 unk13; // 36
    }
}