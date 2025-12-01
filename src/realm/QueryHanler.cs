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
using System.Text;
using WaadShared;

namespace WaadRealmServer
{
    public partial class Session
    {
        private void HandleCreatureQueryOpcode(WorldPacket p)
        {
            var sLog = new Logger();

            if (!Utils.CheckPacketSize(p, 12, this))
            {
                sLog.OutError("CREATURE_QUERY", "Taille de paquet invalide. Déconnexion.");
                Disconnect();
                return;
            }

            uint entry;
            ulong guid;
            try
            {
                entry = p.ReadUInt32();
                guid = p.ReadUInt64();
            }
            catch (Exception ex)
            {
                sLog.OutError("CREATURE_QUERY", $"Erreur lors de la lecture de l'entry ou du GUID : {ex.Message}. Déconnexion.");
                Disconnect();
                return;
            }

            var data = new WorldPacket((ushort)Opcodes.SMSG_CREATURE_QUERY_RESPONSE, 250);
            data.WriteUInt32(entry);

            if (entry == 300000)
            {
                data.WriteString("WayPoint");
                data.WriteByte(0);
                data.WriteByte(0);
                data.WriteByte(0);
                data.WriteString("Level is WayPoint ID");
                for (int i = 0; i < 8; i++)
                    data.WriteUInt32(0);
                data.WriteByte(0);
            }
            else
            {
                CreatureInfo ci = Storage.CreatureNameStorage.LookupEntry(entry);
                if (ci == null)
                {
                    sLog.OutDebug("CREATURE_QUERY", $"Aucune créature trouvée pour l'entry : {entry}.");
                    return;
                }
                sLog.OutDebug("CREATURE_QUERY", $"Requête de créature reçue pour : {ci.Name}");

                data.WriteUInt32(entry);
                data.WriteString(ci.Name);
                data.WriteByte(0);
                data.WriteByte(0);
                data.WriteByte(0);
                data.WriteString(ci.SubName ?? "");
                data.WriteString(ci.InfoStr ?? "");
                data.WriteUInt32(ci.Flags1);
                data.WriteUInt32(ci.Type);
                data.WriteUInt32(ci.Family);
                data.WriteUInt32(ci.Rank);
                // data.WriteUInt32(ci.Unknown1);
                data.WriteUInt32(ci.SpellDataID);
                data.WriteUInt32(ci.Male_DisplayID);
                data.WriteUInt32(ci.Female_DisplayID);
                data.WriteUInt32(ci.Male_DisplayID2);
                data.WriteUInt32(ci.Female_DisplayID2);
                data.WriteFloat(ci.UnkFloat1);
                data.WriteFloat(ci.UnkFloat2);
                // data.WriteByte(ci.Civilian);
                data.WriteByte(ci.Leader);
                for (int i = 0; i < 5; i++)
                    data.WriteUInt32(0);
            }

            try
            {
                Wss.SendPacket(data);
                sLog.OutDebug("CREATURE_QUERY", $"Réponse envoyée pour la créature (Entry: {entry}).");
            }
            catch (Exception ex)
            {
                sLog.OutError("CREATURE_QUERY", $"Erreur lors de l'envoi de la réponse : {ex.Message}");
            }
        }
        private void HandleGameObjectQueryOpcode(WorldPacket p)
        {
            var sLog = new Logger();

            if (!Utils.CheckPacketSize(p, 12, this))
            {
                sLog.OutError("GAMEOBJECT_QUERY", "Taille de paquet invalide. Déconnexion.");
                Disconnect();
                return;
            }

            uint entryID;
            ulong guid;
            try
            {
                entryID = p.ReadUInt32();
                guid = p.ReadUInt64();
            }
            catch (Exception ex)
            {
                sLog.OutError("GAMEOBJECT_QUERY", $"Erreur lors de la lecture de l'entry ou du GUID : {ex.Message}. Déconnexion.");
                Disconnect();
                return;
            }

            var data = new WorldPacket((ushort)Opcodes.SMSG_GAMEOBJECT_QUERY_RESPONSE, 900);
            GameObjectInfo goinfo = Storage.GameObjectNameStorage.LookupEntry(entryID);
            if (goinfo == null)
            {
                sLog.OutDebug("GAMEOBJECT_QUERY", $"Aucun objet trouvé pour l'entry : {entryID}.");
                return;
            }

            data.WriteUInt32(entryID);
            data.WriteUInt32(goinfo.Type);
            data.WriteUInt32(goinfo.DisplayID);
            data.WriteString(goinfo.Name);
            data.WriteByte(0);
            data.WriteByte(0);
            data.WriteByte(0);
            data.WriteByte(0);
            data.WriteByte(0);
            data.WriteByte(0);
            data.WriteUInt32(goinfo.SpellFocus);
            foreach (uint sound in goinfo.Sounds)
                data.WriteUInt32(sound);
            foreach (uint unknown in goinfo.Unknowns)
                data.WriteUInt32(unknown);
            data.WriteFloat(goinfo.Unknown15);

            try
            {
                Wss.SendPacket(data);
                sLog.OutDebug("GAMEOBJECT_QUERY", $"Réponse envoyée pour l'objet (Entry: {entryID}).");
            }
            catch (Exception ex)
            {
                sLog.OutError("GAMEOBJECT_QUERY", $"Erreur lors de l'envoi de la réponse : {ex.Message}");
            }
        }
        private void HandleItemQuerySingleOpcode(WorldPacket p)
        {
            var sLog = new Logger();

            if (!Utils.CheckPacketSize(p, 4, this))
            {
                sLog.OutError("ITEM_QUERY_SINGLE", "Taille de paquet invalide. Déconnexion.");
                Disconnect();
                return;
            }

            uint itemId;
            try
            {
                itemId = p.ReadUInt32();
            }
            catch (Exception ex)
            {
                sLog.OutError("ITEM_QUERY_SINGLE", $"Erreur lors de la lecture de l'item ID : {ex.Message}. Déconnexion.");
                Disconnect();
                return;
            }

            ItemPrototype itemProto = Storage.ItemPrototypeStorage.LookupEntry(itemId);
            if (itemProto == null)
            {
                sLog.OutError("ITEM_QUERY_SINGLE", $"Aucun item trouvé pour l'ID : {itemId}.");
                return;
            }

            var data = new WorldPacket((ushort)Opcodes.SMSG_ITEM_QUERY_SINGLE_RESPONSE, 600 + Encoding.UTF8.GetByteCount(itemProto.Name1) + Encoding.UTF8.GetByteCount(itemProto.Description));

            data.WriteUInt32(itemProto.ItemId);
            data.WriteUInt32(itemProto.Class);
            data.WriteUInt32(itemProto.SubClass);
            data.WriteUInt32(itemProto.UnknownBc);
            data.WriteString(itemProto.Name1);
            data.WriteByte(0);
            data.WriteByte(0);
            data.WriteByte(0);
            data.WriteUInt32(itemProto.DisplayInfoID);
            data.WriteUInt32(itemProto.Quality);
            data.WriteUInt32(itemProto.Flags);
            data.WriteUInt32(itemProto.Faction);
            data.WriteUInt32(itemProto.BuyPrice);
            data.WriteUInt32(itemProto.SellPrice);
            data.WriteUInt32(itemProto.InventoryType);
            data.WriteUInt32(itemProto.AllowableClass);
            data.WriteUInt32(itemProto.AllowableRace);
            data.WriteUInt32(itemProto.ItemLevel);
            data.WriteUInt32(itemProto.RequiredLevel);
            data.WriteUInt32(itemProto.RequiredSkill);
            data.WriteUInt32(itemProto.RequiredSkillRank);
            data.WriteUInt32(itemProto.RequiredSkillSubRank);
            data.WriteUInt32(itemProto.RequiredPlayerRank1);
            data.WriteUInt32(itemProto.RequiredPlayerRank2);
            data.WriteUInt32(itemProto.RequiredFaction);
            data.WriteUInt32(itemProto.RequiredFactionStanding);
            data.WriteUInt32(itemProto.Unique);
            data.WriteUInt32(itemProto.MaxCount);
            data.WriteUInt32(itemProto.ContainerSlots);
            data.WriteUInt32(itemProto.StatsCount);
            for (int i = 0; i < itemProto.StatsCount; i++)
            {
                data.WriteUInt32(itemProto.Stats[i].Type);
                data.WriteInt32(itemProto.Stats[i].Value);
            }
            data.WriteUInt32(itemProto.ScalingStatsEntry);
            data.WriteUInt32(itemProto.ScalingStatsFlag);
            for (int i = 0; i < 2; i++)
            {
                data.WriteFloat(itemProto.Damage[i].Min);
                data.WriteFloat(itemProto.Damage[i].Max);
                data.WriteUInt32(itemProto.Damage[i].Type);
            }
            data.WriteUInt32(itemProto.Armor);
            data.WriteUInt32(itemProto.HolyRes);
            data.WriteUInt32(itemProto.FireRes);
            data.WriteUInt32(itemProto.NatureRes);
            data.WriteUInt32(itemProto.FrostRes);
            data.WriteUInt32(itemProto.ShadowRes);
            data.WriteUInt32(itemProto.ArcaneRes);
            data.WriteUInt32(itemProto.Delay);
            data.WriteUInt32(itemProto.AmmoType);
            data.WriteFloat(itemProto.Range);
            for (int i = 0; i < 5; i++)
            {
                data.WriteUInt32(itemProto.Spells[i].Id);
                data.WriteUInt32(itemProto.Spells[i].Trigger);
                data.WriteInt32(itemProto.Spells[i].Charges);
                data.WriteInt32(itemProto.Spells[i].Cooldown);
                data.WriteUInt32(itemProto.Spells[i].Category);
                data.WriteInt32(itemProto.Spells[i].CategoryCooldown);
            }
            data.WriteUInt32(itemProto.Bonding);
            data.WriteString(itemProto.Description);
            data.WriteUInt32(itemProto.PageId);
            data.WriteUInt32(itemProto.PageLanguage);
            data.WriteUInt32(itemProto.PageMaterial);
            data.WriteUInt32(itemProto.QuestId);
            data.WriteUInt32(itemProto.LockId);
            data.WriteUInt32(itemProto.LockMaterial);
            data.WriteUInt32(itemProto.Field108);
            data.WriteUInt32(itemProto.RandomPropId);
            data.WriteUInt32(itemProto.RandomSuffixId);
            data.WriteUInt32(itemProto.Block);
            data.WriteUInt32(itemProto.ItemSet);
            data.WriteUInt32(itemProto.MaxDurability);
            data.WriteUInt32(itemProto.ZoneNameID);
            data.WriteUInt32(itemProto.MapID);
            data.WriteUInt32(itemProto.BagFamily);
            data.WriteUInt32(itemProto.TotemCategory);
            for (int i = 0; i < 3; i++)
            {
                data.WriteUInt32(itemProto.Sockets[i].SocketColor);
                data.WriteUInt32(itemProto.Sockets[i].Unk);
            }
            data.WriteUInt32(itemProto.SocketBonus);
            data.WriteUInt32(itemProto.GemProperties);
            data.WriteInt32(itemProto.DisenchantReqSkill);
            data.WriteUInt32(itemProto.ArmorDamageModifier);
            data.WriteUInt32(itemProto.ExistingDuration);
            data.WriteUInt32(itemProto.ItemLimitCategory);
            data.WriteUInt32(itemProto.HolidayId);

            try
            {
                Wss.SendPacket(data);
                sLog.OutDebug("ITEM_QUERY_SINGLE", $"Réponse envoyée pour l'item (ID: {itemId}).");
            }
            catch (Exception ex)
            {
                sLog.OutError("ITEM_QUERY_SINGLE", $"Erreur lors de l'envoi de la réponse : {ex.Message}");
            }
        }
        private void HandleItemNameQueryOpcode(WorldPacket p)
        {
            var sLog = new Logger();

            if (!Utils.CheckPacketSize(p, 4, this))
            {
                sLog.OutError("ITEM_NAME_QUERY", "Taille de paquet invalide. Déconnexion.");
                Disconnect();
                return;
            }

            uint itemId;
            try
            {
                itemId = p.ReadUInt32();
            }
            catch (Exception ex)
            {
                sLog.OutError("ITEM_NAME_QUERY", $"Erreur lors de la lecture de l'item ID : {ex.Message}. Déconnexion.");
                Disconnect();
                return;
            }

            var data = new WorldPacket((ushort)Opcodes.SMSG_ITEM_NAME_QUERY_RESPONSE, 50);
            data.WriteUInt32(itemId);
            ItemPrototype proto = Storage.ItemPrototypeStorage.LookupEntry(itemId);
            if (proto == null)
                data.WriteString("Item non trouvé");
            else
                data.WriteString(proto.Name1);

            try
            {
                Wss.SendPacket(data);
                sLog.OutDebug("ITEM_NAME_QUERY", $"Réponse envoyée pour le nom de l'item (ID: {itemId}).");
            }
            catch (Exception ex)
            {
                sLog.OutError("ITEM_NAME_QUERY", $"Erreur lors de l'envoi de la réponse : {ex.Message}");
            }
        }

