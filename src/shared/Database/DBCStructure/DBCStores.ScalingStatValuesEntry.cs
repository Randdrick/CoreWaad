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
    public struct ScalingStatValuesEntry
    {
        // public uint Id;                 // 0
        public uint Level;                 // 1
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] ssdMultiplier;       // 2-5 Multiplier for ScalingStatDistribution
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] armorMod;            // 6-9 Armor for level
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public uint[] dpsMod;              // 10-15 DPS mod for level
        public uint spellBonus;            // 16 spell power for level
        public uint ssdMultiplier2;        // 17 there's data from 3.1 dbc ssdMultiplier[3]
        public uint ssdMultiplier3;        // 18 3.3
        // public uint unk2;               // 19 unk, probably also Armor for level (flag 0x80000?)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] armorMod2;           // 20-23 Low Armor for level
    }
}