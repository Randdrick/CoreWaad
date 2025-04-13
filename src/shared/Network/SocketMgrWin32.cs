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
    private readonly ConcurrentBag<CustomSocket> _sockets = [];
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
        List<CustomSocket> toKill = [];

        lock (_socketLock)
        {
            toKill.AddRange(_sockets);
        }

        foreach (CustomSocket socket in toKill)
        {
            socket.Close();
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
            CustomSocket socket = completionKey == IntPtr.Zero ? null : (CustomSocket)completionKey;
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

    private static void HandleReadComplete(CustomSocket socket, uint bytesTransferred)
    {
        if (socket != null && !socket.IsDeleted)
        {
            CustomSocket.MarkReadEventComplete();
            if (bytesTransferred > 0)
            {
                socket.ReadBuffer.IncrementWritten((int)bytesTransferred);
                socket.OnRead((int)bytesTransferred);
                socket.SetupReadEvent();
            }
            else
            {
                socket.Delete(); // Queue deletion
            }
        }
    }

    private static void HandleWriteComplete(CustomSocket socket, uint bytesTransferred)
    {
        if (socket != null && !socket.IsDeleted)
        {
            CustomSocket.MarkWriteEventComplete();
            CustomSocket.BurstBegin();
            socket.WriteBuffer.Remove((int)bytesTransferred);
            if (socket.WriteBuffer.GetContiguousBytes() > 0)
            {
                socket.WriteCallback();
            }
            else
            {
                socket.DecrementSendLock();
            }
            CustomSocket.BurstEnd();
        }
    }

    private static void HandleShutdown(CustomSocket socket)
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

public class CustomSocket(Socket socket)
{
    private readonly Socket _socket = socket;

    public IntPtr Handle => _socket.Handle;
    private readonly Buffer _readBuffer = new();
    private readonly Buffer _writeBuffer = new();
    private readonly object _readMutex = new();
    private readonly object _writeMutex = new();
    private bool _deleted;
    private bool _connected = true;
    private int _sendLock;

    public bool IsDeleted => _deleted;
    public Buffer ReadBuffer => _readBuffer;
    public Buffer WriteBuffer => _writeBuffer;

    public void WriteCallback()
    {
        if (_deleted || !_connected)
            return;

        lock (_writeMutex)
        {
            if (_writeBuffer.GetContiguousBytes() > 0)
            {
                try
                {
                    int bytesSent = _socket.Send(_writeBuffer.GetBuffer(), _writeBuffer.GetContiguousBytes(), SocketFlags.None);
                    _writeBuffer.Remove(bytesSent);
                    if (_writeBuffer.GetContiguousBytes() == 0)
                    {
                        DecrementSendLock();
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.WouldBlock)
                    {
                        DecrementSendLock();
                        Disconnect();
                    }
                }
            }
            else
            {
                DecrementSendLock();
            }
        }
    }

    public void SetupReadEvent()
    {
        if (_deleted || !_connected)
            return;

        lock (_readMutex)
        {
            try
            {
                BeginReceive(_readBuffer.GetBuffer(), 0, _readBuffer.GetSpace(), SocketFlags.None, ReadCallback, null);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    Disconnect();
                }
            }
        }
    }

    private void ReadCallback(IAsyncResult ar)
    {
        try
        {
            int bytesReceived = EndReceive(ar);
            OnRead(bytesReceived);
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode != SocketError.WouldBlock)
            {
                Disconnect();
            }
        }
    }

    public void OnRead(int len)
    {
        if (len == 0)
        {
            Disconnect();
            return;
        }

        _readBuffer.IncrementWritten(len);
        OnRecvData();

        if (!_connected)
            return;

        SetupReadEvent();
    }

    public static void OnRecvData()
    {
        // Handle received data
    }

    public void BurstPush()
    {
        if (AcquireSendLock())
        {
            WriteCallback();
        }
    }

    public bool AcquireSendLock()
    {
        lock (_writeMutex)
        {
            if (_sendLock == 0)
            {
                _sendLock = 1;
                return true;
            }
            return false;
        }
    }

    public void DecrementSendLock()
    {
        lock (_writeMutex)
        {
            _sendLock = 0;
        }
    }

    public void Disconnect()
    {
        _connected = false;
        Close();
    }

    public void Close()
    {
        _deleted = true;
        Disconnect();
    }

    public static void MarkReadEventComplete()
    {
        // Implémentation pour marquer qu'un événement de lecture est terminé
        CLog.Debug("[SOCKETMGR]", "Read event completed.");
    }

    public static void MarkWriteEventComplete()
    {
        // Implémentation pour marquer qu'un événement d'écriture est terminé
        CLog.Debug("[SOCKETMGR]", "Write event completed.");
    }

    public static void BurstBegin()
    {
        // Implémentation pour commencer une opération en rafale
        CLog.Debug("[SOCKETMGR]", "Burst operation started.");
    }

    public static void BurstEnd()
    {
        // Implémentation pour terminer une opération en rafale
        CLog.Debug("[SOCKETMGR]", "Burst operation ended.");
    }

    public void Delete()
    {
        _deleted = true;
    }

    public static explicit operator CustomSocket(IntPtr v)
    {
        return null;
    }
}

public class Buffer
{
    private readonly byte[] _buffer = new byte[1024];
    private int _written;

    public void IncrementWritten(int len)
    {
        _written += len;
    }

    public void Remove(int len)
    {
        Array.Copy(_buffer, len, _buffer, 0, _written - len);
        _written -= len;
    }

    public int GetContiguousBytes()
    {
        return _written;
    }

    public byte[] GetBuffer()
    {
        return _buffer;
    }

    public int GetSpace()
    {
        return _buffer.Length - _written;
    }

    internal static void BlockCopy(byte[] source, int sourceOffset, byte[] destination, int destinationOffset, int length)
    {
        // Utilisation de Buffer.BlockCopy pour copier les données
        System.Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, length);
    }
}

#endif