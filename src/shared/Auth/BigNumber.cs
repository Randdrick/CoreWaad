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
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace WaadShared.Auth;

public class BigNumber
{
    private BigInteger _bn;
    private byte[] _array;

    public BigNumber()
    {
        _bn = new BigInteger(0);
        _array = null;
    }

    public BigNumber(string v)
    {
        _bn = new BigInteger(0);
        _array = null;
    }

    public BigNumber(BigNumber bn)
    {
        _bn = new BigInteger(bn._bn.ToByteArray());
        _array = null;
    }

    public BigNumber(uint val)
    {
        _bn = new BigInteger(val);
        _array = null;
    }

    public void SetDword(uint val)
    {
        _bn = new BigInteger(val);
    }

    public void SetQword(ulong val)
    {
        _bn = new BigInteger(val);
    }

    public void SetBinary(byte[] bytes, int v)
    {
        bytes = [.. bytes.Reverse()];
        _bn = new BigInteger(bytes);
    }

    public void SetHexStr(string str)
    {
        _bn = BigInteger.Parse(str, System.Globalization.NumberStyles.HexNumber);
    }

    public void SetRand(int numbits)
    {
        Random rand = new();
        byte[] bytes = new byte[numbits / 8];
        rand.NextBytes(bytes);
        _bn = new BigInteger(bytes);
    }

    public static BigNumber operator +(BigNumber a, BigNumber b)
    {
        return new BigNumber { _bn = a._bn + b._bn };
    }

    public static BigNumber operator -(BigNumber a, BigNumber b)
    {
        return new BigNumber { _bn = a._bn - b._bn };
    }

    public static BigNumber operator *(BigNumber a, BigNumber b)
    {
        return new BigNumber { _bn = a._bn * b._bn };
    }

    public static BigNumber operator /(BigNumber a, BigNumber b)
    {
        return new BigNumber { _bn = a._bn / b._bn };
    }

    public static BigNumber operator %(BigNumber a, BigNumber b)
    {
        return new BigNumber { _bn = a._bn % b._bn };
    }

    public BigNumber Exp(BigNumber bn)
    {
        return new BigNumber { _bn = BigInteger.Pow(_bn, (int)bn._bn) };
    }

    public BigNumber ModExp(BigNumber bn1, BigNumber bn2)
    {
        return new BigNumber { _bn = BigInteger.ModPow(_bn, bn1._bn, bn2._bn) };
    }

    public int GetNumBytes()
    {
        return (_bn.ToByteArray().Length + 7) / 8;
    }

    public uint AsDword()
    {
        return (uint)_bn;
    }

    public byte[] AsByteArray()
    {
        if (_array != null)
        {
            _array = null;
        }
        _array = _bn.ToByteArray();
        if (_array[0] == 0)
        {
            _array = [.. _array.Skip(1)];
        }
        Array.Reverse(_array);
        return _array;
    }

    public List<byte> AsByteList()
    {
        return [.. AsByteArray()];
    }

    public string AsHexStr()
    {
        return _bn.ToString("X");
    }

    public string AsDecStr()
    {
        return _bn.ToString();
    }
}
