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

using System.Collections.Generic;
using WaadShared;
using WaadShared.Config;

using static DBCStores;

namespace WaadRealmServer;

public class Channel
{

    private static DBCStorage<ChatChannelDBC> DbcChatChannels { get; } = new DBCStorage<ChatChannelDBC>();
    private static List<string> bannedChannels = [];
    private static readonly object confSettingLock = new();
    private readonly object memberLock = new();
    private readonly Dictionary<RPlayerInfo, uint> members = [];
    private readonly HashSet<uint> bannedMembers = [];
    private static readonly ConfigMgr Config = new();
    public string Name { get; set; }
    public string Password { get; set; }
    public byte Flags { get; set; }
    public uint TypeId { get; set; }
    public bool General { get; set; }
    public bool Muted { get; set; }
    public bool Announce { get; set; }
    public uint Team { get; set; }
    public uint MinimumLevel { get; set; }
    public uint ChannelId { get; set; }
    public bool Deleted { get; set; }

    // DBC integration
    public ChatChannelDBC? PDBC { get; set; }

    public int GetNumMembers() { lock (memberLock) { return members.Count; } }

    public static void LoadConfSettings()
    {
        string bannedChannelsStr = Config.MainConfig.GetString("Channels", "BannedChannels");
        lock (confSettingLock)
        {
            bannedChannels = [.. bannedChannelsStr.Split(';')];
        }
    }

    public Channel(string name, uint team, uint typeId, uint id)
    {
        Name = name;
        Team = team;
        TypeId = typeId;
        ChannelId = id;
        Flags = 0;
        Announce = true;
        Muted = false;
        General = false;
        Deleted = false;
        MinimumLevel = 1;

        // DBC lookup
        if (DbcChatChannels != null &&
            DbcChatChannels.LookupEntry(typeId).id != 0)
        {
            PDBC = DbcChatChannels.LookupEntryForced(typeId);
            General = true;
            Announce = false;
            Flags |= (byte)ChannelFlags.Custom; // General flag (0x10)
            // flags (0x08 = trade, 0x20 = city, 0x40 = lfg, 0x80 = voice)
            if (((PDBC.Value.flags & 0x00000008) != 0) && (PDBC.Value.flags & 0x00000020) == 0)
                Flags |= 0x08;
            if ((PDBC.Value.flags & 0x00000010) != 0 || (PDBC.Value.flags & 0x00000020) != 0)
                Flags |= 0x20;
            if ((PDBC.Value.flags & 0x00040000) != 0)
                Flags |= 0x40;
            if ((PDBC.Value.flags & 0x00080000) != 0)
                Flags |= 0x80;
        }
        else
        {
            Flags = (byte)ChannelFlags.Custom;
        }
    }

    public bool HasMember(RPlayerInfo playerInfo)
    {
        lock (memberLock)
        {
            return members.ContainsKey(playerInfo);
        }
    }

    public void AttemptJoin(RPlayerInfo plr, string password)
    {
        if (plr == null)
            return;
        if (plr.Session == null)
            return;
        lock (memberLock)
        {
            if (bannedMembers.Contains(plr.Id))
            {
                SendNotOn(plr);
                return;
            }
            if (!string.IsNullOrEmpty(Password) && Password != password)
            {
                SendNotOn(plr);
                return;
            }
            if (members.ContainsKey(plr))
            {
                SendAlreadyOn(plr, plr);
                return;
            }
            uint flags = members.Count == 0 ? (uint)ChannelFlags.Owner : (uint)ChannelFlags.None;
            members.Add(plr, flags);
            if (Announce)
            {
                var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
                data.WriteByte((byte)ChannelNotifyFlags.Joined);
                data.WriteString(Name);
                data.WriteUInt32(plr.Id);
                SendToAll(data);
            }
        }
    }

