#nullable enable

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
using System.Collections.Concurrent;
using System.Threading;

namespace WaadRealmServer;

public class Task(Action cb)
{
    private readonly Action _cb = cb;
    public bool InProgress { get; set; } = false;
    public bool Completed { get; set; } = false;

    public void Execute()
    {
        _cb();
    }
}

public class TaskExecutor(TaskList starter)
{
    private readonly TaskList _starter = starter;

    public bool Run()
    {
        Task? t;
        while (_starter.Running)
        {
            t = _starter.GetTask();
            if (t != null)
            {
                t.Execute();
                t.Completed = true;
                _starter.RemoveTask(t);
            }
            else
            {
                Thread.Sleep(20);
            }
        }
        return true;
    }
}

public class TaskList
{
    private readonly ConcurrentBag<Task> _tasks = [];
    private readonly object _queueLock = new();
    private int _threadCount;
    public bool Running { get; private set; }

    public void AddTask(Task task)
    {
        lock (_queueLock)
        {
            _tasks.Add(task);
        }
    }

    public Task? GetTask()
    {
        lock (_queueLock)
        {
            foreach (var task in _tasks)
            {
                if (!task.InProgress)
                {
                    task.InProgress = true;
                    return task;
                }
            }
            return null;
        }
    }

    public void RemoveTask(Task task)
    {
        lock (_queueLock)
        {
            _tasks.TryTake(out _);
        }
    }
    public void Start(uint threadCount)
    {
        Running = true;
        _threadCount = (int)threadCount;
        for (int i = 0; i < threadCount; i++)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var executor = new TaskExecutor(this);
                executor.Run();
                Interlocked.Decrement(ref _threadCount);
            });
        }
    }

    public void Wait()
    {
        bool hasTasks;
        do
        {
            // Vérifier si un arrêt est demandé (CTRL+C)
            if (Master.StopEvent)
                break;

            lock (_queueLock)
            {
                hasTasks = false;
                foreach (var task in _tasks)
                {
                    if (!task.Completed)
                    {
                        hasTasks = true;
                        break;
                    }
                }
            }
            Thread.Sleep(20);
        } while (hasTasks);
    }

    public void Kill()
    {
        Running = false;
    }

    public void WaitForThreadsToExit()
    {
        while (_threadCount > 0)
        {
            Thread.Sleep(20);
        }
    }
}
