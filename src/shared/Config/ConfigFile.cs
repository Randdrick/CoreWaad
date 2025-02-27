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
using System.IO;

namespace WaadShared.Config;

public class ConfigFile : IDisposable
{
    private readonly FileIniDataParser _iniParser;
    private FileIniDataParser _iniData;
    private bool _disposed = false;

    public ConfigFile()
    {
        _iniParser = new FileIniDataParser();
        _iniData = null;
    }

    public bool SetSource(string file)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file))
        {
            return false;
        }

        try
        {
            _iniData = _iniParser.ReadFile(file);
            return _iniData != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading config file: {ex.Message}");
            return false;
        }
    }

    public string GetValue(string section, string key)
    {
        if (_iniData == null)
        {
            throw new InvalidOperationException("INI data is not loaded.");
        }

        if (string.IsNullOrEmpty(section))
        {
            throw new ArgumentNullException(nameof(section));
        }

        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        var sectionData = _iniData[section] ?? throw new KeyNotFoundException($"Section '{section}' not found.");
        var value = sectionData[key] ?? throw new KeyNotFoundException($"Key '{key}' not found in section '{section}'.");
        return value;
    }

    public void SetValue(string section, string key, string value)
    {
        if (_iniData == null)
        {
            throw new InvalidOperationException("INI data is not loaded.");
        }

        if (string.IsNullOrEmpty(section))
        {
            throw new ArgumentNullException(nameof(section));
        }

        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }

        ArgumentNullException.ThrowIfNull(value);

        if (_iniData[section] == null)
        {
            _iniData[section] = [];
        }

        _iniData[section][key] = value;
    }

    public void Save(string file)
    {
        if (_iniData == null)
        {
            throw new InvalidOperationException("INI data is not loaded.");
        }

        if (string.IsNullOrEmpty(file))
        {
            throw new ArgumentNullException(nameof(file));
        }

        try
        {
            FileIniDataParser.WriteFile(file, _iniData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config file: {ex.Message}");
        }
    }

    public string GetString(string section, string name, string def = "")
    {
        if (_iniData == null || string.IsNullOrEmpty(section) || string.IsNullOrEmpty(name))
        {
            return def;
        }

        return _iniData[section]?[name] ?? def;
    }

    public bool GetBoolean(string section, string name, bool def = false)
    {
        if (_iniData == null || string.IsNullOrEmpty(section) || string.IsNullOrEmpty(name))
        {
            return def;
        }

        if (bool.TryParse(_iniData[section]?[name], out bool result))
        {
            return result;
        }
        return def;
    }

    public static bool GetBoolDefault(string key, bool defaultValue)
    {
        // Dummy implementation
        return defaultValue;
    }

    public int GetInt32(string section, string name, int def = 0)
    {
        if (_iniData == null || string.IsNullOrEmpty(section) || string.IsNullOrEmpty(name))
        {
            return def;
        }

        if (int.TryParse(_iniData[section]?[name], out int result))
        {
            return result;
        }
        return def;
    }

    public float GetFloat(string section, string name, float def = 0)
    {
        if (_iniData == null || string.IsNullOrEmpty(section) || string.IsNullOrEmpty(name))
        {
            return def;
        }

        if (float.TryParse(_iniData[section]?[name], out float result))
        {
            return result;
        }
        return def;
    }

    public double GetDouble(string section, string name, double def = 0)
    {
        if (_iniData == null || string.IsNullOrEmpty(section) || string.IsNullOrEmpty(name))
        {
            return def;
        }

        if (double.TryParse(_iniData[section]?[name], out double result))
        {
            return result;
        }
        return def;
    }

    ~ConfigFile()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Cleanup managed resources if needed
            }

            // Cleanup unmanaged resources if needed
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class ConfigMgr
{
    public ConfigFile MainConfig { get; set; }
    public ConfigFile RealmConfig { get; set; }
    public ConfigFile ClusterConfig { get; set; }

    public ConfigMgr()
    {
        MainConfig = new ConfigFile();
        RealmConfig = new ConfigFile();
        ClusterConfig = new ConfigFile();
    }
}

public static class Config
{
    public static ConfigMgr Instance { get; } = new ConfigMgr();
}
