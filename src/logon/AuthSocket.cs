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

public class AuthSocket
{
    public Socket socket;
    private Challenge challenge;
    private BigNumber b;
    private readonly BigNumber N;
    private readonly BigNumber g;
    private Account account;
    public bool authenticated;
    public DateTime lastRecv;
    private bool removedFromSet;
    private Patch patch;
    private PatchJob patchJob;
    // Exact bytes sent to client in challenge — used in HandleProof for byte-level consistency
    internal byte[] BBytesSent;
    internal byte[] SaltBytesSent;
    internal Account Account => account;
    private static readonly object authSocketLock = new();
    private static readonly HashSet<AuthSocket> authSockets = [];
    private static DateTime lastCleanup = DateTime.Now;
    private const int CLEANUP_INTERVAL_MS = 60000; // 1 minute
    public PatchJob PatchJob { get; set; }

    public static void BurstBegin() { }
    public static void BurstEnd() { }
    public static bool BurstSend(byte[] data) { return true; }
    public static bool BurstSend(byte[] data, uint length) { return true; }
    public static void BurstPush() { }
    public static uint GetWriteBufferSize() { return 0; }
    private readonly PatchMgr PatchMgr;

    // Constructor: accepts the socket returned by ListenSocket.Accept()
    public AuthSocket(Socket sock)
    {
        PatchMgr = new PatchMgr();
        this.socket = sock;
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
        StartReadThread();
    }

    private void StartReadThread()
    {
        var t = new System.Threading.Thread(() =>
        {
            while (socket != null && !removedFromSet)
            {
                try
                {
                    if (socket.Connected && socket.Available > 0)
                        OnRead();
                    else
                        System.Threading.Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    CLog.Error("[AuthSocket]", "Read thread exception: {0}", ex.Message);
                    break;
                }
            }
            OnDisconnect();
        })
        { IsBackground = true, Name = "AuthSocket" };
        t.Start();
    }

    private static byte[] ToFixedLength(BigNumber value, int size)
    {
        byte[] src = value?.ToByteArray() ?? [];
        if (src.Length == size)
            return src;

        byte[] dst = new byte[size]; // zero-filled
        if (src.Length > size)
        {
            // Take first 'size' bytes — in LE the first bytes are the least-significant
            Array.Copy(src, 0, dst, 0, size);
        }
        else if (src.Length > 0)
        {
            // Copy and leave zeros at the end (high bytes in LE = zero-padding)
            Array.Copy(src, 0, dst, 0, src.Length);
        }
        return dst;
    }

    private static byte[] Sha3(byte[] input)
    {
        using var sha3 = new Sha3Hash();
        sha3.UpdateData(input, input.Length);
        sha3.FinalizeHash();
        return sha3.GetDigest();
    }

