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
using System.Security.Cryptography;

public class Sha3Hash : IDisposable
{
    private SHA3Managed sha3;
    private readonly byte[] mDigest;
    private readonly bool _disposed = false;

    public Sha3Hash()
    {
        sha3 = new SHA3Managed(256); // SHA3-256
        mDigest = new byte[32]; // 256 bits / 8 = 32 bytes
    }

    ~Sha3Hash()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                sha3?.Dispose();
            }

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

        sha3.TransformBlock(data, 0, len, data, 0);
    }

    public void FinalizeHash()
    {
        sha3.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        Array.Copy(sha3.Hash, mDigest, mDigest.Length);
    }

    public byte[] GetDigest()
    {
        return (byte[])mDigest.Clone();
    }
}