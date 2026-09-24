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

using WaadShared.Config;

namespace WaadWorldServer;

public static class WorldConfig
{
    private static readonly ConfigMgr Config = new();
    // Chemins
    public static string MapPath => Config.WorldConfig.GetString("Path", "MapPath", "maps");
    public static string VMapPath => Config.WorldConfig.GetString("Path", "vMapPath", "vmaps");
    public static string MMapPath => Config.WorldConfig.GetString("Path", "MMapPath", "mmaps");
    public static string DBCPath => Config.WorldConfig.GetString("Path", "DBCPath", "dbc");
    public static string ScriptsPath => Config.WorldConfig.GetString("Path", "ScriptsPath", "script_bin");

    // Paramètres du serveur
    public static int PlayerLimit => Config.WorldConfig.GetInt32("Server", "PlayerLimit", 100);
    public static string Motd => Config.WorldConfig.GetString("Server", "Motd", "No MOTD specified.");
    public static bool SendStatsOnJoin => Config.WorldConfig.GetBoolean("Server", "SendStatsOnJoin", true);
    public static bool EnableBreathing => Config.WorldConfig.GetBoolean("Server", "EnableBreathing", true);
    public static int StartingLevel => Config.WorldConfig.GetInt32("Server", "StartingLevel", 1);
    public static int LevelCap => Config.WorldConfig.GetInt32("Server", "LevelCap", 80);
    public static int GenLevelCap => Config.WorldConfig.GetInt32("Server", "GenLevelCap", 80);
    public static bool SeperateChatChannels => Config.WorldConfig.GetBoolean("Server", "SeperateChatChannels", false);
    public static int CompressionThreshold => Config.WorldConfig.GetInt32("Server", "CompressionThreshold", 1000);
    public static int QueueUpdateInterval => Config.WorldConfig.GetInt32("Server", "QueueUpdateInterval", 5000);
    public static int KickAFKPlayers => Config.WorldConfig.GetInt32("Server", "KickAFKPlayers", 0);
    public static int ConnectionTimeout => Config.WorldConfig.GetInt32("Server", "ConnectionTimeout", 180);
    public static int RealmType => Config.WorldConfig.GetInt32("Server", "RealmType", 1);
    public static bool AdjustPriority => Config.WorldConfig.GetBoolean("Server", "AdjustPriority", false);
    public static bool RequireAllSignatures => Config.WorldConfig.GetBoolean("Server", "RequireAllSignatures", false);
    public static bool ShowGMInWhoList => Config.WorldConfig.GetBoolean("Server", "ShowGMInWhoList", true);
    public static int MapUnloadTime => Config.WorldConfig.GetInt32("Server", "MapUnloadTime", 0);
    public static bool LimitedNames => Config.WorldConfig.GetBoolean("Server", "LimitedNames", true);
    public static bool UseAccountData => Config.WorldConfig.GetBoolean("Server", "UseAccountData", false);
    public static bool AllowPlayerCommands => Config.WorldConfig.GetBoolean("Server", "AllowPlayerCommands", false);
    public static bool LoadAIAgents => Config.WorldConfig.GetBoolean("Server", "LoadAIAgents", true);
    public static bool EnableLFGJoin => Config.WorldConfig.GetBoolean("Server", "EnableLFGJoin", false);
    public static bool ForceGMTag => Config.WorldConfig.GetBoolean("Server", "ForceGMTag", false);
    public static bool ShowKickMessage => Config.WorldConfig.GetBoolean("Server", "ShowKickMessage", true);
    public static bool CheckProfessions => Config.WorldConfig.GetBoolean("Server", "CheckProfessions", true);
    public static int DualTalentPrice => Config.WorldConfig.GetInt32("Server", "DualTalentPrice", 1000);

