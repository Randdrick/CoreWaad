
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
using System.Text;
using static System.Buffer;

namespace WaadShared;
public class WorldPacket : ByteBuffer
{

    public ushort Opcode;
    public new int Size;
    public new byte[] Contents;

    private ushort m_opcode;
    public int m_bufferPool;
    private readonly byte[] data;

    public WorldPacket(int bufferSize) : base(bufferSize)
    {
        data = new byte[bufferSize];
    }

    public WorldPacket(ushort opcode, int bufferSize) : this(bufferSize)
    {
        m_opcode = opcode;
        m_bufferPool = -1;
        Contents = new byte[bufferSize];
    }

    public WorldPacket(uint bufferSize) : base(bufferSize)
    {
        m_opcode = 0;
        m_bufferPool = -1;
    }

    public WorldPacket(WorldPacket packet, uint socket) : base(packet)
    {
        m_opcode = packet.m_opcode;
        m_bufferPool = -1;
    }

    private int BufferSize()
    {
        return data.Length;
    }

    public void Initialize(ushort opcode)
    {
        Opcode = opcode;
        Size = 0;
        Contents = [];
    }

    public ushort GetOpcode()
    {
        return m_opcode;
    }

    public void SetOpcode(ushort opcode)
    {
        m_opcode = opcode;
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
            sLog.OutDebug($"STORAGE_SIZE: {BufferSize()}\n");
            sLog.OutDebug("START: ");
            for (int i = 0; i < BufferSize(); ++i)
            {
                sLog.OutDebug($"{Read<byte>(i)} - ");
            }
            sLog.OutDebug("END\n");
        }
    }

    public new void Resize(int size)
    {
        Array.Resize(ref Contents, size);
        Size = size;
    }

    public void WriteUInt16(ushort value)
    {
        BlockCopy(BitConverter.GetBytes(value), 0, Contents, Size, 2);
        Size += 2;
    }

    public new void WriteUInt32(uint value)
    {
        BlockCopy(BitConverter.GetBytes(value), 0, Contents, Size, 4);
        Size += 4;
    }

    public void WriteUInt64(ulong value)
    {
        BlockCopy(BitConverter.GetBytes(value), 0, Contents, Size, 8);
        Size += 8;
    }

    public void WriteString(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        BlockCopy(bytes, 0, Contents, Size, bytes.Length);
        Size += bytes.Length;
    }

    public void WriteByte(byte value)
    {
        Contents[Size] = value;
        Size++;
    }

    public void Write(byte[] value, int offset, int count)
    {
        BlockCopy(value, offset, Contents, Size, count);
        Size += count;
    }

    public void WriteInt32(int value)
    {
        BlockCopy(BitConverter.GetBytes(value), 0, Contents, Size, 4);
        Size += 4;
    }
    
    public int ReadInt32()
    {
        if (Size + 4 > Contents.Length)
            throw new InvalidOperationException("Not enough data to read Int32");
        int value = BitConverter.ToInt32(Contents, Size);
        Size += 4;
        return value;
    }

    public ushort ReadUInt16()
    {
        if (Size + 2 > Contents.Length)
            throw new InvalidOperationException("Not enough data to read UInt16");
        ushort value = BitConverter.ToUInt16(Contents, Size);
        Size += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        if (Size + 4 > Contents.Length)
        {
            throw new InvalidOperationException("Not enough data to read UInt32");
        }
        uint value = BitConverter.ToUInt32(Contents, Size);
        Size += 4;
        return value;
    }

    public ulong ReadUInt64()
    {
        if (Size + 8 > Contents.Length)
        {
            throw new InvalidOperationException("Not enough data to read UInt64");
        }
        ulong value = BitConverter.ToUInt64(Contents, Size);
        Size += 8;
        return value;
    }

    public string ReadString()
    {
        int length = Array.IndexOf<byte>(Contents, 0, Size);
        string value = Encoding.UTF8.GetString(Contents, Size, length);
        Size += length + 1;
        return value;
    }

    public void Read(byte[] buffer, int offset, int count)
    {
        BlockCopy(Contents, Size, buffer, offset, count);
        Size += count;
    }

    internal void WriteByte(string gMFlags)
    {
        if (string.IsNullOrEmpty(gMFlags))
        {
            throw new ArgumentException("gMFlags cannot be null or empty", nameof(gMFlags));
        }
        WriteByte((byte)gMFlags[0]);
    }

    internal void Write(char[] locale, int v1, int v2)
    {
        ArgumentNullException.ThrowIfNull(locale);
        if (v1 < 0 || v2 < 0 || v1 + v2 > locale.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(v1), "v1 and v2 must be within the bounds of the locale array");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(locale, v1, v2);
        BlockCopy(bytes, 0, Contents, Size, bytes.Length);
        Size += bytes.Length;
    }

    public byte ReadByte()
    {
        if (Size >= Contents.Length)
        {
            throw new InvalidOperationException("No more bytes to read");
        }
        byte value = Contents[Size];
        Size++;
        return value;
    }

    public void WriteFloat(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes); // Optionnel : adapte l'endianess si nécessaire
        Write(bytes, 0, bytes.Length);
    }

    public void WriteBytes(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes), "Bytes array cannot be null");
        }
        if (bytes.Length == 0)
        {
            return; // Rien à écrire si le tableau est vide
        }
        if (Size + bytes.Length > Contents.Length)
        {
            Array.Resize(ref Contents, Math.Max(Contents.Length * 2, Size + bytes.Length));
        }
        BlockCopy(bytes, 0, Contents, Size, bytes.Length);
        Size += bytes.Length;
    }

    public void ReadBytes(byte[] key, int v1, int v2)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key), "Key array cannot be null");
        }
        if (v1 < 0 || v2 < 0 || v1 + v2 > key.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(v1), "v1 and v2 must be within the bounds of the key array");
        }
        if (Size + v2 > Contents.Length)
        {
            throw new InvalidOperationException("Not enough data to read the requested number of bytes");
        }
        BlockCopy(Contents, Size, key, v1, v2);
        Size += v2;
    }


}