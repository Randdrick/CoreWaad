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

namespace WaadShared.Network
{
    public class Socket
    {
        private readonly Socket _socket;
        private bool _isConnected;
        private bool _isDeleted;
        private readonly object _writeMutex = new();
        private readonly object _readMutex = new();
        private IPEndPoint _clientEndPoint;
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
            throw new NotImplementedException();
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

        public Socket GetSocket()
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

        public static void SetCompletionPort(IntPtr cp)
        {
            // Removed _completionPort field
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

        public AddressFamily AddressFamily { get; }
        public SocketType SocketType { get; }
        public ProtocolType ProtocolType { get; }
        public bool Blocking { get; internal set; }
        public bool NoDelay { get; private set; }
        public int Handle { get; internal set; }
        public int Available { get; internal set; }
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

        internal int Receive(byte[] bytes, int space, SocketFlags none)
        {
            throw new NotImplementedException();
        }

        internal int Send(byte[] bytes, int v, SocketFlags none)
        {
            throw new NotImplementedException();
        }

        internal int EndReceive(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        internal void BeginReceive(byte[] bytes, int v1, int v2, SocketFlags none, AsyncCallback asyncCallback, Socket socket)
        {
            throw new NotImplementedException();
        }

        internal static void Select(HashSet<Socket> m_readableSet, HashSet<Socket> writable, HashSet<Socket> m_exceptionSet, int v)
        {
            throw new NotImplementedException();
        }

        internal bool Poll(int v, SelectMode selectWrite)
        {
            throw new NotImplementedException();
        }

        internal object EndAccept(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        internal void BeginAccept(AsyncCallback asyncCallback, Socket s)
        {
            throw new NotImplementedException();
        }

        internal void BeginSend(byte[] bytes, int v1, int v2, SocketFlags none, AsyncCallback asyncCallback, Socket s)
        {
            throw new NotImplementedException();
        }

        internal static void Select(List<Socket> sockets, object value1, object value2, int v)
        {
            throw new NotImplementedException();
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
}
