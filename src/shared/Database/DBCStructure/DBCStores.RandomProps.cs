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
    public struct RandomProps
    {
        public uint ID;
        // uint32 name1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] spells;
        // uint32 unk1;
        // uint32 unk2;
        // uint32 name2;
        // uint32 RankAlt1;
        // uint32 RankAlt2;
        // uint32 RankAlt3;
        // uint32 RankAlt4;
        // uint32 RankAlt5;
        // uint32 RankAlt6;
        // uint32 RankAlt7;
        // uint32 RankAlt8;
        // uint32 RankAlt9;
        // uint32 RankAlt10;
        // uint32 RankAlt11;
        // uint32 RankAlt12;
        // uint32 RankAlt13;
        // uint32 RankAlt14;
        // uint32 RankFlags;
    }
}