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

using System.Collections.Concurrent;
using System.Threading;

namespace WaadShared.Threading
{
    /// <summary>
    /// Pool de threads dédié aux tâches réseau (I/O-bound).
    /// Optimisé pour minimiser la latence et gérer les pics de charge.
    /// </summary>
    public class NetworkThreadPool
    {
        private readonly ConcurrentBag<CustomThread> _activeThreads = [];
        private readonly ConcurrentBag<CustomThread> _freeThreads = [];
        private readonly ConcurrentQueue<ThreadBase> _taskQueue = new();
        private readonly ReaderWriterLockSlim _lock = new();

        private int _minThreads = 8;   // Nombre minimal de threads toujours actifs
        private int _maxThreads = 32;   // Nombre maximal de threads

        // Événement pour signaler de nouvelles tâches (optionnel pour async)
        private readonly AutoResetEvent _taskAvailableEvent = new(false);
        private static long _bytesSent = 0;
        private static long _bytesReceived = 0;

        public static long BytesSent => _bytesSent;
        public static long BytesReceived => _bytesReceived;

        // Singleton pour accès global
        private static NetworkThreadPool _instance;

        public static NetworkThreadPool Instance => _instance ??= new NetworkThreadPool();

        // Méthodes pour incrémenter les compteurs
        public static void AddBytesSent(long bytes)
        {
            Interlocked.Add(ref _bytesSent, bytes);
        }

        public static void AddBytesReceived(long bytes)
        {
            Interlocked.Add(ref _bytesReceived, bytes);
        }

        // Réinitialiser les compteurs (optionnel, si besoin)
        public static void ResetCounters()
        {
            Interlocked.Exchange(ref _bytesSent, 0);
            Interlocked.Exchange(ref _bytesReceived, 0);
        }


        /// <summary>
        /// Initialise le pool avec un nombre initial de threads.
        /// </summary>
        public void Startup(int initialThreadCount)
        {
            _minThreads = initialThreadCount;
            for (int i = 0; i < initialThreadCount; i++)
            {
                var thread = CustomThread.StartThread(null);
                _freeThreads.Add(thread);
            }
            CLog.Success("[NETWORK-THREADPOOL]", $"Started with {initialThreadCount} threads (min: {_minThreads}, max: {_maxThreads}).");
        }

        /// <summary>
        /// Exécute une tâche réseau dans le pool.
        /// </summary>
        public void ExecuteTask(ThreadBase executionTarget)
        {
            if (_freeThreads.TryTake(out CustomThread thread))
            {
                thread.ExecutionTarget = executionTarget;
                _activeThreads.Add(thread);
                CustomThread.Resume();
                CLog.Debug("[NETWORK-THREADPOOL]", $"Thread {thread.ManagedThreadId} executing network task.");
            }
            else if (_activeThreads.Count + _freeThreads.Count < _maxThreads)
            {
                // Créer un nouveau thread si la limite n'est pas atteinte
                var newThread = CustomThread.StartThread(executionTarget);
                _activeThreads.Add(newThread);
                CLog.Debug("[NETWORK-THREADPOOL]", $"Created new thread {newThread.ManagedThreadId} for network task.");
            }
            else
            {
                // Mettre en file d'attente si tous les threads sont occupés
                _taskQueue.Enqueue(executionTarget);
                CLog.Warning("[NETWORK-THREADPOOL]", $"Task queued. Active: {_activeThreads.Count}, Free: {_freeThreads.Count}, Queue: {_taskQueue.Count}");
                _taskAvailableEvent.Set(); // Signal qu'une tâche est disponible
            }
        }

        /// <summary>
        /// Vérifie l'intégrité du pool et ajuste le nombre de threads si nécessaire.
        /// </summary>
        public void IntegrityCheck()
        {
            // Maintenir le nombre minimal de threads
            if (_freeThreads.Count < _minThreads)
            {
                int threadsToAdd = _minThreads - _freeThreads.Count;
                for (int i = 0; i < threadsToAdd; i++)
                {
                    var thread = CustomThread.StartThread(null);
                    _freeThreads.Add(thread);
                }
                CLog.Debug("[NETWORK-THREADPOOL]", $"Added {threadsToAdd} threads to maintain minimum.");
            }

            // Traiter les tâches en file d'attente si des threads sont disponibles
            if (_taskQueue.Count > 0 && _freeThreads.Count > 0)
            {
                while (_taskQueue.TryDequeue(out ThreadBase task) && _freeThreads.TryTake(out CustomThread thread))
                {
                    thread.ExecutionTarget = task;
                    _activeThreads.Add(thread);
                    CustomThread.Resume();
                }
            }
        }

        /// <summary>
        /// Met à jour les limites de threads (min/max).
        /// </summary>
        public void SetThreadLimits(int minThreads, int maxThreads)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            CLog.Success("[NETWORK-THREADPOOL]", $"Updated thread limits: min={_minThreads}, max={_maxThreads}");
        }

        /// <summary>
        /// Libère un thread après exécution d'une tâche.
        /// </summary>
        public void ReleaseThread(CustomThread thread)
        {
            _activeThreads.TryTake(out _); // Retire le thread de la liste active
            _freeThreads.Add(thread);
            CLog.Debug("[NETWORK-THREADPOOL]", $"Thread {thread.ManagedThreadId} released to free pool.");

            // Vérifier s'il y a des tâches en attente
            if (_taskQueue.Count > 0 && !_freeThreads.IsEmpty)
                _taskAvailableEvent.Set();
        }

        /// <summary>
        /// Arrête proprement tous les threads du pool.
        /// </summary>
        public void Shutdown()
        {
            CLog.Debug("[NETWORK-THREADPOOL]", "Shutting down...");

            // Arrêter les threads actifs
            foreach (var thread in _activeThreads)
            {
                thread.ExecutionTarget?.OnShutdown();
                CustomThread.RequestCancellation();
            }

            // Arrêter les threads inactifs
            foreach (var thread in _freeThreads)
            {
                CustomThread.RequestCancellation();
            }

            _activeThreads.Clear();
            _freeThreads.Clear();
            _taskQueue.Clear();

            CLog.Success("[NETWORK-THREADPOOL]", "Shutdown complete.");
        }

        /// <summary>
        /// Affiche les statistiques du pool.
        /// </summary>
        public void ShowStats()
        {
            CLog.Debug("[NETWORK-THREADPOOL]", "===== Network ThreadPool Stats =====");
            CLog.Debug("[NETWORK-THREADPOOL]", $"Active Threads: {_activeThreads.Count}");
            CLog.Debug("[NETWORK-THREADPOOL]", $"Free Threads: {_freeThreads.Count}");
            CLog.Debug("[NETWORK-THREADPOOL]", $"Queued Tasks: {_taskQueue.Count}");
            CLog.Debug("[NETWORK-THREADPOOL]", $"Thread Limits: min={_minThreads}, max={_maxThreads}");
            CLog.Debug("[NETWORK-THREADPOOL]", "======================================");
        }
    }
}
