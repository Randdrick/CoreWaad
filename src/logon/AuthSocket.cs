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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using WaadShared;
using WaadShared.Auth;

using static WaadShared.AuthSocket;

namespace LogonServer;

public enum AuthError
{
    CE_SUCCESS = 0x00,
    CE_IPBAN = 0x01,
    CE_ACCOUNT_CLOSED = 0x03,
    CE_NO_ACCOUNT = 0x04,
    CE_ACCOUNT_IN_USE = 0x06,
    CE_PREORDER_TIME_LIMIT = 0x07,
    CE_SERVER_FULL = 0x08,
    CE_WRONG_BUILD_NUMBER = 0x09,
    CE_UPDATE_CLIENT = 0x0a,
    CE_ACCOUNT_FREEZED = 0x0c
}

public class AuthSocket : Socket
{
    public readonly Socket socket;
    private Challenge challenge;
    private readonly BigNumber b;
    private readonly BigNumber N;
    private readonly BigNumber g;
    private Account account;
    public bool authenticated;
    public DateTime lastRecv;
    private readonly bool removedFromSet;
    private Patch patch;
    private PatchJob patchJob;
    private static readonly object authSocketLock = new();
    private static readonly HashSet<AuthSocket> authSockets = [];
    public PatchJob PatchJob { get; set; }

    public static void BurstBegin() { }
    public static void BurstEnd() { }
    public static bool BurstSend(byte[] data) { return true; }
    public static bool BurstSend(byte[] data, uint length) { return true; }
    public static void BurstPush() { }
    public static uint GetWriteBufferSize() { return 0; }
    private readonly PatchMgr PatchMgr;

    // Public parameterless constructor
    public AuthSocket() : base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    {
        PatchMgr = new PatchMgr();
        this.socket = this;
        challenge = new Challenge { I_len = 0 };

        b = new BigNumber();
        N = new BigNumber();
        N.SetHexStr("894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7");
        g = new BigNumber();
        g.SetDword(7);
        authenticated = false;
        account = null;
        lastRecv = DateTime.Now;
        removedFromSet = false;
        patch = null;
        patchJob = null;

        lock (authSocketLock)
        {
            authSockets.Add(this);
        }
    }

    ~AuthSocket()
    {
        if (patchJob != null)
        {
            PatchMgr.AbortPatchJob(patchJob);
            patchJob = null;
        }
    }

    public void OnDisconnect()
    {
        if (!removedFromSet)
        {
            lock (authSocketLock)
            {
                authSockets.Remove(this);
            }
        }

        if (patchJob != null)
        {
            PatchMgr.AbortPatchJob(patchJob);
            patchJob = null;
        }
    }

