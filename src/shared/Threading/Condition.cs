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
using System.Collections.Generic;
using System.Threading;

namespace WaadShared.Threading;

public class Condition(object externalMutex)
{
    private readonly object _lock = new();
    private readonly Queue<ManualResetEvent> _waitSet = new();
    private readonly object _externalMutex = externalMutex;
    private int _lockCount;

    public void BeginSynchronized()
    {
        Monitor.Enter(_externalMutex);
        ++_lockCount;
    }

    public void EndSynchronized()
    {
        if (_lockCount <= 0) throw new InvalidOperationException("Lock count is zero or negative.");
        --_lockCount;
        Monitor.Exit(_externalMutex);
    }

    public bool Wait(TimeSpan timeout)
    {
        if (!Monitor.IsEntered(_externalMutex)) throw new InvalidOperationException("Lock not held by calling thread.");

        ManualResetEvent waitEvent = new(false);
        lock (_lock)
        {
            _waitSet.Enqueue(waitEvent);
        }

        int thisThreadsLockCount = _lockCount;
        _lockCount = 0;

        for (int i = 0; i < thisThreadsLockCount; ++i)
        {
            Monitor.Exit(_externalMutex);
        }

        bool signaled = waitEvent.WaitOne(timeout);

        for (int j = 0; j < thisThreadsLockCount; ++j)
        {
            Monitor.Enter(_externalMutex);
        }

        _lockCount = thisThreadsLockCount;
        waitEvent.Close();

        return signaled;
    }

    public void Wait()
    {
        Wait(Timeout.InfiniteTimeSpan);
    }

    public void Signal()
    {
        ManualResetEvent waitEvent = null;
        lock (_lock)
        {
            if (_waitSet.Count > 0)
            {
                waitEvent = _waitSet.Dequeue();
            }
        }

        waitEvent?.Set();
    }

    public void Broadcast()
    {
        List<ManualResetEvent> eventsToSignal = new();
        lock (_lock)
        {
            while (_waitSet.Count > 0)
            {
                eventsToSignal.Add(_waitSet.Dequeue());
            }
        }

        foreach (var waitEvent in eventsToSignal)
        {
            waitEvent.Set();
        }
    }

    private bool LockHeldByCallingThread()
    {
        bool lockTaken = false;
        try
        {
            Monitor.TryEnter(_externalMutex, ref lockTaken);
            if (!lockTaken || _lockCount == 0)
            {
                if (lockTaken) Monitor.Exit(_externalMutex);
                return false;
            }
            Monitor.Exit(_externalMutex);
            return true;
        }
        catch (SynchronizationLockException)
        {
            return false;
        }
    }
}
