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
using System.IO;
using System.Runtime.InteropServices;

namespace WaadShared;

public static class CrashHandler
{
    private static readonly object crashLock = new();
    private static bool onCrashBreakDebugger;
    private static bool died = false;

    public static void StartCrashHandler()
    {
        onCrashBreakDebugger = Debugger.IsAttached;
    }

    public static void OutputCrashLogLine(string format, params object[] args)
    {
        string logFilePath = Path.Combine("logs", "CrashLog.txt");
        Directory.CreateDirectory("logs");
        File.AppendAllText(logFilePath, string.Format(format, args) + Environment.NewLine);
    }

    public static string GetExceptionDescription(uint exceptionCode)
    {
        return exceptionCode switch
        {
            0x40010005 => "a Control-C",
            0x40010008 => "a Control-Break",
            0x80000002 => "a Datatype Misalignment",
            0x80000003 => "a Breakpoint",
            0xC0000005 => "an Access Violation",
            0xC0000006 => "an In Page Error",
            0xC0000017 => "a No Memory",
            0xC000001D => "an Illegal Instruction",
            0xC0000025 => "a Noncontinuable Exception",
            0xC0000026 => "an Invalid Disposition",
            0xC000008C => "a Array Bounds Exceeded",
            0xC000008D => "a Float Denormal Operand",
            0xC000008E => "a Float Divide by Zero",
            0xC000008F => "a Float Inexact Result",
            0xC0000090 => "a Float Invalid Operation",
            0xC0000091 => "a Float Overflow",
            0xC0000092 => "a Float Stack Check",
            0xC0000093 => "a Float Underflow",
            0xC0000094 => "an Integer Divide by Zero",
            0xC0000095 => "an Integer Overflow",
            0xC0000096 => "a Privileged Instruction",
            0xC00000FD => "a Stack Overflow",
            0xC0000142 => "a DLL Initialization Failed",
            0xE06D7363 => "a Microsoft C++ Exception",
            _ => "an Unknown exception type",
        };
    }

    public static void PrintCrashInformation(Exception exception)
    {
        OutputCrashLogLine("-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-");
        OutputCrashLogLine("Server has crashed. Reason was:");
        OutputCrashLogLine("   {0} at {1}", GetExceptionDescription((uint)Marshal.GetHRForException(exception)), exception.StackTrace);
        OutputCrashLogLine("-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-");
    }

    public static void HandleCrash(Exception exception)
    {
        lock (crashLock)
        {
            if (died)
            {
                Process.GetCurrentProcess().Kill();
            }

            died = true;

            string dumpFilePath = Path.Combine("CrashDumps", $"dump-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}-{Environment.CurrentManagedThreadId}.dmp");
            Directory.CreateDirectory("CrashDumps");

            // Create a dump file using dotnet-dump
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"dump collect --process-id {Environment.ProcessId} -o {dumpFilePath}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
            }

            PrintCrashInformation(exception);
            Console.WriteLine($"Creating crash dump file {dumpFilePath}");

            if (!onCrashBreakDebugger)
            {
                OnCrash();
            }
        }
    }

    private static void OnCrash()
    {
        // Terminate the process
        Environment.Exit(1);
    }
}
// Example usage:
// public class Program
// {
//     public static void Main(string[] args)
//     {
//         CrashHandler.StartCrashHandler();

//         try
//         {
//             // Simulate a crash
//             throw new AccessViolationException();
//         }
//         catch (Exception ex)
//         {
//             CrashHandler.HandleCrash(ex);
//         }
//     }
// }