    public static void HandleChallenge(AuthSocket authSocket)
    {
        var sLog = new Logger();
        var IPBanner = new IPBanner();
        var PatchMgr = new PatchMgr();

        if (authSocket.socket.Available < 4)
            return;

        byte[] buffer = new byte[4];
        authSocket.socket.Receive(buffer);
        ushort fullSize = BitConverter.ToUInt16(buffer, 2);

        sLog.OutDetail(L_N_AUTHSOCK, fullSize);

        if (authSocket.socket.Available < fullSize + 4)
            return;

        buffer = new byte[fullSize + 4];
        authSocket.socket.Receive(buffer);

        if (fullSize > Marshal.SizeOf<Challenge>())
        {
            sLog.OutDebug(L_D_AUTHSOCK_C);
            authSocket.Disconnect();
            return;
        }

        sLog.OutDebug(L_D_AUTHSOCK_C_1);

        authSocket.challenge = Challenge.FromBytes(buffer);

        ushort build = authSocket.challenge.Build;
        if (build > LogonServer.MaxBuild)
        {
            authSocket.SendChallengeError(AuthError.CE_WRONG_BUILD_NUMBER);
            return;
        }

        if (build < LogonServer.MinBuild)
        {
            // can we patch?
            char[] flippedLoc = new char[5];
            flippedLoc[0] = (char)authSocket.challenge.Country[3];
            flippedLoc[1] = (char)authSocket.challenge.Country[2];
            flippedLoc[2] = (char)authSocket.challenge.Country[1];
            flippedLoc[3] = (char)authSocket.challenge.Country[0];

            authSocket.patch = PatchMgr.FindPatchForClient(build, new string(flippedLoc));
            if (authSocket.patch == null)
            {
                // could not find a valid patch
                authSocket.SendChallengeError(AuthError.CE_WRONG_BUILD_NUMBER);
                return;
            }

            sLog.OutDebug("[AuthChallenge]", L_D_AUTHSOCK_C_2, authSocket.patch.Version, authSocket.patch.Locality);

            authSocket.socket.Send([
                0x00, 0x00, 0x00, 0x72, 0x50, 0xa7, 0xc9, 0x27, 0x4a, 0xfa, 0xb8, 0x77, 0x80, 0x70, 0x22,
                0xda, 0xb8, 0x3b, 0x06, 0x50, 0x53, 0x4a, 0x16, 0xe2, 0x65, 0xba, 0xe4, 0x43, 0x6f, 0xe3,
                0x29, 0x36, 0x18, 0xe3, 0x45, 0x01, 0x07, 0x20, 0x89, 0x4b, 0x64, 0x5e, 0x89, 0xe1, 0x53,
                0x5b, 0xbd, 0xad, 0x5b, 0x8b, 0x29, 0x06, 0x50, 0x53, 0x08, 0x01, 0xb1, 0x8e, 0xbf, 0xbf,
                0x5e, 0x8f, 0xab, 0x3c, 0x82, 0x87, 0x2a, 0x3e, 0x9b, 0xb7, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xe1, 0x32, 0xa3,
                0x49, 0x76, 0x5c, 0x5b, 0x35, 0x9a, 0x93, 0x3c, 0x6f, 0x3c, 0x63, 0x6d, 0xc0, 0x00
            ], (SocketFlags)119);
            return;
        }

        BAN_STATUS ipBanStatus = IPBanner.CalculateBanStatus(((IPEndPoint)authSocket.socket.RemoteEndPoint).Address);
        switch (ipBanStatus)
        {
            case BAN_STATUS.BAN_STATUS_PERMANENT_BAN:
                authSocket.SendChallengeError(AuthError.CE_ACCOUNT_CLOSED);
                return;
            case BAN_STATUS.BAN_STATUS_TIME_LEFT_ON_BAN:
                authSocket.SendChallengeError(AuthError.CE_ACCOUNT_FREEZED);
                return;
        }

        // Null-terminate the account string
        authSocket.challenge.I[authSocket.challenge.I_len] = 0;
        if (authSocket.challenge.I_len >= 0x50) { authSocket.Disconnect(); return; }

        string accountName = Encoding.ASCII.GetString(authSocket.challenge.I).TrimEnd('\0');
        authSocket.account = AccountMgr.GetSingleton().GetAccount(accountName);

        // Clear the shitty hash (for server)
        int index = accountName.LastIndexOf('#');
        if (index != -1)
        {
            sLog.OutString(L_N_AUTHSOCK_P);
            return;
        }

        // Look up the account information
        sLog.OutDebug(L_D_AUTHSOCK_C_3, accountName);

        if (authSocket.account == null)
        {
            sLog.OutDebug(L_D_AUTHSOCK_C_4);
            authSocket.SendChallengeError(AuthError.CE_NO_ACCOUNT);
            return;
        }

        if (authSocket.account.Banned == 1)
        {
            sLog.OutDebug(L_D_AUTHSOCK_C_5, authSocket.account.Banned);
            authSocket.SendChallengeError(AuthError.CE_ACCOUNT_CLOSED);
            return;
        }
        else if (authSocket.account.Banned > 0)
        {
            sLog.OutDebug(L_D_AUTHSOCK_C_10, authSocket.account.Banned);
            authSocket.SendChallengeError(AuthError.CE_ACCOUNT_FREEZED);
            return;
        }

        if (!authSocket.account.ForcedLocale)
        {
            authSocket.account.Locale = Encoding.ASCII.GetChars(authSocket.challenge.Country);
        }

        if (authSocket.account.Salt == null || authSocket.account.Verifier == null)
        {
            sLog.OutDebug(L_D_AUTHSOCK_C_6);
            authSocket.account.Salt = BigNumber.GenerateRandom(32);

            using (SHA3_256.Create())
            {
                byte[] hash = SHA3_256.HashData([.. authSocket.account.Salt.ToByteArray(), .. Encoding.ASCII.GetBytes(authSocket.account.UsernamePtr)]);
                authSocket.account.Verifier = BigNumber.ModExp(authSocket.g, new BigNumber(hash), authSocket.N);
            }

            if (authSocket.account.Verifier.AsDword() == 0)
            {
                sLog.OutDebug(L_D_AUTHSOCK_C_7);
                authSocket.SendChallengeError(AuthError.CE_NO_ACCOUNT);
                return;
            }
            SLogonSQL.Execute($"UPDATE accounts SET salt='{authSocket.account.Salt}', verifier='{authSocket.account.Verifier}' WHERE acct={authSocket.account.AccountId};");
        }

        BigNumber b = BigNumber.GenerateRandom(152);
        BigNumber gmod = BigNumber.ModExp(authSocket.g, b, authSocket.N);
        BigNumber B = ((authSocket.account.Verifier.ToInt() * 3) + gmod) % authSocket.N;

        if (gmod.GetNumBytes() > 32)
        {
            throw new InvalidOperationException("gmod has more than 32 bytes.");
        }

        BigNumber unk = BigNumber.GenerateRandom(128);
        byte[] response = new byte[200];
        int c = 0;
        response[c++] = 0;
        response[c++] = 0;
        response[c++] = (byte)AuthError.CE_SUCCESS;
        Array.Copy(B.ToByteArray(), 0, response, c, 32);
        c += 32;
        response[c++] = 1;
        response[c++] = authSocket.g.ToByteArray()[0];
        response[c++] = 32;
        Array.Copy(authSocket.N.ToByteArray(), 0, response, c, 32);
        c += 32;
        Array.Copy(authSocket.account.Salt.ToByteArray(), 0, response, c, authSocket.account.Salt.GetNumBytes());
        c += authSocket.account.Salt.GetNumBytes();
        Array.Copy(unk.ToByteArray(), 0, response, c, 16);
        c += 16;
        response[c++] = 0;

        authSocket.socket.Send(response, c, SocketFlags.None);
    }

