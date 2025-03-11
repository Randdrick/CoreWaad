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
using WaadShared.Config;

namespace WaadShared;

public class PacketLog
{
    private static readonly PacketLog instance = new();

    private PacketLog()
    {
        // clear realm logfile
        if (ConfigFile.GetBoolDefault("LogRealm", false))
        {
            File.WriteAllText("realm.log", string.Empty);
        }
        // clear world logfile
        if (ConfigFile.GetBoolDefault("LogWorld", false))
        {
            File.WriteAllText("world.log", string.Empty);
        }
    }

    public static PacketLog Instance
    {
        get { return instance; }
    }

    private static char MakeHexChar(int i)
    {
        return (i <= 9) ? (char)('0' + i) : (char)('A' + (i - 10));
    }

    private static int HexToInt(char c)
    {
        c = char.ToUpper(c);
        return (c > '9') ? c - 'A' + 10 : c - '0';
    }

    public static void HexDump(byte[] data, int length, string file)
    {
        using StreamWriter writer = new(file, true);
        const int charOffset = 16 * 3 + 2;
        const int lineSize = 16 * 3 + 16 + 3;
        char[] line = new char[lineSize + 1];

        writer.WriteLine("OFFSET  00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F | 0123456789ABCDEF");
        writer.WriteLine("--------------------------------------------------------------------------");

        line[charOffset - 1] = ' ';
        line[charOffset - 2] = ' ';

        for (int i = 0; i < length;)
        {
            int bi = 0;
            int ci = 0;

            int startI = i;

            for (int lineI = 0; i < length && lineI < 16; i++, lineI++)
            {
                line[bi++] = MakeHexChar(data[i] >> 4);
                line[bi++] = MakeHexChar(data[i] & 0x0f);
                line[bi++] = ' ';
                line[charOffset + (ci++)] = char.IsControl((char)data[i]) ? '.' : (char)data[i];
            }

            while (bi < 16 * 3)
            {
                line[bi++] = ' ';
            }

            line[charOffset + (ci++)] = '\n';
            line[charOffset + ci] = '\0';

            writer.Write($"{startI:X6}  {new string(line)}");
        }
        writer.WriteLine("\n");
    }

    public static void HexDump(string data, int length, string file)
    {
        HexDump(System.Text.Encoding.UTF8.GetBytes(data), length, file);
    }

    public static void HexDumpStr(string msg, string data, int len, string file)
    {
        File.AppendAllText(file, msg + Environment.NewLine);
        HexDump(data, len, file);
    }

    public static void RealmHexDump(RealmPacket data, uint socket, bool direction)
    {
        if (!ConfigFile.GetBoolDefault("LogRealm", false))
            return;

        using (StreamWriter writer = new("realm.log", true))
        {
            ushort len = (ushort)(RealmPacket.Size + 2);
            byte opcode = RealmPacket.GetOpcode();
            if (direction)
                writer.WriteLine($"SERVER:\nSOCKET: {socket}\nLENGTH: {len}\nOPCODE: {opcode:X2}\nDATA:");
            else
                writer.WriteLine($"CLIENT:\nSOCKET: {socket}\nLENGTH: {len}\nOPCODE: {opcode:X2}\nDATA:");
        }
        HexDump(RealmPacket.Contents, RealmPacket.Size, "realm.log");
    }

    public static void WorldHexDump(WorldPacket data, uint socket, bool direction)
    {
        if (!ConfigFile.GetBoolDefault("LogWorld", false))
            return;

        using (StreamWriter writer = new("world.log", true))
        {
            ushort len = (ushort)data.Size;
            ushort opcode = data.GetOpcode();
            if (direction)
                writer.WriteLine($"SERVER:\nSOCKET: {socket}\nLENGTH: {len}\nOPCODE: {opcode:X4}\nDATA:");
            else
                writer.WriteLine($"CLIENT:\nSOCKET: {socket}\nLENGTH: {len}\nOPCODE: {opcode:X4}\nDATA:");
        }
        HexDump(data.Contents, data.Size, "world.log");
    }
}

// Dummy class to simulate RealmPacket
public class RealmPacket
{
    public static int Size { get; } = 0;
    public static byte GetOpcode() { return 0; }
    public static byte[] Contents { get; } = [];
}

