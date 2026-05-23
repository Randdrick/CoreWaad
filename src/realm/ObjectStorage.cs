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

using static WaadShared.LogonCommHandler;

namespace WaadRealmServer;

// Definitions des formats de tables
public static class TableFormats
{
    public const string ItemPrototypeFormat =
        "uuuussssuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + // Champs simples (entry à HolidayId, incluant Faction)
        "uuuuuuuuuuuuuuuuuuuu" + // StatsData (stat_type1 à stat_value10)
        "ffffff" + // DamageData (dmg_min1, dmg_max1, dmg_type1)
        "ffffff" + // DamageData (dmg_min2, dmg_max2, dmg_type2)
        "ffffff" + // DamageData (dmg_min3, dmg_max3, dmg_type3)
        "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuu" + // SpellsData (spellid_1 à spellcategorycooldown_5)
        "uuuuuuuuu"; // SocketsData (socket_color_1, unk201_3, socket_color_2, unk201_5, socket_color_3, unk201_7, socket_bonus, GemProperties, ReqDisenchantSkill)

    public const string CreatureNameFormat = "usssuuuuuuuuuuffcc";
    public const string GameObjectNameFormat = "uuusuuuuuuuuuuuuuuuuuuuuuuuuf";
    public const string ItemPageFormat = "usu";
    public const string RealmMapInfoFormat = "uuuuufffusuuuuuuufuu";

}

// Déclarations des stockages globaux
public static class Storage
{
    public static readonly SQLStorage<ItemPrototype, ArrayStorageContainer<ItemPrototype>> ItemPrototypeStorage = new();
    public static readonly SQLStorage<CreatureInfo, HashMapStorageContainer<CreatureInfo>> CreatureNameStorage = new();
    public static readonly SQLStorage<GameObjectInfo, HashMapStorageContainer<GameObjectInfo>> GameObjectNameStorage = new();
    public static readonly SQLStorage<ItemPage, HashMapStorageContainer<ItemPage>> ItemPageStorage = new();
    public static readonly SQLStorage<MapInfo, HashMapStorageContainer<MapInfo>> WorldMapInfoStorage = new();
}

// Fonctions principales
public static class StorageManager
{
    private static int DbType => RealmDatabaseManager.DbType;

    public static void FillTaskList(TaskList tl, ConfigMgr configMgr)
    {
        // Vérifier que les DBCs sont complètement chargés
        if (!DBCStores.IsDbcLoaded)
        {
            CLog.Error("[StorageManager]", "ERREUR: Les DBCs ne sont pas complètement chargés. Impossible de remplir les tâches de stockage.");
            throw new InvalidOperationException("DBCs must be fully loaded before filling task list.");
        }

        string connectionString = RealmDatabaseManager.GetConnectionString(DbType, configMgr);
        CLog.Debug("[StorageManager]", R_D_STORAGE_CONNSTRING, connectionString ?? "NULL");

        // Chargement des tables de stockage
        tl.AddTask(new Task(() => Storage.ItemPrototypeStorage.Load("items", TableFormats.ItemPrototypeFormat, connectionString, DbType)));
        tl.AddTask(new Task(() => Storage.CreatureNameStorage.Load("creature_names", TableFormats.CreatureNameFormat, connectionString, DbType)));
        tl.AddTask(new Task(() => Storage.GameObjectNameStorage.Load("gameobject_names", TableFormats.GameObjectNameFormat, connectionString, DbType)));
        tl.AddTask(new Task(() => Storage.ItemPageStorage.Load("itempages", TableFormats.ItemPageFormat, connectionString, DbType)));
        tl.AddTask(new Task(() => Storage.WorldMapInfoStorage.Load("realmmap_info", TableFormats.RealmMapInfoFormat, connectionString, DbType)));
    }

    public static void Cleanup()
    {
        Storage.ItemPrototypeStorage.Cleanup();
        Storage.CreatureNameStorage.Cleanup();
        Storage.GameObjectNameStorage.Cleanup();
        Storage.ItemPageStorage.Cleanup();
        Storage.WorldMapInfoStorage.Cleanup();
    }

    public static bool ReloadTable(string tableName)
    {
        switch (tableName.ToLower())
        {
            case "items":
                Storage.ItemPrototypeStorage.Reload();
                break;
            case "creature_names":
                Storage.CreatureNameStorage.Reload();
                break;
            case "gameobject_names":
                Storage.GameObjectNameStorage.Reload();
                break;
            case "itempages":
                Storage.ItemPageStorage.Reload();
                break;
            case "realmmap_info":
                Storage.WorldMapInfoStorage.Reload();
                break;
            default:
                return false;
        }
        return true;
    }
}
