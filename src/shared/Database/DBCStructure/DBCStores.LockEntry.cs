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
    public struct LockEntry
    {
        public uint Id;          // 1
        public uint[] locktype;  // 2 - 6 // 0 - no lock, 1 - item needed for lock, 2 - min lockping skill needed
        // uint32 unused         // 7 - 9
        public uint[] lockmisc;  // 10 - 14 // if type is 1 here is a item to unlock, else is unknow for now
        // uint32 unused         // 15 - 17
        public uint[] minlockskill; // 18 - 22 // min skill in lockpiking to unlock.
        // uint32 unused        // 23 - 33
    }
}