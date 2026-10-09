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
using System.Globalization;
using System.Threading.Tasks;
using WaadShared;
using WaadShared.Database;

using static WaadShared.PlayerMessages;

namespace WaadWorldServer;

// Portage de Player::LoadFromDB / Player::LoadFromDBProc (Player.cpp).
public sealed partial class Player
{
    // L'ordre des requêtes est important : LoadFromDBProc les indexe par position.
    private const int Q_CHARACTER = 0;
    private const int Q_TUTORIALS = 1;
    private const int Q_COOLDOWNS = 2;
    private const int Q_SOCIAL_FRIENDS = 8;
    private const int Q_SOCIAL_FRIENDED_BY = 9;
    private const int Q_SOCIAL_IGNORES = 10;
    private const int Q_SKILLS = 12;
    private const int Q_TALENTS = 14;
    private const int Q_GLYPHS = 15;
    private const int Q_SPELLS = 16;

    // characters.banned (fields[33] côté C++).
    private const int FIELD_BANNED = 33;

    // Auras jamais restaurées à la connexion (cf. C++).
    private const uint AURA_IGNORED_1 = 43869;
    private const uint AURA_IGNORED_2 = 43958;

    // Charge le joueur en asynchrone ; onLoaded(player, succès) est appelé sur le thread de chargement.
    public void LoadFromDB(Action<Player, bool> onLoaded)
    {
        uint guid = Guid;
        Database characterDatabase = WorldDatabaseManager.GetCharacterDatabase();
        string rankColumn = characterDatabase?.QuoteIdentifier("rank") ?? "\"rank\"";
        string[] queries =
        [
            /*[ 0]*/ $"SELECT * FROM characters WHERE guid={guid} AND forced_rename_pending = 0",
            /*[ 1]*/ $"SELECT * FROM tutorials WHERE playerId={guid}",
            /*[ 2]*/ $"SELECT cooldown_type, cooldown_misc, cooldown_expire_time, cooldown_spellid, cooldown_itemid FROM playercooldowns WHERE player_guid={guid}",
            /*[ 3]*/ $"SELECT * FROM questlog WHERE player_guid={guid}",
            /*[ 4]*/ $"SELECT * FROM playeritems WHERE ownerguid={guid} ORDER BY containerslot ASC",
            /*[ 5]*/ $"SELECT * FROM playerpets WHERE ownerguid={guid} ORDER BY petnumber",
            /*[ 6]*/ $"SELECT * FROM playersummonspells where ownerguid={guid} ORDER BY entryid",
            /*[ 7]*/ $"SELECT * FROM mailbox WHERE player_guid = {guid}",
            /*[ 8]*/ $"SELECT friend_guid, note FROM social_friends WHERE character_guid = {guid}",
            /*[ 9]*/ $"SELECT character_guid FROM social_friends WHERE friend_guid = {guid}",
            /*[10]*/ $"SELECT ignore_guid FROM social_ignores WHERE character_guid = {guid}",
            /*[11]*/ $"SELECT * from achievements WHERE player = {guid}",
            /*[12]*/ $"SELECT * FROM playerskills WHERE player_guid = {guid} AND type <> {PlayerConstants.SKILL_TYPE_LANGUAGE} ORDER BY skill_id ASC, currentlvl DESC",
            /*[13]*/ $"SELECT * FROM playerpetactionbar WHERE ownerguid={guid} ORDER BY petnumber",
            /*[14]*/ $"SELECT spec, tid, {rankColumn} FROM playertalents WHERE guid = {guid}",
            /*[15]*/ $"SELECT * FROM playerglyphs WHERE guid = {guid}",
            /*[16]*/ $"SELECT spellid FROM playerspells WHERE guid = {guid}",
        ];

        Task.Run(() =>
        {
            bool ok;
            try
            {
                Database db = characterDatabase;
                if (db == null || !db.IsInitialized)
                {
                    CLog.Error("[Player]", string.Format(W_E_PLAYER_CHAR_DB_UNAVAILABLE, guid));
                    ok = false;
                }
                else
                {
                    // Database.Query() matérialise les lignes (contrairement à AsyncQuery/FQuery côté MySQL).
                    var results = new QueryResult[queries.Length];
                    for (int i = 0; i < queries.Length; i++)
                        results[i] = db.Query(queries[i]);

                    ok = LoadFromDBProc(results);
                }
            }
            catch (Exception ex)
            {
                CLog.Error("[Player]", string.Format(W_E_PLAYER_LOAD_EXCEPTION, guid, ex.Message));
                ok = false;
            }

            onLoaded?.Invoke(this, ok);
        });
    }

