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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using WaadShared;
using WaadShared.Config;
using WaadShared.Network;
using WaadShared.RandomGen;
using WaadShared.Threading;

using static WaadShared.Common;
using static WaadShared.Main;
using static WaadShared.Master;

namespace WaadWorldServer;

public static class Master
{
    // === Constantes globales ===
    private static volatile bool m_stopEvent = false;
    public static bool StopEvent
    {
        get => m_stopEvent;
        set => m_stopEvent = value;
    }

    private static string configFile = Path.Combine(AppContext.BaseDirectory, "waad-world.ini");
    private static readonly Logger sLog = new();
    private static readonly ConfigMgr Config = new();
    private static readonly BufferPool BufferPool = new();
    private static readonly SocketMgr SocketMgr = new();
    private static DateTime UNIXTIME;
    private static readonly string BANNER = "| WAAD {0} r{1}/{2}-{3} ({4}) :: World Server           |";

    // === Gestion des threads ===
    private static readonly CancellationTokenSource s_cts = new();
    private static readonly ManualResetEventSlim s_mainLoopDelay = new(false);
    private const int OBJECT_WAIT_TIME = 50; // ms
    private static readonly int s_taskListThreadCount = Environment.ProcessorCount;

    // === Gestion du PID (Unix) ===
    private static readonly string PID_FILE = "waad-worldserver.pid";
    private static volatile bool m_crashed = false;
    private static readonly object m_crashedMutex = new();
    private static int s_fiveMinuteHeavyMaintenanceRunning = 0;

    // === Informations Git ===
    public static string BRANCH_NAME { get; set; } = "unknown";
    public static int REVISION { get; set; } = 0;
    public static int DbcLoadTimeout { get; private set; } = 30000;
    public static bool ServerShutdown { get; set; } = false;

    // === Configuration réseau ===
    private static uint worldServerPort = 8129;
    private static string listenHost = "0.0.0.0";
    private static string rsHostName = "127.0.0.1";
    private static uint rsPort = 11010;
    private static string rsPassword = "change_me_logon";

