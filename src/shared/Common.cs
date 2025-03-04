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
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WaadShared;

public static partial class Common
{
#if WINDOWS
    public const string PLATFORM_TEXT = "Windows";
#elif LINUX
    public const string PLATFORM_TEXT = "Linux";
#elif FREEBSD || DRAGONFLY
    public const string PLATFORM_TEXT = "FreeBSD";
#endif
#if DEBUG
    public const string CONFIG = "Debug";
#elif RELEASE
    public const string CONFIG = "Release";
#endif
    public static readonly string ARCH = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");

    // Define these if you are creating a repack
    // public const string REPACK = "Moocow's Repack";
    // public const string REPACK_AUTHOR = "Trelorn";
    // public const string REPACK_WEBSITE = "www.google.com";

    public enum TimeVariables
    {
        TIME_SECOND = 1,
        TIME_MINUTE = TIME_SECOND * 60,
        TIME_HOUR = TIME_MINUTE * 60,
        TIME_DAY = TIME_HOUR * 24,
        TIME_MONTH = TIME_DAY * 30,
        TIME_YEAR = TIME_MONTH * 12,
    }

    public enum MsTimeVariables
    {
        MSTIME_SECOND = 1000,
        MSTIME_MINUTE = MSTIME_SECOND * 60,
        MSTIME_HOUR = MSTIME_MINUTE * 60,
        MSTIME_DAY = MSTIME_HOUR * 24,
    }

