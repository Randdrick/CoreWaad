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
public class CircularQueue<T>
{
    private readonly T[] m_elements;
    private int m_pos;
    private readonly int ELEMENTCOUNT;

    public CircularQueue(int elementCount)
    {
        ELEMENTCOUNT = elementCount;
        m_elements = new T[ELEMENTCOUNT];
        m_pos = 0;
    }

    public void Push(T val)
    {
        m_elements[m_pos] = val;
        m_pos = (m_pos + 1) % ELEMENTCOUNT;
    }

    public T[] Get()
    {
        return m_elements;
    }

    public void Print()
    {
        Console.Write("Elements of CircularQueue[{0}]: ", ELEMENTCOUNT);
        foreach (var element in m_elements)
        {
            Console.Write(element + " ");
        }
        Console.WriteLine();
    }
}
