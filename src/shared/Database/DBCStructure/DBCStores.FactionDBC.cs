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
    public struct FactionDBC
    {
        public uint ID;                // 1
        public int RepListId;          // 2
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] baseRepMask;     // 3 - 6
        // uint32 unk1[4]; // 7 - 10
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public int[] baseRepValue;     // 11 - 14
        // uint32 unk2[4]; // 15 - 18
        public uint parentFaction;     // 19
        // uint32 unused; // 20 - 23
        // public string Name; // 24
        // uint32 unused; // 25
        public string Name;            // 26
        // uint32 unused; // 27 - 57
    }
}