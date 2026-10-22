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
using System.Linq;
using WaadShared;
using WaadShared.Database;
using static WaadShared.PlayerMessages;

namespace WaadWorldServer;

public sealed partial class Player
{
    public const long SaveIntervalMs = 300000;
    private string[] _characterColumns;
    private bool _skillsInTable;
    private long _lastPlayedTimeMs;
    private long _nextSaveMs;

    public bool IsLoaded { get; private set; }
    public bool IsInWorld { get; private set; }
    public bool IsBeingPushed { get; set; }
    public bool IsInCombat { get; set; }
    public bool IsDueling { get; set; }
    public bool IsLogoutRooted { get; internal set; }

    public void AddToWorld()
    {
        lock (Session.SyncRoot)
        {
            if (!IsLoaded || Session.IsDisposed)
                throw new InvalidOperationException("Joueur non charge ou session detruite.");
            if (IsInWorld)
                return;
            MapId = Session.MapId;
            InstanceId = Session.InstanceId;
            SendInitialLogonPackets();
            IsInWorld = true;
            IsBeingPushed = false;
            OnTimeSyncRequest();
        }
    }

    public void RemoveFromWorld()
    {
        lock (Session.SyncRoot)
        {
            IsInWorld = false;
            IsBeingPushed = false;
            IsLogoutRooted = false;
            _timeSyncPending = false;
        }
    }

    public void Update(uint elapsedMs)
    {
        lock (Session.SyncRoot)
        {
            SendPendingUpdateFields();

            if (IsInWorld && !Session.IsDisposed && Environment.TickCount64 >= _nextSaveMs)
            {
                if (!SaveToDB())
                    _nextSaveMs = Environment.TickCount64 + 10000;
            }
        }
    }

    public bool SaveToDB(bool newCharacter = false)
    {
        lock (Session.SyncRoot)
        {
            if (newCharacter)
            {
                if (_createInfo == null)
                    return false;

                Database createDatabase = WorldDatabaseManager.GetCharacterDatabase();
                if (createDatabase == null || !createDatabase.IsInitialized)
                    return false;

                try
                {
                    return createDatabase.ExecuteTransaction(BuildCreateStatements(createDatabase));
                }
                catch (Exception ex)
                {
                    CLog.Error("[Player]", W_E_PLAYER_CREATE_FAILED, Guid, ex.Message);
                    return false;
                }
            }

            if (!IsLoaded || _characterColumns == null || _characterColumns.Length < 85)
            {
                CLog.Error("[Player]", W_E_PLAYER_SAVE_REFUSED_NOT_LOADED, Guid);
                return false;
            }

            Database database = WorldDatabaseManager.GetCharacterDatabase();
            if (database == null || !database.IsInitialized)
                return false;

            long nowMs = Environment.TickCount64;
            uint elapsedSeconds = (uint)Math.Max(0, (nowMs - _lastPlayedTimeMs) / 1000);
            uint levelTime = PlayedTime[0] + elapsedSeconds;
            uint totalTime = PlayedTime[1] + elapsedSeconds;
            uint timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            try
            {
                var statements = BuildSaveStatements(database, timestamp, levelTime, totalTime);
                if (!database.ExecuteTransaction(statements))
                    return false;
            }
            catch (Exception ex)
            {
                CLog.Error("[Player]", "Echec de preparation de la sauvegarde du joueur {0}: {1}", Guid, ex.Message);
                return false;
            }

            PlayedTime[0] = levelTime;
            PlayedTime[1] = totalTime;
            _lastPlayedTimeMs += (long)elapsedSeconds * 1000;
            _nextSaveMs = nowMs + SaveIntervalMs;
            if (Session.IsLoggingOut)
                TimeLogoff = timestamp;
            return true;
        }
    }

