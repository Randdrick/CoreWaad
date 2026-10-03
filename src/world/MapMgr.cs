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

namespace WaadWorldServer;

public sealed class MapMgr : Singleton<MapMgr>
{
    private readonly Logger sLog = new();

    // Portage minimal de ClusterInterface::HandleCreateInstance (MapMgr* mgr = sInstanceMgr.ClusterCreateInstance(...)).
    // TODO: charger réellement la carte/instance une fois le système de cartes (Map/InstanceMgr) porté.
    private readonly HashSet<(uint MapId, uint InstanceId)> _clusterInstances = [];
    private readonly object _clusterInstancesLock = new();

    public bool ClusterCreateInstance(uint mapId, uint instanceId)
    {
        lock (_clusterInstancesLock)
        {
            _clusterInstances.Add((mapId, instanceId));
            return true;
        }
    }

    public void Update()
    {
        // Mise à jour des cartes
    }

    public void Dispose()
    {
        sLog.OutDebug("[MapMgr] Nettoyage des cartes...");
    }
}