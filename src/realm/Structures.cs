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
    RACE_BROKEN = 14, // Roué
    RACE_SKELETON = 15,
    RACE_VRYKUL = 16,
    RACE_TUSKAR = 17, // Rohart
    RACE_FORTESTROLL = 18, // Troll des forêts
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
    [DbField("u")]
    public uint ItemId { get; set; }

    [DbField("u")]
    public uint Class { get; set; }

    [DbField("u")]
    public uint SubClass { get; set; }

    [DbField("u")]
    public uint Field4 { get; set; }

    [DbField("s")]
    public string Name1 { get; set; } = string.Empty;

    [DbField("s")]
    public string Name2 { get; set; } = string.Empty;

    [DbField("s")]
    public string Name3 { get; set; } = string.Empty;

    [DbField("s")]
    public string Name4 { get; set; } = string.Empty;

    [DbField("u")]
    public uint DisplayInfoID { get; set; }

    [DbField("u")]
    public uint Quality { get; set; }

    [DbField("u")]
    public uint Flags { get; set; }

    [DbField("u")]
    public uint BuyPrice { get; set; }

    [DbField("u")]
    public uint SellPrice { get; set; }

    [DbField("u")]
    public uint InventoryType { get; set; }

    [DbField("u")]
    public uint AllowableClass { get; set; }

    [DbField("u")]
    public uint AllowableRace { get; set; }

    [DbField("u")]
    public uint ItemLevel { get; set; }

    [DbField("u")]
    public uint RequiredLevel { get; set; }

    [DbField("u")]
    public uint RequiredSkill { get; set; }

    [DbField("u")]
    public uint RequiredSkillRank { get; set; }

    [DbField("u")]
    public uint RequiredSkillSubRank { get; set; }

    [DbField("u")]
    public uint RequiredPlayerRank1 { get; set; }

    [DbField("u")]
    public uint RequiredPlayerRank2 { get; set; }

    [DbField("u")]
    public uint RequiredFaction { get; set; }

    [DbField("u")]
    public uint RequiredFactionStanding { get; set; }

    [DbField("u")]
    public uint Unique { get; set; }

    [DbField("u")]
    public uint MaxCount { get; set; }

    [DbField("u")]
    public uint ContainerSlots { get; set; }

    [DbField("u")]
    public uint StatsCount { get; set; }

    [DbField("u", Length = 20)]
    public uint[] StatsData { get; set; } = new uint[20];

    public ItemStat[] Stats { get; set; } = new ItemStat[10];

    [DbField("f", Length = 15)]
    public float[] DamageData { get; set; } = new float[15];

    public ItemDamage[] Damage { get; set; } = new ItemDamage[5];

    [DbField("u")]
    public uint Armor { get; set; }

    [DbField("u")]
    public uint HolyRes { get; set; }

    [DbField("u")]
    public uint FireRes { get; set; }

    [DbField("u")]
    public uint NatureRes { get; set; }

    [DbField("u")]
    public uint FrostRes { get; set; }

    [DbField("u")]
    public uint ShadowRes { get; set; }

    [DbField("u")]
    public uint ArcaneRes { get; set; }

    [DbField("u")]
    public uint Delay { get; set; }

    [DbField("u")]
    public uint AmmoType { get; set; }

    [DbField("f")]
    public float Range { get; set; }

    [DbField("u", Length = 30)]
    public uint[] SpellsData { get; set; } = new uint[30];

    public ItemSpell[] Spells { get; set; } = new ItemSpell[5];

    [DbField("u")]
    public uint Bonding { get; set; }

    [DbField("s")]
    public string Description { get; set; } = string.Empty;

    [DbField("u")]
    public uint PageId { get; set; }

    [DbField("u")]
    public uint PageLanguage { get; set; }

    [DbField("u")]
    public uint PageMaterial { get; set; }

    [DbField("u")]
    public uint QuestId { get; set; }

    [DbField("u")]
    public uint LockId { get; set; }

    [DbField("u")]
    public uint LockMaterial { get; set; }

    [DbField("u")]
    public uint SheathID { get; set; }

    [DbField("u")]
    public uint RandomPropId { get; set; }

    [DbField("u")]
    public uint Block { get; set; }

    [DbField("u")]
    public uint ItemSet { get; set; }

    [DbField("u")]
    public uint MaxDurability { get; set; }

    [DbField("u")]
    public uint ZoneNameID { get; set; }

    [DbField("u")]
    public uint MapID { get; set; }

    [DbField("u")]
    public uint BagFamily { get; set; }

    [DbField("u")]
    public uint TotemCategory { get; set; }

    [DbField("u", Length = 9)]
    public uint[] SocketsData { get; set; } = new uint[9];

    public SocketInfo[] Sockets { get; set; } = new SocketInfo[3];

    [DbField("u")]
    public uint SocketBonus { get; set; }

    [DbField("u")]
    public uint GemProperties { get; set; }

    [DbField("i")]
    public int DisenchantReqSkill { get; set; }

    [DbField("u")]
    public uint ArmorDamageModifier { get; set; }

    [DbField("u")]
    public uint ExistingDuration { get; set; }

    [DbField("u")]
    public uint ItemLimitCategory { get; set; }

    [DbField("u")]
    public uint HolidayId { get; set; }

    [DbField("u")]
    public uint Unk203_1 { get; set; }

    [DbField("u")]
    public uint Unk201_3 { get; set; }

    [DbField("u")]
    public uint Unk201_5 { get; set; }

    [DbField("u")]
    public uint Unk201_7 { get; set; }

    [DbField("u")]
    public uint Unk2 { get; set; }

    public ItemPrototype()
    {
        for (int i = 0; i < 10; i++)
            Stats[i] = new ItemStat();

        for (int i = 0; i < 5; i++)
            Damage[i] = new ItemDamage();

        for (int i = 0; i < 5; i++)
            Spells[i] = new ItemSpell();

        for (int i = 0; i < 3; i++)
            Sockets[i] = new SocketInfo();
    }

    public void ParseStats()
    {
        if (StatsData == null)
        {
            CLog.Error("ItemPrototype", "StatsData est null.");
            StatsData = new uint[20];
        }

        if (Stats == null)
        {
            CLog.Error("ItemPrototype", "Stats est null.");
            Stats = new ItemStat[10];
        }

        for (int i = 0; i < 10; i++)
        {
            if (Stats[i] == null)
                Stats[i] = new ItemStat();

            if (i * 2 + 1 >= StatsData.Length)
            {
                CLog.Error("ItemPrototype", "StatsData n'a pas assez d'éléments.");
                break;
            }

            Stats[i].Type = StatsData[i * 2];
            Stats[i].Value = (int)StatsData[i * 2 + 1];
        }
    }

    public void ParseDamage()
    {
        if (DamageData == null)
        {
            CLog.Error("ItemPrototype", "DamageData est null.");
            DamageData = new float[15];
        }

        if (Damage == null)
        {
            CLog.Error("ItemPrototype", "Damage est null.");
            Damage = new ItemDamage[5];
        }

        for (int i = 0; i < 5; i++)
        {
            if (Damage[i] == null)
                Damage[i] = new ItemDamage();

            if (i * 3 + 2 >= DamageData.Length)
            {
                CLog.Error("ItemPrototype", "DamageData n'a pas assez d'éléments.");
                break;
            }

            Damage[i].Min = DamageData[i * 3];
            Damage[i].Max = DamageData[i * 3 + 1];
            Damage[i].Type = (uint)DamageData[i * 3 + 2];
        }
    }

    public void ParseSpells()
    {
        if (SpellsData == null)
        {
            CLog.Error("ItemPrototype", "SpellsData est null.");
            SpellsData = new uint[30];
        }

        if (Spells == null)
        {
            CLog.Error("ItemPrototype", "Spells est null.");
            Spells = new ItemSpell[5];
        }

        for (int i = 0; i < 5; i++)
        {
            if (Spells[i] == null)
                Spells[i] = new ItemSpell();

            if (i * 6 + 5 >= SpellsData.Length)
            {
                CLog.Error("ItemPrototype", "SpellsData n'a pas assez d'éléments.");
                break;
            }

            Spells[i].Id = SpellsData[i * 6];
            Spells[i].Trigger = SpellsData[i * 6 + 1];
            Spells[i].Charges = (int)SpellsData[i * 6 + 2];
            Spells[i].Cooldown = (int)SpellsData[i * 6 + 3];
            Spells[i].Category = SpellsData[i * 6 + 4];
            Spells[i].CategoryCooldown = (int)SpellsData[i * 6 + 5];
        }
    }

    public void ParseSockets()
    {
        if (SocketsData == null)
        {
            CLog.Error("ItemPrototype", "SocketsData est null.");
            SocketsData = new uint[9];
        }

        if (Sockets == null)
        {
            CLog.Error("ItemPrototype", "Sockets est null.");
            Sockets = new SocketInfo[3];
        }

        for (int i = 0; i < 3; i++)
        {
            if (Sockets[i] == null)
                Sockets[i] = new SocketInfo();

            if (i * 3 + 1 >= SocketsData.Length)
            {
                CLog.Error("ItemPrototype", "SocketsData n'a pas assez d'éléments.");
                break;
            }

            Sockets[i].SocketColor = SocketsData[i * 3];
            Sockets[i].Unk = SocketsData[i * 3 + 1];
        }
    }
}

