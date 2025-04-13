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
using System.Collections.Generic;
using System.Threading;

namespace WaadShared.Threading;

// Définir ThreadBase dans un seul endroit
public abstract class ThreadBase
{
    public readonly bool mrunning;

    public abstract bool Run(CancellationToken token);
    public virtual void OnShutdown() { }
}

public class ThreadPool
{
    private readonly HashSet<CustomThread> activeThreads = [];
    private readonly HashSet<CustomThread> freeThreads = [];
    private readonly Mutex mutex = new();
    private int threadsToExit = 0;
    private int _threadsExitedSinceLastCheck;
    private int _threadsRequestedSinceLastCheck;
    private int _threadsEaten;

    public ThreadPool()
    {
        threadsToExit = 0;
        _threadsExitedSinceLastCheck = 0;
        _threadsRequestedSinceLastCheck = 0;
        _threadsEaten = 0;
    }

    public bool ThreadExit(CustomThread t)
    {
        mutex.WaitOne();

        activeThreads.Remove(t);

        if (threadsToExit > 0)
        {
            --threadsToExit;
            ++_threadsExitedSinceLastCheck;
            if (t.DeleteAfterExit)
                freeThreads.Remove(t);

            mutex.ReleaseMutex();
            CustomThread.RequestCancellation();
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
        mutex.ReleaseMutex();
        return true;
    }

    public void ExecuteTask(ThreadBase executionTarget)
    {
        CustomThread t;
        mutex.WaitOne();
        ++_threadsRequestedSinceLastCheck;
        --_threadsEaten;

        if (freeThreads.Count > 0)
        {
            var enumerator = freeThreads.GetEnumerator();
            enumerator.MoveNext();
            t = enumerator.Current;
            freeThreads.Remove(t);

            t.ExecutionTarget = executionTarget;

            CustomThread.Resume();
            CLog.Debug("[THREADPOOL]", $"Thread {t.ManagedThreadId} left the thread pool.");
        }
        else
        {
            t = StartThread(executionTarget);
        }

        CLog.Debug("[THREADPOOL]", $"Thread {t.ManagedThreadId} is now executing task.");
        activeThreads.Add(t);
        mutex.ReleaseMutex();
    }

    public void ShowStats()
    {
        mutex.WaitOne();
        Console.WriteLine("============ ThreadPool Status =============");
        Console.WriteLine($"Active Threads: {activeThreads.Count}");
        Console.WriteLine($"Suspended Threads: {freeThreads.Count}");
        Console.WriteLine($"Requested-To-Freed Ratio: {(float)(_threadsRequestedSinceLastCheck + 1) / (_threadsExitedSinceLastCheck + 1) * 100.0f:F3}% ({_threadsRequestedSinceLastCheck}/{_threadsExitedSinceLastCheck})");
        Console.WriteLine($"Eaten Count: {_threadsEaten} (negative is bad!)");
        Console.WriteLine("============================================");
        mutex.ReleaseMutex();
    }

    public void IntegrityCheck(byte threadCount)
    {
        mutex.WaitOne();
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
            uint new_threads = (uint)(5 - gobbled);
            for (uint i = 0; i < new_threads; ++i)
                StartThread(null);

            CLog.Debug("[THREADPOOL]", $"IntegrityCheck: (gobbled <= 5) Spawning {new_threads} threads.");
        }
        else if (gobbled > threadCount)
        {
            uint kill_count = (uint)(gobbled - threadCount);
            KillFreeThreads(kill_count);
            _threadsEaten -= (int)kill_count;
            CLog.Debug("[THREADPOOL]", $"IntegrityCheck: (gobbled > 5) Killing {kill_count} threads.");
        }
        else
        {
            CLog.Success("[THREADPOOL]", "IntegrityCheck: Perfect!");
        }

        _threadsExitedSinceLastCheck = 0;
        _threadsRequestedSinceLastCheck = 0;

        mutex.ReleaseMutex();
    }

    public void KillFreeThreads(uint count)
    {
        CLog.Debug("[THREADPOOL]", $"Killing {count} excess threads.");
        mutex.WaitOne();
        CustomThread t;
        var enumerator = freeThreads.GetEnumerator();
        for (uint i = 0; i < count && enumerator.MoveNext(); ++i)
        {
            t = enumerator.Current;
            t.ExecutionTarget = null;
            t.DeleteAfterExit = true;
            ++threadsToExit;
            CustomThread.Resume();
        }
        mutex.ReleaseMutex();
    }

    public void Shutdown()
    {
        mutex.WaitOne();
        int tcount = activeThreads.Count + freeThreads.Count;
        CLog.Debug("[THREADPOOL]", $"Shutting down {tcount} threads.");
        KillFreeThreads((uint)freeThreads.Count);
        threadsToExit += (int)(uint)activeThreads.Count;

        foreach (var t in activeThreads)
        {
            t.ExecutionTarget?.OnShutdown();
        }
        mutex.ReleaseMutex();

        while (true)
        {
            mutex.WaitOne();
            if (activeThreads.Count > 0 || freeThreads.Count > 0)
            {
                CLog.Debug("[THREADPOOL]", $"{activeThreads.Count + freeThreads.Count} threads remaining...");

                if (activeThreads.Count > 0)
                    activeThreads.Clear();

                mutex.ReleaseMutex();
                Thread.Sleep(1000);
                continue;
            }

            activeThreads.Clear();
            freeThreads.Clear();
            mutex.ReleaseMutex();

            break;
        }
    }
    public static CustomThread StartThread(ThreadBase executionTarget)
    {       
        if (executionTarget == null)
        {
            CLog.Debug("[THREADPOOL]","Attempt to start a thread with no execution target.");
            return null;
        }

        CLog.Debug("[THREADPOOL]", "Starting a new custom thread.");
        
        var cts = new CancellationTokenSource();
        CustomThread t = new(() => RunThread(executionTarget, cts.Token), cts.Token);
        t.Start();
        return t;
    }


    private static bool RunThread(ThreadBase target, CancellationToken token)
    {      
        if (target == null)
        {
            CLog.Debug("[THREADPOOL]", "Thread has no execution target.");
            return false;
        }

        bool res = false;
        try
        {
            res = target.Run(token);
        }
        catch (Exception ex)
        {
            CLog.Error("[THREADPOOL]", $"Thread crashed: {ex.Message}");
        }
        return res;
    }

    public static void Startup(byte threadCount)
    {
        for (int i = 0; i < threadCount; ++i)
            StartThread(null);

        CLog.Success("[THREADPOOL]", $"Startup, launched {threadCount} threads.");
    }
}

public class CustomThread(Func<bool> value, CancellationToken token)
{
    private static readonly object threadIdLock = new();
    private static int threadid_count;

    public ThreadBase ExecutionTarget { get; set; }
    public bool DeleteAfterExit { get; set; }
    public Thread ControlInterface { get; private set; } = new Thread(() => RunThread(value));
    public Mutex SetupMutex { get; private set; } = new Mutex();
    public int ManagedThreadId { get; private set; } = GenerateThreadId();
    public readonly CancellationToken _token = token;

    public void Start()
    {
        ControlInterface.Start();
    }

    public static void RequestCancellation()
    {
        // Signal the thread to cancel
        // The thread should periodically check the token and exit if cancellation is requested
    }

    public static void Resume()
    {
        // Resume the thread if it was suspended
    }

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

    internal static void Yield()
    {
        Thread.Yield();
    }
}