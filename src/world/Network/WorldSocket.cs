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

using WaadShared;
using WaadShared.Network;

namespace WaadWorldServer;

public class WorldSocket : Socket
{
    public static int SendBufferSize { get; set; } = 131078;
    public static int RecvBufferSize { get; set; } = 16384;

    public WorldSocket(System.Net.Sockets.Socket socket)
        : base(socket, SendBufferSize, RecvBufferSize)
    {
    }

    public override void OnConnectVirtual()
    {
        // TODO: démarrer la session client (auth, handshake) une fois WorldSession porté.
        CLog.Debug("[WorldSocket]", "Nouvelle connexion depuis {0}", GetRemoteIP());
    }

    public override void OnDisconnect()
    {
        CLog.Debug("[WorldSocket]", "Déconnexion de {0}", GetRemoteIP());
    }

    public override void OnRead()
    {
        // TODO: décoder les paquets clients une fois WorldSession porté.
    }
}