    // Rates
    public static float HealthRate => Config.WorldConfig.GetFloat("Rates", "Health", 1.0f);
    public static float Power1Rate => Config.WorldConfig.GetFloat("Rates", "Power1", 1.0f);
    public static float Power2Rate => Config.WorldConfig.GetFloat("Rates", "Power2", 1.0f);
    public static float Power3Rate => Config.WorldConfig.GetFloat("Rates", "Power3", 1.0f);
    public static float Power4Rate => Config.WorldConfig.GetFloat("Rates", "Power4", 1.0f);
    public static float QuestReputationRate => Config.WorldConfig.GetFloat("Rates", "QuestReputation", 1.0f);
    public static float KillReputationRate => Config.WorldConfig.GetFloat("Rates", "KillReputation", 1.0f);
    public static float HonorRate => Config.WorldConfig.GetFloat("Rates", "Honor", 1.0f);
    public static int PvPTimer => Config.WorldConfig.GetInt32("Rates", "PvPTimer", 300000);
    public static float XPRate => Config.WorldConfig.GetFloat("Rates", "XP", 1.0f);
    public static float QuestXPRate => Config.WorldConfig.GetFloat("Rates", "QuestXP", 1.0f);
    public static float RestXPRate => Config.WorldConfig.GetFloat("Rates", "RestXP", 1.0f);
    public static float DropGreyRate => Config.WorldConfig.GetFloat("Rates", "DropGrey", 1.0f);
    public static float DropWhiteRate => Config.WorldConfig.GetFloat("Rates", "DropWhite", 1.0f);
    public static float DropGreenRate => Config.WorldConfig.GetFloat("Rates", "DropGreen", 1.0f);
    public static float DropBlueRate => Config.WorldConfig.GetFloat("Rates", "DropBlue", 1.0f);
    public static float DropPurpleRate => Config.WorldConfig.GetFloat("Rates", "DropPurple", 1.0f);
    public static float DropOrangeRate => Config.WorldConfig.GetFloat("Rates", "DropOrange", 1.0f);
    public static float DropArtifactRate => Config.WorldConfig.GetFloat("Rates", "DropArtifact", 1.0f);
    public static float DropMoneyRate => Config.WorldConfig.GetFloat("Rates", "DropMoney", 1.0f);
    public static int SaveInterval => Config.WorldConfig.GetInt32("Rates", "Save", 300000);
    public static float SkillChanceRate => Config.WorldConfig.GetFloat("Rates", "SkillChance", 1.0f);
    public static float SkillRate => Config.WorldConfig.GetFloat("Rates", "SkillRate", 1.0f);
    public static float ArenaMultiplier2x => Config.WorldConfig.GetFloat("Rates", "ArenaMultiplier2x", 1.0f);
    public static float ArenaMultiplier3x => Config.WorldConfig.GetFloat("Rates", "ArenaMultiplier3x", 1.0f);
    public static float ArenaMultiplier5x => Config.WorldConfig.GetFloat("Rates", "ArenaMultiplier5x", 1.0f);

    // Battlegrounds
    public static class Battleground
    {
        public static int WarsongMinPlayers => Config.WorldConfig.GetInt32("Battleground.Warsongs", "MinPlayer", 5);
        public static bool WarsongEnabled => Config.WorldConfig.GetBoolean("Battleground.Warsongs", "State", true);
        public static string WarsongMasterSubName => Config.WorldConfig.GetString("Battleground.Warsongs", "MasterSubNameContent", "Groulet des Chanteguerres");

        public static int ArathiMinPlayers => Config.WorldConfig.GetInt32("Battleground.Arathi", "MinPlayer", 5);
        public static bool ArathiEnabled => Config.WorldConfig.GetBoolean("Battleground.Arathi", "State", true);
        public static string ArathiMasterSubName => Config.WorldConfig.GetString("Battleground.Arathi", "MasterSubNameContent", "Bassin d'Arathi");

        public static int AlteracMinPlayers => Config.WorldConfig.GetInt32("Battleground.Alterac", "MinPlayer", 10);
        public static bool AlteracEnabled => Config.WorldConfig.GetBoolean("Battleground.Alterac", "State", false);
        public static string AlteracMasterSubName => Config.WorldConfig.GetString("Battleground.Alterac", "MasterSubNameContent", "Vallée d'Alterac");

        public static int NetherStormMinPlayers => Config.WorldConfig.GetInt32("Battleground.NetherStorm", "MinPlayer", 10);
        public static bool NetherStormEnabled => Config.WorldConfig.GetBoolean("Battleground.NetherStorm", "State", true);
        public static string NetherStormMasterSubName => Config.WorldConfig.GetString("Battleground.NetherStorm", "MasterSubNameContent", "Oeil du Cyclone");

        public static int SOTAMinPlayers => Config.WorldConfig.GetInt32("Battleground.SOTA", "MinPlayer", 5);
        public static bool SOTAEnabled => Config.WorldConfig.GetBoolean("Battleground.SOTA", "State", false);
        public static string SOTAMasterSubName => Config.WorldConfig.GetString("Battleground.SOTA", "MasterSubNameContent", "Rivage des Anciens");

        public static int IOCMinPlayers => Config.WorldConfig.GetInt32("Battleground.IOC", "MinPlayer", 10);
        public static bool IOCEnabled => Config.WorldConfig.GetBoolean("Battleground.IOC", "State", true);
        public static string IOCMasterSubName => Config.WorldConfig.GetString("Battleground.IOC", "MasterSubNameContent", "Ile des Conquérants");
    }

    // Arena Season
    public static int ArenaSeasonNumber => Config.WorldConfig.GetInt32("ArenaSeason", "SeasonNumber", 8);
    public static bool ArenaSeasonOnTime => Config.WorldConfig.GetBoolean("ArenaSeason", "SeasonOnTime", true);
    public static string ArenaSeasonMasterSubName => Config.WorldConfig.GetString("ArenaSeason", "MasterSubNameContent", "Arenes");
}