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
using System.Diagnostics;
using static System.Runtime.InteropServices.Marshal;

namespace WaadShared;

public class PreallocatedQueue<Type>
{
    private byte[] Buffer;
    private int m_readpos;
    private int m_writepos;
    private int m_buffersize;
    private readonly int type_size;
    private readonly int m_reallocsize;

    public PreallocatedQueue(int size, int realloc_size = 100)
    {
        // Create buffer
        type_size = SizeOf<Type>();
        Buffer = new byte[size * type_size];
        m_readpos = m_writepos = 0;
        m_buffersize = size * type_size;
        m_reallocsize = realloc_size;
    }

    public void PushBack(Type item)
    {
        if ((type_size + m_writepos) > m_buffersize)
            Reallocate(m_buffersize + (m_reallocsize * type_size));

        byte[] itemBytes = new byte[type_size];
        IntPtr ptr = AllocHGlobal(type_size);
        try
        {
            StructureToPtr(item, ptr, true);
            Copy(ptr, itemBytes, 0, type_size);
        }
        finally
        {
            FreeHGlobal(ptr);
        }
        Array.Copy(itemBytes, 0, Buffer, m_writepos, type_size);
        m_writepos += type_size;
    }

    public Type PopFront()
    {
        Debug.Assert((m_readpos + type_size) <= m_writepos);
        Type returner = default(Type);
        byte[] returnerBytes = new byte[type_size];
        Array.Copy(Buffer, m_readpos, returnerBytes, 0, type_size);
        m_readpos += type_size;

        // Clear buffer completely if we're at the end of the buffer
        if (m_readpos == m_writepos)
            Clear();

        return returner;
    }

    public void Clear()
    {
        m_readpos = m_writepos = 0;
    }

    public bool IsEmpty()
    {
        return m_readpos == m_writepos;
    }

    private void Reallocate(int size)
    {
        Array.Resize(ref Buffer, size);
        m_buffersize = size;
    }
}

public static class TypeExtensions
{
    public static int SizeOf<T>(this T obj)
    {
        return SizeOf(obj);
    }
}
