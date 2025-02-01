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
    public struct skilllinespell // SkillLineAbility.dbc
    {
        public uint Id;                       // 1
        public uint skilline;                 // 2
        public uint spell;                    // 3 
        public uint raceMask;                 // 4 // Val = 16 bits
        public uint classMask;                // 5 // Val = 16 bits
        public uint excludeRace;              // 6
        public uint excludeClass;             // 7
        public uint minSkillLineRank;         // 8
        public uint supercededBySpell;        // 9  // SpellId ?
        public uint acquireMethod;            // 10
        public uint trivialSkillLineRankHigh; // 11
        public uint trivialSkillLineRankLow;  // 12
        public uint abandonable;              // 13
        public uint reqTP;                    // 14
    }
}