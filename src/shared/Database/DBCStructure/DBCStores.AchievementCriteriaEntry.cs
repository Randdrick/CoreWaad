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
    public struct AchievementCriteriaEntry
    {
        public uint ID;                                             // 1
        public uint referredAchievement;                            // 2
        public uint requiredType;                                   // 3
        public uint MainRequirement;                                // 4
        public uint MainRequirementCount;                           // 5
        public uint AdditionnalRequirement1_Type;                   // 6
        public uint AdditionnalRequirement1_Value;                  // 7
        public uint AdditionnalRequirement2_Type;                   // 8
        public uint AdditionnalRequirement2_Value;                  // 9
        // public string name;                                      // 10  enGB
        // unused                                                   // 11
        public string name;                                         // 12 Fr
        // 13-26 
        public uint completionFlag;                                 // 27
        public uint groupFlag;                                      // 28
        // public string Descript;                                  // 29 non c'est uint32 avec 332.11403 (Branruz), Id de quelque chose (335.12340)
        public uint timeLimit;                                      // 30 time limit in seconds
        // uint32 unk1;                                             // 31
    }
}