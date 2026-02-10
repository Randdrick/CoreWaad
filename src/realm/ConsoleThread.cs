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
using System.Threading;
using WaadShared;
using static WaadShared.Common;

#nullable enable

namespace WaadRealmServer;

/// <summary>
/// Console thread for realm server - handles local stdin input and command processing.
/// </summary>
public class ConsoleThread : WaadShared.Threading.ThreadBase
{
    private bool m_running = true;

    public ConsoleThread()
    {
    }

    public override bool Run(CancellationToken token)
    {
        SetThreadName("RealmConsole");

        try
        {
            while (m_running && !Master.StopEvent && !token.IsCancellationRequested)
            {
                string? input = Console.ReadLine();
                if (input == null || Master.StopEvent || token.IsCancellationRequested)
                    break;

                // Use ConsoleListener to handle the input
                LocalConsole console = new();
                ConsoleListener.HandleConsoleInput(console, input);
            }
        }
        catch (Exception ex)
        {
            CLog.Error("[ConsoleThread]", "Exception in console thread: {0}", ex.Message);
        }
        finally
        {
            m_running = false;
            CLog.Notice("[ConsoleThread]", "Console thread exiting");
        }

        return true;
    }

    public void Terminate()
    {
        m_running = false;
    }
}

/// <summary>
/// Local console implementation for stdin/stdout.
/// </summary>
public class LocalConsole : IConsole
{
    public void Write(string format, params object[] args)
    {
        try
        {
            if (args.Length > 0)
                Console.WriteLine(format, args);
            else
                Console.WriteLine(format);
        }
        catch (Exception ex)
        {
            CLog.Error("[LocalConsole] Error writing to console: {0}", ex.Message);
        }
    }
}
