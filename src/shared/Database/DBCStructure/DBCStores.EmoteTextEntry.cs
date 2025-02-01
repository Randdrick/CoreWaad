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
    public struct EmoteTextEntry
    {
        public uint Id;      // 1
        // uint32 name;      // 2
        public uint textid;  // 3
        public uint textid2; // 4
        public uint textid3; // 5
        public uint textid4; // 6
        // uint32 unk1;      // 7
        public uint textid5; // 8
        // uint32 unk2;      // 9
        public uint textid6; // 10
        // uint32 unk3;
        public uint textid7; // 12
        public uint textid8; // 13
        // uint32 unk6;
        // uint32 unk7;
        public uint textid9; // 16
        // uint32 unk9;
        // uint32 unk10;
        // uint32 unk11;
    }
}