    public static void HandleProof(AuthSocket authSocket)
    {
        var PatchMgr = new PatchMgr();
        var sLog = new Logger();

        if (authSocket.socket.Available < Marshal.SizeOf<LogonProof>())
            return;

        if (authSocket.patch != null && authSocket.account == null)
        {
            sLog.OutDebug(L_D_AUTHSOCK_P);
            authSocket.socket.ReceiveBufferSize -= 75;
            byte[] bytes = [0x01, 0x0a];
            authSocket.socket.Send(bytes);
            global::LogonServer.PatchMgr.InitiatePatch(authSocket.patch, authSocket);
            return;
        }

        if (authSocket.account == null)
            return;

        sLog.OutDebug(L_D_AUTHSOCK_P_1);

        LogonProof lp = LogonProof.FromBytes(authSocket.socket.ReceiveBufferSize);
        BigNumber A = new(lp.A);
        BigNumber B = new(authSocket.challenge.I);

        if (A % authSocket.N == null)
        {
            sLog.OutDebug(L_D_AUTHSOCK_P);
            authSocket.SendChallengeError(AuthError.CE_NO_ACCOUNT);
            return;
        }

        using (SHA3_256.Create())
        {
            byte[] hash = SHA3_256.HashData(A.ToByteArray());
            BigNumber u = new(hash);
            BigNumber S = BigNumber.ModExp(A * BigNumber.ModExp(authSocket.account.Verifier, u, authSocket.N), authSocket.b, authSocket.N);
            byte[] t = S.ToByteArray();
            byte[] t1 = new byte[16];
            byte[] vK = new byte[40];

            for (int i = 0; i < 16; i++)
            {
                t1[i] = t[i * 2];
            }

            hash = SHA3_256.HashData(t1);
            for (int i = 0; i < 20; i++)
            {
                vK[i * 2] = hash[i];
            }

            for (int i = 0; i < 16; i++)
            {
                t1[i] = t[(i * 2) + 1];
            }

            hash = SHA3_256.HashData(t1);
            for (int i = 0; i < 20; i++)
            {
                vK[(i * 2) + 1] = hash[i];
            }

            authSocket.Sessionkey = new BigNumber(vK);

            hash = SHA3_256.HashData(authSocket.N.ToByteArray());
            byte[] hash2 = SHA3_256.HashData(authSocket.g.ToByteArray());
            for (int i = 0; i < 20; i++)
            {
                hash[i] ^= hash2[i];
            }

            BigNumber t3 = new(hash);
            hash = SHA3_256.HashData(Encoding.ASCII.GetBytes(authSocket.account.UsernamePtr));
            BigNumber t4 = new(hash);
            hash = SHA3_256.HashData(
            [
                .. t3.ToByteArray(),
                .. t4.ToByteArray(),
                .. authSocket.account.Salt.ToByteArray(),
                .. A.ToByteArray(),
                .. B.ToByteArray(),
                .. authSocket.Sessionkey.ToByteArray(),
            ]);
            BigNumber M = new(hash);

            if (!lp.M1.SequenceEqual(M.ToByteArray()))
            {
                sLog.OutDebug(L_D_AUTHSOCK_P_2);
                authSocket.SendChallengeError(AuthError.CE_NO_ACCOUNT);
                return;
            }

            authSocket.account.SetSessionKey(authSocket.Sessionkey.ToByteArray());
            hash = SHA3_256.HashData([.. A.ToByteArray(), .. M.ToByteArray(), .. authSocket.Sessionkey.ToByteArray()]);
            authSocket.SendProofError(0, hash);
            sLog.OutDebug(L_D_AUTHSOCK_P_3);
            authSocket.authenticated = true;

            SLogonSQL.Execute($"UPDATE accounts SET lastlogin=NOW(), lastip='{authSocket.socket.RemoteEndPoint}' WHERE acct={authSocket.account.AccountId};");
        }
    }

