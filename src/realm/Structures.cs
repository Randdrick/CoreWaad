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
using System.Collections.Generic;
using WaadShared;

namespace WaadRealmServer;

public enum Classes : byte
{
    UNK_CLASS1 = 0,
    WARRIOR = 1,
    PALADIN = 2,
    HUNTER = 3,
    ROGUE = 4,
    PRIEST = 5,
    DEATHKNIGHT = 6,
    SHAMAN = 7,
    MAGE = 8,
    WARLOCK = 9,
    UNK_CLASS2 = 10,
    DRUID = 11,
}

public enum Races : byte
{
    RACE_HUMAN = 1,
    RACE_ORC = 2,
    RACE_DWARF = 3,
    RACE_NIGHTELF = 4,
    RACE_UNDEAD = 5,
    RACE_TAUREN = 6,
    RACE_GNOME = 7,
    RACE_TROLL = 8,
    RACE_GOBLIN = 9,
    RACE_BLOODELF = 10,
    RACE_DRAENEI = 11,
    MAX_RACE_NORMAL = 12,
    RACE_NAGA = 13,
    RACE_BROKEN = 14, // Rou�
    RACE_SKELETON = 15,
    RACE_VRYKUL = 16,
    RACE_TUSKAR = 17, // Rohart
    RACE_FORTESTROLL = 18, // Troll des forets
    RACE_TAUNKA = 19,
    RACE_LK_SQUELETON = 20, // Squelette du Northrend
    RACE_ICE_TROLL = 21,  // Troll des glaces
    MAX_RACE_EXTENDED
}

public class SocketInfo
{
    public uint SocketColor { get; set; }
    public uint Unk { get; set; }
}

public class ItemSpell
{
    public uint Id { get; set; }
    public uint Trigger { get; set; }
    public int Charges { get; set; }
    public int Cooldown { get; set; }
    public uint Category { get; set; }
    public int CategoryCooldown { get; set; }
}

public class ItemDamage
{
    public float Min { get; set; }
    public float Max { get; set; }
    public uint Type { get; set; }
}

public class ItemStat
{
    public uint Type { get; set; }
    public int Value { get; set; }
}

public class ItemPrototype
{
    public uint ItemId { get; set; }
    public uint Class { get; set; }
    public uint SubClass { get; set; }
    public uint UnknownBc { get; set; }
    public string Name1 { get; set; }
    public string Name2 { get; set; }
    public string Name3 { get; set; }
    public string Name4 { get; set; }
    public uint DisplayInfoID { get; set; }
    public uint Quality { get; set; }
    public uint Flags { get; set; }
    public uint Faction { get; set; }
    public uint BuyPrice { get; set; }
    public uint SellPrice { get; set; }
    public uint InventoryType { get; set; }
    public uint AllowableClass { get; set; }
    public uint AllowableRace { get; set; }
    public uint ItemLevel { get; set; }
    public uint RequiredLevel { get; set; }
    public uint RequiredSkill { get; set; }
    public uint RequiredSkillRank { get; set; }
    public uint RequiredSkillSubRank { get; set; }
    public uint RequiredPlayerRank1 { get; set; }
    public uint RequiredPlayerRank2 { get; set; }
    public uint RequiredFaction { get; set; }
    public uint RequiredFactionStanding { get; set; }
    public uint Unique { get; set; }
    public uint MaxCount { get; set; }
    public uint ContainerSlots { get; set; }
    public uint StatsCount { get; set; }
    public ItemStat[] Stats { get; set; } = new ItemStat[10];
    public uint ScalingStatsEntry { get; set; }
    public uint ScalingStatsFlag { get; set; }
    public ItemDamage[] Damage { get; set; } = new ItemDamage[2];
    public uint Armor { get; set; }
    public uint HolyRes { get; set; }
    public uint FireRes { get; set; }
    public uint NatureRes { get; set; }
    public uint FrostRes { get; set; }
    public uint ShadowRes { get; set; }
    public uint ArcaneRes { get; set; }
    public uint Delay { get; set; }
    public uint AmmoType { get; set; }
    public float Range { get; set; }
    public ItemSpell[] Spells { get; set; } = new ItemSpell[5];
    public uint Bonding { get; set; }
    public string Description { get; set; }
    public uint PageId { get; set; }
    public uint PageLanguage { get; set; }
    public uint PageMaterial { get; set; }
    public uint QuestId { get; set; }
    public uint LockId { get; set; }
    public uint LockMaterial { get; set; }
    public uint Field108 { get; set; }
    public uint RandomPropId { get; set; }
    public uint RandomSuffixId { get; set; }
    public uint Block { get; set; }
    public uint ItemSet { get; set; }
    public uint MaxDurability { get; set; }
    public uint ZoneNameID { get; set; }
    public uint MapID { get; set; }
    public uint BagFamily { get; set; }
    public uint TotemCategory { get; set; }
    public SocketInfo[] Sockets { get; set; } = new SocketInfo[3];
    public uint SocketBonus { get; set; }
    public uint GemProperties { get; set; }
    public int DisenchantReqSkill { get; set; }
    public uint ArmorDamageModifier { get; set; }
    public uint ExistingDuration { get; set; }
    public uint ItemLimitCategory { get; set; }
    public uint HolidayId { get; set; }
}

