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
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WaadShared.Threading;

// Définir ThreadBase dans un seul endroit
public abstract class ThreadBase
{
    public bool mrunning;

    public abstract bool Run(CancellationToken token);
    public virtual void OnShutdown() { }
}

public class ThreadPool : IDisposable
{
    private readonly HashSet<CustomThread> activeThreads = [];
    private readonly HashSet<CustomThread> freeThreads = [];
    private readonly object mutex = new();
    private int threadsToExit = 0;
    private int _threadsExitedSinceLastCheck;
    private int _threadsRequestedSinceLastCheck;
    private int _threadsEaten;
    private bool _disposed = false;

    public ThreadPool()
    {
        threadsToExit = 0;
        _threadsExitedSinceLastCheck = 0;
        _threadsRequestedSinceLastCheck = 0;
        _threadsEaten = 0;
    }

    public bool ThreadExit(CustomThread t)
    {
        lock (mutex)
        {
            activeThreads.Remove(t);

            if (threadsToExit > 0)
            {
                --threadsToExit;
                ++_threadsExitedSinceLastCheck;
                if (t.DeleteAfterExit)
                    freeThreads.Remove(t);

                t.RequestCancellation();
                return false;
            }

            ++_threadsExitedSinceLastCheck;
            ++_threadsEaten;
            if (freeThreads.Contains(t))
            {
                CLog.Debug("[THREADPOOL]", $"Thread {t.ManagedThreadId} duplicated with thread {t.ManagedThreadId}");
            }
            freeThreads.Add(t);

            CLog.Debug("[THREADPOOL]", $"Thread {t.ManagedThreadId} entered the free pool.");
            return true;
        }
    }

    public void ExecuteTask(ThreadBase executionTarget)
    {
        CustomThread t;
        lock (mutex)
        {
            ++_threadsRequestedSinceLastCheck;
            --_threadsEaten;

            if (freeThreads.Count > 0)
            {
                var enumerator = freeThreads.GetEnumerator();
                enumerator.MoveNext();
                t = enumerator.Current;
                freeThreads.Remove(t);

                t.ExecutionTarget = executionTarget;
                t.Start();

                CLog.Debug("[THREADPOOL]", $"Thread {t.ManagedThreadId} left the thread pool.");
            }
            else
            {
                t = StartThread(executionTarget);
            }

            CLog.Debug("[THREADPOOL]", $"Thread {t.ManagedThreadId} is now executing task.");
            activeThreads.Add(t);
        }
    }

    public void ShowStats()
    {
        lock (mutex)
        {
            Console.WriteLine("============ ThreadPool Status =============");
            Console.WriteLine($"Active Threads: {activeThreads.Count}");
            Console.WriteLine($"Suspended Threads: {freeThreads.Count}");
            Console.WriteLine($"Requested-To-Freed Ratio: {(float)(_threadsRequestedSinceLastCheck + 1) / (_threadsExitedSinceLastCheck + 1) * 100.0f:F3}% ({_threadsRequestedSinceLastCheck}/{_threadsExitedSinceLastCheck})");
            Console.WriteLine($"Eaten Count: {_threadsEaten} (negative is bad!)");
            Console.WriteLine("============================================");
        }
    }

    public void IntegrityCheck(byte threadCount)
    {
        lock (mutex)
        {
            int gobbled = _threadsEaten;

            if (gobbled < 0)
            {
                uint new_threads = (uint)(Math.Abs(gobbled) + threadCount);
                _threadsEaten = 0;

                for (uint i = 0; i < new_threads; ++i)
                    StartThread(null);

                CLog.Debug("[THREADPOOL]", $"IntegrityCheck: (gobbled < 0) Spawning {new_threads} threads.");
            }
            else if (gobbled < threadCount)
            {
                uint new_threads = (uint)(threadCount - gobbled);
                for (uint i = 0; i < new_threads; ++i)
                    StartThread(null);

                CLog.Debug("[THREADPOOL]", $"IntegrityCheck: (gobbled < {threadCount}) Spawning {new_threads} threads.");
            }
            else if (gobbled > threadCount)
            {
                uint kill_count = (uint)(gobbled - threadCount);
                KillFreeThreads(kill_count);
                _threadsEaten -= (int)kill_count;
                CLog.Debug("[THREADPOOL]", $"IntegrityCheck: (gobbled > {threadCount}) Killing {kill_count} threads.");
            }
            else
            {
                CLog.Success("[THREADPOOL]", "IntegrityCheck: Perfect!");
            }

            _threadsExitedSinceLastCheck = 0;
            _threadsRequestedSinceLastCheck = 0;
        }
    }

