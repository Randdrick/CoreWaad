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
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;

using WaadShared;
using WaadShared.AuthCodes;
using WaadShared.Database;

using static DBCStores;
using static WaadShared.CharacterHandler;

namespace WaadRealmServer
{
    // Utilitaires
    public static class Utils
    {
        public static long UNIXTIME => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static uint GUID_LOPART(ulong guid) => (uint)(guid & 0xFFFFFFFF);

        /// Vérifie si la taille du paquet est égale à la taille attendue.
        public static bool CheckPacketSize(WorldPacket packet, int expectedSize, Session session)
        {
            if (packet.Size != expectedSize)
            {
                session.Disconnect();
                return false;
            }
            return true;
        }
    }

    // Accès base de données
    public static class CharacterDatabase
    {
        public static QueryResult Query(string sql) => RealmDatabaseManager.GetDatabase()?.Query(sql);
        public static QueryResult Query(string sql, params object[] args) => RealmDatabaseManager.GetDatabase()?.Query(sql, args);
    }
    public static class WorldDatabase
    {
        public static QueryResult Query(string sql) => RealmDatabaseManager.GetDatabase()?.Query(sql);
        public static QueryResult Query(string sql, params object[] args) => RealmDatabaseManager.GetDatabase()?.Query(sql, args);
    }

    public static class InventorySlots
    {
        public const int EQUIPMENT_SLOT_START = 0;
        public const int EQUIPMENT_SLOT_HEAD = 0;
        public const int EQUIPMENT_SLOT_NECK = 1;
        public const int EQUIPMENT_SLOT_SHOULDERS = 2;
        public const int EQUIPMENT_SLOT_BODY = 3;
        public const int EQUIPMENT_SLOT_CHEST = 4;
        public const int EQUIPMENT_SLOT_WAIST = 5;
        public const int EQUIPMENT_SLOT_LEGS = 6;
        public const int EQUIPMENT_SLOT_FEET = 7;
        public const int EQUIPMENT_SLOT_WRISTS = 8;
        public const int EQUIPMENT_SLOT_HANDS = 9;
        public const int EQUIPMENT_SLOT_FINGER1 = 10;
        public const int EQUIPMENT_SLOT_FINGER2 = 11;
        public const int EQUIPMENT_SLOT_TRINKET1 = 12;
        public const int EQUIPMENT_SLOT_TRINKET2 = 13;
        public const int EQUIPMENT_SLOT_BACK = 14;
        public const int EQUIPMENT_SLOT_MAINHAND = 15;
        public const int EQUIPMENT_SLOT_OFFHAND = 16;
        public const int EQUIPMENT_SLOT_RANGED = 17;
        public const int EQUIPMENT_SLOT_TABARD = 18;
        public const int EQUIPMENT_SLOT_END = 19;
        public const int INVENTORY_SLOT_BAG_START = 19;
        public const int INVENTORY_SLOT_BAG_1 = 19;
        public const int INVENTORY_SLOT_BAG_2 = 20;
        public const int INVENTORY_SLOT_BAG_3 = 21;
        public const int INVENTORY_SLOT_BAG_4 = 22;
        public const int INVENTORY_SLOT_BAG_END = 23;
    }


    [Flags]
    public enum PlayerFlags : uint
    {
        NOHELM = 0x000400,
        NOCLOAK = 0x000800
    }

    public class PlayerItem
    {
        public uint DisplayId { get; set; }
        public byte InvType { get; set; }
        public uint Enchantment { get; set; } // added in 2.4
    }

    public partial class Session
    {
        private static ClusterMgr sClusterMgr = ClusterMgr.Instance;
        private static ClientMgr sClientMgr = ClientMgr.Instance;
        public WorkerServerSocket Wss { get; set; }
        public WorldSocket Ws { get; set; }
        public static DBCStorage<EnchantEntry> DbcEnchant { get; } = new DBCStorage<EnchantEntry>();

        #region Classe de données pour le personnage
        private class CharacterData(QueryResult result)
        {
            public ulong Guid { get; } = Convert.ToUInt64(result.GetValue(0));
            public string Name { get; } = result.GetValue(1)?.ToString() ?? "";
            public uint MapId { get; } = Convert.ToUInt32(result.GetValue(6));
            public float PositionX { get; } = Convert.ToSingle(result.GetValue(7));
            public float PositionY { get; } = Convert.ToSingle(result.GetValue(8));
            public float PositionZ { get; } = Convert.ToSingle(result.GetValue(9));
            public uint Banned { get; } = Convert.ToUInt32(result.GetValue(11));
            public uint RecoveryMapId { get; } = Convert.ToUInt32(result.GetValue(15));
            public float RecoveryPositionX { get; } = Convert.ToSingle(result.GetValue(16));
            public float RecoveryPositionY { get; } = Convert.ToSingle(result.GetValue(17));
            public float RecoveryPositionZ { get; } = Convert.ToSingle(result.GetValue(18));
        }
        #endregion

