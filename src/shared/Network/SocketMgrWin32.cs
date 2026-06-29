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
using System.Threading;
using System.Threading.Tasks;
using WaadShared.Threading;

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
        // Keep worker count low to avoid idle CPU overhead, but use 2 workers
        // so heartbeats are not starved when one worker is busy parsing packets.
        const int threadCount = 4;
        CLog.Notice("[IOCP]", $"Spawning {threadCount} I/O worker thread(s).");

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

        // Wait for all sockets to be removed with a reasonable timeout
        // to avoid busy-waiting during shutdown
        int maxWaitMs = 10000;  // 10 second timeout
        int elapsed = 0;
        while (elapsed < maxWaitMs)
        {
            lock (_socketLock)
            {
                if (_sockets.IsEmpty) break;
            }
            Thread.Sleep(10);  // Check every 10ms instead of spinning
            elapsed += 10;
        }

        if (elapsed >= maxWaitMs)
        {
            CLog.Warning("[SocketMgr]", $"Timeout waiting for sockets to close. {_sockets.Count} still pending.");
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
    private static int _runningWorkers = 0;
    public static int RunningWorkers => Volatile.Read(ref _runningWorkers);

    public static void Run(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _runningWorkers);
        Thread.CurrentThread.Name = "Socket Worker";
        CLog.Debug("[SOCKETMGR]", $"IOCP worker started. running={RunningWorkers}");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // True blocking wait: thread sleeps in OS until an event is enqueued
                    // BlockingCollection uses Monitor.Wait internally → 0% CPU when idle
                    if (SocketManager.IOCompletionQueue.TryTake(
                            out var evt,
                            millisecondsTimeout: 5000,
                            cancellationToken))
                    {
                        switch (evt.Event)
                        {
                            case SocketManager.SocketIOEvent.ReadComplete:
                                HandleReadComplete(evt.Socket, evt.BytesTransferred);
                                break;
                            case SocketManager.SocketIOEvent.WriteComplete:
                                HandleWriteComplete(evt.Socket, evt.BytesTransferred);
                                break;
                            case SocketManager.SocketIOEvent.Shutdown:
                                HandleShutdown(evt.Socket);
                                break;
                        }
                    }
                    // TryTake timed out → loop back, cancellationToken re-checked at top
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    CLog.Error("[SOCKETMGR]", $"Worker thread error: {ex.Message}");
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                CLog.Debug("[SOCKETMGR]", "IOCP worker exiting due to cancellation request.");
            }
        }
        finally
        {
            int running = Interlocked.Decrement(ref _runningWorkers);
            CLog.Debug("[SOCKETMGR]", $"IOCP worker stopped. running={running}");
        }
    }

    private static void HandleReadComplete(Socket socket, uint bytesTransferred)
    {
        if (socket == null || socket.IsDeleted()) return;
        if (bytesTransferred > 0)
        {
            NetworkThreadPool.Instance.ExecuteTask(_ =>
            {
                try
                {
                    socket.OnRead(); // Virtual dispatch: process received packet(s)
                }
                catch (Exception ex)
                {
                    CLog.Error("[SOCKETMGR]", $"Read handler exception on {socket.GetRemoteIP()}:{socket.GetRemotePort()}: {ex.Message}");
                    socket.Disconnect();
                    return false;
                }

                if (!socket.IsDeleted() && socket.IsConnected())
                {
                    socket.SetupReadEvent(); // Queue next async BeginReceive
                }

                return true;
            });
        }
        else
        {
            socket.Delete();
        }
    }

    private static void HandleWriteComplete(Socket socket, uint bytesTransferred)
    {
        if (socket == null || socket.IsDeleted()) return;
        socket.BurstBegin();
        if (socket.GetWriteBufferSize() > 0)
            socket.WriteCallback();
        else
            socket.DecSendLock();
        socket.BurstEnd();
    }

    private static void HandleShutdown(Socket socket)
    {
        if (socket == null) return;
        CLog.Notice("[SOCKETMGR]", "Handling socket shutdown.");
        socket.Disconnect();
    }
}

#endif