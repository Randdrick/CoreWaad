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
using Org.BouncyCastle.Crypto.Parameters;

namespace WaadShared;

public class RC4Engine
{
    private readonly byte[] perm = new byte[256];
    private byte index1, index2;

    // RC4Engine constructor. Must supply a key and the length of that array.
    public RC4Engine(byte[] keybytes)
    {
        Setup(keybytes);
    }

    public RC4Engine()
    {
        index1 = 0;
        index2 = 0;
        perm[0] = 0;
    }

    // Initializes permutation, etc.
    public void Setup(byte[] keybytes)
    {
        int i = 0;
        byte j = 0, k;
        int keylen = keybytes.Length;

        // Initialize RC4 state (all bytes to 0)
        for (i = 0; i < 256; ++i)
            perm[i] = (byte)i;

        // Randomize the permutation
        for (j = 0, i = 0; i < 256; ++i)
        {
            j += (byte)(perm[i] + keybytes[i % keylen]);
            k = perm[i];
            perm[i] = perm[j];
            perm[j] = k;
        }
    }

    // Processes the specified array. The same function is used for both
    // encryption and decryption.
    public void Process(byte[] input, byte[] output)
    {
        int len = input.Length;
        int i = 0;
        byte j, k;

        for (i = 0; i < len; ++i)
        {
            index1++;
            index2 += perm[index1];

            k = perm[index1];
            perm[index1] = perm[index2];
            perm[index2] = k;

            j = (byte)(perm[index1] + perm[index2]);
            output[i] = (byte)(input[i] ^ perm[j]);
        }
    }

    // Reverses the bytes in an array in the opposite order.
    public static void ReverseBytes(byte[] Pointer)
    {
        int Length = Pointer.Length;
        byte[] Temp = new byte[Length];

        Buffer.BlockCopy(Pointer, 0, Temp, 0, Length);

        for (int i = 0; i < Length; ++i)
        {
            Pointer[i] = Temp[Length - i - 1];
        }
    }

    internal void Init(bool v, KeyParameter keyParameter)
    {
        Setup(keyParameter.GetKey());
    }

    internal void ProcessBytes(byte[] input, int inOff, int length, byte[] output, int outOff)
    {
        byte j, k;

        for (int i = 0; i < length; ++i)
        {
            index1++;
            index2 += perm[index1];

            k = perm[index1];
            perm[index1] = perm[index2];
            perm[index2] = k;

            j = (byte)(perm[index1] + perm[index2]);
            output[outOff + i] = (byte)(input[inOff + i] ^ perm[j]);
        }
    }
}