public class CreatureInfo
{
    [DbField("u")]
    public uint Id { get; set; }

    [DbField("s")]
    public string Name { get; set; } = string.Empty;

    [DbField("s")]
    public string SubName { get; set; } = string.Empty;

    [DbField("s")]
    public string InfoStr { get; set; } = string.Empty;

    [DbField("u")]
    public uint Flags1 { get; set; }

    [DbField("u")]
    public uint Type { get; set; }

    [DbField("u")]
    public uint Family { get; set; }

    [DbField("u")]
    public uint Rank { get; set; }

    [DbField("u")]
    public uint Unknown1 { get; set; }

    [DbField("u")]
    public uint SpellDataID { get; set; }

    [DbField("u")]
    public uint Male_DisplayID { get; set; }

    [DbField("u")]
    public uint Female_DisplayID { get; set; }

    [DbField("u")]
    public uint Male_DisplayID2 { get; set; }

    [DbField("u")]
    public uint Female_DisplayID2 { get; set; }

    [DbField("f")]
    public float UnkFloat1 { get; set; }

    [DbField("f")]
    public float UnkFloat2 { get; set; }

    [DbField("c")]
    public byte Civilian { get; set; }

    [DbField("c")]
    public byte Leader { get; set; }
}

public class GameObjectInfo
{
    [DbField("u")]
    public uint ID { get; set; }