    private static void EnsureVerifierConsistency(Account account)
    {
        if (account == null || string.IsNullOrEmpty(account.UsernamePtr))
            return;

        // Validate salt and verifier exist.
        if (account.Salt == null || account.Salt.GetNumBytes() == 0 ||
            account.Verifier == null || account.Verifier.GetNumBytes() == 0)
        {
            CLog.Warning("[AuthSocket]", "Missing SRP salt/verifier for account {0}", account.UsernamePtr);
            return;
        }

        // The verifier stored in DB was computed by the realm server (WaadAscent C++) and is authoritative.
        // We only attempt recomputation when we have the raw plaintext password (not a SHA-2 or SHA-512 hash).
        // SHA-512 = 128 hex chars, SHA-256 = 64 hex chars — we cannot reverse these to get the plaintext.
        string enc = (account.EncryptedPassword ?? "").Trim();
        bool isSHA512 = enc.Length == 128 && enc.All(Uri.IsHexDigit);
        bool isSHA256 = enc.Length == 64  && enc.All(Uri.IsHexDigit);
        if (string.IsNullOrEmpty(enc) || isSHA512 || isSHA256)
        {
            // Cannot recompute — use the verifier from DB as-is.
            return;
        }

        // encrypted_password is either plaintext or SHA-1 pre-hash (40 hex chars).
        string loginUpper = account.UsernamePtr.ToUpperInvariant();
        bool isPreHashed = enc.Length == 40 && enc.All(Uri.IsHexDigit);
        byte[] identityHash = isPreHashed
            ? Convert.FromHexString(enc)
            : SHA1.HashData(Encoding.ASCII.GetBytes($"{loginUpper}:{enc.ToUpperInvariant()}"));

        // Use LE salt bytes (AsByteArray returns LE) matching WaadAscent C++ s.AsByteArray().
        byte[] s32 = ToFixedLength(account.Salt, 32);
        byte[] xHash = SHA1.HashData([.. s32, .. identityHash]);

        BigNumber Nbig = new();
        Nbig.SetHexStr("894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7");
        BigNumber gbig = new();
        gbig.SetDword(7);

        // WaadAscent C++ SetBinary treats SHA1 bytes as LE integer.
        BigNumber x = new();
        x.SetBinaryLE(xHash);
        BigNumber newVerifier = BigNumber.ModExp(gbig, x, Nbig);
        string newVerifierHex = newVerifier.AsHexStr();
        string oldVerifierHex = account.Verifier?.AsHexStr() ?? "";

        if (!string.Equals(oldVerifierHex, newVerifierHex, StringComparison.OrdinalIgnoreCase))
        {
            account.Verifier = newVerifier;
            string saltHex = account.Salt.AsHexStr();
            SLogonSQL.Execute($"UPDATE account_data SET salt='{saltHex}', verifier='{newVerifierHex}' WHERE acct={account.AccountId};");
            CLog.Notice("[AuthSocket]", "SRP verifier updated for account {0}", account.UsernamePtr);
        }
    }

    ~AuthSocket()
    {
        if (patchJob != null)
        {
            PatchMgr.AbortPatchJob(patchJob);
            patchJob = null;
        }
        
        // Clean up account reference to avoid holding stale references
        account = null;
        patch = null;
        patchJob = null;
    }

    public void OnDisconnect()
    {
        CLog.Debug("[AuthSocket]", "Socket disconnected for account={0}", account?.UsernamePtr ?? "<unknown>");
        if (!removedFromSet)
        {
            lock (authSocketLock)
            {
                authSockets.Remove(this);
                removedFromSet = true;
                
                // Perform periodic cleanup every minute
                var now = DateTime.Now;
                if ((now - lastCleanup).TotalMilliseconds > CLEANUP_INTERVAL_MS)
                {
                    CleanupDeadSockets();
                    lastCleanup = now;
                }
            }
        }

        // Clean up references to release memory
        if (patchJob != null)
        {
            try
            {
                PatchMgr.AbortPatchJob(patchJob);
            }
            catch { }
            patchJob = null;
        }
        
        // Null out large references to help GC
        account = null;
        patch = null;
    }

    private static void CleanupDeadSockets()
    {
        // Remove sockets where removedFromSet should have been set but wasn't
        // This is a failsafe to catch sockets that disconnect without calling OnDisconnect
        // Check for sockets inactive for more than 15 minutes to avoid aggressive cleanup
        var now = DateTime.Now;
        int removedCount = 0;
        
        // Create a snapshot to avoid collection modified exceptions
        var socketsSnapshot = new List<AuthSocket>(authSockets);
        
        foreach (var socket in socketsSnapshot)
        {
            try
            {
                if (!socket.removedFromSet && (now - socket.lastRecv).TotalSeconds > 900) // 15 minutes
                {
                    authSockets.Remove(socket);
                    socket.removedFromSet = true;
                    removedCount++;
                }
            }
            catch
            {
                // Skip errors
            }
        }
        
        if (removedCount > 0)
        {
            var sLog = new Logger();
            sLog.OutDebug($"[AuthSocket] Cleaned up {removedCount} dead sockets");
        }
    }

    public static int GetActiveSocketCount()
    {
        lock (authSocketLock)
        {
            return authSockets.Count;
        }
    }