    private List<DatabaseStatement> BuildCreateStatements(Database database)
    {
        object[] character = new object[85];
        Array.Fill(character, 0u);
        character[0] = Guid;
        character[1] = Session.AccountId;
        character[2] = Name;
        character[3] = Race;
        character[4] = Class;
        character[5] = Gender;
        character[6] = CustomFaction;
        character[7] = Level;
        character[8] = XP;
        character[9] = Csv(ExploredZones);
        character[10] = string.Empty;
        character[11] = WatchedFactionIndex;
        character[12] = ChosenTitle;
        character[13] = KnownTitles;
        character[14] = KnownTitles1;
        character[15] = Coinage;
        character[16] = AmmoId;
        character[17] = CharacterPoints2;
        character[18] = MaxTalentPoints;
        character[19] = Health;
        character[20] = Mana;
        character[21] = PlayerBytes3 >> 24;
        character[22] = PlayerBytes;
        character[23] = PlayerBytes2;
        character[24] = PlayerFlags;
        character[25] = PlayerFieldBytes;
        character[26] = PositionX;
        character[27] = PositionY;
        character[28] = PositionZ;
        character[29] = Orientation;
        character[30] = MapId;
        character[31] = ZoneId;
        character[32] = string.Join(" ", TaxiMask);
        character[33] = Banned;
        character[34] = BanReason;
        character[35] = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        character[36] = 0u;
        character[37] = BindPositionX;
        character[38] = BindPositionY;
        character[39] = BindPositionZ;
        character[40] = BindMapId;
        character[41] = BindZoneId;
        character[42] = IsResting ? 1u : 0u;
        character[43] = RestState;
        character[44] = RestAmount;
        character[45] = "0 0 0 ";
        character[46] = (uint)DeathState;
        character[47] = TalentResetTimes;
        character[48] = FirstLogin ? 1u : 0u;
        character[49] = RenamePending ? 1u : 0u;
        character[50] = ArenaPoints;
        character[51] = StableSlotCount;
        character[52] = InstanceId;
        character[53] = BgEntryPointMap;
        character[54] = BgEntryPointX;
        character[55] = BgEntryPointY;
        character[56] = BgEntryPointZ;
        character[57] = BgEntryPointO;
        character[58] = BgEntryPointInstance;
        character[59] = TaxiPath;
        character[60] = TaxiLastNode;
        character[61] = TaxiMountDisplayId;
        character[62] = TransporterGuid;
        character[63] = TransporterX;
        character[64] = TransporterY;
        character[65] = TransporterZ;
        character[66] = Csv(DeletedSpells.OrderBy(value => value));
        character[67] = Csv(Reputation.OrderBy(entry => entry.Key).SelectMany(entry => new long[]
            { entry.Key, entry.Value.Flag, entry.Value.BaseStanding, entry.Value.Standing }));
        character[68] = Csv(Actions.SelectMany(action => new uint[] { action.Action, action.Type, action.Misc }));
        character[69] = string.Empty;
        character[70] = Csv(FinishedQuests.OrderBy(value => value));
        character[71] = Csv(FinishedDailyQuests.OrderBy(value => value));
        character[72] = HonorRolloverTime;
        character[73] = KillsToday;
        character[74] = KillsYesterday;
        character[75] = KillsLifetime;
        character[76] = HonorToday;
        character[77] = HonorYesterday;
        character[78] = HonorPoints;
        character[79] = InstanceDifficulty;
        character[80] = Height;
        character[81] = Phase;
        character[82] = TalentActiveSpec;
        character[83] = TalentSpecsCount;
        character[84] = NeedTalentReset ? 1u : 0u;

        var statements = new List<DatabaseStatement>();
        AddInsert("characters", character);
        foreach (var skill in Skills.OrderBy(entry => entry.Key))
            AddInsert("playerskills", [Guid, skill.Key, skill.Value.Type, skill.Value.CurrentValue, skill.Value.MaximumValue],
                " (player_guid,skill_id,type,currentlvl,maxlvl)");
        foreach (uint spell in Spells.OrderBy(value => value))
            AddInsert("playerspells", [Guid, spell], " (guid,spellid)");
        AddInsert("tutorials", [Guid, .. Tutorials.Cast<object>()]);
        AddQuestLogStatements(statements);

        for (int spec = 0; spec < Specs.Length; spec++)
            AddInsert("playerglyphs", [Guid, (uint)spec, .. Specs[spec].Glyphs.Cast<object>()]);

        uint itemGuid = _firstStartingItemGuid;
        foreach (CreateInfoItem item in _createInfo.Items)
        {
            if (item.ItemId == 0)
                continue;
            if (itemGuid == 0 || itemGuid == uint.MaxValue)
                throw new InvalidOperationException("Espace des GUID d'objets epuise.");

            AddInsert("playeritems", [Guid, itemGuid++, item.ItemId, 0u, 0u, 0u, item.Amount,
                0u, 0u, 0u, 0u, 0u, 0u, -1, (int)item.Slot, string.Empty]);
        }

        return statements;

        void AddInsert(string table, object[] values, string columns = "")
        {
            var parameters = new Dictionary<string, object>();
            for (int index = 0; index < values.Length; index++)
                parameters["@value" + index] = values[index];
            string placeholders = string.Join(",", parameters.Keys);
            statements.Add(new DatabaseStatement($"INSERT INTO {table}{columns} VALUES ({placeholders})", parameters));
        }
    }

