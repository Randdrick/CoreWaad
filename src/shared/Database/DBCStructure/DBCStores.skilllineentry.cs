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
    public struct skilllineentry // SkillLine.dbc (56 colonnes 335.12340)
    {
        public uint id;             // 1
        public uint type;           // 2
        public uint unk1;           // 3
        // public string Name;       // 4
        // int32 NameAlt1;
        public string Name;         // 6 Name Fr
        // uint32 NameAlt2;
        // uint32 NameAlt3;
        // uint32 NameAlt4;
        // uint32 NameAlt5;
        // uint32 NameAlt6;
        // uint32 NameAlt7;
        // uint32 NameAlt8;
        // uint32 NameAlt9;
        // uint32 NameAlt10;
        // uint32 NameAlt11;
        // uint32 NameAlt12;
        // uint32 NameAlt13;
        // uint32 NameAlt14;
        // uint32 NameAlt15;
        // uint32 NameFlags;
        // uint32 Description;
        // uint32 DescriptionAlt1;
        // uint32 DescriptionAlt2;
        // uint32 DescriptionAlt3;
        // uint32 DescriptionAlt4;
        // uint32 DescriptionAlt5;
        // uint32 DescriptionAlt6;
        // uint32 DescriptionAlt7;
        // uint32 DescriptionAlt8;
        // uint32 DescriptionAlt9;
        // uint32 DescriptionAlt10;
        // uint32 DescriptionAlt11;
        // uint32 DescriptionAlt12;
        // uint32 DescriptionAlt13;
        // uint32 DescriptionAlt14;
        // uint32 DescriptionAlt15;
        // uint32 DescriptionFlags; // 38
        // uint32 unk2;
    }
}