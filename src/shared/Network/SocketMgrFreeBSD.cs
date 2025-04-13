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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace WaadShared.Network
{
    // Définition de la structure KEvent
    public struct KEvent
    {
        public int Ident;
        public int Filter;
        public KEventFlags Flags;
        public int Fflags;
        public long Data;
        public IntPtr Udata;
    }

    [Flags]
    public enum KEventFlags
    {
        EV_ADD = 0x0001,
        EV_ONESHOT = 0x0010,
        EV_DELETE = 0x11
    }

    public enum FilterFlags
    {
        EVFILT_WRITE = -1, // Remplacez par la valeur réelle si nécessaire
        EVFILT_READ = -2   // Remplacez par la valeur réelle si nécessaire
    }

    public class SocketMgr
    {
        // Déclaration des constantes au niveau de la classe pour une accessibilité globale
        public const int THREAD_EVENT_SIZE = 4096; // Accessible globalement

        private static SocketMgr instance;
        private readonly ConcurrentDictionary<int, Socket> fds = new();
        private readonly ConcurrentDictionary<int, Socket> listenfds = new();
        private readonly List<KEvent> eventList = [];
        private readonly object eventListLock = new(); // Utilisation d'un objet pour le verrouillage
        public object EventListLock => eventListLock;
        private int socketCount;

        public SocketMgr()
        {
            socketCount = 0;
        }

        public static SocketMgr GetInstance()
        {
            instance ??= new SocketMgr();
            return instance;
        }

        public void AddSocket(Socket s)
        {
            if (fds.ContainsKey(s.Handle))
            {
                throw new InvalidOperationException("Socket already exists.");
            }
            fds[s.Handle] = s;
            socketCount++;

            KEvent ev = new()
            {
                Ident = s.Handle,
                Filter = s.Available > 0 ? (int)FilterFlags.EVFILT_WRITE : (int)FilterFlags.EVFILT_READ,
                Flags = s.Available > 0 ? KEventFlags.EV_ADD | KEventFlags.EV_ONESHOT : KEventFlags.EV_ADD,
                Fflags = 0,
                Data = 0,
                Udata = IntPtr.Zero
            };

            Kevent(ev);
        }

        public void AddListenSocket(Socket s)
        {
            if (listenfds.ContainsKey(s.Handle))
            {
                throw new InvalidOperationException("Listen socket already exists.");
            }
            listenfds[s.Handle] = s;

            KEvent ev = new()
            {
                Ident = s.Handle,
                Filter = (int)FilterFlags.EVFILT_READ,
                Flags = KEventFlags.EV_ADD,
                Fflags = 0,
                Data = 0,
                Udata = IntPtr.Zero
            };

            Kevent(ev);
        }

        public void RemoveSocket(Socket s)
        {
            if (fds.TryRemove(s.Handle, out _))
            {
                s.Close();
                socketCount--;

                KEvent ev = new()
                {
                    Ident = s.Handle,
                    Filter = (int)FilterFlags.EVFILT_WRITE,
                    Flags = KEventFlags.EV_DELETE,
                    Fflags = 0,
                    Data = 0,
                    Udata = IntPtr.Zero
                };

                Kevent(ev);

                ev.Filter = (int)FilterFlags.EVFILT_READ;
                Kevent(ev);
            }
            else
            {
                Console.WriteLine($"Duplicate removal of fd {s.Handle}!");
            }
        }

        public void CloseAll()
        {
            foreach (var socket in fds.Values)
            {
                socket.Close();
            }
            fds.Clear();
            socketCount = 0;
        }

        public static void SpawnWorkerThreads()
        {
            for (int i = 0; i < 1; i++)
            {
                Thread workerThread = new(new SocketWorkerThread().Run);
                workerThread.Start();
            }
        }

        private void Kevent(KEvent ev)
        {
            lock (eventListLock) // Utilisation d'un objet pour le verrouillage
            {
                if (ev.Flags.HasFlag(KEventFlags.EV_DELETE))
                {
                    eventList.RemoveAll(e => e.Ident == ev.Ident && e.Filter == ev.Filter);
                }
                else
                {
                    eventList.Add(ev);
                }
            }
        }

        // Méthode publique pour accéder à eventList
        public List<KEvent> GetEventList()
        {
            lock (eventListLock)
            {
                return [.. eventList];
            }
        }
        public void AddEvent(KEvent ev)
        {
            Kevent(ev);
        }

        // Méthode publique pour vider eventList
        public void ClearEventList()
        {
            lock (eventListLock)
            {
                eventList.Clear();
            }
        }

        // Méthode publique pour accéder à fds
        public bool TryGetSocket(int handle, out Socket socket)
        {
            return fds.TryGetValue(handle, out socket);
        }
    }

    public class SocketWorkerThread
    {
        private volatile bool m_threadRunning = true;
        private readonly KEvent[] events = new KEvent[SocketMgr.THREAD_EVENT_SIZE];

        public void Run()
        {
            SocketMgr mgr = SocketMgr.GetInstance();

            while (m_threadRunning)
            {
                // Simuler le délai de 5 secondes
                Thread.Sleep(5000);

                // Traiter les événements
                lock (mgr.EventListLock) // Utilisation de la propriété publique pour le verrouillage
                {
                    foreach (var ev in mgr.GetEventList())
                    {
                        if (mgr.TryGetSocket(ev.Ident, out Socket socket))
                        {
                            if (ev.Filter == (int)FilterFlags.EVFILT_WRITE)
                            {
                                socket.BurstPush();
                            }
                            else if (ev.Filter == (int)FilterFlags.EVFILT_READ)
                            {
                                socket.ReadCallback(0);
                            }
                        }
                    }
                    mgr.ClearEventList();
                }
            }
        }

        public void OnShutdown()
        {
            m_threadRunning = false;
        }
    }


    public static class SocketExtensions
    {
        private static readonly object m_readMutex = new();
        private static readonly object m_writeMutex = new();

        public static void PostEvent(this Socket socket, int events, bool oneshot)
        {
            KEvent ev = new()
            {
                Ident = socket.Handle,
                Filter = events,
                Flags = oneshot ? KEventFlags.EV_ADD | KEventFlags.EV_ONESHOT : KEventFlags.EV_ADD,
                Fflags = 0,
                Data = 0,
                Udata = IntPtr.Zero
            };

            SocketMgr.GetInstance().AddEvent(ev);
        }

        public static void ReadCallback(this Socket socket, uint len)
        {
            lock (m_readMutex)
            {
                int space = Buffer.GetSpace();
                int bytes = 0;
                try
                {
                    bytes = socket.Receive(Buffer.GetBuffer(), space, SocketFlags.None);
                }
                catch (SocketException)
                {
                    Disconnect(socket);
                    return;
                }

                if (bytes <= 0)
                {
                    Disconnect(socket);
                    return;
                }
                else if (bytes > 0)
                {
                    Buffer.IncrementWritten(bytes);
                    OnRead(socket);
                }
            }
        }

        public static void WriteCallback(this Socket socket)
        {
            lock (m_writeMutex)
            {
                int bytesWritten = 0;
                try
                {
                    bytesWritten = socket.Send(Buffer.GetBufferStart(), Buffer.GetContiguousBytes(), SocketFlags.None);
                }
                catch (SocketException)
                {
                    Disconnect(socket);
                    return;
                }

                if (bytesWritten < 0)
                {
                    Disconnect(socket);
                    return;
                }

                Buffer.Remove(bytesWritten);
            }
        }

        public static void BurstPush(this Socket socket)
        {
            if (AcquireSendLock(socket))
            {
                socket.PostEvent((int)FilterFlags.EVFILT_WRITE, true);
            }
        }

        private static bool AcquireSendLock(Socket socket)
        {
            // Implémentation simple pour acquérir un verrou d'envoi
            // Dans une implémentation réelle, cela pourrait impliquer une logique plus complexe
            return true;
        }

        private static void Disconnect(Socket socket)
        {
            // Logique de déconnexion
            socket.Close();
            SocketMgr.GetInstance().RemoveSocket(socket);
        }

        private static void OnRead(Socket socket)
        {
            // Logique de callback de lecture
            // Vous pouvez traiter les données lues ici
            Console.WriteLine("Data read from socket.");
        }

        private static class Buffer
        {
            private static readonly byte[] buffer = new byte[1024]; // Taille du buffer
            private static int writePos = 0;

            public static int GetSpace()
            {
                return buffer.Length - writePos;
            }

            public static byte[] GetBuffer()
            {
                return buffer;
            }

            public static void IncrementWritten(int bytes)
            {
                writePos += bytes;
            }

            public static byte[] GetBufferStart()
            {
                return buffer;
            }

            public static int GetContiguousBytes()
            {
                return writePos;
            }

            public static void Remove(int bytes)
            {
                if (bytes > 0 && bytes <= writePos)
                {
                    Array.Copy(buffer, bytes, buffer, 0, writePos - bytes);
                    writePos -= bytes;
                }
            }
        }
    }
}

#endif