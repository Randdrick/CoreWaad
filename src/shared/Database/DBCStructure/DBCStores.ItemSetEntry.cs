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
    public struct ItemSetEntry // 335.12340
    {
        public uint id;                  // 1
        // public string name;           // 2 enGB
        // 3
        public string name;              // 4 Fr
        // uint32 unused_shit[15];       // 5 - 17
        public uint flag;                // 18
        public uint[] itemid;            // 19 - 26
        // uint32 more_unused_shit[7];   // 29 - 35
        public uint[] SpellID;           // 36 - 43
        public uint[] itemscount;        // 36 - 43
        public uint RequiredSkillID;     // 52
        public uint RequiredSkillAmt;    // 53
        /*
        Id
        Name
        ItemID[17]
        SetSpellID[8]
        SetThreshold[8] 
        RequiredSkill 
        RequiredSkillRank
        */
    }
}