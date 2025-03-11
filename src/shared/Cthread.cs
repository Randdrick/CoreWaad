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

namespace WaadShared;

public class MapMgr { }
public class Object { }
public class Player { }
public class WorldSession { }
public class Creature { }
public class GameObject { }

public enum CThreadState
{
    THREADSTATE_TERMINATE,
    THREADSTATE_PAUSED,
    THREADSTATE_SLEEPING,
    THREADSTATE_BUSY,
    THREADSTATE_AWAITING,
    THREADSTATE_IDLE
}

public abstract class ThreadBase
{
    public abstract bool Run();
    public abstract void OnShutdown();
    public static void SetThreadName(string format, params object[] args)
    {
        string threadName = string.Format(format, args);
        // Implement your logic here to set the thread name
        Console.WriteLine(threadName); // Example implementation
    }
}

public class CThread : ThreadBase
{
    public CThread() { }

    public void ThreadProcQuery()
    {
        SetThreadName("Database Execute Thread");
        SetThreadState(CThreadState.THREADSTATE_BUSY);
        ThreadRunning = true;

        while (ThreadRunning)
        {
            ProcessQueries();

            if (ThreadState == CThreadState.THREADSTATE_TERMINATE)
            {
                ThreadRunning = false;
            }
        }
    }

    private CThreadState ThreadState { get; set; }
    private DateTime StartTime { get; set; }
    private int ThreadId { get; set; }
    private bool ThreadRunning { get; set; }

    private void SetThreadState(CThreadState threadState) => ThreadState = threadState;

    public CThreadState GetThreadState() => ThreadState;

    public int GetThreadId() => ThreadId;

    public DateTime GetStartTime() => StartTime;

    protected ThreadBase db;

    public override void OnShutdown() => SetThreadState(CThreadState.THREADSTATE_TERMINATE);

    public override bool Run()
    {
        ThreadProcQuery();
        return true;
    }

    private void ProcessQueries()
    {
        // This method should be implemented in the Database class
        // Here we assume that the Database class has a reference to this CThread instance
        if (db is Database.Database database)
        {
            database.ProcessQueries();
        }
        else
        {
            throw new InvalidOperationException("ProcessQueries can only be called on a Database instance.");
        }
    }
}
