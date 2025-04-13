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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using static System.Threading.Thread;

using WaadShared;
using WaadShared.Config;
using WaadShared.Network;
using WaadShared.Threading;
using static WaadShared.Common;
using static WaadShared.Main;
using static WaadShared.Network.Socket;
using static WaadShared.Threading.ThreadPool;

namespace LogonServer;

public class LogonServer
{
    private static bool mrunning = true;
    public static uint MaxBuild { get; private set; } = 0;
    public static uint MinBuild { get; private set; } = 0;
    public static object[] BRANCH_NAME { get; private set; }
    public static int REVISION { get; private set; }
    public static readonly byte[] sql_hash = new byte[20];
    private static string configFile = Path.Combine(AppContext.BaseDirectory, "waad-logonserver.ini");

    const string BANNER = "WAAD {0} r{1}/{2}-{3} ({4}) :: Logon Server";

    public static void Main(string[] args)
    {
        var gitInfo = ReadGitInfo();
        if (gitInfo.HasValue)
        {
            BRANCH_NAME = [gitInfo.Value.Item1];
            REVISION = gitInfo.Value.Item2;
        }
        else
        {
            BRANCH_NAME = ["unknown"];
            REVISION = 0;
        }
        RunLS(args);
    }

    static (string, int)? ReadGitInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("waad-logonserver.GitInfo.txt");
        if (stream == null)
        {
            Console.WriteLine("Git information not found.");
            return null;
        }

        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string branchName = "unknown";
        int commitCount = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("Branch:"))
            {
                branchName = line["Branch:".Length..].Trim();
            }
            else if (line.StartsWith("Commit Count:"))
            {
                commitCount = int.Parse(line["Commit Count:".Length..].Trim());
            }
        }

        return (branchName, commitCount);
    }

    public static void Run(string[] args, DateTime g_localTime, DateTime uNIXTIME)
    {
        var sLog = new Logger();
        var configMgr = new ConfigMgr();
        var ThreadPool = new ThreadPool();
        int fileLogLevel = -1;
        int screenLogLevel = 3;
        bool doCheckConf = false;
        bool doVersion = false;

        if (!configMgr.MainConfig.SetSource(configFile))
        {

            sLog.OutString(L_N_MAIN_1);
            return;
        }

        var options = new Dictionary<string, Action<string>>
        {
            { "--checkconf", _ => doCheckConf = true },
            { "--screenloglevel", arg => screenLogLevel = int.Parse(arg) },
            { "--fileloglevel", arg => fileLogLevel = int.Parse(arg) },
            { "--version", _ => doVersion = true },
            { "--conf", arg => configFile = arg }
        };

        for (int i = 0; i < args.Length; i++)
        {
            if (options.TryGetValue(args[i], out Action<string> value))
            {
                value(i + 1 < args.Length ? args[i + 1] : null);
                i++;
            }
        }

        if (!doVersion && !doCheckConf)
        {
            screenLogLevel = configMgr.MainConfig.GetInt32("LogLevel", "Screen");
            fileLogLevel = configMgr.MainConfig.GetInt32("LogLevel", "File");
            sLog.Init(fileLogLevel, screenLogLevel);
        }
        else
        {
            sLog.Init(fileLogLevel, screenLogLevel);
        }

        sLog.OutString(BANNER, BRANCH_NAME[0], REVISION, CONFIG, PLATFORM_TEXT, ARCH);
#if REPACK
        sLog.OutString($"Repack: {REPACK} | Auteur: {REPACK_AUTHOR} | {REPACK_WEBSITE}\n");
#endif
        sLog.OutString("==============================================================================");
        sLog.OutString("");
        if (doVersion)
            return;

        if (doCheckConf)
        {
            CLog.Notice("[Config]", string.Format(L_N_MAIN_3, configFile));

            if (configMgr.MainConfig.SetSource(configFile))
                sLog.OutString(L_N_MAIN_4);
            else
                sLog.OutError(L_N_MAIN_5);

            string die = configMgr.MainConfig.GetString("die", "msg");
            string die2 = configMgr.MainConfig.GetString("die2", "msg");

            if (!string.IsNullOrEmpty(die) || !string.IsNullOrEmpty(die2))
                sLog.OutString(L_N_MAIN_6);

            return;
        }

        sLog.OutString(L_N_MAIN_7);
        sLog.OutString("");

        CLog.Notice("[Config]", L_N_MAIN_8);
        if (!Rehash(new object()))
            return;

        CLog.Notice("[ThreadMgr]", L_N_MAIN_9);
        Startup(5);

        if (!DatabaseManager.InitializeDatabases(configMgr, sLog)) return;

        CLog.Notice("[AccountMgr]", L_N_MAIN_9);
        CLog.Notice("[InfoCore]", L_N_MAIN_9);
        CLog.Notice("[AccountMgr]", L_N_MAIN_10);
        
        AccountMgr.Instance.ReloadAccounts(true);
        CLog.Notice("[AccountMgr]", string.Format(L_N_MAIN_11, AccountMgr.Instance.GetAccountCount()));
        if (AccountMgr.Instance.GetAccountCount() == 0)
        {
            CLog.Warning("[Main]", "No accounts were loaded. Please check the database and ReloadAccounts logic.");
        }
        CLog.Line();

        int atime = configMgr.MainConfig.GetInt32("Rates", "AccountRefresh", 600) * 1000;
        var pfc = new PeriodicFunctionCaller<AccountMgr>(AccountMgr.Instance, AccountMgr.Instance.ReloadAccountsCallback, (uint)atime);
        ThreadPool.ExecuteTask(pfc);

        if (!SocketManager.InitializeSockets(configMgr, sLog)) return;

        AppDomain.CurrentDomain.ProcessExit += (sender, e) => OnSignal();

#if !WIN32
        File.WriteAllText("waad-logonserver.pid", Process.GetCurrentProcess().Id.ToString());
#endif

        byte[] HashNameShaMagicNumber = [0x57, 0x61, 0x61, 0x64, 0x00, 0x00, 0x00];

        CLog.Success("Main", L_N_MAIN_12);

        uint loopCounter = 0;
        while (mrunning)
        {
            if (++loopCounter % 400 == 0) // 20 seconds
                CheckForDeadSockets();

            if (loopCounter % 10000 == 0) // 2 minutes
                ThreadPool.IntegrityCheck(2);

            if (loopCounter % 100 == 0) // 1 second
            {
                InformationCore.Instance.TimeoutSockets();
                CheckForDeadSockets();
                uNIXTIME = DateTime.UtcNow;
                g_localTime = DateTime.Now;
            }

            PatchMgr.Instance.UpdateJobs();
            Sleep(10);
        }

        sLog.OutString(L_N_MAIN_13);

        pfc.Kill();

        var Instance = new SocketMgr(); // Create or retrieve the appropriate instance
        Instance.CloseAll();
#if WIN32
        Instance.ShutdownThreads();
#endif
        LogonConsole.Instance.Kill();

        sLog.OutString(L_N_MAIN_14);

        DatabaseManager.RemoveDatabase();
        ThreadPool.Shutdown();

        File.Delete("waad-logonserver.pid");

        AccountMgr.CloseSocket();
        var serverSocket = new LogonCommServerSocket(); // Create or retrieve the appropriate instance
        InformationCore.Instance.RemoveServerSocket(serverSocket);
        Instance = null;

        sLog.OutString(L_N_MAIN_15);
    }

    private static void OnSignal()
    {
        mrunning = false;
    }

    public static bool Rehash(object allowedIpLock)
    {
        short ServerTrustMe;
        short ServerModTrustMe;
        var Config = new ConfigMgr();
        var allowedIps = new List<AllowedIP>();
        var allowedModIps = new List<AllowedIP>();

        if (!Config.MainConfig.SetSource(configFile))
        {
            var SLog = new Logger();
            SLog.OutString(L_N_MAIN_1);
            return false;
        }

        // Recup des valeurs si elle existe, sinon pas actif par defaut
        ServerTrustMe = (short)(Config.MainConfig.GetInt32("LogonServer", "ServerTrustMe", 1) == 1 ? 1 : 0);
        ServerModTrustMe = (short)(Config.MainConfig.GetInt32("LogonServer", "ServerModTrustMe", 1) == 1 ? 1 : 0);

        // re-set the allowed server IP's
        string ips = Config.MainConfig.GetString("LogonServer", "AllowedIPs");
        string ipsmod = Config.MainConfig.GetString("LogonServer", "AllowedModIPs");

        // Verif Console.... (Branruz)
        if (ServerTrustMe == 1)
            CLog.Warning("[AllowedIPs]", L_W_MAIN_AI);
        else
            CLog.Notice("[AllowedIPs]", L_W_MAIN_AI_1);

        if (ServerModTrustMe == 1)
            CLog.Warning("[AllowedModIPs]", L_W_MAIN_AI);
        else
            CLog.Notice("[AllowedModIPs]", "AllowedModIPs : ", L_W_MAIN_AI_1);

        var vips = StrSplit(ips, " ");
        var vipsmod = StrSplit(ipsmod, " ");
        lock (allowedIpLock)
        {
            allowedIps.Clear();
            allowedModIps.Clear();

            foreach (var ip in vips)
            {
                var parts = ip.Split('/');
                if (parts.Length != 2)
                {
                    CLog.Warning("[AllowedIPs]", string.Format(L_W_MAIN_2, ip));
                    continue;
                }

                if (!uint.TryParse(parts[1], out uint ipmask))
                {
                    CLog.Warning("[AllowedIPs]", string.Format(L_W_MAIN_2, ip));
                    continue;
                }

                var ipraw = MakeIP(parts[0]);
                if (ipraw == 0 || ipmask == 0)
                {
                    CLog.Warning("[AllowedIPs]", string.Format(L_W_MAIN_2, ip));
                    continue;
                }

                allowedIps.Add(new AllowedIP { IP = ipraw, Bytes = (int)ipmask });
            }

            foreach (var ip in vipsmod)
            {
                var parts = ip.Split('/');
                if (parts.Length != 2)
                {
                    CLog.Warning("[AllowedModIPs]", string.Format(L_W_MAIN_2, ip));
                    continue;
                }

                if (!uint.TryParse(parts[1], out uint ipmask))
                {
                    CLog.Warning("[AllowedModIPs]", string.Format(L_W_MAIN_2, ip));
                    continue;
                }

                var ipraw = MakeIP(parts[0]);
                if (ipraw == 0 || ipmask == 0)
                {
                    CLog.Warning("[AllowedModIPs]", string.Format(L_W_MAIN_2, ip));
                    continue;
                }

                allowedModIps.Add(new AllowedIP { IP = ipraw, Bytes = (int)ipmask });
            }
        }

        return true;
    }

    private static void CheckForDeadSockets()
    {
        var deadSockets = new List<Socket>();
        var Socket = new Socket();

        foreach (var socket in GetSocketList())
        {
            if (!Socket.IsConnected())
            {
                deadSockets.Add((Socket)socket);
            }
        }

        foreach (var deadSocket in deadSockets)
        {
            RemoveSocket(deadSocket);
            deadSocket.Disconnect();
            CLog.Warning("[LogonServer]", "Removed dead socket: " + deadSocket.RemoteEndPoint);
        }
    }

    private static uint MakeIP(string ipStr)
    {
        var parts = ipStr.Split('.');
        if (parts.Length != 4) return 0;

        if (byte.TryParse(parts[0], out byte b1) &&
            byte.TryParse(parts[1], out byte b2) &&
            byte.TryParse(parts[2], out byte b3) &&
            byte.TryParse(parts[3], out byte b4))
        {
            return (uint)(b1 << 24 | b2 << 16 | b3 << 8 | b4);
        }

        return 0;
    }

    private static List<string> StrSplit(string str, string delimiter)
    {
        return [.. str.Split([delimiter], StringSplitOptions.RemoveEmptyEntries)];
    }

    private static void OnSignal(int s)
    {
        switch (s)
        {
#if !WIN32
            case (int)Signum.SIGHUP:
                sLog.OutString("Recu signal SIGHUP, rechargement des comptes.");
                AccountMgr.Instance.ReloadAccounts(true);
                break;
#endif
            case (int)Signum.SIGINT:
            case (int)Signum.SIGTERM:
            case (int)Signum.SIGABRT:
#if WIN32
            case (int)Signum.SIGBREAK:
#endif
                mrunning = false;
                break;
        }        
        OnSignal(s);
    }

    public static void RunLS(string[] args)
    {
        var uNIXTIME = DateTime.UtcNow;
        var g_localTime = DateTime.Now;
        _ = new LogonServer();
        Run(args, uNIXTIME, g_localTime);
    }
}

public class AllowedIP
{
    public uint IP { get; set; }
    public int Bytes { get; set; }
}

public enum Signum
{
    SIGHUP = 1,
    SIGINT = 2,
    SIGQUIT = 3,
    SIGILL = 4,
    SIGTRAP = 5,
    SIGABRT = 6,
    SIGBUS = 7,
    SIGFPE = 8,
    SIGKILL = 9,
    SIGUSR1 = 10,
    SIGSEGV = 11,
    SIGUSR2 = 12,
    SIGPIPE = 13,
    SIGALRM = 14,
    SIGTERM = 15,
    SIGSTKFLT = 16,
    SIGCHLD = 17,
    SIGCONT = 18,
    SIGSTOP = 19,
    SIGTSTP = 20,
    SIGTTIN = 21,
    SIGTTOU = 22,
    SIGURG = 23,
    SIGXCPU = 24,
    SIGXFSZ = 25,
    SIGVTALRM = 26,
    SIGPROF = 27,
    SIGWINCH = 28,
    SIGIO = 29,
    SIGPWR = 30,
    SIGSYS = 31,
    SIGBREAK = 32
}
