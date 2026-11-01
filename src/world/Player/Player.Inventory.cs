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
using System.Globalization;
using System.Linq;
using WaadShared;
using WaadShared.Database;

namespace WaadWorldServer;

public sealed class PlayerInventoryItem
{
    public uint OwnerGuid { get; init; }
    public uint ItemGuid { get; init; }
    public uint Entry { get; init; }
    public uint WrappedItemId { get; init; }
    public uint GiftCreator { get; init; }
    public uint Creator { get; init; }
    public uint StackCount { get; init; }
    public uint Charges { get; init; }
    public uint Flags { get; set; }
    public uint RandomProperty { get; init; }
    public uint RandomSuffix { get; init; }
    public uint ItemTextId { get; init; }
    public uint Durability { get; init; }
    public int ContainerSlot { get; set; }
    public int Slot { get; set; }
    public string Enchantments { get; init; } = string.Empty;
    public ItemPrototype Prototype { get; init; }

    // HIGHGUID_ITEM (0x4000) partagé par les objets et les conteneurs en 3.3.5a.
    public ulong Guid64 => (0x4000UL << 48) | ItemGuid;
}

public sealed partial class Player
{
    public const int EquipmentSlotCount = 19;
    public const int TopLevelInventorySlotCount = 23;
    public const int BagSlotStart = 19;
    public const int BagSlotEnd = 23;
    public const int BankBagSlotStart = 67;
    public const int BankBagSlotEnd = 74;
    // Emplacements de niveau racine jusqu'au porte-clés inclus (champs INV_SLOT_HEAD..KEYRING contigus).
    public const int InventorySlotCount = 118;
    public Dictionary<uint, PlayerInventoryItem> Inventory { get; } = [];
    public PlayerInventoryItem[] Equipment { get; } = new PlayerInventoryItem[EquipmentSlotCount];
    public Dictionary<uint, List<PlayerInventoryItem>> BagContents { get; } = [];
    private readonly List<PlayerInventoryItem> _placedItems = [];

    private static bool IsBagContainerSlot(int slot) =>
        slot is (>= BagSlotStart and < BagSlotEnd) or (>= BankBagSlotStart and < BankBagSlotEnd);

    private void LoadInventory(QueryResult result)
    {
        Inventory.Clear();
        if (result == null)
            return;

        while (result.NextRow())
        {
            if (result.FieldCount < 16)
            {
                CLog.Warning("[Player]", "Ligne playeritems incomplete pour le joueur {0}.", Guid);
                continue;
            }

            uint itemGuid = ToUInt32(result.GetValue(1));
            if (itemGuid == 0 || Inventory.ContainsKey(itemGuid))
            {
                CLog.Warning("[Player]", "Objet playeritems invalide ou duplique pour le joueur {0}: {1}.", Guid, itemGuid);
                continue;
            }

            uint entry = ToUInt32(result.GetValue(2));
            if (!ObjectMgr.GetInstance().TryGetItemPrototype(entry, out ItemPrototype prototype))
                CLog.Warning("[Player]", "Prototype item {0} absent pour le joueur {1}; l'objet est conserve sans metadonnees.", entry, Guid);

            Inventory[itemGuid] = new PlayerInventoryItem
            {
                OwnerGuid = ToUInt32(result.GetValue(0)),
                ItemGuid = itemGuid,
                Entry = entry,
                WrappedItemId = ToUInt32(result.GetValue(3)),
                GiftCreator = ToUInt32(result.GetValue(4)),
                Creator = ToUInt32(result.GetValue(5)),
                StackCount = ToUInt32(result.GetValue(6)),
                Charges = ToUInt32(result.GetValue(7)),
                Flags = ToUInt32(result.GetValue(8)),
                RandomProperty = ToUInt32(result.GetValue(9)),
                RandomSuffix = ToUInt32(result.GetValue(10)),
                ItemTextId = ToUInt32(result.GetValue(11)),
                Durability = ToUInt32(result.GetValue(12)),
                ContainerSlot = ToInt32(result.GetValue(13)),
                Slot = ToInt32(result.GetValue(14)),
                Enchantments = ToStringValue(result.GetValue(15)),
                Prototype = prototype,
            };
        }

        RebuildInventoryViews();
    }

    public PlayerInventoryItem GetInventoryItem(uint itemGuid) =>
        Inventory.TryGetValue(itemGuid, out PlayerInventoryItem item) ? item : null;

    public PlayerInventoryItem GetEquippedItem(int slot) =>
        (uint)slot < (uint)Equipment.Length ? Equipment[slot] : null;

