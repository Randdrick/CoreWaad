/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2025 WAAD Team <https://arbonne.games-rpg.net/>
 *
 * From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore
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
 */

using System;
using WaadShared;
using WaadShared.Config;
using WaadShared.Database;

namespace WaadRealmServer;

public static class RealmDatabaseManager
{

    // Permet d'obtenir l'instance active de base de données (MySQL, Postgres, SQLite)
    public static Database GetDatabase()
    {
        return DbType switch
        {
            1 => sRealmSQL,
            2 => pRealmSQL,
            3 => slRealmSQL,
            _ => null
        };
    }

    private static readonly MySQLDatabase sRealmSQL = new();
    private static readonly PostgresDatabase pRealmSQL = new();
    private static readonly SQLiteDatabase slRealmSQL = new();
    public static int DbType { get; private set; } = 1; // Default to MySQL

    internal static string GetConnectionString(int dbType, ConfigMgr configMgr)
    {
        string hostname = configMgr.ClusterConfig.GetString("Database.Realm", "Hostname");
        string username = configMgr.ClusterConfig.GetString("Database.Realm", "Username");
        string password = configMgr.ClusterConfig.GetString("Database.Realm", "Password");
        string database = configMgr.ClusterConfig.GetString("Database.Realm", "Name");
        int port = configMgr.ClusterConfig.GetInt32("Database.Realm", "Port");

        return dbType switch
        {
            1 => $"Server={hostname};Port={port};Database={database};Uid={username};Pwd={password};",
            2 => $"Host={hostname};Port={port};Username={username};Password={password};Database={database};",
            3 => $"Data Source={database};Version=3;",
            _ => throw new InvalidOperationException("Type de base de données non supporté.")
        };
    }

    public static bool InitializeDatabases(ConfigMgr configMgr, Logger sLog)
    {
        // Configure Main Database
        string hostname = configMgr.ClusterConfig.GetString("Database.Realm", "Hostname");
        string username = configMgr.ClusterConfig.GetString("Database.Realm", "Username");
        string password = configMgr.ClusterConfig.GetString("Database.Realm", "Password");
        string database = configMgr.ClusterConfig.GetString("Database.Realm", "Name");
        int port = configMgr.ClusterConfig.GetInt32("Database.Realm", "Port");
        int type = configMgr.ClusterConfig.GetInt32("Database.Realm", "Type", 1); // Default is MySQL

        bool result = !string.IsNullOrEmpty(hostname) && !string.IsNullOrEmpty(username)
                    && !string.IsNullOrEmpty(database) && port > 0;

        if (!result)
        {
            sLog.OutError("[Database] Invalid or missing Realm DB config.");
            return false;
        }

        _ = type switch
        {
            1 => $"Server={hostname};Port={port};Database={database};Uid={username};Pwd={password};",
            2 => $"Host={hostname};Port={port};Username={username};Password={password};Database={database};",
            3 => $"Data Source={database};Version=3;",
            _ => throw new InvalidOperationException("Unsupported database type.")
        };

        // Initialize it
        bool ok = (type == 1 && sRealmSQL.Initialize(hostname, (uint)port, username, password, database,
                        (uint)configMgr.ClusterConfig.GetInt32("Database.Realm", "ConnectionCount", 5), 16384))
               || (type == 2 && pRealmSQL.Initialize(hostname, (uint)port, username, password, database,
                        (uint)configMgr.ClusterConfig.GetInt32("Database.Realm", "ConnectionCount", 5), 16384))
               || (type == 3 && slRealmSQL.Initialize(hostname, (uint)port, username, password, database,
                        (uint)configMgr.ClusterConfig.GetInt32("Database.Realm", "ConnectionCount", 5), 16384));

        if (!ok)
        {
            sLog.OutError("[Database] Failed to initialize Realm database connection.");
            return false;
        }

        DbType = type;
        return true;
    }

    public static void RemoveDatabase()
    {
        if (DbType == 1)
            sRealmSQL.Shutdown();
        else if (DbType == 2)
            pRealmSQL.Shutdown();
        else if (DbType == 3)
            slRealmSQL.Shutdown();
    }
}
