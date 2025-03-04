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

namespace WaadShared;
public class CLog
{
    private static readonly object lockObject = new object();
    private readonly int logLevel = 3;

    public CLog()
    {
        // Initialisation du niveau de log
    }

    private static void Color(ConsoleColor color)
    {
        Console.ForegroundColor = color;
    }

    public static void Notice(string source, string format, params object[] args)
    {
        lock (lockObject)
        {
            string message = string.Format(format, args);
            if (message.Length > 300)
            {
                Color(ConsoleColor.Red);
                Console.WriteLine("(Message trop long > 300 caractères!)");
                Color(ConsoleColor.Gray);
                return;
            }

            Console.Write($"[{DateTime.Now:HH:mm}] N ");
            if (!string.IsNullOrEmpty(source))
            {
                Color(ConsoleColor.White);
                Console.Write($"{source}: ");
                Color(ConsoleColor.Gray);
            }

            Console.WriteLine(message);
            Color(ConsoleColor.Gray);
        }
    }

    public void Warning(string source, string format, params object[] args)
    {
        if (logLevel < 2) return;

        lock (lockObject)
        {
            Console.Write($"[{DateTime.Now:HH:mm}] ");
            Color(ConsoleColor.Yellow);
            Console.Write("W ");
            if (!string.IsNullOrEmpty(source))
            {
                Color(ConsoleColor.White);
                Console.Write($"{source}: ");
                Color(ConsoleColor.Yellow);
            }

            Console.WriteLine(string.Format(format, args));
            Color(ConsoleColor.Gray);
        }
    }

    public void Success(string source, string format, params object[] args)
    {
        if (logLevel < 2) return;

        lock (lockObject)
        {
            Console.Write($"[{DateTime.Now:HH:mm}] ");
            Color(ConsoleColor.Green);
            Console.Write("S ");
            if (!string.IsNullOrEmpty(source))
            {
                Color(ConsoleColor.White);
                Console.Write($"{source}: ");
                Color(ConsoleColor.Green);
            }

            Console.WriteLine(string.Format(format, args));
            Color(ConsoleColor.Gray);
        }
    }

    public void ZPvP(string source, string format, params object[] args)
    {
        if (logLevel < 1) return;

        lock (lockObject)
        {
            Console.Write($"[{DateTime.Now:HH:mm}] ");
            Color(ConsoleColor.Blue);
            Console.Write("S ");
            if (!string.IsNullOrEmpty(source))
            {
                Color(ConsoleColor.White);
                Console.Write($"{source}: ");
                Color(ConsoleColor.Blue);
            }

            Console.WriteLine(string.Format(format, args));
            Color(ConsoleColor.Gray);
        }
    }

    public void Error(string source, string format, params object[] args)
    {
        if (logLevel < 1) return;

        lock (lockObject)
        {
            Console.Write($"[{DateTime.Now:HH:mm}] ");
            Color(ConsoleColor.Red);
            Console.Write("E ");
            if (!string.IsNullOrEmpty(source))
            {
                Color(ConsoleColor.White);
                Console.Write($"{source}: ");
                Color(ConsoleColor.Red);
            }

            Console.WriteLine(string.Format(format, args));
            Color(ConsoleColor.Gray);
        }
    }

    public void Debug(string source, string format, params object[] args)
    {
        if (logLevel < 3) return;

        lock (lockObject)
        {
            Console.Write($"[{DateTime.Now:HH:mm}] ");
            Color(ConsoleColor.Blue);
            Console.Write("D ");
            if (!string.IsNullOrEmpty(source))
            {
                Color(ConsoleColor.White);
                Console.Write($"{source}: ");
                Color(ConsoleColor.Blue);
            }

            Console.WriteLine(string.Format(format, args));
            Color(ConsoleColor.Gray);
        }
    }

    public static void Line()
    {
        lock (lockObject)
        {
            Console.WriteLine();
        }
    }

    public static void LargeErrorMessage(bool isError, params string[] lines)
    {
        lock (lockObject)
        {
            Color(isError ? ConsoleColor.Red : ConsoleColor.Yellow);

            Console.WriteLine("*********************************************************************");
            Console.WriteLine("*                        MAJOR ERROR/WARNING                        *");
            Console.WriteLine("*                        ===================                        *");

            foreach (var line in lines)
            {
                Console.WriteLine($"* {line.PadRight(65)} *");
            }

            Console.WriteLine("*********************************************************************");

            if (isError)
            {
                MessageBox.Show("MAJOR ERROR/WARNING:\n" + string.Join("\n", lines), "Error");
            }
            else
            {
                Console.WriteLine("Sleeping for 5 seconds.");
                Thread.Sleep(5000);
            }

            Color(ConsoleColor.Gray);
        }
    }

    private static class MessageBox
    {
        public static void Show(string message, string caption)
        {
            Console.WriteLine($"{caption}: {message}");
        }
    }
}

public static class Log
{
    private static readonly CLog instance = new();
    public static CLog Instance => instance;
    public static Logger SLog => Logger.Instance;
    private static readonly SessionLogWriter sCheatLog = new("anticheat.log", true);
    private static readonly SessionLogWriter sGMLog = new("gmcommand.log", true);
    private static readonly SessionLogWriter sPlrLog = new("player.log", true);
    public static SessionLogWriter SCheatLog => sCheatLog;
    public static SessionLogWriter SGMLog => sGMLog;
    public static SessionLogWriter SPlrLog => sPlrLog;
    public static WorldLog SWorldLog => WorldLog.Instance;
}
