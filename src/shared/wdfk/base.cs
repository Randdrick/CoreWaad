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

using System.Collections.Generic;

namespace WDFK;

public class ArrayAllocator<T>
{
    protected uint m_Size;
    protected uint m_Capacity;
    protected uint m_Growth;
    protected T[] m_Array;

    public ArrayAllocator()
    {
        m_Size = 0;
        m_Capacity = 0;
        m_Growth = 4;
        m_Array = null;
    }

    public ArrayAllocator(uint grow)
    {
        m_Size = 0;
        m_Capacity = 0;
        m_Growth = grow;
        m_Array = null;
    }

    public virtual void Clear(bool destruct)
    {
        if (destruct)
        {
            if (m_Capacity > 0)
            {
                m_Capacity = m_Size = 0;
                m_Array = null;
            }
        }
        else if (m_Size > 0)
        {
            m_Size = 0;
            if (m_Capacity > m_Growth)
            {
                m_Capacity = m_Growth;
                m_Array = new T[m_Capacity];
            }
        }
    }

    public ArrayAllocator<T> Assign(ArrayAllocator<T> arg)
    {
        Clear(false);

        m_Size = arg.m_Size;
        m_Capacity = arg.m_Capacity;

        T[] to = new T[arg.m_Capacity];
        T[] from = arg.m_Array;

        for (uint i = 0; i < m_Size; ++i)
            to[i] = from[i];

        m_Array = to;
        return this;
    }

    public void Clear()
    {
        Clear(false);
    }

    public bool Empty()
    {
        return m_Size == 0;
    }

    public uint Size()
    {
        return m_Size;
    }

    protected void Init(ref T[] var)
    {
        m_Array = var;
        var = null;
    }

    public void SetGrowth(uint grow)
    {
        m_Growth = grow;
    }

    public ArrayAllocator<T> Insert(T key)
    {
        Increase();
        m_Array[m_Size - 1] = key;
        return this;
    }

    public ArrayAllocator<T> Insert(uint index, T key)
    {
        if (index > m_Size)
            Increase((int)(index - m_Size));
        else
            Increase();

        for (uint i = index + 1; i < m_Size; ++i)
            m_Array[i] = m_Array[i - 1];

        m_Array[index] = key;
        return this;
    }

    public ArrayAllocator<T> Remove(uint index)
    {
        Decrease(index);
        return this;
    }

    private void Increase()
    {
        uint os = m_Size;
        uint oc = m_Capacity;
        if (++m_Size > m_Capacity)
        {
            T[] old = m_Array;
            m_Capacity += m_Growth;
            m_Array = new T[m_Capacity];
            for (uint i = 0; i < os; ++i)
                m_Array[i] = old[i];
            if (os > 0 || oc > 0)
                old = null;
        }
    }

    private void Increase(int quantum)
    {
        while (quantum-- > 0)
            Increase();
    }

    private void Decrease(uint pivot)
    {
        if (--m_Size < (m_Capacity - m_Growth))
        {
            T[] old = m_Array;
            m_Capacity -= m_Growth;
            m_Array = new T[m_Capacity];
            for (uint i = 0; i < m_Size; ++i)
                m_Array[i] = (i < pivot) ? old[i] : old[i + 1];
            old = null;
        }
        else
        {
            for (uint i = pivot; i < m_Size; ++i)
                m_Array[i] = m_Array[i + 1];
        }
    }
}

// Définition des comparateurs de base
public class Comparable<T>
{
    public virtual bool Eq(T left, T right)
    {
        return EqualityComparer<T>.Default.Equals(left, right);
    }

    public virtual bool Gt(T left, T right)
    {
        return Comparer<T>.Default.Compare(left, right) > 0;
    }

    public virtual bool GtOrEq(T left, T right)
    {
        return Comparer<T>.Default.Compare(left, right) >= 0;
    }

    public virtual bool Lt(T left, T right)
    {
        return Comparer<T>.Default.Compare(left, right) < 0;
    }

    public virtual bool LtOrEq(T left, T right)
    {
        return Comparer<T>.Default.Compare(left, right) <= 0;
    }
}

public class StringComparable : Comparable<string>
{
    public override bool Eq(string left, string right) => left.Equals(right);
    public override bool Gt(string left, string right) => left.CompareTo(right) > 0;
    public override bool GtOrEq(string left, string right) => left.CompareTo(right) >= 0;
    public override bool Lt(string left, string right) => left.CompareTo(right) < 0;
    public override bool LtOrEq(string left, string right) => left.CompareTo(right) <= 0;
}

public static class HashMapper
{
    private static uint[] hashMap = [0x12345678, 0x87654321];

    public static uint[] HashMap
    {
        get { return hashMap; }
        set { hashMap = value; }
    }
}

// Définition des calculs de hachage de base
public class Hashable<T>
{
    public virtual uint HashCode(T key)
    {
        int sz = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        uint c = 0;
        for (int i = 0; i < sz; ++i)
        {
            c ^= HashMapper.HashMap[i % 2];
            c ^= (uint)sz << i;
            c += 61;
        }
        return c;
    }
}

public class IntHashable : Hashable<int>
{
    public override uint HashCode(int key)
    {
        uint c2 = HashMapper.HashMap[0];
        uint arg = (uint)key;
        arg = arg ^ 61 ^ (arg >> 16);
        arg += arg << 3;
        arg ^= arg >> 4;
        arg *= c2;
        arg ^= arg >> 15;
        return arg;
    }
}

public class UInt32Hashable : Hashable<uint>
{
    public override uint HashCode(uint key)
    {
        uint c2 = HashMapper.HashMap[1];
        key = key ^ 61 ^ (key >> 16);
        key += key << 3;
        key ^= key >> 4;
        key *= c2;
        key ^= key >> 15;
        return key;
    }
}

public class StringHashable : Hashable<string>
{
    public override uint HashCode(string key)
    {
        uint c = HashMapper.HashMap[0];
        for (int i = 0; i < key.Length; ++i)
        {
            c ^= (uint)(key[i] << (4 % (i + 1)));
        }
        c = c ^ 61 ^ (c >> 16);
        c += c << 3;
        c ^= c >> 4;
        c *= HashMapper.HashMap[1];
        c ^= c >> 15;
        return c;
    }
}

public abstract class PrimitiveType<T>(T arg)
{
    protected T m_Content = arg;

    public abstract override string ToString();
}

public class Int<T>(T arg) : PrimitiveType<T>(arg)
{
    public override string ToString()
    {
        return m_Content.ToString();
    }
}

public class Pointer<T>(T arg) : PrimitiveType<T>(arg)
{
    public override string ToString()
    {
        return m_Content.ToString();
    }
}

public abstract class DictionaryBase<K, V, HK, CK, Entry>
{
    public abstract Entry GetNextEntry(Entry arg);
    public abstract Entry GetPreviousEntry(Entry arg);
}
