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
        int key = s.GetFd().Handle.GetHashCode();
        if (socket_count >= 1024)
        {
            CLog.Warning("[SocketMgr]", $"AddSocket: socket limit reached ({socket_count}), refusing new connection from {s.GetRemoteIP()}");
            s.Delete();
            return;
        }
        if (fds.ContainsKey(key))
        {
            // Handle collision (socket handle reuse by OS after close)
            CLog.Warning("[SocketMgr]", $"AddSocket: handle key collision for {s.GetRemoteIP()}, replacing stale entry.");
        }

        lock (m_setLock)
        {
            m_allSet.Add(s);
            fds[key] = s;
            socket_count++;
        }
    }

    public void RemoveSocket(Socket s)
    {
        int key = s.GetFd().Handle.GetHashCode();
        if (!fds.TryRemove(key, out _))
            return;

        lock (m_setLock)
        {
            m_allSet.Remove(s);
            socket_count--;
        }
    }

    // True IOCP completion queue: BeginReceive callbacks enqueue here,
    // worker thread blocks on TryTake → 0% CPU when idle
    public enum SocketIOEvent { ReadComplete, WriteComplete, Shutdown }
    internal static readonly BlockingCollection<(SocketIOEvent Event, Socket Socket, uint BytesTransferred)>
        IOCompletionQueue = new(boundedCapacity: 10000);

    public static int IOQueueDepth => IOCompletionQueue.Count;
#endif
    public void CloseAll()
    {
        List<Socket> toKill;

        lock (m_setLock)
        {
            toKill = [.. fds.Values];
        }

        foreach (Socket socket in toKill)
        {
            try
            {
                socket.Disconnect();
            }
            catch { }
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
            HashSet<Socket> writable;

            lock (m_setLock)
            {
                if (socket_count == 0)
                {
                    Thread.Sleep(50);
                    continue;
                }

                m_readableSet = [.. m_allSet];
                writable = [.. m_writableSet];
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
                    s.Disconnect();
                }
            }

            m_exceptionSet.Clear();
        }
    }

    public static void Close()
    {
        Instance.CloseAll();
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

    public static void ShutdownThreads()
    {
        // Implementation for shutting down threads
    }
}

