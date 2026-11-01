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
using System.IO;
using System.Linq;

namespace WaadWorldServer;

// Objets et conteneurs vus par leur propriétaire (équivalent de ItemInterface::m_CreateForPlayer).
public sealed partial class Player
{
    private const byte UpdateTypeValues = 0;
    private const byte UpdateTypeCreateObject = 2;
    private const byte TypeIdItem = 1;
    private const byte TypeIdContainer = 2;
    private const ushort UpdateFlagLowGuid = 0x0010;
    private const int MaxContainerSlots = 36;
    private const int EnchantmentSlotCount = 12;

    private PlayerInventoryItem FindItemAt(int container, int slot) =>
        _placedItems.FirstOrDefault(item => item.ContainerSlot == container && item.Slot == slot);

    private uint[] BuildItemValues(PlayerInventoryItem item)
    {
        ItemPrototype proto = item.Prototype;
        bool container = proto.IsContainer;
        var values = new uint[container ? EContainerFields.CONTAINER_END : EItemFields.ITEM_END];

        SetValue64(values, EObjectFields.OBJECT_FIELD_GUID, item.Guid64);
        values[EObjectFields.OBJECT_FIELD_TYPE] = container ? 7u : 3u;
        values[EObjectFields.OBJECT_FIELD_ENTRY] = item.Entry;
        values[EObjectFields.OBJECT_FIELD_SCALE_X] = BitConverter.SingleToUInt32Bits(1.0f);

        SetValue64(values, EItemFields.ITEM_FIELD_OWNER, Guid);
        PlayerInventoryItem parent = item.ContainerSlot < 0 ? null : FindItemAt(-1, item.ContainerSlot);
        SetValue64(values, EItemFields.ITEM_FIELD_CONTAINED, parent?.Guid64 ?? Guid);
        SetValue64(values, EItemFields.ITEM_FIELD_CREATOR, item.Creator);
        SetValue64(values, EItemFields.ITEM_FIELD_GIFTCREATOR, item.GiftCreator);
        values[EItemFields.ITEM_FIELD_STACK_COUNT] = Math.Max(item.StackCount, 1u);
        values[EItemFields.ITEM_FIELD_SPELL_CHARGES] = item.Charges;
        values[EItemFields.ITEM_FIELD_FLAGS] = item.Flags;
        ApplyEnchantments(values, item.Enchantments);

        // Suffixe aléatoire : identifiant négatif, graine dérivée du GUID pour rester stable entre deux connexions.
        if (item.RandomProperty != 0)
        {
            values[EItemFields.ITEM_FIELD_RANDOM_PROPERTIES_ID] = item.RandomProperty;
        }
        else if (item.RandomSuffix != 0)
        {
            values[EItemFields.ITEM_FIELD_RANDOM_PROPERTIES_ID] = unchecked((uint)-(int)item.RandomSuffix);
            values[EItemFields.ITEM_FIELD_PROPERTY_SEED] = item.ItemGuid;
        }

        values[EItemFields.ITEM_FIELD_DURABILITY] = item.Durability;
        values[EItemFields.ITEM_FIELD_MAXDURABILITY] = proto.MaxDurability;

        if (container)
        {
            values[EContainerFields.CONTAINER_FIELD_NUM_SLOTS] = proto.ContainerSlots;
            if (BagContents.TryGetValue((uint)item.Slot, out List<PlayerInventoryItem> contents) && item.ContainerSlot < 0)
            {
                foreach (PlayerInventoryItem content in contents)
                    if ((uint)content.Slot < MaxContainerSlots)
                        SetValue64(values, EContainerFields.CONTAINER_FIELD_SLOT_0 + content.Slot * 2, content.Guid64);
            }
        }

        return values;
    }

    private static void SetValue64(uint[] values, int index, ulong value)
    {
        values[index] = (uint)value;
        values[index + 1] = (uint)(value >> 32);
    }