    public void Part(RPlayerInfo plr, bool silent)
    {
        if (plr == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint flags))
                return;
            members.Remove(plr);
            if (!silent && Announce)
            {
                var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
                data.WriteByte((byte)ChannelNotifyFlags.Left);
                data.WriteString(Name);
                data.WriteUInt32(plr.Id);
                SendToAll(data);
            }
        }
    }

    public void Kick(RPlayerInfo plr, RPlayerInfo diePlayer, bool ban)
    {
        if (plr == null || diePlayer == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags) || !members.TryGetValue(diePlayer, out _))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.Kicked);
            data.WriteString(Name);
            data.WriteUInt32(diePlayer.Id);
            SendToAll(data);
            if (ban)
                bannedMembers.Add(diePlayer.Id);
            members.Remove(diePlayer);
            // Optionally: send YouLeft to diePlayer
            var data2 = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data2.WriteByte((byte)ChannelNotifyFlags.YouLeft);
            data2.WriteString(Name);
            data2.WriteUInt32(TypeId);
            data2.WriteUInt32(0);
            data2.WriteByte(0);
            diePlayer.GetSession().SendPacket(data2);
        }
    }

    public void Invite(RPlayerInfo plr, RPlayerInfo newPlayer)
    {
        if (plr == null || newPlayer == null)
            return;
        lock (memberLock)
        {
            if (!members.ContainsKey(plr))
                return;
            if (members.ContainsKey(newPlayer))
            {
                SendAlreadyOn(plr, newPlayer);
                return;
            }
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.Invited);
            data.WriteString(Name);
            data.WriteUInt32(plr.Id);
            newPlayer.GetSession().SendPacket(data);

            data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.YouInvited);
            data.WriteString(Name);
            data.WriteUInt32(newPlayer.Id);
            plr.GetSession().SendPacket(data);
        }
    }

    public void Moderate(RPlayerInfo plr)
    {
        if (plr == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            Muted = !Muted;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)(Muted ? ChannelNotifyFlags.Moderated : ChannelNotifyFlags.Unmoderated));
            data.WriteString(Name);
            data.WriteUInt32(plr.Id);
            SendToAll(data);
        }
    }

    public void Mute(RPlayerInfo plr, RPlayerInfo diePlayer)
    {
        if (plr == null || diePlayer == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags) || !members.TryGetValue(diePlayer, out uint oldflags))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            members[diePlayer] |= (uint)ChannelFlags.Muted;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.ModeChange);
            data.WriteString(Name);
            data.WriteUInt32(diePlayer.Id);
            data.WriteByte((byte)oldflags);
            data.WriteByte((byte)(oldflags | (uint)ChannelFlags.Muted));
            SendToAll(data);
        }
    }

    public void Voice(RPlayerInfo plr, RPlayerInfo vPlayer)
    {
        if (plr == null || vPlayer == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags) || !members.TryGetValue(vPlayer, out uint oldflags))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            members[vPlayer] |= (uint)ChannelFlags.Voiced;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.ModeChange);
            data.WriteString(Name);
            data.WriteUInt32(vPlayer.Id);
            data.WriteByte((byte)oldflags);
            data.WriteByte((byte)(oldflags | (uint)ChannelFlags.Voiced));
            SendToAll(data);
        }
    }

    public void Unmute(RPlayerInfo plr, RPlayerInfo diePlayer)
    {
        if (plr == null || diePlayer == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags) || !members.TryGetValue(diePlayer, out uint oldflags))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            members[diePlayer] &= ~(uint)ChannelFlags.Muted;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.ModeChange);
            data.WriteString(Name);
            data.WriteUInt32(diePlayer.Id);
            data.WriteByte((byte)oldflags);
            data.WriteByte((byte)(oldflags & ~(uint)ChannelFlags.Muted));
            SendToAll(data);
        }
    }

    public void Devoice(RPlayerInfo plr, RPlayerInfo vPlayer)
    {
        if (plr == null || vPlayer == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags) || !members.TryGetValue(vPlayer, out uint oldflags))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            members[vPlayer] &= ~(uint)ChannelFlags.Voiced;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.ModeChange);
            data.WriteString(Name);
            data.WriteUInt32(vPlayer.Id);
            data.WriteByte((byte)oldflags);
            data.WriteByte((byte)(oldflags & ~(uint)ChannelFlags.Voiced));
            SendToAll(data);
        }
    }

    public void Say(RPlayerInfo plr, string message, RPlayerInfo forGmClient, bool forced)
    {
        if (plr == null)
            return;
        lock (memberLock)
        {
            if (!forced && !members.ContainsKey(plr))
                return;
            if (plr.Level < MinimumLevel)
                return;

            var packet = new WorldPacket((ushort)Opcodes.SMSG_MESSAGECHAT, 100 + message.Length);
            packet.WriteByte(14); // CHAT_MSG_CHANNEL
            packet.WriteUInt32(0); // language
            packet.WriteUInt32(plr.Id); // sender guid (low part)
            packet.WriteUInt32(0); // zone/unused
            packet.WriteString(Name); // channel name
            packet.WriteUInt32(plr.Id); // sender guid (low part again)
            packet.WriteUInt32((uint)(message.Length + 1)); // message length (with null)
            packet.WriteString(message); // message
            packet.WriteByte((byte)(!string.IsNullOrEmpty(plr.GMPermissions) ? 4 : 0)); // GM flag
            if (forGmClient != null)
            {
                forGmClient.GetSession().SendPacket(packet);
            }
            else
            {
                SendToAll(packet);
            }
        }
    }

    public void Unban(RPlayerInfo plr, RPlayerInfo bplr)
    {
        if (plr == null || bplr == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            if (!bannedMembers.Contains(bplr.Id))
                return;
            bannedMembers.Remove(bplr.Id);
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.Unbanned);
            data.WriteString(Name);
            data.WriteUInt32(bplr.Id);
            SendToAll(data);
        }
    }

    public void GiveModerator(RPlayerInfo plr, RPlayerInfo newPlayer)
    {
        if (plr == null || newPlayer == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags) || !members.TryGetValue(newPlayer, out uint oldflags))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            members[newPlayer] |= (uint)ChannelFlags.Moderator;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.ModeChange);
            data.WriteString(Name);
            data.WriteUInt32(newPlayer.Id);
            data.WriteByte((byte)oldflags);
            data.WriteByte((byte)(oldflags | (uint)ChannelFlags.Moderator));
            SendToAll(data);
        }
    }

    public void TakeModerator(RPlayerInfo plr, RPlayerInfo newPlayer)
    {
        if (plr == null || newPlayer == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint plrFlags) || !members.TryGetValue(newPlayer, out uint oldflags))
                return;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            members[newPlayer] &= ~(uint)ChannelFlags.Moderator;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.ModeChange);
            data.WriteString(Name);
            data.WriteUInt32(newPlayer.Id);
            data.WriteByte((byte)oldflags);
            data.WriteByte((byte)(oldflags & ~(uint)ChannelFlags.Moderator));
            SendToAll(data);
        }
    }

    public void AnnounceMethod(RPlayerInfo plr)
    {
        if (plr == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint value))
                return;
            uint plrFlags = value;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            Announce = !Announce;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)(Announce ? ChannelNotifyFlags.EnableAnnounce : ChannelNotifyFlags.DisableAnnounce));
            data.WriteString(Name);
            data.WriteUInt32(plr.Id);
            SendToAll(data);
        }
    }

    public void PasswordMethod(RPlayerInfo plr, string pass)
    {
        if (plr == null)
            return;
        lock (memberLock)
        {
            if (!members.TryGetValue(plr, out uint value))
                return;
            uint plrFlags = value;
            if ((plrFlags & (uint)ChannelFlags.Owner) == 0 && (plrFlags & (uint)ChannelFlags.Moderator) == 0)
                return;
            Password = pass;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.SetPass);
            data.WriteString(Name);
            data.WriteUInt32(plr.Id);
            SendToAll(data);
        }
    }

    public void List(RPlayerInfo plr)
    {
        if (plr == null)
            return;
        var packet = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_LIST, 50 + (members.Count * 9));
        lock (memberLock)
        {
            packet.WriteByte(1); // always 1 for channel list
            packet.WriteString(Name);
            packet.WriteByte(Flags);
            packet.WriteUInt32((uint)members.Count);
            foreach (var member in members.Keys)
            {
                packet.WriteString(member.Name);
            }
        }
        plr.GetSession().SendPacket(packet);
    }

    public void GetOwner(RPlayerInfo plr)
    {
        if (plr == null)
            return;
        lock (memberLock)
        {
            foreach (var kvp in members)
            {
                if ((kvp.Value & (uint)ChannelFlags.Owner) != 0)
                {
                    var packet = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
                    packet.WriteByte((byte)ChannelNotifyFlags.WhoOwner);
                    packet.WriteString(Name);
                    packet.WriteUInt32(kvp.Key.Id);
                    plr.GetSession().SendPacket(packet);
                    return;
                }
            }
        }
    }

    public void SetOwner(RPlayerInfo oldpl, RPlayerInfo plr)
    {
        if (plr == null)
            return;
        lock (memberLock)
        {
            RPlayerInfo pOwner = null;
            uint oldflags = 0, oldflags2 = 0;
            if (oldpl != null && members.TryGetValue(oldpl, out uint value))
            {
                oldflags = value;
                members[oldpl] &= ~(uint)ChannelFlags.Owner;
            }
            if (members.TryGetValue(plr, out uint value2))
            {
                oldflags2 = value2;
                members[plr] |= (uint)ChannelFlags.Owner;
                pOwner = plr;
            }
            if (pOwner == null)
                return;
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data.WriteByte((byte)ChannelNotifyFlags.ChangeOwner);
            data.WriteString(Name);
            data.WriteUInt32(pOwner.Id);
            SendToAll(data);

            // send the mode changes
            var data2 = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
            data2.WriteByte((byte)ChannelNotifyFlags.ModeChange);
            data2.WriteString(Name);
            data2.WriteUInt32(pOwner.Id);
            data2.WriteByte((byte)oldflags2);
            data2.WriteByte((byte)(oldflags2 | (uint)ChannelFlags.Owner));
            SendToAll(data2);
        }
    }

    public void SendAlreadyOn(RPlayerInfo plr, RPlayerInfo plr2)
    {
        var packet = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
        packet.WriteByte((byte)ChannelNotifyFlags.AlreadyOn);
        packet.WriteString(Name);
        packet.WriteUInt32(plr2.Id); // Use WriteUInt32 for player id
        plr.GetSession().SendPacket(packet);
    }

    public void SendNotOn(RPlayerInfo plr)
    {
        var packet = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_NOTIFY, 100);
        packet.WriteByte((byte)ChannelNotifyFlags.NotOn);
        packet.WriteString(Name);
        plr.GetSession().SendPacket(packet);
    }

    public void SendToAll(WorldPacket data)
    {
        lock (memberLock)
        {
            foreach (var member in members.Keys)
            {
                member.GetSession().SendPacket(data);
            }
        }
    }

    public void SendToAll(WorldPacket data, RPlayerInfo plr)
    {
        lock (memberLock)
        {
            foreach (var member in members.Keys)
            {
                if (member != plr)
                    member.GetSession().SendPacket(data);
            }
        }
    }
    // Ajoutez ici les méthodes pour la gestion de la voix si besoin
}
