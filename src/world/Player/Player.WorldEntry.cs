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
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using WaadShared;

namespace WaadWorldServer;

// Portage de Player::OnPrePushToWorld / PushCreationData / PushUpdateData / ProcessPendingUpdates.
public sealed partial class Player
{
    private const int MaxUpdateBufferSize = 63000;
    private const int ReputationListSize = 128;
    private const byte UpdateTypeCreateObject2 = 3;
    private const byte UpdateTypeOutOfRange = 4;
    private const byte TypeIdPlayer = 4;
    private const ushort UpdateFlagSelf = 0x0001;
    private const ushort UpdateFlagLiving = 0x0020;

    // Verrou feuille : les autres joueurs y écrivent sans prendre Session.SyncRoot (évite les interblocages).
    private readonly object _bufferLock = new();
    private readonly MemoryStream _creationBuffer = new();
    private readonly MemoryStream _updateBuffer = new();
    private readonly Queue<byte[]> _sealedPayloads = new();
    private readonly ConcurrentQueue<(ushort Opcode, byte[] Data)> _delayedPackets = new();
    private uint _creationCount;
    private uint _updateCount;

    // Vitesses par défaut : marche, course, course arrière, nage, nage arrière, vol, vol arrière, rotation, tangage.
    private static readonly float[] DefaultSpeeds = [2.5f, 7.0f, 4.5f, 4.722222f, 2.5f, 7.0f, 4.5f, 3.141594f, 3.141594f];

    // Champs publics (visibles par les autres joueurs), d'après les flags de UpdateFields.cs.
    private static readonly PlayerUpdateMask VisibleMask = CreateVisibleMask();

    private static PlayerUpdateMask CreateVisibleMask()
    {
        var mask = new PlayerUpdateMask(EPlayerFields.PLAYER_END);
        void Range(int first, int last)
        {
            for (int index = first; index <= last; index++)
                mask.SetBit(index);
        }

        const int o = EObjectFields.OBJECT_END;
        Range(0, EObjectFields.OBJECT_FIELD_SCALE_X);
        Range(o + 0x00, o + 0x03); // CHARM, SUMMON
        Range(o + 0x06, o + 0x28); // CHARMEDBY .. POWER_REGEN_FLAT_MODIFIER
        Range(o + 0x30, o + 0x39); // LEVEL .. BASEATTACKTIME
        Range(o + 0x3B, o + 0x3F); // BOUNDINGRADIUS .. MOUNTDISPLAYID
        Range(o + 0x44, o + 0x46); // BYTES_1 .. PET_NAME_TIMESTAMP
        Range(o + 0x49, o + 0x4D); // DYNAMIC_FLAGS .. NPC_EMOTESTATE
        mask.SetBit(o + 0x72);     // BASE_MANA
        mask.SetBit(o + 0x74);     // BYTES_2
        mask.SetBit(o + 0x8C);     // HOVERHEIGHT
        Range(EPlayerFields.PLAYER_DUEL_ARBITER, EPlayerFields.PLAYER_GUILD_TIMESTAMP);
        Range(EPlayerFields.PLAYER_VISIBLE_ITEM_1_ENTRYID, EPlayerFields.PLAYER_FAKE_INEBRIATION);
        return mask;
    }

    internal void OnPrePushToWorld()
    {
        SendWorldVerification();
        SendInitialLogonPackets();
    }

    // Ordre attendu par le client 3.3.5a : LOGIN_VERIFY_WORLD en premier.
    private void SendWorldVerification()
    {
        using var packet = new WorldPacket((ushort)Opcodes.SMSG_LOGIN_VERIFY_WORLD, 20);
        packet.WriteUInt32(MapId);
        packet.WriteFloat(PositionX);
        packet.WriteFloat(PositionY);
        packet.WriteFloat(PositionZ);
        packet.WriteFloat(Orientation);
        SendPacket(packet);

        packet.Initialize((ushort)Opcodes.SMSG_FEATURE_SYSTEM_STATUS);
        packet.WriteByte(2);
        packet.WriteByte(0);
        SendPacket(packet);

        string[] lines = WorldConfig.Motd.Split('@', StringSplitOptions.RemoveEmptyEntries);
        packet.Initialize((ushort)Opcodes.SMSG_MOTD);
        packet.WriteUInt32((uint)lines.Length);
        foreach (string line in lines)
            packet.WriteString(line);
        SendPacket(packet);
    }

    public void SendInitialFactions()
    {
        lock (Session.SyncRoot)
        {
            using var packet = BuildInitialFactionsPacket();
            SendPacket(packet);
        }
    }

