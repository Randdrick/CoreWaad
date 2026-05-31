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

        byte[] s = [0xC2, 0xB3, 0x72, 0x3C, 0xC6, 0xAE, 0xD9, 0xB5, 0x34, 0x3C, 0x53, 0xEE, 0x2F, 0x43, 0x67, 0xCE];
        byte[] r = [0xCC, 0x98, 0xAE, 0x04, 0xE8, 0x97, 0xEA, 0xCA, 0x12, 0xDD, 0xC0, 0x93, 0x42, 0x91, 0x53, 0x57];
        byte[] pass = new byte[1024];

        byte[] decryptHash;
        byte[] encryptHash;
        using (var hmac = new HMACSHA1(s))
            decryptHash = hmac.ComputeHash(K);
        using (var hmac = new HMACSHA1(r))
            encryptHash = hmac.ComputeHash(K);

        // Debug : Affiche les clés générées (pour vérification manuelle)
        CLog.Debug("[WowCrypt]", "decryptHash: " + BitConverter.ToString(decryptHash).Replace("-", ""));
        CLog.Debug("[WowCrypt]", "encryptHash: " + BitConverter.ToString(encryptHash).Replace("-", ""));

        // initialize rc4 structs
        m_clientDecrypt.Init(false, new KeyParameter(decryptHash), decryptHash.Length);
        m_serverEncrypt.Init(true, new KeyParameter(encryptHash), encryptHash.Length);

        // initial encryption pass -- this is just to get key position,
        // the data doesn't actually have to be initialized as discovered
        // by client debugging.
        m_serverEncrypt.ProcessBytes(pass, 0, pass.Length, pass, 0);
        m_clientDecrypt.ProcessBytes(pass, 0, pass.Length, pass, 0);

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