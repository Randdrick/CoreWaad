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
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WaadShared.Network;

public class Socket
{
    private readonly System.Net.Sockets.Socket _socket;
    private bool _isConnected;
    private bool _isDeleted;
    private readonly object _writeMutex = new();
    private readonly object _readMutex = new();
    private IPEndPoint _clientEndPoint;
    private readonly byte[] _readBuffer;
    private readonly byte[] _writeBuffer;
    private IntPtr _completionPort; // Equivalent to HANDLE in C++
    private int _writeLock;

    public Socket(System.Net.Sockets.Socket socket, int sendBufferSize, int recvBufferSize)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _isConnected = false;
        _isDeleted = false;
        _writeLock = 0;

        // Allocate Buffers
        _readBuffer = new byte[recvBufferSize];
        _writeBuffer = new byte[sendBufferSize];

#if CONFIG_USE_IOCP
        // Simulate getting a completion port from a manager
        _completionPort = SocketManager.GetCompletionPort();
#endif
    }

    public Socket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
    {
        _socket = new System.Net.Sockets.Socket(addressFamily, socketType, protocolType);
    }

    ~Socket()
    {
        Disconnect();
    }

    public bool Connect(string address, int port)
    {
        try
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(address);
            IPAddress[] addresses = hostEntry.AddressList;

            if (addresses.Length == 0)
                return false;

            _clientEndPoint = new IPEndPoint(addresses[0], port);
            _socket.Connect(_clientEndPoint);

            OnConnect();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return false;
        }
    }

    public void Accept(IPAddress any, IPEndPoint address)
    {
        _clientEndPoint = address;
        OnConnect();
    }

    private void OnConnect()
    {
        _socket.Blocking = false;
        _socket.NoDelay = true;
        _isConnected = true;

        // Call virtual onconnect
        OnConnect();
    }

    public bool Send(byte[] bytes)
    {
        lock (_writeMutex)
        {
            try
            {
                return _socket.Send(bytes) == bytes.Length;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send failed: {ex.Message}");
                return false;
            }
        }
    }

    public void BurstBegin()
    {
        Monitor.Enter(_writeMutex);
    }

    public bool BurstSend(byte[] bytes)
    {
        try
        {
            Buffer.BlockCopy(bytes, 0, _writeBuffer, 0, bytes.Length);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BurstSend failed: {ex.Message}");
            return false;
        }
    }

    public static void BurstPush()
    {
        // Implementation for pushing events to the queue
    }

    public void BurstEnd()
    {
        Monitor.Exit(_writeMutex);
    }

    public string GetRemoteIP()
    {
        return _clientEndPoint?.Address.ToString() ?? "noip";
    }

    public int GetRemotePort()
    {
        return _clientEndPoint?.Port ?? 0;
    }

    public System.Net.Sockets.Socket GetSocket()
    {
        return _socket;
    }

    public void Disconnect()
    {
        if (!_isConnected) return;

        _isConnected = false;
        _socket.Close();

        // Call virtual ondisconnect
        OnDisconnect();

        if (!_isDeleted) Delete();
    }

    public void Delete()
    {
        if (_isDeleted) return;
        _isDeleted = true;

        if (_isConnected) Disconnect();
        // Assuming there's a garbage collector mechanism in place
    }

    public bool IsDeleted()
    {
        return _isDeleted;
    }

    public bool IsConnected()
    {
        return _isConnected;
    }

    public IPEndPoint GetRemoteStruct()
    {
        return _clientEndPoint;
    }

    public byte[] GetReadBuffer()
    {
        return _readBuffer;
    }

    public byte[] GetWriteBuffer()
    {
        return _writeBuffer;
    }

    public IPAddress GetRemoteAddress()
    {
        return _clientEndPoint?.Address;
    }

    protected virtual void OnRead()
    {
        // Implementation for derived classes
    }

    protected virtual void OnDisconnect()
    {
        // Implementation for derived classes
    }

    // Platform-specific methods ( placeholders )
    public static void SetupReadEvent()
    {
        // Implementation for setting up read events
    }

    public static void ReadCallback(uint len)
    {
        // Implementation for read callback
    }

    public static void WriteCallback()
    {
        // Implementation for write callback
    }

#if CONFIG_USE_IOCP
    // Simulated SocketManager class
    public static class SocketManager
    {
        public static IntPtr GetCompletionPort()
        {
            // Simulate obtaining a completion port handle
            return new IntPtr(1); // Placeholder value
        }
    }

    public void SetCompletionPort(IntPtr cp)
    {
        _completionPort = cp;
    }

    public void IncSendLock()
    {
        Interlocked.Increment(ref _writeLock);
    }

    public void DecSendLock()
    {
        Interlocked.Decrement(ref _writeLock);
    }

    public bool AcquireSendLock()
    {
        if (_writeLock > 0)
            return false;
        else
        {
            IncSendLock();
            return true;
        }
    }

    public void ReleaseSendLock()
    {
        DecSendLock();
    }

    internal void Bind(IPEndPoint address)
    {
        _socket.Bind(address);
    }

    internal void Close()
    {
        _socket.Close();
    }

    internal Socket Accept(int minPort)
    {
        _socket.Listen(1);
        var acceptedSocket = _socket.Accept();
        return new Socket(acceptedSocket, _writeBuffer.Length, _readBuffer.Length);
    }

    internal Socket Accept(IPAddress any, ref int len, int minPort, int maxPort)
    {
        _socket.Listen(1);
        var acceptedSocket = _socket.Accept();
        return new Socket(acceptedSocket, _writeBuffer.Length, _readBuffer.Length);
    }

    internal void SetSocketOption(SocketOptionLevel socket, SocketOptionName reuseAddress, bool v)
    {
        _socket.SetSocketOption(socket, reuseAddress, v);
    }

    internal void Listen(int v)
    {
        _socket.Listen(v);
    }

    internal Socket Accept()
    {
        var acceptedSocket = _socket.Accept();
        return new Socket(acceptedSocket, _writeBuffer.Length, _readBuffer.Length);
    }

    internal Socket Accept(object m)
    {
        var acceptedSocket = _socket.Accept();
        return new Socket(acceptedSocket, _writeBuffer.Length, _readBuffer.Length);
    }

    // Placeholder for overlapped structures
    private class OverlappedStruct
    {
        // Implementation for overlapped structure
    }

    private OverlappedStruct _readEvent;
    private OverlappedStruct _writeEvent;

    public AddressFamily AddressFamily { get; }
    public SocketType SocketType { get; }
    public ProtocolType ProtocolType { get; }
    public bool Blocking { get; internal set; }
#endif
}