    internal WorldPacket BuildInitialFactionsPacket()
    {
        var flags = new byte[ReputationListSize];
        var standings = new int[ReputationListSize];
        ObjectMgr objectMgr = ObjectMgr.GetInstance();
        foreach (var (factionId, reputation) in Reputation)
        {
            if (!objectMgr.TryGetFaction(factionId, out ObjectMgr.FactionReputationEntry faction)
                || faction.RepListId < 0 || faction.RepListId >= ReputationListSize)
                continue;

            flags[faction.RepListId] = (byte)reputation.Flag;
            // Équivalent de FactionReputation::CalcStanding() : le client ajoute lui-même la base DBC.
            standings[faction.RepListId] = reputation.Standing - reputation.BaseStanding;
        }

        var packet = new WorldPacket((ushort)Opcodes.SMSG_INITIALIZE_FACTIONS, 4 + ReputationListSize * 5);
        packet.WriteUInt32(ReputationListSize);
        for (int index = 0; index < ReputationListSize; index++)
        {
            packet.WriteByte(flags[index]);
            packet.WriteInt32(standings[index]);
        }
        return packet;
    }

    // Les joueurs chargés depuis la base n'ont pas ces champs publics initialisés.
    private void EnsureVisualDefaults()
    {
        if (GetFloatValue(EObjectFields.OBJECT_FIELD_SCALE_X) == 0f)
            SetFloatValue(EObjectFields.OBJECT_FIELD_SCALE_X, 1.0f);
        if (GetFloatValue(EUnitFields.UNIT_FIELD_BOUNDINGRADIUS) == 0f)
            SetFloatValue(EUnitFields.UNIT_FIELD_BOUNDINGRADIUS, 0.388999998569489f);
        if (GetFloatValue(EUnitFields.UNIT_FIELD_COMBATREACH) == 0f)
            SetFloatValue(EUnitFields.UNIT_FIELD_COMBATREACH, 1.5f);
    }

    // Bloc CREATE_OBJECT2 : forSelf = tous les champs non nuls, sinon uniquement les champs publics.
    internal WorldPacket BuildCreateUpdatePacket(bool forSelf = true)
    {
        var mask = new PlayerUpdateMask(EPlayerFields.PLAYER_END);
        for (int index = 0; index < EPlayerFields.PLAYER_END; index++)
            if (Uint32Values[index] != 0 && (forSelf || VisibleMask.IsSet(index)))
                mask.SetBit(index);

        var packet = new WorldPacket((ushort)Opcodes.SMSG_UPDATE_OBJECT,
            64 + DefaultSpeeds.Length * sizeof(float) + mask.WordCount * sizeof(uint) + mask.BitCount * sizeof(uint));
        packet.WriteUInt32(1);
        packet.WriteByte(UpdateTypeCreateObject2);
        WritePackedGuid(packet, Guid);
        packet.WriteByte(TypeIdPlayer);

        packet.WriteUInt16(forSelf ? (ushort)(UpdateFlagSelf | UpdateFlagLiving) : UpdateFlagLiving);
        packet.WriteUInt32(0); // move flags
        packet.WriteUInt16(0); // move flags 2
        packet.WriteUInt32((uint)Environment.TickCount64);
        packet.WriteFloat(PositionX);
        packet.WriteFloat(PositionY);
        packet.WriteFloat(PositionZ);
        packet.WriteFloat(Orientation);
        packet.WriteUInt32(0); // fall time
        foreach (float speed in DefaultSpeeds)
            packet.WriteFloat(speed);

        packet.WriteByte((byte)mask.WordCount);
        for (int wordIndex = 0; wordIndex < mask.WordCount; wordIndex++)
            packet.WriteUInt32(mask.GetWord(wordIndex));
        for (int fieldIndex = 0; fieldIndex < mask.BitCount; fieldIndex++)
            if (mask.IsSet(fieldIndex))
                packet.WriteUInt32(Uint32Values[fieldIndex]);

        return packet;
    }

    // Envoie le CREATE initial ; l'appelant marque ensuite le joueur comme présent en monde.
    private void SendCreateForSelf()
    {
        EnsureVisualDefaults();
        PushItemCreates();
        using WorldPacket packet = BuildCreateUpdatePacket();
        PushCreationData(packet.Contents.AsSpan(4), 1);
        ClearUpdateMask();
        ProcessPendingUpdates();
    }