    // === Méthodes de gestion des signaux ===
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
            case "SIGTERM":
            case "SIGINT":
                s_cts.Cancel();
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
            sLog.OutString("[Crash] Sauvegarde de l'état avant le crash...");
            WorldDatabaseManager.RemoveDatabases();
            sLog.OutString("[Crash] Connexions aux bases de données fermées.");
        }
        catch (Exception ex)
        {
            sLog.OutError($"[Crash] Exception lors de la gestion du crash : {ex.Message}");
        }

        sLog.OutString("[Crash] Arrêt en cours...");
        Environment.Exit(1);
    }

    // === Gestion du fichier PID ===
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

    // === Rehash (rechargement de la configuration) ===
    public static void Rehash(bool load)
    {
        if (load)
            Config.WorldConfig.SetSource(configFile);

        // Chargement des chemins DBC
        DBCStores.DbcPath = WorldConfig.DBCPath;

        // Timeout du chargement des DBCs
        DbcLoadTimeout = Config.WorldConfig.GetInt32("Startup", "DbcLoadTimeout", 30000);

        // Ports et configuration réseau
        worldServerPort = Config.WorldConfig.GetUInt32("Listen", "WorldServerPort", 8129);
        listenHost = Config.WorldConfig.GetString("Listen", "Host", "0.0.0.0");

        // Configuration du cluster (LogonServer)
        rsHostName = Config.WorldConfig.GetString("Cluster", "RSHostName", "127.0.0.1");
        rsPort = Config.WorldConfig.GetUInt32("Cluster", "RSPort", 11010);
        rsPassword = Config.WorldConfig.GetString("Cluster", "RSPassword", "change_me_logon");
    }

    // === Lecture des infos Git ===
    private static (string, int)? ReadGitInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("waad-world.GitInfo.txt");
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

    // === Point d'entrée principal ===
    public static void Main(string[] args)
    {
        var gitInfo = ReadGitInfo();
        if (gitInfo.HasValue)
        {
            BRANCH_NAME = gitInfo.Value.Item1;
            REVISION = gitInfo.Value.Item2;
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

    // === Démarrage principal ===
    public static void Run(string[] args)
    {
        UNIXTIME = DateTime.UtcNow;
        int fileLogLevel = -1;
        int screenLogLevel = 3;
        bool doCheckConf = false;
        bool doVersion = false;

        // Parsing des arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--checkconf": doCheckConf = true; break;
                case "--screenloglevel": if (i + 1 < args.Length) screenLogLevel = int.Parse(args[++i]); break;
                case "--fileloglevel": if (i + 1 < args.Length) fileLogLevel = int.Parse(args[++i]); break;
                case "--version": doVersion = true; break;
                case "--conf": if (i + 1 < args.Length) configFile = args[++i]; break;
            }
        }

        // Initialisation des logs
        if (!doVersion && !doCheckConf)
        {
            sLog.Init(fileLogLevel, screenLogLevel);
        }
        else
        {
            sLog.Init(fileLogLevel, 1);
        }

        // Affichage de la bannière
        sLog.OutString("|=============================================================|");
        sLog.OutString("| Waad Cluster System - World Server                          |");
        sLog.OutString("| Version 1.0                                                 |");
        sLog.OutString(BANNER, BRANCH_NAME, REVISION, CONFIG, PLATFORM_TEXT, ARCH);
        sLog.OutString("|=============================================================|");
        sLog.OutString("");
        sLog.OutString("Copyright (C) 2007-2025 WAAD Team. https://arbonne.games-rpg.net/");
        sLog.OutString("From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore");
        sLog.OutString("This program comes with ABSOLUTELY NO WARRANTY, and is FREE SOFTWARE.");
        sLog.OutString("You are welcome to redistribute it under the terms of the GNU Affero");
        sLog.OutString("General Public License, either version 3 or any later version.");
        sLog.OutString("");

        if (doVersion)
            return;

        if (doCheckConf)
        {
            sLog.OutString("[Config] Vérification du fichier de configuration: {0}", configFile);
            if (Config.WorldConfig.SetSource(configFile))
                CLog.Success("[Config]", "Fichier de configuration valide.");
            else
                sLog.OutError("[Config] Fichier de configuration invalide.");
            return;
        }

        // Chargement de la configuration
        if (!Config.WorldConfig.SetSource(configFile))
        {
            sLog.OutError("[Config] Impossible de charger le fichier de configuration: {0}", configFile);
            return;
        }

        // Mise à jour des logs selon la configuration
        sLog.Init(
            Config.WorldConfig.GetInt32("LogLevel", "File", -1),
            Config.WorldConfig.GetInt32("LogLevel", "Screen", 1)
        );

        // Vérification des configurations "die"
        string die = Config.WorldConfig.GetString("die", "msg");
        string die2 = Config.WorldConfig.GetString("die2", "msg");
        if (!string.IsNullOrEmpty(die) || !string.IsNullOrEmpty(die2))
        {
            sLog.OutError("[Config] Le serveur est configuré pour ne pas démarrer (die/die2).");
            return;
        }

        Rehash(true);

        // Gestion de CTRL+C
        Console.CancelKeyPress += (sender, e) =>
        {
            CLog.Notice("[Main]", L_N_MAIN_12_A);
            s_cts.Cancel();
            m_stopEvent = true;
            e.Cancel = true;
        };

        // Initialisation du générateur de nombres aléatoires
        MersenneTwister.InitRandomNumberGenerators();
        CLog.Success("[Rnd]", R_S_MASTER_1);

        // Initialisation du BufferPool
        BufferPool.Init();

        // Initialisation des bases de données (World et Character)
        if (!WorldDatabaseManager.InitializeDatabases(Config, sLog))
        {
            sLog.OutError("[Database]", R_E_MASTER_7);
            WorldDatabaseManager.RemoveDatabases();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }
        CLog.Success("[Database]", R_S_MASTER_2);

        // Vérification du répertoire DBC
        string dbcPath = Config.WorldConfig.GetString("Path", "DBCPath", "dbc");
        if (!Directory.Exists(dbcPath))
        {
            sLog.OutError($"[Storage] Le répertoire DBC est introuvable : {dbcPath}");
            WorldDatabaseManager.RemoveDatabases();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }

        // Chargement des DBC
        if (!DBCStores.LoadDBCs())
        {
            sLog.OutError("[Storage]", R_E_MASTER_2);
            WorldDatabaseManager.RemoveDatabases();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }
        CLog.Success("[Storage]", R_S_MASTER_3);

        // Attente de la fin du chargement des DBCs
        if (!DBCStores.WaitForDbcLoading(DbcLoadTimeout))
        {
            sLog.OutError("[Storage] Les DBCs n'ont pas pu être chargés dans le délai imparti.");
            WorldDatabaseManager.RemoveDatabases();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }
        sLog.OutDebug("[Storage] DBCs complètement chargés et prêts.");

        CLog.Notice("[Storage]", R_S_MASTER_3_1, s_taskListThreadCount);

        // Initialisation des singletons (EventMgr/World/ScriptMgr du Master.cpp original)
        _ = WorldMgr.GetInstance();
        _ = ObjectMgr.GetInstance();
        _ = MapMgr.GetInstance();

        // Initialisation de la table des gestionnaires de paquets (WorldSession::InitPacketHandlerTable)
        WorldSession.InitPacketHandlerTable();

        // Démarrage du SocketMgr
        SocketMgr.SpawnWorkerThreads();

        // Création du listener pour les clients
        ListenSocket<WorldSocket> worldListener = null;
        try
        {
            CLog.Success("[Network]", R_S_MASTER_4);
            worldListener = new ListenSocket<WorldSocket>(listenHost, worldServerPort, (socket) => new WorldSocket(socket));
        }
        catch (Exception ex)
        {
            sLog.OutError("[Network] Exception lors de l'ouverture du listener : " + ex.Message);
            SocketMgr.CloseAll();
            WorldDatabaseManager.RemoveDatabases();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }

        if (!worldListener.IsOpen())
        {
            sLog.OutError("[Network] Impossible d'ouvrir le port " + worldServerPort);
            SocketMgr.CloseAll();
            WorldDatabaseManager.RemoveDatabases();
            NetworkThreadPool.Instance.Shutdown();
            BufferPool.Destroy();
            return;
        }

        // Démarrage du thread du listener
        var worldListenerThread = new Thread(() =>
        {
            try
            {
                worldListener.Run(s_cts.Token);
            }
            catch (OperationCanceledException)
            {
                sLog.OutDebug("[WorldListener] Arrêt demandé.");
            }
            catch (Exception ex)
            {
                sLog.OutError($"[WorldListener] Exception : {ex}");
                m_stopEvent = true;
            }
        })
        {
            IsBackground = true,
            Name = "WorldListener"
        };
        worldListenerThread.Start();

        // Connexion au serveur de Royaume(s) (si mode cluster) - Master.cpp: sClusterInterface.ConnectToRealmServer()
        if (Config.WorldConfig.GetBoolean("Cluster", "EnableClusterMode", true))
        {
            try
            {
                ClusterInterface.Instance.Startup(rsHostName, rsPort, rsPassword);
                CLog.Success("[Network]", "Connexion au serveur de Royaume(s) établie.");
            }
            catch (Exception ex)
            {
                sLog.OutError("[Network] Échec de la connexion au serveur de Royaume(s) : " + ex.Message);
                SocketMgr.CloseAll();
                WorldDatabaseManager.RemoveDatabases();
                NetworkThreadPool.Instance.Shutdown();
                BufferPool.Destroy();
                return;
            }
        }

        // Thread de console locale (Master.cpp: ConsoleThread * console = new ConsoleThread();)
        ConsoleThread consoleThread = new();
        var localConsoleThread = new Thread(() =>
        {
            try
            {
                consoleThread.Run(s_cts.Token);
            }
            catch (OperationCanceledException)
            {
                sLog.OutDebug("[WorldConsole] Arrêt demandé.");
            }
            catch (Exception ex)
            {
                sLog.OutError($"[WorldConsole] Exception : {ex}");
                m_stopEvent = true;
            }
        })
        {
            IsBackground = true,
            Name = "WorldConsole"
        };
        localConsoleThread.Start();
        CLog.Notice("[Console]", "Thread de console local démarré.");

        // Écriture du fichier PID
        WritePidFile();

        // Enregistrement des signaux POSIX (Unix uniquement)
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                PosixSignalRegistration.Create(PosixSignal.SIGHUP, ctx => { HandleSignal("SIGHUP"); ctx.Cancel = true; });
                PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { s_cts.Cancel(); m_stopEvent = true; ctx.Cancel = true; });
                PosixSignalRegistration.Create(PosixSignal.SIGINT, ctx => { s_cts.Cancel(); m_stopEvent = true; ctx.Cancel = true; });
            }
            catch
            {
                // Ignorer si non supporté
            }
        }

        sLog.OutString("[Main] Démarrage terminé. Entrée dans la boucle principale.");

        // === Boucle principale ===
        ulong loopCounter = 0;
        while (!m_stopEvent && !ServerShutdown && !s_cts.IsCancellationRequested)
        {
            loopCounter++;

            // Tâches périodiques
            HandlePeriodicTasks(loopCounter);

            // Mise à jour des services
            if (Config.WorldConfig.GetBoolean("Cluster", "EnableClusterMode", true))
                ClusterInterface.Instance.Update();

            WorldMgr.Instance.Update();
            ObjectMgr.Instance.Update();
            MapMgr.Instance.Update();
            SocketGarbageCollector.Instance.Update();

            s_mainLoopDelay.Wait(OBJECT_WAIT_TIME);
        }

        // === Début de la séquence d'arrêt ===
        CLog.Notice("[Fermeture]", R_N_MASTER_6, UNIXTIME.ToString("yyyy-MM-dd HH:mm:ss"));
        ServerShutdown = true;
        s_cts.Cancel();
        s_mainLoopDelay.Set();

        // Suppression du fichier PID
        RemovePidFile();

        // Arrêt des services (ordre inverse de l'initialisation)
        CLog.Notice("[~Network]", R_N_MASTER_8);
        worldListener?.Close();
        CLog.Success("[~Network]", R_N_MASTER_8_1);

        if (Config.WorldConfig.GetBoolean("Cluster", "EnableClusterMode", true))
        {
            CLog.Notice("[~Network]", "Fermeture de la connexion au serveur de Royaume(s)...");
            ClusterInterface.Instance.Dispose();
            CLog.Success("[~Network]", "Connexion au serveur de Royaume(s) fermée.");
        }

        CLog.Notice("[~Network]", R_N_MASTER_11);
        SocketMgr.CloseAll();
        CLog.Success("[~Network]", R_N_MASTER_11_1);

        CLog.Notice("[ThreadPool]", R_N_MASTER_15);
        NetworkThreadPool.Instance.Shutdown();
        CLog.Success("[ThreadPool]", R_N_MASTER_15_1);

        CLog.Notice("[BufferPool]", R_N_MASTER_16);
        BufferPool.Destroy();
        CLog.Success("[BufferPool]", R_N_MASTER_16_1);

        CLog.Notice("[Database]", R_N_MASTER_13);
        WorldDatabaseManager.RemoveDatabases();
        CLog.Success("[Database]", R_N_MASTER_14);

        CLog.Notice("[Storage]", R_N_MASTER_17);    
        CLog.Success("[Storage]", R_N_MASTER_17_1);

        // Libération des singletons
        CLog.Notice("[Shutdown]", "Libération des instances singleton...");
        MapMgr.GetInstancePtr()?.Dispose();
        MapMgr.DestroyInstance();
        ObjectMgr.GetInstancePtr()?.Dispose();
        ObjectMgr.DestroyInstance();
        WorldMgr.GetInstancePtr()?.Dispose();
        WorldMgr.DestroyInstance();
        CLog.Success("[Shutdown]", "Instances singleton libérées.");

        CLog.Notice("[Shutdown]", "Arrêt terminé proprement.");
        CLog.Notice("[Shutdown]", "Au revoir !");
    }

    // === Gestion des tâches périodiques ===
    private static void HandlePeriodicTasks(ulong loopCounter)
    {
        // Toutes les 1 minute
        if ((loopCounter % (60UL * (1000UL / (ulong)OBJECT_WAIT_TIME))) == 0)
        {
            ShowNetworkStats();
        }

        // Toutes les 5 minutes (Master.cpp: ThreadPool.ShowStats(); ThreadPool.IntegrityCheck(); g_bufferPool.Optimize();)
        if ((loopCounter % (300UL * (1000UL / (ulong)OBJECT_WAIT_TIME))) == 0)
        {
            NetworkThreadPool.Instance.ShowStats();
            TryRunFiveMinuteHeavyMaintenance();
        }
    }

    private static void TryRunFiveMinuteHeavyMaintenance()
    {
        if (Interlocked.CompareExchange(ref s_fiveMinuteHeavyMaintenanceRunning, 1, 0) != 0)
        {
            CLog.Warning("[Master]", "Maintenance lourde 5 minutes déjà en cours, saut...");
            return;
        }

        NetworkThreadPool.Instance.ExecuteTask(ct =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                NetworkThreadPool.Instance.IntegrityCheck();
                BufferPool.Optimize();
            }
            catch (Exception ex)
            {
                CLog.Error("[Master]", "Échec de la maintenance lourde 5 minutes : {0}", ex.Message);
            }
            finally
            {
                sw.Stop();
                CLog.Debug("[Master]", "Maintenance lourde 5 minutes terminée en {0} ms.", sw.ElapsedMilliseconds);
                Interlocked.Exchange(ref s_fiveMinuteHeavyMaintenanceRunning, 0);
            }
            return true;
        });
    }

    // === Affichage des statistiques réseau ===
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
        sLog.OutDebug("Taux d'envoi : {0:F5}{1}", sdata, rateExtensions[sextensionoffset]);
        sLog.OutDebug("Taux de réception : {0:F5}{1}", rdata, rateExtensions[rextensionoffset]);
        sLog.OutDebug("Taux total : {0:F5}{1}", tdata, rateExtensions[textensionoffset]);
        sLog.OutDebug("============================================");

        NetworkThreadPool.ResetCounters();
    }
}