    private static bool ReadExact(System.Net.Sockets.Socket sock, byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read;
            try
            {
                read = sock.Receive(buffer, offset + total, count - total, SocketFlags.None);
            }
            catch
            {
                return false;
            }

            if (read <= 0)
                return false;

            total += read;
        }
        return true;
    }

    private static bool SendExact(System.Net.Sockets.Socket sock, byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int sent;
            try
            {
                sent = sock.Send(buffer, total, count - total, SocketFlags.None);
            }
            catch
            {
                return false;
            }

            if (sent <= 0)
                return false;

            total += sent;
        }

        return true;
    }

    public static void HandleChallenge(AuthSocket authSocket)
    {
        var sLog = new Logger();
        var IPBanner = new IPBanner();
        var PatchMgr = new PatchMgr();

        sLog.OutDebug(L_D_AUTHSOCK_C_0);

        // WoW 3.3.5a auth challenge (after cmd byte consumed by OnRead):
        // error(1)+size(2)+gamename(4)+version(3)+build(2)+platform(4)+os(4)+country(4)+tz(4)+ip(4)+I_len(1) = 33 bytes
        const int FIXED_HDR = 33;
        byte[] hdr = new byte[FIXED_HDR];
        if (!ReadExact(authSocket.socket, hdr, 0, FIXED_HDR))
        {
            authSocket.Disconnect();
            return;
        }

        ushort build   = BitConverter.ToUInt16(hdr, 10);
        byte[] country = hdr[20..24];
        byte   I_len   = hdr[32];

        sLog.OutDebug("[AuthSocket] Challenge parsed: build={0}, MinBuild={1}, MaxBuild={2}, I_len={3}", build, SocketManager.MinBuild, SocketManager.MaxBuild, I_len);

        sLog.OutDetail(L_N_AUTHSOCK, build);

        if (I_len == 0 || I_len >= 0x50) { authSocket.Disconnect(); return; }

        byte[] I = new byte[I_len + 1];
        if (!ReadExact(authSocket.socket, I, 0, I_len))
        {
            authSocket.Disconnect();
            return;
        }
        I[I_len] = 0;

        authSocket.challenge = new Challenge
        {
            Build   = build,
            I       = I,
            I_len   = I_len,
            Country = country
        };

        sLog.OutDebug(L_D_AUTHSOCK_C_1);

        if (build > SocketManager.MaxBuild)
        {
            authSocket.SendChallengeError(AuthError.CE_WRONG_BUILD_NUMBER);
            return;
        }

        if (build < SocketManager.MinBuild)
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

            sLog.OutDebug(L_D_AUTHSOCK_C_2, authSocket.patch.Version, authSocket.patch.Locality);

            byte[] patchChallenge = [
                0x00, 0x00, 0x00, 0x72, 0x50, 0xa7, 0xc9, 0x27, 0x4a, 0xfa, 0xb8, 0x77, 0x80, 0x70, 0x22,
                0xda, 0xb8, 0x3b, 0x06, 0x50, 0x53, 0x4a, 0x16, 0xe2, 0x65, 0xba, 0xe4, 0x43, 0x6f, 0xe3,
                0x29, 0x36, 0x18, 0xe3, 0x45, 0x01, 0x07, 0x20, 0x89, 0x4b, 0x64, 0x5e, 0x89, 0xe1, 0x53,
                0x5b, 0xbd, 0xad, 0x5b, 0x8b, 0x29, 0x06, 0x50, 0x53, 0x08, 0x01, 0xb1, 0x8e, 0xbf, 0xbf,
                0x5e, 0x8f, 0xab, 0x3c, 0x82, 0x87, 0x2a, 0x3e, 0x9b, 0xb7, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xe1, 0x32, 0xa3,
                0x49, 0x76, 0x5c, 0x5b, 0x35, 0x9a, 0x93, 0x3c, 0x6f, 0x3c, 0x63, 0x6d, 0xc0, 0x00
            ];
            _ = SendExact(authSocket.socket, patchChallenge, patchChallenge.Length);
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
        authSocket.account = AccountMgr.GetAccount(accountName);

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

        // Keep verifier aligned with current SRP algorithm for legacy accounts.
        EnsureVerifierConsistency(authSocket.account);

        if (authSocket.account.Salt == null || authSocket.account.Verifier == null)
        {
            sLog.OutError("[AuthSocket] Missing SRP data (salt/verifier) for account {0}", accountName);
            authSocket.SendChallengeError(AuthError.CE_NO_ACCOUNT);
            return;
        }

        authSocket.b = BigNumber.GenerateRandom(152);
        BigNumber gmod = BigNumber.ModExp(authSocket.g, authSocket.b, authSocket.N);
        BigNumber B = ((authSocket.account.Verifier * new BigNumber(3u)) + gmod) % authSocket.N;

        if (gmod.GetNumBytes() > 32)
        {
            CLog.Error("[AuthSocket]", "gmod has more than 32 bytes ({0}), normalizing", gmod.GetNumBytes());
        }

        BigNumber unk = BigNumber.GenerateRandom(128);
        // Capture exact bytes to be sent so HandleProof can use the identical values
        byte[] b32sent = ToFixedLength(B, 32);
        byte[] s32sent = ToFixedLength(authSocket.account.Salt, 32);
        authSocket.BBytesSent = b32sent;
        authSocket.SaltBytesSent = s32sent;

        CLog.Debug("[AuthSocket]", "SRP challenge: B={0}", BitConverter.ToString(b32sent).Replace("-", ""));
        CLog.Debug("[AuthSocket]", "SRP challenge: s={0}", BitConverter.ToString(s32sent).Replace("-", ""));

        byte[] response = new byte[200];
        int c = 0;
        response[c++] = 0;
        response[c++] = 0;
        response[c++] = (byte)AuthError.CE_SUCCESS;
        Array.Copy(b32sent, 0, response, c, 32);
        c += 32;
        response[c++] = 1;
        response[c++] = ToFixedLength(authSocket.g, 1)[0];
        response[c++] = 32;
        Array.Copy(ToFixedLength(authSocket.N, 32), 0, response, c, 32);
        c += 32;
        Array.Copy(s32sent, 0, response, c, 32);
        c += 32;
        Array.Copy(ToFixedLength(unk, 16), 0, response, c, 16);
        c += 16;
        response[c++] = 0;

        if (!SendExact(authSocket.socket, response, c))
        {
            CLog.Error("[AuthSocket]", "Failed to send full challenge response ({0} bytes)", c);
            authSocket.Disconnect();
            return;
        }
        CLog.Debug("[AuthSocket]", "Challenge response sent ({0} bytes)", c);
    }

    public static void HandleProof(AuthSocket authSocket)
    {
        var PatchMgr = new PatchMgr();
        var sLog = new Logger();

        CLog.Debug("[AuthSocket]", "HandleProof entered: patch={0}, account={1}", authSocket.patch != null, authSocket.account?.UsernamePtr ?? "<null>");

        if (authSocket.patch != null && authSocket.account == null)
        {
            sLog.OutDebug(L_D_AUTHSOCK_P);
            authSocket.socket.Receive(new byte[74]); // discard proof data (command byte already consumed)
            byte[] bytes = [0x01, 0x0a];
            authSocket.socket.Send(bytes);
            global::LogonServer.PatchMgr.InitiatePatch(authSocket.patch, authSocket);
            return;
        }

        if (authSocket.account == null)
            return;

        sLog.OutDebug(L_D_AUTHSOCK_P_1);

        // WoW 3.3.5a proof payload AFTER command byte: A(32) + M1(20) + crc(20) + nkeys(1) + secflags(1) = 74 bytes
        byte[] proofData = new byte[74];
        if (!ReadExact(authSocket.socket, proofData, 0, 74))
        {
            CLog.Error("[AuthSocket]", "HandleProof failed to read 74-byte proof payload");
            return;
        }
        CLog.Debug("[AuthSocket]", "HandleProof payload read successfully");

        // --- All byte arrays are little-endian (WoW protocol), matching C++ AsByteArray() ---

        byte[] a32 = proofData[0..32];          // client's A, LE 32 bytes (raw wire bytes)
        byte[] M1received = proofData[32..52];  // client's M1, 20 bytes

        // Use the EXACT bytes sent in HandleChallenge for byte-level consistency
        byte[] b32 = authSocket.BBytesSent;
        byte[] s32 = authSocket.SaltBytesSent;
        byte[] n32 = ToFixedLength(authSocket.N, 32);   // LE N (32 bytes)
        byte[] gBytes = authSocket.g.ToByteArray();     // LE g (1 byte = {7})

        if (b32 == null || s32 == null)
        {
            CLog.Error("[AuthSocket]", "HandleProof: missing BBytesSent or SaltBytesSent — challenge not completed");
            return;
        }

        // A: read from wire as little-endian into BigNumber (needed for modular exponentiation)
        BigNumber A = new();
        A.SetBinaryLE(a32, 32);

        // v: the verifier (recomputed with LE salt by EnsureVerifierConsistency)
        BigNumber v = authSocket.account.Verifier;

        // u = SHA1(A_LE | B_LE) — exact wire bytes.
        // C++ Ascent SetBinary reverses bytes (LE input convention), matching WoW client's convention.
        byte[] uBytes = SHA1.HashData([.. a32, .. b32]);
        BigNumber u = new();
        u.SetBinaryLE(uBytes);  // LE integer — matches C++ Ascent BigNumber::SetBinary behaviour

        // S = (A * v^u)^b mod N
        BigNumber S = BigNumber.ModExp(A * BigNumber.ModExp(v, u, authSocket.N), authSocket.b, authSocket.N);

        // Session key K: 40-byte interleaved SHA1 of even/odd bytes of S (LE)
        byte[] t = ToFixedLength(S, 32);
        byte[] t1 = new byte[16];
        byte[] vK = new byte[40];
        for (int i = 0; i < 16; i++) t1[i] = t[i * 2];
        byte[] kHash = SHA1.HashData(t1);
        for (int i = 0; i < 20; i++) vK[i * 2] = kHash[i];
        for (int i = 0; i < 16; i++) t1[i] = t[(i * 2) + 1];
        kHash = SHA1.HashData(t1);
        for (int i = 0; i < 20; i++) vK[(i * 2) + 1] = kHash[i];

        // M1 = SHA1( H(N) XOR H(g) | H(I) | s | A | B | K )
        byte[] hashN = SHA1.HashData(n32);
        byte[] hashG = SHA1.HashData(gBytes);
        for (int i = 0; i < 20; i++) hashN[i] ^= hashG[i];
        // C++ Ascent: t3.SetBinary(hash,20) stores as LE, t3.AsByteArray() returns original bytes unchanged.
        // No reversal needed — use the direct XOR output.
        // WoW SRP always hashes the uppercased login for H(I)
        string username = authSocket.account.UsernamePtr.ToUpperInvariant();
        byte[] userHash = SHA1.HashData(Encoding.ASCII.GetBytes(username));

        byte[] m1Computed = SHA1.HashData([.. hashN, .. userHash, .. s32, .. a32, .. b32, .. vK]);

        // Diagnostic logging — compare all components to identify divergence
        CLog.Debug("[AuthSocket]", "SRP a32 ={0}", BitConverter.ToString(a32).Replace("-", ""));
        CLog.Debug("[AuthSocket]", "SRP b32 ={0}", BitConverter.ToString(b32).Replace("-", ""));
        CLog.Debug("[AuthSocket]", "SRP s32 ={0}", BitConverter.ToString(s32).Replace("-", ""));
        CLog.Debug("[AuthSocket]", "SRP u   ={0}", BitConverter.ToString(uBytes).Replace("-", ""));
        CLog.Debug("[AuthSocket]", "SRP S   ={0}", BitConverter.ToString(t).Replace("-", ""));
        CLog.Debug("[AuthSocket]", "SRP K   ={0}", BitConverter.ToString(vK).Replace("-", ""));
        CLog.Debug("[AuthSocket]", "SRP HNg ={0}", BitConverter.ToString(hashN).Replace("-", ""));
        CLog.Debug("[AuthSocket]", "SRP HI  ={0}", BitConverter.ToString(userHash).Replace("-", "")) ;
        CLog.Debug("[AuthSocket]", "SRP user={0}", username);

        if (!M1received.SequenceEqual(m1Computed))
        {
            CLog.Error("[AuthSocket]", "SRP M1 mismatch for account {0}", authSocket.account.UsernamePtr);
            CLog.Error("[AuthSocket]", "M1 recv={0}", BitConverter.ToString(M1received).Replace("-", ""));
            CLog.Error("[AuthSocket]", "M1 calc={0}", BitConverter.ToString(m1Computed).Replace("-", ""));
            sLog.OutDebug(L_D_AUTHSOCK_P_2);
            authSocket.SendChallengeError(AuthError.CE_NO_ACCOUNT);
            return;
        }

        // M2 = SHA1(A | M1 | K)
        authSocket.Sessionkey = new BigNumber(vK);
        authSocket.account.SetSessionKey(vK);
        byte[] m2 = SHA1.HashData([.. a32, .. m1Computed, .. vK]);
        authSocket.SendProofError(0, m2);
        CLog.Debug("[AuthSocket]", "SRP proof accepted for account {0}", authSocket.account.UsernamePtr);
        sLog.OutDebug(L_D_AUTHSOCK_P_3);
        authSocket.authenticated = true;

        SLogonSQL.Execute($"UPDATE accounts SET lastlogin=CURRENT_TIMESTAMP, lastip='{((IPEndPoint)authSocket.socket.RemoteEndPoint).Address}' WHERE acct={authSocket.account.AccountId};");
    }

    public void SendChallengeError(AuthError error)
    {
        byte[] buffer = [0, 0, (byte)error];
        _ = SendExact(socket, buffer, buffer.Length);
    }

    public void SendProofError(byte error, byte[] m2 = null)
    {
        byte[] buffer = new byte[32];
        buffer[0] = 1;
        buffer[1] = error;

        if (m2 == null)
        {
            BitConverter.GetBytes(3).CopyTo(buffer, 2);
            _ = SendExact(socket, buffer, 6);
        }
        else
        {
            Array.Copy(m2, 0, buffer, 2, 20);
            _ = SendExact(socket, buffer, 32);
        }
    }

    public void OnRead()
    {
        if (socket.Available < 1)
            return;

        byte[] cmdBuffer = new byte[1];
        int received = socket.Receive(cmdBuffer, 0, 1, SocketFlags.None);
        if (received != 1)
            return;

        byte command = cmdBuffer[0];
        lastRecv = DateTime.Now;
        CLog.Debug("[AuthSocket]", "OnRead command={0}, available={1}", command, socket.Available);

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
        // Consume the 4-byte unknown field in the CMD_REALM_LIST client packet
        // (cmd byte already consumed by OnRead; 4 bytes remain and must be drained)
        byte[] unk = new byte[4];
        ReadExact(authSocket.socket, unk, 0, 4);

        InformationCore.Instance.SendRealms(authSocket);
        CLog.Debug("[AuthSocket]", "Realm list sent to account {0}", authSocket.Account?.UsernamePtr ?? "<unknown>");
    }

    public static void HandleReconnectChallenge(AuthSocket authSocket)
    {
        var sLog = new Logger();
        var IPBanner = new IPBanner();

        byte[] buffer = new byte[4];
        if (!ReadExact(authSocket.socket, buffer, 0, 4))
        {
            authSocket.Disconnect();
            return;
        }
        ushort fullSize = BitConverter.ToUInt16(buffer, 2);
        sLog.OutDetail(L_N_AUTHSOCK_1, fullSize);

        buffer = new byte[fullSize + 4];
        if (!ReadExact(authSocket.socket, buffer, 0, fullSize + 4))
        {
            authSocket.Disconnect();
            return;
        }

        if (fullSize + 4 > Marshal.SizeOf<Challenge>())
        {
            authSocket.Disconnect();
            return;
        }

        sLog.OutDebug(L_D_AUTHSOCK_C_8);

        authSocket.challenge = Challenge.FromBytes(buffer);

        if (authSocket.challenge.Build > SocketManager.MaxBuild || authSocket.challenge.Build < SocketManager.MinBuild)
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
        authSocket.account = AccountMgr.GetAccount(accountName);
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

        SLogonSQL.Execute($"UPDATE accounts SET lastlogin=CURRENT_TIMESTAMP, lastip='{((System.Net.IPEndPoint)authSocket.socket.RemoteEndPoint).Address}' WHERE acct={authSocket.account.AccountId};");

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

        PatchMgr.BeginPatchJob(authSocket.patch, authSocket, 0);
    }

    public static void HandleTransferResume(AuthSocket authSocket)
    {
        var PatchMgr = new PatchMgr();
        if (authSocket.patch == null)
            return;

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