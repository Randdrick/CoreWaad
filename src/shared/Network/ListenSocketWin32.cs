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
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace WaadShared;

public class ListenSocket<T> : Threading.ThreadBase where T : new()
{
    private readonly Socket m_socket;
    private Socket aSocket;
    private readonly IPEndPoint m_address;
    private readonly IPEndPoint m_tempAddress;
    private bool m_opened;
    private readonly int len;
    private T socket;
    private readonly IntPtr m_cp;

    public ListenSocket(string ListenAddress, uint Port)
    {
        m_socket = SocketOps.CreateTCPFileDescriptor();
        SocketOps.ReuseAddr(m_socket);
        SocketOps.Blocking(m_socket);

        m_address = new IPEndPoint(IPAddress.Any, (int)Port);
        m_tempAddress = new IPEndPoint(IPAddress.Any, (int)Port);
        m_opened = false;

        if (ListenAddress != "0.0.0.0")
        {
            IPHostEntry hostname = Dns.GetHostEntry(ListenAddress);
            if (hostname != null)
                m_address.Address = hostname.AddressList[0];
        }

        // bind.. well attempt to.
        try
        {
            m_socket.Bind(m_address);
        }
        catch (SocketException)
        {
            Console.WriteLine($"Bind unsuccessful on port {Port}.");
            return;
        }

        try
        {
            m_socket.Listen(5);
        }
        catch (SocketException)
        {
            Console.WriteLine($"Unable to listen on port {Port}.");
            return;
        }

        m_opened = true;
        len = Marshal.SizeOf(typeof(IPEndPoint));
        m_cp = SSocketMgr.GetCompletionPort();
    }

    ~ListenSocket()
    {
        Close();
    }

    public bool Run()
    {
        while (m_opened)
        {
            aSocket = m_socket.Accept();
            if (aSocket == null)
                continue; // shouldn't happen, we are blocking.

            socket = new T();
            ((dynamic)socket).SetCompletionPort(m_cp);
            ((dynamic)socket).Accept(m_tempAddress);
        }
        return false;
    }

    public void Close()
    {
        // prevent a race condition here.
        if (m_opened)
        {
            m_opened = false;
            SocketOps.CloseSocket(m_socket);
        }
    }

    public bool IsOpen() { return m_opened; }

    public override bool Run(CancellationToken token)
    {
        while (m_opened && !token.IsCancellationRequested)
        {
            try
            {
                aSocket = m_socket.Accept();
                if (aSocket == null)
                    continue; // shouldn't happen, we are blocking.

                socket = new T();
                ((dynamic)socket).SetCompletionPort(m_cp);
                ((dynamic)socket).Accept(m_tempAddress);
            }
            catch (SocketException)
            {
                // Handle socket exceptions if necessary
                Console.WriteLine("Socket exception occurred.");
            }
            catch (ObjectDisposedException)
            {
                // Handle object disposed exceptions if necessary
                Console.WriteLine("Socket has been disposed.");
            }
        }
        return false;
    }
}

public static class SSocketMgr
{
    // public static object Instance;

    public static IntPtr GetCompletionPort()
    {
        // Implementation for getting the completion port
        return IntPtr.Zero;
    }
}
#endif
