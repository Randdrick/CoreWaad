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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WaadShared;

public class ByteBufferException(bool add, uint pos, uint esize, uint size) : Exception($"Attempted to {(add ? "put" : "get")} in ByteBuffer (pos: {pos} size: {size}) value with size: {esize}")
{
}

public class ByteBuffer
{
    protected List<byte> buffer;
    private readonly List<byte> _storage;
    private int _rpos;
    private int _wpos;
    public string Opcodename { get; set; }
    public int Size => _storage.Count;

    public const int DEFAULT_SIZE = 0x1000;

    public ByteBuffer()
    {
        _storage = new List<byte>(DEFAULT_SIZE);
    }

    public ByteBuffer(int res)
    {
        _storage = new List<byte>(res);
    }

    public ByteBuffer(ByteBuffer buf)
    {
        _rpos = buf._rpos;
        _wpos = buf._wpos;
        _storage = [.. buf._storage];
    }

    public ByteBuffer(uint res)
    {
        buffer = new List<byte>((int)res);
    }
    public ByteBuffer(WorldPacket packet)
    {
        buffer = [.. packet.buffer];
    }

    public void Clear()
    {
        _storage.Clear();
        _rpos = _wpos = 0;
        Opcodename = "";
    }

    public void Append<T>(T value)
    {
        byte[] bytes = GetBytes(value);
        Append(bytes, bytes.Length);
    }

    public void Put<T>(int pos, T value)
    {
        byte[] bytes = GetBytes(value);
        Put(pos, bytes, bytes.Length);
    }

    public ByteBuffer Write(bool value)
    {
        Append((byte)(value ? 1 : 0));
        return this;
    }

    public ByteBuffer Write(byte value)
    {
        Append(value);
        return this;
    }

    public ByteBuffer Write(ushort value)
    {
        Append(BitConverter.GetBytes(value));
        return this;
    }

    public ByteBuffer Write(uint value)
    {
        Append(BitConverter.GetBytes(value));
        return this;
    }

    public ByteBuffer Write(ulong value)
    {
        Append(BitConverter.GetBytes(value));
        return this;
    }

    public ByteBuffer Write(sbyte value)
    {
        Append((byte)value);
        return this;
    }

    public ByteBuffer Write(short value)
    {
        Append(BitConverter.GetBytes(value));
        return this;
    }

    public ByteBuffer Write(int value)
    {
        Append(BitConverter.GetBytes(value));
        return this;
    }

    public ByteBuffer Write(long value)
    {
        Append(BitConverter.GetBytes(value));
        return this;
    }

    public ByteBuffer Write(float value)
    {
        Append(BitConverter.GetBytes(value));
        return this;
    }

    public ByteBuffer Write(double value)
    {
        Append(BitConverter.GetBytes(value));
        return this;
    }

    public ByteBuffer Write(string value)
    {
        Append(Encoding.UTF8.GetBytes(value));
        Append((byte)0);
        return this;
    }

    public ByteBuffer Read(ref bool value)
    {
        value = Read<byte>() > 0;
        return this;
    }

    public ByteBuffer Read(ref byte value)
    {
        value = Read<byte>();
        return this;
    }

    public ByteBuffer Read(ref ushort value)
    {
        value = BitConverter.ToUInt16(ReadBytes(_rpos, sizeof(ushort)), 0);
        _rpos += sizeof(ushort);
        return this;
    }

    public ByteBuffer Read(ref uint value)
    {
        value = BitConverter.ToUInt32(ReadBytes(_rpos, sizeof(uint)), 0);
        _rpos += sizeof(uint);
        return this;
    }

    public ByteBuffer Read(ref ulong value)
    {
        value = BitConverter.ToUInt64(ReadBytes(_rpos, sizeof(ulong)), 0);
        _rpos += sizeof(ulong);
        return this;
    }

    public ByteBuffer Read(ref sbyte value)
    {
        value = (sbyte)Read<byte>();
        return this;
    }

    public ByteBuffer Read(ref short value)
    {
        value = BitConverter.ToInt16(ReadBytes(_rpos, sizeof(short)), 0);
        _rpos += sizeof(short);
        return this;
    }

    public ByteBuffer Read(ref int value)
    {
        value = BitConverter.ToInt32(ReadBytes(_rpos, sizeof(int)), 0);
        _rpos += sizeof(int);
        return this;
    }

    public ByteBuffer Read(ref long value)
    {
        value = BitConverter.ToInt64(ReadBytes(_rpos, sizeof(long)), 0);
        _rpos += sizeof(long);
        return this;
    }

    public ByteBuffer Read(ref float value)
    {
        value = BitConverter.ToSingle(ReadBytes(_rpos, sizeof(float)), 0);
        _rpos += sizeof(float);
        return this;
    }

    public ByteBuffer Read(ref double value)
    {
        value = BitConverter.ToDouble(ReadBytes(_rpos, sizeof(double)), 0);
        _rpos += sizeof(double);
        return this;
    }

    public ByteBuffer Read(ref string value)
    {
        value = "";
        while (true)
        {
            char c = (char)Read<byte>();
            if (c == 0)
                break;
            value += c;
        }
        return this;
    }