    public static ulong Now()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return (ulong)Environment.TickCount64;
        }
        else
        {
            var time = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (ulong)time.TotalMilliseconds;
        }
    }

    public static void ReverseArray(byte[] array)
    {
        int count = array.Length;
        byte[] temp = new byte[count];
        Array.Copy(array, temp, count);
        for (int i = 0; i < count; ++i)
        {
            array[i] = temp[count - i - 1];
        }
    }

    public static void ToLower(ref string str)
    {
        str = str.ToLower();
    }

    public static void ToUpper(ref string str)
    {
        str = str.ToUpper();
    }

    public static int Int32Abs(int value)
    {
        return (value ^ (value >> 31)) - (value >> 31);
    }

    public static uint Int32Abs2Uint32(int value)
    {
        return (uint)((value ^ (value >> 31)) - (value >> 31));
    }

    public static int Float2Int32(float value)
    {
        return (int)Math.Round(value);
    }

    public static int Long2Int32(double value)
    {
        return (int)Math.Round(value);
    }

    public static List<string> StrSplit(string src, string sep)
    {
        List<string> result = [];
        StringBuilder sb = new();

        foreach (char c in src)
        {
            if (sep.Contains(c))
            {
                if (sb.Length > 0)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
        {
            result.Add(sb.ToString());
        }

        return result;
    }

    public static void SetThreadName(string format, params object[] args)
    {
        string threadName = string.Format(CultureInfo.InvariantCulture, format, args);
        if (Thread.CurrentThread.Name == null)
        {
            Thread.CurrentThread.Name = threadName;
        }
    }

    // #if WINDOWS
    //     [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    //     private static extern void RaiseException(uint dwExceptionCode, uint dwExceptionFlags, uint nNumberOfArguments, ref THREADNAME_INFO lpArguments);

    //     public static void SetThreadName(string format, params object[] args)
    //     {
    //         if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    //         {
    //             string threadName = string.Format(CultureInfo.InvariantCulture, format, args);
    //             THREADNAME_INFO info = new()
    //             {
    //                 dwType = 0x1000,
    //                 dwThreadID = (uint)Environment.CurrentManagedThreadId,
    //                 dwFlags = 0,
    //                 szName = threadName
    //             };

    //             try
    //             {
    //                 RaiseException(0x406D1388, 0, (uint)Marshal.SizeOf(info) / sizeof(uint), ref info);
    //             }
    //             catch (Exception)
    //             {
    //                 Console.WriteLine("Error Occurred");
    //             }
    //         }
    //     }
    // #endif

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct THREADNAME_INFO
    {
        public uint dwType;
        public uint dwThreadID;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string szName;
    }

    public static DateTime ConvTimePeriod(uint dLength, char dType)
    {
        DateTime dateTime = DateTime.Now;

        dateTime = dType switch
        {
            'h' => dateTime.AddHours(dLength),
            'd' => dateTime.AddDays(dLength),
            'w' => dateTime.AddDays(7 * dLength),
            'm' => dateTime.AddMonths((int)dLength),
            'y' => dateTime.AddYears((int)dLength),
            _ => dateTime.AddMinutes((int)dLength),
        };
        return dateTime;
    }

    public static int GetTimePeriodFromString(string str)
    {
        int timeToBan = 0;
        int index = 0;

        while (index < str.Length)
        {
            if (!char.IsDigit(str[index]))
                break;

            StringBuilder numberTemp = new();

            while (index < str.Length && char.IsDigit(str[index]))
            {
                numberTemp.Append(str[index]);
                index++;
            }

            if (index >= str.Length)
                break;

            int multiplier = 0;
            switch (char.ToLower(str[index]))
            {
                case 'y':
                    multiplier = (int)TimeVariables.TIME_YEAR;
                    break;
                case 'm':
                    multiplier = (int)TimeVariables.TIME_MONTH;
                    break;
                case 'd':
                    multiplier = (int)TimeVariables.TIME_DAY;
                    break;
                case 'h':
                    multiplier = (int)TimeVariables.TIME_HOUR;
                    break;
                default:
                    return -1;
            }

            index++;
            int multipliee = int.Parse(numberTemp.ToString());
            timeToBan += multiplier * multipliee;
        }

        return timeToBan;
    }

    private static readonly string[] szDayNames = {
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
    };

    private static readonly string[] szMonthNames = {
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"
    };

    private static void MakeIntString(StringBuilder buf, int num)
    {
        if (num < 10)
        {
            buf.Append('0');
            buf.Append(num);
        }
        else
        {
            buf.Append(num);
        }
    }

    private static void MakeIntStringNoZero(StringBuilder buf, int num)
    {
        buf.Append(num);
    }

    public static string ConvertTimeStampToString(uint timestamp)
    {
        int seconds = (int)timestamp;
        int mins = 0;
        int hours = 0;
        int days = 0;
        int months = 0;
        int years = 0;

        if (seconds >= 60)
        {
            mins = seconds / 60;
            seconds %= 60;
            if (mins >= 60)
            {
                hours = mins / 60;
                mins %= 60;
                if (hours >= 24)
                {
                    days = hours / 24;
                    hours %= 24;
                    if (days >= 30)
                    {
                        months = days / 30;
                        days %= 30;
                        if (months >= 12)
                        {
                            years = months / 12;
                            months %= 12;
                        }
                    }
                }
            }
        }

        StringBuilder result = new();

        if (years > 0)
        {
            result.Append(years).Append(" years, ");
        }
        if (months > 0)
        {
            result.Append(months).Append(" months, ");
        }
        if (days > 0)
        {
            result.Append(days).Append(" days, ");
        }
        if (hours > 0)
        {
            result.Append(hours).Append(" hours, ");
        }
        if (mins > 0)
        {
            result.Append(mins).Append(" minutes, ");
        }
        if (seconds > 0)
        {
            result.Append(seconds).Append(" seconds");
        }

        return result.ToString().TrimEnd(' ', ',');
    }

    public static string ConvertTimeStampToDataTime(uint timestamp)
    {
        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp);
        StringBuilder result = new();

        result.Append(szDayNames[(int)dateTime.DayOfWeek]).Append(", ");
        MakeIntString(result, dateTime.Day);
        result.Append(' ').Append(szMonthNames[dateTime.Month - 1]).Append(' ');
        MakeIntString(result, dateTime.Year);
        result.Append(", ");
        MakeIntString(result, dateTime.Hour);
        result.Append(':');
        MakeIntString(result, dateTime.Minute);
        result.Append(':');
        MakeIntString(result, dateTime.Second);

        return result.ToString();
    }

    public static bool ParseCIDRBan(uint ip, uint mask, int maskBits)
    {
        byte[] sourceIp = BitConverter.GetBytes(ip);
        byte[] maskArray = BitConverter.GetBytes(mask);
        int fullBytes = maskBits / 8;
        int leftoverBits = maskBits % 8;

        if (maskBits > 32)
            return false;

        byte[] leftoverBitsCompare = [
            0x00, 0x80, 0xC0, 0xE0, 0xF0, 0xF8, 0xFC, 0xFE, 0xFF
        ];

        if (fullBytes > 0)
        {
            for (int i = 0; i < fullBytes; i++)
            {
                if (sourceIp[i] != maskArray[i])
                    return false;
            }
        }

        if (leftoverBits > 0 && (sourceIp[fullBytes] & leftoverBitsCompare[leftoverBits]) != (maskArray[fullBytes] & leftoverBitsCompare[leftoverBits]))
        {
            return false;
        }

        return true;
    }

    public static uint MakeIP(string str)
    {
        string[] bytes = str.Split('.');
        if (bytes.Length != 4)
            return 0;

        uint ip = (uint)(Convert.ToUInt32(bytes[0]) | (Convert.ToUInt32(bytes[1]) << 8) | (Convert.ToUInt32(bytes[2]) << 16) | (Convert.ToUInt32(bytes[3]) << 24));
        return ip;
    }

    public static string ConvertMSTimeToString(uint timestamp)
    {
        uint centieme = timestamp % 1000;
        uint seconds = timestamp / 1000 % 60;
        uint minutes = timestamp / 60000 % 60;
        uint hours = timestamp / 3600000 % 24;
        uint days = timestamp / 86400000;

        return $"{days}J {hours:00}H {minutes:00}min {seconds:00}sec:{centieme:000}";
    }

    public static string ConvertUnixTimeToString(uint timestamp)
    {
        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp);
        if (dateTime.Day == 1 && dateTime.Month == 1 && dateTime.Year == 1970)
        {
            return $"{dateTime:HH:mm:ss}";
        }
        else
        {
            return $"{dateTime:HH:mm:ss (dd-MM-yyyy)}";
        }
    }

    public static string GetCurrentFileName()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().Location;
    }

    public static string GetCurrentPath()
    {
        return System.IO.Path.GetDirectoryName(GetCurrentFileName());
    }

    public static string LTrim(string s, string findStr)
    {
        int start = s.IndexOfAny(findStr.ToCharArray());
        return (start == -1) ? s : s[start..];
    }

    public static string RTrim(string s, string findStr)
    {
        int end = s.LastIndexOfAny(findStr.ToCharArray());
        return (end == -1) ? s : s[..(end + 1)];
    }

    public static string Trim(string s, string findStr)
    {
        return RTrim(LTrim(s, findStr), findStr);
    }
    public class Field
    {
        public static bool GetBool() => false;
        public static byte GetUInt8() => 0;
        public static ushort GetUInt16() => 0;
        public static uint GetUInt32() => 0;
        public static int GetInt32() => 0;
        public static float GetFloat() => 0;
        public static string GetString() => "";

        public static uint GetUInt32(Field field) => throw new NotImplementedException();

        public static byte GetUInt8(Field field) => throw new NotImplementedException();

        public static string GetString(Field field) => throw new NotImplementedException();
    }
}

