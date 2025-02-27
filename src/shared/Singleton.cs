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

namespace WaadShared;

public class Singleton<T> where T : class, new()
{
    private static T _instance;
    private static readonly object _lock = new();
    public static T Instance => _instance;

    protected Singleton()
    {
        if (_instance != null)
        {
            throw new InvalidOperationException("Singleton instance already exists.");
        }
    }

    public static T GetInstance()
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new T();
            }
        }
        return _instance;
    }

    public static void DestroyInstance()
    {
        lock (_lock)
        {
            _instance = null;
        }
    }

    public static T GetInstancePtr()
    {
        return _instance;
    }
}

