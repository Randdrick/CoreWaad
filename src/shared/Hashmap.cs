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

namespace WaadShared;

public static class HashMapConstants
{
    public const int MAP_MISSING = -3;  // No such element
    public const int MAP_FULL = -2;     // Hashmap is full
    public const int MAP_OMEM = -1;     // Out of Memory
    public const int MAP_OK = 0;        // OK
}
public class HashMap<TKey, TValue>
{
    private const int InitialSize = 1024;
    private int tableSize;
    private int size;
    private List<HashMapElement> data;

    private class HashMapElement
    {
        public TKey Key { get; set; }
        public bool InUse { get; set; }
        public TValue Data { get; set; }
    }

    public HashMap()
    {
        tableSize = InitialSize;
        size = 0;
        data = [.. new HashMapElement[InitialSize]];
    }

    private int Hash(TKey key)
    {
        int hash = key.GetHashCode();

        // Robert Jenkins' 32 bit Mix Function
        hash += hash << 12;
        hash ^= hash >> 22;
        hash += hash << 4;
        hash ^= hash >> 9;
        hash += hash << 10;
        hash ^= hash >> 2;
        hash += hash << 7;
        hash ^= hash >> 12;

        // Knuth's Multiplicative Method
        hash = (int)((hash >> 3) * 2654435761);

        return Math.Abs(hash % tableSize);
    }

    private int FindIndex(TKey key)
    {
        int curr = Hash(key);
        for (int i = 0; i < tableSize; i++)
        {
            if (data[curr] == null || !data[curr].InUse)
                return curr;

            if (data[curr].InUse && EqualityComparer<TKey>.Default.Equals(data[curr].Key, key))
                return curr;

            curr = (curr + 1) % tableSize;
        }

        return HashMapConstants.MAP_FULL; 
    }

    private void Rehash()
    {
        var oldData = data;
        tableSize *= 2;
        size = 0;
        data = [.. new HashMapElement[tableSize]];

        foreach (var element in oldData)
        {
            if (element != null && element.InUse)
            {
                Put(element.Key, element.Data);
            }
        }
    }

    public int Put(TKey key, TValue value)
    {
        int index = FindIndex(key);
        while (index == HashMapConstants.MAP_FULL)
        {
            try
            {
                Rehash();
            }
            catch (OutOfMemoryException)
            {
                return HashMapConstants.MAP_OMEM;
            }
            index = FindIndex(key);
        }

        if (data[index] == null)
        {
            data[index] = new HashMapElement();
        }

        data[index].Key = key;
        data[index].Data = value;
        data[index].InUse = true;
        size++;

        return HashMapConstants.MAP_OK;
    }

    public bool Get(TKey key, out TValue value)
    {
        int curr = Hash(key);
        for (int i = 0; i < tableSize; i++)
        {
            if (data[curr] != null && data[curr].InUse && EqualityComparer<TKey>.Default.Equals(data[curr].Key, key))
            {
                value = data[curr].Data;
                return true;
            }

            curr = (curr + 1) % tableSize;
        }

        value = default;
        return false;
    }

    public bool GetAtIndex(int index, out TKey key, out TValue value)
    {
        int count = 0;
        for (int i = 0; i < tableSize; i++)
        {
            if (data[i] != null && data[i].InUse)
            {
                if (count == index)
                {
                    key = data[i].Key;
                    value = data[i].Data;
                    return true;
                }
                count++;
            }
        }

        key = default;
        value = default;
        return false;
    }

    public int Remove(TKey key)
    {
        int curr = Hash(key);

        for (int i = 0; i < tableSize; i++)
        {
            if (data[curr] != null && data[curr].InUse && EqualityComparer<TKey>.Default.Equals(data[curr].Key, key))
            {
                // Blank out the fields
                data[curr].InUse = false;
                data[curr].Data = default;
                data[curr].Key = default;

                // Reduce the size
                size--;
                return HashMapConstants.MAP_OK;
            }

            curr = (curr + 1) % tableSize;
        }

        // Data not found
        return HashMapConstants.MAP_MISSING;
    }

    public int Length => size;
}

public class HashMap64<TValue>
{
    private const int InitialSize = 1024;
    private int tableSize;
    private int size;
    private List<HashMapElement64> data;

    private class HashMapElement64
    {
        public long Key { get; set; }
        public bool InUse { get; set; }
        public TValue Data { get; set; }
    }

    public HashMap64()
    {
        tableSize = InitialSize;
        size = 0;
        data = [.. new HashMapElement64[InitialSize]];
    }

    private int Hash(long key)
    {
        key = (~key) + (key << 18);
        key ^= key >> 31;
        key *= 21;
        key ^= key >> 11;
        key += key << 6;
        key ^= key >> 22;

        return (int)(key % tableSize);
    }

    private int FindIndex(long key)
    {
        int curr = Hash(key);
        for (int i = 0; i < tableSize; i++)
        {
            if (data[curr] == null || !data[curr].InUse)
                return curr;

            if (data[curr].InUse && data[curr].Key == key)
                return curr;

            curr = (curr + 1) % tableSize;
        }

        return HashMapConstants.MAP_FULL;
    }

    private void Rehash()
    {
        var oldData = data;
        tableSize *= 2;
        size = 0;
        data = [.. new HashMapElement64[tableSize]];

        foreach (var element in oldData)
        {
            if (element != null && element.InUse)
            {
                Put(element.Key, element.Data);
            }
        }
    }

    public int Put(long key, TValue value)
    {
        int index = FindIndex(key);
        while (index == HashMapConstants.MAP_FULL)
        {
            try
            {
                Rehash();
            }
            catch (OutOfMemoryException)
            {
                return HashMapConstants.MAP_OMEM;
            }
            index = FindIndex(key);
        }

        if (data[index] == null)
        {
            data[index] = new HashMapElement64();
        }

        data[index].Key = key;
        data[index].Data = value;
        data[index].InUse = true;
        size++;

        return HashMapConstants.MAP_OK;
    }

    public bool Get(long key, out TValue value)
    {
        int curr = Hash(key);
        for (int i = 0; i < tableSize; i++)
        {
            if (data[curr] != null && data[curr].InUse && data[curr].Key == key)
            {
                value = data[curr].Data;
                return true;
            }

            curr = (curr + 1) % tableSize;
        }

        value = default;
        return false;
    }

    public bool GetAtIndex(int index, out long key, out TValue value)
    {
        int count = 0;
        for (int i = 0; i < tableSize; i++)
        {
            if (data[i] != null && data[i].InUse)
            {
                if (count == index)
                {
                    key = data[i].Key;
                    value = data[i].Data;
                    return true;
                }
                count++;
            }
        }

        key = default;
        value = default;
        return false;
    }

    public int Remove(long key)
    {
        int curr = Hash(key);

        for (int i = 0; i < tableSize; i++)
        {
            if (data[curr] != null && data[curr].InUse && data[curr].Key == key)
            {
                // Blank out the fields
                data[curr].InUse = false;
                data[curr].Data = default;
                data[curr].Key = default;

                // Reduce the size
                size--;
                return HashMapConstants.MAP_OK;
            }

            curr = (curr + 1) % tableSize;
        }

        // Data not found
        return HashMapConstants.MAP_MISSING;
    }

    public int Length => size;
}
