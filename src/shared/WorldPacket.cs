
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

namespace WaadShared;

public class WorldPacket : ByteBuffer
{
    public ushort Opcode { get; set; }
    public int m_bufferPool;

    public WorldPacket(int bufferSize) : base(bufferSize)
    {
        Opcode = 0;
        m_bufferPool = -1;
    }

    public WorldPacket(ushort opcode, int bufferSize) : base(bufferSize)
    {
        Opcode = opcode;
        m_bufferPool = -1;
    }

    public WorldPacket(uint bufferSize) : base(bufferSize)
    {
        Opcode = 0;
        m_bufferPool = -1;
    }

    public WorldPacket(WorldPacket packet, uint socket) : base(packet)
    {
        Opcode = packet.Opcode;
        m_bufferPool = -1;
    }

    public void Initialize(ushort opcode)
    {
        Opcode = opcode;
        Clear();
    }

    public ushort GetOpcode()
    {
        return Opcode;
    }

    public void SetOpcode(ushort opcode)
    {
        Opcode = opcode;
    }

    public static WorldPacket Create()
    {
        return new WorldPacket(0);
    }

    public void PrintStorage()
    {
        var sLog = new Logger();
        if (Logger.IsOutProcess())
        {
            sLog.OutDebug($"STORAGE_SIZE: {Size}\n");
            sLog.OutDebug("Packet data: ");
            foreach (byte b in Contents)
            {
                sLog.OutDebug($"{b:X2} ");
            }
            sLog.OutDebug("\nEND\n");
        }
    }

    public new void Resize(int size)
    {
        Clear();
        for (int i = 0; i < size; i++)
        {
            Append((byte)0);
        }
    }

    public void WriteUInt16(ushort value)
    {
        Append(value);
    }

    public new void WriteUInt32(uint value)
    {
        Append(value);
    }

    public void WriteUInt64(ulong value)
    {
        Append(value);
    }

    public void WriteString(string value)
    {
        value ??= string.Empty;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
        Append(bytes, bytes.Length);
        Append((byte)0);
    }

    public void WriteByte(byte value)
    {
        Append(value);
    }

    public void Write(byte[] value, int offset, int count)
    {
        // Optimisation : éviter la copie inutile
        if (count > 0)
        {
            byte[] tmp = new byte[count];
            Array.Copy(value, offset, tmp, 0, count);
            Append(tmp, count);
        }
    }

    public void WriteInt32(int value)
    {
        Append(value);
    }

    public int ReadInt32()
    {
        int value = 0;
        Read(ref value);
        return value;
    }

    public ushort ReadUInt16()
    {
        ushort value = 0;
        Read(ref value);
        return value;
    }

    public uint ReadUInt32()
    {
        uint value = 0;
        Read(ref value);
        return value;
    }

    public float ReadFloat()
    {
        float value = 0;
        Read(ref value);
        return value;
    }

    public ulong ReadUInt64()
    {
        ulong value = 0;
        Read(ref value);
        return value;
    }

    public string ReadString()
    {
        string result = "";
        byte b;
        while ((b = Read<byte>()) != 0)
        {
            result += (char)b;
        }
        return result;
    }

    public void Read(byte[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = Read<byte>();
        }
    }

    public byte ReadByte()
    {
        return Read<byte>();
    }

    public void WriteFloat(float value)
    {
        Append(value);
    }

    public void WriteBytes(byte[] bytes)
    {
        if (bytes != null && bytes.Length > 0)
        {
            Append(bytes);
        }
    }

    public void ReadBytes(byte[] key, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            key[offset + i] = Read<byte>();
        }
    }
}
