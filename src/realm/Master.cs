/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2025 WAAD Team <https://arbonne.games-rpg.net/>
 *
 * From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore
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
 */

using System;
using System.IO;
using WaadShared;
using WaadShared.Config;
using WaadShared.Network;
using WaadShared.RandomGen;
using WaadShared.Threading;

using static System.Threading.Thread;
using static WaadShared.Common;
using static WaadShared.Master;

namespace WaadRealmServer;

public class Master
{
    private static volatile bool m_stopEvent = false;
    public static bool StopEvent
    {
        get { return m_stopEvent; }
        set { m_stopEvent = value; }
    }

    private static string configFile = Path.Combine(AppContext.BaseDirectory, "waad-cluster.ini");
    private static string realmConfigFile = Path.Combine(AppContext.BaseDirectory, "waad-realms.ini");
    private static readonly Logger sLog = new();
    private static readonly ConfigMgr Config = new();
    private static readonly BufferPool BufferPool = new();
    private static readonly SocketMgr SocketMgr = new();
    private static DateTime UNIXTIME;
    private static readonly string BANNER = "WAAD r{0}/{1}-{2}-{3} :: Realm Server";

    // Ajout des variables globales manquantes et des TODO pour la fidélité au C++
    private static string mainchannel = "Général";
    public static string MainChannelName => mainchannel;
    private static string tradechannel = "Commerce";
    public static string TradeChannelName => tradechannel;
    private static string defensechannel = "DéfenseLocale";
    public static string DefenseChannelName => defensechannel;
    private static string defunichannel = "DéfenseUniverselle";
    public static string DefUniChannelName => defunichannel;
    private static string guildchannel = "RecrutementDeGuilde";
    public static string GuildChannelName => guildchannel;
    private static string lookingforchannel = "RechercheDeGroupe";
    public static string LookingForChannelName => lookingforchannel;
    private static string citychannel = "Capitales";
    public static string CityChannelName => citychannel;
    private static string GmClientChannel = "gm_sync_channel";
    public static string GmClientChannelName => GmClientChannel;
    private static bool m_lfgForNonLfg = false;
    public static bool LfgForNonLfg => m_lfgForNonLfg;
    private static bool bServerShutdown = false;
    public static bool ServerShutdown
    {
        get { return bServerShutdown; }
        set { bServerShutdown = value; }    }

    private static readonly int OBJECT_WAIT_TIME = 50; // ms
    public static string BRANCH_NAME { get; set; }
    public static int REVISION { get; set; }

    // Configuration du ThreadPool
    private static bool m_enableMultithreadedLoading = true;
    private static int m_maxThreadCount = 32; // Limite absolue
    private static uint rlport = 8129;
    private static uint rsport = 11010;

