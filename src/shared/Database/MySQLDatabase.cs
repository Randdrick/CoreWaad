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
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;

using static System.Threading.Thread;

namespace WaadShared.Database;
public class MySQLDatabase : Database
{
    private new MySqlConnection[] Connections;
    private new int mConnectionCount;
    private readonly uint fieldCount = 0;
    private readonly uint rowCount = 0;
    private string _connStr;
    private string _queryConnStr; // non-pooled variant for short-lived Query() calls

    public bool DumpDatabase(string filePath)
    {
        // Utilise mysqldump (doit être dans le PATH)
        try
        {
            var conn = (Connections != null && Connections.Length > 0 ? Connections[0] : null) ?? throw new InvalidOperationException("Aucune connexion MySQL active.");
            var csb = new MySqlConnectionStringBuilder(conn.ConnectionString);
            var args = $"--user=\"{csb.UserID}\" --password=\"{csb.Password}\" --host=\"{csb.Server}\" --port={csb.Port} {csb.Database} --result-file=\"{filePath}\" --skip-lock-tables";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mysqldump",
                Arguments = args,
                RedirectStandardOutput = false,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                var err = proc.StandardError.ReadToEnd();
                CLog.Error("[MySQLDatabase]", $"mysqldump error: {err}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            CLog.Error("[MySQLDatabase]", $"DumpDatabase exception: {ex.Message}");
            return false;
        }
    }

    public bool Initialize(string Hostname, uint port, string Username, string Password, string DatabaseName, uint ConnectionCount, uint BufferSize)
    {
        mConnectionCount = (int)ConnectionCount;
        Connections = new MySqlConnection[mConnectionCount];

        CLog.Notice("[MySQLDatabase]", string.Format("Connecting to {0}, database {1}...", Hostname, DatabaseName));

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
                MaximumPoolSize = (uint)(int)ConnectionCount
            }.ToString();

            if (i == 0)
            {
                _connStr = connString; // store pooled string (used by Connections[])
                // Build a non-pooled variant for Query() — avoids pool exhaustion
                var qcsb = new MySqlConnectionStringBuilder(connString) { Pooling = false };
                _queryConnStr = qcsb.ToString();
            }

            var conn = new MySqlConnection(connString);

            try
            {
                conn.Open();
                Connections[i] = conn;
            }
            catch (MySqlException ex)
            {
                CLog.Error("[MySQLDatabase]", $"Connection failed due to: `{ex.Message}`");
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
                    CLog.Error("[MySQLDatabase]", $"Error shutting down connection: {ex.Message}");
                }
            }
        }
        Connections = null;
        CLog.Notice("[MySQLDatabase]", "All connections have been shut down.");
    }

    public override string EscapeString(string escape)
    {
        ArgumentNullException.ThrowIfNull(escape);
        DatabaseConnection conn = GetFreeConnection();
        _ = conn;
        return MySqlHelper.EscapeString(escape);
    }

    public override void EscapeLongString(string str, uint len, StringBuilder outStr)
    {
        ArgumentNullException.ThrowIfNull(str);
        ArgumentNullException.ThrowIfNull(outStr);
        DatabaseConnection conn = GetFreeConnection();
        _ = conn;
        string escapedStr = MySqlHelper.EscapeString(str);
        outStr.Append(escapedStr);
    }

    protected override bool SendQuery(DatabaseConnection con, string sql, bool self)
    {
        ArgumentNullException.ThrowIfNull(con);

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
            CLog.Error("[MySQLDatabase]", $"Error executing query: {ex.Message}");
            return false;
        }
    }

    protected override QueryResult StoreQueryResult(DatabaseConnection con)
    {
        ArgumentNullException.ThrowIfNull(con);

        try
        {
            using var cmd = new MySqlCommand("SELECT LAST_INSERT_ID()", (MySqlConnection)con);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new MySQLQueryResult(reader, fieldCount, rowCount);
            }
        }
        catch (MySqlException ex)
        {
            CLog.Error("[MySQLDatabase]", $"Error storing query result: {ex.Message}");
        }

        return null;
    }

    protected override void BeginTransaction(DatabaseConnection con)
    {
        ArgumentNullException.ThrowIfNull(con);

        try
        {
            using var cmd = new MySqlCommand("START TRANSACTION", (MySqlConnection)con);
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            CLog.Error("[MySQLDatabase]", $"Error starting transaction: {ex.Message}");
        }
    }

    protected override void EndTransaction(DatabaseConnection con)
    {
        ArgumentNullException.ThrowIfNull(con);

        try
        {
            using var cmd = new MySqlCommand("COMMIT", (MySqlConnection)con);
            cmd.ExecuteNonQuery();
        }
        catch (MySqlException ex)
        {
            Log.Error("[MySQLDatabase]", $"Error committing transaction: {ex.Message}");
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

    public override string EscapeString(string esc, DatabaseConnection con)
    {
        ArgumentNullException.ThrowIfNull(esc);
        return EscapeString(esc);
    }

    protected override void SetThreadName(string v)
    {
        // Set the name of the current thread
        CurrentThread.Name = v;
    }

    // Override Query() to properly execute SELECT statements via ExecuteReader.
    // The base Database.Query() calls SendQuery() (ExecuteNonQuery) + StoreQueryResult()
    // which discards rows — this override fixes that for MySQL.
    public override QueryResult Query(string QueryString, params object[] args)
    {
        if (string.IsNullOrEmpty(_queryConnStr)) return null;

        string sql = args.Length > 0 ? string.Format(QueryString, args) : QueryString;

        try
        {
            using var conn = new MySqlConnection(_queryConnStr);
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            int fc = reader.FieldCount;
            var allRows = new List<Field[]>();
            while (reader.Read())
            {
                var row = new Field[fc];
                for (int i = 0; i < fc; i++)
                {
                    row[i] = new Field();
                    row[i].SetValue(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }
                allRows.Add(row);
            }
            if (allRows.Count == 0) return null;
            return new MySQLInMemoryResult(allRows, (uint)fc);
        }
        catch (Exception ex)
        {
            CLog.Error("[MySQLDatabase]", $"Query error: {ex.Message} | SQL: {sql}");
            return null;
        }
    }

    public override QueryResult QueryNA(string QueryString) => Query(QueryString);

    private sealed class MySQLInMemoryResult : QueryResult
    {
        private readonly List<Field[]> _rows;
        private int _index = -1;

        internal MySQLInMemoryResult(List<Field[]> rows, uint fieldCount)
            : base(fieldCount, (uint)rows.Count)
        {
            _rows = rows;
        }

        public override bool NextRow()
        {
            _index++;
            if (_index >= _rows.Count) return false;
            mCurrentRow = _rows[_index];
            return true;
        }
    }
}

internal class MySQLQueryResult(MySqlDataReader reader, uint fieldCount, uint rowCount) : QueryResult(fieldCount, rowCount)
{
    private readonly MySqlDataReader reader = reader;

    public override bool NextRow()
    {
        return reader.Read();
    }
}

public class DatabaseConnection
{
    public MySqlConnection MySql { get; set; }
    public SemaphoreSlim Busy { get; } = new SemaphoreSlim(1, 1);

    internal void Dispose()
    {
        MySql?.Dispose();
    }

    public static explicit operator MySqlConnection(DatabaseConnection v)
    {
        return v.MySql;
    }
}

