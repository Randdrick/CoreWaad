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
using System.Runtime.InteropServices;
using System.Text;

public class MD5Hash : IDisposable
{
    private MD5_CTX mC;
    private byte[] mDigest;
    private bool _disposed = false;

    // Use conditional compilation to specify the correct library name for each platform
    #if WINDOWS
    private const string LIBCRYPTO = "libcrypto.dll";
    #else
    private const string LIBCRYPTO = "libcrypto.so";
    #endif

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern void MD5_Init(ref MD5_CTX c);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern void MD5_Update(ref MD5_CTX c, byte[] data, int len);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern void MD5_Final(byte[] md, ref MD5_CTX c);

    public MD5Hash()
    {
        mC = new MD5_CTX();
        mDigest = new byte[MD5_DIGEST_LENGTH];
        MD5_Init(ref mC);
    }

    ~MD5Hash()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Cleanup managed resources if needed
                mDigest = null;
            }

            // Cleanup unmanaged resources if needed

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void UpdateData(string str)
    {
        if (str == null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        byte[] data = Encoding.UTF8.GetBytes(str);
        UpdateData(data, data.Length);
    }

    public void UpdateData(byte[] data, int len)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (len < 0 || len > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(len));
        }

        MD5_Update(ref mC, data, len);
    }

    public void FinalizeHash()
    {
        MD5_Final(mDigest, ref mC);
    }

    public byte[] GetDigest()
    {
        return (byte[])mDigest.Clone();
    }

    private const int MD5_DIGEST_LENGTH = 16;

    [StructLayout(LayoutKind.Sequential)]
    private struct MD5_CTX
    {
        public uint count0;
        public uint count1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] buffer;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] state;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] digest;
    }
}