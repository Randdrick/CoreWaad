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

using System.Collections.Generic;
using System.IO;
using WaadShared;
using static DBCStores;

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

    public bool LoadPlayerDbcs(string dbcPath)
    {
        if (!DBCStores.IsDbcLoaded)
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
        // Pour l'instant, on se contente de valider que race et classe existent individuellement
        return IsValidRace(race) && IsValidClass(classId);
    }
}