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
using System.Collections.Generic;
using System.Threading;

namespace WaadShared.Threading;

public class NetworkThreadPool : IDisposable
{
    private sealed class DelegateThreadTask : ThreadBase
    {
        private readonly Func<CancellationToken, bool> _runner;

        public DelegateThreadTask(Func<CancellationToken, bool> runner)
        {
            _runner = runner;
        }

        public override bool Run(CancellationToken token)
        {
            return _runner(token);
        }
    }

    private readonly ConcurrentDictionary<int, CustomThread> _workers = new();
    private readonly ConcurrentDictionary<int, byte> _busyWorkers = new();
    private readonly ConcurrentQueue<ThreadBase> _taskQueue = new();
    private readonly object _syncRoot = new();
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

    private int LiveWorkerCount
    {
        get
        {
            int count = 0;
            foreach (var worker in _workers.Values)
            {
                if (worker.ControlInterface != null && worker.ControlInterface.IsAlive)
                    count++;
            }
            return count;
        }
    }

    private int BusyWorkerCount
    {
        get
        {
            int count = 0;
            foreach (var workerId in _busyWorkers.Keys)
            {
                if (_workers.TryGetValue(workerId, out var worker) && worker.ControlInterface != null && worker.ControlInterface.IsAlive)
                    count++;
            }
            return count;
        }
    }

    private int IdleWorkerCount => Math.Max(0, LiveWorkerCount - BusyWorkerCount);

    private void SpawnWorker()
    {
        CustomThread worker = null;
        worker = new CustomThread(ct => WorkerLoop(worker, ct));
        _workers[worker.ManagedThreadId] = worker;
        worker.Start();
    }

    private bool WorkerLoop(CustomThread worker, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!_taskQueue.TryDequeue(out var task))
            {
                _busyWorkers.TryRemove(worker.ManagedThreadId, out _);
                _taskAvailableEvent.WaitOne(250);
                continue;
            }

            _busyWorkers[worker.ManagedThreadId] = 1;

