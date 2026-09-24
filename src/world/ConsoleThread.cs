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
using WaadShared;
using ThreadBase = WaadShared.Threading.ThreadBase;

using static WaadShared.Common;

namespace WaadWorldServer;

// Portage minimal du thread console local de Master::Run (ConsoleThread * console = new ConsoleThread();).
// La table de commandes complète (ConsoleCommands.cpp) reste à porter.
public class ConsoleThread : ThreadBase
{
    private volatile bool m_running = true;
    private readonly Logger sLog = new();

    public override bool Run(CancellationToken token)
    {
        SetThreadName("WorldConsole");

        try
        {
            while (m_running && !Master.StopEvent && !token.IsCancellationRequested)
            {
                string line = Console.ReadLine();
                if (line == null || Master.StopEvent || token.IsCancellationRequested)
                    break;

                // TODO: brancher un véritable interpréteur de commandes World une fois porté.
                sLog.OutString("[Console] > {0}", line);
            }
        }
        catch (Exception ex)
        {
            sLog.OutError("[ConsoleThread] Exception : {0}", ex.Message);
        }
        finally
        {
            m_running = false;
        }

        return true;
    }

    public void Terminate() => m_running = false;
}
