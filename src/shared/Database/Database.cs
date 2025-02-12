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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WaadShared.Database;

public abstract class SQLCallbackBase
{
    public abstract void Run(List<AsyncQueryResult> queries);
    public abstract void Dispose();
}

public abstract class Database : CThread
{
    protected const int MAXBUFFER = 10000;
    protected const int MAXQUERY = 16384;

    protected int _counter;
    protected int mPort;
    protected DatabaseConnection[] Connections;
    protected QueryThread qt;
    protected int mConnectionCount;
    protected bool ThreadRunning;
    protected Queue<string> queries_queue = new();
    protected Queue<QueryBuffer> query_buffer = new();
    private readonly DatabaseConnection con;
    private readonly object _lock = new();
    private readonly CThreadState ThreadState;
    protected abstract void SetThreadName(string v);

    public Database() : base()
    {
        _counter = 0;
        mPort = 0;
        Connections = null;
        qt = null;
        mConnectionCount = -1;   // Not connected.
        ThreadRunning = true;
    }

    ~Database()
    {
        Dispose(false);
    }

    protected void Initialize()
    {
        // Spawn Database thread
        ThreadPool.QueueUserWorkItem(state => Run());

        // launch the query thread
        qt = new QueryThread(this);
        ThreadPool.QueueUserWorkItem(state => qt.Run());
    }

    public DatabaseConnection GetFreeConnection()
    {
        uint i = 0;
        while (true)
        {
            DatabaseConnection con = Connections[(i++) % mConnectionCount];
            if (con.Busy.Wait(0))
                return con;
        }
    }

    public QueryResult Query(string QueryString, params object[] args)
    {
        string sql = string.Format(QueryString, args);

        // Send the query
        QueryResult qResult = null;
        DatabaseConnection con = GetFreeConnection();

        if (SendQuery(con, sql, false))
            qResult = StoreQueryResult(con);

        con.Busy.Release();
        return qResult;
    }

    public QueryResult QueryNA(string QueryString)
    {
        // Send the query
        QueryResult qResult = null;
        DatabaseConnection con = GetFreeConnection();

        if (SendQuery(con, QueryString, false))
            qResult = StoreQueryResult(con);

        con.Busy.Release();
        return qResult;
    }

    public QueryResult FQuery(string QueryString, DatabaseConnection con)
    {
        // Send the query
        QueryResult qResult = null;
        if (SendQuery(con, QueryString, false))
            qResult = StoreQueryResult(con);

        return qResult;
    }

    public void FWaitExecute(string QueryString, DatabaseConnection con)
    {
        // Send the query
        SendQuery(con, QueryString, false);
    }

    public bool Execute(string QueryString, params object[] args)
    {
        string query = string.Format(QueryString, args);

        if (!ThreadRunning)
            return WaitExecuteNA(query);

        lock (_lock)
        {
            queries_queue.Enqueue(query);
        }
        return true;
    }

    public bool ExecuteNA(string QueryString)
    {
        if (!ThreadRunning)
            return WaitExecuteNA(QueryString);

        lock (_lock)
        {
            queries_queue.Enqueue(QueryString);
        }
        return true;
    }

    public bool WaitExecute(string QueryString, params object[] args)
    {
        string sql = string.Format(QueryString, args);

        DatabaseConnection con = GetFreeConnection();
        bool Result = SendQuery(con, sql, false);
        con.Busy.Release();
        return Result;
    }

    public bool WaitExecuteNA(string QueryString)
    {
        DatabaseConnection con = GetFreeConnection();
        bool Result = SendQuery(con, QueryString, false);
        con.Busy.Release();
        return Result;
    }

    public new void ThreadProcQuery()
    {
        SetThreadName("Database Execute Thread");
        SetThreadState(CThreadState.THREADSTATE_BUSY);
        ThreadRunning = true;

        ProcessQueries();

        ThreadRunning = false;
    }

     private new void SetThreadState(CThreadState THREADSTATE_BUSY)
    {
        string query = queries_queue.Dequeue();
        DatabaseConnection con = GetFreeConnection();

        while (query != null)
        {
            SendQuery(con, query, false);
            query = null;  // Release the query string

            if (ThreadState == CThreadState.THREADSTATE_TERMINATE)
                break;

            query = queries_queue.Dequeue();
        }

        con.Busy.Release();

        if (queries_queue.Count > 0)
        {
            // Execute all the remaining queries
            while (queries_queue.TryDequeue(out query))
            {
                con = GetFreeConnection();
                SendQuery(con, query, false);
                con.Busy.Release();
                // Release the query string if necessary
                query = null;
            }
        }
    }

