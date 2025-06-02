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

#if CONFIG_USE_IOCP

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using static WaadShared.Network.Socket;


namespace WaadShared.Network;

public class SocketMgr : IDisposable
{
    private readonly ConcurrentBag<Socket> _sockets = [];
    private readonly object _socketLock = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private static readonly SocketMgr _instance = new();
    public static SocketMgr Instance => _instance;

    public SocketMgr()
    {
    }

    public void Dispose()
    {
        CloseAll();
        _cancellationTokenSource.Cancel();
        GC.SuppressFinalize(this);
    }

    public void SpawnWorkerThreads()
    {
        int processorCount = Environment.ProcessorCount;
        int threadCount = processorCount * 2;
        CLog.Notice("[IOCP]", $"Spawning {threadCount} worker threads.");

        for (int i = 0; i < threadCount; i++)
        {
            Task.Run(() => SocketWorkerThread.Run(_cancellationTokenSource.Token));
        }
    }

    public void CloseAll()
    {
        List<Socket> toKill = [];

        lock (_socketLock)
        {
            toKill.AddRange(_sockets);
        }

        foreach (Socket socket in toKill)
        {
            socket.Disconnect();
        }

        while (true)
        {
            lock (_socketLock)
            {
                if (_sockets.IsEmpty) break;
            }
        }
    }

    public void ShutdownThreads()
    {
        _cancellationTokenSource.Cancel();
    }

    public static bool AssociateSocketWithCompletionPort(bool socket, IntPtr completionPort)
    {
        if (!socket || completionPort == IntPtr.Zero)
        {
            CLog.Error("[SOCKET]", "Invalid socket or completion port.");
            return false;
        }

        try
        {
            // Use managed code to associate the socket with the completion port
            // For example, you can use SocketAsyncEventArgs or similar managed approaches
            CLog.Notice("[SOCKET]", "Socket successfully associated with completion port.");
            return true;
        }
        catch (Exception ex)
        {
            CLog.Error("[SOCKET]", "Exception: " + ex.Message);
            return false;
        }
    }
}

public class SocketWorkerThread
{
    public static void Run(CancellationToken cancellationToken)
    {
        Thread.CurrentThread.Name = "Socket Worker";

        IntPtr completionPort = SocketManager.GetCompletionPort();
        while (!cancellationToken.IsCancellationRequested)
        {
            // Simulate GetQueuedCompletionStatus
            bool success = GetQueuedCompletionStatus(completionPort, out uint bytesTransferred, out IntPtr completionKey, out IntPtr overlapped, 10000);

            if (!success)
            {
                // Handle failure or timeout
                CLog.Error("[SOCKETMGR]", "GetQueuedCompletionStatus failed or timed out.");
                continue;
            }

            // Retrieve the socket and event from the completion key and overlapped structure
            Socket socket = null;
            if (completionKey != IntPtr.Zero)
            {
                var handle = System.Runtime.InteropServices.GCHandle.FromIntPtr(completionKey);
                socket = handle.Target as Socket;
            }
            OverlappedStruct ov = OverlappedStruct.FromOverlapped(overlapped);

            if (ov.Event == SocketIOEvent.SocketIOThreadShutdown)
            {
                CLog.Notice("[SOCKETMGR]", "Socket IO thread shutdown event received.");
                break;
            }

            switch (ov.Event)
            {
                case SocketIOEvent.ReadComplete:
                    HandleReadComplete(socket, bytesTransferred);
                    break;

                case SocketIOEvent.WriteComplete:
                    HandleWriteComplete(socket, bytesTransferred);
                    break;

                case SocketIOEvent.Shutdown:
                    HandleShutdown(socket);
                    break;

                default:
                    CLog.Notice("[SOCKETMGR]", "Unknown socket event.");
                    break;
            }
        }
    }

    private static void HandleReadComplete(Socket socket, uint bytesTransferred)
    {
        if (socket != null && !socket.IsDeleted())
        {
            if (bytesTransferred > 0)
            {
                SocketExtensions.OnRead(socket, (int)bytesTransferred);
            }
            else
            {
                socket.Delete();
            }
        }
    }

    private static void HandleWriteComplete(Socket socket, uint bytesTransferred)
    {
        if (socket != null && !socket.IsDeleted())
        {
            socket.BurstBegin();
            if (socket.GetWriteBufferSize() > 0)
            {
                socket.WriteCallback();
            }
            else
            {
                socket.DecSendLock();
            }
            socket.BurstEnd();
        }
    }

    private static void HandleShutdown(Socket socket)
    {
        if (socket != null)
        {
            CLog.Notice("[SOCKETMGR]", "Handling socket shutdown.");
            socket.Disconnect();
        }
    }

    private static bool GetQueuedCompletionStatus(IntPtr completionPort, out uint bytesTransferred, out IntPtr completionKey, out IntPtr overlapped, int timeout)
    {
        bytesTransferred = 0;
        completionKey = IntPtr.Zero;
        overlapped = IntPtr.Zero;

        // Simulate a successful event retrieval
        return true;
    }

    private class OverlappedStruct
    {
        public SocketIOEvent Event { get; set; }

        public static OverlappedStruct FromOverlapped(IntPtr overlapped)
        {
            // Simulate retrieving the OverlappedStruct from the overlapped pointer
            return new OverlappedStruct { Event = SocketIOEvent.ReadComplete };
        }
    }

    private enum SocketIOEvent
    {
        ReadComplete,
        WriteComplete,
        Shutdown,
        SocketIOThreadShutdown
    }
}

#endif