        #region HandleCharacterEnum
        public static void HandleCharacterEnum(QueryResult result)
        {
            var items = new PlayerItem[InventorySlots.INVENTORY_SLOT_BAG_END];
            uint levelMaxFound = 0;
            int side = -1;
            // Cache all rows for safe buffer allocation and processing
            var rows = new List<object[]>();
            if (result != null)
            {
                int colCount = 19; // Number of columns expected per row
                while (result.NextRow())
                {
                    var row = new object[colCount];
                    for (int i = 0; i < colCount; i++)
                        row[i] = result.GetValue(i);
                    rows.Add(row);
                }
            }
            int rowCount = rows.Count;
            int bufferSize = Math.Max(256, rowCount * 256);
            var data = new WorldPacket((ushort)Opcodes.SMSG_CHAR_ENUM, bufferSize);
            uint numChar = 0;
            foreach (var values in rows)
            {
                numChar++;
                ulong charGuid = (ulong)values[0];
                uint bytes2 = (uint)values[6];
                byte classId = (byte)values[3];
                uint flags = (uint)values[17];
                byte race = (byte)values[2];

                if (side < 0)
                {
                    byte[] sides = [0, 0, 1, 0, 0, 1, 1, 0, 1, 0, 1, 0];
                    side = sides[race];
                }

                data.WriteUInt64(charGuid); // guid
                data.WriteString(values[7]?.ToString() ?? ""); // name
                data.WriteByte(race); // race
                data.WriteByte(classId); // class
                data.WriteByte((byte)values[4]); // gender
                data.WriteUInt32((uint)values[5]); // PLAYER_BYTES
                data.WriteByte((byte)(bytes2 & 0xFF)); // facial hair
                uint levelFound = (byte)values[1];
                data.WriteByte((byte)levelFound);
                data.WriteUInt32((uint)values[12]); // zoneid
                data.WriteUInt32((uint)values[11]); // Mapid
                data.WriteFloat(Convert.ToSingle(values[8]));
                data.WriteFloat(Convert.ToSingle(values[9]));
                data.WriteFloat(Convert.ToSingle(values[10]));
                data.WriteUInt32((uint)values[18]); // GuildID

                uint banned = (uint)values[13];
                if (banned != 0 && (banned < 10 || banned > (uint)Utils.UNIXTIME))
                    data.WriteUInt32(0x01A04040);
                else if ((uint)values[16] != 0)
                    data.WriteUInt32(0x00A04342);
                else if ((uint)values[15] != 0)
                    data.WriteUInt32(8704); // Dead (displaying as Ghost)
                else
                    data.WriteUInt32(1); // alive

                data.WriteUInt32(0); // Added in 3.0.2
                data.WriteByte(0);   // Added in 3.2.0

                QueryResult infos = null;
                QueryResult res = null;
                if (classId == (byte)Classes.WARLOCK || classId == (byte)Classes.HUNTER)
                {
                    res = CharacterDatabase.Query($"SELECT entry FROM playerpets WHERE ownerguid={Utils.GUID_LOPART(charGuid)} AND (active % 10) = 1");
                    if (res != null && res.NextRow())
                    {
                        uint entry = (uint)res.GetValue(0);
                        infos = WorldDatabase.Query($"SELECT * FROM creature_names WHERE entry={entry}");
                    }
                }
                if (infos != null && infos.NextRow())
                {
                    data.WriteUInt32((uint)infos.GetValue(10)); // male_DisplayID
                    data.WriteUInt32(10); // level
                    data.WriteUInt32((uint)infos.GetValue(6)); // family
                }
                else
                {
                    data.WriteUInt32(0);
                    data.WriteUInt32(0);
                    data.WriteUInt32(0);
                }

                res = CharacterDatabase.Query($"SELECT containerslot, slot, entry, enchantments FROM playeritems WHERE ownerguid={Utils.GUID_LOPART(charGuid)}");
                if (levelFound > levelMaxFound) levelMaxFound = levelFound;
                ulong dkGuid;
                if (classId == (byte)Classes.DEATHKNIGHT) dkGuid = charGuid;

                uint enchantid = 0;
                Array.Clear(items, 0, items.Length);
                if (res != null)
                {
                    while (res.NextRow())
                    {
                        int containerslot = Convert.ToInt32(res.GetValue(0));
                        int slot = Convert.ToInt32(res.GetValue(1));
                        if (containerslot == -1 && slot < InventorySlots.EQUIPMENT_SLOT_END && slot >= InventorySlots.EQUIPMENT_SLOT_START)
                        {
                            var proto = Storage.ItemPrototypeStorage.LookupEntry((int)Convert.ToUInt32(res.GetValue(2)));
                            if (proto != null)
                            {
                                if (!(slot == InventorySlots.EQUIPMENT_SLOT_HEAD && (flags & (uint)PlayerFlags.NOHELM) != 0) &&
                                    !(slot == InventorySlots.EQUIPMENT_SLOT_BACK && (flags & (uint)PlayerFlags.NOCLOAK) != 0))
                                {
                                    if (items[slot] == null) items[slot] = new PlayerItem();
                                    items[slot].DisplayId = proto.DisplayInfoID;
                                    items[slot].InvType = (byte)proto.InventoryType;
                                    if (slot == InventorySlots.EQUIPMENT_SLOT_MAINHAND || slot == InventorySlots.EQUIPMENT_SLOT_OFFHAND)
                                    {
                                        string enchantField = res.GetValue(3)?.ToString() ?? "";
                                        if (uint.TryParse(enchantField.Split(',')[0], out enchantid) && enchantid > 0)
                                        {
                                            var enc = DbcEnchant.LookupEntry(enchantid);
                                            items[slot].Enchantment = enc.Id > 0 ? enc.visual : 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                for (int i = 0; i < InventorySlots.INVENTORY_SLOT_BAG_END; ++i)
                {
                    data.WriteUInt32(items[i]?.DisplayId ?? 0);
                    data.WriteByte(items[i]?.InvType ?? 0);
                    data.WriteUInt32(items[i]?.Enchantment ?? 0);
                }
            }
        }

        private void HandleCharEnumOpcode(WorldPacket p)
        {
            // Equivalent to C++: Async query for character enumeration
            uint accountId = GetAccountId();
            string sql = $"SELECT guid, level, race, class, gender, bytes, bytes2, name, positionX, positionY, positionZ, mapId, zoneId, banned, restState, deathstate, forced_rename_pending, player_flags, guild_data.guildid FROM characters LEFT JOIN guild_data ON characters.guid = guild_data.playerid WHERE acct={accountId} ORDER BY guid ASC LIMIT 10";
            QueryResult result = CharacterDatabase.Query(sql);
            HandleCharacterEnum(result);
        }
        #endregion

        #region HandleCharacter Create - Delete - Rename
        private void HandleCharacterCreate(WorldPacket p)
        {
            // Parse packet (name, race, class)
            // Reset packet read position if needed (stub, not implemented)
            string name = p.ReadString();
            byte race = p.ReadByte();
            byte classId = p.ReadByte();
            var sLog = new Logger();

            // Vérifier la taille du paquet (doit être 10 octets pour un Personnage)
            var SizeOfCharacterCreatePacket = 10;
            if (!Utils.CheckPacketSize(p, SizeOfCharacterCreatePacket, this))
            {
                sLog.OutError("CMSG_CHAR_CREATE", R_E_CHARHAN_COM, p.Size, SizeOfCharacterCreatePacket);
                return;
            }

            // Log
            CLog.Debug("CMSG_CHAR_CREATE: name {0}, race {1}, class {2}", name, race, classId);

            // Name validation (stub)
            if (!VerifyName(name))
            {
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_NAME_FAILURE]); // 0x5B (Caractère invalide)
                sLog.OutDetail("CMSG_CHAR_CREATE: CHAR_NAME_FAILURE");
                return;
            }

            // Check if name is already used
            if (sClientMgr.GetRPlayer(name) != null)
            {
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_NAME_IN_USE]); // 0x32
                sLog.OutDetail("CMSG_CHAR_CREATE: CHAR_CREATE_IN_USE");
                return;
            }

            // Check banned names
            var result = CharacterDatabase.Query($"SELECT COUNT(*) FROM banned_names WHERE name = '{name.Replace("'", "''")}'");
            if (result != null && result.NextRow() && Convert.ToUInt32(result.GetValue(0)) > 0)
            {
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_NAME_IN_USE]); // 0x32
                sLog.OutDetail("CMSG_CHAR_CREATE: CHAR_CREATE_IN_USE (banned)");
                return;
            }

            // Expansion flags check (Blood Elf, Death Knight)
            if (race == 10 /*RACE_BLOODELF*/ && (m_accountFlags & (uint)AccountFlagsEnum.ACCOUNT_FLAG_XPACK_01) == 0)
            {
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_ONLY_EXISTING]); // 0x38
                sLog.OutDetail("CMSG_CHAR_CREATE: CHAR_CREATE_ONLY_EXISTING 1");
                return;
            }
            if (classId == 6 /*DEATHKNIGHT*/)
            {
                // Vérifie l'extension WotLK
                if ((m_accountFlags & (uint)AccountFlagsEnum.ACCOUNT_FLAG_XPACK_02) == 0)
                {
                    Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_ONLY_EXISTING]); // 0x38
                    sLog.OutDetail("CMSG_CHAR_CREATE: CHAR_CREATE_ONLY_EXISTING 2");
                    return;
                }

