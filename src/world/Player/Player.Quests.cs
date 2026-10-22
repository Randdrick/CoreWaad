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

using System;
using System.Collections.Generic;
using WaadShared;
using WaadShared.Database;
using static WaadShared.PlayerMessages;

namespace WaadWorldServer;

public sealed class PlayerQuestLogEntry
{
    public uint QuestId { get; init; }
    public int Slot { get; init; }
    public uint TimeLeft { get; init; }
    public uint[] ExploredAreas { get; } = new uint[4];
    public uint[] MobCounts { get; } = new uint[4];
    public uint PlayerSlain { get; init; }
}

public sealed partial class Player
{
    public const int QuestLogSlotCount = 25;
    public const int QuestLogSlotFieldCount = 5;
    public PlayerQuestLogEntry[] QuestLog { get; } = new PlayerQuestLogEntry[QuestLogSlotCount];

    private void LoadQuestLog(QueryResult result)
    {
        ClearQuestLogFields();
        if (result == null)
            return;

        while (result.NextRow())
        {
            if (result.FieldCount < 13)
            {
                CLog.Warning("[Player]", W_W_PLAYER_QUESTLOG_INCOMPLETE_ROW, Guid);
                continue;
            }

            uint questId = ToUInt32(result.GetValue(1));
            int slot = unchecked((int)ToUInt32(result.GetValue(2)));
            if (questId == 0 || (uint)slot >= QuestLogSlotCount || QuestLog[slot] != null)
            {
                CLog.Warning("[Player]", W_W_PLAYER_QUESTLOG_INVALID_ROW, Guid, questId, slot);
                continue;
            }

            var entry = new PlayerQuestLogEntry
            {
                QuestId = questId,
                Slot = slot,
                TimeLeft = ToUInt32(result.GetValue(3)),
                PlayerSlain = ToUInt32(result.GetValue(12)),
            };
            for (int index = 0; index < entry.ExploredAreas.Length; index++)
                entry.ExploredAreas[index] = ToUInt32(result.GetValue(4 + index));
            for (int index = 0; index < entry.MobCounts.Length; index++)
                entry.MobCounts[index] = ToUInt32(result.GetValue(8 + index));

            QuestLog[slot] = entry;
            UpdateQuestLogFields(entry);
        }
    }

    public PlayerQuestLogEntry GetQuestLogEntry(uint questId)
    {
        for (int slot = 0; slot < QuestLog.Length; slot++)
            if (QuestLog[slot]?.QuestId == questId)
                return QuestLog[slot];
        return null;
    }

    private void ClearQuestLogFields()
    {
        Array.Clear(QuestLog);
        for (int slot = 0; slot < QuestLogSlotCount; slot++)
        {
            int baseIndex = EPlayerFields.PLAYER_QUEST_LOG_1_1 + slot * QuestLogSlotFieldCount;
            SetUInt32Value(baseIndex, 0);
            SetUInt32Value(baseIndex + 1, 0);
            SetUInt64Value(baseIndex + 2, 0);
            SetUInt32Value(baseIndex + 4, 0);
        }
    }

    private void UpdateQuestLogFields(PlayerQuestLogEntry entry)
    {
        int baseIndex = EPlayerFields.PLAYER_QUEST_LOG_1_1 + entry.Slot * QuestLogSlotFieldCount;
        ulong progress = entry.PlayerSlain & 0xFFu;
        for (int index = 0; index < entry.MobCounts.Length; index++)
            progress |= (ulong)(entry.MobCounts[index] & 0xFFu) << (index * 16);

        SetUInt32Value(baseIndex, entry.QuestId);
        SetUInt32Value(baseIndex + 1, 0);
        SetUInt64Value(baseIndex + 2, progress);
        SetUInt32Value(baseIndex + 4, entry.TimeLeft);
    }

    private void AddQuestLogStatements(List<DatabaseStatement> statements)
    {
        var deleteParameters = new Dictionary<string, object> { ["@guid"] = Guid };
        statements.Add(new DatabaseStatement("DELETE FROM questlog WHERE player_guid=@guid", deleteParameters));

        foreach (PlayerQuestLogEntry entry in QuestLog)
        {
            if (entry == null)
                continue;

            var parameters = new Dictionary<string, object>
            {
                ["@guid"] = Guid,
                ["@quest"] = entry.QuestId,
                ["@slot"] = entry.Slot,
                ["@time"] = entry.TimeLeft,
                ["@area0"] = entry.ExploredAreas[0],
                ["@area1"] = entry.ExploredAreas[1],
                ["@area2"] = entry.ExploredAreas[2],
                ["@area3"] = entry.ExploredAreas[3],
                ["@mob0"] = entry.MobCounts[0],
                ["@mob1"] = entry.MobCounts[1],
                ["@mob2"] = entry.MobCounts[2],
                ["@mob3"] = entry.MobCounts[3],
                ["@slain"] = entry.PlayerSlain,
            };
            statements.Add(new DatabaseStatement(
                "INSERT INTO questlog VALUES (@guid,@quest,@slot,@time,@area0,@area1,@area2,@area3,@mob0,@mob1,@mob2,@mob3,@slain)", parameters));
        }
    }
}
