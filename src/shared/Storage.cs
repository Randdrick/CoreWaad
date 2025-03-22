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
using System.Data.SqlClient;
using static WaadShared.Common;

namespace WaadShared;

public class StorageAllocationPool<T> where T : new()
{
    private T[][] _pool;
    private int _count;
    internal int _max;

    public void Init(int count)
    {
        _pool = new T[count + 100][];
        _count = 0;
        _max = count + 100;
    }

    public T[] Get()
    {
        if (_count >= _max)
        {
            Console.WriteLine("StorageAllocationPool Get() failed!");
            return default;
        }

        return _pool[_count++];
    }

    public void Free()
    {
        _pool = null;
    }
}

public abstract class StorageContainerIterator<T>
{
    protected T Pointer;

    public T Get() => Pointer;

    public void Set(T p) => Pointer = p;

    public bool AtEnd() => Pointer == null;

    public abstract bool Inc();
}

public class ArrayStorageContainer<T> where T : class, new()
{
    private T[][] _array;
    internal int _max;
    // Propriété publique pour accéder à _array
    public T[][] Array => _array;
    private StorageAllocationPool<T> _pool;

    public void InitPool(int cnt)
    {
        _pool = new StorageAllocationPool<T>();
        _pool.Init(cnt);
    }

    public StorageContainerIterator<T> MakeIterator()
    {
        return new ArrayStorageIterator<T>(this);
    }

    public void Setup(int max)
    {
        _array = new T[max][];
        _max = max;
    }

    public void Resetup(int max)
    {
        if (max < _max)
            return;

        var newArray = new T[max][];
        
        System.Array.Copy(_array, newArray, _max);
        _array = newArray;
        _max = max;
    }

    public T AllocateEntry(int entry)
    {
        if (entry >= _max || _array[entry] != null)
            return default;

        _array[entry] = _pool.Get();
        return _array[entry][0];
    }

    public bool DeallocateEntry(int entry)
    {
        if (entry >= _max || _array[entry] == null)
            return false;

        _array[entry] = null;
        return true;
    }

    public T LookupEntry(int entry)
    {
        if (entry >= _max)
            return default;
        return _array[entry][0];
    }

    public bool SetEntry(int entry, T pointer)
    {
        if (entry > _max)
            return false;

        _array[entry] = [pointer];
        return true;
    }

    public T LookupEntryAllocate(int entry)
    {
        T ret = LookupEntry(entry);
        ret ??= AllocateEntry(entry);
        return ret;
    }

    public void Clear()
    {
        for (int i = 0; i < _max; ++i)
        {
            _array[i] = null;
        }
    }
}

public class HashMapStorageContainer<T> where T : new()
{
    internal Dictionary<int, T> _map = [];
    private StorageAllocationPool<T> _pool;

    public void InitPool(int cnt)
    {
        _pool = new StorageAllocationPool<T>();
        _pool.Init(cnt);
    }

    public StorageContainerIterator<T> MakeIterator()
    {
        return new HashMapStorageIterator<T>(this);
    }

    public T AllocateEntry(int entry)
    {
        if (_map.ContainsKey(entry))
            return default;

        T[] nArray = _pool.Get();
        T n = nArray != null ? nArray[0] : default;
        _map[entry] = n;
        return n;
    }

    public bool DeallocateEntry(int entry)
    {
        if (!_map.ContainsKey(entry))
            return false;

        _map.Remove(entry);
        return true;
    }

    public T LookupEntry(int entry)
    {
        if (!_map.TryGetValue(entry, out T value))
            return default;
        return value;
    }

    public bool SetEntry(int entry, T pointer)
    {
        if (!_map.ContainsKey(entry))
        {
            _map[entry] = pointer;
            return true;
        }

        _map[entry] = pointer;
        return true;
    }

    public T LookupEntryAllocate(int entry)
    {
        T ret = LookupEntry(entry);
        ret ??= AllocateEntry(entry);
        return ret;
    }

    public void Clear()
    {
        _map.Clear();
    }
}

public class ArrayStorageIterator<T> : StorageContainerIterator<T> where T : class, new()
{
    private readonly ArrayStorageContainer<T> _source;
    private int _myIndex;

    public ArrayStorageIterator(ArrayStorageContainer<T> source)
    {
        _source = source;
        _myIndex = 0;
        GetNextElement();
    }

    public override bool Inc()
    {
        GetNextElement();
        return Pointer != null;
    }

    private void GetNextElement()
    {
        while (_myIndex < _source._max)
        {
            // Iterate over the inner array
            for (int i = 0; i < _source.Array[_myIndex].Length; i++)
            {
                if (_source.Array[_myIndex][i] != null)
                {
                    Set(_source.Array[_myIndex][i]);
                    ++_myIndex;
                    return;
                }
            }
            ++_myIndex;
        }
        Set(default);
    }
}

public class HashMapStorageIterator<T> : StorageContainerIterator<T> where T : new()
{
    private readonly HashMapStorageContainer<T> _source;
    private readonly IEnumerator<KeyValuePair<int, T>> _itr;

