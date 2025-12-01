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

using static System.Threading.Thread;

namespace WaadShared.Database;

public class SQLiteDatabase : Database
{
private SQLiteConnection Connection;
private string mHostname;
private string mUsername;
private string mPassword;
private string mDatabaseName;
private new int mConnectionCount;

public bool DumpDatabase(string filePath)
{
    try
    {
        if (Connection == null)
            throw new InvalidOperationException("Aucune connexion SQLite active.");

        // SQLite n'a pas d'outil natif, on exporte via l'API
        using var cmd = new SQLiteCommand(".backup main '" + filePath.Replace("'", "''") + "'", Connection);
        cmd.ExecuteNonQuery();
        return true;
    }
    catch (Exception ex)
    {
        Log.Error("SQLiteDatabase", $"DumpDatabase exception: {ex.Message}");
        return false;
    }
}

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

public bool Initialize(string Hostname, uint port, string Username, string Password, string DatabaseName, uint ConnectionCount, uint BufferSize)
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
            Pooling = true
        }.ToString();

        Connection = new SQLiteConnection(connString);
        Connection.Open();
    }
    catch (SQLiteException ex)
    {
        Log.Error("SQLiteDatabase", $"Connection failed due to: `{ex.Message}`");
        return false;
    }

    Initialize();
    return true;
}

public void Shutdown()
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

protected override bool SendQuery(DatabaseConnection con, string sql, bool self)
{
    ArgumentNullException.ThrowIfNull(con);

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
    ArgumentNullException.ThrowIfNull(con);

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

public override string EscapeString(string esc, DatabaseConnection con)
{
    ArgumentNullException.ThrowIfNull(esc);
    return EscapeString(esc);
}

public override void EscapeLongString(string str, uint len, StringBuilder outStr)
{
    ArgumentNullException.ThrowIfNull(str);
    ArgumentNullException.ThrowIfNull(outStr);

    string escapedStr = EscapeString(str);
    outStr.Append(escapedStr);
}
}

public class SQLiteQueryResult(SQLiteDataReader reader) : QueryResult((uint)reader.FieldCount, (uint)reader.RecordsAffected)
{
private readonly SQLiteDataReader reader = reader;

public override bool NextRow() => reader.Read();
}

