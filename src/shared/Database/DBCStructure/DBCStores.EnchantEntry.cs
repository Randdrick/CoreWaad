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
    public struct EnchantEntry
    {
        public uint Id;                 //  1
        // uint32 Unk2;               //  2
        public uint[] type;            //  3 -  5 // Valeur = 8 Max en 332.11403 - 335.12340
        public int[] min;              //  6 -  8 For compat, in practice min==max
        public int[] max;              //  9 - 11
        public uint[] spell;           // 12 - 14
        // public string Name;        // 15 enGB
        // uint32 NameAlt;            // 16 
        public string Name;            // 17 Fr
        // 18 - 30
        // uint32 NameFlags;          // 31
        public uint visual;             // 32
        public uint EnchantGroups;      // 33
        public uint GemEntry;           // 34
        public uint unk7;               // Gem Related  // 35
        public uint unk8;               // Gem Related  // 36
        public uint unk9;               // Gem Related  // 37
        public uint LevelMin;           // 38
    }
}