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
    public struct DBCTaxiNode
    {
        public uint id;
        public uint mapid;
        public float x;
        public float y;
        public float z;
        // public uint name;
        // public uint namealt1;
        public uint name; // 8 frFR
        // public uint namealt3;
        // public uint namealt4;
        // public uint namealt5;
        // public uint namealt6;
        // public uint namealt7;
        // public uint namealt8;
        // public uint namealt9;
        // public uint namealt10;
        // public uint namealt11;
        // public uint namealt12;
        // public uint namealt13;
        // public uint namealt14;
        // public uint namealt15;
        // public uint nameflags;
        public uint horde_mount;
        public uint alliance_mount;
    }
}