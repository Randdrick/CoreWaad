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
    private readonly System.Net.Sockets.Socket m_fd;
    private bool m_connected;
    private bool m_deleted;
    private readonly object m_writeMutex = new();
    private readonly object m_readMutex = new();
    private IPEndPoint m_client;
    private readonly byte[] readBuffer;
    private readonly byte[] writeBuffer;
    private IntPtr m_completionPort; // Equivalent to HANDLE in C++
    private int m_writeLock;

    public Socket(System.Net.Sockets.Socket fd, int sendBufferSize, int recvBufferSize)
    {
        m_fd = fd ?? new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        m_connected = false;
        m_deleted = false;
        m_writeLock = 0;

        // Allocate Buffers
        readBuffer = new byte[recvBufferSize];
        writeBuffer = new byte[sendBufferSize];

        // Check for needed fd allocation.
        m_fd ??= new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
#if CONFIG_USE_IOCP
        // Simulate getting a completion port from a manager
        m_completionPort = SocketManager.GetCompletionPort();
#endif
    }

    public Socket(AddressFamily interNetwork, SocketType stream, ProtocolType tcp)
    {
        InterNetwork = interNetwork;
        Stream = stream;
        Tcp = tcp;
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

            m_client = new IPEndPoint(addresses[0], port);
            m_fd.Connect(m_client);

            OnConnect();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Accept(IPAddress any, IPEndPoint address)
    {
        m_client = address;
        OnConnect();
    }

    private void OnConnect()
    {
        m_fd.Blocking = false;
        m_fd.NoDelay = true;
        m_connected = true;

        // Call virtual onconnect
        OnConnect();
    }

    public bool Send(byte[] bytes)
    {
        lock (m_writeMutex)
        {
            try
            {
                return m_fd.Send(bytes) == bytes.Length;
            }
            catch
            {
                return false;
            }
        }
    }

    public void BurstBegin()
    {
        Monitor.Enter(m_writeMutex);
    }

    public bool BurstSend(byte[] bytes)
    {
        try
        {
            Buffer.BlockCopy(bytes, 0, writeBuffer, 0, bytes.Length);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void BurstPush()
    {
        // Implementation for pushing events to the queue
    }

    public void BurstEnd()
    {
        Monitor.Exit(m_writeMutex);
    }

    public string GetRemoteIP()
    {
        return m_client?.Address.ToString() ?? "noip";
    }

    public int GetRemotePort()
    {
        return m_client?.Port ?? 0;
    }

    public System.Net.Sockets.Socket GetFd()
    {
        return m_fd;
    }

    public void Disconnect()
    {
        if (!m_connected) return;

        m_connected = false;
        m_fd.Close();

        // Call virtual ondisconnect
        OnDisconnect();

        if (!m_deleted) Delete();
    }

    public void Delete()
    {
        if (m_deleted) return;
        m_deleted = true;

        if (m_connected) Disconnect();
        // Assuming there's a garbage collector mechanism in place
    }

    public bool IsDeleted()
    {
        return m_deleted;
    }

    public bool IsConnected()
    {
        return m_connected;
    }

    public IPEndPoint GetRemoteStruct()
    {
        return m_client;
    }

    public byte[] GetReadBuffer()
    {
        return readBuffer;
    }

    public byte[] GetWriteBuffer()
    {
        return writeBuffer;
    }

    public IPAddress GetRemoteAddress()
    {
        return m_client?.Address;
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
    public void SetupReadEvent()
    {
        // Implementation for setting up read events
    }

    public void ReadCallback(uint len)
    {
        // Implementation for read callback
    }

    public void WriteCallback()
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
        m_completionPort = cp;
    }

    public void IncSendLock()
    {
        Interlocked.Increment(ref m_writeLock);
    }

    public void DecSendLock()
    {
        Interlocked.Decrement(ref m_writeLock);
    }

    public bool AcquireSendLock()
    {
        if (m_writeLock > 0)
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

    internal void Bind(IPEndPoint m_address)
    {
        throw new NotImplementedException();
    }

    internal void Close()
    {
        throw new NotImplementedException();
    }

    internal Socket Accept(int minPort)
    {
        throw new NotImplementedException();
    }

    internal Socket Accept(IPAddress any, ref int len, int minPort, int maxPort)
    {
        throw new NotImplementedException();
    }

    internal void SetSocketOption(SocketOptionLevel socket, SocketOptionName reuseAddress, bool v)
    {
        throw new NotImplementedException();
    }

    internal void Listen(int v)
    {
        throw new NotImplementedException();
    }

    internal Socket Accept()
    {
        throw new NotImplementedException();
    }

    internal Socket Accept(object m)
    {
        throw new NotImplementedException();
    }

    // Placeholder for overlapped structures
    private class OverlappedStruct
    {
        // Implementation for overlapped structure
    }

    private OverlappedStruct m_readEvent;
    private OverlappedStruct m_writeEvent;

    public AddressFamily InterNetwork { get; }
    public SocketType Stream { get; }
    public ProtocolType Tcp { get; }
    public bool Blocking { get; internal set; }
#endif
}
