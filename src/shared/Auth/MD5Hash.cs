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

namespace WaadShared.Auth
{
    public class MD5Hash : IDisposable
    {
        private readonly MD5 _md5;
        private bool _disposed = false;

        public MD5Hash()
        {
            _md5 = MD5.Create();
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
                    _md5?.Dispose();
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
            UpdateData(data);
        }

        public void UpdateData(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _md5.TransformBlock(data.ToArray(), 0, data.Length, null, 0);
        }

        public void FinalizeHash()
        {
            _md5.TransformFinalBlock([], 0, 0);
        }

        public byte[] GetDigest()
        {
            return _md5.Hash;
        }
    }
}