                // Vérifie les prérequis DK
                var reqResult = CharacterDatabase.Query($"SELECT * FROM _blizzrequirements WHERE acct_id = {GetAccountId()}");
                if (reqResult == null || !reqResult.NextRow())
                {
                    CLog.Error("[Blizz Prerequis]", R_E_CHARHAN_BR, GetAccountId());
                    Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_ERROR]); // 0x30
                    return;
                }

                _ = Convert.ToUInt32(reqResult.GetValue(0));
                var dkGuid = Convert.ToUInt32(reqResult.GetValue(1));
                var maxLevelPlayerGet = Convert.ToUInt32(reqResult.GetValue(2));

                // Un seul DK par compte
                if (dkGuid != 0)
                {
                    CLog.Notice("[Blizz Prerequis]", R_N_CHARHAN_BR, GetAccountName());
                    Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_UNIQUE_CLASS_LIMIT]); // 0x3C
                    return;
                }
                // Niveau minimum requis
                if (maxLevelPlayerGet < 55)
                {
                    CLog.Notice("[Blizz Prerequis]", R_N_CHARHAN_BR_1, GetAccountName());
                    Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_LEVEL_REQUIREMENT]); // 0x3B
                    return;
                }
            }

            // Get any instance (stub)
            var instance = sClusterMgr.GetAnyInstance();
            if (instance == null)
            {
                sLog.OutError("CHAR_CREATE", "No available instance");
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_FAILED]); // 0x31
                return;
            }

            if (GetAccountId() != 0)
            {
                // Build and distribute creation packet
                var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_CREATE_PLAYER, 10 + p.Size);
                var sLogonCommHandler = new LogonCommHandler();
                data.WriteUInt32(GetAccountId());
                data.WriteUInt16(p.Opcode);
                data.WriteUInt32((uint)p.Size);
                data.WriteBytes(p.ToArray()); // Use ToArray() for packet bytes
                sClusterMgr.DistributePacketToAll(data);
                sLogonCommHandler.UpdateAccountCount(GetAccountId(), 1);
            }
            else
            {
                CLog.Warning("CHAR_CREATE", R_E_CHARHAN_CC_1, GetAccountId());
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_CREATE, 1, [(byte)LoginErrorCode.CHAR_CREATE_NAME_IN_USE]); // 0x32
                return;
            }
        } 
        private bool VerifyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length < 3 || name.Length > 12)
                return false;
            return AscentGetOpt.VerifyName(name, true);
        }
        private void HandleCharacterDelete(WorldPacket p)
        {
            var sLog = new Logger();

            // Vérifier la taille du paquet (doit être 8 octets pour un GUID)
            var SizeOfGuidPacket = 8;
            if (!Utils.CheckPacketSize(p, SizeOfGuidPacket, this))
            {
                sLog.OutError("CHAR_DELETE", R_E_CHARHAN_COM, p.Size, SizeOfGuidPacket);
                return;
            }

            ulong guid = p.ReadUInt64();
            sLog.OutDebug("CHAR_DELETE", R_D_CHARHAN_CD, p.Opcode);

            // Récupérer les informations du personnage pour les logs
            m_currentPlayer = sClientMgr.CreateRPlayer(Utils.GUID_LOPART(guid));
            RPlayerInfo info = m_currentPlayer;

            sLog.OutDebug("CHAR_DELETE", R_D_CHARHAN_CD_1, info?.Guid);
            sLog.OutDebug("CHAR_DELETE", R_D_CHARHAN_CD_2, info?.AccountId);
            sLog.OutDebug("CHAR_DELETE", R_D_CHARHAN_CD_3, GetAccountId());
            sLog.OutDebug("CHAR_DELETE", R_D_CHARHAN_CD_4, info?.References);

            // 1. Vérifier que le personnage appartient bien au compte
            var result = CharacterDatabase.Query(
                "SELECT name FROM characters WHERE guid = {0} AND acct = {1}",
                Utils.GUID_LOPART(guid),
                GetAccountId()
            );
            if (result == null || !result.NextRow())
            {
                CLog.Warning("CHAR_DELETE", R_W_CHARHAN_CD, guid, GetAccountId());
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_DELETE, 1, [(byte)LoginErrorCode.CHAR_DELETE_FAILED]); // 0x48
                return;
            }
            string name = result.GetValue(0)?.ToString() ?? "";

            // 2. Vérifier si le personnage est chef de guilde
            var guildResult = CharacterDatabase.Query(
                "SELECT leaderGuid FROM guilds WHERE leaderGuid = {0}",
                Utils.GUID_LOPART(guid)
            );
            if (guildResult != null && guildResult.NextRow())
            {
                CLog.Warning("CHAR_DELETE", R_W_CHARHAN_CD_1, name);
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_DELETE, 1, [(byte)LoginErrorCode.CHAR_DELETE_FAILED_GUILD_LEADER]); // 0x4A
                return;
            }

            // 3. Vérifier si le personnage est chef d'équipe d'arène
            var arenaResult = CharacterDatabase.Query(
                "SELECT leader FROM arenateams WHERE id = {0}",
                Utils.GUID_LOPART(guid)
            );
            if (arenaResult != null && arenaResult.NextRow())
            {
                CLog.Warning("CHAR_DELETE", R_W_CHARHAN_CD_2, name);
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_DELETE, 1, [(byte)LoginErrorCode.CHAR_DELETE_FAILED_ARENA_CAPTAIN]); // 0x4B
                return;
            }

            // 4. Récupérer une instance pour envoyer la demande de suppression
            var instance = sClusterMgr.GetAnyInstance();
            if (instance == null)
            {
                CLog.Error("CHAR_DELETE", R_W_CHARHAN_CD_3);
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_DELETE, 1, [(byte)LoginErrorCode.CHAR_DELETE_FAILED]); // 0x48
                return;
            }

            // 5. Envoyer la demande de suppression au serveur de monde
            var data = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_DELETE_PLAYER, 8);
            data.WriteUInt64(guid);
            instance.ServerSocket.SendPacket(data);

            // 6. Supprimer les données du personnage de toutes les tables (en transaction si possible)
            uint lowGuid = Utils.GUID_LOPART(guid);
            try
            {
                // Utilisation de requêtes paramétrées pour éviter les injections SQL
                CharacterDatabase.Query("DELETE FROM characters WHERE guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM achievements WHERE player = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM auctions WHERE owner = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM charters WHERE leaderGuid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM gm_tickets WHERE guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM guild_data WHERE playerid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM instances WHERE creator_guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM mailbox WHERE player_guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playercooldowns WHERE player_guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playerglyphs WHERE guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playeritems WHERE ownerguid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playerpets WHERE ownerguid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playerpetspells WHERE ownerguid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playerpettalents WHERE ownerguid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playerskills WHERE player_guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playerspells WHERE guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playersummonspells WHERE ownerguid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM playertalents WHERE guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM questlog WHERE player_guid = {0}", lowGuid);
                CharacterDatabase.Query("DELETE FROM social_friends WHERE character_guid = {0} OR friend_guid = {0}", lowGuid, lowGuid);
                CharacterDatabase.Query("DELETE FROM social_ignores WHERE character_guid = {0} OR ignore_guid = {0}", lowGuid, lowGuid);
                CharacterDatabase.Query("DELETE FROM tutorials WHERE playerId = {0}", lowGuid);
            }
            catch (Exception ex)
            {
                CLog.Error("CHAR_DELETE", R_W_CHARHAN_CD_4, name, ex.Message);
                Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_DELETE, 1, [(byte)LoginErrorCode.CHAR_DELETE_FAILED]); // 0x48
                return;
            }

            // 7. Supprimer le personnage du gestionnaire de clients
            sClientMgr.DestroyRPlayerInfo(lowGuid);

            // 8. Envoyer un paquet de succès au client
            Ws.OutPacket((ushort)Opcodes.SMSG_CHAR_DELETE, 1, [(byte)LoginErrorCode.CHAR_DELETE_SUCCESS]); // 0x47
        }

        private void HandleCharacterRename(WorldPacket p)
        {
            var sLog = new Logger();

            // 1. Vérification de la taille minimale du paquet (8 octets pour le GUID + au moins 3 octets pour le nom)
            var SizeOfMinimumPacket = 11; // 8 + 3
            if (p.Size < SizeOfMinimumPacket) // 8 (GUID) + 3 (nom minimum)
            {
                sLog.OutError("CHAR_RENAME", R_E_CHARHAN_COM,  p.Size, SizeOfMinimumPacket);
                Disconnect();
                return;
            }

            // 2. Lecture sécurisée du GUID et du nouveau nom
            ulong guid;
            try
            {
                guid = p.ReadUInt64();
            }
            catch (Exception ex)
            {
                sLog.OutError("CHAR_RENAME", R_E_CHARHAN_COM_1, ex.Message);
                Disconnect();
                return;
            }

            string newName = p.ReadString();
            if (string.IsNullOrEmpty(newName))
            {
                sLog.OutError("CHAR_RENAME", R_W_CHARHAN_CR_4);
                Disconnect();
                return;
            }

            sLog.OutDebug("CHAR_RENAME", R_D_CHARHAN_CR, p.Opcode);
            sLog.OutDebug("CHAR_RENAME", R_D_CHARHAN_CR_1, guid);
            sLog.OutDebug("CHAR_RENAME", R_D_CHARHAN_CR_2, newName);

            // 3. Vérification de l'appartenance du personnage au compte (requête paramétrée)
            var result = CharacterDatabase.Query(
                "SELECT forced_rename_pending FROM characters WHERE guid = @guid AND acct = @accountId",
                new { guid = Utils.GUID_LOPART(guid), accountId = GetAccountId() }
            );
            if (result == null || !result.NextRow())
            {
                sLog.OutWarning("CHAR_RENAME", R_W_CHARHAN_CR, guid, GetAccountId());
                SendRenameResponse((byte)LoginErrorCode.CHAR_NAME_FAILURE, guid, newName);
                return;
            }

            // 4. Validation stricte du nom
            byte errorCode = ValidateCharacterName(newName);
            if (errorCode != (byte)LoginErrorCode.RESPONSE_SUCCESS) //0x00
            {
                sLog.OutWarning("CHAR_RENAME", R_W_CHARHAN_CR_1, newName, errorCode);
                SendRenameResponse(errorCode, guid, newName);
                return;
            }

            // 5. Vérification des noms interdits (requête paramétrée)
            var bannedResult = CharacterDatabase.Query(
                "SELECT COUNT(*) FROM banned_names WHERE name = @name",
                new { name = newName }
            );
            if (bannedResult != null && bannedResult.NextRow() && Convert.ToUInt32(bannedResult.GetValue(0)) > 0)
            {
                sLog.OutWarning("CHAR_RENAME", R_W_CHARHAN_CR_2, newName);
                SendRenameResponse((byte)LoginErrorCode.CHAR_NAME_PROFANE, guid, newName); //0x5E
                return;
            }

            // 6. Vérification de la disponibilité du nom
            if (sClientMgr.GetRPlayer(newName) != null)
            {
                sLog.OutWarning("CHAR_RENAME", R_W_CHARHAN_CR_3, newName);
                SendRenameResponse((byte)LoginErrorCode.CHAR_CREATE_NAME_IN_USE, guid, newName); //0x32
                return;
            }

            // 7. Mise à jour sécurisée du nom dans la base de données (transaction + requête paramétrée)
            try
            {
                // Mise à jour du nom
                CharacterDatabase.Query(
                    "UPDATE characters SET name = @newName, forced_rename_pending = 0 WHERE guid = @guid AND acct = @accountId",
                    new { newName, guid = Utils.GUID_LOPART(guid), accountId = GetAccountId() }
                );

                // Mise à jour du nom dans le gestionnaire de clients
                RPlayerInfo playerInfo = sClientMgr.GetRPlayer(Utils.GUID_LOPART(guid));
                if (playerInfo != null)
                {
                    playerInfo.Name = newName;
                }

                // 8. Envoi d'un paquet de succès
                SendRenameResponse((byte)LoginErrorCode.RESPONSE_SUCCESS, guid, newName); //0x00
                sLog.OutDetail("CHAR_RENAME", R_N_CHARHAN_CR, guid, newName);

            }
            catch (Exception ex)
            {
                sLog.OutError("CHAR_RENAME", R_E_CHARHAN_CR, guid, ex.Message);
                SendRenameResponse((byte)LoginErrorCode.CHAR_CREATE_NAME_IN_USE, guid, newName); //0x32
            }
        }

        // Méthode utilitaire pour valider le nom d'un personnage
        private byte ValidateCharacterName(string name)
        {
            // Longueur minimale et maximale
            if (name.Length < 3)
                return (byte)LoginErrorCode.CHAR_NAME_TOO_SHORT;
            if (name.Length > 12)
                return (byte)LoginErrorCode.CHAR_NAME_TOO_LONG;

            if (!VerifyName(name))
            {
                return (byte)LoginErrorCode.CHAR_NAME_INVALID_CHARACTER; // 0x5C (caractère invalide)
            }

            return (byte)LoginErrorCode.RESPONSE_SUCCESS; // 0x00
        }

        // Méthode utilitaire pour envoyer la réponse de renommage
        private void SendRenameResponse(byte errorCode, ulong guid, string newName)
        {
            var response = new WorldPacket((ushort)Opcodes.SMSG_CHAR_RENAME, 1 + 8 + Encoding.UTF8.GetByteCount(newName) + 1);
            response.WriteByte(errorCode);
            response.WriteUInt64(guid);
            response.WriteString(newName);
            Wss.SendPacket(response);
        }
        #endregion
        private void HandlePlayerLogin(WorldPacket p)
        {
            var sLog = new Logger();

            // 1. Vérification de la taille minimale du paquet
            if (!CheckPacketSize(p, 8, sLog))
                return;

            // 2. Lecture du GUID du personnage
            if (!TryReadPlayerGuid(p, out ulong playerGuid, sLog))
                return;

            sLog.OutDebug("PLAYER_LOGIN", R_D_CHARHAN_COM, playerGuid);

            // 3. Vérification si le personnage est déjà connecté
            if (IsPlayerAlreadyConnected(playerGuid, sLog))
                return;

            // 4. Vérification que le personnage appartient bien au compte
            var characterData = FetchCharacterData(playerGuid, sLog);
            if (characterData == null)
                return;

            // 5. Vérification que le GUID lu correspond bien à celui du paquet
            if (!ValidateCharacterGuid(characterData.Guid, playerGuid, sLog))
                return;

            // 6. Vérification de l'état du personnage (banni, etc.)
            if (IsCharacterBanned(characterData.Banned, characterData.Name, sLog))
                return;

            // 7. Vérification de la carte et récupération de l'instance
            if (!TryGetValidInstance(characterData, out Instance destinationInstance, out bool isRecoveryUsed, sLog))
                return;

            // 8. Préparation de la session pour le personnage
            var player = PreparePlayerSession(characterData.Guid, sLog);
            if (player == null)
                return;

            // 9. Mise à jour des coordonnées du joueur
            UpdatePlayerCoordinates(player, characterData, isRecoveryUsed);

            // 10. Envoi des données au serveur de monde
            SendPlayerLoginDataToWorldServer(player, characterData, destinationInstance, sLog);
        }

        #region Vérifications et utilitaires
        private bool CheckPacketSize(WorldPacket p, int minSize, Logger sLog)
        {
            if (!Utils.CheckPacketSize(p, minSize, this))
            {
                sLog.OutError("PLAYER_LOGIN", R_E_CHARHAN_COM, p.Size, minSize);
                Disconnect();
                return false;
            }
            return true;
        }

        private bool TryReadPlayerGuid(WorldPacket p, out ulong playerGuid, Logger sLog)
        {
            playerGuid = 0;
            try
            {
                playerGuid = p.ReadUInt64();
                return true;
            }
            catch (Exception ex)
            {
                sLog.OutError("PLAYER_LOGIN", R_E_CHARHAN_COM_1, ex.Message);
                Disconnect();
                return false;
            }
        }

        private bool IsPlayerAlreadyConnected(ulong playerGuid, Logger sLog)
        {
            if (sClientMgr.GetRPlayer(Utils.GUID_LOPART(playerGuid)) != null)
            {
                Ws.OutPacket((ushort)Opcodes.SMSG_CHARACTER_LOGIN_FAILED, 1, [(byte)LoginErrorCode.CHAR_LOGIN_DUPLICATE_CHARACTER]);
                return true;
            }
            return false;
        }

        private CharacterData FetchCharacterData(ulong playerGuid, Logger sLog)
        {
            var result = CharacterDatabase.Query(
                @"SELECT guid, name, race, class, gender, level, mapId, positionX, positionY, positionZ,
                 zoneId, banned, restState, deathstate, player_flags, guild_data.guildid,
                 recoveryMapId, recoveryPositionX, recoveryPositionY, recoveryPositionZ
                FROM characters
                LEFT JOIN guild_data ON characters.guid = guild_data.playerid
                WHERE guid = {0} AND acct = {1}",
                Utils.GUID_LOPART(playerGuid),
                GetAccountId()
            );

            if (result == null || !result.NextRow())
            {                
                Ws.OutPacket((ushort)Opcodes.SMSG_CHARACTER_LOGIN_FAILED, 1, [(byte)LoginErrorCode.CHAR_LOGIN_NO_CHARACTER]);
                return null;
            }

            return new CharacterData(result);
        }

        private bool ValidateCharacterGuid(ulong dbGuid, ulong packetGuid, Logger sLog)
        {
            if (dbGuid != packetGuid)
            {
                sLog.OutError("PLAYER_LOGIN", R_E_CHARHAN_PL_6, dbGuid, packetGuid);
                Disconnect();
                return false;
            }
            return true;
        }

        private bool IsCharacterBanned(uint banned, string characterName, Logger sLog)
        {
            if (banned != 0 && (banned < 10 || banned > (uint)Utils.UNIXTIME))
            {
                sLog.OutWarning("PLAYER_LOGIN", R_W_CHARHAN_PL, characterName);
                Ws.OutPacket((ushort)Opcodes.SMSG_CHARACTER_LOGIN_FAILED, 1, [(byte)LoginErrorCode.CHAR_LOGIN_DISABLED]);
                return true;
            }
            return false;
        }

        private bool TryGetValidInstance(CharacterData characterData, out Instance destinationInstance, out bool isRecoveryUsed, Logger sLog)
        {
            isRecoveryUsed = false;

            // Tentative de récupérer une instance pour la carte actuelle
            destinationInstance = sClusterMgr.GetInstanceByMapId(characterData.MapId);
            if (destinationInstance == null || destinationInstance.Server == null)
            {
                sLog.OutError("PLAYER_LOGIN", R_E_CHARHAN_PL_7, characterData.MapId);

                // Utilisation des coordonnées de récupération
                isRecoveryUsed = true;
                var recoveryMapInfo = Storage.WorldMapInfoStorage.LookupEntry(characterData.RecoveryMapId);
                if (recoveryMapInfo != null)
                {
                    destinationInstance = sClusterMgr.GetInstanceByMapId(characterData.RecoveryMapId);
                    if (destinationInstance != null && destinationInstance.Server == null)
                    {
                        destinationInstance.Server = sClusterMgr.GetServerByMapId(characterData.RecoveryMapId);
                        if (destinationInstance.Server == null)
                        {
                            sLog.OutError("PLAYER_LOGIN", R_E_CHARHAN_PL_8, characterData.RecoveryMapId);
                            Ws.OutPacket((ushort)Opcodes.SMSG_CHARACTER_LOGIN_FAILED, 1, [(byte)LoginErrorCode.CHAR_LOGIN_NO_WORLD]);
                            sClientMgr.DestroyRPlayerInfo(Utils.GUID_LOPART(characterData.Guid));
                            return false;
                        }
                    }
                    else if (destinationInstance == null)
                    {
                        sLog.OutError("PLAYER_LOGIN", R_E_CHARHAN_PL_9, characterData.RecoveryMapId);
                        Ws.OutPacket((ushort)Opcodes.SMSG_CHARACTER_LOGIN_FAILED, 1, [(byte)LoginErrorCode.CHAR_LOGIN_NO_INSTANCES]);
                        sClientMgr.DestroyRPlayerInfo(Utils.GUID_LOPART(characterData.Guid));
                        return false;
                    }
                }
                else
                {
                    sLog.OutError("PLAYER_LOGIN", R_E_CHARHAN_PL_10, characterData.MapId);
                    Ws.OutPacket((ushort)Opcodes.SMSG_CHARACTER_LOGIN_FAILED, 1, [(byte)LoginErrorCode.CHAR_LOGIN_NO_WORLD]);
                    sClientMgr.DestroyRPlayerInfo(Utils.GUID_LOPART(characterData.Guid));
                    return false;
                }
            }

            return true;
        }

        private RPlayerInfo PreparePlayerSession(ulong playerGuid, Logger sLog)
        {
            // 1. Créer le joueur
            m_currentPlayer = sClientMgr.CreateRPlayer(Utils.GUID_LOPART(playerGuid));

            if (m_currentPlayer == null)
            {
                sLog.OutError("PLAYER_LOGIN", R_E_CHARHAN_PL_11, playerGuid);
                Ws.OutPacket((ushort)Opcodes.SMSG_CHARACTER_LOGIN_FAILED, 1, [(byte)LoginErrorCode.CHAR_LOGIN_FAILED]);
                return null;
            }

            // 2. Initialiser _session avec les valeurs de la session actuelle
            uint accountId = AccountId; // Récupère l'accountId de la session actuelle
            uint sessionId = SessionId; // Récupère le sessionId de la session actuelle
            m_currentPlayer.InitializeSession(accountId, sessionId);

            return m_currentPlayer;
        }


        private void UpdatePlayerCoordinates(RPlayerInfo player, CharacterData characterData, bool isRecoveryUsed)
        {
            if (isRecoveryUsed)
            {
                player.MapId = characterData.RecoveryMapId;
                player.PositionX = characterData.RecoveryPositionX;
                player.PositionY = characterData.RecoveryPositionY;
                player.PositionZ = characterData.RecoveryPositionZ;
            }
            else
            {
                player.MapId = characterData.MapId;
                player.PositionX = characterData.PositionX;
                player.PositionY = characterData.PositionY;
                player.PositionZ = characterData.PositionZ;
            }
        }

        private void SendPlayerLoginDataToWorldServer(RPlayerInfo player, CharacterData characterData, Instance destinationInstance, Logger sLog)
        {
            var playerLoginData = new WorldPacket((ushort)WorkerServerOpcodes.ISMSG_PLAYER_LOGIN);
            sClientMgr.AddStringPlayerInfo(player);

            // Ajout des informations de base
            playerLoginData.WriteUInt32(Utils.GUID_LOPART(characterData.Guid));
            playerLoginData.WriteUInt32(destinationInstance.MapId);
            playerLoginData.WriteUInt32(destinationInstance.InstanceId);

            // Ajout des informations du compte
            playerLoginData.WriteUInt32(GetAccountId());
            playerLoginData.WriteUInt32(m_accountFlags);
            playerLoginData.WriteUInt32(m_sessionId);
            playerLoginData.WriteString(m_GMPermissions);
            playerLoginData.WriteString(GetAccountName());
            playerLoginData.WriteUInt32(m_ClientBuild);

            // Ajout des données de compte (8 slots possibles)
            for (byte i = 0; i < 8; i++)
            {
                var acd = GetAccountData(i);
                if (acd != null && acd.Value.Data != null && acd.Value.Data.Length > 0)
                {
                    playerLoginData.WriteUInt32((uint)acd.Value.Data.Length);
                    playerLoginData.WriteBytes(acd.Value.Data);
                }
                else
                {
                    playerLoginData.WriteUInt32(0);
                }
            }

            // Envoi du paquet de succès au client
            Ws.OutPacket((ushort)WorkerServerOpcodes.ICMSG_PLAYER_LOGIN_RESULT, 1, [(byte)LoginErrorCode.CHAR_LOGIN_SUCCESS]);

            // Envoi des données au serveur de monde
            destinationInstance.Server.SendPacket(playerLoginData);
            m_nextServer = destinationInstance.Server;

            sLog.OutDetail("PLAYER_LOGIN", R_D_CHARHAN_PL, characterData.Name, characterData.Guid, player.MapId);
        }
        #endregion
        #region HandleUpdateAccountData
        private void HandleUpdateAccountData(WorldPacket p)
        {
            var sLog = new Logger();

            // 1. Lire l'ID du slot de données de compte (0-7)
            uint uiID;
            try
            {
                uiID = p.ReadUInt32();
            }
            catch (Exception ex)
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_1, ex.Message);
                Disconnect();
                return;
            }

            // 2. Vérifier que l'ID est valide (0-7)
            if (uiID > 7)
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_2, uiID);
                Disconnect();
                return;
            }

            // 3. Lire le timestamp
            uint _time;
            try
            {
                _time = p.ReadUInt32();
            }
            catch (Exception ex)
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_3, ex.Message);
                Disconnect();
                return;
            }

            // 4. Lire la taille des données décompressées
            uint uiDecompressedSize;
            try
            {
                uiDecompressedSize = p.ReadUInt32();
            }
            catch (Exception ex)
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_4, ex.Message);
                Disconnect();
                return;
            }

            // 5. Vérifier la taille des données
            if (uiDecompressedSize == 0)
            {
                SetAccountData(uiID, null, false, 0);
                sLog.OutDebug("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_5, uiID, GetAccountName());
                return;
            }

            if (uiDecompressedSize > 100000)
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_6, uiDecompressedSize);
                Disconnect();
                return;
            }

            if (uiDecompressedSize >= 65534)
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_7, uiDecompressedSize);
                Disconnect();
                return;
            }

            // 6. Lire les données compressées
            byte[] compressedData;
            try
            {
                int bytesRemaining = p.Contents.Length - p.Size;
                if (bytesRemaining <= 0)
                {
                    sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_8);
                    Disconnect();
                    return;
                }
                compressedData = new byte[bytesRemaining];
                p.ReadBytes(compressedData, 0, bytesRemaining);
            }
            catch (Exception ex)
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_9, ex.Message);
                Disconnect();
                return;
            }

            // 7. Décompresser les données si nécessaire
            byte[] decompressedData;
            if (uiDecompressedSize > (uint)compressedData.Length)
            {
                try
                {
                    decompressedData = new byte[uiDecompressedSize];
                    using var compressedStream = new MemoryStream(compressedData);
                    using var decompressedStream = new MemoryStream(decompressedData);
                    using var zlibStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
                    zlibStream.CopyTo(decompressedStream);
                }
                catch (Exception ex)
                {
                    sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA, ex.Message);
                    Disconnect();
                    return;
                }
            }
            else
            {
                decompressedData = compressedData;
            }

            // 8. Valider les données décompressées
            if (!ValidateDecompressedData(decompressedData, uiDecompressedSize))
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_10);
                Disconnect();
                return;
            }

            // 9. Sauvegarder les données dans le compte
            try
            {
                SetAccountData(uiID, decompressedData, false, uiDecompressedSize);
                sLog.OutDebug("UPDATE_ACCOUNT_DATA", R_D_CHARHAN_UA, uiID, GetAccountName(), uiDecompressedSize);
            }
            catch (Exception ex)
            {
                sLog.OutError("UPDATE_ACCOUNT_DATA", R_E_CHARHAN_UA_11, ex.Message);
            }
        }

        // Méthode utilitaire pour valider les données décompressées
        private static bool ValidateDecompressedData(byte[] data, uint expectedSize)
        {
            // 1. Vérifier que la taille des données correspond à celle attendue
            if (data == null || data.Length != expectedSize)
            {
                return false;
            }

            // 2. Vérifier les motifs binaires suspects
            if (MaliciousPatternDetector.ContainsMaliciousPatterns(data))
            {
                return false;
            }

            // 3. Vérifier les caractères non imprimables
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b >= 32 && b <= 126)
                {
                    continue;
                }
                if (b == 9 || b == 10 || b == 13)
                {
                    continue;
                }
                if (b == 0)
                {
                    continue;
                }
                return false;
            }

            return true;
        }
        #endregion

        private void HandleRequestAccountData(WorldPacket p)
        {
            var sLog = new Logger();

            // 1. Lire l'ID du slot de données de compte (0-7)
            uint id;
            try
            {
                id = p.ReadUInt32();
            }
            catch (Exception ex)
            {
                sLog.OutError("REQUEST_ACCOUNT_DATA", R_E_CHARHAN_RA, ex.Message);
                Disconnect();
                return;
            }

            // 2. Vérifier que l'ID est valide (0-7)
            if (id > 7)
            {
                sLog.OutError("REQUEST_ACCOUNT_DATA", R_E_CHARHAN_RA_1, id);
                Disconnect();
                return;
            }

            // 3. Récupérer les données du compte depuis la base de données
            var result = CharacterDatabase.Query(
                "SELECT data FROM account_data WHERE acct = {0} AND slot = {1}",
                GetAccountId(),
                id
            );

            // 4. Préparer les données à envoyer
            byte[] accountData = null;
            uint dataSize = 0;

            if (result != null && result.NextRow())
            {
                accountData = result.GetValue(0) as byte[];
                dataSize = accountData != null ? (uint)accountData.Length : 0;
            }

            // 5. Calculer la taille nécessaire pour le paquet
            // Taille minimale : 4 octets pour l'ID + 4 octets pour la taille des données
            int packetSize = 8;

            // Ajouter la taille des données (compressées ou non)
            if (dataSize > 0)
            {
                // Si les données sont grandes, on les compressera, donc on estime la taille compressée
                // En général, la compression réduit la taille, mais on prend une marge de sécurité
                packetSize += dataSize > 200 ? (int)(dataSize * 0.8) : (int)dataSize;
            }

            // 6. Créer le paquet avec la taille calculée
            var dataPacket = new WorldPacket((ushort)Opcodes.SMSG_UPDATE_ACCOUNT_DATA, packetSize);

            // 7. Écrire l'ID du slot dans le paquet
            dataPacket.WriteUInt32(id);

            // 8. Écrire la taille des données (ou 0 si aucune donnée)
            dataPacket.WriteUInt32(dataSize);

            // 9. Si des données existent, les ajouter au paquet
            if (dataSize > 0 && accountData != null)
            {
                try
                {
                    if (dataSize > 200) // Seuil arbitraire pour la compression
                    {
                        using var outputStream = new MemoryStream();
                        using (var compressionStream = new DeflateStream(outputStream, CompressionLevel.Optimal))
                        {
                            compressionStream.Write(accountData, 0, accountData.Length);
                        }
                        byte[] compressedData = outputStream.ToArray();
                        dataPacket.WriteBytes(compressedData);
                    }
                    else
                    {
                        dataPacket.WriteBytes(accountData);
                    }
                }
                catch (Exception ex)
                {
                    sLog.OutWarning("REQUEST_ACCOUNT_DATA", R_W_CHARHAN_RA, ex.Message);
                    dataPacket.WriteBytes(accountData);
                }
            }

            // 10. Envoyer le paquet de réponse au client
            try
            {
                Wss.SendPacket(dataPacket);
                sLog.OutDebug("REQUEST_ACCOUNT_DATA", R_D_CHARHAN_RA, id, GetAccountName());
            }
            catch (Exception ex)
            {
                sLog.OutError("REQUEST_ACCOUNT_DATA", R_E_CHARHAN_RA_2, ex.Message);
            }
        }
    }
}
