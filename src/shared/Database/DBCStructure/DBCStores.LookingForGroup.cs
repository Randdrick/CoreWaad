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
    public struct LookingForGroup
    {
        public uint ID;                       // 0
        // public string[] name;              // 1-17 Name lang
        public uint minlevel;                 // 18
        public uint maxlevel;                 // 19
        public uint reclevel;                 // 20
        public uint recminlevel;              // 21
        public uint recmaxlevel;              // 22
        public int map;                       // 23
        public uint difficulty;               // 24
        // public uint unk;                   // 25
        public uint type;                     // 26
        // public int unk2;                   // 27
        // public string unk3;                // 28
        public uint expansion;                // 29
        // public uint unk4;                  // 30
        public uint grouptype;                // 31
        // public string[] desc;              // 32-47 Description

        public uint GetEntry()
        {
            return ID + (type << 24);
        }
    }
}