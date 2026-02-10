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
using System.Data.SQLite;
using System.Reflection;
using MySql.Data.MySqlClient;
using Npgsql;

using static WaadShared.Common;

namespace WaadShared;

[AttributeUsage(AttributeTargets.Property)]
public class DbFieldAttribute(string format) : Attribute
{
    public string Format { get; } = format;
    public int Length { get; set; }
}

public class StorageAllocationPool<T> where T : new()
{
    private T[][] _pool;
    private int _count;
    internal int _max;

    public void Init(int count)
    {
        _pool = new T[count + 100][];
        for (int i = 0; i < count + 100; i++)
        {
            _pool[i] = new T[1];
            try
            {
                _pool[i][0] = new T();
            }
            catch (Exception ex)
            {
                CLog.Error("Storage", $"Erreur lors de l'initialisation du pool à l'index {i} : {ex.Message}");
                _pool[i][0] = default;
            }
        }
        _count = 0;
        _max = count + 100;
    }

    public T[] Get()
    {
        if (_count >= _max)
        {
            CLog.Error("Storage", "StorageAllocationPool Get() failed!");
            return null;
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

public class ArrayStorageContainer<T> : IStorageContainer<T> where T : class, new()
{
    private T[][] _array;
    internal int _max;
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
        if (entry >= _max)
        {
            CLog.Error("Storage", $"Index hors limites : {entry}");
            return null;
        }

        if (_array[entry] != null)
        {
            CLog.Warning("Storage", $"L'entrée {entry} est déjà allouée.");
            return _array[entry][0];
        }

        T[] newEntry = new T[1];
        try
        {
            newEntry[0] = new T();
        }
        catch (Exception ex)
        {
            CLog.Error("Storage", $"Erreur lors de l'instanciation de l'entrée {entry} : {ex.Message}");
            return null;
        }

        _array[entry] = newEntry;
        return newEntry[0];
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
        if (entry >= _max || _array[entry] == null)
            return null;
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
        if (ret == null)
        {
            ret = AllocateEntry(entry);
        }
        return ret;
    }

    public void Clear()
    {
        for (int i = 0; i < _max; ++i)
        {
            _array[i] = null;
        }
    }

    public bool NeedsMax()
    {
        return true;
    }

    public T LookupEntry(uint entry)
    {
        return LookupEntry((int)entry);
    }
}


public class HashMapStorageContainer<T> : IStorageContainer<T> where T : new()
{
    internal Dictionary<int, T> _map = [];
    internal Dictionary<uint, T> map = [];
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

        T n;
        if (_pool != null)
        {
            T[] nArray = _pool.Get();
            n = nArray != null ? nArray[0] : new T();
        }
        else
        {
            n = new T();
        }

        _map[entry] = n;
        return n;
    }

    public T AllocateEntry(uint entry)
    {
        if (map.ContainsKey(entry))
            return default;
        T[] nArray = _pool.Get();
        T n = nArray != null ? nArray[0] : new T();
        map[entry] = n;
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
        _map[entry] = pointer;
        return true;
    }

    public T LookupEntryAllocate(int entry)
    {
        T ret = LookupEntry(entry) ?? AllocateEntry(entry);
        return ret;
    }

    public void Clear()
    {
        _map.Clear();
    }

    public bool NeedsMax()
    {
        // Un HashMap n'a pas besoin d'une taille maximale fixe
        return false;
    }

    public void Setup(int max)
    {
        // Pour un HashMap, Setup peut initialiser la capacité si nécessaire
        _map = new Dictionary<int, T>(max);
    }

    public void Resetup(int max)
    {
        // Pour un HashMap, Resetup peut réinitialiser la capacité si nécessaire
        var newMap = new Dictionary<int, T>(max);
        foreach (var kvp in _map)
            newMap.Add(kvp.Key, kvp.Value);
        _map = newMap;
    }

    public T LookupEntry(uint entry)
    {
        T ret = LookupEntry(entry) ?? AllocateEntry(entry);
        return ret;
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
    T LookupEntry(uint entry);
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

    public T LookupEntry(uint entry)
    {
        return _storage.LookupEntry(entry);
    }

    public abstract void Reload();

    public virtual void Load(string indexName, string formatString, string connectionString, int dbType)
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
        if (_formatString == null || allocated == null)
            return;

        var properties = typeof(T).GetProperties();
        int fieldIndex = 0;

        foreach (var property in properties)
        {
            var dbFieldAttribute = property.GetCustomAttribute<DbFieldAttribute>();
            if (dbFieldAttribute == null)
                continue;

            try
            {
                switch (dbFieldAttribute.Format)
                {
                    case "s": // string
                        property.SetValue(allocated, string.Empty);
                        break;
                    case "u":
                        if (property.PropertyType == typeof(uint))
                        {
                            property.SetValue(allocated, 0u);
                        }
                        else if (property.PropertyType == typeof(uint[]))
                        {
                            uint[] array = new uint[dbFieldAttribute.Length];
                            property.SetValue(allocated, array);
                        }
                        break;
                    case "i":
                        property.SetValue(allocated, 0);
                        break;
                    case "f":
                        if (property.PropertyType == typeof(float))
                        {
                            property.SetValue(allocated, 0f);
                        }
                        break;
                    case "c":
                        property.SetValue(allocated, (byte)0);
                        break;
                }
            }
            catch (Exception ex)
            {
                CLog.Error("Storage", $"Erreur lors de la libération de la propriété {property.Name} : {ex.Message}");
            }

            fieldIndex++;
        }
    }
}

public class SQLStorage<T, StorageType> : Storage<T, StorageType> where T : new() where StorageType : IStorageContainer<T>
{
    public SQLStorage() : base()
    {
        _storage = Activator.CreateInstance<StorageType>();
    }
    private const int STORAGE_ARRAY_MAX = 200000;

