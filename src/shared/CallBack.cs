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

namespace WaadShared;

public abstract class CallbackBase
{
    public abstract void Execute();
}

public class CallbackFP(Action cb)
{
    private Action myCallback = cb;

    public void Invoke()
    {
        try
        {
            myCallback?.Invoke();
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Execute()
    {
        Invoke();
    }

    public void Set(Action cb)
    {
        myCallback = cb;
    }

    public CallbackFP Create()
    {
        return new CallbackFP(myCallback);
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class CallbackP0<T>(T classInstance, Action<T> method) : CallbackBase
{
    private readonly T _obj = classInstance;
    private readonly Action<T> _func = method;

    public override void Execute()
    {
        try
        {
            _func(_obj);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class CallbackP1<T, P1>(T classInstance, Action<T, P1> method, P1 p1) : CallbackBase
{
    private readonly T _obj = classInstance;
    private readonly Action<T, P1> _func = method;
    private readonly P1 _p1 = p1;

    public override void Execute()
    {
        try
        {
            _func(_obj, _p1);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class CallbackP2<T, P1, P2>(T classInstance, Action<T, P1, P2> method, P1 p1, P2 p2) : CallbackBase
{
    private readonly T _obj = classInstance;
    private readonly Action<T, P1, P2> _func = method;
    private readonly P1 _p1 = p1;
    private readonly P2 _p2 = p2;

    public override void Execute()
    {
        try
        {
            _func(_obj, _p1, _p2);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class CallbackP3<T, P1, P2, P3>(T classInstance, Action<T, P1, P2, P3> method, P1 p1, P2 p2, P3 p3) : CallbackBase
{
    private readonly T _obj = classInstance;
    private readonly Action<T, P1, P2, P3> _func = method;
    private readonly P1 _p1 = p1;
    private readonly P2 _p2 = p2;
    private readonly P3 _p3 = p3;

    public override void Execute()
    {
        try
        {
            _func(_obj, _p1, _p2, _p3);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class CallbackP4<T, P1, P2, P3, P4>(T classInstance, Action<T, P1, P2, P3, P4> method, P1 p1, P2 p2, P3 p3, P4 p4) : CallbackBase
{
    private readonly T _obj = classInstance;
    private readonly Action<T, P1, P2, P3, P4> _func = method;
    private readonly P1 _p1 = p1;
    private readonly P2 _p2 = p2;
    private readonly P3 _p3 = p3;
    private readonly P4 _p4 = p4;

    public override void Execute()
    {
        try
        {
            _func(_obj, _p1, _p2, _p3, _p4);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class CallbackP5<T, P1, P2, P3, P4, P5>(T classInstance, Action<T, P1, P2, P3, P4, P5> method, P1 p1, P2 p2, P3 p3, P4 p4, P5 p5) : CallbackBase
{
    private readonly T _obj = classInstance;
    private readonly Action<T, P1, P2, P3, P4, P5> _func = method;
    private readonly P1 _p1 = p1;
    private readonly P2 _p2 = p2;
    private readonly P3 _p3 = p3;
    private readonly P4 _p4 = p4;
    private readonly P5 _p5 = p5;

    public override void Execute()
    {
        try
        {
            _func(_obj, _p1, _p2, _p3, _p4, _p5);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class QueryResult { }

public struct AsyncQueryResult { }

public class SQLCallbackBase
{
    public virtual void Run(List<AsyncQueryResult> result) { }
}

public class SQLClassCallbackP0<T>(T instance, Action<T, List<AsyncQueryResult>> method) : SQLCallbackBase
{
    private readonly T _base = instance;
    private readonly Action<T, List<AsyncQueryResult>> _method = method;

    public override void Run(List<AsyncQueryResult> data)
    {
        try
        {
            _method(_base, data);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class SQLClassCallbackP1<T, P1>(T instance, Action<T, List<AsyncQueryResult>, P1> method, P1 p1) : SQLCallbackBase
{
    private readonly T _base = instance;
    private readonly Action<T, List<AsyncQueryResult>, P1> _method = method;
    private readonly P1 _par1 = p1;

    public override void Run(List<AsyncQueryResult> data)
    {
        try
        {
            _method(_base, data, _par1);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class SQLClassCallbackP2<T, P1, P2>(T instance, Action<T, List<AsyncQueryResult>, P1, P2> method, P1 p1, P2 p2) : SQLCallbackBase
{
    private readonly T _base = instance;
    private readonly Action<T, List<AsyncQueryResult>, P1, P2> _method = method;
    private readonly P1 _par1 = p1;
    private readonly P2 _par2 = p2;

    public override void Run(List<AsyncQueryResult> data)
    {
        try
        {
            _method(_base, data, _par1, _par2);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class SQLClassCallbackP3<T, P1, P2, P3>(T instance, Action<T, List<AsyncQueryResult>, P1, P2, P3> method, P1 p1, P2 p2, P3 p3) : SQLCallbackBase
{
    private readonly T _base = instance;
    private readonly Action<T, List<AsyncQueryResult>, P1, P2, P3> _method = method;
    private readonly P1 _par1 = p1;
    private readonly P2 _par2 = p2;
    private readonly P3 _par3 = p3;

    public override void Run(List<AsyncQueryResult> data)
    {
        try
        {
            _method(_base, data, _par1, _par2, _par3);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class SQLClassCallbackP4<T, P1, P2, P3, P4>(T instance, Action<T, List<AsyncQueryResult>, P1, P2, P3, P4> method, P1 p1, P2 p2, P3 p3, P4 p4) : SQLCallbackBase
{
    private readonly T _base = instance;
    private readonly Action<T, List<AsyncQueryResult>, P1, P2, P3, P4> _method = method;
    private readonly P1 _par1 = p1;
    private readonly P2 _par2 = p2;
    private readonly P3 _par3 = p3;
    private readonly P4 _par4 = p4;

    public override void Run(List<AsyncQueryResult> data)
    {
        try
        {
            _method(_base, data, _par1, _par2, _par3, _par4);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class SQLFunctionCallbackP0(Action<QueryResult> method) : SQLCallbackBase
{
    private readonly Action<QueryResult> _method = method;

    public override void Run(List<AsyncQueryResult> data)
    {
        try
        {
            // Convert List<AsyncQueryResult> to QueryResult if necessary
            QueryResult queryResult = new();
            _method(queryResult);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}

public class SQLFunctionCallbackP1<T1>(Action<QueryResult, T1> method, T1 par1) : SQLCallbackBase
{
    private readonly Action<QueryResult, T1> _method = method;
    private readonly T1 _p1 = par1;

    public override void Run(List<AsyncQueryResult> data)
    {
        try
        {
            // Convert List<AsyncQueryResult> to QueryResult if necessary
            QueryResult queryResult = new();
            _method(queryResult, _p1);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static void HandleException(Exception ex)
    {
        // Log or handle the exception as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}