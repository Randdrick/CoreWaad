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
    public struct AuctionHouseDBC
    {
        public uint id;    // 1
        public uint unk;   // 2
        public uint fee;   // 3
        public uint tax;   // 4
        // public string name; // 5
        // public string nameAlt1; // 6
        // public string nameAlt2; // 7
        // public string nameAlt3; // 8
        // public string nameAlt4; // 9
        // public string nameAlt5; // 10
        // public string nameAlt6; // 11
        // public string nameAlt7; // 12
        // public string nameAlt8; // 13
        // public string nameAlt9; // 14
        // public string nameAlt10; // 15
        // public string nameAlt11; // 16
        // public string nameAlt12; // 17
        // public string nameAlt13; // 18
        // public string nameAlt14; // 19
        // public string nameAlt15; // 20
        // public string nameFlags; // 21
    }
}