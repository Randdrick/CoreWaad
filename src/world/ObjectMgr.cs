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
    public static DBCStorage<CharClassEntry> DbcCharClassEntry { get; } = new DBCStorage<CharClassEntry>();
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