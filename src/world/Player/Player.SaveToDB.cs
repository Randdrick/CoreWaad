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
            MapId = Session.MapId;
            InstanceId = Session.InstanceId;
            IsInWorld = true;
            IsBeingPushed = false;
        }
    }

    public void RemoveFromWorld()
    {
        lock (Session.SyncRoot)
        {
            IsInWorld = false;
            IsBeingPushed = false;
            IsLogoutRooted = false;
        }
    }

    public void Update(uint elapsedMs)
    {
        lock (Session.SyncRoot)
        {
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
            if (newCharacter || !IsLoaded || _characterColumns == null || _characterColumns.Length < 85)
            {
                CLog.Error("[Player]", "Sauvegarde refusee pour {0}: personnage non charge ou creation non portee.", Guid);
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

    private List<DatabaseStatement> BuildSaveStatements(Database database, uint timestamp, uint levelTime, uint totalTime)
    {
        var values = new Dictionary<int, object>
        {
            [7] = Level, [8] = XP, [9] = Csv(ExploredZones),
            [11] = WatchedFactionIndex, [12] = ChosenTitle, [13] = KnownTitles, [14] = KnownTitles1,
            [15] = Coinage, [16] = AmmoId, [17] = Math.Min(CharacterPoints2, 2u), [18] = MaxTalentPoints,
            [19] = LoadHealth, [20] = LoadMana, [21] = PlayerBytes3 >> 24, [22] = PlayerBytes, [23] = PlayerBytes2,
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
        Insert("tutorials", "", new object[] { Guid }.Concat(Tutorials.Cast<object>()).ToArray());

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