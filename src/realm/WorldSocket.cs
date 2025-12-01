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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using WaadShared;
using WaadShared.Auth;
using WaadShared.AuthCodes;
using WaadShared.Network;
using WaadShared.RandomGen;
using static WaadShared.WorldSocket;

namespace WaadRealmServer
{
    public class WorldSocket : Socket, IWorldSocketSessionInfo
    {
        // Buffer sizes (configurables)
        public static int SendBufferSize { get; set; } = 131078;
        public static int RecvBufferSize { get; set; } = 16384;

        public bool Authed;
        private readonly byte[] K = new byte[40];
        private int mSize, mOpcode, mRemaining;
        private int _latency;
        private Session m_session;
        private readonly uint mSeed;
        private uint mClientSeed;
        private uint mClientBuild;
        private uint mRequestID;
        private WorldPacket pAuthenticationPacket;
        private string m_fullAccountName;
        private readonly WowCrypt _crypt;
        private readonly FastQueue<WorldPacket, DummyLock> _queue;
        private readonly object queueLock = new();

        // Packet header structures
        internal struct ClientPktHeader(ushort size = 0, uint cmd = 0)
        {
            public ushort size = size;
            public uint cmd = cmd;
            public readonly byte[] ToBytes()
            {
                var arr = new byte[6];
                BitConverter.GetBytes(size).CopyTo(arr, 0);
                BitConverter.GetBytes(cmd).CopyTo(arr, 2);
                return arr;
            }
        }

        internal struct ServerPktHeader
        {
            public ushort size;
            public ushort cmd;
            public readonly byte[] ToBytes()
            {
                var arr = new byte[4];
                BitConverter.GetBytes(size).CopyTo(arr, 0);
                BitConverter.GetBytes(cmd).CopyTo(arr, 2);
                return arr;
            }
        }

        internal enum OutPacketResult
        {
            Success,
            NoRoomInBuffer,
            NotConnected,
            SocketError
        }

        public WorldSocket(System.Net.Sockets.Socket socket)
            : this(socket, SendBufferSize, RecvBufferSize)
        {
        }

        public WorldSocket(System.Net.Sockets.Socket socket, int sendBufferSize, int recvBufferSize)
            : base(socket, sendBufferSize, recvBufferSize)
        {
            Authed = false;
            Array.Clear(K, 0, K.Length);
            mSize = mOpcode = mRemaining = 0;
            _latency = 0;
            m_session = null;
            mSeed = (uint)(MersenneTwister.RandomUInt() % 0xFFFFFFF0 + 10);
            mClientSeed = 0;
            mClientBuild = 0;
            pAuthenticationPacket = null;
            mRequestID = 0;
            socket.NoDelay = false;
            m_fullAccountName = null;
            _crypt = new WowCrypt();
            _queue = new FastQueue<WorldPacket, DummyLock>();
        }

        public override void OnDisconnect()
        {
            if (m_session != null)
            {
                m_session.SetServer(null);
                m_session.SetSocket(null);
                ClientMgr.Instance.DestroySession(m_session.SessionId);
                m_session = null;
            }
            if (mRequestID != 0)
            {
                LogonCommHandler.Instance.UnauthedSocketClose(mRequestID);
                mRequestID = 0;
            }
            // Nettoyage des ressources
            pAuthenticationPacket = null;
        }

