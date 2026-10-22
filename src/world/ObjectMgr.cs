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
using System.Globalization;
using System.IO;
using WaadShared;
using WaadShared.AuthCodes;
using WaadShared.Database;
using static DBCStores;
using static WaadShared.ObjectMgrMessages;

namespace WaadWorldServer;

public sealed class ObjectMgr : Singleton<ObjectMgr>
{
    private readonly Logger sLog = new();

    public void Update()
    {
        // Mise à jour des objets
    }

    public void Dispose()
    {
        sLog.OutDebug("[ObjectMgr] Nettoyage des objets...");
    }

    public static DBCStorage<SkillLineLookupEntry> DbcSkillLineEntry { get; } = new DBCStorage<SkillLineLookupEntry>();
    public static DBCStorage<FactionReputationEntry> DbcFactionEntry { get; } = new DBCStorage<FactionReputationEntry>();

    private bool _playerDbcsLoaded;
    private readonly Dictionary<(byte Race, byte Class), PlayerCreateInfo> _playerCreateInfos = [];
    private readonly object _characterCreateLock = new();

    public bool LoadPlayerDbcs(string dbcPath)
    {
        if (!IsDbcLoaded)
            return false;
        if (_playerDbcsLoaded)
            return true;

        bool loaded = DbcSkillLineEntry.Load(Path.Combine(dbcPath, "SkillLine.dbc"), SkillLineEntryFormat, true, true)
            && DbcFactionEntry.Load(Path.Combine(dbcPath, "Faction.dbc"), FactionDBCFormat, true, true);
        if (!loaded)
        {
            sLog.OutError("[ObjectMgr] Impossible de charger les DBC de compétences et de factions.");
            return false;
        }

        _playerDbcsLoaded = true;
        return true;
    }