    public byte this[int pos]
    {
        get
        {
            if (pos < 0 || pos >= Size)
                throw new ByteBufferException(false, (uint)pos, sizeof(byte), (uint)Size);
            return Read<byte>(pos);
        }
    }

    public int Rpos
    {
        get { return _rpos; }
        set
        {
            if (value < 0 || value > Size)
                throw new ArgumentOutOfRangeException(nameof(value), "Read position out of range.");
            _rpos = value;
        }
    }

    public int Wpos
    {
        get { return _wpos; }
        set
        {
            if (value < 0 || value > Size)
                throw new ArgumentOutOfRangeException(nameof(value), "Write position out of range.");
            _wpos = value;
        }
    }

    public T Read<T>(int pos)
    {
        byte[] bytes = ReadBytes(pos, Marshal.SizeOf(typeof(T)));
        if (bytes.Length == Marshal.SizeOf(typeof(T)))
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }
        return default;
    }

    public T Read<T>()
    {
        T value = Read<T>(_rpos);
        _rpos += Marshal.SizeOf(typeof(T));
        return value;
    }

    public void Read(byte[] dest, int len)
    {
        if (_rpos + len <= Size)
        {
            Array.Copy(_storage.ToArray(), _rpos, dest, 0, len);
        }
        else
        {
            throw new ByteBufferException(false, (uint)_rpos, (uint)len, (uint)Size);
        }
        _rpos += len;
    }

    public uint ReadPackedTime()
    {
        uint packedDate = Read<uint>();
        DateTime date = new((int)(1900 + ((packedDate >> 24) & 0x1F)), (int)((packedDate >> 20) & 0xF), (int)(((packedDate >> 14) & 0x3F) + 1),
                                  (int)((packedDate >> 6) & 0x1F), (int)(packedDate & 0x3F), 0);
        return (uint)(date - new DateTime(1970, 1, 1)).TotalSeconds;
    }

    public ByteBuffer ReadPackedTime(ref uint time)
    {
        time = ReadPackedTime();
        return this;
    }

    public byte[] Contents => [.. _storage];

    public void Resize(int newsize)
    {
        if (newsize < 0)
            throw new ArgumentOutOfRangeException(nameof(newsize), "New size cannot be negative.");
        _storage.Capacity = newsize;
        _rpos = 0;
        _wpos = Size;
    }

    public void Reserve(int ressize)
    {
        if (ressize > Size)
            _storage.Capacity = ressize;
    }

    public void Append(string str)
    {
        Append(Encoding.UTF8.GetBytes(str));
    }

    public void Append(byte[] src, int cnt)
    {
        if (cnt == 0) return;

        if (_storage.Count < _wpos + cnt)
            _storage.Capacity = _wpos + cnt;

        for (int i = 0; i < cnt; i++)
        {
            _storage.Add(src[i]);
        }
        _wpos += cnt;
    }

    public void Append(ByteBuffer buffer)
    {
        if (buffer.Size > 0)
            Append(buffer.Contents, buffer.Size);
    }

    public void Put(int pos, byte[] src, int cnt)
    {
        if (pos + cnt <= Size)
        {
            for (int i = 0; i < cnt; i++)
            {
                _storage[pos + i] = src[i];
            }
        }
        else
        {
            throw new ByteBufferException(true, (uint)pos, (uint)cnt, (uint)Size);
        }
    }

    public void Hexlike()
    {
        int j = 1, k = 1;
        Console.WriteLine($"STORAGE_SIZE: {Size}");
        for (int i = 0; i < Size; i++)
        {
            if ((i == (j * 8)) && ((i != (k * 16))))
            {
                Console.Write($"| {_storage[i]:X2} ");
                j++;
            }
            else if (i == (k * 16))
            {
                Rpos -= 16;
                Console.Write(" | ");
                for (int x = 0; x < 16; x++)
                {
                    Console.Write((char)_storage[i - 16 + x]);
                }
                Console.Write($"\n{_storage[i]:X2} ");
                k++;
                j++;
            }
            else
            {
                Console.Write($"{_storage[i]:X2} ");
            }
        }
        Console.WriteLine();
    }

    public void Hexlike(StreamWriter output)
    {
        int j = 1, k = 1;
        output.WriteLine($"STORAGE_SIZE: {Size}");
        for (int i = 0; i < Size; i++)
        {
            if ((i == (j * 8)) && ((i != (k * 16))))
            {
                output.Write($"| {_storage[i]:X2} ");
                j++;
            }
            else if (i == (k * 16))
            {
                Rpos -= 16;
                output.Write(" | ");
                for (int x = 0; x < 16; x++)
                {
                    output.Write((char)_storage[i - 16 + x]);
                }
                output.Write($"\n{_storage[i]:X2} ");
                k++;
                j++;
            }
            else
            {
                output.Write($"{_storage[i]:X2} ");
            }
        }
        output.WriteLine();
    }

    public void Reverse()
    {
        _storage.Reverse();
    }

    private byte[] ReadBytes(int pos, int length)
    {
        if (pos + length <= Size)
        {
            return [.. _storage.GetRange(pos, length)];
        }
        throw new ByteBufferException(false, (uint)pos, (uint)length, (uint)Size);
    }

    private static byte[] GetBytes<T>(T value)
    {
        int size = Marshal.SizeOf(value);
        byte[] bytes = new byte[size];
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, ptr, true);
            Marshal.Copy(ptr, bytes, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
        return bytes;
    }
    public void SetUInt16(int index, ushort value)
    {
        var bytes = BitConverter.GetBytes(value);
        _storage[index] = bytes[0];
        _storage[index + 1] = bytes[1];
    }

    public void SetUInt32(int index, uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        _storage[index] = bytes[0];
        _storage[index + 1] = bytes[1];
        _storage[index + 2] = bytes[2];
        _storage[index + 3] = bytes[3];
    }

    public byte[] ToArray()
    {
        return [.. _storage];
    }
}

