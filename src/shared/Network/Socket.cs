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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WaadShared.Network;

public class Socket
{
    private readonly System.Net.Sockets.Socket _socket;
    private bool m_connected;
    private bool m_deleted;
#if CONFIG_USE_IOCP    
    private int m_writeLock;
#endif
    private IPEndPoint m_clientEndPoint;
    private readonly CircularBuffer readBuffer;
    private readonly CircularBuffer writeBuffer;
    private readonly object m_writeMutex = new();
    private readonly object m_readMutex = new();

    public Socket(System.Net.Sockets.Socket socket, int sendbuffersize, int recvbuffersize)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        m_connected = false;
        m_deleted = false;
#if CONFIG_USE_IOCP
        m_writeLock = 0;
#endif
        readBuffer = new CircularBuffer();
        writeBuffer = new CircularBuffer();
        readBuffer.Allocate(recvbuffersize);
        writeBuffer.Allocate(sendbuffersize);
        m_clientEndPoint = null;

#if CONFIG_USE_IOCP
        m_writeLock = 0;
#endif

        _socket ??= new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public Socket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
    {
        _socket = new System.Net.Sockets.Socket(addressFamily, socketType, protocolType);
        m_connected = false;
        m_deleted = false;
#if CONFIG_USE_IOCP
        m_writeLock = 0;
#endif
        readBuffer = new CircularBuffer();
        writeBuffer = new CircularBuffer();
        readBuffer.Allocate(ByteBuffer.DEFAULT_SIZE);
        writeBuffer.Allocate(ByteBuffer.DEFAULT_SIZE);
        m_clientEndPoint = null;
    }

    public Socket()
    {
        _socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        m_connected = false;
        m_deleted = false;
#if CONFIG_USE_IOCP
        m_writeLock = 0;
#endif
        readBuffer = new CircularBuffer();
        writeBuffer = new CircularBuffer();
        readBuffer.Allocate(ByteBuffer.DEFAULT_SIZE);
        writeBuffer.Allocate(ByteBuffer.DEFAULT_SIZE);
        m_clientEndPoint = null;
    }

    ~Socket()
    {
        // No explicit cleanup needed, but call Disconnect for symmetry
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

            m_clientEndPoint = new IPEndPoint(addresses[0], port);
            SocketOps.Blocking(_socket);
            _socket.Connect(m_clientEndPoint);

#if CONFIG_USE_IOCP
            // IOCP: assign completion port if needed
            m_completionPort = SocketManager.GetCompletionPort();
#endif

            // Ensure non-blocking connect is completed before proceeding
            if (!CheckNonBlockingConnectCompleted())
                return false;

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
        m_clientEndPoint = address;
        OnConnect();
    }

    protected void OnConnect()
    {
        SetToConnected();

#if CONFIG_USE_IOCP
        CLog.Notice("[SOCKET]", "Initializing IOCP Read Event for socket.");
        ThreadPool.QueueUserWorkItem(_ => SocketExtensions.SetupReadEvent(this));

        SocketManager.Instance.AddSocket(this);
#endif
#if CONFIG_USE_EPOLL
        SocketMgr.AddSocket(_socket);
#endif
        OnConnectVirtual();
    }

    public virtual void OnConnectVirtual() { }
    public virtual void OnRead() { }
    public virtual void OnDisconnect() { }

    public bool Send(byte[] bytes)
    {
        bool rv;
        BurstBegin();
        rv = BurstSend(bytes, bytes.Length);
        if (rv)
            BurstPush();
        BurstEnd();
        return rv;
    }

    internal int Send(byte[] bytes, int size, SocketFlags flags)
    {
        lock (m_writeMutex)
        {
            try
            {
                if (_socket == null)
                {
                    Console.WriteLine("_socket is not initialized.");
                    return 0;
                }

                int bytesSent = _socket.Send(bytes, size, flags);
                return bytesSent == bytes.Length ? bytesSent : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send failed: {ex.Message}");
                return 0;
            }
        }
    }

    public void BurstBegin()
    {
        Monitor.Enter(m_writeMutex);
    }

    public bool BurstSend(byte[] bytes, int size)
    {
        return writeBuffer.Write(bytes, size);
    }

    public static void BurstPush()
    {
        // In IOCP, this would post a write event. Here, just a placeholder.
    }

    public void BurstEnd()
    {
        Monitor.Exit(m_writeMutex);
    }

    public string GetRemoteIP()
    {
        return m_clientEndPoint?.Address.ToString() ?? "noip";
    }

    public int GetRemotePort()
    {
        return m_clientEndPoint?.Port ?? 0;
    }

    public System.Net.Sockets.Socket GetFd() => _socket;

    public void Disconnect()
    {
        m_connected = false;
    #if CONFIG_USE_IOCP
        SocketManager.Instance.RemoveSocket(this);
    #endif
    #if CONFIG_USE_EPOLL
        SocketMgr.RemoveSocket(_socket);
    #endif
        SocketOps.CloseSocket(_socket);
        OnDisconnect();
        if (!m_deleted) Delete();
    }

    public void Delete()
    {
        if (m_deleted) return;
        m_deleted = true;
        if (m_connected) Disconnect();
        SocketGarbageCollector.Instance.QueueSocket(this);
    }