    public void SendChallengeError(AuthError error)
    {
        byte[] buffer = [0, 0, (byte)error];
        socket.Send(buffer);
    }

    public void SendProofError(byte error, byte[] m2 = null)
    {
        byte[] buffer = new byte[m2 == null ? 6 : 32];
        buffer[0] = 1;
        buffer[1] = error;

        if (m2 == null)
        {
            BitConverter.GetBytes(3).CopyTo(buffer, 2);
            socket.Send(buffer, 6, SocketFlags.None);
        }
        else
        {
            Array.Copy(m2, 0, buffer, 2, 20);
            socket.Send(buffer, 32, SocketFlags.None);
        }
    }

    public void OnRead()
    {
        if (socket.Available < 1)
            return;

        byte command = (byte)socket.Receive(new byte[1]);
        lastRecv = DateTime.Now;

        if (command < MAX_AUTH_CMD && Handlers.ContainsKey(command))
        {
            Handlers[command](this);
        }
        else
        {
            CLog.Notice("[AuthSocket]", $"Unknown command: {command}");
        }
    }

    public static void HandleRealmlist(AuthSocket authSocket)
    {
        var InfoCore = new InformationCore();
        InfoCore.SendRealms();
    }

    public static void HandleReconnectChallenge(AuthSocket authSocket)
    {
        var sLog = new Logger();
        var IPBanner = new IPBanner();

        if (authSocket.socket.Available < 4)
            return;

        byte[] buffer = new byte[4];
        authSocket.socket.Receive(buffer);
        ushort fullSize = BitConverter.ToUInt16(buffer, 2);
        sLog.OutDetail(L_N_AUTHSOCK_1, fullSize);

        if (authSocket.socket.Available < fullSize + 4)
            return;

        buffer = new byte[fullSize + 4];
        authSocket.socket.Receive(buffer);

        if (fullSize + 4 > Marshal.SizeOf<Challenge>())
        {
            authSocket.Disconnect();
            return;
        }

        sLog.OutDebug(L_D_AUTHSOCK_C_8);

        authSocket.challenge = Challenge.FromBytes(buffer);

        if (authSocket.challenge.Build > LogonServer.MaxBuild || authSocket.challenge.Build < LogonServer.MinBuild)
        {
            authSocket.SendChallengeError(AuthError.CE_WRONG_BUILD_NUMBER);
            return;
        }

        BAN_STATUS ipBanStatus = IPBanner.CalculateBanStatus(((IPEndPoint)authSocket.socket.RemoteEndPoint).Address);
        switch (ipBanStatus)
        {
            case BAN_STATUS.BAN_STATUS_PERMANENT_BAN:
                authSocket.SendChallengeError(AuthError.CE_ACCOUNT_CLOSED);
                return;
            case BAN_STATUS.BAN_STATUS_TIME_LEFT_ON_BAN:
                authSocket.SendChallengeError(AuthError.CE_ACCOUNT_FREEZED);
                return;
        }

        string accountName = Encoding.ASCII.GetString(authSocket.challenge.I).TrimEnd('\0');
        sLog.OutDebug(L_D_AUTHSOCK_C_9, accountName);
        authSocket.account = AccountMgr.GetSingleton().GetAccount(accountName);
        if (authSocket.account == null)
        {
            sLog.OutDebug(L_D_AUTHSOCK_C_4);
            authSocket.SendChallengeError(AuthError.CE_NO_ACCOUNT);
            return;
        }

        if (authSocket.account.Banned == 1)
        {
            authSocket.SendChallengeError(AuthError.CE_ACCOUNT_CLOSED);
            return;
        }
        else if (authSocket.account.Banned > 0)
        {
            authSocket.SendChallengeError(AuthError.CE_ACCOUNT_FREEZED);
            return;
        }

        if (authSocket.account.SessionKey == null)
        {
            authSocket.SendChallengeError(AuthError.CE_SERVER_FULL);
            return;
        }
        byte[] hash = MD5.HashData(authSocket.account.SessionKey);
        byte[] response = new byte[34];
        BitConverter.GetBytes((ushort)2).CopyTo(response, 0);
        Array.Copy(hash, 0, response, 2, 20);
        BitConverter.GetBytes(0UL).CopyTo(response, 22);
        BitConverter.GetBytes(0UL).CopyTo(response, 30);
        authSocket.socket.Send(response);
    }