public class CreatureInfo
{
    public uint Id { get; set; }
    public string Name { get; set; }
    public string SubName { get; set; }
    public string InfoStr { get; set; }
    public uint Flags1 { get; set; }
    public uint Type { get; set; }
    public uint Family { get; set; }
    public uint Rank { get; set; }
    public uint Unknown1 { get; set; }
    public uint SpellDataID { get; set; }
    public uint Male_DisplayID { get; set; }
    public uint Female_DisplayID { get; set; }
    public uint Male_DisplayID2 { get; set; }
    public uint Female_DisplayID2 { get; set; }
    public float UnkFloat1 { get; set; }
    public float UnkFloat2 { get; set; }
    public byte Civilian { get; set; }
    public byte Leader { get; set; }
}

public class GameObjectInfo
{
    public uint ID { get; set; }
    public uint Type { get; set; }
    public uint DisplayID { get; set; }
    public string Name { get; set; }
    public uint SpellFocus { get; set; }
    public uint[] Sounds { get; set; } = new uint[9];
    public uint[] Unknowns { get; set; } = new uint[14];
    public float Unknown15 { get; set; }
}

public class ItemPage
{
    public uint Id { get; set; }
    public string Text { get; set; }
    public uint NextPage { get; set; }
}

public class Quest
{
    public uint Id { get; set; }
    public uint ZoneId { get; set; }
    public uint QuestSort { get; set; }
    public uint QuestFlags { get; set; }
    public uint MinLevel { get; set; }
    public uint MaxLevel { get; set; }
    public uint Type { get; set; }
    public uint RequiredRaces { get; set; }
    public uint RequiredClass { get; set; }
    public uint RequiredTradeskill { get; set; }
    public uint RequiredTradeskillValue { get; set; }
    public uint RequiredRepFaction { get; set; }
    public uint RequiredRepValue { get; set; }
    public uint RequiredPlayers { get; set; }
    public uint Time { get; set; }
    public uint SpecialFlags { get; set; }
    public uint PreviousQuestId { get; set; }
    public uint NextQuestId { get; set; }
    public uint SrcItem { get; set; }
    public uint SrcItemCount { get; set; }
    public string Title { get; set; }
    public string Details { get; set; }
    public string Objectives { get; set; }
    public string CompletionText { get; set; }
    public string IncompleteText { get; set; }
    public string EndText { get; set; }
    public string[] ObjectiveTexts { get; set; } = new string[4];
    public uint[] RequiredMob { get; set; } = new uint[4];
    public uint[] RequiredMobCount { get; set; } = new uint[4];
    public uint RequiredKillPlayer { get; set; }
    public uint[] RequiredItem { get; set; } = new uint[6];
    public uint[] RequiredItemCount { get; set; } = new uint[6];
    public uint[] RecItemDuringQuestId { get; set; } = new uint[4];
    public uint[] RequiredSpell { get; set; } = new uint[4];
    public uint[] RewardChoiceItem { get; set; } = new uint[6];
    public uint[] RewardChoiceItemCount { get; set; } = new uint[6];
    public uint[] RewardItem { get; set; } = new uint[4];
    public uint[] RewardItemCount { get; set; } = new uint[4];
    public uint[] RewardRepFaction { get; set; } = new uint[5];
    public int[] RewardRepValueIndex { get; set; } = new int[5];
    public uint RewardTitle { get; set; }
    public uint RewardXpAsMoney { get; set; }
    public uint RewardMoney { get; set; }
    public uint BonusHonor { get; set; }
    public uint RewardXp { get; set; }
    public uint RewardSpell { get; set; }
    public uint RewardTalents { get; set; }
    public uint EffectOnPlayer { get; set; }
    public uint PointMapId { get; set; }
    public uint PointX { get; set; }
    public uint PointY { get; set; }
    public uint PointOpt { get; set; }
    public uint RequiredMoney { get; set; }
    public uint[] RequiredTriggers { get; set; } = new uint[4];
    public uint[] RequiredQuests { get; set; } = new uint[4];
    public int IsRepeatable { get; set; }
    public uint CountRequiredMob { get; set; }
    public uint CountRequiredQuests { get; set; }
    public uint CountRequiredTriggers { get; set; }
    public uint CountReceiveItems { get; set; }
    public uint CountRewardChoiceItem { get; set; }
    public uint CountRequiredItem { get; set; }
    public uint[] RequiredMobType { get; set; } = new uint[4];
    public uint CountRewardItem { get; set; }
    public uint RewardRepLimit { get; set; }
}

