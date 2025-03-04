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

using System.Net.Sockets;

namespace WaadShared;

public static class SocketOps
{
    // Create file descriptor for socket i/o operations.
    public static Socket CreateTCPFileDescriptor()
    {
        return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    // Disable blocking send/recv calls.
    public static bool Nonblocking(Socket socket)
    {
        try
        {
            socket.Blocking = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Enable blocking send/recv calls.
    public static bool Blocking(Socket socket)
    {
        try
        {
            socket.Blocking = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Disable Nagle buffering algorithm
    public static bool DisableBuffering(Socket socket)
    {
        try
        {
            socket.NoDelay = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Enable Nagle buffering algorithm
    public static bool EnableBuffering(Socket socket)
    {
        try
        {
            socket.NoDelay = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Set internal buffer size to socket.
    public static bool SetSendBufferSize(Socket socket, int size)
    {
        try
        {
            socket.SendBufferSize = size;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Set internal buffer size to socket.
    public static bool SetRecvBufferSize(Socket socket, int size)
    {
        try
        {
            socket.ReceiveBufferSize = size;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Closes a socket fully.
    public static void CloseSocket(Socket socket)
    {
        try
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }
        catch
        {
            // Handle exception if necessary
        }
    }

    // Sets reuseaddr
    public static void ReuseAddr(Socket socket)
    {
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }
        catch
        {
            // Handle exception if necessary
        }
    }
}
#endif