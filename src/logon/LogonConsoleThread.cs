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
using System.Threading;

using static WaadShared.ThreadBase;
using static WaadShared.LogonConsole;

#nullable enable

namespace LogonServer;

public class LogonConsoleThread : WaadShared.Threading.ThreadBase
{
    public bool kill;

    public LogonConsoleThread()
    {
        kill = false;
    }

    public bool Run()
    {
        SetThreadName(L_N_LOGONCON_3);
        LogonConsole.Instance._thread = this;

        try
        {
            while (!kill)
            {
                string? cmd = Console.ReadLine();
                if (kill || cmd == null) break;

                LogonConsole.Instance.ProcessCmd(cmd);
            }
        }
        finally
        {
            LogonConsole.Instance._thread = null;
        }

        return true;
    }

    public override bool Run(CancellationToken token)
    {
        SetThreadName(L_N_LOGONCON_3);
        LogonConsole.Instance._thread = this;

        try
        {
            while (!kill && !token.IsCancellationRequested)
            {
                string? cmd = Console.ReadLine();
                if (kill || cmd == null || token.IsCancellationRequested) break;

                LogonConsole.Instance.ProcessCmd(cmd);
            }
        }
        finally
        {
            LogonConsole.Instance._thread = null;
        }

        return true;
    }
}

