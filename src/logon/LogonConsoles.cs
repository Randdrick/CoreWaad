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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WaadShared;

using static WaadShared.LogonConsole;

namespace LogonServer;

public class LogonConsole
{
    private static LogonConsole instance;
    public static LogonConsole Instance => instance ??= new LogonConsole();

    public LogonConsoleThread _thread;
    public bool mrunning = true;

    private LogonConsole() { }

    public static void TranslateRehash(string str)
    {
        CLog.Notice("[LogonConsole]", L_N_LOGONCON);
        LogonServer.Rehash(str);
    }

    public void Kill()
    {
        if (_thread == null)
        {
            CLog.Notice("[LogonConsole]", L_N_LOGONCON_9);
            return;
        }
#if WIN32
        // Simulate keydown/keyup event
        _thread.kill = true;

        // Utiliser une tâche pour simuler l'entrée utilisateur
        Task.Run(async () =>
        {
            await Task.Delay(100); // Délai pour simuler l'entrée utilisateur
            Console.WriteLine("\r"); // Simuler l'appui sur la touche Entrée
        });

        CLog.Notice("[LogonConsole]", L_N_LOGONCON_1);

        while (_thread != null)
        {
            Thread.Sleep(100);
        }

        CLog.Notice("[LogonConsole]", L_N_LOGONCON_2);
#else
        CLog.Notice("[LogonConsole]", L_N_LOGONCON_2);
#endif
    }

    public void ProcessCmd(string cmd)
    {
        var sLog = new Logger();
        var cmds = new Dictionary<string, Action<string>>
        {
            { "?", TranslateHelp }, { "help", TranslateHelp },
            { "reload", ReloadAccts },
            { "rehash", TranslateRehash },
            { "shutdown", TranslateQuit }, { "quit", TranslateQuit }, { "exit", TranslateQuit }
        };

        var cmdLower = cmd.ToLower();
        foreach (var command in cmds)
        {
            if (cmdLower.StartsWith(command.Key))
            {
                command.Value(cmd[command.Key.Length..].Trim());
                return;
            }
        }

        sLog.OutError(L_N_LOGONCON_4);
    }

    public static void ReloadAccts(string str)
    {
        AccountMgr.Instance.ReloadAccounts(false);
        IPBanner.Reload();
    }

    public void TranslateQuit(string str)
    {
        int delay = string.IsNullOrEmpty(str) ? 5000 : int.TryParse(str, out var d) && d > 0 ? d * 1000 : 5000;
        ProcessQuit(delay);
    }

    public void ProcessQuit(int delay)
    {
        mrunning = false;
        Task.Run(async () => {
            await Task.Delay(delay);
            CLog.Notice("[LogonConsole]", $"Arrêt du serveur dans {delay / 1000} secondes...");
        });
    }

    public static void TranslateHelp(string str)
    {
        ProcessHelp(null);
    }

    public static void ProcessHelp(string command)
    {
        var sLog = new Logger();
        if (command == null)
        {
            sLog.OutString(L_N_LOGONCON_5);
            sLog.OutString(L_N_LOGONCON_6);
            sLog.OutString(L_N_LOGONCON_7);
            sLog.OutString(L_N_LOGONCON_8);
        }
    }
}
// Dummy structures and methods to mimic Windows API calls
public enum KEY_EVENT : uint
{
    KEY_EVENT = 1
}

public struct CHAR_UNION
{
    public char AsciiChar;
}

public struct KEY_EVENT_RECORD
{
    public bool bKeyDown;
    public uint dwControlKeyState;
    public CHAR_UNION uChar;
    public ushort wRepeatCount;
    public ushort wVirtualKeyCode;
    public ushort wVirtualScanCode;
}

public struct INPUT_RECORD
{
    public KEY_EVENT EventType;
    public KEY_EVENT_RECORD Event;
}