    // Appelés par WorldMap : ce joueur devient visible / invisible pour viewer.
    internal void ShowTo(Player viewer)
    {
        EnsureVisualDefaults();
        using WorldPacket packet = BuildCreateUpdatePacket(forSelf: false);
        viewer.PushCreationData(packet.Contents.AsSpan(4), 1);
    }

    internal void HideFrom(Player viewer)
    {
        var block = new List<byte> { UpdateTypeOutOfRange, 1, 0, 0, 0 };
        byte mask = 0;
        for (int byteIndex = 0; byteIndex < sizeof(uint); byteIndex++)
            if (((Guid >> (byteIndex * 8)) & 0xFF) != 0)
                mask |= (byte)(1 << byteIndex);
        block.Add(mask);
        for (int byteIndex = 0; byteIndex < sizeof(uint); byteIndex++)
            if ((mask & (1 << byteIndex)) != 0)
                block.Add((byte)(Guid >> (byteIndex * 8)));
        viewer.PushCreationData([.. block], 1);
    }

    // Paquet complet envoyé tel quel au prochain ProcessPendingUpdates du joueur.
    internal void QueueDelayedPacket(ushort opcode, byte[] data) => _delayedPackets.Enqueue((opcode, data));

    // data : blocs sans l'entête de comptage (uint32) du paquet SMSG_UPDATE_OBJECT.
    internal void PushCreationData(ReadOnlySpan<byte> data, uint blockCount)
    {
        lock (_bufferLock)
            PushBlocks(_creationBuffer, ref _creationCount, data, blockCount);
    }

    internal void PushUpdateData(ReadOnlySpan<byte> data, uint blockCount)
    {
        lock (_bufferLock)
            PushBlocks(_updateBuffer, ref _updateCount, data, blockCount);
    }

    private void PushBlocks(MemoryStream buffer, ref uint count, ReadOnlySpan<byte> data, uint blockCount)
    {
        // Le client limite les paquets serveur à 2^16 octets.
        if (buffer.Length + data.Length >= MaxUpdateBufferSize)
            SealBuffer(buffer, ref count);

        buffer.Write(data);
        count += blockCount;
    }

    private void SealBuffer(MemoryStream buffer, ref uint count)
    {
        if (count == 0)
            return;

        byte[] payload = new byte[sizeof(uint) + (int)buffer.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, count);
        buffer.GetBuffer().AsSpan(0, (int)buffer.Length).CopyTo(payload.AsSpan(sizeof(uint)));
        buffer.SetLength(0);
        count = 0;
        _sealedPayloads.Enqueue(payload);
    }

    // À appeler depuis le thread propriétaire de la session.
    internal void ProcessPendingUpdates()
    {
        byte[][] payloads;
        lock (_bufferLock)
        {
            SealBuffer(_creationBuffer, ref _creationCount);
            SealBuffer(_updateBuffer, ref _updateCount);
            payloads = [.. _sealedPayloads];
            _sealedPayloads.Clear();
        }

        bool canSend = !Session.IsDisposed && !Session.IsDisconnected;
        if (canSend)
        {
            foreach (byte[] payload in payloads)
                SendUpdatePayload(payload);
        }

        while (_delayedPackets.TryDequeue(out var delayed))
        {
            if (!canSend)
                continue;
            using var delayedPacket = new WorldPacket(delayed.Opcode, delayed.Data.Length);
            delayedPacket.WriteBytes(delayed.Data);
            SendPacket(delayedPacket);
        }
    }

    private void ClearPendingUpdates()
    {
        lock (_bufferLock)
        {
            _creationBuffer.SetLength(0);
            _updateBuffer.SetLength(0);
            _creationCount = 0;
            _updateCount = 0;
            _sealedPayloads.Clear();
        }
        _delayedPackets.Clear();
    }

    private void SendUpdatePayload(byte[] payload)
    {
        if (payload.Length >= WorldConfig.CompressionThreshold)
        {
            byte[] compressed = Compress(payload);
            if (compressed.Length + sizeof(uint) < payload.Length)
            {
                using var compressedPacket = new WorldPacket((ushort)Opcodes.SMSG_COMPRESSED_UPDATE_OBJECT, compressed.Length + sizeof(uint));
                compressedPacket.WriteUInt32((uint)payload.Length);
                compressedPacket.WriteBytes(compressed);
                SendPacket(compressedPacket);
                return;
            }
        }

        using var packet = new WorldPacket((ushort)Opcodes.SMSG_UPDATE_OBJECT, payload.Length);
        packet.WriteBytes(payload);
        SendPacket(packet);
    }

    private static byte[] Compress(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(payload);
        return output.ToArray();
    }
}