    public void LoadBlock(Field[] fields, T allocated)
    {
        if (fields == null)
        {
            CLog.Error("Storage", "Fields est null.");
            return;
        }

        if (allocated == null)
        {
            CLog.Error("Storage", "Allocated est null.");
            return;
        }

        var properties = typeof(T).GetProperties();
        int fieldIndex = 0;

        foreach (var property in properties)
        {
            var dbFieldAttribute = property.GetCustomAttribute<DbFieldAttribute>();
            if (dbFieldAttribute == null)
                continue;

            try
            {
                if (fieldIndex >= fields.Length)
                {
                    CLog.Error("Storage", $"Index de champ hors limites : {fieldIndex}");
                    break;
                }

                Field field = fields[fieldIndex];
                if (field.Equals(null))
                {
                    CLog.Debug("Storage", $"Valeur null pour la propriété {property.Name}");
                }

                switch (dbFieldAttribute.Format)
                {
                    case "u":
                        if (property.PropertyType == typeof(uint))
                        {
                            try
                            {
                                property.SetValue(allocated, field.Equals(null) ? 0u : field.GetUInt32());
                            }
                            catch (Exception ex)
                            {
                                CLog.Error("Storage", $"Erreur lors de la conversion en uint pour {property.Name} : {ex.Message}");
                                property.SetValue(allocated, 0u);
                            }
                        }
                        else if (property.PropertyType == typeof(uint[]))
                        {
                            uint[] array = new uint[dbFieldAttribute.Length];
                            for (int i = 0; i < dbFieldAttribute.Length; i++)
                            {
                                if (fieldIndex + i >= fields.Length)
                                {
                                    CLog.Error("Storage", $"Index de tableau hors limites : {fieldIndex + i}");
                                    break;
                                }
                                try
                                {
                                    array[i] = fields[fieldIndex + i].Equals(null) ? 0u : fields[fieldIndex + i].GetUInt32();
                                }
                                catch (Exception ex)
                                {
                                    CLog.Error("Storage", $"Erreur lors de la conversion en uint pour {property.Name}[{i}] : {ex.Message}");
                                    array[i] = 0u;
                                }
                            }
                            property.SetValue(allocated, array);
                            fieldIndex += dbFieldAttribute.Length - 1;
                        }
                        break;

                    case "i":
                        try
                        {
                            property.SetValue(allocated, field.Equals(null) ? 0 : field.GetInt32());
                        }
                        catch (Exception ex)
                        {
                            CLog.Error("Storage", $"Erreur lors de la conversion en int pour {property.Name} : {ex.Message}");
                            property.SetValue(allocated, 0);
                        }
                        break;

                    case "s":
                        try
                        {
                            property.SetValue(allocated, field.Equals(null) ? string.Empty : field.GetString());
                        }
                        catch (Exception ex)
                        {
                            CLog.Error("Storage", $"Erreur lors de la conversion en string pour {property.Name} : {ex.Message}");
                            property.SetValue(allocated, string.Empty);
                        }
                        break;

                    case "f":
                        if (property.PropertyType == typeof(float))
                        {
                            try
                            {
                                property.SetValue(allocated, field.Equals(null) ? 0f : field.GetFloat());
                            }
                            catch (Exception ex)
                            {
                                CLog.Error("Storage", $"Erreur lors de la conversion en float pour {property.Name} : {ex.Message}");
                                property.SetValue(allocated, 0f);
                            }
                        }
                        break;

                    case "c":
                        try
                        {
                            if (field.Equals(null))
                            {
                                property.SetValue(allocated, (byte)0);
                            }
                            else
                            {
                                property.SetValue(allocated, field.GetUInt8());
                            }
                        }
                        catch (Exception ex)
                        {
                            CLog.Error("Storage", $"Erreur lors de la conversion en byte pour {property.Name} : {ex.Message}");
                            property.SetValue(allocated, (byte)0);
                        }
                        break;

                    default:
                        CLog.Warning("Storage", $"Format de champ inconnu : {dbFieldAttribute.Format}");
                        break;
                }
            }
            catch (Exception ex)
            {
                CLog.Error("Storage", $"Erreur lors de l'affectation de la propriété {property.Name} à l'index {fieldIndex} : {ex.Message}");
            }

            fieldIndex++;
        }
    }

