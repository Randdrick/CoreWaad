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
using System.Threading;

public class ConsoleHandler
{
    private bool _isRunning;
    private bool _threadRunning;
    private LocalConsole _localConsole;

    public ConsoleHandler()
    {
        _isRunning = true;
        _threadRunning = true;
        _localConsole = new LocalConsole();
    }

    public bool Run()
    {
        while (_isRunning)
        {
            if (_threadRunning)
            {
                string cmd = Console.ReadLine();
                if (cmd == null)
                    continue;

                cmd = cmd.Trim();
                HandleConsoleInput(cmd);
            }
            else
            {
                break;
            }
        }
        _isRunning = false;
        return false;
    }

    private void HandleConsoleInput(string cmd)
    {
        // Implémentez la logique de traitement des commandes ici
        _localConsole.Write("Command received: {0}\n", cmd);
    }

    public void Stop()
    {
        _isRunning = false;
        _threadRunning = false;
    }
}