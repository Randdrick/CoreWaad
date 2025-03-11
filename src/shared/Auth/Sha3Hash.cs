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
using System.Text;
using Org.BouncyCastle.Crypto.Digests;

namespace WaadShared.Auth;

public class Sha3Hash : IDisposable
{
    private readonly Sha3Digest sha3 = new(512);
    private readonly byte[] mDigest = new byte[32];
    private bool _disposed = false;

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
                // No need to dispose of sha3 as it doesn't implement IDisposable
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
        ArgumentNullException.ThrowIfNull(str);

        byte[] data = Encoding.UTF8.GetBytes(str);
        UpdateData(data, data.Length);
    }

    public void UpdateData(byte[] data, int len)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (len < 0 || len > data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(len));
        }

        sha3.BlockUpdate(data, 0, len);
    }

    public void FinalizeHash()
    {
        sha3.DoFinal(mDigest, 0);
    }

    public byte[] GetDigest()
    {
        return (byte[])mDigest.Clone();
    }
}
