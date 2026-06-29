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
using System.Collections.Concurrent;
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

public class BufferPool
{
    public class BufferBucket
    {
        public static readonly int[] BufferSizes =
        [
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
        private readonly ConcurrentBag<WorldPacket> m_packetBuffer = new();
        private int m_used;
        private int m_allocCounter;

        public BufferBucket(BufferPool parent, BufferBucketSize sz)
        {
            m_pool = parent;
            m_size = sz;
            m_byteSize = BufferSizes[(int)sz];
            m_used = 0;
            m_allocCounter = 0;
            FillUp();
        }

        public void FillUp()
        {
            // Croissance exponentielle pour éviter la fragmentation
            int targetCount = 100;
            while (m_packetBuffer.Count < targetCount)
            {
                var packet = new WorldPacket(m_byteSize)
                {
                    m_bufferPool = (int)m_size
                };
                m_packetBuffer.Add(packet);
            }
        }

        public void Queue(WorldPacket pData)
        {
            Interlocked.Decrement(ref m_allocCounter);
            Interlocked.Decrement(ref m_used);

            pData.Clear();
            m_packetBuffer.Add(pData);
        }

        public WorldPacket Dequeue()
        {
            Interlocked.Increment(ref m_allocCounter);
            Interlocked.Increment(ref m_used);

            if (m_packetBuffer.TryTake(out var packet))
            {
                return packet;
            }

            // Si le pool est vide, créer un nouveau packet
            return new WorldPacket(m_byteSize)
            {
                m_bufferPool = (int)m_size
            };
        }

        public void Stats()
        {
            int mem = (m_packetBuffer.Count + m_used) * m_byteSize;
            Console.WriteLine($" Bucket[{m_size}]: {m_byteSize} bytes: sz = {m_packetBuffer.Count} used = {m_used} alloc: {m_allocCounter} mem: {mem / 1024.0f:F3} K");
        }

        public void Optimize()
        {
            // Garder une taille minimale de 50 buffers
            int targetSize = Math.Max(50, m_used * 2);
            
            // Si on a trop de buffers, en supprimer
            while (m_packetBuffer.Count > targetSize)
            {
                m_packetBuffer.TryTake(out _);
            }
            
            // Si on en a trop peu, en ajouter
            while (m_packetBuffer.Count < targetSize)
            {
                var packet = new WorldPacket(m_byteSize)
                {
                    m_bufferPool = (int)m_size
                };
                m_packetBuffer.Add(packet);
            }

            m_allocCounter = 0;
        }
    }

    private class BufferBucketNode(BufferBucket bck)
    {
        public BufferBucket m_bucket = bck;
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
        return bucketNode.m_bucket.Dequeue();
    }

    public void Deallocate(WorldPacket pck)
    {
        if (pck.m_bufferPool == -1)
        {
            pck = null;
            return;
        }

        BufferBucketNode b = m_buckets[pck.m_bufferPool];
        b.m_bucket.Queue(pck);
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
            bucketNode.m_bucket.Stats();
        }
    }

    public void Optimize()
    {
        for (int x = 0; x < (int)BufferBucketSize.BufferBucketCount; ++x)
        {
            BufferBucketNode bucketNode = m_buckets[x];
            bucketNode.m_bucket.Optimize();
        }
    }
}

