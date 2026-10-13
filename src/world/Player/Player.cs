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

namespace WaadWorldServer;

// Portage progressif de Player (Player.h/Player.cpp). Object/Unit ne sont pas encore portés.
public sealed partial class Player(uint guid, ClusterPlayerSession session)
{
    public uint Guid { get; } = guid;
    public ClusterPlayerSession Session { get; } = session;
    private uint[] _uint32Values { get; } = CreateUpdateValues(guid);
    public PlayerUpdateMask UpdateMask { get; } = CreateInitialUpdateMask();
    public ReadOnlyMemory<uint> UInt32Values => _uint32Values;

    private static uint[] CreateUpdateValues(uint guid)
    {
        var values = new uint[EPlayerFields.PLAYER_END];
        values[EObjectFields.OBJECT_FIELD_TYPE] = 0x19;
        values[EObjectFields.OBJECT_FIELD_GUID] = guid;
        return values;
    }

    private static PlayerUpdateMask CreateInitialUpdateMask()
    {
        var mask = new PlayerUpdateMask(EPlayerFields.PLAYER_END);
        mask.SetBit(EObjectFields.OBJECT_FIELD_TYPE);
        mask.SetBit(EObjectFields.OBJECT_FIELD_GUID);
        return mask;
    }

    public uint GetUInt32Value(int index) => _uint32Values[index];

    public void SetUInt32Value(int index, uint value)
    {
        if (_uint32Values[index] == value)
            return;

        _uint32Values[index] = value;
        UpdateMask.SetBit(index);
    }

    public ulong GetUInt64Value(int index) =>
        GetUInt32Value(index) | ((ulong)GetUInt32Value(index + 1) << 32);

    public void SetUInt64Value(int index, ulong value)
    {
        SetUInt32Value(index, (uint)value);
        SetUInt32Value(index + 1, (uint)(value >> 32));
    }

    public float GetFloatValue(int index) => BitConverter.UInt32BitsToSingle(GetUInt32Value(index));

    public void SetFloatValue(int index, float value) =>
        SetUInt32Value(index, BitConverter.SingleToUInt32Bits(value));

    private byte GetByteValue(int index, int offset) =>
        (byte)(GetUInt32Value(index) >> (offset * 8));

    private void SetByteValue(int index, int offset, byte value)
    {
        int shift = offset * 8;
        uint mask = 0xFFu << shift;
        SetUInt32Value(index, (GetUInt32Value(index) & ~mask) | ((uint)value << shift));
    }

    internal void SetExploredZones(IEnumerable<uint> zones)
    {
        for (int clearIndex = 0; clearIndex < EPlayerFields.PLAYER_EXPLORED_ZONES_MAX; clearIndex++)
            SetUInt32Value(EPlayerFields.PLAYER_EXPLORED_ZONES_1 + clearIndex, 0);

        int index = 0;
        foreach (uint value in zones)
        {
            if (index == EPlayerFields.PLAYER_EXPLORED_ZONES_MAX)
                break;

            SetUInt32Value(EPlayerFields.PLAYER_EXPLORED_ZONES_1 + index, value);
            index++;
        }
    }

    private IEnumerable<uint> ReadExploredZones()
    {
        for (int index = 0; index < EPlayerFields.PLAYER_EXPLORED_ZONES_MAX; index++)
            yield return GetUInt32Value(EPlayerFields.PLAYER_EXPLORED_ZONES_1 + index);
    }

    private void ClearUpdateMask() => UpdateMask.ClearAll();

    // Identité
    public string Name { get; private set; } = string.Empty;
    public byte Race
    {
        get => GetByteValue(EUnitFields.UNIT_FIELD_BYTES_0, 0);
        private set => SetByteValue(EUnitFields.UNIT_FIELD_BYTES_0, 0, value);
    }
    public byte Class
    {
        get => GetByteValue(EUnitFields.UNIT_FIELD_BYTES_0, 1);
        private set => SetByteValue(EUnitFields.UNIT_FIELD_BYTES_0, 1, value);
    }
    public byte Gender
    {
        get => GetByteValue(EUnitFields.UNIT_FIELD_BYTES_0, 2);
        private set => SetByteValue(EUnitFields.UNIT_FIELD_BYTES_0, 2, value);
    }
    public uint CustomFaction { get; private set; }
    public PlayerTeam Team { get; private set; }
    public PlayerTeam BgTeam { get; private set; }