    public bool LoadPlayerCreateInfos()
    {
        Database database = WorldDatabaseManager.GetWorldDatabase();
        QueryResult result = database?.Query("SELECT * FROM playercreateinfo");
        if (result == null)
        {
            sLog.OutError(W_E_OBJMGR_PLAYERCREATEINFO_LOAD_FAILED);
            return false;
        }

        var createInfos = new Dictionary<(byte Race, byte Class), PlayerCreateInfo>();
        var actionBarsByClass = new Dictionary<byte, List<CreateInfoActionBar>>();
        try
        {
            while (result.NextRow())
            {
                if (result.FieldCount < 24)
                {
                    sLog.OutError(W_E_OBJMGR_PLAYERCREATEINFO_TOO_FEW_COLUMNS);
                    return false;
                }

                uint index = ReadUInt32(result, 0);
                var info = new PlayerCreateInfo
                {
                    Index = index,
                    Race = (byte)ReadUInt32(result, 1),
                    FactionTemplate = ReadUInt32(result, 2),
                    Class = (byte)ReadUInt32(result, 3),
                    MapId = ReadUInt32(result, 4),
                    ZoneId = ReadUInt32(result, 5),
                    PositionX = ReadSingle(result, 6),
                    PositionY = ReadSingle(result, 7),
                    PositionZ = ReadSingle(result, 8),
                    DisplayId = ReadUInt32(result, 9),
                    Strength = ReadUInt32(result, 10),
                    Agility = ReadUInt32(result, 11),
                    Stamina = ReadUInt32(result, 12),
                    Intellect = ReadUInt32(result, 13),
                    Spirit = ReadUInt32(result, 14),
                    Health = ReadUInt32(result, 15),
                    Mana = ReadUInt32(result, 16),
                    Rage = ReadUInt32(result, 17),
                    Focus = ReadUInt32(result, 18),
                    Energy = ReadUInt32(result, 19),
                    Runic = ReadUInt32(result, 20),
                    AttackPower = ReadUInt32(result, 21),
                    MinimumDamage = ReadSingle(result, 22),
                    MaximumDamage = ReadSingle(result, 23),
                };

                if (info.Race == 0 || info.Class == 0 || !createInfos.TryAdd((info.Race, info.Class), info))
                {
                    sLog.OutError(W_E_OBJMGR_PLAYERCREATEINFO_INVALID_OR_DUPLICATE, info.Race, info.Class);
                    return false;
                }

                QueryResult skills = database.Query("SELECT * FROM playercreateinfo_skills WHERE indexid={0}", index);
                while (skills != null && skills.NextRow())
                    info.Skills.Add(new(ReadUInt32(skills, 1), ReadUInt32(skills, 2), ReadUInt32(skills, 3)));

                QueryResult spells = database.Query("SELECT * FROM playercreateinfo_spells WHERE indexid={0}", index);
                while (spells != null && spells.NextRow())
                    info.Spells.Add(ReadUInt32(spells, 1));

                QueryResult items = database.Query("SELECT * FROM playercreateinfo_items WHERE indexid={0}", index);
                while (items != null && items.NextRow())
                    info.Items.Add(new(ReadUInt32(items, 1), (byte)ReadUInt32(items, 2), ReadUInt32(items, 3)));

                if (!actionBarsByClass.TryGetValue(info.Class, out List<CreateInfoActionBar> actionBars))
                {
                    actionBars = [];
                    QueryResult bars = database.Query("SELECT * FROM playercreateinfo_bars WHERE class={0}", info.Class);
                    while (bars != null && bars.NextRow())
                        actionBars.Add(new(ReadUInt32(bars, 2), ReadUInt32(bars, 3), ReadUInt32(bars, 4), ReadUInt32(bars, 5)));
                    actionBarsByClass.Add(info.Class, actionBars);
                }
                info.ActionBars.AddRange(actionBars);
            }
        }
        catch (Exception ex)
        {
            sLog.OutError(W_E_OBJMGR_PLAYERCREATEINFO_LOAD_ERROR, ex.Message);
            return false;
        }

        if (createInfos.Count == 0)
        {
            sLog.OutError(W_E_OBJMGR_PLAYERCREATEINFO_NONE_LOADED);
            return false;
        }

        _playerCreateInfos.Clear();
        foreach (var entry in createInfos)
            _playerCreateInfos.Add(entry.Key, entry.Value);

        sLog.OutString(W_N_OBJMGR_PLAYERCREATEINFO_LOADED, _playerCreateInfos.Count);
        return true;
    }

    public bool TryGetPlayerCreateInfo(byte race, byte classId, out PlayerCreateInfo info) =>
        _playerCreateInfos.TryGetValue((race, classId), out info);

    public LoginErrorCode CreatePlayerCharacter(uint accountId, WorldPacket request) =>
        CreatePlayerCharacter(accountId, request, out _);

