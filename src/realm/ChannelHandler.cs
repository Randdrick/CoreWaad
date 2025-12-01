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
using WaadShared;

namespace WaadRealmServer;

public partial class Session
{
    private void HandleChannelJoin(WorldPacket recvPacket)
    {
        uint dbc_id = recvPacket.ReadUInt32();
        byte crap1 = recvPacket.ReadByte();
        byte crap2 = recvPacket.ReadByte();
        ushort crap = (ushort)((crap2 << 8) | crap1); // Simulate ReadUInt16
        string channelname = recvPacket.ReadString();
        string pass = recvPacket.ReadString();

        if (CurrentPlayer == null)
            return;

        // LFG channel restriction logic (based on C++ logic)
        if (dbc_id == (uint)ChannelType.RechercheGroupe && !Master.LfgForNonLfg)
        {
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_CHANNEL_LFG_DUNGEON_STATUS_REQUEST, 4 + 4 + 2 + channelname.Length + pass.Length);
            data.WriteUInt32(CurrentPlayer.Guid & 0xFFFFFFFF);
            data.WriteUInt32((CurrentPlayer.Guid >> 32) & 0xFFFFFFFF);
            data.WriteUInt32(dbc_id);
            data.WriteUInt16(crap);
            data.WriteString(channelname);
            data.WriteString(pass);
            GetServer()?.GetType().GetMethod("SendPacket")?.Invoke(GetServer(), [data]);
            return;
        }

        // GM channel restriction logic (based on C++ logic)
        if (!string.IsNullOrEmpty(Master.GmClientChannelName) &&
            string.Equals(Master.GmClientChannelName, channelname, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(GMPermissions))
        {
            // Player is not a GM, block join
            return;
        }

        var chn = ChannelMgr.GetOrCreateChannel(channelname, CurrentPlayer, dbc_id);
        if (chn == null)
            return;
        chn.AttemptJoin(CurrentPlayer, pass);
    }

    private void HandleChannelLeave(WorldPacket recvPacket)
    {
        uint code = recvPacket.ReadUInt32();
        string channelname = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        if (chn == null) return;
        chn.Part(CurrentPlayer, false);
    }

    private void HandleChannelList(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        chn?.List(CurrentPlayer);
    }

    private void HandleChannelPassword(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string pass = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        chn?.PasswordMethod(CurrentPlayer, pass);
    }

    private void HandleChannelSetOwner(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.SetOwner(CurrentPlayer, plr);
    }

    private void HandleChannelOwner(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        chn?.GetOwner(CurrentPlayer);
    }

    private void HandleChannelModerator(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.GiveModerator(CurrentPlayer, plr);
    }

    private void HandleChannelUnmoderator( WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.TakeModerator(CurrentPlayer, plr);
    }

    private void HandleChannelMute(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.Mute(CurrentPlayer, plr);
    }

    private void HandleChannelUnmute(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.Unmute(CurrentPlayer, plr);
    }

    private void HandleChannelInvite(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.Invite(CurrentPlayer, plr);
    }

    private void HandleChannelKick(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.Kick(CurrentPlayer, plr, false);
    }

    private void HandleChannelBan(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.Kick(CurrentPlayer, plr, true);
    }

    private void HandleChannelUnban(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        string newp = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        var plr = ClientMgr.Instance.GetRPlayer(newp);
        if (chn != null && plr != null)
            chn.Unban(CurrentPlayer, plr);
    }

    private void HandleChannelAnnounce(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        chn?.AnnounceMethod(CurrentPlayer);
    }

    private void HandleChannelModerate(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        chn?.Moderate(CurrentPlayer);
    }

    private void HandleChannelRosterQuery( WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        chn?.List(CurrentPlayer);
    }

    private void HandleChannelNumMembersQuery(WorldPacket recvPacket)
    {
        string channelname = recvPacket.ReadString();
        var chn = ChannelMgr.GetChannel(channelname, CurrentPlayer);
        if (chn != null)
        {
            var packet = new WorldPacket((ushort)Opcodes.SMSG_CHANNEL_MEMBER_COUNT, 8 + channelname.Length);
            packet.WriteString(channelname);
            packet.WriteByte(chn.Flags);
            packet.WriteUInt32((uint)chn.GetNumMembers());
            Wss.SendPacket(packet);
        }
    }
}