    // Rehash (reload config, channels, etc.)
    public static void Rehash(bool load)
    {
        if (load)
            Config.ClusterConfig.SetSource(configFile);

        // ChannelMgr singleton, DBC path, etc.
        if (ChannelMgr.Instance == null)
            _ = ClusterMgr.Instance;

        string dbcPath = Config.ClusterConfig.GetString("Path", "DBCPath", "dbc");
        DBCStores.DbcPath = dbcPath;

        // Multuthreaded loading config
        m_enableMultithreadedLoading = Config.ClusterConfig.GetBoolean("Startup", "EnableMultithreadedLoading", true);
        m_maxThreadCount = Config.ClusterConfig.GetInt32("Startup", "MaxThreadCount", 32);

        // NetworkThreadPool setup
        int networkThreadCount = Config.ClusterConfig.GetInt32("Network.ThreadPool", "InitialThreads", 8);
        NetworkThreadPool.Instance.Startup(networkThreadCount);

        // NetworkThreadPool limits
        int minThreads = Config.ClusterConfig.GetInt32("Network.ThreadPool", "MinThreads", 8);
        int maxThreads = Config.ClusterConfig.GetInt32("Network.ThreadPool", "MaxThreads", 32);
        NetworkThreadPool.Instance.SetThreadLimits(minThreads, maxThreads);

        //Cluster Ports
        rsport = Config.ClusterConfig.GetUInt32("Cluster", "RSPort", 8129);
        rlport = Config.ClusterConfig.GetUInt32("Cluster", "RLPort", 11010);

        mainchannel = Config.ClusterConfig.GetString("Server", "MainChannel", "Général");
        tradechannel = Config.ClusterConfig.GetString("Server", "TradeChannel", "Commerce");
        defensechannel = Config.ClusterConfig.GetString("Server", "DefenseChannel", "DéfenseLocale");
        defunichannel = Config.ClusterConfig.GetString("Server", "DefenseUniverselChannel", "DéfenseUniverselle");
        guildchannel = Config.ClusterConfig.GetString("Server", "GuildChannel", "RecrutementDeGuilde");
        lookingforchannel = Config.ClusterConfig.GetString("Server", "LookingForChannel", "RechercheDeGroupe");
        citychannel = Config.ClusterConfig.GetString("Server", "CityChannel", "Capitales");
        GmClientChannel = Config.ClusterConfig.GetString("GMClient", "GmClientChannel", "gm_sync_channel");
        m_lfgForNonLfg = Config.ClusterConfig.GetBoolean("Server", "EnableLFGJoin");

        // ChannelMgr config reload
        Channel.LoadConfSettings();
    }

    public static void Main(string[] args)
    {
        // Lecture des infos Git (branche, révision)
        var gitInfo = ReadGitInfo();
        if (gitInfo.HasValue)
        {
            BRANCH_NAME = gitInfo.Value.Item1;
            REVISION = gitInfo.Value.Item2;
        }
        else
        {
            BRANCH_NAME = "unknown";
            REVISION = 0;
        }
        
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            sLog.OutError($"Unhandled exception: {e.ExceptionObject}");
            Environment.Exit(1);
        };

        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            sLog.OutError($"Fatal error in main loop: {ex}");
            Environment.Exit(1);
        }
    }

    private static (string, int)? ReadGitInfo()
    {
        var assembly = typeof(Master).Assembly;
        using var stream = assembly.GetManifestResourceStream("waad-realmserver.GitInfo.txt");
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
                branchName = line["Branch:".Length..].Trim();
            else if (line.StartsWith("Commit Count:"))
                commitCount = int.Parse(line["Commit Count:".Length..].Trim());
        }
        return (branchName, commitCount);
    }

    // Démarrage principal
    public static void Run(string[] args)
    {

        UNIXTIME = DateTime.UtcNow;
        int fileLogLevel = -1;
        int screenLogLevel = 3;
        bool doCheckConf = false;
        bool doVersion = false;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--checkconf": doCheckConf = true; break;
                case "--screenloglevel": if (i + 1 < args.Length) screenLogLevel = int.Parse(args[++i]); break;
                case "--fileloglevel": if (i + 1 < args.Length) fileLogLevel = int.Parse(args[++i]); break;
                case "--version": doVersion = true; break;
                case "--conf": if (i + 1 < args.Length) configFile = args[++i]; break;
                case "--realmconf": if (i + 1 < args.Length) realmConfigFile = args[++i]; break;
            }
        }

        if (!doVersion && !doCheckConf)
        {
            sLog.Init(fileLogLevel, screenLogLevel);
        }
        else
        {
            sLog.Init(fileLogLevel, 1);
        }

        sLog.OutString("============================================================");
        sLog.OutString("| Waad Cluster System - Realm Server                        |");
        sLog.OutString("| Version 1.0                                              |");
        sLog.OutString("|", BANNER, REVISION, BRANCH_NAME, CONFIG, PLATFORM_TEXT, ARCH, "|");
        sLog.OutString("============================================================");
