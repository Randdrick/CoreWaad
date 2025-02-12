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
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace WaadShared.Auth;
public class WowCrypt : IDisposable
{
    private bool m_initialized;
    private RC4Engine m_clientDecrypt;
    private RC4Engine m_serverEncrypt;
    private bool _disposed = false;

    public WowCrypt()
    {
        m_initialized = false;
        m_clientDecrypt = new RC4Engine();
        m_serverEncrypt = new RC4Engine();
    }

    public void Init(byte[] K)
    {
        ArgumentNullException.ThrowIfNull(K);

        if (K.Length != 40)
        {
            throw new ArgumentException("Key length must be 40 bytes", nameof(K));
        }

        KeyParameter keyParameter = new KeyParameter(K);
        m_clientDecrypt.Init(false, keyParameter);
        m_serverEncrypt.Init(true, keyParameter);
        m_initialized = true;
    }

    public void Decrypt(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!m_initialized)
        {
            throw new InvalidOperationException("WowCrypt is not initialized.");
        }

        byte[] output = new byte[data.Length];
        m_clientDecrypt.ProcessBytes(data, 0, data.Length, output, 0);
        Array.Copy(output, data, data.Length);
    }

    public void Encrypt(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!m_initialized)
        {
            throw new InvalidOperationException("WowCrypt is not initialized.");
        }

        byte[] output = new byte[data.Length];
        m_serverEncrypt.ProcessBytes(data, 0, data.Length, output, 0);
        Array.Copy(output, data, data.Length);
    }

    // Dispose pattern implementation
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                m_clientDecrypt = null;
                m_serverEncrypt = null;
            }

            _disposed = true;
        }
    }
}