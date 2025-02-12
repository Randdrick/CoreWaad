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

namespace WaadShared.Network;

public class CircularBuffer
{
    private byte[] m_buffer;
    private int m_bufferEnd;
    private int m_regionAPointer;
    private int m_regionBPointer;
    private int m_regionASize;
    private int m_regionBSize;

    public CircularBuffer()
    {
        m_buffer = null;
        m_bufferEnd = m_regionAPointer = m_regionBPointer = 0;
        m_regionASize = m_regionBSize = 0;
    }

    ~CircularBuffer()
    {
        m_buffer = null;
    }

    public bool Read(byte[] destination, int bytes)
    {
        int cnt = bytes;
        int aRead = 0, bRead = 0;
        if ((m_regionASize + m_regionBSize) < bytes)
            return false;

        if (m_regionASize > 0)
        {
            aRead = (cnt > m_regionASize) ? m_regionASize : cnt;
            Array.Copy(m_buffer, m_regionAPointer, destination, 0, aRead);
            m_regionASize -= aRead;
            m_regionAPointer += aRead;
            cnt -= aRead;
        }

        if (cnt > 0 && m_regionBSize > 0)
        {
            bRead = (cnt > m_regionBSize) ? m_regionBSize : cnt;
            Array.Copy(m_buffer, m_regionBPointer, destination, aRead, bRead);
            m_regionBSize -= bRead;
            m_regionBPointer += bRead;
            cnt -= bRead;
        }

        if (m_regionASize == 0)
        {
            if (m_regionBSize > 0)
            {
                if (m_regionBPointer != 0)
                    Array.Copy(m_buffer, m_regionBPointer, m_buffer, 0, m_regionBSize);

                m_regionAPointer = 0;
                m_regionASize = m_regionBSize;
                m_regionBPointer = 0;
                m_regionBSize = 0;
            }
            else
            {
                m_regionBPointer = 0;
                m_regionBSize = 0;
                m_regionAPointer = 0;
                m_regionASize = 0;
            }
        }

        return true;
    }

    public bool Write(byte[] data, int bytes)
    {
        if (m_regionBPointer != 0)
        {
            if (GetBFreeSpace() < bytes)
                return false;

            Array.Copy(data, 0, m_buffer, m_regionBPointer + m_regionBSize, bytes);
            m_regionBSize += bytes;
            return true;
        }

        if (GetAFreeSpace() < GetSpaceBeforeA())
        {
            AllocateB();
            if (GetBFreeSpace() < bytes)
                return false;

            Array.Copy(data, 0, m_buffer, m_regionBPointer + m_regionBSize, bytes);
            m_regionBSize += bytes;
            return true;
        }
        else
        {
            if (GetAFreeSpace() < bytes)
                return false;

            Array.Copy(data, 0, m_buffer, m_regionAPointer + m_regionASize, bytes);
            m_regionASize += bytes;
            return true;
        }
    }

    public int GetSpace()
    {
        if (m_regionBPointer != 0)
            return GetBFreeSpace();
        else
        {
            if (GetAFreeSpace() < GetSpaceBeforeA())
            {
                AllocateB();
                return GetBFreeSpace();
            }

            return GetAFreeSpace();
        }
    }

    public int GetSize()
    {
        return m_regionASize + m_regionBSize;
    }

    public int GetContiguousBytes()
    {
        if (m_regionASize > 0)
            return m_regionASize;
        else
            return m_regionBSize;
    }

    public void Remove(int len)
    {
        int cnt = len;
        int aRem, bRem;

        if (m_regionASize > 0)
        {
            aRem = (cnt > m_regionASize) ? m_regionASize : cnt;
            m_regionASize -= aRem;
            m_regionAPointer += aRem;
            cnt -= aRem;
        }

        if (cnt > 0 && m_regionBSize > 0)
        {
            bRem = (cnt > m_regionBSize) ? m_regionBSize : cnt;
            m_regionBSize -= bRem;
            m_regionBPointer += bRem;
            cnt -= bRem;
        }

        if (m_regionASize == 0)
        {
            if (m_regionBSize > 0)
            {
                if (m_regionBPointer != 0)
                    Array.Copy(m_buffer, m_regionBPointer, m_buffer, 0, m_regionBSize);

                m_regionAPointer = 0;
                m_regionASize = m_regionBSize;
                m_regionBPointer = 0;
                m_regionBSize = 0;
            }
            else
            {
                m_regionBPointer = 0;
                m_regionBSize = 0;
                m_regionAPointer = 0;
                m_regionASize = 0;
            }
        }
    }

    public IntPtr GetBuffer()
    {
        if (m_regionBPointer != 0)
            return (IntPtr)(m_regionBPointer + m_regionBSize);
        else
            return (IntPtr)(m_regionAPointer + m_regionASize);
    }

    public void Allocate(int size)
    {
        m_buffer = new byte[size];
        m_bufferEnd = size;
        m_regionAPointer = 0;
    }

    public void IncrementWritten(int len)
    {
        if (m_regionBPointer != 0)
            m_regionBSize += len;
        else
            m_regionASize += len;
    }

    public IntPtr GetBufferStart()
    {
        if (m_regionASize > 0)
            return (IntPtr)m_regionAPointer;
        else
            return (IntPtr)m_regionBPointer;
    }

    private int GetBFreeSpace()
    {
        return (m_regionBPointer == 0) ? 0 : (m_regionAPointer - m_regionBPointer - m_regionBSize);
    }

    private int GetAFreeSpace()
    {
        return (m_bufferEnd - m_regionAPointer - m_regionASize);
    }

    private int GetSpaceBeforeA()
    {
        return (m_regionAPointer - 0);
    }

    private void AllocateB()
    {
        // Implémentez cette méthode selon vos besoins
    }
}
