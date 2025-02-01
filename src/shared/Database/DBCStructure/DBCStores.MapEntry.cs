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
    public struct MapEntry
    {
        public uint id;                    // 1
        public string name_internal;       // 2
        public uint map_type;              // 3
        // 4 - unused
        public uint IsPvPZone;             // 5 - 3.3.2
        // public string real_name;        // 6 - 3.0.9 enGB
        // 7 - unused
        public string real_name;           // 8 - 335.12340 Fr
        // 9 - 22 unused
        public uint linked_zone;           // 23
        // public string hordeIntro;       // 24 - 3.0.9 enGB
        // 25 - 
        public string hordeIntro;          // 26 - 3.3.5.12340 Fr
        // 27 - 40 unused
        // public string allianceIntro;    // 41 - 3.0.9 enGB
        // 42
        public string allianceIntro;       // 43 - 3.3.5 Fr
        // 44 - 57 unused
        public uint multimap_id;           // 58 
        // 59 - unused
        public uint parent_map;            // 60 map_id of parent map
        public float start_x;              // 61 enter x coord (if single entry)
        public float start_y;              // 62 enter y coord (if single entry)
        // 63 - unused
        public uint addon;                 // 64 (0-WoW original, 1-tbc expansion -2 WowTLK )
        // 65 - unused
        public uint MaxPlayer;             // 66
    }
}