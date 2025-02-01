/*
 * Ascent MMORPG Server
 * Copyright (C) 2005-2008 Ascent Team <http://www.ascentcommunity.com/>
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

#define _LISTENSOCKET_H
using System;

namespace waad_shared.Network
{
#if CONFIG_USE_EPOLL

    using System;
    using System.Net;
    using System.Net.Sockets;

    public abstract class ListenSocketBase
    {
        public abstract void OnAccept();
        public abstract int GetFd();
    }

    public class ListenSocket<T> : ListenSocketBase where T : class
    {
        private Socket m_socket;
        private Socket aSocket;
        private IPEndPoint m_address;
        private IPEndPoint m_tempAddress;
        private bool m_opened;
        private int len;
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

            len = m_address.Serialize().Size;
            m_opened = true;
            sSocketMgr.AddListenSocket(this);
        }

        ~ListenSocket()
        {
            if (m_opened)
                m_socket.Close();
        }

        public void Close()
        {
            if (m_opened)
                m_socket.Close();
            m_opened = false;
        }

        public override void OnAccept()
        {
            aSocket = m_socket.Accept();
            if (aSocket == null)
                return;

            dsocket = Activator.CreateInstance(typeof(T), aSocket) as T;
            (dsocket as dynamic).Accept(m_tempAddress);
        }

        public bool IsOpen() { return m_opened; }
        public override int GetFd() { return (int)m_socket.Handle; }
    }

#endif

}