    public bool IsDeleted() => m_deleted;
    public bool IsConnected() => m_connected;
    public IPEndPoint GetRemoteStruct() => m_clientEndPoint;
    public CircularBuffer GetReadBuffer() => readBuffer;
    public CircularBuffer GetWriteBuffer() => writeBuffer;

#if CONFIG_USE_IOCP
    private IntPtr m_completionPort;
    public void SetCompletionPort(IntPtr cp) { m_completionPort = cp; }
    public void IncSendLock() { Interlocked.Increment(ref m_writeLock); }
    public void DecSendLock() { Interlocked.Decrement(ref m_writeLock); }
    public string RemoteEndPoint { get; set; }
    public int Handle { get; internal set; }

    public bool AcquireSendLock()
    {
        if (m_writeLock != 0)
            return false;
        IncSendLock();
        return true;
    }

    internal static void Accept(IPEndPoint remoteEndPoint)
    {
        if (remoteEndPoint == null)
        {
            CLog.Error("[SOCKET]", "Remote endpoint is null.");
            return;
        }

        CLog.Notice("[SOCKET]", $"Accepted connection from {remoteEndPoint.Address}:{remoteEndPoint.Port}");
        var socketInstance = new Socket
        {
            m_clientEndPoint = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port)
        };

        socketInstance.OnConnect();
    }

    internal static void Close()
    {
        Close();
    }
#endif

#if CONFIG_USE_EPOLL
    private volatile int _writeLock;

    public void PostEvent(uint events)
    {
        // Implementation for posting epoll event
    }

    public void IncSendLock()
    {
        Interlocked.Increment(ref _writeLock);
    }

    public void DecSendLock()
    {
        Interlocked.Decrement(ref _writeLock);
    }

    public bool HasSendLock()
    {
        return _writeLock != 0;
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
#endif

#if CONFIG_USE_KQUEUE
    private volatile int _writeLock;

    public void PostEvent(int events, bool oneshot)
    {
        // Implementation for posting kqueue event
    }

    public void IncSendLock()
    {
        Interlocked.Increment(ref _writeLock);
    }

    public void DecSendLock()
    {
        Interlocked.Decrement(ref _writeLock);
    }

    public bool HasSendLock()
    {
        return _writeLock != 0;
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
#endif

    public static T ConnectTCPSocket<T>(string hostname, ushort port) where T : Socket, new()
    {
        var hostEntry = Dns.GetHostEntry(hostname);
        if (hostEntry.AddressList.Length == 0)
            return null;

        var conn = new IPEndPoint(hostEntry.AddressList[0], port);
        T s = new();
        if (!s.Connect(hostname, port))
        {
            s.Delete();
            return null;
        }
        return s;
    }
    public static IEnumerable<object> GetSocketList()
    {
        // Implementation for getting the socket list
        return [];
    }
    public static void RemoveSocket(Socket deadSocket)
    {
        // Implementation for removing a socket
    }
    internal static void Select(HashSet<Socket> m_readableSet, HashSet<Socket> writable, HashSet<Socket> m_exceptionSet, int v)
    {
        try
        {
            Select(m_readableSet, writable, m_exceptionSet, v);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Select failed: {ex.Message}");
        }
    }

    // Add these helper methods to ensure non-blocking connect and connected state are handled properly
    private bool CheckNonBlockingConnectCompleted()
    {
        // For non-blocking sockets, check if the connection is established
        try
        {
            if (_socket == null)
                return false;

            // Poll with SelectWrite to check if the socket is connected
            bool connected = _socket.Poll(0, SelectMode.SelectWrite) && _socket.Connected;
            if (!connected)
            {
                Console.WriteLine("Non-blocking connect not completed or socket not connected.");
            }
            return connected;
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"CheckNonBlockingConnectCompleted SocketException: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CheckNonBlockingConnectCompleted Exception: {ex.Message}");
            return false;
        }
    }

    private void SetToConnected()
    {
        // Set socket to non-blocking and disable Nagle's algorithm
        try
        {
            if (_socket == null)
                return;

            _socket.Blocking = false;
            _socket.NoDelay = true;
            m_connected = true;
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"SetToConnected SocketException: {ex.Message}");
            m_connected = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SetToConnected Exception: {ex.Message}");
            m_connected = false;
        }
    }
    public static IPAddress GetRemoteAddress(Socket socket)
    {
        return socket?.m_clientEndPoint?.Address;
    }
}

// Garbage collector for sockets
public class SocketGarbageCollector
{
    private const int SOCKET_GC_TIMEOUT = 15;
    private readonly ConcurrentDictionary<Socket, long> deletionQueue = new();
    private readonly object lockObj = new();

    private static readonly Lazy<SocketGarbageCollector> instance = new(() => new SocketGarbageCollector());
    public static SocketGarbageCollector Instance => instance.Value;

    private SocketGarbageCollector() { }

    ~SocketGarbageCollector()
    {
        foreach (var socket in deletionQueue.Keys)
        {
            socket.Delete();
        }
    }

    public void Update()
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        lock (lockObj)
        {
            foreach (var socket in deletionQueue.Keys.ToList())
            {
                if (deletionQueue[socket] <= currentTime)
                {
                    socket.Delete();
                    deletionQueue.TryRemove(socket, out _);
                }
            }
        }
    }

    public void QueueSocket(Socket socket)
    {
        lock (lockObj)
        {
            deletionQueue[socket] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + SOCKET_GC_TIMEOUT;
        }
    }
}