        public void InformationRetreiveCallback(WorldPacket recvData, uint requestId)
        {
            if (requestId != mRequestID)
                return;

            uint error = recvData.ReadUInt32();
            if (error != 0 || pAuthenticationPacket == null)
            {
                OutPacket((ushort)Opcodes.SMSG_AUTH_RESPONSE, 1, [(byte)LoginErrorCode.AUTH_FAILED]);
                return;
            }

            string accountName = recvData.ReadString();
            string gmFlags = recvData.ReadString();
            byte accountFlags = recvData.ReadByte();
            uint accountId = recvData.ReadUInt32();
            string lang = "enUS";

            string forcedPerms = LogonCommHandler.Instance.GetForcedPermissions(accountName);
            if (!string.IsNullOrEmpty(forcedPerms))
                gmFlags = forcedPerms;

            CLog.Debug("[GM FORCED PERMISSIONS]", R_D_WRDSOCK, accountId, accountName);
            CLog.Notice(" >> ", R_N_WRDSOCK, accountName, accountId, mRequestID);
            mRequestID = 0;

            recvData.Read(K, 0, 40);
            _crypt.Init(K);

            var BNK = new BigNumber();
            BNK.SetBinary(K, 40);

            if (recvData.Size < recvData.Contents.Length)
                lang = recvData.ReadString();

            var session = ClientMgr.Instance.GetSessionByAccountId(accountId);
            if (session != null)
            {
                session.Disconnect();
                OutPacket((ushort)Opcodes.SMSG_AUTH_RESPONSE, 1, [(byte)LoginErrorCode.AUTH_REJECT]);
                return;
            }

            m_session = ClientMgr.Instance.CreateSession(accountId);
            if (m_session == null)
            {
                OutPacket((ushort)Opcodes.SMSG_AUTH_RESPONSE, 1, [(byte)LoginErrorCode.AUTH_FAILED]);
                CLog.Error("[WorldSocket]", R_E_WRDSOCK_2);
                return;
            }

            byte[] digest = new byte[32];
            pAuthenticationPacket.Read(digest, 0, 32);

            using (var sha3 = new Sha3Hash())
            {
                sha3.UpdateData(!string.IsNullOrEmpty(m_fullAccountName) ? m_fullAccountName : accountName);
                byte[] t = BitConverter.GetBytes(0u);
                sha3.UpdateData(t, 4);
                byte[] clientSeedBytes = BitConverter.GetBytes(mClientSeed);
                sha3.UpdateData(clientSeedBytes, 4);
                byte[] serverSeedBytes = BitConverter.GetBytes(mSeed);
                sha3.UpdateData(serverSeedBytes, 4);
                byte[] bnBytes = BNK.AsByteArray();
                sha3.UpdateData(bnBytes, bnBytes.Length);
                sha3.FinalizeHash();
                byte[] computedHash = sha3.GetDigest();

                if (!Enumerable.SequenceEqual(computedHash, digest))
                {
                    OutPacket((ushort)Opcodes.SMSG_AUTH_RESPONSE, 1, [(byte)LoginErrorCode.AUTH_UNKNOWN_ACCOUNT]);
                    return;
                }
            }

            // Réinitialisation de m_fullAccountName après utilisation
            m_fullAccountName = null;

            m_session.AccountFlags = accountFlags;
            m_session.GMPermissions = gmFlags;
            m_session.AccountId = accountId;
            m_session.Latency = (uint)_latency;
            m_session.AccountName = accountName;
            m_session.ClientBuild = mClientBuild;
            m_session.Language = LanguageStringToId(lang);

            if (recvData.Size < recvData.Contents.Length)
                m_session.Muted = recvData.ReadByte();

            for (uint i = 0; i < 8; i++)
                m_session.SetAccountData(i, null, true, 0);

            CLog.Notice("Auth", R_N_WRDSOCK_1, accountName, GetRemoteIP(), GetRemotePort(), _latency);
            Authenticate();
        }