            try
            {
                task?.Run(token);
            }
            catch (OperationCanceledException)
            {
                // Normal during shutdown.
            }
            catch (Exception ex)
            {
                CLog.Error("[NETWORK-THREADPOOL]", $"Worker {worker.ManagedThreadId} failed: {ex.Message}");
            }
            finally
            {
                _busyWorkers.TryRemove(worker.ManagedThreadId, out _);
            }
        }

        _busyWorkers.TryRemove(worker.ManagedThreadId, out _);
        return true;
    }

    private void EnsureMinimumThreads()
    {
        lock (_syncRoot)
        {
            int alive = LiveWorkerCount;
            if (alive >= _minThreads)
                return;

            int toAdd = _minThreads - alive;
            for (int i = 0; i < toAdd; i++)
                SpawnWorker();

            if (toAdd > 0)
                CLog.Debug("[NETWORK-THREADPOOL]", $"Added {toAdd} worker thread(s) to maintain minimum.");
        }
    }

    private void TrimExcessIdleWorkers()
    {
        lock (_syncRoot)
        {
            int alive = LiveWorkerCount;
            int targetMax = Math.Max(_minThreads, _maxThreads);
            if (alive <= targetMax)
                return;

            int toStop = alive - targetMax;
            foreach (var worker in _workers.Values)
            {
                if (toStop == 0)
                    break;

                if (_busyWorkers.ContainsKey(worker.ManagedThreadId))
                    continue;

                if (_workers.TryRemove(worker.ManagedThreadId, out var removed))
                {
                    removed.RequestCancellation();
                    toStop--;
                }
            }
        }
    }

    private void PruneDeadWorkers()
    {
        foreach (var kvp in _workers)
        {
            var worker = kvp.Value;
            if (worker.ControlInterface == null || !worker.ControlInterface.IsAlive)
            {
                _workers.TryRemove(kvp.Key, out _);
                _busyWorkers.TryRemove(kvp.Key, out _);
                try
                {
                    worker.Dispose();
                }
                catch
                {
                }
            }
        }
    }

    public void Startup(int initialThreadCount)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NetworkThreadPool));

        initialThreadCount = Math.Clamp(initialThreadCount, _minThreads, _maxThreads);

        lock (_syncRoot)
        {
            int alive = LiveWorkerCount;
            int toAdd = Math.Max(0, initialThreadCount - alive);
            for (int i = 0; i < toAdd; i++)
                SpawnWorker();

            CLog.Success("[NETWORK-THREADPOOL]", $"Started/updated pool with {LiveWorkerCount} live thread(s) (min: {_minThreads}, max: {_maxThreads}).");
        }
    }

    public void ExecuteTask(ThreadBase executionTarget)
    {
        if (executionTarget == null)
        {
            CLog.Error("[NETWORK-THREADPOOL]", "Attempt to execute a null task.");
            return;
        }

        _taskQueue.Enqueue(executionTarget);
        _taskAvailableEvent.Set();

        lock (_syncRoot)
        {
            int alive = LiveWorkerCount;
            int idle = IdleWorkerCount;
            int queued = _taskQueue.Count;

            if (queued > idle && alive < _maxThreads)
            {
                SpawnWorker();
                CLog.Debug("[NETWORK-THREADPOOL]", "Spawned one additional worker for pending network tasks.");
            }
        }
    }

    public void ExecuteTask(Func<CancellationToken, bool> taskRunner)
    {
        if (taskRunner == null)
        {
            CLog.Error("[NETWORK-THREADPOOL]", "Attempt to execute a null delegate task.");
            return;
        }

        ExecuteTask(new DelegateThreadTask(taskRunner));
    }

    public void IntegrityCheck()
    {
        if (_disposed)
            return;

        PruneDeadWorkers();
        EnsureMinimumThreads();
        TrimExcessIdleWorkers();

        if (!_taskQueue.IsEmpty)
            _taskAvailableEvent.Set();
    }

    public void SetThreadLimits(int minThreads, int maxThreads)
    {
        if (minThreads < 1)
            minThreads = 1;

        if (maxThreads < minThreads)
            maxThreads = minThreads;

        _minThreads = minThreads;
        _maxThreads = maxThreads;

        EnsureMinimumThreads();
        TrimExcessIdleWorkers();

        CLog.Success("[NETWORK-THREADPOOL]", $"Updated thread limits: min={_minThreads}, max={_maxThreads}");
    }

    public void ReleaseThread(CustomThread thread)
    {
        if (thread == null)
            return;

        // Network workers are persistent; releasing means marking as idle.
        _busyWorkers.TryRemove(thread.ManagedThreadId, out _);

        if (!_taskQueue.IsEmpty)
            _taskAvailableEvent.Set();
    }

    public void Shutdown()
    {
        if (_disposed) return;

        CLog.Debug("[NETWORK-THREADPOOL]", "Shutting down...");

        List<CustomThread> workers;
        lock (_syncRoot)
        {
            workers = [.._workers.Values];
            _workers.Clear();
            _busyWorkers.Clear();
            _taskQueue.Clear();
        }

        foreach (var thread in workers)
        {
            thread.RequestCancellation();
        }

        foreach (var thread in workers)
        {
            try
            {
                if (thread.ControlInterface != null && thread.ControlInterface.IsAlive)
                    thread.ControlInterface.Join(1000);
            }
            catch
            {
            }

            thread.Dispose();
        }

        _taskAvailableEvent.Dispose();

        _disposed = true;
        CLog.Success("[NETWORK-THREADPOOL]", "Shutdown complete.");
    }

    public void ShowStats()
    {
        int aliveWorkers = LiveWorkerCount;
        int busyWorkers = BusyWorkerCount;
        int idleWorkers = Math.Max(0, aliveWorkers - busyWorkers);

        CLog.Debug("[NETWORK-THREADPOOL]", "===== Network ThreadPool Stats =====");
        CLog.Debug("[NETWORK-THREADPOOL]", $"Active Threads: {busyWorkers} (alive: {busyWorkers})");
        CLog.Debug("[NETWORK-THREADPOOL]", $"Free Threads: {idleWorkers} (alive: {idleWorkers})");
        CLog.Debug("[NETWORK-THREADPOOL]", $"Total Live Workers: {aliveWorkers}");
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