    public static class SLogonSQL
    {
        private static string connectionString;
        private static int databaseType;

        public static int DatabaseType
        {
            get => databaseType;
            set => databaseType = value;
        }

        public static void SetConnectionString(string connString, int dbType)
        {
            connectionString = connString;
            databaseType = dbType;
        }

        public static dynamic CreateConnection()
        {
            return databaseType switch
            {
                1 => new MySqlConnection(connectionString), // MySQL
                2 => new NpgsqlConnection(connectionString), // PostgreSQL
                3 => new SQLiteConnection(connectionString), // SQLite
                _ => throw new InvalidOperationException("Unsupported database type.")
            };
        }

        public static dynamic CreateCommand(string query, dynamic connection)
        {
            return databaseType switch
            {
                1 => new MySqlCommand(query, connection), // MySQL
                2 => new NpgsqlCommand(query, connection), // PostgreSQL
                3 => new SQLiteCommand(query, connection), // SQLite
                _ => throw new InvalidOperationException("Unsupported database type.")
            };
        }
    }

    public override void Load(string indexName, string formatString, string connectionString, int dbType)
    {
        base.Load(indexName, formatString, connectionString, dbType);

        CLog.Debug("Storage", $"Début du chargement de la table {indexName}...");

        SLogonSQL.SetConnectionString(connectionString, dbType);

        if (SLogonSQL.DatabaseType == 0)
        {
            CLog.Error("Storage", "Database type is not set. Cannot load data.");
            return;
        }

        using var connection = SLogonSQL.CreateConnection();
        try
        {
            CLog.Debug("Storage", $"Ouverture de la connexion à la base de données pour {indexName}...");
            connection.Open();
        }
        catch (Exception ex)
        {
            CLog.Error("Storage", $"Erreur lors de l'ouverture de la connexion pour {indexName}: {ex.Message}");
            return;
        }

        int max = STORAGE_ARRAY_MAX;
        if (_storage.NeedsMax())
        {
            try
            {
                using var command = SLogonSQL.CreateCommand($"SELECT MAX(entry) FROM {indexName}", connection);
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    max = reader.GetInt32(0) + 1;
                    if (max > STORAGE_ARRAY_MAX)
                    {
                        CLog.Warning("Storage", $"La table '{indexName}' a un maximum d'entrée de {max}, qui dépasse {STORAGE_ARRAY_MAX}. Les entrées au-delà de {STORAGE_ARRAY_MAX} ne seront pas chargées.");
                        max = STORAGE_ARRAY_MAX;
                    }
                }
                _storage.Setup(max);
            }
            catch (Exception ex)
            {
                CLog.Error("Storage", $"Erreur lors de la récupération du MAX(entry) pour {indexName}: {ex.Message}");
                return;
            }
        }

        int cols = formatString.Length;
        int count = 0;
        using (var command = SLogonSQL.CreateCommand($"SELECT * FROM {indexName}", connection))
        {
            using var reader = command.ExecuteReader();
            if (reader.FieldCount != cols)
            {
                if (reader.FieldCount > cols)
                {
                    CLog.Warning("Storage", $"Format invalide dans {indexName} ({cols}/{reader.FieldCount}), chargement quand même car nous avons assez de données.");
                }
                else
                {
                    CLog.Error("Storage", $"Format invalide dans {indexName} ({cols}/{reader.FieldCount}), pas assez de données pour continuer.");
                    return;
                }
            }

            while (reader.Read())
            {
                try
                {
                    int entry = reader.GetInt32(0);
                    T allocated = _storage.AllocateEntry(entry);
                    if (allocated != null)
                    {
                        Field[] fields = new Field[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            fields[i] = new Field(reader.GetValue(i));
                        }
                        LoadBlock(fields, allocated);
                        CallParseMethods(allocated);
                        count++;
                    }
                    else
                    {
                        CLog.Warning("Storage", $"Échec de l'allocation de l'entrée {entry} dans {indexName}.");
                    }
                }
                catch (Exception ex)
                {
                    CLog.Error("Storage", $"Erreur lors du chargement d'une entrée dans {indexName}: {ex.Message}");
                    CLog.Error("Storage", ex.StackTrace);
                }
            }
            CLog.Notice("Storage", $"{count} entrées chargées depuis la table {indexName}.");
        }
    }