    public HashMapStorageIterator(HashMapStorageContainer<T> source)
    {
        _source = source;
        _itr = _source._map.GetEnumerator();
        if (_itr.MoveNext())
            Set(_itr.Current.Value);
        else
            Set(default);
    }

    public override bool Inc()
    {
        if (_itr.MoveNext())
        {
            Set(_itr.Current.Value);
            return true;
        }
        Set(default);
        _itr.Dispose();
        return false;
    }
}

// Définir l'interface avec la méthode MakeIterator
public interface IStorageContainer<T>
{
    StorageContainerIterator<T> MakeIterator();
    T LookupEntry(int entry);
    void Clear();
    bool NeedsMax();
    void Setup(int max);
    T AllocateEntry(int entry);
    T LookupEntryAllocate(int entry);
    void Resetup(int Max);
}
public abstract class Storage<T, StorageType> where StorageType : IStorageContainer<T>
{
    protected StorageType _storage;
    protected string _indexName;
    protected string _formatString;

    public string GetIndexName() => _indexName;
    public string GetFormatString() => _formatString;

    public Storage()
    {
        _indexName = null;
        _formatString = null;
    }

    public StorageContainerIterator<T> MakeIterator()
    {
        return _storage.MakeIterator();
    }

    public T LookupEntry(int entry)
    {
        return _storage.LookupEntry(entry);
    }

    public abstract void Reload();

    public virtual void Load(string indexName, string formatString)
    {
        _indexName = indexName;
        _formatString = formatString;
    }

    public virtual void Cleanup()
    {
        Console.WriteLine($"Deleting database cache of `{_indexName}`...");
        var itr = _storage.MakeIterator();
        while (!itr.AtEnd())
        {
            FreeBlock(itr.Get());
            if (!itr.Inc())
                break;
        }
        _storage.Clear();
    }

    public void FreeBlock(T allocated)
    {
        if (_formatString == null) return;

        // Assuming T is a class with fields that match the format string
        // This is a simplified example and may need adjustment based on the actual structure of T
        foreach (char formatChar in _formatString)
        {
            switch (formatChar)
            {
                case 's': // string
                    // Assuming the field is a string, set it to null
                    typeof(T).GetField("StringFieldName")?.SetValue(allocated, null);
                    break;
                case 'u':
                case 'i':
                case 'f':
                    // Assuming the fields are value types, set them to default
                    typeof(T).GetField("UInt32FieldName")?.SetValue(allocated, default(uint));
                    typeof(T).GetField("Int32FieldName")?.SetValue(allocated, default(int));
                    typeof(T).GetField("FloatFieldName")?.SetValue(allocated, default(float));
                    break;
                case 'h':
                    typeof(T).GetField("UInt16FieldName")?.SetValue(allocated, default(ushort));
                    break;
                case 'c':
                    typeof(T).GetField("UInt8FieldName")?.SetValue(allocated, default(byte));
                    break;
            }
        }
    }
}

public class SQLStorage<T, StorageType> : Storage<T, StorageType> where T : new() where StorageType : IStorageContainer<T>
{
    public SQLStorage() : base() { }
    private const int STORAGE_ARRAY_MAX = 200000;

    public void LoadBlock(Field[] fields, T allocated)
    {
        if (_formatString == null) return;

        int offset = 0;
        int fieldIndex = 0;

        foreach (char formatChar in _formatString)
        {
            if (fieldIndex >= fields.Length) break;

            Field field = fields[fieldIndex];

            switch (formatChar)
            {
                case 'b': // Boolean
                    typeof(T).GetFields()[offset].SetValue(allocated, Field.GetBool());
                    offset++;
                    break;
                case 'c': // Byte
                    typeof(T).GetFields()[offset].SetValue(allocated, Field.GetUInt8());
                    offset++;
                    break;
                case 'h': // UInt16
                    typeof(T).GetFields()[offset].SetValue(allocated, Field.GetUInt16());
                    offset++;
                    break;
                case 'u': // UInt32
                    typeof(T).GetFields()[offset].SetValue(allocated, Field.GetUInt32());
                    offset++;
                    break;
                case 'i': // Int32
                    typeof(T).GetFields()[offset].SetValue(allocated, Field.GetInt32());
                    offset++;
                    break;
                case 'f': // Float
                    typeof(T).GetFields()[offset].SetValue(allocated, Field.GetFloat());
                    offset++;
                    break;
                case 's': // String
                    string strValue = Field.GetString() ?? string.Empty;
                    typeof(T).GetFields()[offset].SetValue(allocated, strValue);
                    offset++;
                    break;
                case 'x': // Skip
                    break;
                default:
                    Console.WriteLine($"Unknown field type in string: `{formatChar}`");
                    break;
            }
            fieldIndex++;
        }
    }

