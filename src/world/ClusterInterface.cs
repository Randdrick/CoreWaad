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
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using WaadShared;
using WaadShared.AuthCodes;
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
                if (buffer.GetSize() < 6)
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

// Représente un joueur connecté via le RealmServer (WorldSession/Player restent à porter).
// Sert de point d'ancrage pour router les paquets ISMSG_WOW_PACKET/ICMSG_WOW_PACKET.
public sealed class ClusterPlayerSession(uint sessionId, uint guid, uint accountId) : IDisposable
{
    internal object SyncRoot { get; } = new();
    public bool IsDisposed { get; private set; }
    public long CurrentTimeMs { get; internal set; }
    public long LastPacketTimeMs { get; private set; } = Environment.TickCount64;
    public bool RecentLogout { get; set; }
    public bool IsLoggingOut { get; internal set; }
    public bool IsDisconnected { get; private set; }
    public bool RemovalRequested { get; internal set; }
    public long LogoutDeadlineMs { get; private set; }
    public long DisconnectedAtMs { get; private set; }
    public uint SessionId { get; } = sessionId;
    public uint Guid { get; } = guid;
    public uint AccountId { get; } = accountId;
    public uint MapId { get; set; }
    public uint InstanceId { get; set; }
    public uint AccountFlags { get; set; }
    public uint ClientBuild { get; set; }
    public string GMPermissions { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Player Player { get; set; }

    public ConcurrentQueue<WorldPacket> IncomingPackets { get; } = new();

    public void SetLogoutTimer(uint delayMs)
    {
        lock (SyncRoot)
            LogoutDeadlineMs = delayMs == 0 ? 0 : Environment.TickCount64 + delayMs;
    }

    public void MarkDisconnected()
    {
        lock (SyncRoot)
        {
            if (IsDisposed || IsDisconnected)
                return;
            IsDisconnected = true;
            DisconnectedAtMs = Environment.TickCount64;
            if (LogoutDeadlineMs == 0)
                SetLogoutTimer(WorldSession.PlayerLogoutDelayMs);
        }
    }

    public void QueuePacket(WorldPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        lock (SyncRoot)
        {
            if (IsDisposed || IsDisconnected || RemovalRequested)
            {
                packet.Dispose();
                return;
            }

            LastPacketTimeMs = Environment.TickCount64;
            IncomingPackets.Enqueue(packet);
        }
    }

    public void Dispose()
    {
        lock (SyncRoot)
        {
            if (IsDisposed)
                return;

            if (!WorldSession.LogoutPlayer(this, true, false))
                throw new InvalidOperationException("Impossible de detruire une session dont la sauvegarde a echoue.");
            IsDisposed = true;
            Player = null;
            while (IncomingPackets.TryDequeue(out var packet))
                packet.Dispose();
        }
    }

    public void Update() => WorldSession.Update(this, InstanceId);

    public int Update(uint instanceId) => WorldSession.Update(this, instanceId);
}

public sealed class ClusterInterface : IDisposable
{
    private static readonly Lazy<ClusterInterface> s_instance = new(() => new ClusterInterface());
    public static ClusterInterface Instance => s_instance.Value;

