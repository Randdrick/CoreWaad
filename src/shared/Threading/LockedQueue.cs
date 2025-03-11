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

namespace WaadShared.Threading;

public class LockedQueue<T>
{
    private readonly Queue<T> _queue = new();
    private readonly Mutex _mutex = new();

    public void Add(T element)
    {
        _mutex.WaitOne();
        _queue.Enqueue(element);
        _mutex.ReleaseMutex();
    }

    public T Next()
    {
        _mutex.WaitOne();
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException("Queue is empty");
        }
        T element = _queue.Dequeue();
        _mutex.ReleaseMutex();
        return element;
    }

    public int Size()
    {
        _mutex.WaitOne();
        int count = _queue.Count;
        _mutex.ReleaseMutex();
        return count;
    }

    public bool Empty()
    {
        _mutex.WaitOne();
        bool isEmpty = _queue.Count == 0;
        _mutex.ReleaseMutex();
        return isEmpty;
    }

    public T GetFirstElement()
    {
        _mutex.WaitOne();
        T element;
        if (_queue.Count == 0)
        {
            element = default(T);
        }
        else
        {
            element = _queue.Peek();
        }
        _mutex.ReleaseMutex();
        return element;
    }

    public void Pop()
    {
        _mutex.WaitOne();
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException("Queue is empty");
        }
        _queue.Dequeue();
        _mutex.ReleaseMutex();
    }
}