    public LoginErrorCode CreatePlayerCharacter(uint accountId, WorldPacket request, out uint createdGuid)
    {
        createdGuid = 0;
        lock (_characterCreateLock)
        {
            try
            {
                if (request.GetOpcode() != (ushort)Opcodes.CMSG_CHAR_CREATE)
                    return LoginErrorCode.CHAR_CREATE_ERROR;

                string name = request.ReadString();
                byte race = request.ReadByte();
                byte classId = request.ReadByte();
                byte gender = request.ReadByte();
                byte skin = request.ReadByte();
                byte face = request.ReadByte();
                byte hairStyle = request.ReadByte();
                byte hairColor = request.ReadByte();
                byte facialHair = request.ReadByte();
                byte outfit = request.ReadByte();

                if (request.Size != System.Text.Encoding.ASCII.GetByteCount(name) + 10
                    || name.Length is < 3 or > 12
                    || !IsValidCharacterName(name))
                    return LoginErrorCode.CHAR_NAME_FAILURE;

                if (!IsValidRaceClassCombination(race, classId)
                    || !TryGetPlayerCreateInfo(race, classId, out PlayerCreateInfo info))
                    return LoginErrorCode.CHAR_CREATE_RESTRICTED_RACECLASS;

                Database database = WorldDatabaseManager.GetCharacterDatabase();
                if (database == null || !database.IsInitialized)
                    return LoginErrorCode.CHAR_CREATE_FAILED;

                QueryResult existingName = database.Query(
                    "SELECT COUNT(*) FROM characters WHERE LOWER(name)=LOWER('{0}')", name);
                if (existingName == null || !existingName.NextRow())
                    return LoginErrorCode.CHAR_CREATE_FAILED;
                if (ReadUInt32(existingName, 0) != 0)
                    return LoginErrorCode.CHAR_CREATE_NAME_IN_USE;

                QueryResult highestGuidResult = database.Query("SELECT COALESCE(MAX(guid),0) FROM characters");
                if (highestGuidResult == null || !highestGuidResult.NextRow())
                    return LoginErrorCode.CHAR_CREATE_FAILED;
                uint highestGuid = ReadUInt32(highestGuidResult, 0);
                if (highestGuid == uint.MaxValue)
                    return LoginErrorCode.CHAR_CREATE_FAILED;

                uint firstItemGuid = 0;
                if (info.Items.Count > 0)
                {
                    QueryResult highestItemGuidResult = database.Query("SELECT COALESCE(MAX(guid),0) FROM playeritems");
                    if (highestItemGuidResult == null || !highestItemGuidResult.NextRow())
                        return LoginErrorCode.CHAR_CREATE_FAILED;
                    uint highestItemGuid = ReadUInt32(highestItemGuidResult, 0);
                    if (highestItemGuid == uint.MaxValue)
                        return LoginErrorCode.CHAR_CREATE_FAILED;
                    firstItemGuid = highestItemGuid + 1;
                }

                var createRequest = new CharacterCreateRequest(
                    name, race, classId, gender, skin, face, hairStyle, hairColor, facialHair, outfit);
                Player player = Player.CreateNewCharacter(highestGuid + 1, accountId, firstItemGuid, info, createRequest);
                if (!player.SaveToDB(newCharacter: true))
                    return LoginErrorCode.CHAR_CREATE_FAILED;

                createdGuid = highestGuid + 1;
                return LoginErrorCode.CHAR_CREATE_SUCCESS;
            }
            catch (Exception ex)
            {
                sLog.OutError(W_E_OBJMGR_CHARACTER_CREATE_FAILED, accountId, ex.Message);
                return LoginErrorCode.CHAR_CREATE_ERROR;
            }
        }
    }