    private void ProcessQueries()
    {
        DatabaseConnection con = GetFreeConnection();

        while (true)
        {
            string query = null;
            lock (_lock)
            {
                if (queries_queue.Count > 0)
                {
                    query = queries_queue.Dequeue();
                }
            }

            if (query == null)
                break;

            SendQuery(con, query, false);

            if (ThreadState == CThreadState.THREADSTATE_TERMINATE)
                break;
        }

        con.Busy.Release();
    }

    protected abstract bool SendQuery(DatabaseConnection con, string sql, bool self);
    protected abstract QueryResult StoreQueryResult(DatabaseConnection con);

    public void PerformQueryBuffer(QueryBuffer b, DatabaseConnection ccon)
    {
        if (b.Queries.Count == 0)
            return;

        DatabaseConnection con = ccon ?? GetFreeConnection();

        BeginTransaction(con);

        foreach (var query in b.Queries)
        {
            SendQuery(con, query, false);
        }

        EndTransaction(con);

        if (ccon == null)
            con.Busy.Release();
    }

    public void QueueAsyncQuery(AsyncQuery query)
    {
        query.Db = this;
        query.Perform();
    }

    public void AddQueryBuffer(QueryBuffer b)
    {
        if (qt != null)
            lock (_lock)
            {
                query_buffer.Enqueue(b);
            }
        else
        {
            PerformQueryBuffer(b, null);
            b.Dispose();
        }
    }

    protected abstract void BeginTransaction(DatabaseConnection con);
    protected abstract void EndTransaction(DatabaseConnection con);

    public abstract string EscapeString(string escape);
    public abstract void EscapeLongString(string str, uint len, StringBuilder outStr);
    public abstract string EscapeString(string esc, DatabaseConnection con);
    public abstract bool SupportsReplaceInto();
    public abstract bool SupportsTableLocking();

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            if (Connections != null)
            {
                foreach (var connection in Connections)
                {
                    connection?.Dispose();
                }
                Connections = null;
            }
        }

        // Dispose unmanaged resources
    }
}

public class QueryBuffer : IDisposable
{
    internal static int Size;

    public List<string> Queries { get; } = [];

    public void AddQuery(string format, params object[] args)
    {
        string query = string.Format(format, args);
        Queries.Add(query);
    }

    public void AddQueryNA(string str)
    {
        Queries.Add(str);
    }

    public void AddQueryStr(string str)
    {
        Queries.Add(str);
    }

    public void Dispose()
    {
        Queries.Clear();
        GC.SuppressFinalize(this);
    }
}

public class AsyncQuery(SQLCallbackBase f) : IDisposable
{
    public Database Db { get; set; }
    public List<AsyncQueryResult> Queries { get; } = [];
    public SQLCallbackBase Func { get; set; } = f;

    public void AddQuery(string format, params object[] args)
    {
        AsyncQueryResult res = new();
        string query = string.Format(format, args);
        res.Query = query;
        res.Result = null;
        Queries.Add(res);
    }

    public void Perform()
    {
        DatabaseConnection conn = Db.GetFreeConnection();
        foreach (var query in Queries)
        {
            query.Result = Db.FQuery(query.Query, conn);
        }

        conn.Busy.Release();
        Func.Run(Queries);

        Dispose();
    }

    public void Dispose()
    {
        Queries.Clear();
        GC.SuppressFinalize(this);
    }
}


public class AsyncQueryResult
{
    public string Query { get; set; }
    public QueryResult Result { get; set; }
}

public class QueryThread(Database database) : CThread
{
    private new readonly Database db = database;

    public void Terminate() => db.SetThreadState(CThreadState.THREADSTATE_TERMINATE);
}

public static class ThreadPool
{
    public static void QueueUserWorkItem(WaitCallback callback)
    {
        Task.Run(() => callback(null));
    }
}

public static class Thread
{
    public static void Sleep(int millisecondsTimeout) => Task.Delay(millisecondsTimeout).Wait();
}
public static class CThreadExtensions
{
    public static CThreadState ThreadState { get; private set; }
    public static void SetThreadName(this CThread thread, string name, bool qt)
    {
        SetThreadState(CThreadState.THREADSTATE_TERMINATE);
        Queue<QueryBuffer> queryBuffer = new();        
        Queue<string> queriesQueue = new();
        
        bool ThreadRunning = false;
        while (ThreadRunning || qt)
        {
            if (queryBuffer.Count == 0)
                Monitor.PulseAll(queryBuffer);

            if (queriesQueue.Count == 0)
                Monitor.PulseAll(queriesQueue);

            Thread.Sleep(100);
            if (!ThreadRunning)
                break;

            Thread.Sleep(1000);
        }
    }

    private static void SetThreadState(CThreadState THREADSTATE_TERMINATE) => ThreadState = THREADSTATE_TERMINATE;
}