public static class ByteBufferExtensions
{
    public static ByteBuffer Write<T>(this ByteBuffer b, List<T> v)
    {
        b.Write(v.Count);
        foreach (var item in v)
        {
            if (item is bool boolItem)
                b.Write(boolItem);
            else if (item is byte byteItem)
                b.Write(byteItem);
            else if (item is ushort ushortItem)
                b.Write(ushortItem);
            else if (item is uint uintItem)
                b.Write(uintItem);
            else if (item is ulong ulongItem)
                b.Write(ulongItem);
            else if (item is sbyte sbyteItem)
                b.Write(sbyteItem);
            else if (item is short shortItem)
                b.Write(shortItem);
            else if (item is int intItem)
                b.Write(intItem);
            else if (item is long longItem)
                b.Write(longItem);
            else if (item is float floatItem)
                b.Write(floatItem);
            else if (item is double doubleItem)
                b.Write(doubleItem);
            else if (typeof(T) == typeof(string))
                b.Write(item as string);
            else
                throw new ByteBufferException(true, (uint)b.Wpos, (uint)Marshal.SizeOf(item), (uint)b.Size);
        }
        return b;
    }

    public static ByteBuffer Read<T>(this ByteBuffer b, ref List<T> v)
    {
        int count = b.Read<int>();
        v = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            T item = b.Read<T>();
            v.Add(item);
        }
        return b;
    }

    public static ByteBuffer Write<T>(this ByteBuffer b, Dictionary<T, T> m)
    {
        b.Write(m.Count);
        foreach (var pair in m)
        {
            if (pair.Key is bool boolKey)
                b.Write(boolKey);
            else if (pair.Key is byte byteKey)
                b.Write(byteKey);
            else if (pair.Key is ushort ushortKey)
                b.Write(ushortKey);
            else if (pair.Key is uint uintKey)
                b.Write(uintKey);
            else if (pair.Key is ulong ulongKey)
                b.Write(ulongKey);
            else if (pair.Key is sbyte sbyteKey)
                b.Write(sbyteKey);
            else if (pair.Key is short shortKey)
                b.Write(shortKey);
            else if (pair.Key is int intKey)
                b.Write(intKey);
            else if (pair.Key is long longKey)
                b.Write(longKey);
            else if (pair.Key is float floatKey)
                b.Write(floatKey);
            else if (pair.Key is double doubleKey)
                b.Write(doubleKey);
            else if (typeof(T) == typeof(string))
                b.Write(pair.Key as string);
            else
                throw new ByteBufferException(true, (uint)b.Wpos, (uint)Marshal.SizeOf(pair.Key), (uint)b.Size);

            if (pair.Value is bool boolValue)
                b.Write(boolValue);
            else if (pair.Value is byte byteValue)
                b.Write(byteValue);
            else if (pair.Value is ushort ushortValue)
                b.Write(ushortValue);
            else if (pair.Value is uint uintValue)
                b.Write(uintValue);
            else if (pair.Value is ulong ulongValue)
                b.Write(ulongValue);
            else if (pair.Value is sbyte sbyteValue)
                b.Write(sbyteValue);
            else if (pair.Value is short shortValue)
                b.Write(shortValue);
            else if (pair.Value is int intValue)
                b.Write(intValue);
            else if (pair.Value is long longValue)
                b.Write(longValue);
            else if (pair.Value is float floatValue)
                b.Write(floatValue);
            else if (pair.Value is double doubleValue)
                b.Write(doubleValue);
            else if (typeof(T) == typeof(string))
                b.Write(pair.Value as string);
            else
                throw new ByteBufferException(true, (uint)b.Wpos, (uint)Marshal.SizeOf(pair.Value), (uint)b.Size);
        }
        return b;
    }

    public static ByteBuffer Read<T>(this ByteBuffer b, ref Dictionary<T, T> m)
    {
        int count = b.Read<int>();
        m = new Dictionary<T, T>(count);
        for (int i = 0; i < count; i++)
        {
            T key = b.Read<T>();
            T value = b.Read<T>();
            m.Add(key, value);
        }
        return b;
    }
}