        private void HandlePageTextQueryOpcode(WorldPacket p)
        {
            var sLog = new Logger();

            if (!Utils.CheckPacketSize(p, 4, this))
            {
                sLog.OutError("PAGE_TEXT_QUERY", "Taille de paquet invalide. Déconnexion.");
                Disconnect();
                return;
            }

            uint pageId;
            try
            {
                pageId = p.ReadUInt32();
            }
            catch (Exception ex)
            {
                sLog.OutError("PAGE_TEXT_QUERY", $"Erreur lors de la lecture de l'ID de page : {ex.Message}. Déconnexion.");
                Disconnect();
                return;
            }

            while (pageId != 0)
            {
                ItemPage page = Storage.ItemPageStorage.LookupEntry(pageId);
                if (page == null)
                    break;

                var data = new WorldPacket((ushort)Opcodes.SMSG_PAGE_TEXT_QUERY_RESPONSE, 10000);
                data.WriteUInt32(page.Id);
                data.WriteString(page.Text);
                data.WriteUInt32(page.NextPage);

                try
                {
                    Wss.SendPacket(data);
                    sLog.OutDebug("PAGE_TEXT_QUERY", $"Réponse envoyée pour la page (ID: {pageId}).");
                }
                catch (Exception ex)
                {
                    sLog.OutError("PAGE_TEXT_QUERY", $"Erreur lors de l'envoi de la réponse : {ex.Message}");
                }

                pageId = page.NextPage;
            }
        }
    
