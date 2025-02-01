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

public abstract class SQLCallbackBase
{
    ~SQLCallbackBase()
    {
    }
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
    protected Queue<string> queries_queue = new Queue<string>();
    protected Queue<QueryBuffer> query_buffer = new Queue<QueryBuffer>();
    private readonly object _lock = new object();

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

    protected void _Initialize()
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

        // shouldn't be reached
        return null;
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

    public bool Run()
    {
        SetThreadName("Database Execute Thread");
        SetThreadState(CThreadState.THREADSTATE_BUSY);
        ThreadRunning = true;

        ProcessQueries();

        ThreadRunning = false;
        return false;
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

    public void FreeQueryResult(QueryResult p)
    {
        p.Dispose();
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class QueryBuffer : IDisposable
{
    public List<string> Queries { get; } = new List<string>();

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
    }
}

public class AsyncQuery : IDisposable
{
    public Database Db { get; set; }
    public List<AsyncQueryResult> Queries { get; } = new List<AsyncQueryResult>();
    public SQLCallbackBase Func { get; set; }

    public AsyncQuery(SQLCallbackBase f)
    {
        Func = f;
    }

    public void AddQuery(string format, params object[] args)
    {
        AsyncQueryResult res = new AsyncQueryResult();
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
        Func.Dispose();
        foreach (var query in Queries)
        {
            query.Result?.Dispose();
        }
        Queries.Clear();
    }
}

public class AsyncQueryResult
{
    public string Query { get; set; }
    public QueryResult Result { get; set; }
}

public class QueryThread : CThread
{
    private Database db;

    public QueryThread(Database database)
    {
        db = database;
    }

    public override void Run()
    {
        db.ThreadProcQuery();
    }

    ~QueryThread()
    {
        db.qt = null;
    }
}

public abstract class CThread
{
    public virtual void Run()
    {
        // Implémentez la logique de thread ici
    }
}

public class DatabaseConnection : IDisposable
{
    public SemaphoreSlim Busy { get; } = new SemaphoreSlim(1, 1);

    public void Dispose()
    {
        Busy.Dispose();
    }
}

public abstract class QueryResult : IDisposable
{
    protected uint mFieldCount;
    protected uint mRowCount;
    protected Field[] mCurrentRow;

    public QueryResult(uint fields, uint rows)
    {
        mFieldCount = fields;
        mRowCount = rows;
        mCurrentRow = null;
    }

    public abstract bool NextRow();
    public void Delete() { Dispose(); }

    public Field[] Fetch() { return mCurrentRow; }
    public uint GetFieldCount() { return mFieldCount; }
    public uint GetRowCount() { return mRowCount; }

    public abstract void Dispose();
}

public static class ThreadPool
{
    public static void QueueUserWorkItem(WaitCallback callback)
    {
        Task.Run(() => callback(null));
    }
}

public static class Log
{
    public static void Notice(string source, string message)
    {
        Console.WriteLine($"[{source}] {message}");
    }

    public static void Error(string source, string message)
    {
        Console.WriteLine($"[{source}] ERROR: {message}");
    }
}

public enum CThreadState
{
    THREADSTATE_BUSY,
    THREADSTATE_TERMINATE
}

public static class CThreadExtensions
{
    public static void SetThreadName(this CThread thread, string name)
    {
        // Implémentez la logique pour définir le nom du thread ici
    }

    public static void SetThreadState(this CThread thread, CThreadState state)
    {
        // Implémentez la logique pour définir l'état du thread ici
    }
}

public class Field
{
    private object value;

    public void SetValue(object val)
    {
        value = val;
    }

    public object GetValue()
    {
        return value;
    }
}