    // Progression
    public uint Level
    {
        get => GetUInt32Value(EUnitFields.UNIT_FIELD_LEVEL);
        private set => SetUInt32Value(EUnitFields.UNIT_FIELD_LEVEL, value);
    }
    public uint XP
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_XP);
        private set => SetUInt32Value(EPlayerFields.PLAYER_XP, value);
    }
    public uint WatchedFactionIndex
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FIELD_WATCHED_FACTION_INDEX);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FIELD_WATCHED_FACTION_INDEX, value);
    }
    public uint ChosenTitle
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_CHOSEN_TITLE);
        private set => SetUInt32Value(EPlayerFields.PLAYER_CHOSEN_TITLE, value);
    }
    public ulong KnownTitles
    {
        get => GetUInt64Value(EPlayerFields.PLAYER__FIELD_KNOWN_TITLES);
        private set => SetUInt64Value(EPlayerFields.PLAYER__FIELD_KNOWN_TITLES, value);
    }
    public ulong KnownTitles1
    {
        get => GetUInt64Value(EPlayerFields.PLAYER__FIELD_KNOWN_TITLES1);
        private set => SetUInt64Value(EPlayerFields.PLAYER__FIELD_KNOWN_TITLES1, value);
    }

    public bool HasKnownTitle(uint titleNumber)
    {
        if (!TryGetKnownTitleMask(titleNumber, out bool secondField, out ulong mask))
            return false;

        return ((secondField ? KnownTitles1 : KnownTitles) & mask) != 0;
    }

    public void AddKnownTitle(uint titleNumber)
    {
        if (!TryGetKnownTitleMask(titleNumber, out bool secondField, out ulong mask))
            return;

        if (secondField)
            KnownTitles1 |= mask;
        else
            KnownTitles |= mask;
    }

    public void RemoveKnownTitle(uint titleNumber)
    {
        if (!TryGetKnownTitleMask(titleNumber, out bool secondField, out ulong mask))
            return;

        if (secondField)
            KnownTitles1 &= ~mask;
        else
            KnownTitles &= ~mask;

        if (ChosenTitle == titleNumber)
            ChosenTitle = 0;
    }

    internal bool TrySetChosenTitle(uint titleNumber)
    {
        if (titleNumber != 0 && !HasKnownTitle(titleNumber))
            return false;

        ChosenTitle = titleNumber;
        return true;
    }

    private static bool TryGetKnownTitleMask(uint titleNumber, out bool secondField, out ulong mask)
    {
        secondField = titleNumber >= 64;
        mask = 0;
        if (titleNumber == 0 || titleNumber >= 128)
            return false;

        int bit = (int)(titleNumber % 64);
        mask = 1UL << bit;
        return true;
    }

    public uint Coinage
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FIELD_COINAGE);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FIELD_COINAGE, value);
    }
    public uint AmmoId
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_AMMO_ID);
        private set => SetUInt32Value(EPlayerFields.PLAYER_AMMO_ID, value);
    }
    public uint CharacterPoints2
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_CHARACTER_POINTS2);
        private set => SetUInt32Value(EPlayerFields.PLAYER_CHARACTER_POINTS2, value);
    }
    public uint ArmorProficiency { get; private set; } = 1;
    public uint WeaponProficiency { get; private set; } = 0x4000;
    public ushort MaxTalentPoints { get; private set; }
    public uint LoadHealth { get; private set; }
    public uint LoadMana { get; private set; }
    public uint Health
    {
        get => GetUInt32Value(EUnitFields.UNIT_FIELD_HEALTH);
        private set => SetUInt32Value(EUnitFields.UNIT_FIELD_HEALTH, value);
    }
    public uint Mana
    {
        get => GetUInt32Value(EUnitFields.UNIT_FIELD_POWER1);
        private set => SetUInt32Value(EUnitFields.UNIT_FIELD_POWER1, value);
    }
    public uint ArenaPoints
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FIELD_ARENA_CURRENCY);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FIELD_ARENA_CURRENCY, value);
    }
    public byte RestState
    {
        get => GetByteValue(EPlayerFields.PLAYER_BYTES_2, 3);
        private set => SetByteValue(EPlayerFields.PLAYER_BYTES_2, 3, value);
    }

    // Apparence / flags
    public uint PlayerBytes
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_BYTES);
        private set => SetUInt32Value(EPlayerFields.PLAYER_BYTES, value);
    }
    public uint PlayerBytes2
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_BYTES_2);
        private set => SetUInt32Value(EPlayerFields.PLAYER_BYTES_2, value);
    }
    public uint PlayerBytes3
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_BYTES_3);
        private set => SetUInt32Value(EPlayerFields.PLAYER_BYTES_3, value);
    }
    public uint PlayerFlags
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FLAGS);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FLAGS, value);
    }
    public uint PlayerFieldBytes
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FIELD_BYTES);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FIELD_BYTES, value);
    }

    // Position
    public float PositionX { get; private set; }
    public float PositionY { get; private set; }
    public float PositionZ { get; private set; }
    public float Orientation { get; private set; }
    public uint MapId { get; private set; }
    public uint ZoneId { get; private set; }
    public uint InstanceId { get; private set; }
    public uint Phase { get; private set; } = 1;
    public float Height { get; private set; } = 1.0f;

    // Point de lien (pierre de foyer)
    public float BindPositionX { get; private set; }
    public float BindPositionY { get; private set; }
    public float BindPositionZ { get; private set; }
    public uint BindMapId { get; private set; }
    public uint BindZoneId { get; private set; }

    // Repos / temps de jeu
    public bool IsResting { get; private set; }
    public uint RestAmount { get; private set; }
    public uint[] PlayedTime { get; } = new uint[2];
    public uint TimeLogoff { get; private set; }

    // État
    public DeathState DeathState { get; private set; }
    public uint Banned { get; private set; }
    public string BanReason { get; private set; } = string.Empty;
    public uint TalentResetTimes { get; private set; }
    public bool FirstLogin { get; private set; }
    public bool RenamePending { get; private set; }
    public uint StableSlotCount { get; private set; }
    public uint InstanceDifficulty { get; private set; }

    // Point d'entrée champ de bataille
    public uint BgEntryPointMap { get; private set; }
    public float BgEntryPointX { get; private set; }
    public float BgEntryPointY { get; private set; }
    public float BgEntryPointZ { get; private set; }
    public float BgEntryPointO { get; private set; }
    public uint BgEntryPointInstance { get; private set; }

    // Taxi / transport
    public uint TaxiPath { get; private set; }
    public uint TaxiLastNode { get; private set; }
    public uint TaxiMountDisplayId { get; private set; }
    public uint[] TaxiMask { get; } = new uint[PlayerConstants.TAXIMASK_SIZE];
    public uint TransporterGuid { get; private set; }
    public float TransporterX { get; private set; }
    public float TransporterY { get; private set; }
    public float TransporterZ { get; private set; }

    public bool SetTaximaskNode(uint nodeIndex)
    {
        if (nodeIndex == 0 || nodeIndex > (uint)(TaxiMask.Length * sizeof(uint) * 8))
            return false;

        uint zeroBasedIndex = nodeIndex - 1;
        int wordIndex = (int)(zeroBasedIndex / 32);
        uint mask = 1u << (int)(zeroBasedIndex % 32);
        if ((TaxiMask[wordIndex] & mask) != 0)
            return false;

        TaxiMask[wordIndex] |= mask;
        return true;
    }

    // Honneur
    public uint HonorRolloverTime { get; private set; }
    public uint KillsToday { get; private set; }
    public uint KillsYesterday { get; private set; }
    public uint KillsLifetime
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FIELD_LIFETIME_HONORBALE_KILLS);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FIELD_LIFETIME_HONORBALE_KILLS, value);
    }
    public uint HonorToday
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FIELD_TODAY_CONTRIBUTION);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FIELD_TODAY_CONTRIBUTION, value);
    }
    public uint HonorYesterday
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FIELD_YESTERDAY_CONTRIBUTION);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FIELD_YESTERDAY_CONTRIBUTION, value);
    }
    public uint HonorPoints
    {
        get => GetUInt32Value(EPlayerFields.PLAYER_FIELD_HONOR_CURRENCY);
        private set => SetUInt32Value(EPlayerFields.PLAYER_FIELD_HONOR_CURRENCY, value);
    }
    public IEnumerable<uint> ExploredZones => ReadExploredZones();

    // Talents
    public uint TalentActiveSpec { get; private set; }
    public uint TalentSpecsCount { get; private set; } = 1;
    public bool NeedTalentReset { get; private set; }
    public PlayerSpec[] Specs { get; } = [new PlayerSpec(), new PlayerSpec()];

    // Collections chargées depuis la base
    public uint[] Tutorials { get; } = new uint[PlayerConstants.TUTORIALS_COUNT];
    public ActionButton[] Actions { get; } = new ActionButton[PlayerConstants.PLAYER_ACTION_BUTTON_COUNT];
    public Dictionary<uint, PlayerSkill> Skills { get; } = [];
    public HashSet<uint> Spells { get; } = [];
    public HashSet<uint> DeletedSpells { get; } = [];
    public Dictionary<uint, FactionReputation> Reputation { get; } = [];
    public List<LoginAura> LoginAuras { get; } = [];
    public HashSet<uint> FinishedQuests { get; } = [];
    public HashSet<uint> FinishedDailyQuests { get; } = [];
    public Dictionary<uint, PlayerCooldown>[] Cooldowns { get; } = [[], [], []];
    public Dictionary<uint, string> Friends { get; } = [];
    public HashSet<uint> HasFriendList { get; } = [];
    public HashSet<uint> Ignores { get; } = [];

    internal void ApplySkillProficiency(uint skillId)
    {
        if (!PlayerConstants.TryGetProficiencyForSkill(skillId, out byte itemClass, out uint subclassMask))
            return;

        if (itemClass == 4)
            ArmorProficiency |= subclassMask;
        else
            WeaponProficiency |= subclassMask;
    }

    public uint GetTutorialInt(uint tutorialId) =>
        tutorialId < (uint)Tutorials.Length ? Tutorials[(int)tutorialId] : 0;

    public void SetTutorialInt(uint tutorialId, uint value)
    {
        if (tutorialId < (uint)Tutorials.Length)
            Tutorials[(int)tutorialId] = value;
    }

    public void AddToFinishedQuests(uint questId) => FinishedQuests.Add(questId);

    public bool HasFinishedQuest(uint questId) => FinishedQuests.Contains(questId);

    public bool GetQuestRewardStatus(uint questId) => HasFinishedQuest(questId);

    public bool AddToFinishedDailyQuests(uint questId)
    {
        if (questId == 0 || FinishedDailyQuests.Contains(questId) || FinishedDailyQuests.Count >= PlayerConstants.MAX_DAILY_QUESTS)
            return false;

        int slot = FinishedDailyQuests.Count;
        FinishedDailyQuests.Add(questId);
        SetUInt32Value(EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1 + slot, questId);
        return true;
    }

    public bool HasFinishedDailyQuest(uint questId) => FinishedDailyQuests.Contains(questId);

    public void WipeDailyQuests()
    {
        for (int slot = 0; slot < PlayerConstants.MAX_DAILY_QUESTS; slot++)
            SetUInt32Value(EPlayerFields.PLAYER_FIELD_DAILY_QUESTS_1 + slot, 0);
        FinishedDailyQuests.Clear();
    }
}

public sealed class PlayerUpdateMask
{
    private readonly uint[] _words;

    public PlayerUpdateMask(int bitCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bitCount);

        BitCount = bitCount;
        _words = new uint[(bitCount + 31) / 32];
    }

    public int BitCount { get; }
    public int WordCount => _words.Length;
    public ReadOnlyMemory<uint> Words => _words;
    public bool IsEmpty
    {
        get
        {
            foreach (uint word in _words)
                if (word != 0)
                    return false;
            return true;
        }
    }

    public uint GetWord(int index) => _words[index];

    public bool IsSet(int index)
    {
        ValidateIndex(index);
        return (_words[index >> 5] & (1u << (index & 31))) != 0;
    }

    public void SetBit(int index)
    {
        ValidateIndex(index);
        _words[index >> 5] |= 1u << (index & 31);
    }

    public void ClearBit(int index)
    {
        ValidateIndex(index);
        _words[index >> 5] &= ~(1u << (index & 31));
    }

    public void ClearAll() => Array.Clear(_words);

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)BitCount)
            throw new ArgumentOutOfRangeException(nameof(index));
    }
}
