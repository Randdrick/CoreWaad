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
using System.Linq;
using System.Threading;

using static WaadShared.Network.SocketManager;
using static WaadShared.Network.Socket;

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
        if (m_socket == null)
        {
            Console.WriteLine("Failed to create TCP file descriptor.");
            return;
        }

        SocketOps.ReuseAddr(m_socket);
        SocketOps.Blocking(m_socket);

        m_address = new IPEndPoint(IPAddress.Any, (int)Port);
        m_tempAddress = new IPEndPoint(IPAddress.Any, (int)Port);
        m_opened = false;

        if (ListenAddress != "0.0.0.0")
        {
            try
            {
                if (ListenAddress == "127.0.0.1")
                {
                    // Explicitly set to loopback address
                    m_address.Address = IPAddress.Loopback;
                }
                else
                {
                    IPHostEntry hostname = Dns.GetHostEntry(ListenAddress);
                    if (hostname != null && hostname.AddressList.Length > 0)
                    {
                        // Select the first IPv4 address from the AddressList
                        m_address.Address = hostname.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                                            ?? throw new Exception($"No valid IPv4 address found for {ListenAddress}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resolve ListenAddress '{ListenAddress}': {ex.Message}");
                return;
            }
        }

        // Log the value of m_address before binding
        CLog.Notice("[ListenSocket]", $"En attente d'écoute sur l'adresse : {m_address.Address}, Port: {m_address.Port}");

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
        catch (SocketException)
        {
            Console.WriteLine($"Unable to listen on port {Port}.");
            return;
        }

        m_opened = true;

        // Replace Marshal.SizeOf with a fixed size for IPEndPoint
        len = IntPtr.Size * 2; // Assuming 2 pointers for Address and Port
        m_cp = GetCompletionPort();
    }

    ~ListenSocket()
    {
        Close();
    }

    public bool Run()
    {
        while (m_opened)
        {
            try
            {
                aSocket = m_socket.Accept();
                if (aSocket == null)
                {
                    Console.WriteLine("Accept returned null.");
                    continue; // shouldn't happen, we are blocking.
                }

                socket = new T();
                if (socket == null)
                {
                    Console.WriteLine("Failed to create a new socket instance.");
                    continue;
                }
                bool isSocketValid = true;

                if (m_cp == IntPtr.Zero)
                {
                    Console.WriteLine("Completion port is not initialized.");
                    continue;
                }

                if (m_tempAddress == null)
                {
                    Console.WriteLine("Temporary address is null.");
                    continue;
                }

                SetCompletionPort(m_cp, isSocketValid);

                Accept(m_tempAddress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
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
                aSocket = m_socket?.Accept();
                if (aSocket == null)
                {
                    Console.WriteLine("Accept returned null.");
                    continue; // shouldn't happen, we are blocking.
                }

                socket = new T();
                // socket = (T)Activator.CreateInstance(typeof(T), aSocket, ByteBuffer.DEFAULT_SIZE, ByteBuffer.DEFAULT_SIZE);
                if (socket == null)
                {
                    Console.WriteLine("Failed to create a new socket instance.");
                    continue;
                }
#if CONFIG_USE_IOCP
                if (m_cp == IntPtr.Zero)
                {
                    Console.WriteLine("Completion port is not initialized.");
                    continue;
                }
#endif
                if (m_tempAddress == null)
                {
                    Console.WriteLine("Temporary address is null.");
                    continue;
                }

                // Ensure SetCompletionPort and Accept are properly invoked
                SetCompletionPort(m_cp, true);
                Accept(m_tempAddress);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket exception occurred: {ex.Message}");
            }
            catch (ObjectDisposedException ex)
            {
                Console.WriteLine($"Socket has been disposed: {ex.Message}");
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine($"Null reference encountered: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error to Run(CancellationToken token): {ex.Message}");
            }
        }
        return false;
    }
}

public class ThreadPoolCompletionPort
{
    public IntPtr Handle { get; } = new IntPtr(1); // Dummy handle to simulate IOCP

    public ThreadPoolCompletionPort()
    {
        // Initialize the ThreadPoolCompletionPort
    }
}

#endif