        private void Authenticate()
        {
            if (pAuthenticationPacket == null || m_session == null)
                return;

            byte[] authResponse;
            if (m_session.HasFlag(AccountFlagsEnum.ACCOUNT_FLAG_XPACK_02))
                authResponse = [(byte)0x0C, 0x30, 0x78, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02];
            else if (m_session.HasFlag(AccountFlagsEnum.ACCOUNT_FLAG_XPACK_01))
                authResponse = [(byte)0x0C, 0x30, 0x78, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01];
            else
                authResponse = [(byte)0x0C, 0x30, 0x78, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

            OutPacket((ushort)Opcodes.SMSG_AUTH_RESPONSE, authResponse.Length, authResponse);
            SendAddonInfoPacket(pAuthenticationPacket, (uint)pAuthenticationPacket.Rpos, m_session);
            m_session.Latency = (uint)_latency;
            pAuthenticationPacket = null; // Nettoyage
        }

        private static uint LanguageStringToId(string langCode)
        {
            return langCode switch
            {
                "enUS" => 0,
                "koKR" => 1,
                "frFR" => 2,
                "deDE" => 3,
                "zhCN" => 4,
                "zhTW" => 5,
                "esES" => 6,
                "esMX" => 7,
                "ruRU" => 8,
                "ptBR" => 9,
                "itIT" => 10,
                _ => 0
            };
        }

        private static void SendAddonInfoPacket(WorldPacket source, uint pos, Session session)
        {
            if (source == null || session == null)
                return;

            if (pos >= source.Size)
            {
                CLog.Debug("CMSG_AUTH_SESSION", $"SendAddonInfoPacket: invalid pos ({pos}) >= source.Size ({source.Size})");
                return;
            }

            source.Rpos = (int)pos;
            try
            {
                var returnPacket = new WorldPacket((ushort)Opcodes.SMSG_ADDON_INFO);
                uint realsize = source.ReadUInt32();
                if (realsize == 0 || realsize > 0xFFFFFF)
                {
                    CLog.Debug("CMSG_AUTH_SESSION", R_D_WRDSOCK_3);
                    return;
                }

                int remainingSize = source.Size - source.Rpos;
                byte[] addonData = new byte[remainingSize];
                source.Read(addonData, remainingSize);

                byte[] uncompressed = new byte[realsize];
                using (var ms = new MemoryStream(addonData))
                using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    int decompressedBytes = deflate.Read(uncompressed, 0, uncompressed.Length);
                    if (decompressedBytes != realsize)
                    {
                        CLog.Debug("CMSG_AUTH_SESSION", R_D_WRDSOCK_3_1);
                        return;
                    }
                }

                var addonBuffer = new ByteBuffer();
                addonBuffer.Append(uncompressed, uncompressed.Length);
                addonBuffer.Rpos = 0;
                uint addoncount = addonBuffer.Read<uint>();

                byte[] PublicKey = [
                    0x02, 0x01, 0x01, 0xC3, 0x5B, 0x50, 0x84, 0xB9, 0x3E, 0x32, 0x42, 0x8C, 0xD0, 0xC7, 0x48, 0xFA,
                    0x0E, 0x5D, 0x54, 0x5A, 0xA3, 0x0E, 0x14, 0xBA, 0x9E, 0x0D, 0xB9, 0x5D, 0x8B, 0xEE, 0xB6, 0x84,
                    0x93, 0x45, 0x75, 0xFF, 0x31, 0xFE, 0x2F, 0x64, 0x3F, 0x3D, 0x6D, 0x07, 0xD9, 0x44, 0x9B, 0x40,
                    0x85, 0x59, 0x34, 0x4E, 0x10, 0xE1, 0xE7, 0x43, 0x69, 0xEF, 0x7C, 0x16, 0xFC, 0xB4, 0xED, 0x1B,
                    0x95, 0x28, 0xA8, 0x23, 0x76, 0x51, 0x31, 0x57, 0x30, 0x2B, 0x79, 0x08, 0x50, 0x10, 0x1C, 0x4A,
                    0x1A, 0x2C, 0xC8, 0x8B, 0x8F, 0x05, 0x2D, 0x22, 0x3D, 0xDB, 0x5A, 0x24, 0x7A, 0x0F, 0x13, 0x50,
                    0x37, 0x8F, 0x5A, 0xCC, 0x9E, 0x04, 0x44, 0x0E, 0x87, 0x01, 0xD4, 0xA3, 0x15, 0x94, 0x16, 0x34,
                    0xC6, 0xC2, 0xC3, 0xFB, 0x49, 0xFE, 0xE1, 0xF9, 0xDA, 0x8C, 0x50, 0x3C, 0xBE, 0x2C, 0xBB, 0x57,
                    0xED, 0x46, 0xB9, 0xAD, 0x8B, 0xC6, 0xDF, 0x0E, 0xD6, 0x0F, 0xBE, 0x80, 0xB3, 0x8B, 0x1E, 0x77,
                    0xCF, 0xAD, 0x22, 0xCF, 0xB7, 0x4B, 0xCF, 0xFB, 0xF0, 0x6B, 0x11, 0x45, 0x2D, 0x7A, 0x81, 0x18,
                    0xF2, 0x92, 0x7E, 0x98, 0x56, 0x5D, 0x5E, 0x69, 0x72, 0x0A, 0x0D, 0x03, 0x0A, 0x85, 0xA2, 0x85,
                    0x9C, 0xCB, 0xFB, 0x56, 0x6E, 0x8F, 0x44, 0xBB, 0x8F, 0x02, 0x22, 0x68, 0x63, 0x97, 0xBC, 0x85,
                    0xBA, 0xA8, 0xF7, 0xB5, 0x40, 0x68, 0x3C, 0x77, 0x86, 0x6F, 0x4B, 0xD7, 0x88, 0xCA, 0x8A, 0xD7,
                    0xCE, 0x36, 0xF0, 0x45, 0x6E, 0xD5, 0x64, 0x79, 0x0F, 0x17, 0xFC, 0x64, 0xDD, 0x10, 0x6F, 0xF3,
                    0xF5, 0xE0, 0xA6, 0xC3, 0xFB, 0x1B, 0x8C, 0x29, 0xEF, 0x8E, 0xE5, 0x34, 0xCB, 0xD1, 0x2A, 0xCE,
                    0x79, 0xC3, 0x9A, 0x0D, 0x36, 0xEA, 0x01, 0xE0, 0xAA, 0x91, 0x20, 0x54, 0xF0, 0x72, 0xD8, 0x1E,
                    0xC7, 0x89, 0xD2, 0x00, 0x00, 0x00, 0x00, 0x00
                ];

                for (uint i = 0; i < addoncount && addonBuffer.Rpos < addonBuffer.Size; ++i)
                {
                    string addonName = addonBuffer.Read<string>();
                    byte enable = addonBuffer.Read<byte>();
                    uint crc = addonBuffer.Read<uint>();
                    uint unknown = addonBuffer.Read<uint>();

                    byte unk = (byte)(enable != 0 ? 2 : 1);
                    returnPacket.Append(unk);
                    byte unk1 = (byte)(enable != 0 ? 1 : 0);
                    returnPacket.Append(unk1);

                    if (unk1 != 0)
                    {
                        if (crc != 0x4C1C776D)
                        {
                            returnPacket.Append((byte)1);
                            returnPacket.Append(PublicKey, PublicKey.Length);
                        }
                        else
                            returnPacket.Append((byte)0);

                        returnPacket.Append((uint)0);
                    }

                    byte unk2 = (byte)(enable != 0 ? 0 : 1);
                    returnPacket.Append(unk2);
                    if (unk2 != 0)
                        returnPacket.Append((byte)0);
                }

                returnPacket.Append((uint)0);
                session.SendPacket(returnPacket);
            }
            catch (Exception ex)
            {
                CLog.Error("CMSG_AUTH_SESSION", R_E_WRDSOCK_4, ex.Message);
            }
        }

