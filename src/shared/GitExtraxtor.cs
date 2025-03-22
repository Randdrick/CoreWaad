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
using System.Linq;
using LibGit2Sharp;

namespace WaadShared;

public class GitExtractor
{
    static GitExtractor()
    {
        // Définit le répertoire courant comme étant le répertoire de l'exécutable
        string baseDirectory = AppContext.BaseDirectory;
        string relativePath = @"..\..\..\"; // Chemin relatif vers le dépôt Git
        Environment.CurrentDirectory = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
    }

    public static string GetBranchName()
    {
        string repoPath = Repository.Discover(Environment.CurrentDirectory);
        if (string.IsNullOrEmpty(repoPath))
        {
            Console.WriteLine("Warning: Unable to locate a valid Git repository.");
            return "unknown"; // Retourne une valeur par défaut si le chemin est invalide
        }

        using var repo = new Repository(repoPath);
        return repo.Head.FriendlyName;
    }

    public static int GetCommitCount()
    {
        string repoPath = Repository.Discover(Environment.CurrentDirectory);
        if (string.IsNullOrEmpty(repoPath))
        {
            Console.WriteLine("Warning: Unable to locate a valid Git repository.");
            return 0; // Retourne une valeur par défaut si le chemin est invalide
        }

        using var repo = new Repository(repoPath);
        return repo.Commits.Count();
    }
}