    public override void Load(string indexName, string formatString)
    {
        base.Load(indexName, formatString);
        var cLog = new CLog();

        // Assuming WorldDatabase is a SqlConnection or similar
        using SqlConnection connection = new("YourConnectionString");
        connection.Open();

        int max = STORAGE_ARRAY_MAX; // Define this constant
        if (_storage.NeedsMax())
        {
            using (SqlCommand command = new($"SELECT MAX(entry) FROM {indexName}", connection))
            {
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    max = reader.GetInt32(0) + 1;
                    if (max > STORAGE_ARRAY_MAX)
                    {
                        Console.WriteLine($"The table, '{indexName}', has a maximum entry of {max}, which is less {STORAGE_ARRAY_MAX}. Any items higher than {STORAGE_ARRAY_MAX} will not be loaded.");
                        max = STORAGE_ARRAY_MAX;
                    }
                }
            }
            _storage.Setup(max);
        }

        int cols = formatString.Length;
        using (SqlCommand command = new($"SELECT * FROM {indexName}", connection))
        {
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.FieldCount != cols)
            {
                if (reader.FieldCount > cols)
                {
                    cLog.Warning("Storage", $"Invalid format in {indexName} ({cols}/{reader.FieldCount}), loading anyway because we have enough data");
                }
                else
                {
                    cLog.Error("Storage", $"Invalid format in {indexName} ({cols}/{reader.FieldCount}), not enough data to proceed.");
                    return;
                }
            }

            while (reader.Read())
            {
                int entry = reader.GetInt32(0);
                T allocated = _storage.AllocateEntry(entry);
                if (allocated != null)
                {
                    Field[] fields = new Field[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        fields[i] = new Field (reader.GetValue(i));
                    }
                    LoadBlock(fields, allocated);
                }
            }
            CLog.Notice("Storage", $"{reader.RecordsAffected} entries loaded from table {indexName}.");
        }
    }

    public void LoadAdditionalData(string indexName, string formatString)
    {
        base.Load(indexName, formatString);
        var cLog = new CLog();

        // Assuming WorldDatabase is a SqlConnection or similar
        using SqlConnection connection = new("YourConnectionString");
        connection.Open();

        int max = STORAGE_ARRAY_MAX;
        if (_storage.NeedsMax())
        {
            using (SqlCommand command = new($"SELECT MAX(entry) FROM {indexName}", connection))
            {
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    max = reader.GetInt32(0) + 1;
                    if (max > STORAGE_ARRAY_MAX)
                    {
                        cLog.Error("Storage", $"The table, '{indexName}', has a maximum entry of {max}, which is less {STORAGE_ARRAY_MAX}. Any items higher than {STORAGE_ARRAY_MAX} will not be loaded.");
                        max = STORAGE_ARRAY_MAX;
                    }
                }
            }
            _storage.Resetup(max);
        }

        int cols = formatString.Length;
        using (SqlCommand command = new($"SELECT * FROM {indexName}", connection))
        {
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.FieldCount != cols)
            {
                if (reader.FieldCount > cols)
                {
                    cLog.Warning("Storage", $"Invalid format in {indexName} ({cols}/{reader.FieldCount}), loading anyway because we have enough data");
                }
                else
                {
                    cLog.Error("Storage", $"Invalid format in {indexName} ({cols}/{reader.FieldCount}), not enough data to proceed.");
                    return;
                }
            }

            while (reader.Read())
            {
                int entry = reader.GetInt32(0);
                T allocated = _storage.LookupEntryAllocate(entry);
                if (allocated != null)
                {
                    Field[] fields = new Field[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        fields[i] = new Field(reader.GetValue(i));
                    }
                    LoadBlock(fields, allocated);
                }
            }
            CLog.Notice("Storage", $"{reader.RecordsAffected} entries loaded from table {indexName}.");
        }
    }

    public override void Reload()
    {
        var cLog = new CLog();
        CLog.Notice("Storage", $"Reloading database cache from `{_indexName}`...");

        // Assuming WorldDatabase is a SqlConnection or similar
        using SqlConnection connection = new("YourConnectionString");
        connection.Open();

        using (SqlCommand command = new($"SELECT MAX(entry) FROM {_indexName}", connection))
        {
            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
                return;

            int max = reader.GetInt32(0);
            if (max == 0)
                return;

            if (_storage.NeedsMax())
            {
                if (max > STORAGE_ARRAY_MAX)
                    max = STORAGE_ARRAY_MAX;

                _storage.Resetup(max + 1);
            }
        }

        int cols = _formatString.Length;
        using (SqlCommand command = new($"SELECT * FROM {_indexName}", connection))
        {
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.FieldCount != cols)
            {
                cLog.Error("Storage", $"Invalid format in {_indexName} ({cols}/{reader.FieldCount}).");
                return;
            }

            while (reader.Read())
            {
                int entry = reader.GetInt32(0);
                T allocated = _storage.LookupEntryAllocate(entry);
                if (allocated != null)
                {
                    Field[] fields = new Field[reader.FieldCount];
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        fields[i] = new Field(reader.GetValue(i));
                    }
                    LoadBlock(fields, allocated);
                }
            }
        }
    }
}
