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
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WaadShared;
using WaadShared.Auth;
using WaadShared.Network;
using static WaadShared.ConsoleListener;


namespace WaadRealmServer;

public enum ConsoleState
{
    User = 1,
    Password = 2,
    Logged = 3,
    Waiting = 4
}

public class ConsoleSession
{
    internal Socket socket;
    private readonly RemoteConsole console;
    private readonly byte[] buffer;
    private int bufferPos;
    private ConsoleState state;
    private string username;
    private string password;
    private int requestNo;

    public ConsoleSession() {
        buffer = new byte[2048];
        bufferPos = 0;
        console = new RemoteConsole(this);
        state = ConsoleState.User;
        requestNo = 0;
    }

    public ConsoleSession(Socket socket)
    {
        this.socket = socket;
        buffer = new byte[2048];
        bufferPos = 0;
        console = new RemoteConsole(this);
        state = ConsoleState.User;
        requestNo = 0;
    }

    public void OnRead(byte[] data, int length)
    {
        Array.Copy(data, 0, buffer, bufferPos, length);
        bufferPos += length;
        int start = 0;
        for (int i = 0; i < bufferPos; i++)
        {
            if (buffer[i] == '\n')
            {
                int lineLen = i - start;
                if (lineLen > 0 && buffer[i - 1] == '\r') lineLen--;
                string line = Encoding.UTF8.GetString(buffer, start, lineLen);
                HandleLine(line);
                start = i + 1;
            }
        }
        if (start > 0)
        {
            Array.Copy(buffer, start, buffer, 0, bufferPos - start);
            bufferPos -= start;
        }
    }

    private void HandleLine(string line)
    {
        switch (state)
        {
            case ConsoleState.User:
                console.Write(R_N_CONLIS_L_2);
                username = line;
                console.Write(R_N_CONLIS_P);
                state = ConsoleState.Password;
                break;
            case ConsoleState.Password:
                password = line;
                console.Write(R_N_CONLIS_P_1);
                state = ConsoleState.Waiting;
                requestNo = ConsoleAuthManager.Instance.GenerateRequestId();
                ConsoleAuthManager.Instance.SetRequest(requestNo, this);
                ConsoleListener.TestConsoleLogin(username, password, requestNo);
                break;
            case ConsoleState.Logged:
                if (line.StartsWith(R_N_CONLIS_P_2, StringComparison.OrdinalIgnoreCase))
                {
                    Disconnect();
                    break;
                }
                ConsoleListener.HandleConsoleInput(console, line);
                break;
        }
    }

    public void OnConnect()
    {
        console.Write(R_N_CONLIS_L);
        console.Write(R_N_CONLIS_L_1);
    }

    public void OnDisconnect()
    {
        if (requestNo != 0)
        {
            ConsoleAuthManager.Instance.SetRequest(requestNo, null);
            requestNo = 0;
        }
    }

    public void Disconnect()
    {
        socket?.Disconnect();
        OnDisconnect();
    }

    public void AuthCallback(bool result)
    {
        ConsoleAuthManager.Instance.SetRequest(requestNo, null);
        requestNo = 0;
        if (!result)
        {
            console.Write(R_N_CONLIS_A);
            Disconnect();
        }
        else
        {
            console.Write(R_N_CONLIS_A_1, username);
            _ = ConsoleCommands.HandleInfoCommand(console, 1, []);
            console.Write(R_N_CONLIS_A_2);
            state = ConsoleState.Logged;
        }
    }
}

public class RemoteConsole(ConsoleSession session) : IConsole
{
    private readonly ConsoleSession session = session;

    public void Write(string format, params object[] args)
    {
        string msg = string.Format(format, args);
        byte[] data = Encoding.UTF8.GetBytes(msg + "\r\n");
        _ = (session?.socket?.Send(data));
    }
}

public class ConsoleAuthManager
{
    private static readonly ConsoleAuthManager instance = new();
    private int highRequestId = 1;
    private readonly Dictionary<int, ConsoleSession> requestMap = new();
    private readonly object lockObj = new();
    public static ConsoleAuthManager Instance => instance;
    public int GenerateRequestId()
    {
        lock (lockObj) { return highRequestId++; }
    }
    public void SetRequest(int id, ConsoleSession session)
    {
        lock (lockObj)
        {
            if (session == null) _ = requestMap.Remove(id);
            else requestMap[id] = session;
        }
    }
    public ConsoleSession GetRequest(int id)
    {
        lock (lockObj)
        {
            _ = requestMap.TryGetValue(id, out var session);
            return session;
        }
    }
}

public static class ConsoleListener
{
    private static ListenSocket<ConsoleSession> listener;
    private static readonly List<ConsoleSession> sessions = [];
    public static void StartFromConfig()
    {
        var configPath = System.IO.Path.Combine(AppContext.BaseDirectory, "waad-world.ini");
        var configMgr = new WaadShared.Config.ConfigMgr();
        if (!configMgr.MainConfig.SetSource(configPath))
        {
            CLog.Notice("[ConsoleListener]", $"Config file not found: {configPath}");
            return;
        }

        string enabledStr = configMgr.MainConfig.GetString("RemoteConsole", "Enabled", "0");
        string address = configMgr.MainConfig.GetString("RemoteConsole", "Host", "0.0.0.0");
        string portStr = configMgr.MainConfig.GetString("RemoteConsole", "Port", "8092");

        bool enabled = enabledStr == "1" || enabledStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            CLog.Notice("[ConsoleListener]", "Remote console is disabled in config.");
            return;
        }

