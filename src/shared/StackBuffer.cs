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
using System.Runtime.InteropServices;

namespace WaadShared;

public class StackBuffer
{
    protected byte[] m_stackBuffer;
    protected int m_readPos;
    protected int m_writePos;
    protected byte[] m_bufferPointer;
    protected byte[] m_heapBuffer;
    protected int m_space;
    private readonly byte[] buffer;
    private uint BufferSize()
    {
        return (uint)buffer.Length;
    }

    public StackBuffer(byte[] ptr, uint sz)
    {
        m_stackBuffer = ptr;
        m_readPos = 0;
        m_writePos = 0;
        m_bufferPointer = m_stackBuffer;
        m_heapBuffer = null;
        m_space = (int)sz;
        buffer = new byte[sz];
        Array.Copy(ptr, buffer, sz);
    }
    
    public class StackPacket(ushort opcode, byte[] ptr, uint sz) : StackBuffer(ptr, sz)
    {
        private ushort m_opcode = opcode;

        public void Initialize(ushort opcode)
        {
            m_opcode = opcode;
        }

        public ushort GetOpcode()
        {
            return m_opcode;
        }

        public void SetOpcode(ushort opcode)
        {
            m_opcode = opcode;
        }
    }

    ~StackBuffer()
    {
        if (m_heapBuffer != null)
        {
            m_heapBuffer = null; // In C#, we rely on the garbage collector to free memory.
        }
    }

    public void ReallocateOnHeap()
    {
        Console.WriteLine("!!!!!!! WARNING! STACK BUFFER OVERFLOW !!!!!!!!!!!!!");
        if (m_heapBuffer != null)
        {
            Array.Resize(ref m_heapBuffer, m_space + 200);
            m_bufferPointer = m_heapBuffer;
            m_space += 200;
        }
        else
        {
            m_heapBuffer = new byte[m_space + 200];
            if (m_heapBuffer != null)
            {
                Array.Copy(m_stackBuffer, m_heapBuffer, m_writePos);
                m_space += 200;
                m_bufferPointer = m_heapBuffer;
            }
        }
    }

    public byte[] GetBufferPointer()
    {
        return m_bufferPointer;
    }

    public int GetWritePos()
    {
        return m_writePos;
    }
    public void PrintStorage()
    {
        var sLog = new Logger();
        if (Logger.IsOutProcess())
        {
            sLog.OutDebug($"STORAGE_SIZE: {BufferSize()}\n");
            sLog.OutDebug("START: ");
            for (uint i = 0; i < BufferSize(); ++i)
            {
                sLog.OutDebug($"{Read<byte>(i)} - ");
            }
            sLog.OutDebug("END\n");
        }
    }

    public T Read<T>(uint i) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        if (m_readPos + size <= m_writePos)
        {
            T ret = ByteArrayToStructure<T>(m_bufferPointer, m_readPos);
            m_readPos += size;
            return ret;
        }
        return default;
    }

    public void Write<T>(T data) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        if (m_writePos + size > m_space)
        {
            ReallocateOnHeap();
        }

        byte[] bytes = StructureToByteArray(data);
        Array.Copy(bytes, 0, m_bufferPointer, m_writePos, size);
        m_writePos += size;
    }

    public void Write(byte[] data, int size)
    {
        if (m_writePos + size > m_space)
        {
            ReallocateOnHeap();
        }

        Array.Copy(data, 0, m_bufferPointer, m_writePos, size);
        m_writePos += size;
    }

    public void EnsureBufferSize(int bytes)
    {
        if (m_writePos + bytes > m_space)
        {
            ReallocateOnHeap();
        }
    }

    public void Clear()
    {
        Array.Clear(buffer, 0, buffer.Length);
        m_writePos = m_readPos = 0;
    }

    public int GetSize()
    {
        return m_writePos;
    }

    private static T ByteArrayToStructure<T>(byte[] bytes, int offset) where T : struct
    {
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            IntPtr buffer = Marshal.UnsafeAddrOfPinnedArrayElement(bytes, offset);
            return (T)Marshal.PtrToStructure(buffer, typeof(T));
        }
        finally
        {
            handle.Free();
        }
    }

    private static byte[] StructureToByteArray<T>(T structure) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        byte[] buffer = new byte[size];
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(structure, Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0), false);
        }
        finally
        {
            handle.Free();
        }
        return buffer;
    }
}

// Example usage of WoWGuid and LocationVector would need to be implemented separately.