    // Format "id,durée,emplacement;..." ; l'emplacement 0 est l'enchantement permanent.
    private static void ApplyEnchantments(uint[] values, string enchantments)
    {
        if (string.IsNullOrWhiteSpace(enchantments))
            return;

        int fallbackSlot = 0;
        foreach (string group in enchantments.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] tokens = group.Split(',', StringSplitOptions.RemoveEmptyEntries);
            int slot = fallbackSlot++;
            if (tokens.Length >= 3 && int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSlot))
                slot = parsedSlot;
            if ((uint)slot >= EnchantmentSlotCount || tokens.Length == 0
                || !uint.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint id) || id == 0)
                continue;

            int fieldBase = EItemFields.ITEM_FIELD_ENCHANTMENT_1_1 + slot * 3;
            values[fieldBase] = id;
            if (slot != 0 && tokens.Length >= 2 && uint.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint duration))
                values[fieldBase + 1] = duration;
        }
    }

    private static void WritePackedGuid64(BinaryWriter writer, ulong guid)
    {
        byte mask = 0;
        for (int byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
            if (((guid >> (byteIndex * 8)) & 0xFF) != 0)
                mask |= (byte)(1 << byteIndex);

        writer.Write(mask);
        for (int byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
            if ((mask & (1 << byteIndex)) != 0)
                writer.Write((byte)(guid >> (byteIndex * 8)));
    }

    // include == null : tous les champs non nuls.
    private static void WriteMaskedValues(BinaryWriter writer, uint[] values, Func<int, bool> include)
    {
        int wordCount = (values.Length + 31) / 32;
        var words = new uint[wordCount];
        for (int index = 0; index < values.Length; index++)
        {
            bool selected = include == null ? values[index] != 0 : include(index);
            if (selected)
                words[index >> 5] |= 1u << (index & 31);
        }

        writer.Write((byte)wordCount);
        foreach (uint word in words)
            writer.Write(word);
        for (int index = 0; index < values.Length; index++)
            if ((words[index >> 5] & (1u << (index & 31))) != 0)
                writer.Write(values[index]);
    }

    private byte[] BuildItemCreateBlock(PlayerInventoryItem item)
    {
        uint[] values = BuildItemValues(item);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(UpdateTypeCreateObject);
        WritePackedGuid64(writer, item.Guid64);
        writer.Write(item.Prototype.IsContainer ? TypeIdContainer : TypeIdItem);
        writer.Write(UpdateFlagLowGuid);
        writer.Write(item.ItemGuid);
        WriteMaskedValues(writer, values, null);
        writer.Flush();
        return stream.ToArray();
    }

    private byte[] BuildItemValuesBlock(PlayerInventoryItem item, IReadOnlyCollection<int> fields)
    {
        uint[] values = BuildItemValues(item);
        var selected = fields.ToHashSet();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(UpdateTypeValues);
        WritePackedGuid64(writer, item.Guid64);
        WriteMaskedValues(writer, values, selected.Contains);
        writer.Flush();
        return stream.ToArray();
    }

    // Objets et sacs d'abord : le CREATE du joueur référence leurs GUID.
    private void PushItemCreates()
    {
        foreach (PlayerInventoryItem item in _placedItems.OrderBy(item => item.ContainerSlot).ThenBy(item => item.Slot))
            PushCreationData(BuildItemCreateBlock(item), 1);
    }

    // Après un déplacement : nouveau conteneur parent et contenu des sacs touchés.
    private void PushItemMoveUpdates(IEnumerable<PlayerInventoryItem> moved, IEnumerable<int> touchedBags)
    {
        foreach (PlayerInventoryItem item in moved.Where(item => item != null && _placedItems.Contains(item)))
        {
            PushUpdateData(BuildItemValuesBlock(item,
                [EItemFields.ITEM_FIELD_CONTAINED, EItemFields.ITEM_FIELD_CONTAINED + 1, EItemFields.ITEM_FIELD_FLAGS]), 1);
        }

        foreach (int bagSlot in touchedBags.Distinct())
        {
            PlayerInventoryItem bag = FindItemAt(-1, bagSlot);
            if (bag == null || !bag.Prototype.IsContainer)
                continue;

            var fields = new List<int>();
            for (int index = 0; index < MaxContainerSlots * 2; index++)
                fields.Add(EContainerFields.CONTAINER_FIELD_SLOT_0 + index);
            PushUpdateData(BuildItemValuesBlock(bag, fields), 1);
        }
    }
}