public class GossipText_Text
{
    public float Prob { get; set; }
    public string[] Text { get; set; } = new string[2];
    public uint Lang { get; set; }
    public uint[] Emote { get; set; } = new uint[6];
}

public class GossipText
{
    public uint ID { get; set; }
    public GossipText_Text[] Texts { get; set; } = new GossipText_Text[8];
}

public class MapInfo
{
    public uint MapId { get; set; }
    public uint ScreenId { get; set; }
    public uint Type { get; set; }
    public uint PlayerLimit { get; set; }
    public uint MinLevel { get; set; }
    public float RepopX { get; set; }
    public float RepopY { get; set; }
    public float RepopZ { get; set; }
    public uint RepopMapId { get; set; }
    public string Name { get; set; }
    public uint Flags { get; set; }
    public uint Cooldown { get; set; }
    public uint LvlModA { get; set; }
    public uint RequiredQuestA1 { get; set; }
    public uint RequiredQuestH2 { get; set; }
    public uint RequiredItem { get; set; }
    public uint HeroicKey1 { get; set; }
    public uint HeroicKey2 { get; set; }
    public float UpdateDistance { get; set; }
    public uint CheckpointId { get; set; }
    public uint Collision { get; set; }
    public uint ClusteringHandled { get; set; }
    public bool HasFlag(uint flag) => (Flags & flag) != 0;
}

public class RPlayerInfo
{
    public uint Guid { get; set; }
    public uint Id { get => Guid; set => Guid = value; }
    public uint AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public uint GuildId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public uint ZoneId { get; set; }
    public byte Race { get; set; }
    public byte Class { get; set; }
    public byte Gender { get; set; }
    public uint Latency { get; set; }
    public string GMPermissions { get; set; } = string.Empty;
    public uint AccountFlags { get; set; }
    public uint InstanceId { get; set; }
    public uint MapId { get; set; }
    public uint InstanceType { get; set; }
    public int References { get; set; }
    public uint ClientBuild { get; set; }
    public uint Team { get; set; }
    public object Session { get; set; } = new object(); 
    public uint SessionId { get; set; }
    private Session _session; 

    // Constructeur par défaut
    public RPlayerInfo()
    {
        _session = new Session(0, 0);
    }

    // Méthode pour initialiser ou réinitialiser _session
    public void InitializeSession(uint accountId, uint sessionId)
    {
        SessionId = sessionId;
        _session = new Session(accountId, sessionId);
    }

    public uint RecoveryMapId { get; set; }
    public LocationVector RecoveryPosition { get; set; } = new LocationVector();

    // Social (thread-safe)
    private readonly object socialLock = new();
    private readonly Dictionary<uint, string> friends = [];
    private readonly HashSet<uint> ignores = [];
    private readonly HashSet<uint> hasFriendList = [];

    public void Social_AddFriend(RPlayerInfo info, string note)
    {
        lock (socialLock)
        {
            friends[info.Guid] = note;
            hasFriendList.Add(info.Guid);
        }
    }

