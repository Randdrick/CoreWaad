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
 
using System.Threading;

namespace WaadShared.Threading;

public class RWLock
{
    private readonly object lockObject = new();
    private readonly AutoResetEvent cond = new(false);
    private int readers;
    private int writers;

    public RWLock()
    {
        readers = 0;
        writers = 0;
    }

    public void AcquireReadLock()
    {
        Monitor.Enter(lockObject);
        readers++;
        Monitor.Exit(lockObject);
    }

    public void ReleaseReadLock()
    {
        Monitor.Enter(lockObject);
        if (--readers == 0 && writers > 0)
        {
            cond.Set();
        }
        Monitor.Exit(lockObject);
    }

    public void AcquireWriteLock()
    {
        Monitor.Enter(lockObject);
        writers++;
        while (readers > 0)
        {
            Monitor.Wait(lockObject);
        }
    }

    public void ReleaseWriteLock()
    {
        if (--writers == 0)
        {
            cond.Set();
        }
        Monitor.Exit(lockObject);
    }
}