        public void OutPacket(ushort opcode, int len, byte[] data)
        {
            if ((len + 10) > SendBufferSize)
            {
                Logger.OutColor(LogColor.TRED, "[WorldSocket]", R_W_WRDSOCK, len, opcode);
                return;
            }

            var res = OutPacketInternal(opcode, len, data);
            if (res == OutPacketResult.Success)
                UpdateQueuedPackets();
            else if (res == OutPacketResult.NoRoomInBuffer)
            {
                lock (queueLock)
                {
                    var queued = new WorldPacket(opcode, len > 0 ? len : 0);
                    if (len > 0 && data != null)
                        queued.Write(data, 0, len);
                    _queue.Push(queued);
                }
            }
        }

        private OutPacketResult OutPacketInternal(ushort opcode, int len, byte[] data)
        {
            if (!IsConnected())
                return OutPacketResult.NotConnected;

            BurstBegin();
            if (GetWriteBuffer().GetSize() < (len + 4))
            {
                BurstEnd();
                return OutPacketResult.NoRoomInBuffer;
            }

            var header = new ServerPktHeader { cmd = opcode, size = (ushort)IPAddress.HostToNetworkOrder((short)(len + 2)) };
            var headerBytes = header.ToBytes();
            try { _crypt?.Encrypt(headerBytes); }
            catch (InvalidOperationException) { /* Crypt not initialized */ }

            if (!BurstSend(headerBytes, 4))
            {
                BurstEnd();
                return OutPacketResult.SocketError;
            }

            if (len > 0 && data != null && !BurstSend(data, len))
            {
                BurstEnd();
                return OutPacketResult.SocketError;
            }

            BurstPush();
            BurstEnd();
            return OutPacketResult.Success;
        }

        public override void OnConnectVirtual()
        {
            _latency = Environment.TickCount;
            var data = new WorldPacket((ushort)Opcodes.SMSG_AUTH_CHALLENGE, 25);
            data.WriteUInt32(1);
            data.WriteUInt32(mSeed);
            data.WriteUInt32(0xF3539DA3);
            data.WriteUInt32(0x6E8547B9);
            data.WriteUInt32(0x9A6AA2F8);
            data.WriteUInt32(0xA4F170F4);
            OutPacket(data.GetOpcode(), data.Size, data.Contents);
        }

        private void HandleAuthSession(WorldPacket recvPacket)
        {
            if (recvPacket == null)
                return;

            try
            {
                _latency = Environment.TickCount - _latency;
                mClientBuild = recvPacket.ReadUInt32();
                uint unk1 = recvPacket.ReadUInt32();
                string account = recvPacket.ReadString();
                uint unk2 = recvPacket.ReadUInt32();
                mClientSeed = recvPacket.ReadUInt32();
                ulong unk3 = recvPacket.ReadUInt64();
                uint unk4 = recvPacket.ReadUInt32();
                uint unk5 = recvPacket.ReadUInt32();
                uint unk6 = recvPacket.ReadUInt32();

                mRequestID = LogonCommHandler.Instance.ClientConnected(account, this);
                if (mRequestID == 0xFFFFFFFF)
                {
                    Disconnect();
                    return;
                }

                m_fullAccountName = account;
                pAuthenticationPacket = recvPacket;
            }
            catch (Exception ex)
            {
                CLog.Error("[WorldSocket]", R_E_WRDSOCK_1, ex.Message);
                Disconnect();
            }
        }