        _ = uint.TryParse(portStr, out uint port);
        CLog.Notice("[ConsoleListener]", $"Remote console enabled on {address}:{port}");
        
        listener = new ListenSocket<ConsoleSession>(address, port, sock => {
            var waadSocket = new Socket(sock, 2048, 2048);
            return new ConsoleSession(waadSocket);
        });
        _ = ThreadPool.QueueUserWorkItem(_ => RunListener());
    }
    private static void RunListener()
    {
        while (listener.IsOpen())
        {
            // Accept les connexions et crée les sessions
            // Ici, on suppose que ListenSocket<T> crée et gère les instances ConsoleSession
            // Si besoin, ajoute un callback ou une gestion d'événement pour chaque nouvelle session
            Thread.Sleep(100); // Polling simple, à remplacer par un vrai event/callback si dispo
        }
    }

    // Wrapper statique pour l'appel du handler d'auth console depuis LogonCommClientSocket
    public static void ConsoleAuthCallback(uint requestId, uint result)
    {
        var session = ConsoleAuthManager.Instance.GetRequest((int)requestId);
        session?.AuthCallback(result != 0);
    }

    public static void TestConsoleLogin(string username, string password, int requestId)
    {
        var session = ConsoleAuthManager.Instance.GetRequest(requestId);
        bool result = false;
        if (session != null)
        {
            var db = RealmDatabaseManager.GetDatabase();
            if (db != null)
            {
                string safeLogin = db.EscapeString(username);
                var acctResult = db.Query("SELECT acct FROM accounts WHERE login = '{0}'", safeLogin);
                if (acctResult != null && acctResult.NextRow())
                {
                    int acctId = Convert.ToInt32(acctResult.GetValue(0));
                    var srpData = db.Query("SELECT salt, verifier FROM account_data WHERE acct = {0}", acctId);
                    if (srpData != null && srpData.NextRow())
                    {
                        var salt = srpData.GetValue(0)?.ToString();
                        var verifier = srpData.GetValue(1)?.ToString();
                        if (!string.IsNullOrEmpty(salt) && !string.IsNullOrEmpty(verifier))
                        {
                            var srp = new WowSRP6();
                            var saltBn = new BigNumber(salt);
                            var computedVerifier = srp.ComputeVerifier(safeLogin, password, saltBn);
                            if (string.Equals(computedVerifier.ToString(), verifier, StringComparison.OrdinalIgnoreCase))
                            {
                                result = true;
                            }
                        }
                    }
                }
            }
            session.AuthCallback(result);
        }
    }

    public static void HandleConsoleInput(IConsole console, string input)
    {
        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return;
        var commandMap = new Dictionary<string, Func<IConsole, int, string[], bool>>
        {
            { "info", ConsoleCommands.HandleInfoCommand },
            { "ban", ConsoleCommands.HandleBanAccountCommand },
            { "unban", ConsoleCommands.HandleUnbanAccountCommand },
            { "createaccount", ConsoleCommands.HandleCreateAccountCommand },
            { "resetpassword", ConsoleCommands.HandleResetPasswordCommand },
            { "backupdb", ConsoleCommands.HandleBackupDBCommand },
            { "cancel", ConsoleCommands.HandleCancelCommand },
            { "rehash", ConsoleCommands.HandleRehashCommand },
            { "kick", ConsoleCommands.HandleKickCommand },
            { "motd", ConsoleCommands.HandleMOTDCommand },
            { "online", ConsoleCommands.HandleOnlinePlayersCommand },
            { "gms", ConsoleCommands.HandleGMsCommand },
            { "playerinfo", ConsoleCommands.HandlePlayerInfoCommand },
            { "shutdown", ConsoleCommands.HandleShutDownCommand },
            { "whisper", ConsoleCommands.HandleWhisperCommand },
            { "announce", ConsoleCommands.HandleAnnounceCommand },
            { "wannounce", ConsoleCommands.HandleWAnnounceCommand },
            // { "help", ConsoleCommands.HandleHelpCommand }
        };
        // Ajout de la gestion help et ?
        if (tokens[0].Equals("help", StringComparison.OrdinalIgnoreCase) || tokens[0] == "?")
        {
            console.Write("=========================================================================================================");
            console.Write("| {0,15} | {1,30} | {2,50} |", "Name", "Arguments", "Description");
            console.Write("=========================================================================================================");
            // Pour la description, il faudrait un mapping, ici on met un placeholder
            foreach (var cmd in commandMap.Keys)
            {
                console.Write("| {0,15} | {1,30} | {2,50} |", cmd, "args", "description");
            }
            return;
        }
        if (commandMap.TryGetValue(tokens[0].ToLowerInvariant(), out var handler))
        {
            if (!handler(console, tokens.Length, tokens))
            {
                console.Write($"Usage: {tokens[0]}");
                return;
            }
            return;
        }
        console.Write(R_N_CONLIS_I_19, tokens[0]);
    }
}
