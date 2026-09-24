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

using WaadShared;
using WaadShared.Config;
using WaadShared.Database;

namespace WaadWorldServer;

public static class WorldDatabaseManager
{
    // === Bases de données World === 
    private static readonly MySQLDatabase sWorldSQL = new();
    private static readonly PostgresDatabase pWorldSQL = new();
    private static readonly SQLiteDatabase slWorldSQL = new();

    // === Bases de données Character === 
    private static readonly MySQLDatabase sCharacterSQL = new();
    private static readonly PostgresDatabase pCharacterSQL = new();
    private static readonly SQLiteDatabase slCharacterSQL = new();

    // === Types de bases de données === 
    public static int DbType { get; private set; } = 1; // Par défaut MySQL
    public static int CharacterDbType { get; private set; } = 1; // Par défaut MySQL

    // === Méthodes pour obtenir les instances de bases de données ===
    
    /// <summary>
    /// Obtient l'instance active de la base de données World
    /// </summary>
    public static Database GetWorldDatabase()
    {
        return DbType switch
        {
            1 => sWorldSQL,
            2 => pWorldSQL,
            3 => slWorldSQL,
            _ => null
        };
    }

    /// <summary>
    /// Obtient l'instance active de la base de données Character
    /// </summary>
    public static Database GetCharacterDatabase()
    {
        return CharacterDbType switch
        {
            1 => sCharacterSQL,
            2 => pCharacterSQL,
            3 => slCharacterSQL,
            _ => null
        };
    }

    // === Initialisation des bases de données ===
    
    /// <summary>
    /// Initialise les connexions aux bases de données World et Character
    /// </summary>
    public static bool InitializeDatabases(ConfigMgr config, Logger logger)
    {
        // === Initialisation de la base World ===
        string hostname = config.WorldConfig.GetString("Database.World", "Hostname", "localhost");
        string username = config.WorldConfig.GetString("Database.World", "Username", "root");
        string password = config.WorldConfig.GetString("Database.World", "Password", "");
        string database = config.WorldConfig.GetString("Database.World", "Name", "waad_world");
        int port = config.WorldConfig.GetInt32("Database.World", "Port", 3306);
        int type = config.WorldConfig.GetInt32("Database.World", "Type", 1); // 1=MySQL, 2=PostgreSQL, 3=SQLite

        // Validation de la configuration World
        bool worldConfigValid = type switch
        {
            1 or 2 => !string.IsNullOrEmpty(hostname) 
                   && !string.IsNullOrEmpty(username) 
                   && !string.IsNullOrEmpty(database) 
                   && port > 0,
            3 => !string.IsNullOrEmpty(database),
            _ => false
        };

        if (!worldConfigValid)
        {
            logger.OutError("[Database] Configuration World DB invalide ou manquante.");
            return false;
        }

        // Initialisation selon le type
        bool worldOk = false;
        uint connectionCount = (uint)config.WorldConfig.GetInt32("Database.World", "ConnectionCount", 5);
        
        if (type == 1)
        {
            worldOk = sWorldSQL.Initialize(hostname, (uint)port, username, password, database, connectionCount, 16384);
        }
        else if (type == 2)
        {
            worldOk = pWorldSQL.Initialize(hostname, (uint)port, username, password, database, connectionCount, 16384);
        }
        else if (type == 3)
        {
            worldOk = slWorldSQL.Initialize(hostname, (uint)port, username, password, database, connectionCount, 16384);
        }

        if (!worldOk)
        {
            logger.OutError("[Database] Échec de l'initialisation de la connexion à la base World.");
            return false;
        }

        DbType = type;
        logger.OutString("[Database] Connexion à la base World établie avec succès.");

        // === Initialisation de la base Character ===
        string charHostname = config.WorldConfig.GetString("Database.Character", "Hostname");
        string charUsername = config.WorldConfig.GetString("Database.Character", "Username");
        string charPassword = config.WorldConfig.GetString("Database.Character", "Password");
        string charDatabase = config.WorldConfig.GetString("Database.Character", "Name");
        int charPort = config.WorldConfig.GetInt32("Database.Character", "Port");
        int charType = config.WorldConfig.GetInt32("Database.Character", "Type", type);

        // Validation de la configuration Character
        bool charConfigValid = charType switch
        {
            1 or 2 => !string.IsNullOrEmpty(charHostname) 
                   && !string.IsNullOrEmpty(charUsername) 
                   && !string.IsNullOrEmpty(charDatabase) 
                   && charPort > 0,
            3 => !string.IsNullOrEmpty(charDatabase),
            _ => false
        };

        bool charOk = false;
        uint charConnectionCount = (uint)config.WorldConfig.GetInt32("Database.Character", "ConnectionCount", 5);
        
        if (charConfigValid)
        {
            if (charType == 1)
            {
                charOk = sCharacterSQL.Initialize(charHostname, (uint)charPort, charUsername, charPassword, charDatabase, charConnectionCount, 16384);
            }
            else if (charType == 2)
            {
                charOk = pCharacterSQL.Initialize(charHostname, (uint)charPort, charUsername, charPassword, charDatabase, charConnectionCount, 16384);
            }
            else if (charType == 3)
            {
                charOk = slCharacterSQL.Initialize(charHostname, (uint)charPort, charUsername, charPassword, charDatabase, charConnectionCount, 16384);
            }

            if (!charOk)
            {
                logger.OutWarning("[Database] Échec de l'initialisation de la base Character, utilisation de la base World pour les requêtes character.");
                CharacterDbType = DbType; // Utilise le même type que World
            }
            else
            {
                CharacterDbType = charType;
                logger.OutString("[Database] Connexion à la base Character établie avec succès.");
            }
        }
        else
        {
            logger.OutWarning("[Database] Configuration Character DB manquante, utilisation de la base World pour les requêtes character.");
            CharacterDbType = DbType; // Utilise le même type que World
            charOk = true;
        }

        return charOk;
    }

    // === Fermeture des bases de données ===
    
    /// <summary>
    /// Ferme toutes les connexions aux bases de données
    /// </summary>
    public static void RemoveDatabases()
    {
        // Fermeture de la base World
        if (DbType == 1)
            sWorldSQL.Shutdown();
        else if (DbType == 2)
            pWorldSQL.Shutdown();
        else if (DbType == 3)
            slWorldSQL.Shutdown();

        // Fermeture de la base Character
        if (CharacterDbType == 1)
            sCharacterSQL.Shutdown();
        else if (CharacterDbType == 2)
            pCharacterSQL.Shutdown();
        else if (CharacterDbType == 3)
            slCharacterSQL.Shutdown();
    }
}