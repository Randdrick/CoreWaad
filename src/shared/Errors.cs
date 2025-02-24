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
using System.Diagnostics;

namespace WaadShared;

public static class ErrorHandling
{
    // Simulate a logger singleton
    private static readonly Logger Log = Logger.GetInstance();

    // WPAssert equivalent in C#
    [Conditional("DEBUG")]
    public static void WPAssert(bool assertion, string message = "")
    {
        if (!assertion)
        {
            Console.Error.WriteLine($"\n{message}ASSERTION FAILED:\n  {assertion}");
            ShowCallstack();
            Debug.Assert(assertion);
        }
    }

    // WPError equivalent in C#
    public static void WPError(bool assertion, string errmsg)
    {
        if (!assertion)
        {
            Logger.OutError($"{errmsg}ERROR:\n  {assertion}");
            Debug.Assert(false);
        }
    }

    // WPWarning equivalent in C#
    public static void WPWarning(bool assertion, string errmsg)
    {
        if (!assertion)
        {
            Logger.OutError($"{errmsg}WARNING:\n  {assertion}");
        }
    }

    // WPFatal equivalent in C#
    public static void WPFatal(bool assertion, string errmsg)
    {
        if (!assertion)
        {
            Logger.OutError($"{errmsg}FATAL ERROR:\n  {assertion}");
            Debug.Assert(false);
            Environment.FailFast(errmsg);
        }
    }

    // Simulate showing call stack
    private static void ShowCallstack()
    {
        StackTrace stackTrace = new();
        Console.WriteLine(stackTrace.ToString());
    }
}

// Simulate a logger class
public class Logger
{
    private static Logger instance;

    private Logger() { }

    public static Logger GetInstance()
    {
        return instance ??= new Logger();
    }

    public static void OutError(string message)
    {
        Console.Error.WriteLine(message);
    }
}
