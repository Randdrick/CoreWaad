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

#if CONFIG_USE_EPOLL
using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;


namespace WaadShared
{
    public abstract class ListenSocketBase
    {
        public abstract void OnAccept();
        public abstract int GetFd();
    }

    public class ListenSocket<T> : ListenSocketBase where T : class
    {
        private readonly Socket m_socket;
        private Socket aSocket;
        private readonly IPEndPoint m_address;
        private EndPoint m_tempAddress;
        private bool m_opened;
        private readonly int len;
        private T dsocket;
        private readonly Func<Socket, T> _factory;

        public ListenSocket(string ListenAddress, uint Port, Func<Socket, T> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            SocketOps.Blocking(m_socket);

            // Initialisation de m_address
            if (ListenAddress == "0.0.0.0")
            {
                m_address = new IPEndPoint(IPAddress.Any, (int)Port);
            }
            else if (ListenAddress == "127.0.0.1")
            {
                m_address = new IPEndPoint(IPAddress.Loopback, (int)Port);
            }
            else
            {
                try
                {
                    IPHostEntry hostname = Dns.GetHostEntry(ListenAddress);
                    if (hostname != null && hostname.AddressList.Length > 0)
                    {
                        var ip = hostname.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                        if (ip == null)
                            throw new Exception($"No valid IPv4 address found for {ListenAddress}.");
                        m_address = new IPEndPoint(ip, (int)Port);
                    }
                    else
                    {
                        throw new Exception($"Failed to resolve ListenAddress '{ListenAddress}'.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to resolve ListenAddress '{ListenAddress}': {ex.Message}");
                    return;
                }
            }

            // Initialisation de m_tempAddress
            m_tempAddress = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                m_socket.Bind(m_address);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Bind unsuccessful on port {Port}: {ex.Message}");
                return;
            }

            try
            {
                m_socket.Listen(5);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Unable to listen on port {Port}: {ex.Message}");
                return;
            }

            len = m_address.Serialize().Size;
            m_opened = true;
            SocketMgr.AddListenSocket(m_socket);
        }

        ~ListenSocket()
        {
            if (m_opened)
                m_socket.Close();
        }

        public override void OnAccept()
        {
            aSocket = m_socket.Accept();
            if (aSocket == null)
                return;

            if (_factory != null)
                dsocket = _factory(aSocket);
            else
                throw new InvalidOperationException("No factory provided to create socket instance.");

            if (dsocket == null)
            {
                Console.WriteLine("Failed to create a new socket instance.");
                return;
            }

            // Utilisation de m_tempAddress pour stocker l'adresse du client
            if (aSocket.RemoteEndPoint is IPEndPoint remoteEndPoint)
            {
                m_tempAddress = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
            }

            (dsocket as dynamic).Accept(m_tempAddress);
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
}
#endif
