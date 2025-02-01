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
    public struct CreatureFamilyEntry
    {
        public uint ID;            // 1
        public float minsize;      // 2
        public uint minlevel;      // 3
        public float maxsize;      // 4
        public uint maxlevel;      // 5
        public uint skilline;      // 6
        public uint tameable;      // 7 - second skill line - 270 Generic
        public uint petdietflags;  // 8
        public int pettalenttype;  // 9
        // 10 - unsused
        // 11 - unsused
        // 12 - unused
        public string name;        // 13 - NameFr
        // uint32 namealt1; // 14
        // uint32 namealt2; // 15
        // uint32 namealt3; // 16
        // uint32 namealt4; // 17
        // uint32 namealt5; // 18
        // uint32 namealt6; // 19
        // uint32 namealt7; // 20
        // uint32 namealt8; // 21
        // uint32 namealt9; // 22
        // uint32 namealt10; // 23
        // uint32 namealt11; // 24
        // uint32 namealt12; // 25
        // uint32 namealt13; // 26
        // uint32 namealt14; // 27
        // uint32 nameflags; // 28
        // uint32 iconID; // interface icon
    }
}