#if REPACK
sLog.OutString($"Repack: {REPACK} | Auteur: {REPACK_AUTHOR} | {REPACK_WEBSITE}\n");
#endif
        sLog.OutString("");
        sLog.OutString("Copyright (C) 2007-2025 WAAD Team. https://arbonne.games-rpg.net/");
        sLog.OutString("From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore");
        sLog.OutString("This program comes with ABSOLUTELY NO WARRANTY, and is FREE SOFTWARE.");
        sLog.OutString("You are welcome to redistribute it under the terms of the GNU Affero");
        sLog.OutString("General Public License, either version 3 or any later version. For a");
        sLog.OutString("copy of this license, see the COPYING file provided with this distribution.");
        sLog.OutString("");
#if REPACK
sLog.OutString("");
sLog.OutError("Warning: Using repacks is potentially dangerous. You should always compile");
sLog.OutError("from the source yourself at arbonne.games-rpg.net");
sLog.OutError("By using this repack, you agree to not visit the WaadTeam website and ask\nfor support.");
sLog.OutError("For all support, you should visit the repacker's website at {0}", REPACK_WEBSITE);
sLog.OutError("");
#endif

        if (doVersion)
            return;

        if (doCheckConf)
        {
            sLog.OutString("[Config]", R_N_MASTER, ":{0}", configFile);
            if (Config.ClusterConfig.SetSource(configFile))
                CLog.Success("[Config]", R_S_MASTER);
            else
                sLog.OutError("[Config]", R_W_MASTER);
            if (Config.RealmConfig.SetSource(realmConfigFile))
                CLog.Success("[Config]", R_S_MASTER);
            else
                sLog.OutError("[Config]", R_W_MASTER);
            return;
        }

        // Chargement des configs
        if (!Config.ClusterConfig.SetSource(configFile))
        {
            sLog.OutError("[Config]", R_E_MASTER_9, ":{0}", configFile);
            return;
        }
        if (!Config.RealmConfig.SetSource(realmConfigFile))
        {
            sLog.OutError("[Config]", R_E_MASTER_9, ":{0}", realmConfigFile);
            return;
        }

        //Update sLog to obey config setting
        sLog.Init(Config.ClusterConfig.GetInt32("LogLevel", "File", -1), Config.ClusterConfig.GetInt32("LogLevel", "Screen", 1));

        // die/die2 checks (arrêt si config demande)
        string die = Config.ClusterConfig.GetString("die", "msg");
        string die2 = Config.ClusterConfig.GetString("die2", "msg");
        if (!string.IsNullOrEmpty(die) || !string.IsNullOrEmpty(die2))
        {
            sLog.OutError("[Config]", R_W_MASTER_1);
            return;
        }

        Rehash(true);

        MersenneTwister.InitRandomNumberGenerators();
        CLog.Success("[Rnd]", R_S_MASTER_1);

        // Gestion signaux (CTRL+C)
        Console.CancelKeyPress += (sender, e) =>
        {
            sLog.OutString("[Main] SIGINT received, shutting down...");
            m_stopEvent = true;
            e.Cancel = true;
        };

        // === Démarrage des services principaux ===
        // 1. ThreadPool (singleton pattern, doit être démarré avant tout thread ou pool)
        int optimalThreadCount = CalculateOptimalThreadCount();
        NetworkThreadPool.Instance.Startup((byte)optimalThreadCount);
        sLog.OutDebug($"[ThreadPool] Starting with {optimalThreadCount} threads (Max: {m_maxThreadCount}).");

        // 2. BufferPool (singleton, pour les buffers réseaux)
        BufferPool.Init();

        // 3. Database initialization (pattern: static manager, voir RealmDatabaseManager.cs)
        if (!RealmDatabaseManager.InitializeDatabases(Config, sLog))
        {
            sLog.OutError("[Database]", R_E_MASTER_7);
            RealmDatabaseManager.RemoveDatabase();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }
        CLog.Success("[Database]", R_S_MASTER_2);

        // 4. DBC/Storage loading (doit être fait après DB, avant managers)
        if (!DBCStores.LoadRSDBCs())
        {
            sLog.OutError("[Storage]", R_E_MASTER_2);
            RealmDatabaseManager.RemoveDatabase();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }
        CLog.Success("[Storage]", R_S_MASTER_3);

        // 5. ChannelMgr, ClientMgr, ClusterMgr singletons (C++: new, C#: Instance property)
        // Forcer l'initialisation des singletons (pas de new, pattern C#)
        _ = ChannelMgr.Instance;
        _ = ClientMgr.Instance;
        _ = ClusterMgr.Instance;

        // 6. SocketMgr singleton (doit être démarré après les managers)
        SocketMgr.SpawnWorkerThreads();

        // 7. Création des listeners réseau (pattern: instance, pas static)
        ListenSocket<WorldSocket> worldListener = null;
        ListenSocket<WorkerServerSocket> wsListener = null;
        try
        {
            CLog.Success("[Network]", R_S_MASTER_4);
            worldListener = new ListenSocket<WorldSocket>("0.0.0.0", rlport, (socket) => new WorldSocket(socket));
            wsListener = new ListenSocket<WorkerServerSocket>("0.0.0.0", rsport, (socket) => new WorkerServerSocket(socket));
        }

        catch (Exception ex)
        {
            sLog.OutError("[Network] Exception opening network listeners: {0}", ex.Message);
            SocketMgr.CloseAll();
            RealmDatabaseManager.RemoveDatabase();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }
        if (!worldListener.IsOpen() || !wsListener.IsOpen())
        {
            sLog.OutError("[Network]", R_E_MASTER_5);
            SocketMgr.CloseAll();
            RealmDatabaseManager.RemoveDatabase();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }

        // 8. LogonCommHandler singleton (dépend du réseau)
        _ = LogonCommHandler.Instance;
        LogonCommHandler.Instance.Startup();

        sLog.OutString("[Main] Startup complete. Entering main loop.");

        // === Boucle principale ===
        ulong loopcounter = 0;
        while (!m_stopEvent || !ServerShutdown)
        {
            loopcounter++;

            // Gestion des tâches périodiques (1 min et 5 min)
            HandlePeriodicTasks(loopcounter);

            // Tick principaux services
            LogonCommHandler.Instance.UpdateSockets();
            ClientMgr.Instance.Update();
            ClusterMgr.Instance.Update();
            SocketGarbageCollector.Instance.Update();
            Sleep(OBJECT_WAIT_TIME);
        }

        // Début de la séquence d'arrêt
        sLog.OutString("[Fermeture]", R_N_MASTER_6, UNIXTIME.ToString("yyyy-MM-dd HH:mm:ss"));

        bServerShutdown = true;

        // === Arrêt propre des services (ordre inverse de l'init) ===
        // 1. Arrêt des listeners réseau
        sLog.OutString("[~Network]", R_N_MASTER_8);
        worldListener?.Close();
        sLog.OutString("[~Network]", R_N_MASTER_8_1);
        sLog.OutString("~[~Network]", R_N_MASTER_9);
        wsListener?.Close();
        sLog.OutString("[~Network]", R_N_MASTER_9_1);

        // 2. Arrêt du LogonCommHandler
        sLog.OutString("[~Network]", R_N_MASTER_7);
        LogonCommHandler.Instance?.Dispose();
        sLog.OutString("[~Network]", R_N_MASTER_10);

        // 3. Arrêt du SocketMgr
        sLog.OutString("[~Network]", R_N_MASTER_11);
        SocketMgr.CloseAll();
        sLog.OutString("[~Network]", R_N_MASTER_11_1);

        // 4. Arrêt du ThreadPool
        sLog.OutString("[ThreadPool]", R_N_MASTER_15);
        NetworkThreadPool.Instance.Shutdown();
        sLog.OutString("[ThreadPool]", R_N_MASTER_15_1);

        // 5. BufferPool cleanup
        sLog.OutString("[BufferPool]", R_N_MASTER_16);
        BufferPool.Destroy();
        sLog.OutString("[BufferPool]", R_N_MASTER_16_1);

        // 6. DB shutdown
        sLog.OutString("[Database]", R_N_MASTER_13);
        RealmDatabaseManager.RemoveDatabase();
        sLog.OutString("[Database]", R_N_MASTER_14);

        // 7. DBC/Storage cleanup
        sLog.OutString("[Storage]", R_N_MASTER_17);
        // DBCStores.Cleanup();
        sLog.OutString("[Storage]", R_N_MASTER_17_1);

        // 8. Nettoyage des instances singleton
        sLog.OutString("[Fermeture]", R_N_MASTER_19);

        // Libération de ClusterMgr
        if (ClusterMgr.Instance != null)
        {
            ClusterMgr.Instance.Dispose();
            ClusterMgr.ResetInstance();
            sLog.OutString("[~ClusterMgr]", R_N_MASTER_18_1);
        }

        // Libération de ClientMgr
        if (ClientMgr.Instance != null)
        {
            (ClientMgr.Instance as IDisposable)?.Dispose();
            // Réinitialisation via réflexion
            var clientField = typeof(ClientMgr).GetField(
                "<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
            );
            if (clientField != null)
            {
                clientField.SetValue(null, null);
                sLog.OutString("[~ClientMgr]", R_N_MASTER_18_1);
            }
            else
            {
                sLog.OutError("[~ClientMgr]", R_E_MASTER_10);
            }
        }

        // Libération de ChannelMgr
        if (ChannelMgr.Instance != null)
        {
            ChannelMgr.Instance.Dispose();
            ChannelMgr.ResetInstance(); // Si la méthode ResetInstance() existe
            sLog.OutString("[Fermeture] ~ChannelMgr() : Ressources libérées.");
        }

        sLog.OutString("[Fermeture]", R_N_MASTER_20);
        sLog.OutString("[Fermeture]", R_N_MASTER_21);
    }

    /// <summary>
    /// Calcule le nombre optimal de threads pour un serveur I/O-bound (réseau, DB).
    /// </summary>
    private static int CalculateOptimalThreadCount()
    {
        if (!m_enableMultithreadedLoading)
            return 1;

        int logicalCoreCount = Environment.ProcessorCount;
        // Pour un serveur I/O-bound, on utilise 2 × le nombre de cœurs logiques (avec une limite à 32)
        int optimalThreadCount = Math.Min(logicalCoreCount * 2, 32);
        return optimalThreadCount;
    }

    private static void HandlePeriodicTasks(ulong loopcounter)
    {
        // Toutes les 1 minute
        if ((loopcounter % (60UL * (1000UL / (ulong)OBJECT_WAIT_TIME))) == 0)
        {
            ShowNetworkStats();
        }

        // Toutes les 5 minutes
        if ((loopcounter % (300UL * (1000UL / (ulong)OBJECT_WAIT_TIME))) == 0)
        {
            LogonCommHandler.Instance.ReloadForcedPermissions();
            NetworkThreadPool.Instance.ShowStats();
            NetworkThreadPool.Instance.IntegrityCheck();
            BufferPool.Optimize();
        }
    }

    private static void ShowNetworkStats()
    {
        float sdata = (float)(NetworkThreadPool.BytesSent << 3) / 60; // bits/sec
        float rdata = (float)(NetworkThreadPool.BytesReceived << 3) / 60;
        float tdata = sdata + rdata;

        string[] rateExtensions = [" bits/sec", "kbps", "mbps", "gbps", "tbps"];
        int sextensionoffset = 0, rextensionoffset = 0, textensionoffset = 0;

        while (sdata > 1024) { sdata /= 1024; sextensionoffset++; }
        while (rdata > 1024) { rdata /= 1024; rextensionoffset++; }
        while (tdata > 1024) { tdata /= 1024; textensionoffset++; }

        sLog.OutDebug("============ Network Status ================");
        sLog.OutDebug($"Send Rate: {sdata:F5}{rateExtensions[sextensionoffset]}");
        sLog.OutDebug($"Receive Rate: {rdata:F5}{rateExtensions[rextensionoffset]}");
        sLog.OutDebug($"Total Rate: {tdata:F5}{rateExtensions[textensionoffset]}");
        sLog.OutString("============================================");

        // Réinitialiser les compteurs après affichage
        NetworkThreadPool.ResetCounters();
    }
}
