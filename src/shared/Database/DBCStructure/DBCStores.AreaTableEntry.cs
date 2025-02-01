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
    public struct AreaTableEntry
    {
        public uint id;                       // 0
        public int rootId;                    // 1
        public int adtId;                     // 2
        public int groupId;                   // 3
        // uint field4;
        // uint field5;
        // uint field6;
        // uint field7;
        // uint field8;
        public uint flags;                    // 9
        public uint areaId;                   // 10  ref -> AreaTableEntry
        // public string Name;                // 16
        // public uint nameflags;
    }
}