    private bool LoadFromDBProc(QueryResult[] results)
    {
        QueryResult result = results[Q_CHARACTER];
        if (result == null || !result.NextRow())
        {
            CLog.Error("[Player]", string.Format(W_E_PLAYER_LOGIN_FAILED_GUID_NOT_FOUND, Guid));
            return false;
        }

        var f = new FieldReader(result);

        uint acct = ToUInt32(result.GetValue(1));
        if (acct != Session.AccountId)
        {
            CLog.Warning("[Player]", string.Format(W_W_PLAYER_WRONG_ACCOUNT, Session.AccountId, Guid, acct));
            return false;
        }

        uint banned = ToUInt32(result.GetValue(FIELD_BANNED));
        if (banned != 0 && (banned < 10 || banned > (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
        {
            CLog.Notice("[Player]", string.Format(W_N_PLAYER_BANNED, Guid));
            return false;
        }

        f.Index = 2;
        Name = f.NextString();
        Race = f.NextByte();
        Class = f.NextByte();
        Gender = f.NextByte();
        CustomFaction = f.NextUInt32();

        // Valider race/classe via ChrRaces.dbc/ChrClasses.dbc
        var objectMgr = ObjectMgr.GetInstance();
        if (!objectMgr.IsValidRaceClassCombination(Race, Class))
        {
            CLog.Error("[Player]", string.Format(W_E_PLAYER_INVALID_RACE_CLASS, Guid, Race, Class));
            return false;
        }

        Team = BgTeam = ObjectMgr.GetInstance().GetTeamForRace(Race);

        Level = f.NextUInt32();
        uint levelCap = (uint)Math.Max(1, WorldConfig.LevelCap);
        if (Level > levelCap)
            Level = levelCap;

        XP = f.NextUInt32();

        uint[] explored = ParseCsvUInt32(f.NextString());
        Array.Copy(explored, ExploredZones, Math.Min(explored.Length, ExploredZones.Length));

        QueryResult skills = results[Q_SKILLS];
        if (skills != null)
        {
            LoadSkills(skills);
            f.Skip();
        }
        else
        {
            LoadOldStyleSkills(f.NextString());
        }

        WatchedFactionIndex = f.NextUInt32();
        ChosenTitle = f.NextUInt32();
        KnownTitles = f.NextUInt64();
        KnownTitles1 = f.NextUInt64();
        Coinage = f.NextUInt32();
        AmmoId = f.NextUInt32();
        CharacterPoints2 = f.NextUInt32();
        MaxTalentPoints = (ushort)f.NextUInt32();
        LoadHealth = f.NextUInt32();
        LoadMana = f.NextUInt32();

        byte pvpRank = f.NextByte();
        PlayerBytes = f.NextUInt32();
        PlayerBytes2 = f.NextUInt32();
        PlayerBytes3 = Gender | ((uint)pvpRank << 24);
        PlayerFlags = f.NextUInt32();
        PlayerFieldBytes = f.NextUInt32();

        PositionX = f.NextFloat();
        PositionY = f.NextFloat();
        PositionZ = f.NextFloat();
        Orientation = f.NextFloat();
        MapId = f.NextUInt32();
        ZoneId = f.NextUInt32();

        if (CustomFaction != 0)
            Team = BgTeam = ObjectMgr.GetInstance().GetTeamForRace((byte)CustomFaction);

        LoadTaxiMask(f.NextString());

        Banned = f.NextUInt32();
        BanReason = f.NextString();
        TimeLogoff = f.NextUInt32();
        f.Skip(); // online

        BindPositionX = f.NextFloat();
        BindPositionY = f.NextFloat();
        BindPositionZ = f.NextFloat();
        BindMapId = f.NextUInt32();
        BindZoneId = f.NextUInt32();

        IsResting = f.NextBool();
        RestState = f.NextByte();
        RestAmount = f.NextUInt32();

        LoadPlayedTime(f.NextString());

        DeathState = (DeathState)f.NextUInt32();
        if (LoadHealth != 0 && DeathState == DeathState.JUST_DIED)
        {
            DeathState = DeathState.CORPSE;
            LoadHealth = 0;
        }

        TalentResetTimes = f.NextUInt32();
        FirstLogin = f.NextBool();
        RenamePending = f.NextBool();
        ArenaPoints = f.NextUInt32();
        StableSlotCount = f.NextUInt32();
        InstanceId = f.NextUInt32();
        BgEntryPointMap = f.NextUInt32();
        BgEntryPointX = f.NextFloat();
        BgEntryPointY = f.NextFloat();
        BgEntryPointZ = f.NextFloat();
        BgEntryPointO = f.NextFloat();
        BgEntryPointInstance = f.NextUInt32();

        // TODO: valider le chemin via TaxiMgr une fois porté (le C++ ignore la monture si le chemin est inconnu).
        TaxiPath = f.NextUInt32();
        if (TaxiPath != 0)
        {
            TaxiLastNode = f.NextUInt32();
            TaxiMountDisplayId = f.NextUInt32();
        }
        else
        {
            f.Skip();
            f.Skip();
        }

        // TODO: résoudre le transporteur via ObjectMgr.GetTransporter une fois porté.
        TransporterGuid = f.NextUInt32();
        TransporterX = f.NextFloat();
        TransporterY = f.NextFloat();
        TransporterZ = f.NextFloat();

        foreach (uint spellId in ParseCsvUInt32(f.NextString()))
            DeletedSpells.Add(spellId);

        LoadReputation(f.NextString());
        LoadActionBars(f.NextString());
        LoadAuras(f.NextString());

        foreach (uint questId in ParseCsvUInt32(f.NextString()))
            FinishedQuests.Add(questId);

        foreach (uint questId in ParseCsvUInt32(f.NextString()))
            FinishedDailyQuests.Add(questId);

        HonorRolloverTime = f.NextUInt32();
        KillsToday = f.NextUInt32();
        KillsYesterday = f.NextUInt32();
        KillsLifetime = f.NextUInt32();
        HonorToday = f.NextUInt32();
        HonorYesterday = f.NextUInt32();
        HonorPoints = f.NextUInt32();
        RolloverHonor();

        InstanceDifficulty = f.NextUInt32();
        Height = f.NextFloat();
        Phase = f.NextUInt32();

        TalentActiveSpec = f.NextUInt32();
        TalentSpecsCount = f.NextUInt32();
        if (TalentSpecsCount > PlayerConstants.MAX_SPEC_COUNT)
            TalentSpecsCount = PlayerConstants.MAX_SPEC_COUNT;
        if (TalentActiveSpec >= TalentSpecsCount)
            TalentActiveSpec = 0;
        NeedTalentReset = f.NextBool();

        LoadTalents(results[Q_TALENTS]);
        LoadGlyphs(results[Q_GLYPHS]);
        LoadSpells(results[Q_SPELLS]);
        LoadTutorials(results[Q_TUTORIALS]);
        LoadPlayerCooldowns(results[Q_COOLDOWNS]);
        LoadSocial(results[Q_SOCIAL_FRIENDS], results[Q_SOCIAL_FRIENDED_BY], results[Q_SOCIAL_IGNORES]);

        // TODO: questlog [3], items [4], pets [5]/[6]/[13], mailbox [7], achievements [11]
        // une fois QuestMgr/ItemInterface/Pet/MailSystem/AchievementInterface portés.

        _characterColumns = new string[result.FieldCount];
        for (int index = 0; index < _characterColumns.Length; index++)
            _characterColumns[index] = result.GetFieldName(index);
        _skillsInTable = skills != null;
        _lastPlayedTimeMs = Environment.TickCount64;
        _nextSaveMs = _lastPlayedTimeMs + SaveIntervalMs;
        IsLoaded = true;

        CLog.Success("[Player]", string.Format(W_S_PLAYER_LOADED, Name, Guid, Level, Race, Class, MapId, PositionX, PositionY, PositionZ));
        return true;
    }


    private void RolloverHonor()
    {
        DateTime now = DateTime.Now;
        // Équivalent de (tm_year << 16) | tm_yday.
        uint current = ((uint)(now.Year - 1900) << 16) | (uint)(now.DayOfYear - 1);
        if (current != HonorRolloverTime)
        {
            HonorRolloverTime = current;
            HonorYesterday = HonorToday;
            KillsYesterday = KillsToday;
            HonorToday = KillsToday = 0;
        }
    }

    private void LoadTaxiMask(string data)
    {
        string[] tokens = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < TaxiMask.Length && i < tokens.Length; i++)
            TaxiMask[i] = ParseUInt32(tokens[i]);
    }

    private void LoadPlayedTime(string data)
    {
        string[] tokens = data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return;

        PlayedTime[0] = ParseUInt32(tokens[0]);
        PlayedTime[1] = tokens.Length > 1 ? ParseUInt32(tokens[1]) : PlayedTime[0] + 1;
    }

    private void LoadSkills(QueryResult result)
    {
        // TODO: proficiences armes/armures et contrôle des professions primaires (sWorld.CheckProfessions).
        while (result.NextRow())
        {
            uint skillId = ToUInt32(result.GetValue(1));
            Skills[skillId] = new PlayerSkill
            {
                Type = ToUInt32(result.GetValue(2)),
                CurrentValue = ToUInt32(result.GetValue(3)),
                MaximumValue = ToUInt32(result.GetValue(4)),
            };
        }
    }

    // Ancien format "id;courant;max;..." stocké dans characters.skills.
    private void LoadOldStyleSkills(string data)
    {
        // TODO: si vide, initialiser depuis PlayerCreateInfo.skills une fois disponible dans DBCStores
        string[] tokens = data.Split(';');
        for (int i = 0; i + 2 < tokens.Length - 1; i += 3)
        {
            uint skillId = ParseUInt32(tokens[i]);
            if (skillId == 0)
                continue;

            uint max = ParseUInt32(tokens[i + 2]);
            uint current = skillId == PlayerConstants.SKILL_RIDING ? max : ParseUInt32(tokens[i + 1]);
            Skills.TryAdd(skillId, new PlayerSkill { CurrentValue = current, MaximumValue = max });
        }
    }

    // Format "id,flag,base,standing,..." ; validation via Faction.dbc (RepListId) à porter.
    private void LoadReputation(string data)
    {
        string[] tokens = data.Split(',');
        for (int i = 0; i + 3 < tokens.Length - 1; i += 4)
        {
            Reputation[ParseUInt32(tokens[i])] = new FactionReputation
            {
                Flag = ParseUInt32(tokens[i + 1]),
                BaseStanding = ParseInt32(tokens[i + 2]),
                Standing = ParseInt32(tokens[i + 3]),
            };
        }
        // TODO: _InitialReputation() si aucune réputation (nécessite Faction.dbc).
    }

    // Format "action,type,misc,...".
    private void LoadActionBars(string data)
    {
        string[] tokens = data.Split(',');
        int counter = 0;
        for (int i = 0; i + 2 < tokens.Length - 1 && counter < Actions.Length; i += 3)
        {
            Actions[counter++] = new ActionButton
            {
                Action = (ushort)ParseUInt32(tokens[i]),
                Type = (byte)ParseUInt32(tokens[i + 1]),
                Misc = (byte)ParseUInt32(tokens[i + 2]),
            };
        }
    }

    // Format "id,durée,flags,...".
    private void LoadAuras(string data)
    {
        string[] tokens = data.Split(',');
        for (int i = 0; i + 2 < tokens.Length - 1; i += 3)
        {
            var aura = new LoginAura
            {
                Id = ParseUInt32(tokens[i]),
                Duration = ParseUInt32(tokens[i + 1]),
                Flags = ParseUInt32(tokens[i + 2]),
            };
            if (aura.Id != AURA_IGNORED_1 && aura.Id != AURA_IGNORED_2)
                LoginAuras.Add(aura);
        }
    }

    private void LoadTalents(QueryResult result)
    {
        if (result == null)
            return;

        while (result.NextRow())
        {
            uint spec = ToUInt32(result.GetValue(0));
            if (spec >= PlayerConstants.MAX_SPEC_COUNT)
            {
                CLog.Debug("[Player]", string.Format(W_D_PLAYER_SPECIALIZATION_OUT_OF_RANGE_TALENTS, spec, Guid));
                continue;
            }
            Specs[spec].Talents[ToUInt32(result.GetValue(1))] = (byte)ToUInt32(result.GetValue(2));
        }
    }

    private void LoadGlyphs(QueryResult result)
    {
        if (result == null)
            return;

        while (result.NextRow())
        {
            uint spec = ToUInt32(result.GetValue(1));
            if (spec >= PlayerConstants.MAX_SPEC_COUNT)
            {
                CLog.Debug("[Player]", string.Format(W_D_PLAYER_SPECIALIZATION_OUT_OF_RANGE_GLYPHS, spec, Guid));
                continue;
            }
            for (int i = 0; i < PlayerConstants.GLYPHS_COUNT; i++)
                Specs[spec].Glyphs[i] = (ushort)ToUInt32(result.GetValue(2 + i));
        }
    }

    private void LoadSpells(QueryResult result)
    {
        // TODO: filtrer via Spell.dbc (LookupEntryForced) une fois les DBC chargés côté world.
        if (result == null)
            return;

        while (result.NextRow())
            Spells.Add(ToUInt32(result.GetValue(0)));
    }

    private void LoadTutorials(QueryResult result)
    {
        if (result == null || !result.NextRow())
            return;

        for (int i = 0; i < Tutorials.Length; i++)
            Tutorials[i] = ToUInt32(result.GetValue(i + 1));
    }

    private void LoadPlayerCooldowns(QueryResult result)
    {
        if (result == null)
            return;

        uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while (result.NextRow())
        {
            uint type = ToUInt32(result.GetValue(0));
            uint misc = ToUInt32(result.GetValue(1));
            uint expire = ToUInt32(result.GetValue(2));

            if (type >= PlayerConstants.NUM_COOLDOWN_TYPES)
            {
                CLog.Error("[Player]", string.Format(W_E_PLAYER_INVALID_COOLDOWN_TYPE, Name, Guid, type));
                continue;
            }

            // Cooldowns expirés ou de moins de 10 secondes ignorés.
            if (expire < now || expire - now < 10)
                continue;

            Cooldowns[type][misc] = new PlayerCooldown
            {
                ExpireTime = expire,
                SpellId = ToUInt32(result.GetValue(3)),
                ItemId = ToUInt32(result.GetValue(4)),
            };
        }
    }

    private void LoadSocial(QueryResult friends, QueryResult friendedBy, QueryResult ignores)
    {
        if (friends != null)
        {
            while (friends.NextRow())
            {
                string note = ToStr(friends.GetValue(1));
                Friends[ToUInt32(friends.GetValue(0))] = note.Length > 0 ? note : null;
            }
        }

        if (friendedBy != null)
        {
            while (friendedBy.NextRow())
                HasFriendList.Add(ToUInt32(friendedBy.GetValue(0)));
        }

        if (ignores != null)
        {
            while (ignores.NextRow())
                Ignores.Add(ToUInt32(ignores.GetValue(0)));
        }
    }

    // Reproduit le parsing C++ (strchr ','): le dernier segment non terminé par une virgule est ignoré.
    private static uint[] ParseCsvUInt32(string data)
    {
        string[] tokens = data.Split(',');
        var values = new uint[Math.Max(0, tokens.Length - 1)];
        for (int i = 0; i < values.Length; i++)
            values[i] = ParseUInt32(tokens[i]);
        return values;
    }

    private static uint ParseUInt32(string s) =>
        long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? unchecked((uint)v) : 0;

    private static int ParseInt32(string s) =>
        long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? unchecked((int)v) : 0;

    private static uint ToUInt32(object v) => unchecked((uint)ToUInt64(v));

    private static ulong ToUInt64(object v) => v switch
    {
        null => 0,
        ulong u => u,
        decimal number => decimal.ToUInt64(number),
        string s => ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong r) ? r : (ulong)ParseInt32(s),
        _ => unchecked((ulong)Convert.ToInt64(v, CultureInfo.InvariantCulture)),
    };

    private static string ToStr(object v) => v switch
    {
        null => string.Empty,
        string s => s,
        byte[] b => System.Text.Encoding.UTF8.GetString(b),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    // Lecture séquentielle des colonnes (équivalent de la macro get_next_field).
    private sealed class FieldReader(QueryResult result)
    {
        public int Index { get; set; }

        private object Next() => result.GetValue(Index++);

        public void Skip() => Index++;
        public uint NextUInt32() => ToUInt32(Next());
        public ulong NextUInt64() => ToUInt64(Next());
        public byte NextByte() => (byte)ToUInt32(Next());
        public bool NextBool() => ToUInt32(Next()) != 0;
        public string NextString() => ToStr(Next());

        public float NextFloat()
        {
            object v = Next();
            return v == null ? 0f : Convert.ToSingle(v, CultureInfo.InvariantCulture);
        }
    }
}
