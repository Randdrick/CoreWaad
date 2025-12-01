/*
 * Ascent MMORPG Server - Mersenne Twister Random Generator (C# port)
 * Copyright (C) 2005-2008 Ascent Team <http://www.ascentcommunity.com/>
 * Ported and adapted for WAAD C# shared core
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
 */

using System;

namespace WaadShared.RandomGen
{
    public static class MersenneTwister
    {
        private static readonly object _lock = new();
        private static CRandomMersenne _rng = new((uint)Environment.TickCount);

        public static void InitRandomNumberGenerators()
        {
            lock (_lock)
                _rng = new CRandomMersenne((uint)Environment.TickCount);
        }

        public static void ReseedRandomNumberGenerators()
        {
            lock (_lock)
                _rng.RandomInit((uint)Environment.TickCount);
        }

        public static void CleanupRandomNumberGenerators() { /* No-op for managed code */ }

        public static double RandomDouble() { lock (_lock) return _rng.Random(); }
        public static double RandomDouble(double n) { lock (_lock) return _rng.Random() * n; }
        public static float RandomFloat() { lock (_lock) return (float)_rng.Random(); }
        public static float RandomFloat(float n) { lock (_lock) return (float)(_rng.Random() * n); }
        public static uint RandomUInt() { lock (_lock) return _rng.BRandom(); }
        public static uint RandomUInt(uint n) { lock (_lock) return (uint)(_rng.Random() * n); }
    }

    // Mersenne Twister implementation (MT19937)
    public class CRandomMersenne
    {
        // Constants for MT19937
        private const int MERS_N = 624;
        private const int MERS_M = 397;
        private const uint MERS_A = 0x9908B0DF;
        private const uint MERS_B = 0x9D2C5680;
        private const uint MERS_C = 0xEFC60000;
        private const int MERS_R = 31;
        private const int MERS_U = 11;
        private const int MERS_S = 7;
        private const int MERS_T = 15;
        private const int MERS_L = 18;
        private const uint MERS_F = 1812433253U;
        private const uint UPPER_MASK = 0x80000000U;
        private const uint LOWER_MASK = 0x7FFFFFFFU;

        private readonly uint[] mt = new uint[MERS_N];
        private int mti = MERS_N + 1;
        private uint LastInterval = 0;
        private uint RLimit = 0;

        public CRandomMersenne(uint seed) { RandomInit(seed); }

        public void RandomInit(uint seed)
        {
            mt[0] = seed;
            for (mti = 1; mti < MERS_N; mti++)
                mt[mti] = MERS_F * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + (uint)mti;
        }

        public void RandomInitByArray(uint[] seeds, int length)
        {
            RandomInit(19650218U);
            int i = 1, j = 0, k = MERS_N > length ? MERS_N : length;
            for (; k > 0; k--)
            {
                mt[i] = (mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1664525U)) + seeds[j] + (uint)j;
                i++; j++;
                if (i >= MERS_N) { mt[0] = mt[MERS_N - 1]; i = 1; }
                if (j >= length) j = 0;
            }
            for (k = MERS_N - 1; k > 0; k--)
            {
                mt[i] = (mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1566083941U)) - (uint)i;
                i++;
                if (i >= MERS_N) { mt[0] = mt[MERS_N - 1]; i = 1; }
            }
            mt[0] = 0x80000000U;
        }

        public int IRandom(int min, int max)
        {
            if (max < min) (min, max) = (max, min);
            uint interval = (uint)(max - min + 1);
            if (interval != LastInterval)
            {
                RLimit = 0xFFFFFFFFU / interval * interval;
                LastInterval = interval;
            }
            uint r;
            do { r = BRandom(); } while (r >= RLimit);
            return (int)(min + (r % interval));
        }

        public int IRandomX(int min, int max) => IRandom(min, max); // Exact version not needed for most uses

        public double Random() => (BRandom() >> 1) * (1.0 / 2147483648.0);

        public uint BRandom()
        {
            uint y;
            if (mti >= MERS_N)
            {
                int kk;
                for (kk = 0; kk < MERS_N - MERS_M; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + MERS_M] ^ (y >> 1) ^ ((y & 1) == 0 ? 0U : MERS_A);
                }
                for (; kk < MERS_N - 1; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + (MERS_M - MERS_N)] ^ (y >> 1) ^ ((y & 1) == 0 ? 0U : MERS_A);
                }
                y = (mt[MERS_N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                mt[MERS_N - 1] = mt[MERS_M - 1] ^ (y >> 1) ^ ((y & 1) == 0 ? 0U : MERS_A);
                mti = 0;
            }
            y = mt[mti++];
            y ^= y >> MERS_U;
            y ^= (y << MERS_S) & MERS_B;
            y ^= (y << MERS_T) & MERS_C;
            y ^= y >> MERS_L;
            return y;
        }
    }
}
