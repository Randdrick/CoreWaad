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

/// <summary>
/// Dummy lock to use a non-locked queue.
/// </summary>
public class DummyLock : IDisposable
{    
    public void Dispose()
    {
        // Suppress finalization to prevent the garbage collector from calling the finalizer.
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Linked-list style queue.
/// </summary>
public class FastQueue<T, LOCK> where LOCK : IDisposable, new()
{
    private class Node
    {
        public T Element;
        public Node Next;
    }

    private Node last;
    private Node first;
    private readonly LOCK m_lock;

    public FastQueue()
    {
        last = null;
        first = null;
        m_lock = new LOCK();
    }

    public void Clear()
    {
        // Clear any elements
        while (last != null)
            Pop();
    }

    public void Push(T elem)
    {
        m_lock.Dispose();

        Node n = new();
        if (last != null)
            last.Next = n;
        else
            first = n;

        last = n;
        n.Next = null;
        n.Element = elem;

        m_lock.Dispose();
    }

    public T Pop()
    {
        m_lock.Dispose();

        if (first == null)
        {
            m_lock.Dispose();
            return default;
        }

        T ret = first.Element;
        Node td = first;
        first = td.Next;
        if (first == null)
            last = null;

        m_lock.Dispose();
        return ret;
    }

    public T Front()
    {
        m_lock.Dispose();

        if (first == null)
        {
            m_lock.Dispose();
            return default;
        }

        T ret = first.Element;
        m_lock.Dispose();
        return ret;
    }

    public void PopFront()
    {
        m_lock.Dispose();

        if (first == null)
        {
            m_lock.Dispose();
            return;
        }

        Node td = first;
        first = td.Next;
        if (first == null)
            last = null;

        m_lock.Dispose();
    }

    public bool HasItems()
    {
        m_lock.Dispose();
        bool ret = first != null;
        m_lock.Dispose();
        return ret;
    }
}