    public void KillFreeThreads(uint count)
    {
        CLog.Debug("[THREADPOOL]", $"Killing {count} excess threads.");
        lock (mutex)
        {
            var threadsToKill = new List<CustomThread>();
            foreach (var t in freeThreads)
            {
                if (threadsToKill.Count >= count) break;
                threadsToKill.Add(t);
            }

            foreach (var t in threadsToKill)
            {
                t.ExecutionTarget = null;
                t.DeleteAfterExit = true;
                ++threadsToExit;
                t.RequestCancellation();
                freeThreads.Remove(t);
            }
        }
    }

    public void Shutdown()
    {
        lock (mutex)
        {
            int tcount = activeThreads.Count + freeThreads.Count;
            CLog.Debug("[THREADPOOL]", $"Shutting down {tcount} threads.");
            KillFreeThreads((uint)freeThreads.Count);
            threadsToExit += activeThreads.Count;

            foreach (var t in activeThreads)
            {
                t.ExecutionTarget?.OnShutdown();
                t.RequestCancellation();
            }
        }

        // Attendre la fin des threads avec un timeout
        var shutdownTask = Task.Run(async () =>
        {
            int timeoutSeconds = 10; // Timeout global de 10 secondes
            while (timeoutSeconds-- > 0)
            {
                lock (mutex)
                {
                    if (activeThreads.Count == 0 && freeThreads.Count == 0)
                        return;
                }
                await Task.Delay(1000); // Attendre 1 seconde
            }
            CLog.Warning("[THREADPOOL]", "Timeout reached while shutting down threads.");
        });

        shutdownTask.Wait();
    }

    internal static CustomThread StartThread(ThreadBase executionTarget)
    {
        if (executionTarget == null)
        {
            CLog.Debug("[THREADPOOL]", "Attempt to start a thread with no execution target.");
            return null;
        }

        CLog.Debug("[THREADPOOL]", "Starting a new custom thread.");

        var t = new CustomThread(token => RunThread(executionTarget, token));
        t.ExecutionTarget = executionTarget;
        t.Start();
        return t;
    }

    internal static bool RunThread(ThreadBase target, CancellationToken token)
    {
        if (target == null)
        {
            CLog.Debug("[THREADPOOL]", "Thread has no execution target.");
            return false;
        }

        try
        {
            return target.Run(token);
        }
        catch (Exception ex)
        {
            CLog.Error("[THREADPOOL]", $"Thread crashed: {ex.Message}");
            return false;
        }
    }

    public static void Startup(byte threadCount)
    {
        CLog.Success("[THREADPOOL]", $"Startup, thread pool initialized (no pre-created threads).");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Shutdown();
                foreach (var t in activeThreads)
                {
                    t.Dispose();
                }
                foreach (var t in freeThreads)
                {
                    t.Dispose();
                }
            }
            _disposed = true;
        }
    }
}

public class CustomThread : IDisposable
{
    private static readonly object threadIdLock = new();
    private static int threadid_count;
    private bool _disposed = false;
    private readonly ManualResetEvent _pauseEvent = new(true); // true = non suspendu

    public ThreadBase ExecutionTarget { get; set; }
    public bool DeleteAfterExit { get; set; }
    public Thread ControlInterface { get; private set; }
    public int ManagedThreadId { get; private set; }
    private readonly CancellationTokenSource _cts;

    public CustomThread(Func<CancellationToken, bool> target)
    {
        _cts = new CancellationTokenSource();
        ManagedThreadId = GenerateThreadId();
        ControlInterface = new Thread(() =>
        {
            _pauseEvent.WaitOne(); // Attend si suspendu
            target(_cts.Token);
        });
    }

    public void Start() => ControlInterface.Start();

    public void RequestCancellation() => _cts.Cancel();

    public void Pause() => _pauseEvent.Reset(); // Suspend le thread
    public void Resume() => _pauseEvent.Set(); // Reprend le thread

    public static void Abort()
    {
        // Abort the thread if necessary
    }

    private static bool RunThread(Func<bool> target)
    {
        bool res = false;
        try
        {
            res = target();
        }
        catch (Exception ex)
        {
            CLog.Error("[THREADPOOL]", $"Thread crashed: {ex.Message}");
        }
        return res;
    }

    private static int GenerateThreadId()
    {
        lock (threadIdLock)
        {
            return ++threadid_count;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                RequestCancellation();
                try
                {
                    ControlInterface?.Join(5000); // Timeout de 5 secondes
                }
                catch (ThreadStateException)
                {
                    // Le thread n'a pas démarré ou est déjà terminé
                }
                _cts?.Dispose();
            }
            _disposed = true;
        }
    }
}