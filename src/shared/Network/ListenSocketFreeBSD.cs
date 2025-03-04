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

#if CONFIG_USE_KQUEUE
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace WaadShared.Network;

public abstract class ListenSocketBase
{
    public abstract void OnAccept();
    public abstract int GetFd();
}

public class ListenSocket<T> : ListenSocketBase where T : new()
{
    private readonly Socket m_socket;
    private Socket aSocket;
    private readonly IPEndPoint m_address;
    private readonly IPEndPoint m_tempAddress;
    private bool m_opened;
    private readonly int len;
    private T dsocket;

    public ListenSocket(string ListenAddress, uint Port)
    {
        m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        m_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        m_socket.Blocking = false;

        m_address = new IPEndPoint(IPAddress.Any, (int)Port);
        m_opened = false;

        if (ListenAddress != "0.0.0.0")
        {
            IPHostEntry hostname = Dns.GetHostEntry(ListenAddress);
            if (hostname != null)
                m_address.Address = hostname.AddressList[0];
        }

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

        len = Marshal.SizeOf(typeof(IPEndPoint));
        m_opened = true;
        sSocketMgr.AddListenSocket(this);
    }

    ~ListenSocket()
    {
        if (m_opened)
            Close();
    }

    public override void OnAccept()
    {
        aSocket = m_socket.Accept();
        if (aSocket == null)
            return;

        dsocket = new T();
        ((dynamic)dsocket).Accept(m_tempAddress);
    }

    public void Close()
    {
        if (m_opened)
            m_socket.Close();
        m_opened = false;
    }

    public bool IsOpen() => m_opened;
    public override int GetFd() => (int)m_socket.Handle;
}
#endif