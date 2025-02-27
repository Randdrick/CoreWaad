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
using static System.Runtime.InteropServices.Marshal;

namespace WaadShared;

public class NewByteBuffer
{
    private const int DEFAULT_SIZE = 0x1000;
    private const int DEFAULT_INCREASE_SIZE = 200;

    private byte[] m_buffer;
    private int m_readPos;
    private int m_writePos;
    private int m_buffersize;

    public NewByteBuffer()
    {
        m_buffer = new byte[DEFAULT_SIZE];
        m_readPos = 0;
        m_writePos = 0;
        m_buffersize = DEFAULT_SIZE;
    }

    public NewByteBuffer(int size)
    {
        m_buffer = new byte[size];
        m_readPos = 0;
        m_writePos = 0;
        m_buffersize = size;
    }

    public void Reserve(int size)
    {
        if (m_buffer.Length < size)
        {
            Array.Resize(ref m_buffer, size);
        }
        m_buffersize = size;
    }

    public void Clear()
    {
        m_readPos = 0;
        m_writePos = 0;
    }

    public void Resize(int size)
    {
        m_writePos = size;
    }

    public byte[] Contents()
    {
        return m_buffer;
    }

    public int GetBufferSize()
    {
        return m_writePos;
    }

    public T Read<T>() where T : struct
    {
        int size = SizeOf(typeof(T));
        if (m_readPos + size > m_writePos)
            return default(T);

        T result = ByteArrayToStructure<T>(m_buffer, m_readPos);
        m_readPos += size;
        return result;
    }

    public void Read(byte[] buffer, int length)
    {
        if (m_readPos + length > m_writePos)
            length = m_writePos - m_readPos;

        Array.Copy(m_buffer, m_readPos, buffer, 0, length);
        m_readPos += length;
    }

    public void Write<T>(T data) where T : struct
    {
        int size = SizeOf(typeof(T));
        if (m_writePos + size > m_buffersize)
            Reserve(m_buffersize + DEFAULT_INCREASE_SIZE);

        byte[] bytes = StructureToByteArray(data);
        Array.Copy(bytes, 0, m_buffer, m_writePos, size);
        m_writePos += size;
    }

    public void Write(byte[] data, int size)
    {
        if (m_writePos + size > m_buffersize)
            Reserve(m_buffersize + DEFAULT_INCREASE_SIZE);

        Array.Copy(data, 0, m_buffer, m_writePos, size);
        m_writePos += size;
    }

    public void EnsureBufferSize(int bytes)
    {
        if (m_writePos + bytes > m_buffersize)
            Reserve(m_buffersize + DEFAULT_INCREASE_SIZE);
    }

    public int Size()
    {
        return m_writePos;
    }

    public int ReadPos()
    {
        return m_readPos;
    }

    public int WritePos()
    {
        return m_writePos;
    }

    public void SetReadPos(int pos)
    {
        if (pos <= m_writePos)
            m_readPos = pos;
    }

    public void SetWritePos(int pos)
    {
        if (pos <= m_buffersize)
            m_writePos = pos;
    }

    private static T ByteArrayToStructure<T>(byte[] bytes, int offset) where T : struct
    {
        int size = SizeOf(typeof(T));
        if (size > bytes.Length - offset)
            throw new ArgumentException("Byte array is not large enough.");

        IntPtr ptr = AllocHGlobal(size);
        Copy(bytes, offset, ptr, size);
        T result = PtrToStructure<T>(ptr);
        FreeHGlobal(ptr);
        return result;
    }

    private static byte[] StructureToByteArray<T>(T structure) where T : struct
    {
        int size = SizeOf(typeof(T));
        byte[] bytes = new byte[size];
        IntPtr ptr = AllocHGlobal(size);
        StructureToPtr(structure, ptr, true);
        Copy(ptr, bytes, 0, size);
        FreeHGlobal(ptr);
        return bytes;
    }
}

public static class NewByteBufferExtensions
{
    public static void Write<T>(this NewByteBuffer buffer, List<T> list) where T : struct
    {
        buffer.Write(list.Count);
        foreach (var item in list)
        {
            buffer.Write(item);
        }
    }

    public static void Read<T>(this NewByteBuffer buffer, List<T> list) where T : struct
    {
        int count = buffer.Read<int>();
        list.Clear();
        for (int i = 0; i < count; i++)
        {
            T item = buffer.Read<T>();
            list.Add(item);
        }
    }

    public static void Write<T>(this NewByteBuffer buffer, Dictionary<T, T> dictionary) where T : struct
    {
        buffer.Write(dictionary.Count);
        foreach (var kvp in dictionary)
        {
            buffer.Write(kvp.Key);
            buffer.Write(kvp.Value);
        }
    }

    public static void Read<T>(this NewByteBuffer buffer, Dictionary<T, T> dictionary) where T : struct
    {
        int count = buffer.Read<int>();
        dictionary.Clear();
        for (int i = 0; i < count; i++)
        {
            T key = buffer.Read<T>();
            T value = buffer.Read<T>();
            dictionary.Add(key, value);
        }
    }
}