    private List<DatabaseStatement> BuildSaveStatements(Database database, uint timestamp, uint levelTime, uint totalTime)
    {
        var values = new Dictionary<int, object>
        {
            [7] = Level, [8] = XP, [9] = Csv(ExploredZones),
            [11] = WatchedFactionIndex, [12] = ChosenTitle, [13] = KnownTitles, [14] = KnownTitles1,
            [15] = Coinage, [16] = AmmoId, [17] = Math.Min(CharacterPoints2, 2u), [18] = MaxTalentPoints,
            [19] = Health, [20] = Mana, [21] = PlayerBytes3 >> 24, [22] = PlayerBytes, [23] = PlayerBytes2,
            [24] = PlayerFlags & ~0xFu, [25] = PlayerFieldBytes,
            [26] = PositionX, [27] = PositionY, [28] = PositionZ, [29] = Orientation,
            [30] = MapId, [31] = ZoneId, [32] = string.Join(" ", TaxiMask),
            [35] = timestamp, [36] = Session.IsLoggingOut ? 0 : 1,
            [37] = BindPositionX, [38] = BindPositionY, [39] = BindPositionZ, [40] = BindMapId, [41] = BindZoneId,
            [42] = IsResting ? 1 : 0, [43] = RestState, [44] = RestAmount,
            [45] = FormattableString.Invariant($"{levelTime} {totalTime} 0 "), [46] = (uint)DeathState,
            [47] = TalentResetTimes, [48] = FirstLogin ? 1 : 0, [50] = ArenaPoints, [51] = StableSlotCount,
            [52] = InstanceId, [53] = BgEntryPointMap, [54] = BgEntryPointX, [55] = BgEntryPointY,
            [56] = BgEntryPointZ, [57] = BgEntryPointO, [58] = BgEntryPointInstance,
            [59] = TaxiPath, [60] = TaxiLastNode, [61] = TaxiMountDisplayId,
            [62] = TransporterGuid, [63] = TransporterX, [64] = TransporterY, [65] = TransporterZ,
            [66] = Csv(DeletedSpells.OrderBy(value => value)),
            [67] = Csv(Reputation.OrderBy(entry => entry.Key).SelectMany(entry => new long[]
                { entry.Key, entry.Value.Flag, entry.Value.BaseStanding, entry.Value.Standing })),
            [68] = Csv(Actions.SelectMany(action => new uint[] { action.Action, action.Type, action.Misc })),
            [70] = Csv(FinishedQuests.OrderBy(value => value)), [71] = Csv(FinishedDailyQuests.OrderBy(value => value)),
            [72] = HonorRolloverTime, [73] = KillsToday, [74] = KillsYesterday, [75] = KillsLifetime,
            [76] = HonorToday, [77] = HonorYesterday, [78] = HonorPoints,
            [79] = InstanceDifficulty, [80] = Height, [81] = Phase, [82] = TalentActiveSpec, [83] = TalentSpecsCount
        };

        if (!_skillsInTable)
            values[10] = string.Concat(Skills.OrderBy(entry => entry.Key).Select(entry =>
                FormattableString.Invariant($"{entry.Key};{entry.Value.CurrentValue};{entry.Value.MaximumValue};")));

        var parameters = new Dictionary<string, object> { ["@guid"] = Guid, ["@account"] = Session.AccountId };
        var assignments = new List<string>();
        foreach (var entry in values)
        {
            string parameterName = "@field" + entry.Key;
            parameters[parameterName] = entry.Value;
            assignments.Add(database.QuoteIdentifier(_characterColumns[entry.Key]) + "=" + parameterName);
        }

        var statements = new List<DatabaseStatement>
        {
            new("UPDATE characters SET " + string.Join(",", assignments) + " WHERE " +
                database.QuoteIdentifier(_characterColumns[0]) + "=@guid AND " +
                database.QuoteIdentifier(_characterColumns[1]) + "=@account", parameters, true)
        };

        void Delete(string table, string owner, string extra = "") => statements.Add(new(
            $"DELETE FROM {table} WHERE {owner}=@guid{extra}", new Dictionary<string, object> { ["@guid"] = Guid }));
        void Insert(string table, string columns, params object[] row)
        {
            var insertParameters = new Dictionary<string, object>();
            for (int index = 0; index < row.Length; index++)
                insertParameters["@value" + index] = row[index];
            statements.Add(new($"INSERT INTO {table}{columns} VALUES ({string.Join(",", insertParameters.Keys)})", insertParameters));
        }

        if (_skillsInTable)
        {
            Delete("playerskills", "player_guid", " AND type<>10");
            foreach (var entry in Skills.OrderBy(entry => entry.Key))
                if (entry.Value.Type != PlayerConstants.SKILL_TYPE_LANGUAGE)
                    Insert("playerskills", " (player_guid,skill_id,type,currentlvl,maxlvl)",
                        Guid, entry.Key, entry.Value.Type, entry.Value.CurrentValue, entry.Value.MaximumValue);
        }

        Delete("playerspells", "guid");
        foreach (uint spell in Spells.OrderBy(value => value))
            Insert("playerspells", " (guid,spellid)", Guid, spell);

        Delete("playertalents", "guid", " AND spec<2");
        Delete("playerglyphs", "guid", " AND spec<2");
        for (int spec = 0; spec < Specs.Length; spec++)
        {
            foreach (var entry in Specs[spec].Talents.OrderBy(entry => entry.Key))
                Insert("playertalents", $" (guid,spec,tid,{database.QuoteIdentifier("rank")})", Guid, spec, entry.Key, entry.Value);
            Insert("playerglyphs", "", new object[] { Guid, spec }.Concat(Specs[spec].Glyphs.Cast<object>()).ToArray());
        }

        Delete("tutorials", "playerId");
        Insert("tutorials", "", [Guid, .. Tutorials.Cast<object>()]);

        Delete("playercooldowns", "player_guid");
        for (int type = 0; type < Cooldowns.Length; type++)
            foreach (var entry in Cooldowns[type].OrderBy(entry => entry.Key))
                if (entry.Value.ExpireTime >= timestamp && entry.Value.ExpireTime - timestamp >= 10)
                    Insert("playercooldowns", " (player_guid,cooldown_type,cooldown_misc,cooldown_expire_time,cooldown_spellid,cooldown_itemid)",
                        Guid, type, entry.Key, entry.Value.ExpireTime, entry.Value.SpellId, entry.Value.ItemId);
        return statements;
    }

    private static string Csv<T>(IEnumerable<T> values)
    {
        string joined = string.Join(",", values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)));
        return joined.Length == 0 ? string.Empty : joined + ",";
    }
}