    // Table de dispatch des opcodes ISMSG_* (même convention que WorkerServer.PHandlers côté realm).
    private static readonly Dictionary<WorkerServerOpcodes, Action<ClusterInterface, WorldPacket>> PHandlers = new()
    {
        [WorkerServerOpcodes.ISMSG_AUTH_REQUEST] = (ci, p) => ci.HandleAuthRequest(p),
        [WorkerServerOpcodes.ISMSG_AUTH_RESULT] = (ci, p) => ci.HandleAuthResult(p),
        [WorkerServerOpcodes.ISMSG_REGISTER_RESULT] = (ci, p) => ci.HandleRegisterResult(p),
        [WorkerServerOpcodes.ISMSG_CREATE_INSTANCE] = (ci, p) => ci.HandleCreateInstance(p),
        [WorkerServerOpcodes.ISMSG_PLAYER_LOGIN] = (ci, p) => ci.HandlePlayerLogin(p),
        [WorkerServerOpcodes.ISMSG_WOW_PACKET] = (ci, p) => ci.HandleWoWPacket(p),
        [WorkerServerOpcodes.ISMSG_TELEPORT_RESULT] = (ci, p) => ci.HandleTeleportResult(p),
        [WorkerServerOpcodes.ISMSG_SESSION_REMOVED] = (ci, p) => ci.HandleSessionRemoved(p),
        [WorkerServerOpcodes.ISMSG_SAVE_ALL_PLAYERS] = (ci, p) => ci.HandleSaveAllPlayers(p),
        [WorkerServerOpcodes.ISMSG_TRANSPORTER_MAP_CHANGE] = (ci, p) => HandleTransporterMapChange(p),
        [WorkerServerOpcodes.ISMSG_PLAYER_TELEPORT] = (ci, p) => ci.HandlePlayerTeleport(p),
        [WorkerServerOpcodes.ISMSG_CREATE_PLAYER] = (ci, p) => ci.HandleCreatePlayer(p),
        [WorkerServerOpcodes.ISMSG_DELETE_PLAYER] = (ci, p) => HandleDeletePlayer(p),
        [WorkerServerOpcodes.ISMSG_PACKED_PLAYER_INFO] = (ci, p) => HandlePackedPlayerInfo(p),
        [WorkerServerOpcodes.ISMSG_DESTROY_PLAYER_INFO] = (ci, p) => ci.HandleDestroyPlayerInfo(p),
        [WorkerServerOpcodes.ISMSG_PLAYER_INFO] = (ci, p) => HandlePlayerInfo(p),
        [WorkerServerOpcodes.ISMSG_CHANNEL_ACTION] = (ci, p) => HandleChannelAction(p),
        [WorkerServerOpcodes.ISMSG_CHANNEL_LFG_DUNGEON_STATUS_REQUEST] = (ci, p) => ci.HandleChannelLFGDungeonStatusRequest(p),
    };

    private readonly ConcurrentQueue<WorldPacket> _packets = new();
    private readonly ConcurrentDictionary<uint, ClusterPlayerSession> _sessions = new();
    private readonly ConcurrentDictionary<uint, uint> _onlineGuids = new(); // guid -> sessionId
    private ClusterClientSocket _socket;
    private string _host;
    private uint _port;
    private string _password;
    private byte[] _key;
    private long _lastConnectAttempt;
    private long _connectTimestampMs;
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
        _connectTimestampMs = Environment.TickCount64;
        SendAuthReply();
    }

