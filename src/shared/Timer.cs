/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2025 WAAD Team <https://arbonne.games-rpg.net/>
 *
 * From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore.
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

using System;

namespace WaadShared;

public static class WowTimer
{
    // getMSTimeDiff calculates the difference between two timestamps in milliseconds
    public static uint GetMSTimeDiff(uint oldMSTime, uint newMSTime)
    {
        // Handle the case where the timer has overflowed
        if (oldMSTime > newMSTime)
            return 0xFFFFFFFF - oldMSTime + newMSTime;
        else
            return newMSTime - oldMSTime;
    }

    // Get the number of milliseconds since Windows started (limited to 49.7 days)
    public static uint GetMSTime()
    {
#if WINDOWS
        return (uint)Environment.TickCount64;
#else
        var timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1);
        return (uint)timeSpan.TotalMilliseconds;
#endif
    }

    // Alternative method to get the number of milliseconds since Windows started
    public static uint GetMSTime2()
    {
        uint getTickMs = (uint)Environment.TickCount64; // Milliseconds since Windows started
        long getNormalTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds; // Seconds since Unix epoch
        return (uint)(getNormalTime * 1000 + (getTickMs & 0x000003FF)); // Combine both values
    }
}