    private static void CallParseMethods(object obj)
    {
        if (obj == null)
            return;

        Type type = obj.GetType();

        // Vérifier et appeler ParseStats si la méthode existe
        var parseStatsMethod = type.GetMethod("ParseStats");
        if (parseStatsMethod != null)
        {
            try
            {
                parseStatsMethod.Invoke(obj, null);
            }
            catch (Exception ex)
            {
                CLog.Error("Storage", $"Erreur lors de l'appel de ParseStats: {ex.Message}");
            }
        }

        // Vérifier et appeler ParseDamage si la méthode existe
        var parseDamageMethod = type.GetMethod("ParseDamage");
        if (parseDamageMethod != null)
        {
            try
            {
                parseDamageMethod.Invoke(obj, null);
            }
            catch (Exception ex)
            {
                CLog.Error("Storage", $"Erreur lors de l'appel de ParseDamage: {ex.Message}");
            }
        }

        // Vérifier et appeler ParseSpells si la méthode existe
        var parseSpellsMethod = type.GetMethod("ParseSpells");
        if (parseSpellsMethod != null)
        {
            try
            {
                parseSpellsMethod.Invoke(obj, null);
            }
            catch (Exception ex)
            {
                CLog.Error("Storage", $"Erreur lors de l'appel de ParseSpells: {ex.Message}");
            }
        }

        // Vérifier et appeler ParseSockets si la méthode existe
        var parseSocketsMethod = type.GetMethod("ParseSockets");
        if (parseSocketsMethod != null)
        {
            try
            {
                parseSocketsMethod.Invoke(obj, null);
            }
            catch (Exception ex)
            {
                CLog.Error("Storage", $"Erreur lors de l'appel de ParseSockets: {ex.Message}");
            }
        }
    }


    public void LoadAdditionalData(string indexName, string formatString, string connectionString, int dbType)
    {
        base.Load(indexName, formatString, connectionString, dbType);
        SLogonSQL.SetConnectionString(connectionString, dbType);

        // Vérifiez que le type de base de données est défini
        if (SLogonSQL.DatabaseType == 0)
        {
            CLog.Error("Storage", "Database type is not set. Cannot load data.");
            return;
        }

        // Ouvrir une connexion en fonction du type de base de données
        using var connection = SLogonSQL.CreateConnection();
        connection.Open();

        int max = STORAGE_ARRAY_MAX;
        if (_storage.NeedsMax())
        {
            using (var command = SLogonSQL.CreateCommand($"SELECT MAX(entry) FROM {indexName}", connection))
            {
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    max = reader.GetInt32(0) + 1;
                    if (max > STORAGE_ARRAY_MAX)
                    {
                        CLog.Error("Storage", $"The table, '{indexName}', has a maximum entry of {max}, which is less {STORAGE_ARRAY_MAX}. Any items higher than {STORAGE_ARRAY_MAX} will not be loaded.");
                        max = STORAGE_ARRAY_MAX;
                    }
                }
            }
            _storage.Resetup(max);
        }

        int cols = formatString.Length;
        using (var command = SLogonSQL.CreateCommand($"SELECT * FROM {indexName}", connection))
        {
            using var reader = command.ExecuteReader();
            if (reader.FieldCount != cols)
            {
                if (reader.FieldCount > cols)
                {
                    CLog.Warning("Storage", $"Invalid format in {indexName} ({cols}/{reader.FieldCount}), loading anyway because we have enough data");
                }
                else
                {
                    CLog.Error("Storage", $"Invalid format in {indexName} ({cols}/{reader.FieldCount}), not enough data to proceed.");
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
        CLog.Notice("Storage", $"Reloading database cache from `{_indexName}`...");

        // Vérifiez que le type de base de données est défini
        if (SLogonSQL.DatabaseType == 0)
        {
            CLog.Error("Storage", "Database type is not set. Cannot load data.");
            return;
        }

        // Ouvrir une connexion en fonction du type de base de données
        using var connection = SLogonSQL.CreateConnection();
        connection.Open();

        using (var command = SLogonSQL.CreateCommand($"SELECT MAX(entry) FROM {_indexName}", connection))
        {
            using var reader = command.ExecuteReader();
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
        using (var command = SLogonSQL.CreateCommand($"SELECT * FROM {_indexName}", connection))
        {
            using var reader = command.ExecuteReader();
            if (reader.FieldCount != cols)
            {
                CLog.Error("Storage", $"Invalid format in {_indexName} ({cols}/{reader.FieldCount}).");
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