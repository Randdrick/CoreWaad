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
using System.Collections.Generic;
using WaadShared;

using static WaadShared.Master;

namespace WaadRealmServer;

public class ChannelMgr : IDisposable
{
    private static Lazy<ChannelMgr> _instance = new(() => new ChannelMgr());
    private bool _disposed = false;
    public static ChannelMgr Instance => _instance.Value;

    // For static usage compatibility
    public static ChannelMgr GetSingleton() => Instance;
    public static Channel GetOrCreateChannel(string name, RPlayerInfo p, uint typeId) => Instance.GetCreateChannelInternal(name, p, typeId);
    public static Channel GetChannel(string name, RPlayerInfo p) => Instance.GetChannelByPlayer(name, p);
    public static Channel GetChannelByTeam(string name, uint team) => Instance.GetChannelByTeamInternal(name, team);
    public static Channel GetChannel(uint id) => Instance.GetChannelById(id);
    public static void RemoveChannel(Channel chn) => Instance.RemoveChannelInternal(chn);

    // Instance fields
    private readonly Dictionary<uint, Channel> m_idToChannel = [];
    private uint m_idHigh = 1;
    private readonly Dictionary<string, Channel>[] Channels = { [], [] }; // 0: alliance, 1: horde
    private readonly object _lock = new();
    public bool SeperateChannels = false;

    public ChannelMgr() { }

    private Channel GetCreateChannelInternal(string name, RPlayerInfo p, uint typeId)
    {
        if (string.IsNullOrEmpty(name) || p == null)
            return null;
        uint team = p.Team; // RPlayerInfo.Team is uint
        lock (_lock)
        {
            if (!Channels[team].TryGetValue(name, out var chn))
            {
                chn = new Channel(name, m_idHigh, typeId, team);
                Channels[team][name] = chn;
                m_idToChannel[m_idHigh] = chn;
                m_idHigh++;
            }
            return chn;
        }
    }

    private Channel GetChannelByPlayer(string name, RPlayerInfo p)
    {
        if (string.IsNullOrEmpty(name) || p == null)
            return null;
        uint team = p.Team;
        lock (_lock)
        {
            Channels[team].TryGetValue(name, out var chn);
            return chn;
        }
    }

    private Channel GetChannelByTeamInternal(string name, uint team)
    {
        lock (_lock)
        {
            Channels[team].TryGetValue(name, out var chn);
            return chn;
        }
    }

    private Channel GetChannelById(uint id)
    {
        lock (_lock)
        {
            m_idToChannel.TryGetValue(id, out var chn);
            return chn;
        }
    }

    private void RemoveChannelInternal(Channel chn)
    {
        if (chn == null) return;
        lock (_lock)
        {
            Channels[chn.Team].Remove(chn.Name);
            m_idToChannel.Remove(chn.ChannelId);
        }
    }
    public static void ResetInstance()
    {
        _instance = null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Libérer les ressources managées ici
                CLog.Notice("[~ChannelMgr]", R_N_MASTER_18);
            }
            _disposed = true;
        }
    }

    ~ChannelMgr()
    {
        Dispose(false);
    }
}
