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

namespace WaadWorldServer;

// Portage progressif de Player (Player.h/Player.cpp). Object/Unit ne sont pas encore portés :
// les champs d'update (m_uint32Values) sont donc exposés sous forme de propriétés typées.
public sealed partial class Player(uint guid, ClusterPlayerSession session)
{
    public uint Guid { get; } = guid;
    public ClusterPlayerSession Session { get; } = session;

    // Identité
    public string Name { get; private set; } = string.Empty;
    public byte Race { get; private set; }
    public byte Class { get; private set; }
    public byte Gender { get; private set; }
    public uint CustomFaction { get; private set; }
    public PlayerTeam Team { get; private set; }
    public PlayerTeam BgTeam { get; private set; }

    // Progression
    public uint Level { get; private set; }
    public uint XP { get; private set; }
    public uint WatchedFactionIndex { get; private set; }
    public uint ChosenTitle { get; private set; }
    public ulong KnownTitles { get; private set; }
    public ulong KnownTitles1 { get; private set; }
    public uint Coinage { get; private set; }
    public uint AmmoId { get; private set; }
    public uint CharacterPoints2 { get; private set; }
    public ushort MaxTalentPoints { get; private set; }
    public uint LoadHealth { get; private set; }
    public uint LoadMana { get; private set; }

    // Apparence / flags
    public uint PlayerBytes { get; private set; }
    public uint PlayerBytes2 { get; private set; }
    public uint PlayerBytes3 { get; private set; }
    public uint PlayerFlags { get; private set; }
    public uint PlayerFieldBytes { get; private set; }

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
    public byte RestState { get; private set; }
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
    public uint ArenaPoints { get; private set; }
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

    // Honneur
    public uint HonorRolloverTime { get; private set; }
    public uint KillsToday { get; private set; }
    public uint KillsYesterday { get; private set; }
    public uint KillsLifetime { get; private set; }
    public uint HonorToday { get; private set; }
    public uint HonorYesterday { get; private set; }
    public uint HonorPoints { get; private set; }

    // Talents
    public uint TalentActiveSpec { get; private set; }
    public uint TalentSpecsCount { get; private set; } = 1;
    public bool NeedTalentReset { get; private set; }
    public PlayerSpec[] Specs { get; } = [new PlayerSpec(), new PlayerSpec()];

    // Collections chargées depuis la base
    public uint[] ExploredZones { get; } = new uint[EPlayerFields.PLAYER_EXPLORED_ZONES_MAX];
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
}
