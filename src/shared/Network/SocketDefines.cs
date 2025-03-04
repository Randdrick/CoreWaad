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
using System.Net.Sockets;
using System.Threading;

namespace WaadShared.Network;

#if CONFIG_USE_IOCP
public enum SocketIOEvent
{
    SOCKET_IO_EVENT_READ_COMPLETE = 0,
    SOCKET_IO_EVENT_WRITE_END = 1,
    SOCKET_IO_THREAD_SHUTDOWN = 2,
    NUM_SOCKET_IO_EVENTS = 3,
}

public class OverlappedStruct
{
    public SocketAsyncEventArgs AsyncEventArgs { get; private set; }
    public SocketIOEvent Event { get; private set; }
    private int _inUse;

    public OverlappedStruct(SocketIOEvent ev)
    {
        Event = ev;
        AsyncEventArgs = new SocketAsyncEventArgs();
        _inUse = 0;
    }

    public OverlappedStruct()
    {
        AsyncEventArgs = new SocketAsyncEventArgs();
        _inUse = 0;
    }

    public void Reset(SocketIOEvent ev)
    {
        AsyncEventArgs = new SocketAsyncEventArgs();
        Event = ev;
    }

    public void Mark()
    {
        int val = Interlocked.CompareExchange(ref _inUse, 1, 0);
        if (val != 0)
            Console.WriteLine($"!!!! Network: Detected double use of read/write event! Previous event was {Event}.");
    }

    public void Unmark()
    {
        Interlocked.Exchange(ref _inUse, 0);
    }
}
#endif