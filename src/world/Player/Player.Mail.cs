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
using System.Linq;
using WaadShared;
using WaadShared.Database;

namespace WaadWorldServer;

public sealed class PlayerMailMessage
{
    public uint MessageId { get; init; }
    public uint MessageType { get; init; }
    public uint PlayerGuid { get; init; }
    public uint SenderGuid { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public uint Money { get; init; }
    public List<uint> ItemGuids { get; } = [];
    public uint Cod { get; init; }
    public uint Stationary { get; init; }
    public uint ExpireTime { get; init; }
    public uint DeliveryTime { get; init; }
    public bool CopyMade { get; init; }
    public bool Read { get; init; }
    public bool Deleted { get; init; }
}

public sealed partial class Player
{
    public Dictionary<uint, PlayerMailMessage> MailMessages { get; } = [];

    private void LoadMail(QueryResult result)
    {
        MailMessages.Clear();
        if (result == null)
            return;

        while (result.NextRow())
        {
            if (result.FieldCount < 15)
            {
                CLog.Warning("[Player]", "Ligne mailbox incomplete pour le joueur {0}.", Guid);
                continue;
            }

            uint messageId = ToUInt32(result.GetValue(0));
            if (messageId == 0 || MailMessages.ContainsKey(messageId))
                continue;

            var message = new PlayerMailMessage
            {
                MessageId = messageId,
                MessageType = ToUInt32(result.GetValue(1)),
                PlayerGuid = ToUInt32(result.GetValue(2)),
                SenderGuid = ToUInt32(result.GetValue(3)),
                Subject = ToStringValue(result.GetValue(4)),
                Body = ToStringValue(result.GetValue(5)),
                Money = ToUInt32(result.GetValue(6)),
                Cod = ToUInt32(result.GetValue(8)),
                Stationary = ToUInt32(result.GetValue(9)),
                ExpireTime = ToUInt32(result.GetValue(10)),
                DeliveryTime = ToUInt32(result.GetValue(11)),
                CopyMade = ToUInt32(result.GetValue(12)) != 0,
                Read = ToUInt32(result.GetValue(13)) != 0,
                Deleted = ToUInt32(result.GetValue(14)) != 0,
            };

            foreach (string token in ToStringValue(result.GetValue(7)).Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (uint.TryParse(token, out uint itemGuid) && itemGuid != 0)
                    message.ItemGuids.Add(itemGuid);

            MailMessages.Add(messageId, message);
        }
    }

    private void AddMailStatements(List<DatabaseStatement> statements)
    {
        statements.Add(new DatabaseStatement(
            "DELETE FROM mailbox WHERE player_guid=@guid",
            new Dictionary<string, object> { ["@guid"] = Guid }));

        foreach (PlayerMailMessage message in MailMessages.Values.OrderBy(message => message.MessageId))
        {
            var parameters = new Dictionary<string, object>
            {
                ["@messageId"] = message.MessageId,
                ["@type"] = message.MessageType,
                ["@player"] = message.PlayerGuid,
                ["@sender"] = message.SenderGuid,
                ["@subject"] = message.Subject,
                ["@body"] = message.Body,
                ["@money"] = message.Money,
                ["@items"] = string.Join(',', message.ItemGuids),
                ["@cod"] = message.Cod,
                ["@stationary"] = message.Stationary,
                ["@expire"] = message.ExpireTime,
                ["@delivery"] = message.DeliveryTime,
                ["@copy"] = message.CopyMade ? 1u : 0u,
                ["@read"] = message.Read ? 1u : 0u,
                ["@deleted"] = message.Deleted ? 1u : 0u,
            };
            statements.Add(new DatabaseStatement(
                "INSERT INTO mailbox VALUES (@messageId,@type,@player,@sender,@subject,@body,@money,@items,@cod,@stationary,@expire,@delivery,@copy,@read,@deleted)",
                parameters));
        }
    }
}
