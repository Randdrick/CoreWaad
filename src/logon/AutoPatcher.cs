/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2021 WAAD Team <https://arbonne.games-rpg.net/>
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LogonServer;

public class Patch
{
    public uint FileSize { get; set; }
    public byte[] Data { get; set; }
    public uint Version { get; set; }
    public string Locality { get; set; }
    public uint ULocality { get; set; }
    public byte[] MD5 { get; set; }
}

public class PatchJob(Patch patch, AuthSocket client, uint skip)
{
    private readonly AuthSocket _client = client;
    private byte[] _dataPointer = [.. patch.Data.Skip((int)skip)];
    private uint _bytesLeft = patch.FileSize - skip;

    public AuthSocket Client => _client;

    public bool Update()
    {
        if (AuthSocket.GetWriteBufferSize() != 0)
        {
            return true;
        }

        var header = new TransferDataPacket
        {
            Cmd = 0x31,
            ChunkSize = (ushort)((_bytesLeft > 1500) ? 1500 : _bytesLeft)
        };

        bool result = AuthSocket.BurstSend(header.ToBytes());
        if (result)
        {
            result = AuthSocket.BurstSend(_dataPointer, header.ChunkSize);
            if (result)
            {
                _dataPointer = [.. _dataPointer.Skip(header.ChunkSize)];
                _bytesLeft -= header.ChunkSize;
            }
        }

        if (result)
        {
            AuthSocket.BurstPush();
        }

        return _bytesLeft > 0;
    }
}

public class PatchMgr
{
    private static PatchMgr _instance;
    private readonly List<Patch> _patches = [];
    private readonly List<PatchJob> _patchJobs = [];
    private readonly object _patchJobLock = new();
    public static PatchMgr Instance => _instance ??= new PatchMgr();

    public PatchMgr()
    {
        LoadPatches();
    }

    private void LoadPatches()
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "ClientPatches");
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(directory))
        {
            string fileName = Path.GetFileName(file);
            if (fileName.Length < 6 || !fileName.EndsWith('.'))
            {
                continue;
            }

            string locality = fileName[..4];
            if (!uint.TryParse(fileName[4..^1], out uint srcVersion))
            {
                continue;
            }

            byte[] fileData;
            try
            {
                fileData = File.ReadAllBytes(file);
            }
            catch
            {
                continue;
            }
            byte[] hash = MD5.HashData(fileData);

            var patch = new Patch
            {
                FileSize = (uint)fileData.Length,
                Data = fileData,
                Version = srcVersion,
                Locality = locality.ToLower(),
                ULocality = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(locality.ToLower()), 0),
                MD5 = hash
            };

            _patches.Add(patch);
        }
    }

    public Patch FindPatchForClient(uint version, string locality)
    {
        string tmplocality = locality.ToLower();
        uint ulocality = BitConverter.ToUInt32(Encoding.ASCII.GetBytes(tmplocality), 0);

        foreach (var patch in _patches)
        {
            if (patch.ULocality == ulocality)
            {
                if (patch.Version == version)
                {
                    return patch;
                }
            }
        }

        return _patches.Find(p => p.ULocality == ulocality && p.Version == 0);
    }

    public void BeginPatchJob(Patch patch, AuthSocket client, uint skip)
    {
        var job = new PatchJob(patch, client, skip);
        client.PatchJob = job;
        lock (_patchJobLock)
        {
            _patchJobs.Add(job);
        }
    }

    public void UpdateJobs()
    {
        lock (_patchJobLock)
        {
            for (int i = _patchJobs.Count - 1; i >= 0; i--)
            {
                var job = _patchJobs[i];
                if (!job.Update())
                {
                    job.Client.PatchJob = null;
                    _patchJobs.RemoveAt(i);
                }
            }
        }
    }

    public void AbortPatchJob(PatchJob job)
    {
        lock (_patchJobLock)
        {
            _patchJobs.Remove(job);
        }
    }

    public static bool InitiatePatch(Patch patch, AuthSocket client)
    {
        var init = new TransferInitiatePacket
        {
            Cmd = 0x30,
            StrSize = 5,
            Name = "Patch",
            FileSize = patch.FileSize,
            MD5Hash = patch.MD5
        };

        AuthSocket.BurstBegin();
        bool result = AuthSocket.BurstSend(init.ToBytes());
        if (result)
        {
            AuthSocket.BurstPush();
        }
        AuthSocket.BurstEnd();
        return result;
    }
}

public struct TransferInitiatePacket
{
    public byte Cmd;
    public byte StrSize;
    public string Name;
    public ulong FileSize;
    public byte[] MD5Hash;

    public readonly byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(Cmd);
        bw.Write(StrSize);
        bw.Write(Encoding.ASCII.GetBytes(Name));
        bw.Write(FileSize);
        bw.Write(MD5Hash);
        return ms.ToArray();
    }
}

public struct TransferDataPacket
{
    public byte Cmd;
    public ushort ChunkSize;

    public readonly byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(Cmd);
        bw.Write(ChunkSize);
        return ms.ToArray();
    }
}
