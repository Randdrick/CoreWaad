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

using System.Collections.Generic;
using System.IO;

namespace WaadShared.Config;

internal class FileIniDataParser
{
    public Dictionary<string, Dictionary<string, string>> Sections { get; private set; }

    public FileIniDataParser()
    {
        Sections = [];
    }

    public FileIniDataParser ReadFile(string filePath)
    {
        Sections.Clear();
        var lines = File.ReadAllLines(filePath);
        string currentSection = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                currentSection = trimmedLine[1..^1];
                if (!Sections.ContainsKey(currentSection))
                {
                    Sections[currentSection] = [];
                }
            }
            else if (!string.IsNullOrEmpty(trimmedLine) && currentSection != null)
            {
                var parts = trimmedLine.Split(['='], 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    Sections[currentSection][key] = value;
                }
            }
        }

        return this;
    }

    public static void WriteFile(string filePath, FileIniDataParser iniData)
    {
        using var writer = new StreamWriter(filePath);
        foreach (var section in iniData.Sections)
        {
            writer.WriteLine($"[{section.Key}]");
            foreach (var keyValue in section.Value)
            {
                writer.WriteLine($"{keyValue.Key}={keyValue.Value}");
            }
            writer.WriteLine();
        }
    }

    public Dictionary<string, string> this[string section]
    {
        get
        {
            if (!Sections.TryGetValue(section, out Dictionary<string, string> value))
            {
                value = [];
                Sections[section] = value;
            }
            return value;
        }
        set
        {
            Sections[section] = value;
        }
    }
}
