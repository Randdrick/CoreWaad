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
using System.IO;
using System.Threading;
using WaadShared.Config;

namespace WaadShared;

public enum LogColor
{
    TRED,
    TGREEN,
    TYELLOW,
    TNORMAL,
    TWHITE,
    TBLUE,
    TPURPLE
}

public class Logger : Singleton<Logger>
{
    private int m_screenLogLevel;
    private int m_fileLogLevel;
    private StreamWriter m_file;

    public Logger() { }

    public void Init(int fileLogLevel, int screenLogLevel)
    {
        m_screenLogLevel = screenLogLevel;
        m_fileLogLevel = fileLogLevel;
        if (m_fileLogLevel >= 0)
        {
            try
            {
                m_file = new StreamWriter("file.log", false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error opening 'file.log': {ex.Message}");
            }
        }
    }

    public void OutTime()
    {
        DateTime now = DateTime.Now;
        string timeStamp = $"[ {now:HH:mm} ]";
        m_file?.Write(timeStamp);
    }

    public void OutString(string str, params object[] args)
    {
        if (m_fileLogLevel < 0 && m_screenLogLevel < 0)
            return;

        string message = string.Format(str, args);

        if (m_screenLogLevel >= 0)
        {
            Console.WriteLine(message);
        }
        if (m_fileLogLevel >= 0 && m_file != null)
        {
            OutTime();
            m_file.WriteLine(message);
        }
    }

    public void OutError(string err, params object[] args)
    {
        if (m_fileLogLevel < 1 && m_screenLogLevel < 1)
            return;

        string message = string.Format(err, args);

        if (m_screenLogLevel >= 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ResetColor();
        }
        if (m_fileLogLevel >= 1 && m_file != null)
        {
            OutTime();
            m_file.WriteLine(message);
        }
    }

    public void OutDetail(string str, params object[] args)
    {
        if (m_fileLogLevel < 2 && m_screenLogLevel < 2)
            return;

        string message = string.Format(str, args);

        if (m_screenLogLevel >= 2)
        {
            Console.WriteLine(message);
        }
        if (m_fileLogLevel >= 2 && m_file != null)
        {
            OutTime();
            m_file.WriteLine(message);
        }
    }

    public void OutDebug(string str, params object[] args)
    {
        if (m_fileLogLevel < 3 && m_screenLogLevel < 3)
            return;

        string message = string.Format(str, args);

        if (m_screenLogLevel >= 3)
        {
            Console.WriteLine(message);
        }
        if (m_fileLogLevel >= 3 && m_file != null)
        {
            OutTime();
            m_file.WriteLine(message);
        }
    }

    public static void OutMenu(string str, params object[] args)
    {
        string message = string.Format(str, args);
        Console.Write(message);
    }

    public static void OutColor(LogColor colorcode, string str, params object[] args)
    {
        if (string.IsNullOrEmpty(str)) return;

        string message = string.Format(str, args);
        ConsoleColor originalColor = Console.ForegroundColor;

        switch (colorcode)
        {
            case LogColor.TRED:
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case LogColor.TGREEN:
                Console.ForegroundColor = ConsoleColor.Green;
                break;
            case LogColor.TYELLOW:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case LogColor.TNORMAL:
            case LogColor.TWHITE:
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case LogColor.TBLUE:
                Console.ForegroundColor = ConsoleColor.Blue;
                break;
            case LogColor.TPURPLE:
                Console.ForegroundColor = ConsoleColor.Magenta;
                break;
        }

        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    public void SetScreenLoggingLevel(int level)
    {
        m_screenLogLevel = level;
    }

    public void SetFileLoggingLevel(int level)
    {
        m_fileLogLevel = level;
        if (m_fileLogLevel >= 0)
        {
            try
            {
                m_file = new StreamWriter("file.log", false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error opening 'file.log': {ex.Message}");
            }
        }
    }

    public void Close()
    {
        m_file?.Close();
    }
}

public class SessionLogWriter : IDisposable
{
    private StreamWriter m_file;
    private readonly string m_filename;

    public SessionLogWriter(string filename, bool open = true)
    {
        m_filename = filename;
        if (open)
        {
            Open();
        }
    }

    public void Open()
    {
        m_file?.Dispose();
        m_file = new StreamWriter(m_filename, true);
    }

    public void Write(string format, params object[] args)
    {
        if (m_file == null) return;

        string message = string.Format(format, args);
        DateTime now = DateTime.Now;
        string timeStamp = $"[{now:yyyy-MM-dd HH:mm:ss}] ";
        m_file.WriteLine($"{timeStamp}{message}");
    }

    public void Close()
    {
        m_file?.Close();
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    public bool IsOpen() => m_file != null;
}

public class WorldLog : Singleton<WorldLog>
{
    private bool bEnabled;
    private bool bEnabledXml;
    private StreamWriter m_file;
    private StreamWriter m_xml;
    private readonly Mutex mutex = new();

    public WorldLog()
    {
        var Config = new ConfigMgr();
        bEnabled = false;
        bEnabledXml = false;
        m_file = null;
        m_xml = null;

        if (Config.MainConfig.GetBoolean("LogLevel", "World"))
        {
            Console.WriteLine("Enabling packetlog output to \"world.log\"");
            Enable();
        }
        else
        {
            Disable();
        }

        if (Config.MainConfig.GetBoolean("LogLevel", "WorldXml"))
        {
            Console.WriteLine("Enabling packetlog output to \"world.xml\"");
            EnableXml();
        }
        else
        {
            DisableXml();
        }
    }

    public void Enable()
    {
        if (bEnabled) return;

        bEnabled = true;
        m_file?.Close();
        m_file = new StreamWriter("world.log", false);
    }

    public void Disable()
    {
        if (!bEnabled) return;

        bEnabled = false;
        m_file?.Close();
        m_file = null;
    }

    public void EnableXml()
    {
        if (bEnabledXml) return;

        bEnabledXml = true;
        m_xml?.Close();
        m_xml = new StreamWriter("world.xml", false);
        m_xml.WriteLine("<?xml version=\"1.0\" ?><log>");
    }

    public void DisableXml()
    {
        if (!bEnabledXml) return;

        bEnabledXml = false;
        m_xml?.WriteLine("</log>");
        m_xml?.Close();
        m_xml = null;
    }

    ~WorldLog()
    {
        m_file?.Close();
        m_xml?.Close();
    }
}

// public abstract class Singleton<T> where T : class, new()
// {
//     private static readonly Lazy<T> instance = new(() => new T());

//     public static T Instance => instance.Value;
//     public static Logger GetInstance()
//     {
//         return Logger.Instance;
//     }
// }


