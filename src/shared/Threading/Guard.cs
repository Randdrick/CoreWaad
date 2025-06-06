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

namespace WaadShared.Threading;

public class Guard : IDisposable
{
    private Mutex _mutex;

    public Guard(Mutex mutex)
    {
        _mutex = mutex;
        _mutex.WaitOne();
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        GC.SuppressFinalize(this);
    }

    public Guard Assign(Guard src)
    {
        this._mutex = src._mutex;
        return this;
    }
}