    public static void HandleReconnectProof(AuthSocket authSocket)
    {
        if (authSocket.account == null)
            return;

        SLogonSQL.Execute($"UPDATE accounts SET lastlogin=NOW(), lastip='{authSocket.socket.RemoteEndPoint}' WHERE acct={authSocket.account.AccountId};");
        authSocket.socket.ReceiveBufferSize -= authSocket.socket.ReceiveBufferSize;

        if (authSocket.account.SessionKey == null)
        {
            byte[] buffer = [3, 0, 1, 0];
            authSocket.socket.Send(buffer);
        }
        else
        {
            byte[] buffer = BitConverter.GetBytes(3);
            authSocket.socket.Send(buffer);
        }
    }

    public static void HandleTransferAccept(AuthSocket authSocket)
    {
        var PatchMgr = new PatchMgr();
        if (authSocket.patch == null)
            return;

        authSocket.socket.ReceiveBufferSize -= 1;
        PatchMgr.BeginPatchJob(authSocket.patch, authSocket, 0);
    }

    public static void HandleTransferResume(AuthSocket authSocket)
    {
        var PatchMgr = new PatchMgr();
        if (authSocket.patch == null)
            return;

        authSocket.socket.ReceiveBufferSize -= 1;
        byte[] buffer = new byte[8];
        int receivedBytes = authSocket.socket.Receive(buffer);
        if (receivedBytes != 8)
            return;

        ulong size = BitConverter.ToUInt64(new ReadOnlySpan<byte>(buffer));
        if (size >= (ulong)authSocket.patch.FileSize)
            return;

        PatchMgr.BeginPatchJob(authSocket.patch, authSocket, (uint)size);
    }

    public static void HandleTransferCancel(AuthSocket authSocket)
    {
        authSocket.socket.ReceiveBufferSize -= 1;
        authSocket.Disconnect();
    }

    private void Disconnect()
    {
        socket.Close();
    }

    internal static uint GetAccountID()
    {
        var m_account = new Account();
        return m_account != null ? m_account.AccountId : 0;
    }

    private const int MAX_AUTH_CMD = 53;

    static AuthSocket()
    {
        Handlers = new Dictionary<int, Action<AuthSocket>>
        {
            { 0, HandleChallenge },
            { 1, HandleProof },
            { 2, HandleReconnectChallenge },
            { 3, HandleReconnectProof },
            { 16, HandleRealmlist },
            { 50, HandleTransferAccept },
            { 51, HandleTransferResume },
            { 52, HandleTransferCancel }
        };
    }

    public BigNumber Sessionkey { get; private set; }

    private static Dictionary<int, Action<AuthSocket>> Handlers { get; set; }
}

public struct Challenge
{
    public ushort Build { get; set; }
    public byte[] I { get; set; }
    public int I_len { get; set; }
    public byte[] Country { get; set; }

    public static Challenge FromBytes(byte[] bytes)
    {
        Challenge challenge = new()
        {
            Build = BitConverter.ToUInt16(bytes, 2),
            I_len = BitConverter.ToInt32(bytes, 4)
        };
        challenge.I = new byte[challenge.I_len];
        Array.Copy(bytes, 8, challenge.I, 0, challenge.I_len);
        challenge.Country = new byte[4];
        Array.Copy(bytes, 8 + challenge.I_len, challenge.Country, 0, 4);
        return challenge;
    }
}

public struct LogonProof
{
    public byte[] A { get; set; }
    public byte[] M1 { get; set; }

    public static LogonProof FromBytes(int size)
    {
        LogonProof proof = new()
        {
            A = new byte[32],
            M1 = new byte[20]
        };
        return proof;
    }
}