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

using System.Security.Cryptography;

namespace WaadShared.Auth;

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

    public static BigNumber ComputeSalt()
    {
        byte[] buffer = new byte[32];
        RandomNumberGenerator.Fill(buffer);

        BigNumber s = new();
        s.SetBinary(buffer, 32);

        return s;
    }

    public BigNumber ComputeVerifier(string login, string password, BigNumber s)
    {
        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password) || s.GetNumBytes() == 0)
            return new BigNumber();

        // génération de I
        string strLogin = login.ToUpper();
        string strPassword = password.ToUpper();
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{strLogin}:{strPassword}"));

        BigNumber x = new();
        x.SetBinary(hash, hash.Length);

        BigNumber v = g.Exp(x).ModExp(N, N);
        return v;
    }
}
