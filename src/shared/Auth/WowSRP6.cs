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
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;

public class WowSRP6
{
    private readonly BigNumber N;
    private readonly BigNumber g;

    public WowSRP6()
    {
        N = new BigNumber();
        g = new BigNumber();
        N.SetHexStr("894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7");
        g.SetDword(7);
    }

    public BigNumber ComputeSalt()
    {
        byte[] buffer = new byte[32];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(buffer);
        }

        BigNumber s = new BigNumber();
        s.SetBinary(buffer, 32);

        return s;
    }

    public BigNumber ComputeVerifier(string login, string password, BigNumber s)
    {
        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password) || s.GetNumBytes() == 0)
            return new BigNumber(0);

        // generation de I
        string strLogin = login.ToUpper();
        string strPassword = password.ToUpper();

        Sha3Hash SrpHash = new(256);
        SrpHash.UpdateData($"{strLogin}:{strPassword}");
        SrpHash.FinalizeHash();

        byte[] digest = SrpHash.GetDigest();
        BigNumber x = new();
        x.SetBinary(digest, digest.Length);

        BigNumber v = g.Exp(x).Mod(N);
        return v;
    }
}

public class Sha3Hash : IDisposable
{
    private Sha3Digest sha3;
    private readonly byte[] mDigest;
    private readonly bool _disposed = false;

    public Sha3Hash(int bitLength)
    {
        sha3 = new Sha3Digest(bitLength);
        mDigest = new byte[bitLength / 8];
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
                // Cleanup managed resources if needed
                sha3 = null;
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