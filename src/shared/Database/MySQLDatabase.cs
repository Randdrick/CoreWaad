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
using System.Threading;
using MySql.Data.MySqlClient;

public class MySQLDatabase : Database
{
    private MySqlConnection[] Connections;
    private int mConnectionCount;
    private string mHostname;
    private string mUsername;
    private string mPassword;
    private string mDatabaseName;
    private uint mPort;

    public MySQLDatabase() : base()
    {
        // Initialisation des connexions MySQL
    }

    ~MySQLDatabase()
    {
        Dispose(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cleanup managed resources if needed
        }

        // Cleanup unmanaged resources
        for (int i = 0; i < mConnectionCount; ++i)
        {
            Connections[i]?.Close();
            Connections[i]?.Dispose();
        }

        Connections = null;
    }

    public override bool Initialize(string Hostname, uint port, string Username, string Password, string DatabaseName, uint ConnectionCount, uint BufferSize)
    {
        mConnectionCount = (int)ConnectionCount;
        Connections = new MySqlConnection[mConnectionCount];

        for (int i = 0; i < mConnectionCount; ++i)
        {
            var connString = new MySqlConnectionStringBuilder
            {
                Server = Hostname,
                Port = port,
                UserID = Username,
                Password = Password,
                Database = DatabaseName,
                Pooling = true,
                MinimumPoolSize = 0,
                MaximumPoolSize = (int)ConnectionCount
            }.ToString();

            var conn = new MySqlConnection(connString);

            try
            {
                conn.Open();
                Connections[i] = conn;
            }
            catch (MySqlException ex)
            {
                Log.Error("MySQLDatabase", $"Connection failed due to: `{ex.Message}`");
                conn.Dispose();
                return false;
            }
        }

        _Initialize();
        return true;
    }

    public override void Shutdown()
    {
        for (int i = 0; i < mConnectionCount; ++i)
        {
            if (Connections[i] != null)
            {
                try
                {
                    Connections[i].Close();
                    Connections[i].Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error("MySQLDatabase", $"Error shutting down connection: {ex.Message}");
                }
            }
        }
        Connections = null;
        Log.Notice("MySQLDatabase", "All connections have been shut down.");
    }

    public override string EscapeString(string escape)
    {
        if (escape == null)
        {
            throw new ArgumentNullException(nameof(escape));
        }

        using var conn = GetFreeConnection();
        return MySqlHelper.EscapeString(escape);
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

        using var conn = GetFreeConnection();
        string escapedStr = MySqlHelper.EscapeString(str);
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
            using var cmd = new MySqlCommand(sql, (MySqlConnection)con);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (MySqlException ex)
        {
            Log.Error("MySQLDatabase", $"Error executing query: {ex.Message}");
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
            using var cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", (MySqlConnection)con);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new MySQLQueryResult(reader);
            }
        }
        catch (MySqlException ex)
        {
            Log.Error("MySQLDatabase", $"Error storing query result: {ex.Message}");
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
            using var cmd = new MySqlCommand("START TRANSACTION", (MySqlConnection)con);
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Log.Error("MySQLDatabase", $"Error starting transaction: {ex.Message}");
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
            using var cmd = new MySqlCommand("COMMIT", (MySqlConnection)con);
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Log.Error("MySQLDatabase", $"Error committing transaction: {ex.Message}");
        }
    }

    public override bool SupportsReplaceInto()
    {
        return true;
    }

    public override bool SupportsTableLocking()
    {
        return true;
    }
}

public class MySQLQueryResult(MySqlDataReader reader) : QueryResult((uint)reader.FieldCount, (uint)reader.RecordsAffected)
{
    private MySqlDataReader reader = reader;

    public override bool NextRow()
    {
        return reader.Read();
    }

    public override void Dispose()
    {
        reader?.Dispose();
    }
}

public class MySQLDatabaseConnection : DatabaseConnection
{
    public MySqlConnection MySql { get; set; }
}

public class DatabaseConnection
{
    public MySqlConnection MySql { get; set; }
    public SemaphoreSlim Busy { get; } = new SemaphoreSlim(1, 1);
}

public abstract class QueryResult
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