        private void HandleNameQueryOpcode(WorldPacket p)
        {
            var sLog = new Logger();
            
            if (!Utils.CheckPacketSize(p, 8, this))
            {
                sLog.OutError("NAME_QUERY", "Taille de paquet invalide. Déconnexion.");
                Disconnect();
                return;
            }
            
            ulong guid;
            try
            {
                guid = p.ReadUInt64();
            }
            catch (Exception ex)
            {
                sLog.OutError("NAME_QUERY", $"Erreur lors de la lecture du GUID : {ex.Message}. Déconnexion.");
                Disconnect();
                return;
            }
            
            RPlayerInfo playerInfo = sClientMgr.GetRPlayer(Utils.GUID_LOPART(guid));
            if (playerInfo == null)
            {
                sLog.OutDebug("NAME_QUERY", $"Aucun personnage trouvé pour le GUID : {guid}.");
                return;
            }
            sLog.OutDebug("NAME_QUERY", $"Requête de nom reçue pour le personnage : {playerInfo.Name}");
            
            var data = new WorldPacket((ushort)Opcodes.SMSG_NAME_QUERY_RESPONSE, 5000);
            
            data.WriteUInt64(playerInfo.Guid);  
            data.WriteString(playerInfo.Name);
            data.WriteByte(0);
            data.WriteByte(playerInfo.Race);
            data.WriteByte(playerInfo.Gender);
            data.WriteByte(playerInfo.Class);
            data.WriteByte(0);
            
            try
            {
                Wss.SendPacket(data);
                sLog.OutDebug("NAME_QUERY", $"Réponse envoyée pour le personnage {playerInfo.Name} (GUID: {playerInfo.Guid}).");
            }
            catch (Exception ex)
            {
                sLog.OutError("NAME_QUERY", $"Erreur lors de l'envoi de la réponse : {ex.Message}");
            }
        }

