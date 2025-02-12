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
using System.Linq;
using System.Runtime.InteropServices;

namespace WaadShared.Auth;

public class BigNumber : IDisposable
{
    private IntPtr _bn;
    private bool _disposed = false;

    // Use conditional compilation to specify the correct library name for each platform
#if WINDOWS
    private const string LIBCRYPTO = "libcrypto.dll";
#else
    private const string LIBCRYPTO = "libcrypto.so";
#endif

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr BN_new();

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern void BN_free(IntPtr a);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_lshift(IntPtr r, IntPtr a, int n);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr BN_bin2bn(byte[] s, int len, IntPtr ret);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_hex2bn(ref IntPtr bn, string str);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_rand(IntPtr rnd, int bits, int top, int bottom);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr BN_copy(IntPtr to, IntPtr from);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_add(IntPtr r, IntPtr a, IntPtr b);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_sub(IntPtr r, IntPtr a, IntPtr b);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr BN_CTX_new();

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern void BN_CTX_free(IntPtr ctx);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_mul(IntPtr r, IntPtr a, IntPtr b, IntPtr ctx);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_div(IntPtr dv, IntPtr rem, IntPtr a, IntPtr d, IntPtr ctx);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_mod(IntPtr r, IntPtr a, IntPtr m, IntPtr ctx);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_exp(IntPtr r, IntPtr a, IntPtr p, IntPtr ctx);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_mod_exp(IntPtr r, IntPtr a, IntPtr p, IntPtr m, IntPtr ctx);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_num_bytes(IntPtr a);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint BN_get_word(IntPtr bn);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern int BN_bn2bin(IntPtr a, byte[] to);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr BN_bn2hex(IntPtr a);

    [DllImport(LIBCRYPTO, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr BN_bn2dec(IntPtr a);

    public BigNumber()
    {
        _bn = BN_new();
        if (_bn == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create BigNumber instance.");
        }
    }

    public BigNumber(BigNumber bn)
    {
        _bn = BN_dup(bn._bn);
    }

    public BigNumber(uint val)
    {
        _bn = BN_new();
        BN_set_word(_bn, val);
    }

    ~BigNumber()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_bn != IntPtr.Zero)
            {
                BN_free(_bn);
                _bn = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void SetDword(uint val)
    {
        BN_set_word(_bn, val);
    }

    public void SetQword(ulong val)
    {
        BN_add_word(_bn, (uint)(val >> 32));
        BN_lshift(_bn, _bn, 32);
        BN_add_word(_bn, (uint)(val & 0xFFFFFFFF));
    }

    public void SetBinary(byte[] bytes, int len)
    {
        byte[] t = new byte[1000];
        for (int i = 0; i < len; i++) t[i] = bytes[len - 1 - i];
        BN_bin2bn(t, len, _bn);
    }

    public void SetHexStr(string str)
    {
        BN_hex2bn(ref _bn, str);
    }

    public void SetRand(int numbits)
    {
        BN_rand(_bn, numbits, 0, 1);
    }

    public BigNumber Assign(BigNumber bn)
    {
        BN_copy(_bn, bn._bn);
        return this;
    }

    public BigNumber AddAssign(BigNumber bn)
    {
        BN_add(_bn, _bn, bn._bn);
        return this;
    }

    public BigNumber SubAssign(BigNumber bn)
    {
        BN_sub(_bn, _bn, bn._bn);
        return this;
    }

    public BigNumber MulAssign(BigNumber bn)
    {
        IntPtr bnctx = BN_CTX_new();
        BN_mul(_bn, _bn, bn._bn, bnctx);
        BN_CTX_free(bnctx);
        return this;
    }

    public BigNumber DivAssign(BigNumber bn)
    {
        IntPtr bnctx = BN_CTX_new();
        BN_div(_bn, IntPtr.Zero, _bn, bn._bn, bnctx);
        BN_CTX_free(bnctx);
        return this;
    }

    public BigNumber ModAssign(BigNumber bn)
    {
        IntPtr bnctx = BN_CTX_new();
        BN_mod(_bn, _bn, bn._bn, bnctx);
        BN_CTX_free(bnctx);
        return this;
    }

    public BigNumber Exp(BigNumber bn)
    {
        BigNumber ret = new();
        IntPtr bnctx = BN_CTX_new();
        BN_exp(ret._bn, _bn, bn._bn, bnctx);
        BN_CTX_free(bnctx);
        return ret;
    }

    public BigNumber ModExp(BigNumber bn1, BigNumber bn2)
    {
        BigNumber ret = new();
        IntPtr bnctx = BN_CTX_new();
        BN_mod_exp(ret._bn, _bn, bn1._bn, bn2._bn, bnctx);
        BN_CTX_free(bnctx);
        return ret;
    }

    public int GetNumBytes()
    {
        return BN_num_bytes(_bn);
    }

    public uint AsDword()
    {
        return BN_get_word(_bn);
    }

    public byte[] AsByteArray()
    {
        byte[] array = new byte[GetNumBytes()];
        BN_bn2bin(_bn, array);
        Array.Reverse(array);
        return array;
    }

    public ByteBuffer AsByteBuffer()
    {
        ByteBuffer ret = new ByteBuffer(GetNumBytes());
        ret.Append(AsByteArray(), GetNumBytes());
        return ret;
    }

    public List<byte> AsByteVector()
    {
        List<byte> ret = new List<byte>(GetNumBytes());
        ret.AddRange(AsByteArray());
        return ret;
    }

    public string AsHexStr()
    {
        IntPtr hexStrPtr = BN_bn2hex(_bn);
        return Marshal.PtrToStringAnsi(hexStrPtr);
    }

    public string AsDecStr()
    {
        IntPtr decStrPtr = BN_bn2dec(_bn);
        return Marshal.PtrToStringAnsi(decStrPtr);
    }

    public static BigNumber operator +(BigNumber a, BigNumber b)
    {
        BigNumber t = new(a);
        return t.AddAssign(b);
    }

    public static BigNumber operator -(BigNumber a, BigNumber b)
    {
        BigNumber t = new(a);
        return t.SubAssign(b);
    }

    public static BigNumber operator *(BigNumber a, BigNumber b)
    {
        BigNumber t = new(a);
        return t.MulAssign(b);
    }

    public static BigNumber operator /(BigNumber a, BigNumber b)
    {
        BigNumber t = new(a);
        return t.DivAssign(b);
    }

    public static BigNumber operator %(BigNumber a, BigNumber b)
    {
        BigNumber t = new(a);
        return t.ModAssign(b);
    }
}