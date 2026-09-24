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
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using WaadShared;
using WaadShared.Network;

namespace WaadWorldServer;

// Socket client utilisé par ClusterInterface pour se connecter au WorkerServerSocket du RealmServer.
public sealed class ClusterClientSocket : Socket
{
    private ushort _opcode;
    private int _remaining;
    private ClusterInterface _owner;

    internal void Attach(ClusterInterface owner) => _owner = owner;

    public bool Authenticated { get; internal set; }

    public override void OnConnectVirtual()
    {
        CLog.Success("[ClusterInterface]", "Connecté au serveur de Royaume(s) ({0}).", GetRemoteIP());
    }

    public override void OnDisconnect()
    {
        Authenticated = false;
        _owner?.OnSocketDisconnected(this);
        CLog.Warning("[ClusterInterface]", "Connexion au serveur de Royaume(s) perdue.");
    }

    public override void OnRead()
    {
        CircularBuffer buffer = GetReadBuffer();

        while (true)
        {
            if (_opcode == 0)
            {
                if (buffer.GetContiguousBytes() < 6)
                    return;

                _opcode = buffer.ReadUInt16();
                _remaining = buffer.ReadInt32();
                if (_remaining < 0 || _remaining > 16 * 1024 * 1024)
                {
                    CLog.Error("[ClusterInterface]", "Taille de paquet invalide: {0}.", _remaining);
                    Disconnect();
                    return;
                }
            }

            if (buffer.GetSize() < _remaining)
                return;

            var packet = new WorldPacket(_opcode, _remaining);
            if (_remaining > 0)
            {
                byte[] payload = buffer.ReadBytes((uint)_remaining);
                packet.Append(payload, payload.Length);
            }

            _opcode = 0;
            _remaining = 0;
            _owner?.QueuePacket(packet);
        }
    }
}

public sealed class ClusterInterface : IDisposable
{
    private static readonly Lazy<ClusterInterface> s_instance = new(() => new ClusterInterface());
    public static ClusterInterface Instance => s_instance.Value;

    private readonly ConcurrentQueue<WorldPacket> _packets = new();
    private ClusterClientSocket _socket;
    private string _host;
    private uint _port;
    private string _password;
    private byte[] _key;
    private long _lastConnectAttempt;
    private bool _registered;
    private bool _disposed;

    private ClusterInterface()
    {
    }

    public void Startup(string host, uint port, string password)
    {
        if (string.IsNullOrWhiteSpace(host) || port == 0 || string.IsNullOrEmpty(password))
            throw new ArgumentException("Les paramètres de connexion au serveur de Royaume(s) sont incomplets.");

        _host = host;
        _port = port;
        _password = password;
        _key = SHA1.HashData(Encoding.UTF8.GetBytes(password));
        ConnectToRealmServer();
    }

    private void ConnectToRealmServer()
    {
        _lastConnectAttempt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var socket = Socket.ConnectTCPSocket<ClusterClientSocket>(_host, checked((ushort)_port));
        if (socket == null)
        {
            CLog.Error("[ClusterInterface]", "Impossible de se connecter au serveur de Royaume(s) {0}:{1}.", _host, _port);
            return;
        }

        socket.Attach(this);
        _socket = socket;
        _registered = false;
        SendAuthReply();
    }

    private void SendAuthReply()
    {
        var packet = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_AUTH_REPLY, 64);
        packet.WriteBytes(_key);
        packet.WriteUInt32((uint)Master.REVISION);
        packet.WriteString(GenerateVersionString());
        SendPacket(packet);
        packet.Dispose();
    }

    private static string GenerateVersionString()
    {
        return $"WAAD r{Master.REVISION}/Debug-{Environment.OSVersion.Platform}-{(Environment.Is64BitProcess ? "amd64" : "x86")}";
    }

    internal void OnSocketDisconnected(ClusterClientSocket socket)
    {
        if (ReferenceEquals(_socket, socket))
        {
            _socket = null;
            _registered = false;
        }
    }

    internal void QueuePacket(WorldPacket packet)
    {
        if (!_disposed)
            _packets.Enqueue(packet);
        else
            packet.Dispose();
    }

    private void SendPacket(WorldPacket packet)
    {
        if (_socket == null || !_socket.IsConnected())
            return;

        _socket.BurstBegin();
        try
        {
            _socket.BurstSend(BitConverter.GetBytes(packet.GetOpcode()), 2);
            _socket.BurstSend(BitConverter.GetBytes(packet.Size), 4);
            if (packet.Size > 0)
                _socket.BurstSend(packet.Contents, packet.Size);
            _socket.BurstPush();
        }
        finally
        {
            _socket.BurstEnd();
        }
    }

    private void RegisterWorker()
    {
        var packet = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_REGISTER_WORKER, 12);
        packet.WriteUInt32((uint)Master.REVISION);
        packet.WriteUInt32(0);
        packet.WriteUInt32(0);
        SendPacket(packet);
        packet.Dispose();
    }

    private void HandlePacket(WorldPacket packet)
    {
        try
        {
            switch ((WorkerServerOpcodes)packet.GetOpcode())
            {
                case WorkerServerOpcodes.ISMSG_AUTH_REQUEST:
                    SendAuthReply();
                    break;
                case WorkerServerOpcodes.ISMSG_AUTH_RESULT:
                    if (packet.ReadUInt32() == 0)
                    {
                        CLog.Error("[ClusterInterface]", "Authentification refusée par le serveur de Royaume(s).");
                        _socket?.Disconnect();
                    }
                    else
                    {
                        RegisterWorker();
                    }
                    break;
                case WorkerServerOpcodes.ISMSG_REGISTER_RESULT:
                    _registered = packet.ReadUInt32() != 0;
                    if (!_registered)
                        CLog.Error("[ClusterInterface]", "Enregistrement du worker refusé.");
                    else
                        CLog.Success("[ClusterInterface]", "Worker enregistré auprès du serveur de Royaume(s).");
                    break;
                case WorkerServerOpcodes.ISMSG_WOW_PACKET:
                    CLog.Debug("[ClusterInterface]", "Paquet client reçu avant le portage de WorldSession.");
                    break;
                default:
                    CLog.Warning("[ClusterInterface]", "Opcode de cluster non géré: {0}.", packet.GetOpcode());
                    break;
            }
        }
        finally
        {
            packet.Dispose();
        }
    }

    public void ForwardWoWPacket(uint sessionId, WorldPacket packet)
    {
        if (!_registered || _socket == null || !_socket.IsConnected())
            return;

        var envelope = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_WOW_PACKET, packet.Size + 10);
        envelope.WriteUInt32(sessionId);
        envelope.WriteUInt16(packet.GetOpcode());
        envelope.WriteUInt32((uint)packet.Size);
        if (packet.Size > 0)
            envelope.Write(packet.Contents, 0, packet.Size);
        SendPacket(envelope);
        envelope.Dispose();
    }

    public void Update()
    {
        if (_socket == null && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= _lastConnectAttempt + 5)
            ConnectToRealmServer();

        while (_packets.TryDequeue(out var packet))
            HandlePacket(packet);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _socket?.Disconnect();
        _socket?.Dispose();
        _socket = null;
        while (_packets.TryDequeue(out var packet))
            packet.Dispose();
    }
}