        private void HandleRealmSplitQuery(WorldPacket p)
        {
            var sLog = new Logger();
            uint v;
            
            try
            {
                v = p.ReadUInt32();
            }
            catch (Exception ex)
            {
                sLog.OutError("REALM_SPLIT", $"Erreur lors de la lecture de la requête : {ex.Message}. Déconnexion.");
                Disconnect();
                return;
            }
            
            var data = new WorldPacket((ushort)Opcodes.SMSG_REALM_SPLIT, 17);
            data.WriteUInt32(v);
            data.WriteUInt32(0);
            data.WriteString("01/01/01"); 
            
            try
            {
                Wss.SendPacket(data);
                sLog.OutDebug("REALM_SPLIT", $"Réponse envoyée pour la requête de division de royaume.");
            }
            catch (Exception ex)
            {
                sLog.OutError("REALM_SPLIT", $"Erreur lors de l'envoi de la réponse : {ex.Message}");
            }
        }

        private void HandleQueryTimeOpcode(WorldPacket p)
        {
            var sLog = new Logger();            
            var data = new WorldPacket((ushort)Opcodes.SMSG_QUERY_TIME_RESPONSE, 4);
            
            data.WriteUInt32((uint)Utils.UNIXTIME);
            
            try
            {
                Wss.SendPacket(data);
                sLog.OutDebug("QUERY_TIME", $"Réponse de temps envoyée : {Utils.UNIXTIME}.");
            }
            catch (Exception ex)
            {
                sLog.OutError("QUERY_TIME", $"Erreur lors de l'envoi de la réponse : {ex.Message}");
            }
        }
    }
}
