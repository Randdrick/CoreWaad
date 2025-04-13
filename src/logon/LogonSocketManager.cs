/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2021 WAAD Team <https://arbonne.games-rpg.net/>
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
using System.IO;
using WaadShared;
using WaadShared.Auth;
using WaadShared.Config;
using WaadShared.Network;
using WaadShared.Threading;
using static WaadShared.Main;

namespace LogonServer;

public static class SocketManager
{
    public static readonly byte[] sql_hash = new byte[20];
    private static readonly string configFile = Path.Combine(AppContext.BaseDirectory, "waad-logonserver.ini");
    public static uint MaxBuild { get; private set; } = 0;
    public static uint MinBuild { get; private set; } = 0;
    public static bool InitializeSockets(ConfigMgr configMgr, Logger sLog)
    {
        var ThreadPool = new ThreadPool();
        if (!configMgr.MainConfig.SetSource(configFile))
        {

            sLog.OutString(L_N_MAIN_1);
            return false;
        }

        uint cport = (uint)configMgr.MainConfig.GetInt32("Listen", "RealmListPort", 3724);
        uint sport = (uint)configMgr.MainConfig.GetInt32("Listen", "ServerPort", 8093);
        string host = configMgr.MainConfig.GetString("Listen", "Host", "0.0.0.0");
        string shost = configMgr.MainConfig.GetString("Listen", "ISHost", host);
        MinBuild = (uint)configMgr.MainConfig.GetInt32("Client", "MinBuild", 12340);
        MaxBuild = (uint)configMgr.MainConfig.GetInt32("Client", "MaxBuild", 12340);
        string logonPass = configMgr.MainConfig.GetString("LogonServer", "RemotePassword", "r3m0t3b4d");
        var hash = new Sha3Hash();
        hash.UpdateData(logonPass);
        hash.FinalizeHash();
        Array.Copy(hash.GetDigest(), sql_hash, 20);

        ThreadPool.ExecuteTask(new LogonConsoleThread());

        var Instance = new SocketMgr();
        Instance.SpawnWorkerThreads();

        var authSocket = new ListenSocket<AuthSocket>(host, cport);
        var serverSocket = new ListenSocket<LogonCommServerSocket>(shost, sport);

        if (!authSocket.IsOpen() || !serverSocket.IsOpen())
        {
            sLog.OutError("Failed to open sockets.");
            return false;
        }
#if WIN32
        ThreadPool.ExecuteTask(authSocket);
        ThreadPool.ExecuteTask(serverSocket);
#endif
        return true;
    }
}