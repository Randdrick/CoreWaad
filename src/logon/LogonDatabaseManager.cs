/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2021 WAAD Team <https://arbonne.games-rpg.net/>
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
using System.IO;
using WaadShared;
using WaadShared.Config;
using WaadShared.Database;

using static WaadShared.Main;

namespace LogonServer;
public static class DatabaseManager
{
    private static readonly MySQLDatabase sLogonSQL = new();
    private static readonly PostgresDatabase pLogonSQL = new();
    private static readonly SQLiteDatabase slLogonSQL = new();
    private static readonly string configFile = Path.Combine(AppContext.BaseDirectory, "waad-logonserver.ini");
    private static int dbType = 1; // Default to MySQL
    public static bool InitializeDatabases(ConfigMgr configMgr, Logger sLog)
    {
        if (!configMgr.MainConfig.SetSource(configFile))
        {

            sLog.OutError(L_N_MAIN_1);
            return false;
        }

        // Configure Main Database
        string lhostname = configMgr.MainConfig.GetString("Database.Logon", "Hostname");
        string lusername = configMgr.MainConfig.GetString("Database.Logon", "Username");
        string lpassword = configMgr.MainConfig.GetString("Database.Logon", "Password");
        string ldatabase = configMgr.MainConfig.GetString("Database.Logon", "Name");
        int lport = configMgr.MainConfig.GetInt32("Database.Logon", "Port");
        int ltype = configMgr.MainConfig.GetInt32("Database.Logon", "Type", 1); // Default is MySQL

        bool result = !string.IsNullOrEmpty(lhostname) && !string.IsNullOrEmpty(lusername)
                    && !string.IsNullOrEmpty(ldatabase) && lport > 0;

        if (!result)
        {
            sLog.OutString(L_N_MAIN);
            return result;
        }

        sLog.SetScreenLoggingLevel(configMgr.MainConfig.GetInt32("LogLevel", "Screen"));

        string connectionString = ltype switch
        {
            1 => $"Server={lhostname};Port={lport};Database={ldatabase};Uid={lusername};Pwd={lpassword};",
            2 => $"Host={lhostname};Port={lport};Username={lusername};Password={lpassword};Database={ldatabase};",
            3 => $"Data Source={ldatabase};Version=3;",
            _ => throw new InvalidOperationException("Unsupported database type.")
        };

        SLogonSQL.SetConnectionString(connectionString, ltype);

        // Initialize it
        if ((ltype == 1) && (!sLogonSQL.Initialize(lhostname, (uint)lport, lusername, lpassword, ldatabase,
                                  (uint)configMgr.MainConfig.GetInt32("Database.Logon", "ConnectionCount", 5), 16384))
                                  || (ltype == 2) && (!pLogonSQL.Initialize(lhostname, (uint)lport, lusername, lpassword, ldatabase,
                                  (uint)configMgr.MainConfig.GetInt32("Database.Logon", "ConnectionCount", 5), 16384))
                                  || (ltype == 3) && (!slLogonSQL.Initialize(lhostname, (uint)lport, lusername, lpassword, ldatabase,
                                  (uint)configMgr.MainConfig.GetInt32("Database.Logon", "ConnectionCount", 5), 16384)))

        {
            sLog.OutError(L_E_MAIN);
            return false;
        }

        dbType = ltype;
        return true;
    }
    public static void RemoveDatabase()
    {
        if (dbType == 1)
        {
            sLogonSQL.Shutdown();
        }
        else if (dbType == 2)
        {
            pLogonSQL.Shutdown();
        }
        else if (dbType == 3)
        {
            slLogonSQL.Shutdown();
        }
    }
}