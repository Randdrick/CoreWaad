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
    public struct AchievementEntry
    {
        public uint ID;                // 1
        public uint factionFlag;       // 2 -1=all, 0=horde, 1=alliance
        public uint mapID;             // 3 -1=none
        public uint PrevAchievementId; // 4 
        // public string name;         // 5
        // Unk                         // 6
        public string name;            // 7 Fr
        // Unk                         // 8 à 21
        // public string Description;  // 22  enGB
        // description                 // 23    
        public string Description;     // 22 Fr
        // à 38 
        public uint categoryId;        // 39
        public uint points;            // 40 reward points
        public uint OrderInCategory;   // 41
        public uint flags;             // 42
        public uint is_statistic;      // 43
        // public string RewardName;   // 44 enGB
        // unused                      // 45
        public string RewardName;      // 46 Fr
        // unused                      // 47 à 60
        public uint Count;             // 61       
        public uint refAchievement;    // 62

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public uint[] AssociatedCriteria; // Custom stuff
        public uint AssociatedCriteriaCount;
    }
}