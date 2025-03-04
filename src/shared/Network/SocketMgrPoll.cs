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

#if CONFIG_USE_POLL

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace WaadShared;

public class SocketMgr
{
    private const int SOCKET_HOLDER_SIZE = 65536;
    private static readonly Socket[] fds = new Socket[SOCKET_HOLDER_SIZE];
    private int highest_fd = 0;
    private static int socket_count = 0;
    private readonly List<SocketAsyncEventArgs> pollEvents = new();
    private static readonly object m_readMutex = new();
    private static readonly object m_writeMutex = new();
    private static readonly Socket m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    private static SocketMgr instance;

    private readonly byte[] m_readBuffer = new byte[1024];
    private int m_readByteCount = 0;
    private readonly int m_readBufferSize = 1024;

    private readonly byte[] m_writeBuffer = new byte[1024];
    private int m_writeByteCount = 0;

    private SocketMgr()
    {
        for (int i = 0; i < SOCKET_HOLDER_SIZE; i++)
        {
            fds[i] = null;
        }
    }

    public static SocketMgr GetInstance()
    {
        instance ??= new SocketMgr();
        return instance;
    }

    public static int Count()
    {
        return socket_count;
    }

    public void AddSocket(Socket s)
    {
        Console.WriteLine("AddSocket " + s.Handle.ToInt32());
        if (socket_count >= SOCKET_HOLDER_SIZE || fds[s.Handle.ToInt32()] != null)
        {
            Console.WriteLine("Double add");
            s.Close();
            return;
        }

        fds[s.Handle.ToInt32()] = s;

        if ((s.Handle.ToInt32() + 1) > highest_fd)
        {
            Console.WriteLine("New highest fd " + s.Handle.ToInt32());
            highest_fd = s.Handle.ToInt32() + 1;
        }

        socket_count++;
    }

    public static void RemoveSocket(Socket s)
    {
        Console.WriteLine("RemoveSocket " + s.Handle.ToInt32());
        if (socket_count >= SOCKET_HOLDER_SIZE || fds[s.Handle.ToInt32()] == null)
        {
            return;
        }

        fds[s.Handle.ToInt32()] = null;
        socket_count--;
    }

    public static void WantWrite(int fd)
    {
        // Implementation not provided in the original code
    }

    public void ThreadFunction()
    {
        Console.WriteLine("ThreadFunction()");
        while (true)
        {
            pollEvents.Clear();
            for (int i = 0; i < highest_fd; ++i)
            {
                if (fds[i] != null)
                {
                    SocketAsyncEventArgs args = new()
                    {
                        UserToken = fds[i]
                    };
                    if (fds[i].Available > 0)
                    {
                        args.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
                        fds[i].ReceiveAsync(args);
                    }
                    else
                    {
                        args.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
                        fds[i].SendAsync(args);
                    }
                    pollEvents.Add(args);
                }
            }

            if (pollEvents.Count == 0)
            {
                Thread.Sleep(20);
                continue;
            }

            Thread.Sleep(1000); // Simulate poll timeout

            foreach (var args in pollEvents)
            {
                if (args.UserToken is not Socket s) continue;

                if (args.SocketError == SocketError.Success)
                {
                    if (args.LastOperation == SocketAsyncOperation.Receive)
                    {
                        ReadCallback(s);
                    }
                    else if (args.LastOperation == SocketAsyncOperation.Send)
                    {
                        WriteCallback(s);
                    }
                }
                else
                {
                    s.Close();
                }
            }
        }
    }

    private void OnIOCompleted(object sender, SocketAsyncEventArgs e)
    {
        // Handle IO completion
    }

    private void ReadCallback(Socket s)
    {
        lock (m_readMutex)
        {
            int bytes = 0;
            try
            {
                bytes = s.Receive(m_readBuffer, m_readByteCount, m_readBufferSize - m_readByteCount, SocketFlags.None);
            }
            catch (SocketException)
            {
                RemoveSocket(s);
                return;
            }

            Console.WriteLine($"ReadCallback({s.Handle.ToInt32()}) {bytes} bytes");
            if (bytes < 0)
            {
                Monitor.Exit(m_readMutex);
                Disconnect(s);
                return;
            }
            else if (bytes > 0)
            {
                m_readByteCount += bytes;
                OnRead();
            }

            Monitor.Exit(m_readMutex);
        }
    }

    protected static void OnRead()
    {
        // Implementation for derived classes
    }

    private void WriteCallback(Socket s)
    {
        lock (m_writeMutex)
        {
            int bytesWritten = 0;
            try
            {
                bytesWritten = s.Send(m_writeBuffer, m_writeByteCount, SocketFlags.None);
            }
            catch (SocketException)
            {
                RemoveSocket(s);
                return;
            }

            Console.WriteLine($"WriteCallback() {bytesWritten}/{m_writeByteCount} bytes");
            if (bytesWritten < 0)
            {
                Disconnect(s);
                return;
            }

            if (bytesWritten > 0)
            {
                RemoveWriteBufferBytes(bytesWritten);
            }
        }
    }

    private void RemoveWriteBufferBytes(int bytes)
    {
        // Implement buffer removal logic here
        m_writeByteCount -= bytes;
        if (m_writeByteCount > 0)
        {
            Buffer.BlockCopy(m_writeBuffer, bytes, m_writeBuffer, 0, m_writeByteCount);
        }
    }

    private static void Disconnect(Socket s)
    {
        RemoveSocket(s);
        s.Close();
    }

    // Placeholder for Buffer class
    private class Buffer
    {
        public static int GetSpace()
        {
            // Implement buffer space retrieval logic here
            return 0;
        }

        public static byte[] GetBuffer()
        {
            // Implement buffer retrieval logic here
            return [];
        }

        public static void IncrementWritten(int bytes)
        {
            // Implement buffer write increment logic here
        }

        public static byte[] GetBufferStart()
        {
            // Implement buffer start retrieval logic here
            return [];
        }

        public static int GetContiguousBytes()
        {
            // Implement contiguous bytes retrieval logic here
            return 0;
        }

        public static void Remove(int bytes)
        {
            // Implement buffer removal logic here
        }

        internal static void BlockCopy(byte[] m_writeBuffer1, int bytes, byte[] m_writeBuffer2, int v, int m_writeByteCount)
        {
            throw new NotImplementedException();
        }
    }

    public void CloseAll()
    {
        for (int i = 0; i < highest_fd; ++i)
        {
            if (fds[i] != null)
            {
                fds[i].Close();
                fds[i] = null;
            }
        }
    }

    public void SpawnWorkerThreads()
    {
        int tc = 1;
        for (int i = 0; i < tc; ++i)
        {
            Thread thread = new(new ThreadStart(ThreadFunction));
            thread.Start();
        }
    }

    public static void ShutdownThreads()
    {
        // Implement shutdown threads
    }
}

public class SocketWorkerThread
{
    public static void Run()
    {
        SocketMgr sSocketMgr = SocketMgr.GetInstance();
        sSocketMgr.ThreadFunction();
    }
}

#endif