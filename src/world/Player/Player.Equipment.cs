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

namespace WaadWorldServer;

public enum InventoryResult : byte
{
    Ok = 0,
    CantEquipLevel = 1,
    ItemDoesntGoToSlot = 3,
    BagFull = 4,
    NonEmptyBagOverOtherBag = 5,
    YouCanNeverUseThatItem = 10,
    CantEquipWithTwoHanded = 13,
    CantDualWield = 14,
    ItemDoesntGoIntoBag = 15,
    ItemCantBeEquipped = 20,
    SlotIsEmpty = 22,
    ItemNotFound = 23,
}

// Déplacement et équipement des objets (CMSG_SWAP_INV_ITEM, CMSG_SWAP_ITEM, CMSG_AUTOEQUIP_ITEM).
public sealed partial class Player
{
    private const byte ClientPlayerContainer = 255;
    private const int FirstBackpackSlot = 23;
    private const int LastBackpackSlot = 38;
    private const int MainHandSlot = 15;
    private const int OffHandSlot = 16;
    private const uint DualWieldSpell = 674;
    private const uint TwoHandedWeapon = 17;
    private const uint BindOnEquip = 2;
    private const uint ItemFlagSoulbound = 0x00000001;

    // Emplacements d'équipement acceptés par type d'inventaire (3.3.5a).
    private static int[] GetEquipSlots(ItemPrototype proto) => proto.InventoryType switch
    {
        1 => [0],
        2 => [1],
        3 => [2],
        4 => [3],
        5 or 20 => [4],
        6 => [5],
        7 => [6],
        8 => [7],
        9 => [8],
        10 => [9],
        11 => [10, 11],
        12 => [12, 13],
        13 => [MainHandSlot, OffHandSlot],
        14 or 22 or 23 => [OffHandSlot],
        15 or 25 or 26 or 28 => [17],
        16 => [14],
        17 or 21 => [MainHandSlot],
        18 or 27 => [19, 20, 21, 22],
        19 => [18],
        _ => [],
    };

    private static int? ToContainer(byte clientBag) => clientBag == ClientPlayerContainer ? -1 : clientBag;

    internal void HandleSwapItems(byte srcBag, byte srcSlot, byte dstBag, byte dstSlot)
    {
        InventoryResult result = TrySwap(ToContainer(srcBag).Value, srcSlot, ToContainer(dstBag).Value, dstSlot,
            out PlayerInventoryItem src, out PlayerInventoryItem dst);
        if (result != InventoryResult.Ok)
            SendInventoryFailure(result, src, dst);
    }

    internal void HandleAutoEquip(byte srcBag, byte srcSlot)
    {
        int container = ToContainer(srcBag).Value;
        PlayerInventoryItem item = FindItemAt(container, srcSlot);
        if (item == null)
        {
            SendInventoryFailure(InventoryResult.ItemNotFound, null, null);
            return;
        }

        // Un objet déjà équipé retourne dans le premier emplacement libre du sac à dos.
        int target = -1;
        if (container == -1 && srcSlot < EquipmentSlotCount)
        {
            for (int slot = FirstBackpackSlot; slot <= LastBackpackSlot && target < 0; slot++)
                if (FindItemAt(-1, slot) == null)
                    target = slot;
            if (target < 0)
            {
                SendInventoryFailure(InventoryResult.BagFull, item, null);
                return;
            }
        }
        else
        {
            int[] slots = GetEquipSlots(item.Prototype);
            if (slots.Length == 0)
            {
                SendInventoryFailure(InventoryResult.ItemCantBeEquipped, item, null);
                return;
            }

            target = slots[0];
            foreach (int slot in slots)
            {
                if (FindItemAt(-1, slot) == null)
                {
                    target = slot;
                    break;
                }
            }
        }

        InventoryResult result = TrySwap(container, srcSlot, -1, target, out PlayerInventoryItem src, out PlayerInventoryItem dst);
        if (result != InventoryResult.Ok)
            SendInventoryFailure(result, src, dst);
    }