    private void RebuildInventoryViews()
    {
        Array.Clear(Equipment);
        BagContents.Clear();
        _placedItems.Clear();

        var slotGuids = new ulong[InventorySlotCount];
        var visibleEntries = new uint[EPlayerFields.PLAYER_VISIBLE_ITEM_MAX];
        var visibleEnchants = new uint[EPlayerFields.PLAYER_VISIBLE_ITEM_MAX];
        var virtualItems = new uint[3];

        foreach (PlayerInventoryItem item in Inventory.Values.OrderBy(item => item.ItemGuid))
        {
            // Sans prototype, le client ne peut pas recevoir l'objet : il reste en base mais hors des vues.
            if (item.Prototype == null)
                continue;

            if (item.ContainerSlot < 0)
            {
                if ((uint)item.Slot >= InventorySlotCount)
                    continue;

                slotGuids[item.Slot] = item.Guid64;
                _placedItems.Add(item);
                if (item.Slot < Equipment.Length)
                {
                    Equipment[item.Slot] = item;
                    visibleEntries[item.Slot] = item.Entry;
                    visibleEnchants[item.Slot] = GetPermanentEnchantment(item.Enchantments);
                }

                if (item.Slot is >= 15 and <= 17)
                    virtualItems[item.Slot - 15] = item.Entry;
                continue;
            }

            if (!IsBagContainerSlot(item.ContainerSlot) || item.Slot < 0)
                continue;
            if ((uint)item.Slot >= item.Prototype.ContainerSlots)
            {
                CLog.Warning("[Player]", "Objet {0} depasse la capacite du sac {1} pour le joueur {2}.",
                    item.ItemGuid, item.ContainerSlot, Guid);
            }

            if (!BagContents.TryGetValue((uint)item.ContainerSlot, out List<PlayerInventoryItem> contents))
            {
                contents = [];
                BagContents.Add((uint)item.ContainerSlot, contents);
            }
            contents.Add(item);
            _placedItems.Add(item);
        }

        for (int slot = 0; slot < InventorySlotCount; slot++)
            SetUInt64Value(EPlayerFields.PLAYER_FIELD_INV_SLOT_HEAD + slot * 2, slotGuids[slot]);
        for (int slot = 0; slot < EPlayerFields.PLAYER_VISIBLE_ITEM_MAX; slot++)
        {
            int visibleBase = EPlayerFields.PLAYER_VISIBLE_ITEM_1_ENTRYID + slot * EPlayerFields.PLAYER_VISIBLE_ITEM_LENGTH;
            SetUInt32Value(visibleBase, visibleEntries[slot]);
            SetUInt32Value(visibleBase + 1, visibleEnchants[slot]);
        }
        SetUInt32Value(EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_ID, virtualItems[0]);
        SetUInt32Value(EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_ID_1, virtualItems[1]);
        SetUInt32Value(EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_ID_2, virtualItems[2]);
    }

    private void MarkInventoryFieldsForUpdate()
    {
        for (int slot = 0; slot < InventorySlotCount * 2; slot++)
            UpdateMask.SetBit(EPlayerFields.PLAYER_FIELD_INV_SLOT_HEAD + slot);
        for (int slot = 0; slot < EPlayerFields.PLAYER_VISIBLE_ITEM_MAX * EPlayerFields.PLAYER_VISIBLE_ITEM_LENGTH; slot++)
            UpdateMask.SetBit(EPlayerFields.PLAYER_VISIBLE_ITEM_1_ENTRYID + slot);
        UpdateMask.SetBit(EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_ID);
        UpdateMask.SetBit(EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_ID_1);
        UpdateMask.SetBit(EUnitFields.UNIT_VIRTUAL_ITEM_SLOT_ID_2);
    }

    private static uint GetPermanentEnchantment(string enchantments)
    {
        if (string.IsNullOrWhiteSpace(enchantments))
            return 0;

        string first = enchantments.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
            return 0;

        string enchantmentId = first.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return uint.TryParse(enchantmentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value)
            ? value
            : 0;
    }

    private static int ToInt32(object value) =>
        value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    private static string ToStringValue(object value) => value switch
    {
        null => string.Empty,
        string text => text,
        byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private void AddInventoryStatements(List<DatabaseStatement> statements)
    {
        statements.Add(new DatabaseStatement(
            "DELETE FROM playeritems WHERE ownerguid=@guid",
            new Dictionary<string, object> { ["@guid"] = Guid }));

        foreach (PlayerInventoryItem item in Inventory.Values.OrderBy(item => item.ItemGuid))
        {
            var parameters = new Dictionary<string, object>
            {
                ["@owner"] = Guid,
                ["@itemGuid"] = item.ItemGuid,
                ["@entry"] = item.Entry,
                ["@wrapped"] = item.WrappedItemId,
                ["@giftCreator"] = item.GiftCreator,
                ["@creator"] = item.Creator,
                ["@stack"] = item.StackCount,
                ["@charges"] = item.Charges,
                ["@flags"] = item.Flags,
                ["@randomProperty"] = item.RandomProperty,
                ["@randomSuffix"] = item.RandomSuffix,
                ["@itemText"] = item.ItemTextId,
                ["@durability"] = item.Durability,
                ["@containerSlot"] = item.ContainerSlot,
                ["@slot"] = item.Slot,
                ["@enchantments"] = item.Enchantments,
            };
            statements.Add(new DatabaseStatement(
                "INSERT INTO playeritems VALUES (@owner,@itemGuid,@entry,@wrapped,@giftCreator,@creator,@stack,@charges,@flags,@randomProperty,@randomSuffix,@itemText,@durability,@containerSlot,@slot,@enchantments)",
                parameters));
        }
    }
}
