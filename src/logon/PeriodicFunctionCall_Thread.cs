/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2021 WAAD Team <https://arbonne.games-rpg.net/>
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
using System.Threading;

namespace LogonServer;

public class CallbackBase
{
    public virtual void Execute() { }
}

public class CallbackP0<T>(T callback, Action method) : CallbackBase
{
    private readonly T _callback = callback;
    private readonly Action _method = method;

    public override void Execute()
    {
        _method();
    }
}

public abstract class ThreadBase
{
    protected bool mrunning = true;
    public abstract bool Run();
    public void Kill() { mrunning = false; }
}

public class PeriodicFunctionCaller<Type> : WaadShared.Threading.ThreadBase
{
    private readonly CallbackP0<Type> _cb;
    private readonly uint _interval;
    private bool _running;
    private readonly Thread _thread;
    private readonly AutoResetEvent _event;

    public PeriodicFunctionCaller(Type callback, Action method, uint interval)
    {
        _cb = new CallbackP0<Type>(callback, method);
        _interval = interval;
        _running = true;
        _event = new AutoResetEvent(false);
        _thread = new Thread(RunThread);
        _thread.Start();
    }

    ~PeriodicFunctionCaller()
    {
        Kill();
        _thread.Join();
    }

    private void RunThread()
    {
        while (_running && mrunning)
        {
            _event.WaitOne((int)_interval);

            if (!_running)
                break;

            _cb.Execute();
        }
    }

    public override bool Run()
    {
        return false;
    }

    public void Kill()
    {
        _running = false;
        _event.Set();
        _thread.Join();
    }
}

