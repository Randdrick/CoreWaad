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
    public struct CharTitlesEntry
    {
        public uint ID;      // 1
        // uint32 unk1;  // 2
        // public string name;    // 3
        // uint32 unk;    // 4
        public string name;		// 5 335.12340 Fr
        // uint32 unk;   // 6 - 19
        // public string name2;   // 20 ;                              
        // uint32 unk3;  // 21 - 36                
        public uint bit_index; // 37
    }
}