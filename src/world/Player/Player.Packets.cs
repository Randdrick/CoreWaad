using System;
using System.Collections.Generic;
using System.Linq;
using WaadShared;

namespace WaadWorldServer;

public sealed partial class Player
{
    private uint _nextTimeSyncSequence;
    private uint _pendingTimeSyncSequence;
    private long _timeSyncSentAtMs;
    private bool _timeSyncPending;

    public uint SyncPlayerTickCount { get; private set; }
    public uint SyncPlayerLatency { get; private set; }

    public void SendPacket(WorldPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        lock (Session.SyncRoot)
        {
            if (!Session.IsDisposed && !Session.IsDisconnected)
                ClusterInterface.Instance.ForwardWoWPacket(Session.SessionId, packet);
        }
    }

    public void SendInitialActions()
    {
        lock (Session.SyncRoot)
        {
            using var packet = BuildInitialActionsPacket();
            SendPacket(packet);
        }
    }

    internal WorldPacket BuildInitialActionsPacket()
    {
        var packet = new WorldPacket((ushort)Opcodes.SMSG_ACTION_BUTTONS, 1 + Actions.Length * 4);
        packet.WriteByte(1);
        foreach (ActionButton button in Actions)
        {
            packet.WriteUInt16(button.Action);
            packet.WriteByte(button.Type);
            packet.WriteByte(button.Misc);
        }
        return packet;
    }

    public void SendInitialSpells()
    {
        lock (Session.SyncRoot)
        {
            using var packet = BuildInitialSpellsPacket(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            SendPacket(packet);
        }
    }

    internal WorldPacket BuildInitialSpellsPacket(long nowSeconds)
    {
        if (Spells.Count > ushort.MaxValue)
            throw new InvalidOperationException("Trop de sorts pour SMSG_INITIAL_SPELLS.");

        var entries = new List<(uint Spell, ushort Item, ushort Category, uint SpellTime, uint CategoryTime)>();
        for (int type = 0; type < 2; type++)
        {
            foreach (var entry in Cooldowns[type].OrderBy(entry => entry.Key).ToArray())
            {
                long remainingSeconds = entry.Value.ExpireTime - nowSeconds;
                if (remainingSeconds < 10)
                {
                    Cooldowns[type].Remove(entry.Key);
                    continue;
                }
                uint remainingMs = (uint)Math.Min(remainingSeconds * 1000, uint.MaxValue);
                entries.Add((type == 0 ? entry.Key : entry.Value.SpellId,
                    unchecked((ushort)entry.Value.ItemId), type == 0 ? (ushort)0 : unchecked((ushort)entry.Key),
                    type == 0 ? remainingMs : 0, type == 1 ? remainingMs : 0));
            }
        }
        if (entries.Count > ushort.MaxValue)
            throw new InvalidOperationException("Trop de cooldowns pour SMSG_INITIAL_SPELLS.");

        var packet = new WorldPacket((ushort)Opcodes.SMSG_INITIAL_SPELLS, 5 + Spells.Count * 6 + entries.Count * 16);
        packet.WriteByte(0);
        packet.WriteUInt16((ushort)Spells.Count);
        foreach (uint spell in Spells.OrderBy(spell => spell))
        {
            packet.WriteUInt32(spell);
            packet.WriteUInt16(0);
        }
        packet.WriteUInt16((ushort)entries.Count);
        foreach (var (Spell, Item, Category, SpellTime, CategoryTime) in entries)
        {
            packet.WriteUInt32(Spell);
            packet.WriteUInt16(Item);
            packet.WriteUInt16(Category);
            packet.WriteUInt32(SpellTime);
            packet.WriteUInt32(CategoryTime);
        }
        return packet;
    }

    public void OnTimeSyncRequest()
    {
        lock (Session.SyncRoot)
        {
            if (Session.IsDisposed || Session.IsDisconnected || !IsInWorld)
                return;
            _pendingTimeSyncSequence = _nextTimeSyncSequence;
            _nextTimeSyncSequence = unchecked(_nextTimeSyncSequence + 1);
            _timeSyncSentAtMs = Environment.TickCount64;
            _timeSyncPending = true;
            using var packet = new WorldPacket((ushort)Opcodes.SMSG_TIME_SYNC_REQ, 4);
            packet.WriteUInt32(_pendingTimeSyncSequence);
            SendPacket(packet);
        }
    }

    public bool OnTimeSyncResponse(uint sequence, uint clientTicks)
    {
        lock (Session.SyncRoot)
        {
            if (!_timeSyncPending || !IsInWorld || Session.IsDisposed || Session.IsDisconnected || sequence != _pendingTimeSyncSequence)
                return false;
            SyncPlayerTickCount = clientTicks;
            SyncPlayerLatency = (uint)Math.Clamp((Environment.TickCount64 - _timeSyncSentAtMs) / 2, 0, uint.MaxValue);
            _timeSyncPending = false;
            return true;
        }
    }

    public void SendInitialLogonPackets()
    {
        using var packet = new WorldPacket((ushort)Opcodes.SMSG_QUEST_FORCE_REMOVE, 4);
        packet.WriteUInt32(TimeLogoff);
        SendPacket(packet);

        packet.Initialize((ushort)Opcodes.SMSG_BINDPOINTUPDATE);
        packet.WriteFloat(BindPositionX);
        packet.WriteFloat(BindPositionY);
        packet.WriteFloat(BindPositionZ);
        packet.WriteUInt32(BindMapId);
        packet.WriteUInt32(BindZoneId);
        SendPacket(packet);

        packet.Initialize((ushort)Opcodes.SMSG_TUTORIAL_FLAGS);
        foreach (uint tutorial in Tutorials)
            packet.WriteUInt32(tutorial);
        SendPacket(packet);

        SendInitialSpells();
        packet.Initialize((ushort)Opcodes.SMSG_SEND_UNLEARN_SPELLS);
        packet.WriteUInt32(0);
        SendPacket(packet);
        SendInitialActions();

        packet.Initialize((ushort)Opcodes.SMSG_LOGIN_SETTIMESPEED);
        packet.WriteUInt32(PackGameTime(DateTime.Now));
        packet.WriteFloat(0.0166666669777748f);
        packet.WriteUInt32(0);
        SendPacket(packet);
    }

    public static uint PackGameTime(DateTime time)
    {
        uint weekday = ((uint)time.DayOfWeek + 6) % 7;
        return (uint)time.Minute
            | ((uint)time.Hour << 6)
            | (weekday << 11)
            | ((uint)(time.Day - 1) << 14)
            | ((uint)(time.Month - 1) << 20)
            | ((uint)((time.Year - 2000) & 31) << 24);
    }
}