    // ISMSG_AUTH_REQUEST: le RealmServer annonce sa build ; on répond avec la clé partagée hashée.
    private void HandleAuthRequest(WorldPacket packet)
    {
        uint realmBuild = packet.ReadUInt32();
        long latencyMs = Environment.TickCount64 - _connectTimestampMs;
        CLog.Debug("[ClusterInterface]", "Demande d'authentification reçue de {0} (build {1}).", _socket?.GetRemoteIP(), realmBuild);
        SendAuthReply();
        CLog.Notice("[ClusterInterface]", "Latence entre les serveurs de royaume(s) : {0} ms.", latencyMs);
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
            foreach (var session in _sessions.Values)
                session.MarkDisconnected();
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
            var opcode = (WorkerServerOpcodes)packet.GetOpcode();
            if (PHandlers.TryGetValue(opcode, out var handler))
                handler(this, packet);
            else
                CLog.Warning("[ClusterInterface]", "Opcode de cluster non géré: {0}.", packet.GetOpcode());
        }
        finally
        {
            packet.Dispose();
        }
    }

    private void HandleAuthResult(WorldPacket packet)
    {
        if (packet.ReadUInt32() == 0)
        {
            CLog.Error("[ClusterInterface]", "Authentification refusée par le serveur de Royaume(s).");
            _socket?.Disconnect();
        }
        else
        {
            RegisterWorker();
        }
    }

    private void HandleRegisterResult(WorldPacket packet)
    {
        _registered = packet.ReadUInt32() != 0;
        if (!_registered)
        {
            CLog.Error("[ClusterInterface]", "Enregistrement du worker refusé (build incorrecte). Déconnexion.");
            _socket?.Disconnect();
        }
        else
        {
            CLog.Success("[ClusterInterface]", "Worker enregistré auprès du serveur de Royaume(s).");
        }
    }

    // ISMSG_CREATE_INSTANCE: mapid, instanceid.
    private void HandleCreateInstance(WorldPacket packet)
    {
        uint mapId = packet.ReadUInt32();
        uint instanceId = packet.ReadUInt32();

        if (!MapMgr.Instance.ClusterCreateInstance(mapId, instanceId))
        {
            CLog.Error("[ClusterInterface]", "Échec de création de l'instance {0} sur la carte {1}.", instanceId, mapId);
            _socket?.Disconnect();
            return;
        }

        CLog.Debug("[ClusterInterface]", "Instance {0} créée sur la carte {1}.", instanceId, mapId);
    }

    // ISMSG_PLAYER_LOGIN: guid, mapid, instanceid, accountId, accountFlags, sessionId, GMPermissions,
    // accountName, clientBuild, puis 8 entrées de données de compte (taille + octets optionnels).
    private void HandlePlayerLogin(WorldPacket packet)
    {
        uint guid = packet.ReadUInt32();
        uint mapId = packet.ReadUInt32();
        uint instanceId = packet.ReadUInt32();
        uint accountId = packet.ReadUInt32();
        uint accountFlags = packet.ReadUInt32();
        uint sessionId = packet.ReadUInt32();
        string gmPermissions = packet.ReadString();
        string accountName = packet.ReadString();
        uint clientBuild = packet.ReadUInt32();

        for (int i = 0; i < 8; i++)
        {
            uint size = packet.ReadUInt32();
            if (size > 0)
                packet.ReadBytes(new byte[size], 0, (int)size);
        }

        if (_onlineGuids.ContainsKey(guid))
        {
            CLog.Warning("[ClusterInterface]", "Le joueur {0} est déjà connecté, connexion refusée (session {1}).", guid, sessionId);
            SendPlayerLoginResult(guid, sessionId, LoginErrorCode.CHAR_LOGIN_DUPLICATE_CHARACTER);
            return;
        }

        var session = new ClusterPlayerSession(sessionId, guid, accountId)
        {
            MapId = mapId,
            InstanceId = instanceId,
            AccountFlags = accountFlags,
            ClientBuild = clientBuild,
            GMPermissions = gmPermissions,
            AccountName = accountName
        };

        _sessions[sessionId] = session;
        _onlineGuids[guid] = sessionId;

        CLog.Debug("[ClusterInterface]", "Session {0} enregistrée pour le joueur {1} (carte {2}, instance {3}), chargement en cours.", sessionId, guid, mapId, instanceId);
        new Player(guid, session).LoadFromDB(OnPlayerLoaded);
    }

    // Appelé sur le thread de chargement de Player.LoadFromDB.
    private void OnPlayerLoaded(Player player, bool success)
    {
        ClusterPlayerSession session = player.Session;

        // La session a pu être détruite (ISMSG_SESSION_REMOVED) pendant le chargement.
        if (!_sessions.TryGetValue(session.SessionId, out var current) || !ReferenceEquals(current, session))
            return;

        if (!success)
        {
            DestroySession(session.SessionId);
            SendPlayerLoginResult(player.Guid, session.SessionId, LoginErrorCode.CHAR_LOGIN_NO_CHARACTER);
            return;
        }

        lock (session.SyncRoot)
        {
            if (session.IsDisposed || session.IsDisconnected)
                return;
            session.Player = player;
            player.AddToWorld();
        }
        CLog.Success("[ClusterInterface]", "Session {0}: joueur {1} ({2}) connecté (carte {3}, instance {4}).",
            session.SessionId, player.Name, player.Guid, session.MapId, session.InstanceId);
        SendPlayerLoginResult(player.Guid, session.SessionId, LoginErrorCode.CHAR_LOGIN_SUCCESS);
    }

    private void SendPlayerLoginResult(uint guid, uint sessionId, LoginErrorCode result)
    {
        var packet = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_PLAYER_LOGIN_RESULT, 9);
        packet.WriteUInt32(guid);
        packet.WriteUInt32(sessionId);
        packet.WriteByte((byte)result);
        SendPacket(packet);
        packet.Dispose();
    }

    // ISMSG_WOW_PACKET: sessionid, opcode, size, payload — paquet client relayé par le RealmServer.
    private void HandleWoWPacket(WorldPacket packet)
    {
        uint sessionId = packet.ReadUInt32();
        ushort opcode = packet.ReadUInt16();
        uint size = packet.ReadUInt32();

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            CLog.Error("[ClusterInterface]", "HandleWoWPacket: session invalide {0}.", sessionId);
            return;
        }

        string opcodeName = NameTables.LookupName(opcode, NameTables.OpcodeSharedNames);
        CLog.Debug("[ClusterInterface]", "Transfert {0} vers le client (session {1}).", opcodeName, sessionId);

        // Créer le nouveau paquet client avec validation
        var clientPacket = new WorldPacket(opcode, (int)size);
        
        // Vérification de validité du buffer (équivalent à m_bufferPool == -1 dans le code C++)
        if (clientPacket.m_bufferPool == -1)
        {
            clientPacket.Dispose();
            CLog.Error("[ClusterInterface]", "HandleWoWPacket: Buffer invalide pour l'opcode {0}.", opcodeName);
            return;
        }

        // Si le packet a une taille valide, copier les données directement
        if (size > 0)
        {
            // Lire directement les données dans le buffer du nouveau packet
            for (int i = 0; i < size; i++)
            {
                byte b = packet.ReadByte();
                clientPacket.Append(b);
            }
        }

        // Les paquets sont ajoutés à la file IncomingPackets de la session
        // et seront traités par ClusterPlayerSession.Update() appelé depuis ClusterInterface.Update()
        session.QueuePacket(clientPacket);
    }

    // ISMSG_TELEPORT_RESULT: sessionid, flag(1=même serveur/0=serveur différent), mapid, instanceid, x, y, z, o.
    private void HandleTeleportResult(WorldPacket packet)
    {
        uint sessionId = packet.ReadUInt32();

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            SendErrorHandler(sessionId, 1);
            return;
        }

        byte sameServer = packet.ReadByte();
        uint mapId = packet.ReadUInt32();
        uint instanceId = packet.ReadUInt32();
        float x = packet.ReadFloat();
        float y = packet.ReadFloat();
        float z = packet.ReadFloat();
        float o = packet.ReadFloat();

        session.MapId = mapId;
        session.InstanceId = instanceId;

        // TODO: déclencher le changement de carte réel du joueur une fois Player/EventMgr portés.
        CLog.Debug("[ClusterInterface]", "Résultat téléport session {0}: mapid={1}, instanceid={2}, pos=({3},{4},{5},{6}), mêmeServeur={7}.",
            sessionId, mapId, instanceId, x, y, z, o, sameServer != 0);
    }

    private void SendErrorHandler(uint sessionId, byte reason)
    {
        var packet = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_ERROR_HANDLER, 5);
        packet.WriteUInt32(sessionId);
        packet.WriteByte(reason);
        SendPacket(packet);
        packet.Dispose();
    }

    // ISMSG_SESSION_REMOVED: sessionid.
    private void HandleSessionRemoved(WorldPacket packet)
    {
        uint sessionId = packet.ReadUInt32();
        if (_sessions.TryGetValue(sessionId, out var session))
            session.MarkDisconnected();
    }

    public void DestroySession(uint sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return;

        lock (session.SyncRoot)
        {
            if (!WorldSession.LogoutPlayer(session, true, false))
            {
                session.MarkDisconnected();
                session.SetLogoutTimer(10000);
                return;
            }
            if (!_sessions.TryRemove(sessionId, out _))
                return;
            _onlineGuids.TryRemove(session.Guid, out _);
            session.Dispose();
        }
        CLog.Debug("[ClusterInterface]", "Session {0} (joueur {1}) détruite.", sessionId, session.Guid);
    }

    // ISMSG_SAVE_ALL_PLAYERS: flag (inutilisé).
    private void HandleSaveAllPlayers(WorldPacket packet)
    {
        _ = packet.ReadByte();
        foreach (var session in _sessions.Values)
        {
            lock (session.SyncRoot)
                if (session.Player != null && !session.Player.SaveToDB())
                    CLog.Error("[ClusterInterface]", "Echec de sauvegarde de la session {0}.", session.SessionId);
        }
    }

    // ISMSG_TRANSPORTER_MAP_CHANGE: transporterentry, mapid, x, y, z.
    private static void HandleTransporterMapChange(WorldPacket packet)
    {
        uint transporterEntry = packet.ReadUInt32();
        uint mapId = packet.ReadUInt32();
        float x = packet.ReadFloat();
        float y = packet.ReadFloat();
        float z = packet.ReadFloat();

        // TODO: déplacer réellement le transporteur une fois Transporter/MapMgr portés.
        CLog.Debug("[ClusterInterface]", "Changement de carte du transporteur {0} vers {1} ({2},{3},{4}).", transporterEntry, mapId, x, y, z);
    }

    // ISMSG_PLAYER_TELEPORT: result(doit valoir 2), method, sessionid, mapid, instanceid, x, y, z, targetSessionId.
    private void HandlePlayerTeleport(WorldPacket packet)
    {
        byte result = packet.ReadByte();
        byte method = packet.ReadByte();
        uint sessionId = packet.ReadUInt32();
        uint mapId = packet.ReadUInt32();
        uint instanceId = packet.ReadUInt32();
        float x = packet.ReadFloat();
        float y = packet.ReadFloat();
        float z = packet.ReadFloat();
        uint targetSessionId = packet.ReadUInt32();

        if (result != 2)
        {
            CLog.Warning("[ClusterInterface]", "HandlePlayerTeleport: résultat inattendu {0}.", result);
            return;
        }

        if (!_sessions.TryGetValue(targetSessionId, out _))
        {
            CLog.Error("[ClusterInterface]", "HandlePlayerTeleport: session cible invalide {0}.", targetSessionId);
            return;
        }

        // TODO: téléporter réellement le joueur (Player.EventSafeTeleport) une fois Player/EventMgr portés.
        CLog.Debug("[ClusterInterface]", "Téléportation session {0} (méthode {1}) vers carte {2}/instance {3} ({4},{5},{6}) pour session {7}.",
            sessionId, method, mapId, instanceId, x, y, z, targetSessionId);
    }

    // ISMSG_CREATE_PLAYER: accountid, opcode, size, payload (CMSG_CHAR_CREATE brut).
    private void HandleCreatePlayer(WorldPacket packet)
    {
        uint accountId = packet.ReadUInt32();
        ushort opcode = packet.ReadUInt16();
        uint size = packet.ReadUInt32();
        if (size > 0)
            packet.ReadBytes(new byte[size], 0, (int)size); // TODO: consommer réellement le CMSG_CHAR_CREATE une fois Player/ObjectMgr portés.

        CLog.Warning("[ClusterInterface]", "Création de personnage demandée pour le compte {0} (opcode {1}), système Player non porté.", accountId, opcode);

        var result = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_CREATE_PLAYER, 5);
        result.WriteUInt32(accountId);
        result.WriteByte((byte)LoginErrorCode.CHAR_CREATE_ERROR);
        SendPacket(result);
        result.Dispose();
    }

    // ISMSG_DELETE_PLAYER: guid (uint64).
    private static void HandleDeletePlayer(WorldPacket packet)
    {
        ulong guid = packet.ReadUInt64();
        // TODO: supprimer réellement les données dépendantes (corpses, etc.) une fois WorldDatabaseManager porté.
        CLog.Debug("[ClusterInterface]", "Suppression du personnage {0} demandée.", guid);
    }

    // ISMSG_PACKED_PLAYER_INFO: realsize, puis données compressées (zlib) — nécessite RPlayerInfo.
    private static void HandlePackedPlayerInfo(WorldPacket packet)
    {
        uint realSize = packet.ReadUInt32();
        // TODO: décompresser et dépaqueter RPlayerInfo une fois cette structure partagée avec le RealmServer.
        CLog.Debug("[ClusterInterface]", "Informations joueurs compressées reçues ({0} octets décompressés attendus).", realSize);
    }

    // ISMSG_DESTROY_PLAYER_INFO: sessionid, guid.
    private void HandleDestroyPlayerInfo(WorldPacket packet)
    {
        uint sessionId = packet.ReadUInt32();
        uint guid = packet.ReadUInt32();
        if (!_sessions.ContainsKey(sessionId))
            ((ICollection<KeyValuePair<uint, uint>>)_onlineGuids).Remove(new(guid, sessionId));
        CLog.Debug("[ClusterInterface]", "Informations du joueur {0} (session {1}) supprimées.", guid, sessionId);
    }

    // ISMSG_PLAYER_INFO: guid (uint64), puis RPlayerInfo.Pack (format opaque côté RealmServer).
    private static void HandlePlayerInfo(WorldPacket packet)
    {
        ulong guid = packet.ReadUInt64();
        // TODO: dépaqueter RPlayerInfo une fois cette structure partagée avec le RealmServer.
        CLog.Debug("[ClusterInterface]", "Informations du joueur {0} reçues.", guid);
    }

    // ISMSG_CHANNEL_ACTION: action, puis champs spécifiques (JOIN/PART: guid+cid ; SAY: nom+guid+message+forGmGuid+forced).
    private static void HandleChannelAction(WorldPacket packet)
    {
        byte action = packet.ReadByte();
        switch ((MsgChannelAction)action)
        {
            case MsgChannelAction.CHANNEL_JOIN:
            case MsgChannelAction.CHANNEL_PART:
                {
                    uint guid = packet.ReadUInt32();
                    uint channelId = packet.ReadUInt32();
                    // TODO: appliquer réellement l'action une fois Channel/Player portés côté world.
                    CLog.Debug("[ClusterInterface]", "Action de canal {0} pour le joueur {1} (canal {2}).", action, guid, channelId);
                    break;
                }
            case MsgChannelAction.CHANNEL_SAY:
                {
                    string channelName = packet.ReadString();
                    uint guid = packet.ReadUInt32();
                    string message = packet.ReadString();
                    uint forGmGuid = packet.ReadUInt32();
                    bool forced = packet.ReadByte() != 0;
                    // TODO: relayer réellement le message une fois Channel/Player portés côté world.
                    CLog.Debug("[ClusterInterface]", "Message de canal '{0}' de {1}: {2} (forGm={3}, forced={4}).", channelName, guid, message, forGmGuid, forced);
                    break;
                }
            default:
                CLog.Warning("[ClusterInterface]", "HandleChannelAction: action non gérée {0}.", action);
                break;
        }
    }

    // ISMSG_CHANNEL_LFG_DUNGEON_STATUS_REQUEST: guid, dbc_id, unk, channelname, pass.
    // La réponse ICMSG_CHANNEL_LFG_DUNGEON_STATUS_REPLY utilise i=3 pour indiquer qu'aucun statut LFG n'est disponible.
    private void HandleChannelLFGDungeonStatusRequest(WorldPacket packet)
    {
        uint guid = packet.ReadUInt32();
        uint dbcId = packet.ReadUInt32();
        ushort unk = packet.ReadUInt16();
        string channelName = packet.ReadString();
        string pass = packet.ReadString();

        // TODO: renvoyer le véritable statut de donjon LFG une fois Player/LfgMgr portés.
        var reply = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_CHANNEL_LFG_DUNGEON_STATUS_REPLY, 15 + channelName.Length + pass.Length);
        reply.WriteByte(3);
        reply.WriteUInt32(guid);
        reply.WriteUInt32(dbcId);
        reply.WriteUInt16(unk);
        reply.WriteString(channelName);
        reply.WriteString(pass);
        SendPacket(reply);
        reply.Dispose();
    }


    public void ForwardWoWPacket(uint sessionId, WorldPacket packet)
    {
        if (!_registered || _socket == null || !_socket.IsConnected())
            return;

        var data = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_WOW_PACKET, packet.Size + 10);
        data.WriteUInt32(sessionId);
        data.WriteUInt16(packet.GetOpcode());
        data.WriteUInt32((uint)packet.Size);
        if (packet.Size > 0)
            data.Write(packet.Contents, 0, packet.Size);
        SendPacket(data);
        data.Dispose();
    }

    internal void NotifyPlayerLogout(uint sessionId, uint guid)
    {
        using var packet = new WorldPacket((ushort)WorkerServerOpcodes.ICMSG_PLAYER_LOGOUT, 8);
        packet.WriteUInt32(sessionId);
        packet.WriteUInt32(guid);
        SendPacket(packet);
    }

    public void DisconnectSession(uint sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.MarkDisconnected();
            SendErrorHandler(sessionId, 1);
        }
    }

    public void Update()
    {
        if (_socket == null && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= _lastConnectAttempt + 5)
            ConnectToRealmServer();

        while (_packets.TryDequeue(out var packet))
            HandlePacket(packet);

        foreach (var session in _sessions.Values)
        {
            if (session.Update(session.InstanceId) == 1)
                DestroySession(session.SessionId);
        }
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

        foreach (var sessionId in _sessions.Keys)
            DestroySession(sessionId);
    }
}
