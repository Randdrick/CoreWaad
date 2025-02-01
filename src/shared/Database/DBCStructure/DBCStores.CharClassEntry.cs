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
    public struct CharClassEntry
    {
        public uint class_id;
        // uint32 unk1;
        public uint power_type;
        // uint32 unk2;
        // public string name;
        // uint32 namealt1;
        public string name; // 7 en Fr
        // uint32 namealt2;
        // uint32 namealt3;
        // uint32 namealt4;
        // uint32 namealt5;
        // uint32 namealt6;
        // uint32 namealt7;
        // uint32 namealt8;
        // uint32 namealt9;
        // uint32 namealt10;
        // uint32 namealt11;
        // uint32 namealt12;
        // uint32 namealt13;
        // uint32 namealt14;
        // uint32 namealt15;
        // uint32 nameflags;
        // uint32 unk3;
        // uint32 unk4;
        // uint32 unk5;
    }
}