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
 */

using System;
using WaadShared;
using WaadShared.Network;

using static WaadShared.WorkerServerSocket;

namespace WaadRealmServer
{
    public class WorkerServerSocket(System.Net.Sockets.Socket socket) : Socket(socket, 100000, 100000) 
    {
        private bool _authenticated = false;
        private int _remaining = 0;
        private ushort _cmd = 0;
        private WorkerServer _ws = null;
        private WorldSocket Ws { get; set; }
        private readonly int REVISION = Master.REVISION;

        public void HandleAuthRequest(WorldPacket pck)
        {
            byte[] key = new byte[20];
            uint build;
            string ver;
            pck.ReadBytes(key, 0, 20);
            build = pck.ReadUInt32();
            ver = pck.ReadString();

            CLog.Notice("WSSocket", R_N_WORKSRVSOC, ver, build);

            // accept it
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_AUTH_RESULT, 4);
            data.WriteUInt32(1);
            SendPacket(data);
            _authenticated = true;
        }

        public override void OnRead()
        {
            while (true)
            {
                if (_cmd == 0)
                {
                    if (GetReadBuffer().GetContiguousBytes() < 6)
                        break;
                    _cmd = GetReadBuffer().ReadUInt16();
                    _remaining = GetReadBuffer().ReadInt32();
                }
                if (_remaining > 0 && GetReadBuffer().GetSize() < _remaining)
                    break;

                if (_cmd == (ushort)WorkerServerOpcodes.ICMSG_WOW_PACKET)
                {
                    uint sid = GetReadBuffer().ReadUInt32();
                    ushort op = GetReadBuffer().ReadUInt16();
                    uint sz = GetReadBuffer().ReadUInt32();
                    var session = ClientMgr.Instance.GetSession(sid);
                    if (session != null && session.GetSocket() != null)
                    {
                        byte[] buf = GetReadBuffer().ReadBytes(sz);
                        Ws.OutPacket(op, (int)sz, buf);
                    }
                    else
                    {
                        GetReadBuffer().Remove((int)sz);
                    }
                    _cmd = 0;
                    continue;
                }
                var pck = new WorldPacket(_cmd, _remaining);
                _cmd = 0;
                pck.Resize(_remaining);
                byte[] tempBytes = GetReadBuffer().ReadBytes((uint)_remaining);
                Array.Copy(tempBytes, 0, pck.Contents, 0, _remaining);

                if (_authenticated)
                {
                    if (_ws == null)
                    {
                        if (pck.GetOpcode() == (ushort)WorkerServerOpcodes.ICMSG_REGISTER_WORKER)
                        {
                            HandleRegisterWorker(pck);
                        }
                    }
                    else
                    {
                        _ws.QueuePacket(pck);
                    }
                }
                else
                {
                    if (pck.GetOpcode() != (ushort)WorkerServerOpcodes.ICMSG_AUTH_REPLY)
                        Disconnect();
                    else
                        HandleAuthRequest(pck);
                }
            }
        }

        public void HandleRegisterWorker(WorldPacket pck)
        {
            uint build = pck.ReadUInt32();
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_REGISTER_RESULT, 10);
                if (build == REVISION)
            {
                var newServer = ClusterMgr.Instance.CreateWorkerServer(this);
                if (newServer == null)
                {
                    data.WriteUInt32(0);
                    SendPacket(data);
                    return;
                }
                data.WriteUInt32(1);
                SendPacket(data);
                _ws = newServer;
                // Reset read position to start of packet before queueing
                pck.Rpos = 0;
                _ws.QueuePacket(pck);
            }
            else
            {
                CLog.Error("ClusterMgr", $"Supression du serveur pour cause de build incorrecte({build}/{REVISION})");
                data.WriteUInt32(0);
                SendPacket(data);
                return;
            }
        }

        public void SendPacket(WorldPacket pck) 
        {
            if (!IsConnected()) return;
            BurstBegin();
            ushort opcode = pck.GetOpcode();
            int size = pck.Size;
            BurstSend(BitConverter.GetBytes(opcode), 2);
            BurstSend(BitConverter.GetBytes(size), 4);
            if (size > 0)
                BurstSend(pck.Contents, size);
            BurstPush();
            BurstEnd();
        }

        public void SendWoWPacket(Session from, WorldPacket pck)
        {
            if (!IsConnected()) return;
            BurstBegin();
            uint opcode2 = (uint)WorkerServerOpcodes.ISMSG_WOW_PACKET;
            int size1 = pck.Size;
            int size2 = size1 + 10;
            uint id = from.GetSessionId();
            ushort opcode1 = pck.GetOpcode();
            BurstSend(BitConverter.GetBytes(opcode2), 2);
            BurstSend(BitConverter.GetBytes(size2), 4);
            BurstSend(BitConverter.GetBytes(id), 4);
            BurstSend(BitConverter.GetBytes(opcode1), 2);
            BurstSend(BitConverter.GetBytes(size1), 4);
            if (size1 > 0)
                BurstSend(pck.Contents, size1);
            BurstPush();
            BurstEnd();
        }

        public new void OnConnect()
        {
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_AUTH_REQUEST, 4);
            data.WriteUInt32((uint)REVISION);
            SendPacket(data);
        }

        public override void OnDisconnect()
        {
            ClusterMgr.Instance.OnServerDisconnect(_ws);
        }

        public static explicit operator WorkerServerSocket(Session v)
        {
            if (v == null) return null;

            // Prefer returning the worker server's socket if the session is attached to a server
            try
            {
                var srv = v.GetServer();
                if (srv != null)
                    return srv.ServerSocket;

                // No sensible fallback: sessions usually reference a WorldSocket, not a WorkerServerSocket.
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
