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
    private static Socket _socket;
    private bool _isConnected;
    private bool _isDeleted;
    private readonly object _writeMutex = new();
    private readonly object _readMutex = new();
    private static IPEndPoint _clientEndPoint;
    private readonly byte[] _readBuffer;
    private readonly byte[] _writeBuffer;
    private int _writeLock;

    public Socket(Socket socket, int sendBufferSize, int recvBufferSize)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _isConnected = false;
        _isDeleted = false;

        // Allocate Buffers
        _readBuffer = new byte[recvBufferSize];
        _writeBuffer = new byte[sendBufferSize];

#if CONFIG_USE_IOCP
        _writeLock = 0;
#else
        _writeLock = 0;
#endif
    }

    public Socket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
    {
        _socket = new Socket(addressFamily, socketType, protocolType);
    }

    public Socket()
    {
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

    private void Connect(IPEndPoint clientEndPoint)
    {
        _socket.Connect(clientEndPoint);
        OnConnect();
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
                bool bytesSent = _socket.Send(bytes);
                return bytesSent.CompareTo(bytes.Length) == bytes.Length;
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

    public bool BurstSend(byte[] bytes, int v)
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

    public void BurstEnd()
    {
        Monitor.Exit(_writeMutex);
    }

    public static string GetRemoteIP()
    {
        return _clientEndPoint?.Address.ToString() ?? "noip";
    }

    public static int GetRemotePort()
    {
        return _clientEndPoint?.Port ?? 0;
    }

    public static Socket GetSocket()
    {
        return _socket;
    }

    public void Disconnect()
    {
        if (!_isConnected) return;

        _isConnected = false;
        Close();

        // Call virtual ondisconnect
        OnDisconnect();

        if (!_isDeleted) Delete();
    }

    public void Delete()
    {
        if (_isDeleted) return;
        _isDeleted = true;

        if (_isConnected) Disconnect();
        // Queue the socket for garbage collection
        SocketGarbageCollector.Instance.QueueSocket(this);
    }

    public bool IsDeleted()
    {
        return _isDeleted;
    }

    public bool IsConnected()
    {
        return _isConnected;
    }

    public static IPEndPoint GetRemoteStruct()
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

    public static IPAddress GetRemoteAddress()
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

#if CONFIG_USE_IOCP
    // Simulated SocketManager class
    public static class SocketManager
    {
        private static IntPtr _completionPort = IntPtr.Zero;

        public static IntPtr GetCompletionPort()
        {
            if (_completionPort == IntPtr.Zero)
            {
                // Create a new IOCP completion port if it doesn't already exist
                _completionPort = CreateIoCompletionPort();

                if (_completionPort == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create IOCP completion port.");
                }
            }

            return _completionPort;
        }

        private static IntPtr CreateIoCompletionPort()
        {
            // Use ThreadPool to simulate IOCP behavior
            var completionPort = new ThreadPoolCompletionPort();
            return completionPort.Handle;
        }
        public static void SetCompletionPort(IntPtr completionPort, bool isSocketValid)
        {

            if (completionPort == IntPtr.Zero)
            {
                CLog.Error("[Socket]", "Invalid completion port.");
                return;
            }

            // Ensure the socket handle is valid

            if (!isSocketValid)
            {
                CLog.Error("[Socket]", "Invalid socket handle.");
                return;
            }

            // Associate the socket with the completion port
            SocketMgr.AssociateSocketWithCompletionPort(isSocketValid, completionPort);
        }
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
    internal static void Accept(IPEndPoint remoteEndPoint)
    {
        if (remoteEndPoint == null)
        {
            CLog.Error("[SOCKET]", "Remote endpoint is null.");
            return;
        }

        // Perform any necessary initialization for the accepted connection
        CLog.Notice("[SOCKET]", $"Accepted connection from {remoteEndPoint.Address}:{remoteEndPoint.Port}");

        // Example: Set up the socket for further communication
        _socket.Blocking = false;
        _socket.NoDelay = true;

        // Additional setup logic can be added here
    }

    private static void Listen(IPEndPoint address)
    {
        Listen(address);
    }

    internal static void Bind(IPEndPoint address)
    {
        Bind(address);
    }

    internal static void Close()
    {
        Close();
    }

    internal Socket Accept(int minPort)
    {
        Listen(minPort);
        var acceptedSocket = _socket.Accept();
        return new Socket(acceptedSocket, _writeBuffer.Length, _readBuffer.Length);
    }

    internal Socket Accept(IPAddress any, ref int len, int minPort, int maxPort)
    {
        Listen(1);
        var acceptedSocket = _socket.Accept();
        return new Socket(acceptedSocket, _writeBuffer.Length, _readBuffer.Length);
    }

    internal static void SetSocketOption(SocketOptionLevel socket, SocketOptionName reuseAddress, bool v)
    {
        SetSocketOption(socket, reuseAddress, v);
    }

    internal static void Listen(int v)
    {
        Listen(v);
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

    public AddressFamily AddressFamily { get; }
    public SocketType SocketType { get; }
    public ProtocolType ProtocolType { get; }
    public bool Blocking { get; internal set; }
    public bool NoDelay { get; private set; }
    public int Handle { get; internal set; }
    public int Available { get; internal set; }
    public string RemoteEndPoint { get; set; }
#endif

#if CONFIG_USE_EPOLL
    // Posts a epoll event with the specified arguments.
    public void PostEvent(uint events)
    {
        // Implementation for posting epoll event
    }

    // Atomic wrapper functions for increasing read/write locks
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

    private volatile int _writeLock;
#endif

#if CONFIG_USE_KQUEUE
    // Posts a kqueue event with the specified arguments.
    public void PostEvent(int events, bool oneshot)
    {
        // Implementation for posting kqueue event
    }

    // Atomic wrapper functions for increasing read/write locks
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

    private volatile int _writeLock;
#endif

    /** Connect to a server.
     * @param hostname Hostname or IP address to connect to
     * @param port Port to connect to
     * @return T if successful, otherwise null
     */
    public static T ConnectTCPSocket<T>(string hostname, ushort port) where T : Socket, new()
    {
        try
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(hostname);
            IPAddress[] addresses = hostEntry.AddressList;

            if (addresses.Length == 0)
                return null;

            IPEndPoint conn = new(addresses[0], port);

            T socket = new();
            if (!socket.Connect(hostname, port))
            {
                socket.Delete();
                return null;
            }
            return socket;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return null;
        }
    }

    public static int Receive(byte[] bytes, int space, SocketFlags none)
    {
        try
        {
            return Receive(bytes, space, none);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receive failed: {ex.Message}");
            return 0;
        }
    }

    internal int Send(byte[] bytes, int size, SocketFlags flags)
    {
        lock (_writeMutex)
        {
            try
            {
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

    internal static int EndReceive(IAsyncResult result)
    {
        try
        {
            return EndReceive(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EndReceive failed: {ex.Message}");
            return 0;
        }
    }

    internal static void BeginReceive(byte[] bytes, int v1, int v2, SocketFlags none, AsyncCallback asyncCallback, Socket socket)
    {
        try
        {
            BeginReceive(bytes, v1, v2, none, asyncCallback, socket);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BeginReceive failed: {ex.Message}");
        }
    }

    internal static void Select(HashSet<Socket> m_readableSet, HashSet<Socket> writable, HashSet<Socket> m_exceptionSet, int v)
    {
        try
        {
            Socket.Select(m_readableSet, writable, m_exceptionSet, v);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Select failed: {ex.Message}");
        }
    }

    internal static bool Poll(int v, SelectMode selectWrite)
    {
        try
        {
            return Poll(v, selectWrite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Poll failed: {ex.Message}");
            return false;
        }
    }

    internal static object EndAccept(IAsyncResult result)
    {
        try
        {
            return EndAccept(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EndAccept failed: {ex.Message}");
            return null;
        }
    }

    internal static void BeginAccept(AsyncCallback asyncCallback, Socket s)
    {
        try
        {
            BeginAccept(asyncCallback, s);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BeginAccept failed: {ex.Message}");
        }
    }

    internal static void BeginSend(byte[] bytes, int v1, int v2, SocketFlags none, AsyncCallback asyncCallback, Socket s)
    {
        try
        {
            BeginSend(bytes, v1, v2, none, asyncCallback, s);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BeginSend failed: {ex.Message}");
        }
    }

    internal static void Select(List<Socket> sockets, object value1, object value2, int v)
    {
        try
        {
            Socket.Select(sockets, value1, value2, v);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Select failed: {ex.Message}");
        }
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
}

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
