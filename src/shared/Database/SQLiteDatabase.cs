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
using System.Text;
using System.Data.SQLite;

public class SQLiteDatabase : Database
{
    private SQLiteConnection Connection;
    private string mHostname;
    private int mConnectionCount;
    private string mUsername;
    private string mPassword;
    private string mDatabaseName;

    public SQLiteDatabase() : base()
    {
        // Initialisation des connexions SQLite
    }

    ~SQLiteDatabase()
    {
        Dispose(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cleanup managed resources if needed
            Connection?.Dispose();
        }

        // Cleanup unmanaged resources
        Connection?.Close();
    }

    public override bool Initialize(string Hostname, uint port, string Username, string Password, string DatabaseName, uint ConnectionCount, uint BufferSize)
    {
        mHostname = Hostname;
        mUsername = Username;
        mPassword = Password;
        mDatabaseName = DatabaseName;
        mConnectionCount = (int)ConnectionCount;

        try
        {
            var connString = new SQLiteConnectionStringBuilder
            {
                DataSource = DatabaseName,
                Version = 3,
                Pooling = true,
                MaxPoolSize = (int)ConnectionCount
            }.ToString();

            Connection = new SQLiteConnection(connString);
            Connection.Open();
        }
        catch (SQLiteException ex)
        {
            Log.Error("SQLiteDatabase", $"Connection failed due to: `{ex.Message}`");
            return false;
        }

        _Initialize();
        return true;
    }

    public override void Shutdown()
    {
        try
        {
            Connection?.Close();
            Connection?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("SQLiteDatabase", $"Error shutting down connection: {ex.Message}");
        }
        finally
        {
            Connection = null;
            Log.Notice("SQLiteDatabase", "Connection has been shut down.");
        }
    }

    public override string EscapeString(string escape)
    {
        if (escape == null)
        {
            throw new ArgumentNullException(nameof(escape));
        }

        return SQLiteConnection.EscapeString(escape);
    }

    public override void EscapeLongString(string str, uint len, StringBuilder outStr)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (outStr == null)
        {
            throw new ArgumentNullException(nameof(outStr));
        }

        string escapedStr = SQLiteConnection.EscapeString(str);
        outStr.Append(escapedStr);
    }

    protected override bool SendQuery(DatabaseConnection con, string sql, bool self)
    {
        if (con == null)
        {
            throw new ArgumentNullException(nameof(con));
        }

        if (string.IsNullOrEmpty(sql))
        {
            throw new ArgumentException("SQL query is null or empty", nameof(sql));
        }

        try
        {
            using var cmd = new SQLiteCommand(sql, Connection);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SQLiteException ex)
        {
            Log.Error("SQLiteDatabase", $"Error executing query: {ex.Message}");
            return false;
        }
    }

    protected override QueryResult StoreQueryResult(DatabaseConnection con)
    {
        if (con == null)
        {
            throw new ArgumentNullException(nameof(con));
        }

        try
        {
            using var cmd = new SQLiteCommand("SELECT last_insert_rowid()", Connection);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new SQLiteQueryResult(reader);
            }
        }
        catch (SQLiteException ex)
        {
            Log.Error("SQLiteDatabase", $"Error storing query result: {ex.Message}");
        }

        return null;
    }

    protected override void BeginTransaction(DatabaseConnection con)
    {
        if (con == null)
        {
            throw new ArgumentNullException(nameof(con));
        }

        try
        {
            using var cmd = new SQLiteCommand("BEGIN TRANSACTION", Connection);
            cmd.ExecuteNonQuery();
        }
        catch (SQLiteException ex)
        {
            Log.Error("SQLiteDatabase", $"Error starting transaction: {ex.Message}");
        }
    }

    protected override void EndTransaction(DatabaseConnection con)
    {
        if (con == null)
        {
            throw new ArgumentNullException(nameof(con));
        }

        try
        {
            using var cmd = new SQLiteCommand("COMMIT", Connection);
            cmd.ExecuteNonQuery();
        }
        catch (SQLiteException ex)
        {
            Log.Error("SQLiteDatabase", $"Error committing transaction: {ex.Message}");
        }
    }

    public override bool SupportsReplaceInto()
    {
        return false; // SQLite does not support REPLACE INTO
    }

    public override bool SupportsTableLocking()
    {
        return true;
    }
}

public class SQLiteQueryResult : QueryResult
{
    private SQLiteDataReader reader;

    public SQLiteQueryResult(SQLiteDataReader reader) : base((uint)reader.FieldCount, (uint)reader.RecordsAffected)
    {
        this.reader = reader;
    }

    public override bool NextRow()
    {
        return reader.Read();
    }

    public override void Dispose()
    {
        reader?.Dispose();
    }
}

public abstract class QueryResult : IDisposable
{
    protected uint mFieldCount;
    protected uint mRowCount;
    protected Field[] mCurrentRow;

    protected QueryResult(uint fieldCount, uint rowCount)
    {
        mFieldCount = fieldCount;
        mRowCount = rowCount;
    }

    public abstract bool NextRow();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
        }

        // Dispose unmanaged resources
    }
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

public static class Log
{
    public static void Notice(string source, string message)
    {
        Console.WriteLine($"[{source}] {message}");
    }

    public static void Error(string source, string message)
    {
        Console.WriteLine($"[{source}] ERROR: {message}");
    }
}