    public bool Social_IsIgnoring(RPlayerInfo info)
    {
        lock (socialLock)
        {
            return ignores.Contains(info.Guid);
        }
    }

    public bool Social_IsIgnoring(uint guid)
    {
        lock (socialLock)
        {
            return ignores.Contains(guid);
        }
    }

    public uint GetClassMask() => (uint)(1 << (Class - 1));
    public uint GetRaceMask() => (uint)(1 << (Race - 1));

    // Sérialisation (Pack/Unpack)
    public void Pack(ByteBuffer buf)
    {
        buf.Write(Guid);
        buf.Write(AccountId);
        buf.Write(Name);
        buf.Write(PositionX);
        buf.Write(PositionY);
        buf.Write(PositionZ);
        buf.Write(ZoneId);
        buf.Write(Race);
        buf.Write(Class);
        buf.Write(Gender);
        buf.Write(Latency);
        buf.Write(GMPermissions);
        buf.Write(AccountFlags);
        buf.Write(InstanceId);
        buf.Write(Level);
        buf.Write(GuildId);
        buf.Write(MapId);
        buf.Write(InstanceType);
        buf.Write(ClientBuild);
        buf.Write(Team);
    }

    public int Unpack(ByteBuffer buf)
    {
        // Déclaration des variables locales pour la lecture
        uint guid = 0;
        uint accountId = 0;
        uint zoneId = 0;
        uint accountFlags = 0;
        uint instanceId = 0;
        uint level = 1;
        uint guildId = 0;
        uint mapId = 0;
        uint clientBuild = 0;
        uint team = 0;
        uint instanceType = 0;
        float posX = 0;
        float posY = 0;
        float posZ = 0;
        byte race = 0;
        byte @class = 0;
        byte gender = 0;
        uint latency = 0;
        string name = string.Empty;
        string gmPermissions = string.Empty;

        // Lecture des données depuis le buffer
        buf.Read(ref guid);
        buf.Read(ref accountId);
        buf.Read(ref name);
        buf.Read(ref posX);
        buf.Read(ref posY);
        buf.Read(ref posZ);
        buf.Read(ref zoneId);
        buf.Read(ref race);
        buf.Read(ref @class);
        buf.Read(ref gender);
        buf.Read(ref latency);
        buf.Read(ref gmPermissions);
        buf.Read(ref accountFlags);
        buf.Read(ref instanceId);
        buf.Read(ref level);
        buf.Read(ref guildId);
        buf.Read(ref mapId);
        buf.Read(ref instanceType);
        buf.Read(ref clientBuild);
        buf.Read(ref team);

        // Assignation des valeurs aux propriétés de l'objet
        Guid = guid;
        AccountId = accountId;
        Name = name;
        PositionX = posX;
        PositionY = posY;
        PositionZ = posZ;
        ZoneId = zoneId;
        Race = race;
        Class = @class;
        Gender = gender;
        Latency = latency;
        GMPermissions = gmPermissions;
        AccountFlags = accountFlags;
        InstanceId = instanceId;
        Level = (int)level;
        GuildId = guildId;
        MapId = mapId;
        InstanceType = instanceType;
        ClientBuild = clientBuild;
        Team = team;

        return buf.Rpos;
    }

    public Session GetSession()
    {
        // Vérifie que _session est correctement initialisé
        if (_session.GetAccountId() == 0 && _session.GetSessionId() == 0)
        {
            throw new InvalidOperationException("La session n'a pas été initialisée. Appeler InitializeSession() avant d'utiliser GetSession().");
        }
        return _session;
    }
}


public class LocationVector
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public LocationVector() { }
    public LocationVector(float x, float y, float z) { X = x; Y = y; Z = z; }
}

public class Instance
{
    public uint InstanceId { get; set; }
    public uint MapId { get; set; }
    public uint MapCount { get; set; } = 0; // Used for load balancing
    public WorkerServer Server { get; set; }
    // Optionally, add more fields as needed
    public WorkerServerSocket ServerSocket { get; set; }

    public Instance() { }
    public Instance(uint instanceId, uint mapId, WorkerServer server)
    {
        InstanceId = instanceId;
        MapId = mapId;
        Server = server;
    }
}