public struct WayPoint
{
    public uint Id;
    public float X;
    public float Y;
    public float Z;
    public float O;
    public uint WaitTime; // ms
    public uint Flags;
    public bool ForwardEmoteOneShot;
    public uint ForwardEmoteId;
    public bool BackwardEmoteOneShot;
    public uint BackwardEmoteId;
    public uint ForwardSkinId;
    public uint BackwardSkinId;
    public uint ForwardStandState;
    public uint BackwardStandState;
    public uint ForwardSpellToCast;
    public uint BackwardSpellToCast;
    public string ForwardSayText;
    public string BackwardSayText;
    public ushort Count;

    public WayPoint()
    {
        Id = 0;
        X = 0.0f;
        Y = 0.0f;
        Z = 0.0f;
        O = 0.0f;
        WaitTime = 0; // ms
        Flags = 0;
        ForwardEmoteOneShot = false;
        ForwardEmoteId = 0;
        BackwardEmoteOneShot = false;
        BackwardEmoteId = 0;
        ForwardSkinId = 0;
        BackwardSkinId = 0;
        ForwardStandState = 0;
        BackwardStandState = 0;
        ForwardSpellToCast = 0;
        BackwardSpellToCast = 0;
        ForwardSayText = string.Empty;
        BackwardSayText = string.Empty;
        Count = 0;
    }
}

public class WayPointMap : List<WayPoint> { }
