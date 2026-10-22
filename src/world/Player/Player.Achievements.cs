/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2027 WAAD Team <https://arbonne.games-rpg.net/>
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
using System.Linq;
using WaadShared;
using WaadShared.Database;

namespace WaadWorldServer;

public sealed class PlayerAchievement
{
    public uint AchievementId { get; init; }
    public string Progress { get; init; } = string.Empty;
    public uint CompletedValue { get; init; }
    public bool IsCompleted => CompletedValue != 0;
}

public sealed partial class Player
{
    public Dictionary<uint, PlayerAchievement> Achievements { get; } = [];

    private void LoadAchievements(QueryResult result)
    {
        Achievements.Clear();
        if (result == null)
            return;

        while (result.NextRow())
        {
            if (result.FieldCount < 5)
            {
                CLog.Warning("[Player]", "Ligne achievements incomplete pour le joueur {0}.", Guid);
                continue;
            }

            uint achievementId = ToUInt32(result.GetValue(2));
            if (achievementId == 0 || Achievements.ContainsKey(achievementId))
                continue;

            Achievements.Add(achievementId, new PlayerAchievement
            {
                AchievementId = achievementId,
                Progress = ToStringValue(result.GetValue(3)),
                CompletedValue = ToUInt32(result.GetValue(4)),
            });
        }
    }

    private void AddAchievementStatements(List<DatabaseStatement> statements)
    {
        statements.Add(new DatabaseStatement(
            "DELETE FROM achievements WHERE player=@guid",
            new Dictionary<string, object> { ["@guid"] = Guid }));

        foreach (PlayerAchievement achievement in Achievements.Values.OrderBy(achievement => achievement.AchievementId))
        {
            var parameters = new Dictionary<string, object>
            {
                ["@player"] = Guid,
                ["@achievement"] = achievement.AchievementId,
                ["@progress"] = achievement.Progress,
                ["@completed"] = achievement.CompletedValue,
            };
            statements.Add(new DatabaseStatement(
                "INSERT INTO achievements (player,achievementid,progress,completed) VALUES (@player,@achievement,@progress,@completed)",
                parameters));
        }
    }
}