// Extension methods for Socket to mimic the original C++ methods
public static class SocketExtensions
{
    private static readonly object _readMutex = new();

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
            socket.Disconnect();
            return;
        }

        // Call the virtual OnRead method on the socket instance
        // This allows subclasses (like LogonCommServerSocket) to handle their own packet reading
        socket.OnRead();

        if (!socket.IsConnected() || socket.IsDeleted())
            return;

        // Only setup read event if not already pending
        if (!_pendingRead.ContainsKey(socket))
            socket.SetupReadEvent();
    }

    public static void OnRecvData(Socket socket)
    {
        var readBuffer = socket.GetReadBuffer();
        int available = readBuffer.GetContiguousBytes();
        if (available > 0)
        {
            byte[] data = new byte[available];
            readBuffer.Read(data, available);
        }

        while (true)
        {
            // Un paquet typique commence par un header (ex: 6 octets : 2 pour opcode, 4 pour taille)
            const int headerSize = 6;
            if (available < headerSize)
                break;

            byte[] header = new byte[headerSize];
            readBuffer.Read(header, headerSize);

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
            if (readBuffer.GetContiguousBytes() < size)
            {
                break;
            }

            byte[] payload = new byte[size];
            readBuffer.Read(payload, (int)size);

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

        // Ensure only one async receive is pending per socket.
        if (!_pendingRead.TryAdd(socket, true))
            return;

        // Do NOT use the global _readMutex here – it serialises BeginReceive for all
        // sockets on the same lock, which causes hundreds-of-ms latency on loopback.
        // _pendingRead.TryAdd already guarantees one pending read per socket.
        bool beginReceivePosted = false;
        try
        {
            int space = socket.GetReadBuffer().GetSpace();
            if (space <= 0)
            {
                CLog.Warning("[Socket]", "Read buffer space exhausted, reallocating.");
                var socketReadBuffer = socket.GetReadBuffer();
                socketReadBuffer.Allocate(socketReadBuffer.GetSize() + 8192);
                space = socketReadBuffer.GetSpace();
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
                    bool disconnectSocket = false;
                    bool rearmRead = false;

                    try
                    {
                        bytesReceived = fd.EndReceive(ar);
                        if (bytesReceived > 0)
                        {
                            // Use per-socket read buffer as lock target – avoids the global
                            // _readMutex that would serialise reads across all sockets.
                            var buf = socket.GetReadBuffer();
                            lock (buf)
                            {
                                buf.Write(temp, bytesReceived);
                            }
                        }
                        else
                        {
                            // 0 bytes = remote closed connection gracefully
                            disconnectSocket = true;
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode == SocketError.WouldBlock)
                        {
                            // No data ready (rare on IOCP but possible).
                            // Re-arm the read so the socket continues receiving.
                            rearmRead = true;
                        }
                        else if (ex.SocketErrorCode == SocketError.OperationAborted ||
                                 ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                 ex.SocketErrorCode == SocketError.ConnectionReset ||
                                 ex.SocketErrorCode == SocketError.Interrupted ||
                                 ex.SocketErrorCode == SocketError.Shutdown)
                        {
                            if (socket.IsConnected() && !socket.IsDeleted())
                                CLog.Warning("[SocketMgr]", $"SetupReadEvent receive aborted on active socket: {ex.Message} (Code: {ex.SocketErrorCode})");
                            disconnectSocket = true;
                        }
                        else
                        {
                            if (!socket.IsDeleted())
                                CLog.Error("[SocketMgr]", $"SetupReadEvent SocketException on BeginReceive: {ex.Message} (Code: {ex.SocketErrorCode})");
                            disconnectSocket = true;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        CLog.Error("[SocketMgr]", "SetupReadEvent: Socket has been disposed.");
                        disconnectSocket = true;
                    }
                    catch (Exception ex)
                    {
                        CLog.Error("[SocketMgr]", $"ReadCallback unhandled error: {ex.Message}");
                        disconnectSocket = true;
                    }
                    finally
                    {
                        _pendingRead.TryRemove(socket, out _);
                    }

                    // All post-receive actions are explicit here – no silent early returns above.
                    if (disconnectSocket)
                    {
                        socket.Disconnect();
                    }
                    else if (rearmRead)
                    {
                        // WouldBlock: re-arm the read directly without dispatching to the worker.
                        System.Threading.ThreadPool.QueueUserWorkItem(_ => socket.SetupReadEvent());
                    }
                    else if (bytesReceived > 0)
                    {
                        // Dispatch to IOCP worker thread via blocking queue (true async pattern).
                        // Worker thread calls socket.OnRead() + SetupReadEvent() for next receive.
                        if (!SocketManager.IOCompletionQueue.TryAdd((SocketManager.SocketIOEvent.ReadComplete, socket, (uint)bytesReceived)))
                        {
                            CLog.Error("[SocketMgr]", "IOCompletionQueue is full; disconnecting socket to avoid stalled receive loop.");
                            socket.Disconnect();
                        }
                    }
                    else
                    {
                        // Should not reach here (bytesReceived==0 sets disconnectSocket), but guard anyway.
                        socket.Disconnect();
                    }
                }, socket);
                beginReceivePosted = true;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    return;
                }

                if (ex.SocketErrorCode == SocketError.OperationAborted ||
                    ex.SocketErrorCode == SocketError.ConnectionAborted ||
                    ex.SocketErrorCode == SocketError.ConnectionReset ||
                    ex.SocketErrorCode == SocketError.Interrupted ||
                    ex.SocketErrorCode == SocketError.Shutdown)
                {
                    if (socket.IsConnected() && !socket.IsDeleted())
                    {
                        CLog.Warning("[SocketMgr]", $"SetupReadEvent aborted on active socket: {ex.Message} (Code: {ex.SocketErrorCode})");
                        socket.Disconnect();
                    }
                    return;
                }

                if (!socket.IsDeleted())
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
                            var buf = socket.GetReadBuffer();
                            lock (buf)
                            {
                                buf.Write(temp, bytesReceived);
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
                beginReceivePosted = true;
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
            if (!beginReceivePosted)
            {
                _pendingRead.TryRemove(socket, out _);
            }
        }
    }

    public static void WriteCallback(this Socket socket)
    {
        if (socket.IsDeleted() || !socket.IsConnected())
            return;

        var writeBuffer = socket.GetWriteBuffer();
        lock (writeBuffer)
        {
            int toSend = writeBuffer.GetContiguousBytes();
            if (toSend > 0)
            {
                try
                {
                    byte[] sendBuf = new byte[toSend];
                    writeBuffer.Read(sendBuf, toSend);
                    int bytesSent = socket.Send(sendBuf, toSend, SocketFlags.None);
                    if (bytesSent < toSend)
                    {
                        int unsent = toSend - bytesSent;
                        if (unsent > 0)
                        {
                            byte[] unsentData = new byte[unsent];
                            Array.Copy(sendBuf, bytesSent, unsentData, 0, unsent);

                            int currentAvailable = writeBuffer.GetContiguousBytes();
                            byte[] temp = new byte[currentAvailable];

                            writeBuffer.Read(temp, currentAvailable);
                            writeBuffer.Remove(writeBuffer.GetContiguousBytes());
                            writeBuffer.Write(unsentData, unsent);

                            if (currentAvailable > 0)
                                writeBuffer.Write(temp, currentAvailable);
                        }
                    }
                    if (writeBuffer.GetContiguousBytes() == 0)
                    {
                        socket.DecSendLock();
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.WouldBlock)
                    {
                        socket.DecSendLock();
                        socket.Disconnect();
                    }
                }
            }
            else
            {
                socket.DecSendLock();
            }
        }
    }

    public static int GetWriteBufferSize(this Socket socket)
    {
        return socket.GetWriteBuffer().GetContiguousBytes();
    }

    public static void BurstBegin(this Socket socket)
    {
        if (socket.AcquireSendLock())
        {
            WriteCallback(socket);
        }
    }

    public static void BurstEnd(this Socket socket)
    {
        // Implementation for BurstEnd
    }

}
