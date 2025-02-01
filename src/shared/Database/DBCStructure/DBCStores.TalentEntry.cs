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
    public struct TalentEntry
    {
        public uint TalentID;       // 1
        public uint TalentTree;     // 2
        public uint Row;            // 3
        public uint Col;            // 4
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public uint[] RankID;       // 5 - 9
        // uint32 unused; // 10 - 13
        public uint DependsOn;      // 14
        // uint32 unused; // 15 - 16
        public uint DependsOnRank;  // 17
        // uint32 unused; // 18 - 19
        public uint DependsOnSpell; // 20
        // uint32 unused; // 21 - 23
    }
}