        private void UpdateQueuedPackets()
        {
            lock (queueLock)
            {
                if (!_queue.HasItems())
                    return;

                while (_queue.HasItems())
                {
                    WorldPacket packet = _queue.Front();
                    if (packet == null)
                        break;

                    OutPacketResult res = OutPacketInternal(packet.GetOpcode(), packet.Size, packet.Contents);
                    if (res == OutPacketResult.Success)
                        _queue.PopFront();
                    else if (res == OutPacketResult.NoRoomInBuffer)
                    {
                        CLog.Debug("[WorldSocket]", R_D_WRDSOCK_5);
                        return;
                    }
                    else
                    {
                        CLog.Error("[WorldSocket]", R_E_WRDSOCK_5);
                        _queue.Clear();
                        return;
                    }
                }
            }
        }

        public void UpdateQueuePosition(uint position)
        {
            try
            {
                var queuePacket = new WorldPacket((ushort)Opcodes.SMSG_AUTH_RESPONSE, 15);
                queuePacket.WriteByte(0x1B);
                queuePacket.WriteByte(0x2C);
                queuePacket.WriteByte(0x73);
                queuePacket.WriteByte(0x00);
                queuePacket.WriteByte(0x00);
                queuePacket.WriteUInt32(0);
                queuePacket.WriteByte(0x00);
                queuePacket.WriteUInt32(position);
                OutPacket(queuePacket.GetOpcode(), queuePacket.Size, queuePacket.Contents);
            }
            catch (Exception ex)
            {
                CLog.Error("[WorldSocket]", $"UpdateQueuePosition error: {ex.Message}");
            }
        }

        private void HandlePing(WorldPacket recvPacket)
        {
            if (recvPacket == null || recvPacket.Size < 4)
            {
                CLog.Debug("[WorldSocket]", "Ping packet too small");
                Disconnect();
                return;
            }

            try
            {
                uint ping = recvPacket.ReadUInt32();
                _latency = (int)recvPacket.ReadUInt32();
                OutPacket((ushort)Opcodes.SMSG_PONG, 4, BitConverter.GetBytes(ping));

                var socket = GetFd();
                if (socket != null)
                {
                    socket.NoDelay = _latency >= 250;
                    string nagleStatus = socket.NoDelay ? "disabled" : "enabled";
                    CLog.Debug("[WorldSocket]", $"Nagle {nagleStatus} (latency: {_latency}ms)");
                }
            }
            catch (Exception ex)
            {
                CLog.Error("[WorldSocket]", $"HandlePing error: {ex.Message}");
            }
        }

        public override void OnRead()
        {
            for (; ; )
            {
                if (mRemaining == 0)
                {
                    if (GetReadBuffer().GetSize() < 6)
                        return;

                    ClientPktHeader header;
                    byte[] headerBytes = new byte[6];
                    GetReadBuffer().Read(headerBytes, 6);

                    try { _crypt?.Decrypt(headerBytes); }
                    catch (InvalidOperationException) { /* Crypt not initialized */ }

                    // Conversion depuis le format réseau (big-endian)
                    header.size = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(headerBytes, 0));
                    header.cmd = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(headerBytes, 2));

                    mRemaining = mSize = (int)header.size - 4; // Taille du payload (sans l'opcode)
                    mOpcode = (int)header.cmd;
                }

                if (mRemaining > 0 && GetReadBuffer().GetSize() < mRemaining)
                    return;

                var packet = new WorldPacket((ushort)mOpcode, mSize);
                if (mRemaining > 0)
                {
                    byte[] data = new byte[mRemaining];
                    GetReadBuffer().Read(data, mRemaining);
                    packet.Write(data, 0, mRemaining);
                }

                mRemaining = mSize = mOpcode = 0;
                CLog.Debug("[WorldSocket]", $"OnRead: Opcode={packet.GetOpcode()}");

                switch (packet.GetOpcode())
                {
                    case (ushort)Opcodes.CMSG_PING:
                        HandlePing(packet);
                        break;
                    case (ushort)Opcodes.CMSG_AUTH_SESSION:
                        HandleAuthSession(packet);
                        break;
                    default:
                        if (m_session != null)
                            m_session.QueuePacket(packet);
                        else
                            CLog.Error("[WorldSocket]", $"OnRead: Received non-auth packet {packet.GetOpcode()} before authentication");
                        break;
                }
            }
        }
    }
}
