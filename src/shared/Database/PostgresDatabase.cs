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
using Npgsql;

using static System.Threading.Thread;

namespace WaadShared.Database;

public class PostgresDatabase : Database
{
    private new IDatabaseConnection[] Connections;
    private new int mConnectionCount;

    public PostgresDatabase() : base()
    {
        // Initialisation des connexions PostgreSQL
    }

    ~PostgresDatabase()
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

    public bool Initialize(string Hostname, uint port, string Username, string Password, string DatabaseName, uint ConnectionCount, uint BufferSize)
    {
        mConnectionCount = (int)ConnectionCount;
        Connections = new IDatabaseConnection[mConnectionCount];

        for (int i = 0; i < mConnectionCount; ++i)
        {
            var connString = new NpgsqlConnectionStringBuilder
            {
                Host = Hostname,
                Port = (int)port,
                Username = Username,
                Password = Password,
                Database = DatabaseName,
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = (int)ConnectionCount
            }.ToString();

            var conn = new NpgsqlConnection(connString);
            var wrapper = new NpgsqlConnectionWrapper(conn);

            try
            {
                wrapper.Open();
                Connections[i] = (IDatabaseConnection)wrapper;
            }
            catch (NpgsqlException ex)
            {
                Log.Error("PostgresDatabase", $"Connection failed due to: `{ex.Message}`");
                conn.Dispose();
                return false;
            }
        }

        Initialize();
        return true;
    }

    public void Shutdown()
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
                    Log.Error("PostgresDatabase", $"Error shutting down connection: {ex.Message}");
                }
            }
        }
        Connections = null;
        Log.Notice("PostgresDatabase", "All connections have been shut down.");
    }

    protected override bool SendQuery(DatabaseConnection con, string sql, bool self)
    {
        ArgumentNullException.ThrowIfNull(con);

        if (string.IsNullOrEmpty(sql))
        {
            throw new ArgumentException("SQL query is null or empty", nameof(sql));
        }

        if (con is NpgsqlConnectionWrapper wrapper)
        {
            NpgsqlConnection npgsqlConnection = wrapper.GetConnection();
            try
            {
                using var cmd = new NpgsqlCommand(sql, npgsqlConnection);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (NpgsqlException ex)
            {
                Log.Error("PostgresDatabase", $"Error executing query: {ex.Message}");
                return false;
            }
        }
        else
        {
            throw new ArgumentException("The provided connection is not a NpgsqlConnectionWrapper", nameof(con));
        }
    }

    protected override QueryResult StoreQueryResult(DatabaseConnection con)
    {
        ArgumentNullException.ThrowIfNull(con);

        if (con is NpgsqlConnectionWrapper wrapper)
        {
            NpgsqlConnection npgsqlConnection = wrapper.GetConnection();
            try
            {
                using var cmd = new NpgsqlCommand("SELECT LASTVAL()", npgsqlConnection);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new PostgresQueryResult(reader);
                }
            }
            catch (NpgsqlException ex)
            {
                Log.Error("PostgresDatabase", $"Error storing query result: {ex.Message}");
            }
        }
        else
        {
            throw new ArgumentException("The provided connection is not a NpgsqlConnectionWrapper", nameof(con));
        }

        return null;
    }

    protected override void BeginTransaction(DatabaseConnection con)
    {
        ArgumentNullException.ThrowIfNull(con);

        if (con is NpgsqlConnectionWrapper wrapper)
        {
            NpgsqlConnection npgsqlConnection = wrapper.GetConnection();
            try
            {
                using var cmd = new NpgsqlCommand("BEGIN", npgsqlConnection);
                cmd.ExecuteNonQuery();
            }
            catch (NpgsqlException ex)
            {
                Log.Error("PostgresDatabase", $"Error starting transaction: {ex.Message}");
            }
        }
        else
        {
            throw new ArgumentException("The provided connection is not a NpgsqlConnectionWrapper", nameof(con));
        }
    }

    protected override void EndTransaction(DatabaseConnection con)
    {
        ArgumentNullException.ThrowIfNull(con);

        if (con is NpgsqlConnectionWrapper wrapper)
        {
            NpgsqlConnection npgsqlConnection = wrapper.GetConnection();
            try
            {
                using var cmd = new NpgsqlCommand("COMMIT", npgsqlConnection);
                cmd.ExecuteNonQuery();
            }
            catch (NpgsqlException ex)
            {
                Log.Error("PostgresDatabase", $"Error committing transaction: {ex.Message}");
            }
        }
        else
        {
            throw new ArgumentException("The provided connection is not a NpgsqlConnectionWrapper", nameof(con));
        }
    }

    public override bool SupportsReplaceInto() => false;
    public override bool SupportsTableLocking() => true;

    public override string EscapeString(string escape, DatabaseConnection con)
    {
        ArgumentNullException.ThrowIfNull(escape);
        return EscapeString(escape);
    }

    public override void EscapeLongString(string str, uint len, StringBuilder outStr)
    {
        ArgumentNullException.ThrowIfNull(str);
        ArgumentNullException.ThrowIfNull(outStr);

        string escapedStr = EscapeString(str);
        outStr.Append(escapedStr);
    }

    protected override void SetThreadName(string v)
    {
        // Set the name of the current thread
        CurrentThread.Name = v;
    }

    public override string EscapeString(string escape)
    {
        ArgumentNullException.ThrowIfNull(escape);
        // Escape single quotes by doubling them
        return escape.Replace("'", "''");
    }
}

public class PostgresQueryResult(NpgsqlDataReader reader) : QueryResult((uint)reader.FieldCount, (uint)reader.RecordsAffected)
{
    private readonly NpgsqlDataReader reader = reader;

    public override bool NextRow() => reader.Read();
}
