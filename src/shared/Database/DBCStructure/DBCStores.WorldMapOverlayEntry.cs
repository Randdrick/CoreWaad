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
    public struct WorldMapOverlayEntry
    {
        public uint AreaReference;  // 1
        // unused              // 2
        public uint AreaTableID;    // 3
        // unused              // 4 - 8
        // public string InternalName;  // 9
        // public uint Width;               // 10
        // public uint Height;              // 11
        // public uint Left;                // 12
        // public uint Top;                 // 13
        // public uint Y1;                  // 14
        // public uint X1;                  // 15
        // public uint Y2;                  // 16
        // public uint X2;                  // 17
    }
}