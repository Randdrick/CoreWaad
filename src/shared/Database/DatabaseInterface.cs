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
using System.Data.Common;

namespace WaadShared.Database;
public abstract class DatabaseInterface
{
    protected DatabaseInterface()
    {
    }

    public static void CleanupLibs()
    {
#if ENABLE_DATABASE_MYSQL
        MySql.Data.MySqlClient.MySqlConnection.ClearAllPools();
#endif
    }

    public static Database CreateDatabaseInterface(uint uType)
    {
        switch (uType)
        {
#if ENABLE_DATABASE_MYSQL
            case 1: // MYSQL
                return new MySQLDatabase();
#endif

#if ENABLE_DATABASE_POSTGRES
            case 2: // POSTGRES
                return new PostgresDatabase();
#endif

#if ENABLE_DATABASE_SQLITE
            case 3: // SQLITE
                return new SQLiteDatabase();
#endif
            default:
                Log.LargeErrorMessage("You have attempted to connect to a database that is unsupported or nonexistant.\nCheck your config and try again.");
                throw new NotSupportedException("Unsupported database type.");
        }
    }
}

public abstract class QueryResult(uint fieldCount, uint rowCount)
{
    public uint FieldCount => mFieldCount;
    public virtual string GetFieldName(int index) => throw new NotSupportedException("Noms des colonnes indisponibles.");
    protected uint mFieldCount = fieldCount;
    protected uint mRowCount = rowCount;
    protected Field[] mCurrentRow;

    public abstract bool NextRow();

    // Accès sécurisé à la valeur d'un champ de la ligne courante
    public object GetValue(int index)
    {
        if (mCurrentRow == null || index < 0 || index >= mCurrentRow.Length)
            throw new IndexOutOfRangeException();
        return mCurrentRow[index].GetValue();
    }
}

internal sealed class BufferedQueryResult(List<Field[]> rows, string[] names) : QueryResult((uint)names.Length, (uint)rows.Count)
{
    private int _index = -1;

    public static QueryResult ReadAll(DbDataReader reader)
    {
        var names = new string[reader.FieldCount];
        for (int index = 0; index < names.Length; index++)
            names[index] = reader.GetName(index);
        var rows = new List<Field[]>();
        while (reader.Read())
        {
            var fields = new Field[names.Length];
            for (int index = 0; index < fields.Length; index++)
            {
                fields[index] = new Field();
                fields[index].SetValue(reader.IsDBNull(index) ? null : reader.GetValue(index));
            }
            rows.Add(fields);
        }
        return rows.Count == 0 ? null : new BufferedQueryResult(rows, names);
    }

    public override string GetFieldName(int index) => names[index];

    public override bool NextRow()
    {
        if (_index + 1 >= rows.Count)
            return false;
        mCurrentRow = rows[++_index];
        return true;
    }
}

public static class Log
{
    public static void Notice(string source, string message, string Hostname, string DatabaseName) => Console.WriteLine($"[{source}] {message} {Hostname} {DatabaseName}");
    public static void Error(string source, string message) => Console.WriteLine($"[{source}] ERROR: {message}");
    public static void LargeErrorMessage(string message) => Console.WriteLine(message);
    internal static void Notice(string source, string message) => Console.WriteLine($"[{source}] Notice: {message}");
}
public class Field
{
    private object value;

    public void SetValue(object val)
    {
        value = val;
    }

    public object GetValue()
    {
        return value;
    }
}