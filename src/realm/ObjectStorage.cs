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
using System.Collections.Generic;
using WaadShared;

namespace WaadRealmServer;

// Definitions des formats de tables
public static class TableFormats
{
    public const string ItemPrototypeFormat = "uuuussssuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuffuffuuuuuuuuuufuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuusuuuuuuuuuuuuuuuuuuuuuuuuuuuuu";
    public const string CreatureNameFormat = "usssuuuuuuuuuuffcc";
    public const string GameObjectNameFormat = "uuusuuuuuuuuuuuuuuuuuuuuuuuuf";
    public const string ItemPageFormat = "usu";
    public const string RealmMapInfoFormat = "uuuuufffusuuuuuuufuu";
}

// Déclarations des stockages globaux
public static class Storage
{
    public static readonly SQLStorage<ItemPrototype,ArrayStorageContainer<ItemPrototype>> ItemPrototypeStorage = new();
    public static readonly SQLStorage<CreatureInfo, HashMapStorageContainer<CreatureInfo>> CreatureNameStorage = new();
    public static readonly SQLStorage<GameObjectInfo, HashMapStorageContainer<GameObjectInfo>> GameObjectNameStorage = new();
    public static readonly SQLStorage<ItemPage, HashMapStorageContainer<ItemPage>> ItemPageStorage = new();
    public static readonly SQLStorage<MapInfo, HashMapStorageContainer<MapInfo>> WorldMapInfoStorage = new();
}

public class Task(Action action)
{
    public Action Action { get; } = action;
}

public class TaskList
{
    private readonly List<Task> tasks = [];

    public void AddTask(Task task)
    {
        tasks.Add(task);
    }

    public void ExecuteAll()
    {
        foreach (var task in tasks)
        {
            task.Action();
        }
    }
}

// Fonctions principales
public static class StorageManager
{
    public static void FillTaskList(TaskList tl)
    {
        tl.AddTask(new Task(() => Storage.ItemPrototypeStorage.Load("items", TableFormats.ItemPrototypeFormat)));
        tl.AddTask(new Task(() => Storage.CreatureNameStorage.Load("creature_names", TableFormats.CreatureNameFormat)));
        tl.AddTask(new Task(() => Storage.GameObjectNameStorage.Load("gameobject_names", TableFormats.GameObjectNameFormat)));
        tl.AddTask(new Task(() => Storage.ItemPageStorage.Load("itempages", TableFormats.ItemPageFormat)));
        tl.AddTask(new Task(() => Storage.WorldMapInfoStorage.Load("realmmap_info", TableFormats.RealmMapInfoFormat)));
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
