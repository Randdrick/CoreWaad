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
using System.Net.Sockets;
using System.Threading;

namespace WaadShared.Network;

public class SocketManager
{
    private static readonly SocketManager instance = new();
    private readonly HashSet<Socket> m_allSet = [];
    private HashSet<Socket> m_readableSet = [];
    private readonly HashSet<Socket> m_writableSet = [];
    private readonly HashSet<Socket> m_exceptionSet = [];
    private readonly ConcurrentDictionary<int, Socket> fds = new();
    private int socket_count = 0;
    private readonly object m_setLock = new();
    private readonly object _socketLock = new();

    private readonly ConcurrentBag<Socket> _sockets = [];

    private readonly HashSet<Socket> writable = [];

    public SocketManager()
    {
    }

#if CONFIG_USE_IOCP
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
#endif

    public static SocketManager Instance
    {
        get { return instance; }
    }

#if CONFIG_USE_IOCP
    public void AddSocket(Socket s)
    {
        if (socket_count >= 64 || fds.ContainsKey(s.Handle))
        {
            s.Delete();
            return;
        }

        lock (m_setLock)
        {
            m_allSet.Add(s);
            fds[s.Handle] = s;
            socket_count++;
        }
    }

    public void RemoveSocket(Socket s)
    {
        if (!fds.TryRemove(s.Handle, out _))
            return;

        lock (m_setLock)
        {
            m_allSet.Remove(s);
            socket_count--;
        }
    }
#endif
    public void CloseAll()
    {
        List<SocketManager> toKill = [];

        lock (_socketLock)
        {
            toKill.AddRange((IEnumerable<SocketManager>)_sockets);
        }

        foreach (SocketManager socket in toKill)
        {
            Close();
        }

        while (true)
        {
            lock (_socketLock)
            {
                if (_sockets.IsEmpty) break;
            }
        }
    }

    public void WantWrite(int fd)
    {
        lock (m_setLock)
        {
            m_writableSet.Add(fds[fd]);
        }
    }

    public void ThreadFunction()
    {
        while (true)
        {
            lock (m_setLock)
            {
                if (socket_count == 0)
                {
                    Thread.Sleep(50);
                    continue;
                }

                m_readableSet = [.. m_allSet];
                var writable = new HashSet<Socket>(m_writableSet);
                m_writableSet.Clear();
            }

            Socket.Select(m_readableSet, writable, m_exceptionSet, 20000);

            foreach (var s in fds.Values)
            {
                if (m_readableSet.Contains(s))
                {
                    s.ReadCallback(null);
                }

                if (writable.Contains(s))
                {
                    s.BurstBegin();
                    s.WriteCallback();
                    if (s.GetWriteBufferSize() > 0)
                    {
                        lock (m_setLock)
                        {
                            m_writableSet.Add(s);
                        }
                    }
                    else
                    {
                        s.DecSendLock();
                    }
                    s.BurstEnd();
                }

                if (m_exceptionSet.Contains(s))
                {
                    SocketExtensions.Disconnect();
                }
            }

            m_exceptionSet.Clear();
        }
    }

    public static void Close()
    {
        SocketExtensions.Disconnect();
    }

    public void SpawnWorkerThreads()
    {
        int tc = 1;
        for (int i = 0; i < tc; ++i)
        {
            var thread = new Thread(new ThreadStart(ThreadFunction));
            thread.Start();
        }
    }

    public void ShutdownThreads()
    {
        // Implementation for shutting down threads
    }
}

// Extension methods for Socket to mimic the original C++ methods
public static class SocketExtensions
{
    private static readonly object _readMutex = new();
    private static readonly object _writeMutex = new();
    private static readonly CircularBuffer _readBuffer = new();
    private static readonly CircularBuffer _writeBuffer = new();
    private static readonly BufferPool _bufferPool = new();
    private static bool _deleted;
    private static bool _connected = true;
    private static int _sendLock;

    static SocketExtensions()
    {
        _readBuffer.Allocate(8192);
        _writeBuffer.Allocate(8192);
        _bufferPool.Init();
    }