    private InventoryResult TrySwap(int srcContainer, int srcSlot, int dstContainer, int dstSlot,
        out PlayerInventoryItem src, out PlayerInventoryItem dst)
    {
        src = FindItemAt(srcContainer, srcSlot);
        dst = FindItemAt(dstContainer, dstSlot);
        if (src == null)
            return InventoryResult.SlotIsEmpty;
        if (ReferenceEquals(src, dst))
            return InventoryResult.Ok;

        InventoryResult result = CanPlace(src, dstContainer, dstSlot, dst);
        if (result == InventoryResult.Ok && dst != null)
            result = CanPlace(dst, srcContainer, srcSlot, src);
        if (result != InventoryResult.Ok)
            return result;

        src.ContainerSlot = dstContainer;
        src.Slot = dstSlot;
        if (dst != null)
        {
            dst.ContainerSlot = srcContainer;
            dst.Slot = srcSlot;
        }

        var soulbound = new List<PlayerInventoryItem>();
        foreach (PlayerInventoryItem item in new[] { src, dst })
        {
            if (item != null && item.ContainerSlot < 0 && item.Slot < FirstBackpackSlot
                && item.Prototype.Bonding == BindOnEquip && (item.Flags & ItemFlagSoulbound) == 0)
            {
                item.Flags |= ItemFlagSoulbound;
                soulbound.Add(item);
            }
        }

        RebuildInventoryViews();
        PushItemMoveUpdates([src, dst, .. soulbound],
            [srcContainer, dstContainer]);
        return InventoryResult.Ok;
    }

    private InventoryResult CanPlace(PlayerInventoryItem item, int container, int slot, PlayerInventoryItem replaced)
    {
        ItemPrototype proto = item.Prototype;

        // Les contenus d'un sac suivent son emplacement : on refuse de déplacer un sac non vide.
        if (item.ContainerSlot < 0 && proto.IsContainer && BagContents.ContainsKey((uint)item.Slot))
            return InventoryResult.NonEmptyBagOverOtherBag;

        if (container == -1)
        {
            if (slot < 0 || slot > LastBackpackSlot)
                return InventoryResult.ItemDoesntGoToSlot;
            if (slot >= FirstBackpackSlot)
                return InventoryResult.Ok;
            if (slot >= BagSlotStart)
                return GetEquipSlots(proto).Contains(slot) ? InventoryResult.Ok : InventoryResult.ItemDoesntGoToSlot;

            return CanEquip(item, slot, replaced);
        }

        // Banque, rachat et porte-clés ne sont pas pris en charge (pas de PNJ interactif).
        if (container < BagSlotStart || container >= BagSlotEnd)
            return InventoryResult.ItemDoesntGoToSlot;

        PlayerInventoryItem bag = FindItemAt(-1, container);
        if (bag == null || !bag.Prototype.IsContainer)
            return InventoryResult.ItemDoesntGoIntoBag;
        if (slot < 0 || slot >= bag.Prototype.ContainerSlots || slot >= MaxContainerSlots)
            return InventoryResult.ItemDoesntGoToSlot;
        return proto.InventoryType is 18 or 27 ? InventoryResult.ItemDoesntGoIntoBag : InventoryResult.Ok;
    }

    private InventoryResult CanEquip(PlayerInventoryItem item, int slot, PlayerInventoryItem replaced)
    {
        ItemPrototype proto = item.Prototype;
        if (!GetEquipSlots(proto).Contains(slot))
            return InventoryResult.ItemDoesntGoToSlot;
        if (Level < proto.RequiredLevel)
            return InventoryResult.CantEquipLevel;
        if (!MaskAllows(proto.AllowableClass, Class) || !MaskAllows(proto.AllowableRace, Race))
            return InventoryResult.YouCanNeverUseThatItem;

        PlayerInventoryItem mainHand = Equipment[MainHandSlot];
        PlayerInventoryItem offHand = Equipment[OffHandSlot];
        if (slot == MainHandSlot && proto.InventoryType == TwoHandedWeapon && offHand != null && !ReferenceEquals(offHand, item))
            return InventoryResult.CantEquipWithTwoHanded;
        if (slot == OffHandSlot)
        {
            if (mainHand != null && !ReferenceEquals(mainHand, item) && !ReferenceEquals(mainHand, replaced)
                && mainHand.Prototype.InventoryType == TwoHandedWeapon)
                return InventoryResult.CantEquipWithTwoHanded;
            if (proto.InventoryType is 13 or 22 && !Spells.Contains(DualWieldSpell))
                return InventoryResult.CantDualWield;
        }

        return InventoryResult.Ok;
    }

    // Un masque nul ou plein signifie "sans restriction".
    private static bool MaskAllows(uint mask, uint value) =>
        mask == 0 || mask == uint.MaxValue || value is 0 or > 32 || (mask & (1u << (int)(value - 1))) != 0;

    private void SendInventoryFailure(InventoryResult result, PlayerInventoryItem first, PlayerInventoryItem second)
    {
        using var packet = new WorldPacket((ushort)Opcodes.SMSG_INVENTORY_CHANGE_FAILURE, 18);
        packet.WriteByte((byte)result);
        if (result == InventoryResult.CantEquipLevel)
            packet.WriteUInt32(first?.Prototype.RequiredLevel ?? 0);
        packet.WriteUInt64(first?.Guid64 ?? 0);
        packet.WriteUInt64(second?.Guid64 ?? 0);
        packet.WriteByte(0);
        SendPacket(packet);
    }
}