    [DbField("u")]
    public uint Type { get; set; }

    [DbField("u")]
    public uint DisplayID { get; set; }

    [DbField("s")]
    public string Name { get; set; } = string.Empty;

    [DbField("u")]
    public uint SpellFocus { get; set; }

    [DbField("u", Length = 9)]
    public uint[] Sounds { get; set; } = new uint[9];

    [DbField("u", Length = 14)]
    public uint[] Unknowns { get; set; } = new uint[14];

    [DbField("f")]
    public float Unknown15 { get; set; }
}

public class ItemPage
{
    [DbField("u")]
    public uint Id { get; set; }

    [DbField("s")]
    public string Text { get; set; } = string.Empty;

    [DbField("u")]
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
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Objectives { get; set; } = string.Empty;
    public string CompletionText { get; set; } = string.Empty;
    public string IncompleteText { get; set; } = string.Empty;
    public string EndText { get; set; } = string.Empty;
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
    [DbField("u")]
    public uint MapId { get; set; }

    [DbField("u")]
    public uint ScreenId { get; set; }

    [DbField("u")]
    public uint Type { get; set; }

    [DbField("u")]
    public uint PlayerLimit { get; set; }

    [DbField("u")]
    public uint MinLevel { get; set; }

    [DbField("f")]
    public float RepopX { get; set; }

    [DbField("f")]
    public float RepopY { get; set; }

    [DbField("f")]
    public float RepopZ { get; set; }

    [DbField("u")]
    public uint RepopMapId { get; set; }

    [DbField("s")]
    public string Name { get; set; } = string.Empty;

    [DbField("u")]
    public uint Flags { get; set; }

    [DbField("u")]
    public uint Cooldown { get; set; }

    [DbField("u")]
    public uint RequiredQuestA1 { get; set; }

    [DbField("u")]
    public uint RequiredQuestH2 { get; set; }

    [DbField("u")]
    public uint RequiredItem { get; set; }

    [DbField("u")]
    public uint HeroicKey1 { get; set; }

    [DbField("u")]
    public uint HeroicKey2 { get; set; }

    [DbField("f")]
    public float UpdateDistance { get; set; }

    [DbField("u")]
    public uint CheckpointId { get; set; }

    [DbField("u")]
    public uint Collision { get; set; }
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
    private readonly Dictionary<uint, string> friends = new();
    private readonly HashSet<uint> ignores = new();
    private readonly HashSet<uint> hasFriendList = new();

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
    public WorkerServerSocket ServerSocket { get; set; }

    public Instance() { }
    public Instance(uint instanceId, uint mapId, WorkerServer server)
    {
        InstanceId = instanceId;
        MapId = mapId;
        Server = server;
    }
}
