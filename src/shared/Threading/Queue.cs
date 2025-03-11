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

public class FQueue<T>
{
    private class Node
    {
        public T Value;
        public Node Next;
    }

    private Node first;
    private Node last;
    private int size;
    private readonly object lockObject = new();
    private readonly AutoResetEvent cond = new(false);

    public FQueue()
    {
        first = last = null;
        size = 0;
    }

    public int GetSize()
    {
        lock (lockObject)
        {
            return size;
        }
    }

    public void Push(T item)
    {
        Node p = new() { Value = item, Next = null };

        lock (lockObject)
        {
            if (last != null)
            {
                last.Next = p;
                last = p;
                size++;
            }
            else
            {
                last = first = p;
                size = 1;
                cond.Set();
            }
        }
    }

    public T PopNoWait()
    {
        lock (lockObject)
        {
            if (size == 0)
            {
                return default;
            }

            Node tmp = first;
            if (tmp == null)
            {
                return default;
            }

            if (--size > 0)
            {
                first = first.Next;
            }
            else
            {
                first = last = null;
            }

            T returnVal = tmp.Value;
            return returnVal;
        }
    }

    public T Pop()
    {
        lock (lockObject)
        {
            while (size == 0)
            {
                Monitor.Wait(lockObject);
            }

            Node tmp = first;
            if (tmp == null)
            {
                return default;
            }

            if (--size > 0)
            {
                first = first.Next;
            }
            else
            {
                first = last = null;
            }

            T returnVal = tmp.Value;
            return returnVal;
        }
    }

    public AutoResetEvent GetCond()
    {
        return cond;
    }
}