    public static void ReadCallback(this Socket socket, IAsyncResult ar)
    {
        try
        {
            int bytesReceived = socket.GetFd().EndReceive(ar);
            if (bytesReceived > 0)
            {
                socket.GetReadBuffer().IncrementWritten(bytesReceived);
            }
            socket.OnRead();
            socket.SetupReadEvent();
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode != SocketError.WouldBlock)
            {
                socket.Disconnect();
            }
        }
    }

    public static void OnRead(Socket socket, int len)
    {
        if (len == 0)
        {
            Disconnect();
            return;
        }

        // Data is already written to _readBuffer in ReadCallback
        OnRecvData(socket);

        if (!_connected)
            return;

        // Only setup read event if not already pending
        if (!_pendingRead.ContainsKey(socket))
            socket.SetupReadEvent();
    }

    public static void OnRecvData(Socket socket)
    {
        int available = _readBuffer.GetContiguousBytes();
        if (available > 0)
        {
            byte[] data = new byte[available];
            _readBuffer.Read(data, available);
        }

        while (true)
        {
            // Un paquet typique commence par un header (ex: 6 octets : 2 pour opcode, 4 pour taille)
            const int headerSize = 6;
            if (available < headerSize)
                break;

            byte[] header = new byte[headerSize];
            _readBuffer.Read(header, headerSize);

            ushort opcode = BitConverter.ToUInt16(header, 0);
            uint size = BitConverter.ToUInt32(header, 2);

            // Vérification de la taille du paquet
            if (size > 65535)
            {
                Console.WriteLine("Packet size exceeds maximum allowed size.");
                socket.Disconnect();
                break;
            }

            // Vérifie si tout le paquet est disponible dans le buffer
            if (_readBuffer.GetContiguousBytes() < size)
            {
                break;
            }

            byte[] payload = new byte[size];
            _readBuffer.Read(payload, (int)size);

            // Construction du paquet et traitement
            WorldPacket packet = new(opcode, (int)size);
            if (size > 0)
            {
                packet.Resize((int)size);
                Buffer.BlockCopy(payload, 0, packet.Contents, 0, (int)size);
            }
        }
    }

    private static readonly ConcurrentDictionary<Socket, bool> _pendingRead = new();

    public static void SetupReadEvent(this Socket socket)
    {
        if (socket.IsDeleted() || !socket.IsConnected())
            return;

        Monitor.Enter(_readMutex);
        try
        {
            int space = _readBuffer.GetSpace();
            if (space <= 0)
            {
                CLog.Warning("[Socket]", "Read buffer space exhausted, reallocating.");
                _readBuffer.Allocate(_readBuffer.GetSize() + 8192);
                space = _readBuffer.GetSpace();
            }

#if CONFIG_USE_IOCP

            byte[] temp = new byte[space];
            try
            {
                var fd = socket.GetFd();
                if (fd == null)
                {
                    CLog.Error("[SocketMgr]", $"SetupReadEvent: socket.GetFd() returned null. Socket instance: {socket}");
                    CLog.Debug("[SocketMgr]", $"Socket state: IsDeleted={socket.IsDeleted()}, IsConnected={socket.IsConnected()}");
                    socket.Disconnect();
                    return;
                }
                // Check if the socket is disposed, not connected, or blocking
                if (fd.Blocking)
                {
                    CLog.Debug("[SocketMgr]", $"SetupReadEvent: socket.GetFd() is in blocking mode. Switching to non-blocking.");
                    try
                    {
                        fd.Blocking = false;
                    }
                    catch (Exception ex)
                    {
                        CLog.Error("[SocketMgr]", $"Failed to set socket to non-blocking: {ex.Message}");
                        socket.Disconnect();
                        return;
                    }
                }

                if (!fd.Connected)
                {
                    // Diagnostic: log more details about the socket and endpoint
                    string localEp = fd.LocalEndPoint == null ? "Socket LocalEndPoint is null. The socket may never have been bound." : fd.LocalEndPoint.ToString();
                    string remoteEp = fd.RemoteEndPoint == null ? "Socket RemoteEndPoint is null. The socket may never have connected." : fd.RemoteEndPoint.ToString();
                    CLog.Error("[SocketMgr]", $"SetupReadEvent: socket.GetFd() is not connected. Socket instance: {socket}");
                    CLog.Debug("[SocketMgr]", $"Socket local endpoint: {localEp}");
                    CLog.Debug("[SocketMgr]", $"Socket remote endpoint: {remoteEp}");
                    CLog.Debug("[SocketMgr]", $"Socket state: IsDeleted={socket.IsDeleted()}, IsConnected={socket.IsConnected()}, Blocking={fd.Blocking}");
                    try
                    {
                        bool canWrite = fd.Poll(0, SelectMode.SelectWrite);
                        bool hasError = fd.Poll(0, SelectMode.SelectError);
                        CLog.Debug("[SocketMgr]", $"Poll(SelectWrite)={canWrite}, Poll(SelectError)={hasError}");

                        // Suggestion: check if Accept() or Connect() was successful before using the socket
                        if (socket is { })
                        {
                            // For server sockets: after Accept(), check .Connected and endpoints
                            if (fd.LocalEndPoint == null || fd.RemoteEndPoint == null || !fd.Connected)
                            {
                                CLog.Error("[SocketMgr]", "Accept() did not return a valid, connected socket. Check server accept logic.");
                            }
                        }
                        // For client sockets: after Connect(), check .Connected and endpoints
                        if (!fd.Connected)
                        {
                            CLog.Error("[SocketMgr]", "Connect() did not succeed or remote server is unreachable. Check client connect logic.");
                        }
                    }
                    catch (Exception ex)
                    {
                        CLog.Error("[SocketMgr]", $"Exception during socket.Poll: {ex.Message}");
                    }
                    socket.Disconnect();
                    return;
                }

                fd.BeginReceive(temp, 0, space, SocketFlags.None, ar =>
                {
                    int bytesReceived = 0;
                    try
                    {
                        bytesReceived = fd.EndReceive(ar);
                        if (bytesReceived > 0)
                        {
                            lock (_readMutex)
                            {
                                _readBuffer.Write(temp, bytesReceived);
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.WouldBlock)
                        {
                            CLog.Error("[SocketMgr]", $"SetupReadEvent SocketException on BeginReceive: {ex.Message} (Code: {ex.SocketErrorCode})");
                            socket.Disconnect();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        CLog.Error("[SocketMgr]", "SetupReadEvent: Socket has been disposed.");
                        socket.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ReadCallback error: {ex.Message}");
                    }
                    finally
                    {
                        _pendingRead.TryRemove(socket, out _);
                    }
                    OnRead(socket, bytesReceived);
                    if (socket.IsConnected() && bytesReceived > 0)
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            if (!_pendingRead.ContainsKey(socket))
                                socket.SetupReadEvent();
                        });
                    }
                }, socket);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    CLog.Error("[SocketMgr]", $"SetupReadEvent SocketException: {ex.Message} (Code: {ex.SocketErrorCode})");
                    socket.Disconnect();
                }
            }
            catch (ObjectDisposedException)
            {
                CLog.Error("[SocketMgr]", "SetupReadEvent: Socket has been disposed.");
                socket.Disconnect();
            }
