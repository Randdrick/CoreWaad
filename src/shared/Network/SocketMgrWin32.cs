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

namespace WaadShared.Network;

public class SocketMgr : IDisposable
{
    private readonly ConcurrentBag<CustomSocket> _sockets = [];
    private readonly object _socketLock = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
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
        Console.WriteLine($"[IOCP] : Spawning {threadCount} worker threads.");

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
}

public class SocketWorkerThread
{
    public static void Run(CancellationToken cancellationToken)
    {
        Thread.CurrentThread.Name = "Socket Worker";

        while (!cancellationToken.IsCancellationRequested)
        {
            // Simulate getting completion status
            Thread.Sleep(1000);

            // Handle socket events
            HandleSocketEvents();
        }
    }

    private static void HandleSocketEvents()
    {
        // Simulate handling socket events
        // In a real implementation, you would use async methods to handle socket events
        Console.WriteLine("Handling socket events...");
    }
}

public class CustomSocket(Socket socket)
{
    private readonly Socket _socket = socket;
    private readonly Buffer _readBuffer = new();
    private readonly Buffer _writeBuffer = new();
    private readonly object _readMutex = new();
    private readonly object _writeMutex = new();
    private bool _deleted;
    private bool _connected = true;
    private int _sendLock;

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
                        DecSendLock();
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.WouldBlock)
                    {
                        DecSendLock();
                        Disconnect();
                    }
                }
            }
            else
            {
                DecSendLock();
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
                _socket.BeginReceive(_readBuffer.GetBuffer(), 0, _readBuffer.GetSpace(), SocketFlags.None, ReadCallback, null);
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
            int bytesReceived = _socket.EndReceive(ar);
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

    public void DecSendLock()
    {
        lock (_writeMutex)
        {
            _sendLock = 0;
        }
    }

    public void Disconnect()
    {
        _connected = false;
        _socket.Close();
    }

    public void Close()
    {
        _deleted = true;
        Disconnect();
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