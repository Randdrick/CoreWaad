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
using System.Reflection;
using System.Runtime.InteropServices;
using WaadShared;
using WaadShared.Config;
using WaadShared.Network;
using WaadShared.RandomGen;
using WaadShared.Threading;

using static System.Threading.Thread;
using static WaadShared.Common;
using static WaadShared.Main;
using static WaadShared.Master;

namespace WaadRealmServer;

public class Master
{
    // === Constantes globales pour la gestion des threads ===
    private static int MAX_TOTAL_THREADS = 32; // Limite absolue pour éviter la saturation

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
    const string BANNER = "| WAAD {0} r{1}/{2}-{3} ({4}) :: Realm Server          |";

    // Ajout des variables globales manquantes
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
        set { bServerShutdown = value; }
    }

    private static readonly int OBJECT_WAIT_TIME = 50; // ms
    public static string BRANCH_NAME { get; set; }
    public static int REVISION { get; set; }

    // Configuration du ThreadPool
    private static bool m_enableMultithreadedLoading = true;
    // private static int m_maxThreadCount = 32; // Limite absolue
    private static uint rlport = 8129;
    private static uint rsport = 11010;

    // PID file (Unix only)
    private static readonly string PID_FILE = "waad-realmserver.pid";
    private static volatile bool m_crashed = false;
    private static readonly object m_crashedMutex = new();

    private static void HandleSignal(string signal)
    {
        switch (signal)
        {
            case "SIGHUP":
                sLog.OutString("[Signal] SIGHUP reçu, rechargement de la configuration...");
                Rehash(true);
                sLog.OutString("[Signal] Configuration rechargée.");
                break;
            case "SIGUSR1":
                sLog.OutString("[Signal] SIGUSR1 reçu.");
                break;
            case "SIGSEGV":
            case "SIGFPE":
            case "SIGILL":
            case "SIGBUS":
                HandleCrash(signal);
                break;
# if WIN32
            case "SIGBREAK":
#endif
                StopEvent = true;
                break;
        }
    }

    private static void HandleCrash(string signal)
    {
        if (m_crashed)
        {
            sLog.OutError("[Crash] Un crash est déjà en cours de traitement, abandon...");
            Environment.Exit(1);
            return;
        }

        lock (m_crashedMutex)
        {
            if (m_crashed) return;
            m_crashed = true;
        }

        sLog.OutError($"[Crash] Gestionnaire de signal activé : {signal}...");
        try
        {
            if (ClusterMgr.Instance != null)
            {
                sLog.OutString("[Crash] Sauvegarde de l'état avant le crash...");
                RealmDatabaseManager.RemoveDatabase();
                sLog.OutString("[Crash] Connexions à la base de données fermées.");
            }
        }
        catch (Exception ex)
        {
            sLog.OutError($"[Crash] Exception lors de la gestion du crash : {ex.Message}");
        }

        sLog.OutString("[Crash] Arrêt en cours...");
        Environment.Exit(1);
    }

    private static void WritePidFile()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            int pid = Environment.ProcessId;
            File.WriteAllText(PID_FILE, pid.ToString());
            sLog.OutString($"[PID] PID {pid} écrit dans {PID_FILE}");
        }
        catch (Exception ex)
        {
            sLog.OutError($"[PID] Échec de l'écriture du fichier PID : {ex.Message}");
        }
    }

    private static void RemovePidFile()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            if (File.Exists(PID_FILE))
            {
                File.Delete(PID_FILE);
                sLog.OutString($"[PID] Fichier PID {PID_FILE} supprimé.");
            }
        }
        catch (Exception ex)
        {
            sLog.OutError($"[PID] Échec de la suppression du fichier PID : {ex.Message}");
        }
    }

    // Rehash (rechargement de la configuration, des canaux, etc.)
    public static void Rehash(bool load)
    {
        if (load)
            Config.ClusterConfig.SetSource(configFile);

        // ChannelMgr singleton, chemin DBC, etc.
        if (ChannelMgr.Instance == null)
            _ = ClusterMgr.Instance;

        string dbcPath = Config.ClusterConfig.GetString("Path", "DBCPath", "dbc");
        DBCStores.DbcPath = dbcPath;
        DBCStores.RsdbcPath = dbcPath;

        // Configuration du chargement multithread
        m_enableMultithreadedLoading = Config.ClusterConfig.GetBoolean("Startup", "EnableMultithreadedLoading", true);
        MAX_TOTAL_THREADS = Config.ClusterConfig.GetInt32("Startup", "MaxThreadCount", 32);

        // Configuration du ThreadPool réseau
        int networkThreadCountConfig = Config.ClusterConfig.GetInt32("Network.ThreadPool", "InitialThreads", 8);
        NetworkThreadPool.Instance.Startup((byte)networkThreadCountConfig);

        // Limites du ThreadPool réseau
        int minThreads = Config.ClusterConfig.GetInt32("Network.ThreadPool", "MinThreads", 8);
        int maxThreads = Config.ClusterConfig.GetInt32("Network.ThreadPool", "MaxThreads", 32);
        NetworkThreadPool.Instance.SetThreadLimits(minThreads, maxThreads);

        // Ports du cluster
        rsport = Config.ClusterConfig.GetUInt32("Cluster", "RSPort", 8129);
        rlport = Config.ClusterConfig.GetUInt32("Cluster", "RLPort", 11010);

        // Canaux
        mainchannel = Config.ClusterConfig.GetString("Server", "MainChannel", "Général");
        tradechannel = Config.ClusterConfig.GetString("Server", "TradeChannel", "Commerce");
        defensechannel = Config.ClusterConfig.GetString("Server", "DefenseChannel", "DéfenseLocale");
        defunichannel = Config.ClusterConfig.GetString("Server", "DefenseUniverselChannel", "DéfenseUniverselle");
        guildchannel = Config.ClusterConfig.GetString("Server", "GuildChannel", "RecrutementDeGuilde");
        lookingforchannel = Config.ClusterConfig.GetString("Server", "LookingForChannel", "RechercheDeGroupe");
        citychannel = Config.ClusterConfig.GetString("Server", "CityChannel", "Capitales");
        GmClientChannel = Config.ClusterConfig.GetString("GMClient", "GmClientChannel", "gm_sync_channel");
        m_lfgForNonLfg = Config.ClusterConfig.GetBoolean("Server", "EnableLFGJoin", false);

        // Rechargement de la configuration des canaux
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
            sLog.OutError($"Exception non gérée : {e.ExceptionObject}");
            Environment.Exit(1);
        };

        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            sLog.OutError($"Erreur fatale dans la boucle principale : {ex}");
            Environment.Exit(1);
        }
    }

    static (string, int)? ReadGitInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("waad-realmserver.GitInfo.txt");
        if (stream == null)
        {
            Console.WriteLine("Informations Git introuvables.");
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

        sLog.OutString("|=============================================================|");
        sLog.OutString("| Waad Cluster System - Realm Server                          |");
        sLog.OutString("| Version 1.0                                                 |");
        sLog.OutString(BANNER, BRANCH_NAME[0], REVISION, CONFIG, PLATFORM_TEXT, ARCH);
        sLog.OutString("|=============================================================|");
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

        // Mise à jour de sLog pour respecter les paramètres de configuration
        sLog.Init(Config.ClusterConfig.GetInt32("LogLevel", "File", -1), Config.ClusterConfig.GetInt32("LogLevel", "Screen", 1));

        // Vérification des configurations die/die2 (arrêt si demandé)
        string die = Config.ClusterConfig.GetString("die", "msg");
        string die2 = Config.ClusterConfig.GetString("die2", "msg");
        if (!string.IsNullOrEmpty(die) || !string.IsNullOrEmpty(die2))
        {
            sLog.OutError("[Config]", R_W_MASTER_1);
            return;
        }

        Rehash(true);

        // Gestion de CTRL+C (doit être enregistré avant le démarrage des threads)
        Console.CancelKeyPress += (sender, e) =>
        {
            CLog.Notice("[Main]", L_N_MAIN_12_A);
            m_stopEvent = true;
            e.Cancel = true; // Critique : empêche l'arrêt brutal
        };

        MersenneTwister.InitRandomNumberGenerators();
        CLog.Success("[Rnd]", R_S_MASTER_1);

        // === Gestion des threads (nouvelle logique) ===
        // 1. Calcul du nombre total de threads disponibles
        // int totalAvailableThreads = Math.Min(Environment.ProcessorCount * 2, MAX_TOTAL_THREADS);
        int totalAvailableThreads = CalculateOptimalThreadCount();

        // 2. Répartition entre NetworkThreadPool et TaskList
        int networkThreadCount = totalAvailableThreads / 2;
        int taskListThreadCount = totalAvailableThreads - networkThreadCount;
        if (taskListThreadCount < 5)
            taskListThreadCount = 5;

        // Vérification des cas limites
        if (totalAvailableThreads < 2)
        {
            sLog.OutWarning("[ThreadPool] Nombre de cœurs insuffisant. Allocation minimale de 1 thread par pool.");
            networkThreadCount = 1;
            taskListThreadCount = 1;
        }

        // Vérification de sécurité
        if (networkThreadCount + taskListThreadCount > MAX_TOTAL_THREADS)
        {
            sLog.OutError($"[ThreadPool] Erreur : Le nombre total de threads ({networkThreadCount + taskListThreadCount}) dépasse la limite autorisée ({MAX_TOTAL_THREADS}).");
            return;
        }

        // 3. Initialisation du NetworkThreadPool
        NetworkThreadPool.Instance.Startup((byte)networkThreadCount);
        sLog.OutDebug($"[ThreadPool] Démarrage avec {networkThreadCount} threads (réseau). Limite totale : {totalAvailableThreads} threads.");

        // 4. BufferPool (singleton, pour les buffers réseaux)
        BufferPool.Init();

        // 5. Initialisation de la base de données
        if (!RealmDatabaseManager.InitializeDatabases(Config, sLog))
        {
            sLog.OutError("[Database]", R_E_MASTER_7);
            RealmDatabaseManager.RemoveDatabase();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }
        CLog.Success("[Database]", R_S_MASTER_2);

        // 6. Vérification du répertoire DBC
        string RSDBCPath = Config.ClusterConfig.GetString("Path", "DBCPath", "dbc");
        if (!Directory.Exists(RSDBCPath))
        {
            sLog.OutError($"[Storage] Le répertoire DBC est introuvable : {RSDBCPath}");
            // Arrêt propre des services déjà démarrés
            RealmDatabaseManager.RemoveDatabase();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }

        // 7. Chargement des DBC/Storage
        if (!DBCStores.LoadRSDBCs())
        {
            sLog.OutError("[Storage]", R_E_MASTER_2);
            RealmDatabaseManager.RemoveDatabase();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }
        CLog.Success("[Storage]", R_S_MASTER_3);

        // 8. Remplissage des données de stockage (doit être fait après DB, avant les managers)
        TaskList tl = new();
        StorageManager.FillTaskList(tl,Config);

        // Démarrage des tâches de chargement en parallèle
        CLog.Notice("[Storage]", R_S_MASTER_3_1, taskListThreadCount);
        tl.Start((uint)taskListThreadCount);        

        // Attente de la fin des tâches
        tl.Wait();
        CLog.Success("[Storage]", R_S_MASTER_3_2);

        // Arrêt propre des threads du TaskList
        tl.Kill();

        // 9. Initialisation des singletons ChannelMgr, ClientMgr, ClusterMgr
        _ = ChannelMgr.Instance;
        _ = ClientMgr.Instance;
        _ = ClusterMgr.Instance;

        // Initialisation des gestionnaires de session après l'initialisation des singletons
        Session.InitHandlers();

        // 10. SocketMgr singleton (doit être démarré après les managers)
        SocketMgr.SpawnWorkerThreads();

        // 11. Création des listeners réseau
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
            sLog.OutError("[Network]", $"Exception lors de l'ouverture des listeners réseau : {ex.Message}");
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

        // 12. LogonCommHandler singleton (dépend du réseau)
        _ = LogonCommHandler.Instance;
        LogonCommHandler.Instance.Startup();

        // 13. Thread de console (entrée standard locale)
        ConsoleThread consoleThread = new();
        NetworkThreadPool.Instance.ExecuteTask(consoleThread);
        CLog.Notice("[Console]", "Thread de console démarré.");

        // 14. Listener de console à distance (si activé dans la configuration)
        try
        {
            ConsoleListener.StartFromConfig();
        }
        catch (Exception ex)
        {
            sLog.OutError($"[Console]", $"Échec du démarrage de la console à distance : {ex.Message}");
        }

        // 15. Écriture du fichier PID (Unix uniquement)
        WritePidFile();

        // Enregistrement des gestionnaires de signaux POSIX sur les systèmes Unix
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                PosixSignalRegistration.Create(PosixSignal.SIGHUP, ctx => { HandleSignal("SIGHUP"); ctx.Cancel = true; });
                PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { m_stopEvent = true; ctx.Cancel = true; });
            }
            catch
            {
                // Si les signaux POSIX ne sont pas disponibles sur ce runtime, ignorer
            }
        }

        sLog.OutString("[Main] Démarrage terminé. Entrée dans la boucle principale.");

        // === Boucle principale ===
        ulong loopcounter = 0;
        while (!m_stopEvent && !ServerShutdown)
        {
            loopcounter++;

            // Gestion des tâches périodiques (1 min et 5 min)
            HandlePeriodicTasks(loopcounter);

            // Mise à jour des services principaux
            LogonCommHandler.Instance.UpdateSockets();
            ClientMgr.Instance.Update();
            ClusterMgr.Instance.Update();
            SocketGarbageCollector.Instance.Update();
            Sleep(OBJECT_WAIT_TIME);
        }

        // Début de la séquence d'arrêt
        CLog.Notice("[Fermeture]", R_N_MASTER_6, UNIXTIME.ToString("yyyy-MM-dd HH:mm:ss"));

        bServerShutdown = true;

        // Suppression du fichier PID
        RemovePidFile();

        // === Arrêt propre des services (ordre inverse de l'init) ===
        // 1. Arrêt des listeners réseau
        CLog.Notice("[~Network]", R_N_MASTER_8);
        worldListener?.Close();
        CLog.Success("[~Network]", R_N_MASTER_8_1);
        CLog.Notice("~[~Network]", R_N_MASTER_9);
        wsListener?.Close();
        CLog.Success("[~Network]", R_N_MASTER_9_1);

        // 2. Arrêt du LogonCommHandler
        CLog.Notice("[~Network]", R_N_MASTER_7);
        LogonCommHandler.Instance?.Dispose();
        CLog.Success("[~Network]", R_N_MASTER_10);

        // 3. Arrêt du SocketMgr
        CLog.Notice("[~Network]", R_N_MASTER_11);
        SocketMgr.CloseAll();
        CLog.Success("[~Network]", R_N_MASTER_11_1);

        // 4. Arrêt du ThreadPool
        CLog.Notice("[ThreadPool]", R_N_MASTER_15);
        NetworkThreadPool.Instance.Shutdown();
        CLog.Success("[ThreadPool]", R_N_MASTER_15_1);

        // 5. Nettoyage du BufferPool
        CLog.Notice("[BufferPool]", R_N_MASTER_16);
        BufferPool.Destroy();
        CLog.Success("[BufferPool]", R_N_MASTER_16_1);

        // 6. Fermeture de la base de données
        CLog.Notice("[Database]", R_N_MASTER_13);
        RealmDatabaseManager.RemoveDatabase();
        CLog.Success("[Database]", R_N_MASTER_14);

        // 7. Nettoyage des DBC/Storage
        CLog.Notice("[Storage]", R_N_MASTER_17);
        // DBCStores.Cleanup();
        CLog.Success("[Storage]", R_N_MASTER_17_1);

        // 8. Nettoyage des instances singleton
        CLog.Notice("[Fermeture]", R_N_MASTER_19);

        // Libération de ClusterMgr
        if (ClusterMgr.Instance != null)
        {
            ClusterMgr.Instance.Dispose();
            ClusterMgr.ResetInstance();
            CLog.Success("[~ClusterMgr]", R_N_MASTER_18_1);
        }

        // Libération de ClientMgr
        if (ClientMgr.Instance != null)
        {
            (ClientMgr.Instance as IDisposable)?.Dispose();
            ClientMgr.ResetInstance();
            CLog.Success("[~ClientMgr]", R_N_MASTER_18_1);
        }

        // Libération de ChannelMgr
        if (ChannelMgr.Instance != null)
        {
            ChannelMgr.Instance.Dispose();
            ChannelMgr.ResetInstance();
            CLog.Success("[~ChannelMgr]", R_N_MASTER_18_1);
        }

        CLog.Notice("[Fermeture]", R_N_MASTER_20);
        CLog.Notice("[Fermeture]", R_N_MASTER_21);
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
        int optimalThreadCount = Math.Min(logicalCoreCount * 2, MAX_TOTAL_THREADS);
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

        sLog.OutDebug("============ État du réseau ================");
        sLog.OutDebug($"Taux d'envoi : {sdata:F5}{rateExtensions[sextensionoffset]}");
        sLog.OutDebug($"Taux de réception : {rdata:F5}{rateExtensions[rextensionoffset]}");
        sLog.OutDebug($"Taux total : {tdata:F5}{rateExtensions[textensionoffset]}");
        sLog.OutString("============================================");

        // Réinitialiser les compteurs après affichage
        NetworkThreadPool.ResetCounters();
    }
}
