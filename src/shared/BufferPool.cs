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

namespace WaadShared;

public enum BufferBucketSize
{
    Buffer20Bytes,
    Buffer50Bytes,
    Buffer100Bytes,
    Buffer200Bytes,
    Buffer500Bytes,
    Buffer1KByte,
    Buffer5KByte,
    Buffer10KByte,
    BufferBucketCount
}

public class WorldPacket(int bufferSize)
{
    public int m_bufferPool;
    private readonly byte[] data = new byte[bufferSize];

    public void Clear()
    {
        Array.Clear(data, 0, data.Length);
    }
}

public class BufferPool
{
    public class BufferBucket
    {
        public static readonly int[] BufferSizes = [
            20,      // 20 bytes
            50,      // 50 bytes
            100,     // 100 bytes
            200,     // 200 bytes
            500,     // 500 bytes
            1000,    // 1 kbyte
            5000,    // 5 kbyte
            10000    // 10 kbyte (shouldn't be used much)
        ];

        private readonly BufferPool m_pool;
        private readonly BufferBucketSize m_size;
        private readonly int m_byteSize;
        private readonly List<WorldPacket> m_packetBuffer;
        private int m_packetBufferCount;
        private int m_used;
        private int m_allocCounter;

        public BufferBucket(BufferPool parent, BufferBucketSize sz)
        {
            m_pool = parent;
            m_size = sz;
            m_byteSize = BufferSizes[(int)sz];
            m_packetBufferCount = 100;
            m_allocCounter = 0;
            m_used = 0;
            m_packetBuffer = [];
            FillUp();
        }

        public void FillUp()
        {
            int newSize = 100 + m_packetBufferCount;
            while (m_packetBuffer.Count < newSize)
            {
                m_packetBuffer.Add(new WorldPacket(m_byteSize));
                m_packetBuffer[^1].m_bufferPool = (int)m_size;
            }
        }

        public void Queue(WorldPacket pData)
        {
            m_allocCounter--;
            m_used--;

            if (m_packetBuffer.Count == m_packetBufferCount)
            {
                m_packetBuffer.Capacity = 100 + m_packetBuffer.Count;
            }

            pData.Clear();
            m_packetBuffer[m_packetBufferCount++] = pData;
        }

        public WorldPacket Dequeue()
        {
            m_allocCounter++;
            m_used++;

            if (m_packetBufferCount == 0)
            {
                WorldPacket ret = new(m_byteSize)
                {
                    m_bufferPool = (int)m_size
                };
                return ret;
            }

            return m_packetBuffer[--m_packetBufferCount];
        }

        public void Stats()
        {
            int blocks = (int)((float)(m_packetBufferCount) / m_packetBuffer.Count * 50.0f / 2.0f);
            int mem = (m_packetBufferCount + m_used) * m_byteSize;

            Console.WriteLine($" Bucket[{m_size}]: {m_byteSize} bytes: sz = {m_packetBufferCount} resv = {m_packetBuffer.Count} alloc: {m_allocCounter} used: {m_used} mem: {mem / 1024.0f:F3} K");
            Console.WriteLine(new string('=', blocks) + new string(' ', 50 - blocks));
        }

        public void Optimize()
        {
            int y = Math.Abs(m_allocCounter) + 50;
            int cnt;

            if (m_allocCounter < 0)
            {
                cnt = y + m_packetBuffer.Count;
                while (m_packetBuffer.Count < cnt)
                {
                    m_packetBuffer.Add(new WorldPacket(m_byteSize));
                    m_packetBuffer[^1].m_bufferPool = -1;
                }
            }
            else
            {
                cnt = (m_packetBufferCount > y) ? y : m_packetBufferCount;
                while (m_packetBufferCount > cnt)
                {
                    m_packetBuffer.RemoveAt(--m_packetBufferCount);
                }
            }

            m_allocCounter = 0;
        }
    }

    private class BufferBucketNode(BufferPool.BufferBucket bck)
    {
        public BufferBucket m_bucket = bck;
        private readonly object m_lock = new();

        public object Lock => m_lock;
    }

    private readonly BufferBucketNode[] m_buckets;

    public BufferPool()
    {
        m_buckets = new BufferBucketNode[(int)BufferBucketSize.BufferBucketCount];
    }

    private static int GetBufferPool(int sz)
    {
        for (int x = 0; x < (int)BufferBucketSize.BufferBucketCount; ++x)
        {
            if (BufferBucket.BufferSizes[x] >= sz)
                return x;
        }
        return -1;
    }

    public WorldPacket Allocate(int sz)
    {
        int bufPool = GetBufferPool(sz);
        if (bufPool == -1)
            return new WorldPacket(sz);

        BufferBucketNode bucketNode = m_buckets[bufPool];
        lock (bucketNode.Lock)
        {
            return bucketNode.m_bucket.Dequeue();
        }
    }

    public void Deallocate(WorldPacket pck)
    {
        if (pck.m_bufferPool == -1)
        {
            pck = null;
            return;
        }

        BufferBucketNode b = m_buckets[pck.m_bufferPool];
        lock (b.Lock)
        {
            b.m_bucket.Queue(pck);
        }
    }

    public void Init()
    {
        for (int x = 0; x < (int)BufferBucketSize.BufferBucketCount; ++x)
        {
            m_buckets[x] = new BufferBucketNode(new BufferBucket(this, (BufferBucketSize)x));
        }
    }

    public void Destroy()
    {
        for (int x = 0; x < (int)BufferBucketSize.BufferBucketCount; ++x)
        {
            m_buckets[x] = null;
        }
    }

    public void Stats()
    {
        for (int x = 0; x < (int)BufferBucketSize.BufferBucketCount; ++x)
        {
            BufferBucketNode bucketNode = m_buckets[x];
            lock (bucketNode.Lock)
            {
                bucketNode.m_bucket.Stats();
            }
        }
    }

    public void Optimize()
    {
        for (int x = 0; x < (int)BufferBucketSize.BufferBucketCount; ++x)
        {
            BufferBucketNode bucketNode = m_buckets[x];
            lock (bucketNode.Lock)
            {
                bucketNode.m_bucket.Optimize();
            }
        }
    }
}

