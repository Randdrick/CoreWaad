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
using System.Threading;
using System.Threading.Tasks;
using WaadShared;
using static WaadShared.ThreadingLogs;

namespace LogonServer;

public class CallbackBase
{
    public virtual void Execute() { }
}

public class CallbackP0<T>(T callback, Action method) : CallbackBase, IDisposable
{
    private readonly T _callback = callback;
    private readonly Action _method = method;
    private bool _disposed = false;

    public override void Execute() => _method();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _callback is IDisposable disposableCallback)
            {
                disposableCallback.Dispose();
            }
            _disposed = true;
        }
    }
}

public abstract class ThreadBase
{
    protected bool mrunning = true;

    // Seule méthode abstraite nécessaire (Run() supprimée)
    public abstract bool Run(CancellationToken token);
}

public class PeriodicFunctionCaller<Type> : WaadShared.Threading.ThreadBase, IDisposable
{
    private readonly CallbackP0<Type> _cb;
    private readonly uint _interval;
    private bool _running;
    private readonly CancellationTokenSource _cts;
    private readonly Task _task;
    private bool _disposed = false;
    private readonly string _callbackName;

    public PeriodicFunctionCaller(Type callback, Action method, uint interval, string callbackName = null)
    {
        _cb = new CallbackP0<Type>(callback, method);
        _interval = interval;
        _callbackName = callbackName ?? typeof(Type).Name;
        _running = true;
        _cts = new CancellationTokenSource();
        _task = Task.Run(() => RunAsync(_cts.Token));
        CLog.Debug(R_D_THREAD_START, _callbackName, _interval);
    }

    private async Task RunAsync(CancellationToken token)
    {
        // Utilise uniquement _running pour contrôler l'exécution locale
        while (!token.IsCancellationRequested && _running)
        {
            await Task.Delay((int)_interval, token);
            if (token.IsCancellationRequested || !_running) break;

            try
            {
                _cb.Execute();
            }
            catch (Exception ex)
            {
                CLog.Error(R_E_THREAD_EXCEPTION, _callbackName, ex);
            }
        }
    }

    // Implémentation requise par ThreadBase
    public override bool Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _running)
        {
            try
            {
                token.WaitHandle.WaitOne((int)_interval);
                if (token.IsCancellationRequested || !_running) break;
                _cb.Execute();
            }
            catch (Exception ex)
            {
                CLog.Error(R_E_THREAD_EXCEPTION, _callbackName, ex);
            }
        }
        return true;
    }

    public void Kill()
    {
        _cts.Cancel();
        _running = false;

        try
        {
            if (_task != null && !_task.Wait(5000))
            {
                CLog.Warning(R_W_THREAD_TIMEOUT, _callbackName);
            }
        }
        catch (AggregateException) { }
        CLog.Debug(R_D_THREAD_STOP, _callbackName);
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
                Kill();
                _cb?.Dispose();
                _cts?.Dispose();
            }
            _disposed = true;
        }
    }
}