    private static bool IsValidCharacterName(string name)
    {
        foreach (char character in name)
            if (character is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z'))
                return false;
        return true;
    }

    private static uint ReadUInt32(QueryResult result, int index) =>
        Convert.ToUInt32(result.GetValue(index), CultureInfo.InvariantCulture);

    private static float ReadSingle(QueryResult result, int index) =>
        Convert.ToSingle(result.GetValue(index), CultureInfo.InvariantCulture);

    public bool TryGetSkillLine(uint skillId, out SkillLineLookupEntry skillLine)
    {
        if (_playerDbcsLoaded && DbcSkillLineEntry.HasEntry(skillId))
        {
            skillLine = DbcSkillLineEntry.LookupEntry(skillId);
            return true;
        }

        skillLine = default;
        return false;
    }

    public bool TryGetFaction(uint factionId, out FactionReputationEntry faction)
    {
        if (_playerDbcsLoaded && DbcFactionEntry.HasEntry(factionId))
        {
            faction = DbcFactionEntry.LookupEntry(factionId);
            return true;
        }

        faction = default;
        return false;
    }

    public IEnumerable<FactionReputationEntry> GetFactionEntries()
    {
        if (!_playerDbcsLoaded)
            yield break;

        for (uint row = 0; row < DbcFactionEntry.GetNumRows(); row++)
        {
            FactionReputationEntry faction = DbcFactionEntry.LookupRow(row);
            if (faction.ID != 0)
                yield return faction;
        }
    }

    public sealed class SkillLineLookupEntry
    {
        public uint id;
        public uint type;
        public uint unk1;
        public string Name;
        public uint Unused;
    }

    public sealed class FactionReputationEntry
    {
        public uint ID;
        public int RepListId;
        public uint BaseRepRaceMask0;
        public uint BaseRepRaceMask1;
        public uint BaseRepRaceMask2;
        public uint BaseRepRaceMask3;
        public int BaseRepValue0;
        public int BaseRepValue1;
        public int BaseRepValue2;
        public int BaseRepValue3;
        public uint ParentFaction;
        public string Name;
        public uint Unused;
    }

    /// <summary>
    /// Valide qu'une race est valide et existe dans ChrRaces.dbc
    /// </summary>
    public bool IsValidRace(byte race)
    {
        if (race == 0)
            return false;

        // Vérifier que les DBCs sont chargés
        if (!IsDbcLoaded)
        {
            sLog.OutError("[ObjectMgr] Les DBCs ne sont pas chargés, impossibilité de valider la race.");
            return false;
        }

        // Vérifier que la race existe dans ChrRaces.dbc
        return DBCStores.IsValidRace(race);
    }

    /// <summary>
    /// Valide qu'une classe est valide et existe dans ChrClasses.dbc
    /// </summary>
    public bool IsValidClass(byte classId)
    {
        if (classId == 0)
            return false;

        // Vérifier que les DBCs sont chargés
        if (!IsDbcLoaded)
        {
            sLog.OutError("[ObjectMgr] Les DBCs ne sont pas chargés, impossibilité de valider la classe.");
            return false;
        }

        // Vérifier que la classe existe dans ChrClasses.dbc
        return DBCStores.IsValidClass(classId);
    }

    /// <summary>
    /// Récupère l'équipe (faction) pour une race donnée à partir de ChrRaces.dbc
    /// </summary>
    public PlayerTeam GetTeamForRace(byte race)
    {
        if (!IsValidRace(race))
            return PlayerTeam.FACTION_HORDE; // par défaut

        // Utiliser la méthode statique de DBCStores qui retourne TeamId (0=Alliance, 1=Horde)
        uint teamId = GetTeamIdForRace(race);
        return teamId == 0 ? PlayerTeam.FACTION_ALLY : PlayerTeam.FACTION_HORDE;
    }

    /// <summary>
    /// Vérifie si une combinaison race/classe est valide
    /// </summary>
    public bool IsValidRaceClassCombination(byte race, byte classId)
    {
        return IsValidRace(race) && IsValidClass(classId) && _playerCreateInfos.ContainsKey((race, classId));
    }
}

public sealed class PlayerCreateInfo
{
    public uint Index { get; init; }
    public byte Race { get; init; }
    public uint FactionTemplate { get; init; }
    public byte Class { get; init; }
    public uint MapId { get; init; }
    public uint ZoneId { get; init; }
    public float PositionX { get; init; }
    public float PositionY { get; init; }
    public float PositionZ { get; init; }
    public uint DisplayId { get; init; }
    public uint Strength { get; init; }
    public uint Agility { get; init; }
    public uint Stamina { get; init; }
    public uint Intellect { get; init; }
    public uint Spirit { get; init; }
    public uint Health { get; init; }
    public uint Mana { get; init; }
    public uint Rage { get; init; }
    public uint Focus { get; init; }
    public uint Energy { get; init; }
    public uint Runic { get; init; }
    public uint AttackPower { get; init; }
    public float MinimumDamage { get; init; }
    public float MaximumDamage { get; init; }
    public List<CreateInfoSkill> Skills { get; } = [];
    public HashSet<uint> Spells { get; } = [];
    public List<CreateInfoItem> Items { get; } = [];
    public List<CreateInfoActionBar> ActionBars { get; } = [];
}

public readonly record struct CreateInfoSkill(uint SkillId, uint CurrentValue, uint MaximumValue);
public readonly record struct CreateInfoItem(uint ItemId, byte Slot, uint Amount);
public readonly record struct CreateInfoActionBar(uint Button, uint Action, uint Type, uint Misc);