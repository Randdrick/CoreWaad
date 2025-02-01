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
    public struct CreatureDisplayInfo
    {
        public uint ID;    // 1 - id
        // uint32 unk2; // 2 - ModelData column2?
        // uint32 unk3; // 3 - ExtraDisplayInfo column 18?
        // uint32 unk4; // 4
        public float Scale; // 5
        // uint32 unk6; // 6
        // public string DisplayTag1; // 7
        // public string DisplayTag2; // 8
        // public string DisplayTag3; // 9
        // public string DisplayTag4; // 10
        // uint32 unk11;
        // uint32 unk12;
        // uint32 unk13;
        // uint32 unk14;
        // uint32 unk15;
        // uint32 unk16;
    }
}