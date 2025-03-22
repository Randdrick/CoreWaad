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

using System.Runtime.InteropServices;

namespace LogonServer;

public class AuthStructs
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct sAuthLogonChallengeC
    {
        public byte cmd;
        public byte error;
        public ushort size;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] gamename;
        public byte version1;
        public byte version2;
        public byte version3;
        public ushort build;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] platform;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] os;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] country;
        public uint timezone_bias;
        public uint ip;
        public byte I_len;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
        public byte[] I;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct sAuthReconnectChallengeC
    {
        public byte cmd;
        public byte error;
        public ushort size;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] gamename;
        public byte version1;
        public byte version2;
        public byte version3;
        public ushort build;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] platform;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] os;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] country;
        public uint timezone_bias;
        public uint ip;
        public byte I_len;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 50)]
        public byte[] I;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct sAuthLogonChallengeS
    {
        public byte cmd;
        public byte error;
        public byte unk2;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] B;
        public byte g_len;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] g;
        public byte N_len;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] N;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] s;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] unk3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct sAuthLogonProofC
    {
        public byte cmd;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] A;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] M1;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] crc_hash;
        public byte number_of_keys;
        public byte unk;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct sAuthLogonProofKeyC
    {
        public ushort unk1;
        public uint unk2;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] unk3;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public ushort[] unk4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct sAuthLogonProofS
    {
        public byte cmd;
        public byte error;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] M2;
        public uint unk2;
        public ushort unk203;
    }
}
