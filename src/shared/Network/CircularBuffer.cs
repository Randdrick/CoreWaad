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
    public readonly int Length;

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

    // Alloue le buffer avec une taille donnée
    public void Allocate(int size)
    {
        m_buffer = new byte[size];
        m_bufferEnd = size;
        m_regionAPointer = 0;
        m_regionBPointer = 0;
        m_regionASize = 0;
        m_regionBSize = 0;
    }

    // Lit des octets depuis le buffer circulaire
    public bool Read(byte[] destination, int bytes)
    {
        if ((m_regionASize + m_regionBSize) < bytes)
            return false;

        int remaining = bytes;
        int offset = 0;

        // Lit depuis la région A
        if (m_regionASize > 0)
        {
            int toRead = Math.Min(remaining, m_regionASize);
            Array.Copy(m_buffer, m_regionAPointer, destination, offset, toRead);
            m_regionAPointer += toRead;
            m_regionASize -= toRead;
            offset += toRead;
            remaining -= toRead;
        }

        // Lit depuis la région B si nécessaire
        if (remaining > 0 && m_regionBSize > 0)
        {
            int toRead = Math.Min(remaining, m_regionBSize);
            Array.Copy(m_buffer, m_regionBPointer, destination, offset, toRead);
            m_regionBPointer += toRead;
            m_regionBSize -= toRead;
            remaining -= toRead;
        }

        // Réorganise les régions si la région A est vide
        if (m_regionASize == 0 && m_regionBSize > 0)
        {
            Array.Copy(m_buffer, m_regionBPointer, m_buffer, 0, m_regionBSize);
            m_regionAPointer = 0;
            m_regionASize = m_regionBSize;
            m_regionBPointer = 0;
            m_regionBSize = 0;
        }

        return true;
    }

    // Écrit des octets dans le buffer circulaire
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

    // Lit un UInt16 depuis le buffer
    public ushort ReadUInt16()
    {
        byte[] temp = new byte[2];
        if (!Read(temp, 2))
            throw new InvalidOperationException("Not enough data to read UInt16");
        return BitConverter.ToUInt16(temp, 0);
    }

    // Lit un UInt32 depuis le buffer
    public uint ReadUInt32()
    {
        byte[] temp = new byte[4];
        if (!Read(temp, 4))
            throw new InvalidOperationException("Not enough data to read UInt32");
        return BitConverter.ToUInt32(temp, 0);
    }

    // Lit un Int32 depuis le buffer
    public int ReadInt32()
    {
        byte[] temp = new byte[4];
        if (!Read(temp, 4))
            throw new InvalidOperationException("Not enough data to read Int32");
        return BitConverter.ToInt32(temp, 0);
    }

    // Lit un tableau d'octets depuis le buffer
    public byte[] ReadBytes(uint size)
    {
        byte[] value = new byte[size];
        if (!Read(value, (int)size))
            throw new InvalidOperationException("Not enough data to read bytes");
        return value;
    }

    // Supprime des octets du buffer
    public void Remove(int len)
    {
        int remaining = len;

        if (m_regionASize > 0)
        {
            int toRemove = Math.Min(remaining, m_regionASize);
            m_regionAPointer += toRemove;
            m_regionASize -= toRemove;
            remaining -= toRemove;
        }

        if (remaining > 0 && m_regionBSize > 0)
        {
            int toRemove = Math.Min(remaining, m_regionBSize);
            m_regionBPointer += toRemove;
            m_regionBSize -= toRemove;
            remaining -= toRemove;
        }

        if (m_regionASize == 0 && m_regionBSize > 0)
        {
            Array.Copy(m_buffer, m_regionBPointer, m_buffer, 0, m_regionBSize);
            m_regionAPointer = 0;
            m_regionASize = m_regionBSize;
            m_regionBPointer = 0;
            m_regionBSize = 0;
        }
    }

    // Retourne l'espace libre dans la région A
    private int GetAFreeSpace()
    {
        return m_bufferEnd - m_regionAPointer - m_regionASize;
    }

    // Retourne l'espace libre dans la région B
    private int GetBFreeSpace()
    {
        return (m_regionBPointer == 0) ? 0 : (m_regionAPointer - m_regionBPointer - m_regionBSize);
    }

    // Retourne l'espace avant la région A
    private int GetSpaceBeforeA()
    {
        return m_regionAPointer;
    }

    // Alloue la région B si nécessaire
    private void AllocateB()
    {
        if (m_regionBPointer == 0 && m_regionASize > 0)
        {
            m_regionBPointer = m_regionAPointer + m_regionASize;
        }
    }

    // Retourne la taille totale des données dans le buffer
    public int GetSize()
    {
        return m_regionASize + m_regionBSize;
    }

    // Retourne l'espace libre total dans le buffer
    public int GetSpace()
    {
        if (m_regionBPointer != 0)
            return GetBFreeSpace();
        else
            return GetAFreeSpace();
    }

    // Retourne le nombre d'octets contigus disponibles
    public int GetContiguousBytes()
    {
        return m_regionASize > 0 ? m_regionASize : m_regionBSize;
    }

    // Retourne un pointeur vers la fin du buffer
    public IntPtr GetBuffer()
    {
        return m_regionBPointer != 0
            ? (IntPtr)(m_regionBPointer + m_regionBSize)
            : (IntPtr)(m_regionAPointer + m_regionASize);
    }

    // Retourne un pointeur vers le début des données
    public IntPtr GetBufferStart()
    {
        return m_regionASize > 0 ? (IntPtr)m_regionAPointer : (IntPtr)m_regionBPointer;
    }

    // Incrémente la taille écrite dans la région active
    public void IncrementWritten(int len)
    {
        if (m_regionBPointer != 0)
            m_regionBSize += len;
        else
            m_regionASize += len;
    }
}