#else
            // Non-IOCP fallback (not used on Windows)
            byte[] temp = new byte[space];
            try
            {
                socket.GetFd().BeginReceive(temp, 0, space, SocketFlags.None, ar =>
                {
                    int bytesReceived = 0;
                    try
                    {
                        bytesReceived = socket.GetFd().EndReceive(ar);
                        if (bytesReceived > 0)
                        {
                            lock (_readMutex)
                            {
                                _readBuffer.Write(temp, bytesReceived);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ReadCallback error: {ex.Message}");
                    }
                    finally
                    {
                        _pendingRead.TryRemove(socket, out _);
                    }
                    OnRead(socket, bytesReceived);
                    if (socket.IsConnected() && bytesReceived > 0)
                    {
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            if (!_pendingRead.ContainsKey(socket))
                                socket.SetupReadEvent();
                        });
                    }
                }, socket);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                {
                    CLog.Error("[SocketMgr]", $"No IOCP SetupReadEvent SocketException: {ex.Message} (Code: {ex.SocketErrorCode})");
                    socket.Disconnect();
                }
            }
#endif
        }
        finally
        {
            Monitor.Exit(_readMutex);
        }
    }

    public static void WriteCallback(this Socket socket)
    {
        if (_deleted || !_connected)
            return;

        lock (_writeMutex)
        {
            int toSend = _writeBuffer.GetContiguousBytes();
            if (toSend > 0)
            {
                try
                {
                    byte[] sendBuf = new byte[toSend];
                    _writeBuffer.Read(sendBuf, toSend);
                    int bytesSent = socket.Send(sendBuf, 0, (SocketFlags)toSend);
                    if (bytesSent < toSend)
                    {
                        int unsent = toSend - bytesSent;
                        if (unsent > 0)
                        {
                            byte[] unsentData = new byte[unsent];
                            Array.Copy(sendBuf, bytesSent, unsentData, 0, unsent);

                            int currentAvailable = _writeBuffer.GetContiguousBytes();
                            byte[] temp = new byte[currentAvailable];

                            _writeBuffer.Read(temp, currentAvailable);
                            _writeBuffer.Remove(_writeBuffer.GetContiguousBytes());
                            _writeBuffer.Write(unsentData, unsent);

                            if (currentAvailable > 0)
                                _writeBuffer.Write(temp, currentAvailable);
                        }
                    }
                    if (_writeBuffer.GetContiguousBytes() == 0)
                    {
                        DecSendLock(socket);
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.WouldBlock)
                    {
                        DecSendLock(socket);
                        Disconnect();
                    }
                }
            }
            else
            {
                DecSendLock(socket);
            }
        }
    }

    public static int GetWriteBufferSize(this Socket socket)
    {
        return _writeBuffer.GetContiguousBytes();
    }

    public static void BurstBegin(this Socket socket)
    {
        if (AcqSendLock())
        {
            WriteCallback(socket);
        }
    }

    public static bool AcqSendLock()
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

    public static void BurstEnd(this Socket socket)
    {
        // Implementation for BurstEnd
    }

    public static void DecSendLock(this Socket socket)
    {
        lock (_writeMutex)
        {
            _sendLock = 0;
        }
    }
    public static void Close()
    {
        if (_deleted)
            return;

        _deleted = true;
    }

    public static void Disconnect()
    {
        if (!_connected)
            return;

        _connected = false;
    }
}
