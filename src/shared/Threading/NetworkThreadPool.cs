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
using System.Collections.Concurrent;
using System.Threading;

namespace WaadShared.Threading;

public class NetworkThreadPool : IDisposable
{
    private readonly ConcurrentBag<CustomThread> _activeThreads = [];
    private readonly ConcurrentBag<CustomThread> _freeThreads = [];
    private readonly ConcurrentQueue<ThreadBase> _taskQueue = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly AutoResetEvent _taskAvailableEvent = new(false);

    private int _minThreads = 8;
    private int _maxThreads = 32;
    private static long _bytesSent = 0;
    private static long _bytesReceived = 0;
    private bool _disposed = false;

    public static long BytesSent => _bytesSent;
    public static long BytesReceived => _bytesReceived;
    private static NetworkThreadPool _instance;
    public static NetworkThreadPool Instance => _instance ??= new NetworkThreadPool();

    public static void AddBytesSent(long bytes) => Interlocked.Add(ref _bytesSent, bytes);
    public static void AddBytesReceived(long bytes) => Interlocked.Add(ref _bytesReceived, bytes);
    public static void ResetCounters()
    {
        Interlocked.Exchange(ref _bytesSent, 0);
        Interlocked.Exchange(ref _bytesReceived, 0);
    }

    public void Startup(int initialThreadCount)
    {
        if (initialThreadCount < _minThreads)
            initialThreadCount = _minThreads;

        for (int i = 0; i < initialThreadCount; i++)
        {
            var thread = new CustomThread(ct => false);
            thread.Start();
            _freeThreads.Add(thread);
        }
        CLog.Success("[NETWORK-THREADPOOL]", $"Started with {initialThreadCount} threads (min: {_minThreads}, max: {_maxThreads}).");
    }

    public void ExecuteTask(ThreadBase executionTarget)
    {
        if (executionTarget == null)
        {
            CLog.Error("[NETWORK-THREADPOOL]", "Attempt to execute a null task.");
            return;
        }

        // Réutiliser un thread libre si disponible
        if (_freeThreads.TryTake(out CustomThread freeThread))
        {
            freeThread.ExecutionTarget = executionTarget;
            _activeThreads.Add(freeThread);
            CLog.Debug("[NETWORK-THREADPOOL]", $"Reused thread {freeThread.ManagedThreadId} for network task.");
            return;
        }

        // Sinon, créer un nouveau thread (si on n'a pas atteint _maxThreads)
        if (_activeThreads.Count + _freeThreads.Count < _maxThreads)
        {
            var newThread = new CustomThread(ct => executionTarget.Run(ct));
            newThread.Start();
            _activeThreads.Add(newThread);
            CLog.Debug("[NETWORK-THREADPOOL]", $"Created new thread {newThread.ManagedThreadId} for network task.");
        }
        else
        {
            // Si on a atteint _maxThreads, mettre la tâche en file d'attente
            _taskQueue.Enqueue(executionTarget);
            CLog.Debug("[NETWORK-THREADPOOL]", "Queued task (max threads reached).");
        }
    }

    public void IntegrityCheck()
    {
        // Ne créer des threads que si nécessaire (tâches en attente)
        if (_taskQueue.Count > 0 && _freeThreads.Count < _minThreads)
        {
            int threadsToAdd = Math.Min(
                _minThreads - _freeThreads.Count,
                _maxThreads - (_activeThreads.Count + _freeThreads.Count)
            );
            for (int i = 0; i < threadsToAdd; i++)
            {
                var thread = new CustomThread(ct => false);
                thread.Start();
                _freeThreads.Add(thread);
            }
            CLog.Debug("[NETWORK-THREADPOOL]", $"Added {threadsToAdd} threads to maintain minimum.");
        }

        // Traiter les tâches en file d'attente si des threads sont disponibles
        if (!_taskQueue.IsEmpty && !_freeThreads.IsEmpty)
        {
            while (_taskQueue.TryDequeue(out ThreadBase task) && _freeThreads.TryTake(out CustomThread thread))
            {
                thread.ExecutionTarget = task;
                _activeThreads.Add(thread);
                // Ne pas appeler thread.Start() ici (le thread est déjà démarré)
            }
        }
    }

    public void SetThreadLimits(int minThreads, int maxThreads)
    {
        _minThreads = minThreads;
        _maxThreads = maxThreads;
        CLog.Success("[NETWORK-THREADPOOL]", $"Updated thread limits: min={_minThreads}, max={_maxThreads}");
    }

    public void ReleaseThread(CustomThread thread)
    {
        if (_activeThreads.TryTake(out var activeThread) && activeThread == thread)
        {
            thread.ExecutionTarget = null;
            _freeThreads.Add(thread);
            CLog.Debug("[NETWORK-THREADPOOL]", $"Thread {thread.ManagedThreadId} released to free pool.");

            if (!_taskQueue.IsEmpty)
                _taskAvailableEvent.Set();
        }
        else
        {
            thread.Dispose();
            CLog.Warning("[NETWORK-THREADPOOL]", $"Thread {thread.ManagedThreadId} was not in active threads. Disposed.");
        }
    }

    public void Shutdown()
    {
        if (_disposed) return;

        CLog.Debug("[NETWORK-THREADPOOL]", "Shutting down...");

        foreach (var thread in _activeThreads)
        {
            thread.ExecutionTarget?.OnShutdown();
            thread.RequestCancellation();
            thread.Dispose();
        }

        foreach (var thread in _freeThreads)
        {
            thread.ExecutionTarget?.OnShutdown();
            thread.RequestCancellation();
            thread.Dispose();
        }

        _activeThreads.Clear();
        _freeThreads.Clear();
        _taskQueue.Clear();

        _taskAvailableEvent.Dispose();
        _lock.Dispose();

        _disposed = true;
        CLog.Success("[NETWORK-THREADPOOL]", "Shutdown complete.");
    }

    public void ShowStats()
    {
        CLog.Debug("[NETWORK-THREADPOOL]", "===== Network ThreadPool Stats =====");
        CLog.Debug("[NETWORK-THREADPOOL]", $"Active Threads: {_activeThreads.Count}");
        CLog.Debug("[NETWORK-THREADPOOL]", $"Free Threads: {_freeThreads.Count}");
        CLog.Debug("[NETWORK-THREADPOOL]", $"Queued Tasks: {_taskQueue.Count}");
        CLog.Debug("[NETWORK-THREADPOOL]", $"Thread Limits: min={_minThreads}, max={_maxThreads}");
        CLog.Debug("[NETWORK-THREADPOOL]", "======================================");
    }

    public void Dispose()
    {
        Shutdown();
        GC.SuppressFinalize(this);
    }
}