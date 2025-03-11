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
using System.Threading;

namespace WaadShared.Threading
{
    public class Mutex : IDisposable
    {
        private readonly object _lockObject = new();

        public void WaitOne()
        {
            Monitor.Enter(_lockObject);
        }

        public void ReleaseMutex()
        {
            Monitor.Exit(_lockObject);
        }

        public void Dispose()
        {
            // Nothing to dispose explicitly
            GC.SuppressFinalize(this);
        }
    }

    public class CriticalSection : IDisposable
    {
        private readonly object _lockObject = new();

        public void Enter()
        {
            Monitor.Enter(_lockObject);
        }

        public void Leave()
        {
            Monitor.Exit(_lockObject);
        }

        public void Dispose()
        {
            // Nothing to dispose explicitly
            GC.SuppressFinalize(this);
        }
    }

    public class FastMutex : IDisposable
    {
        private int _lock;
        private int _recursiveCount;

        public FastMutex()
        {
            _lock = 0;
            _recursiveCount = 0;
        }

        public void Acquire()
        {
            int threadId = Environment.CurrentManagedThreadId;
            if (threadId == _lock)
            {
                _recursiveCount++;
                return;
            }

            while (true)
            {
                int owner = Interlocked.CompareExchange(ref _lock, threadId, 0);
                if (owner == 0)
                    break;

                Thread.Yield();
            }

            _recursiveCount++;
        }

        public bool AttemptAcquire()
        {
            int threadId = Environment.CurrentManagedThreadId;
            if (threadId == _lock)
            {
                _recursiveCount++;
                return true;
            }

            int owner = Interlocked.CompareExchange(ref _lock, threadId, 0);
            if (owner == 0)
            {
                _recursiveCount++;
                return true;
            }

            return false;
        }

        public void Release()
        {
            if (--_recursiveCount == 0)
                Interlocked.Exchange(ref _lock, 0);
        }

        public void Dispose()
        {
            // Nothing to dispose explicitly
            GC.SuppressFinalize(this);
        }
    }
}


