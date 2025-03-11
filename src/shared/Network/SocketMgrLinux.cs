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

#if CONFIG_USE_EPOLL

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace WaadShared.Network
{
    public class SocketMgr
    {
        private const int SOCKET_HOLDER_SIZE = 30000;
        private readonly List<Socket> fds;
        private readonly List<Socket> listenfds;
        private int socketCount;
        private static SocketMgr instance;        

        private SocketMgr()
        {
            fds = new List<Socket>(SOCKET_HOLDER_SIZE);
            listenfds = new List<Socket>(SOCKET_HOLDER_SIZE);
            socketCount = 0;
        }

        public static SocketMgr GetInstance()
        {
            instance ??= new SocketMgr();
            return instance;
        }

        public void AddSocket(Socket s)
        {
            if (!fds.Contains(s))
            {
                fds.Add(s);
                socketCount++;
            }
        }

        public void AddListenSocket(Socket s)
        {
            if (!listenfds.Contains(s))
            {
                listenfds.Add(s);
            }
        }

        public void RemoveSocket(Socket s)
        {
            if (fds.Contains(s))
            {
                // fds.Remove(s);
                socketCount--;
            }
        }

        public int Count()
        {
            return socketCount;
        }

        public void CloseAll()
        {
            foreach (var socket in fds)
            {
                socket.Close();
            }
            fds.Clear();
            socketCount = 0;
        }

        public static void SpawnWorkerThreads()
        {
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                var thread = new Thread(new SocketWorkerThread().Run);
                thread.Start();
            }
        }

        public List<Socket> GetSockets()
        {
            return fds;
        }

        public static int GetEpollFd()
        {
            // Simulate getting an epoll file descriptor
            return 0;
        }
    }

    public class SocketWorkerThread
    {
        private bool running;

        public void Run()
        {
            running = true;
            while (running)
            {
                // Simulate epoll_wait using Socket.Select
                Socket.Select(SocketMgr.GetInstance().GetSockets(), null, null, 1000);

                foreach (var socket in SocketMgr.GetInstance().GetSockets())
                {
                    if (socket.Available > 0)
                    {
                        // Handle socket data
                    }
                }
            }
        }

        public void OnShutdown()
        {
            running = false;
        }
    }

    public class Buffer
    {
        private readonly byte[] buffer = new byte[1024];
        private int position = 0;

        public byte[] GetBuffer()
        {
            return buffer;
        }

        public int GetSpace()
        {
            return buffer.Length - position;
        }

        public void IncrementWritten(int bytes)
        {
            position += bytes;
        }

        public byte[] GetBufferStart()
        {
            return buffer;
        }

        public int GetContiguousBytes()
        {
            return position;
        }

        public void Remove(int bytes)
        {
            Array.Copy(buffer, bytes, buffer, 0, position - bytes);
            position -= bytes;
        }

        internal static void BlockCopy(byte[] source, int sourceOffset, byte[] destination, int destinationOffset, int length)
        {
            // Utilisation de Buffer.BlockCopy pour copier les données
            System.Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, length);
        }
    }

    public static class SocketExtensions
    {
        private static readonly Mutex m_readMutex = new();
        private static readonly Buffer readBuffer = new();
        private static readonly Buffer writeBuffer = new();        

        public static void PostEvent(this Socket socket, uint events)
        {
            int epoll_fd = SocketMgr.GetEpollFd();

            // In C#, we would typically use SocketAsyncEventArgs or similar for async operations.
            // Here, we'll simulate the epoll behavior using SocketAsyncEventArgs.
            SocketAsyncEventArgs socketEventArg = new()
            {
                UserToken = socket.Handle,
                SocketFlags = SocketFlags.None
            };

            // Set the event type (EPOLLIN, EPOLLOUT, etc.)
            // In C#, you would handle this differently, possibly using async methods.

            // Post actual event
            try
            {
                // Simulate epoll_ctl with async socket operations
                // This is a simplification; actual implementation would depend on your async model.
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.BeginReceive(new byte[1024], 0, 1024, SocketFlags.None, new AsyncCallback(OnReceive), socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not post event on fd {socket.Handle}: {ex.Message}");
            }
        }

        private static void OnReceive(IAsyncResult result)
        {
            Socket socket = (Socket)result.AsyncState;
            try
            {
                int bytesRead = socket.EndReceive(result);
                if (bytesRead > 0)
                {
                    ReadCallback(socket, bytesRead);
                }
                else
                {
                    Disconnect(socket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnReceive: {ex.Message}");
                Disconnect(socket);
            }
        }

        private static void ReadCallback(Socket socket, int len)
        {
            m_readMutex.WaitOne();

            try
            {
                byte[] buffer = readBuffer.GetBuffer();
                int bytes = socket.Receive(buffer, len, SocketFlags.None);
                if (bytes <= 0)
                {
                    Disconnect(socket);
                    return;
                }
                else
                {
                    readBuffer.IncrementWritten(bytes);
                    OnRead(socket);
                }
            }
            finally
            {
                m_readMutex.ReleaseMutex();
            }
        }

        public static void WriteCallback(Socket socket)
        {
            try
            {
                byte[] buffer = writeBuffer.GetBufferStart();
                int bytesWritten = socket.Send(buffer, writeBuffer.GetContiguousBytes(), SocketFlags.None);
                if (bytesWritten < 0)
                {
                    Disconnect(socket);
                    return;
                }

                writeBuffer.Remove(bytesWritten);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in WriteCallback: {ex.Message}");
                Disconnect(socket);
            }
        }

        public static void BurstPush(Socket socket)
        {
            if (AcquireSendLock(socket))
            {
                socket.PostEvent((uint)SocketFlags.None); // Simulate EPOLLOUT
            }
        }

        private static void Disconnect(Socket socket)
        {
            // Implement disconnection logic here            
            socket.Close();
        }

        private static void OnRead(Socket socket)
        {
            // Implement read handling logic here
        }

        private static bool AcquireSendLock(Socket socket)
        {
            // Implement send lock acquisition logic here
            return true;
        }
    }
}

#endif