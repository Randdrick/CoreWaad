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
using WaadShared;
using WaadShared.Database;

namespace WaadWorldServer;

public sealed class ItemPrototype
{
    public uint ItemId { get; init; }
    public uint Class { get; init; }
    public uint SubClass { get; init; }
    public string Name { get; init; } = string.Empty;
    public uint DisplayInfoId { get; init; }
    public uint Quality { get; init; }
    public uint Flags { get; init; }
    public uint BuyPrice { get; init; }
    public uint SellPrice { get; init; }
    public uint InventoryType { get; init; }
    public uint AllowableClass { get; init; }
    public uint AllowableRace { get; init; }
    public uint ItemLevel { get; init; }
    public uint RequiredLevel { get; init; }
    public uint RequiredSkill { get; init; }
    public uint RequiredSkillRank { get; init; }
    public uint Unique { get; init; }
    public uint MaxCount { get; init; }
    public uint ContainerSlots { get; init; }
    public uint StatsCount { get; init; }
    public uint[] StatsData { get; } = new uint[20];
    public float[] DamageData { get; } = new float[15];
    public uint Armor { get; init; }
    public uint HolyRes { get; init; }
    public uint FireRes { get; init; }
    public uint NatureRes { get; init; }
    public uint FrostRes { get; init; }
    public uint ShadowRes { get; init; }
    public uint ArcaneRes { get; init; }
    public uint Delay { get; init; }
    public uint AmmoType { get; init; }
    public float Range { get; init; }
    public uint[] SpellsData { get; } = new uint[30];
    public uint Bonding { get; init; }
    public uint PageId { get; init; }
    public uint QuestId { get; init; }
    public uint LockId { get; init; }
    public uint SheathId { get; init; }
    public uint RandomPropId { get; init; }
    public uint Block { get; init; }
    public uint ItemSet { get; init; }
    public uint MaxDurability { get; init; }
    public uint ZoneNameId { get; init; }
    public uint MapId { get; init; }
    public uint BagFamily { get; init; }
    public uint[] SocketsData { get; } = new uint[9];
    public uint SocketBonus { get; init; }
    public uint GemProperties { get; init; }
    public int DisenchantRequiredSkill { get; init; }
    public uint ArmorDamageModifier { get; init; }
    public uint ExistingDuration { get; init; }
    public uint ItemLimitCategory { get; init; }
    public uint HolidayId { get; init; }

    public bool IsContainer => InventoryType == 18 && ContainerSlots > 0;
    public bool IsEquipment => InventoryType is >= 1 and <= 24;
}

internal static class ItemPrototypeReader
{
    public static ItemPrototype Read(QueryResult result, IReadOnlyDictionary<string, int> columns)
    {
        var prototype = new ItemPrototype
        {
            ItemId = UInt(result, columns, "entry", "itemid", "id"),
            Class = UInt(result, columns, "class"),
            SubClass = UInt(result, columns, "subclass"),
            Name = Text(result, columns, "name1", "name"),
            DisplayInfoId = UInt(result, columns, "displayid", "displayinfoid"),
            Quality = UInt(result, columns, "quality"),
            Flags = UInt(result, columns, "flags"),
            BuyPrice = UInt(result, columns, "buyprice"),
            SellPrice = UInt(result, columns, "sellprice"),
            InventoryType = UInt(result, columns, "inventorytype"),
            AllowableClass = UInt(result, columns, "allowableclass"),
            AllowableRace = UInt(result, columns, "allowablerace"),
            ItemLevel = UInt(result, columns, "itemlevel"),
            RequiredLevel = UInt(result, columns, "requiredlevel"),
            RequiredSkill = UInt(result, columns, "requiredskill"),
            RequiredSkillRank = UInt(result, columns, "requiredskillrank"),
            Unique = UInt(result, columns, "unique"),
            MaxCount = UInt(result, columns, "maxcount"),
            ContainerSlots = UInt(result, columns, "containerslots"),
            StatsCount = UInt(result, columns, "statscount"),
            Armor = UInt(result, columns, "armor"),
            HolyRes = UInt(result, columns, "holyres"),
            FireRes = UInt(result, columns, "fireres"),
            NatureRes = UInt(result, columns, "natureres"),
            FrostRes = UInt(result, columns, "frostres"),
            ShadowRes = UInt(result, columns, "shadowres"),
            ArcaneRes = UInt(result, columns, "arcaneres"),
            Delay = UInt(result, columns, "delay"),
            AmmoType = UInt(result, columns, "ammotype"),
            Range = Single(result, columns, "range"),
            Bonding = UInt(result, columns, "bonding"),
            PageId = UInt(result, columns, "pageid"),
            QuestId = UInt(result, columns, "questid"),
            LockId = UInt(result, columns, "lockid"),
            SheathId = UInt(result, columns, "sheathid"),
            RandomPropId = UInt(result, columns, "randomproperty"),
            Block = UInt(result, columns, "block"),
            ItemSet = UInt(result, columns, "itemset"),
            MaxDurability = UInt(result, columns, "maxdurability"),
            ZoneNameId = UInt(result, columns, "zonenameid"),
            MapId = UInt(result, columns, "mapid"),
            BagFamily = UInt(result, columns, "bagfamily"),
            SocketBonus = UInt(result, columns, "socketbonus"),
            GemProperties = UInt(result, columns, "gemproperties"),
            DisenchantRequiredSkill = Int(result, columns, "disenchantreqskill"),
            ArmorDamageModifier = UInt(result, columns, "armordamagemodifier"),
            ExistingDuration = UInt(result, columns, "duration"),
            ItemLimitCategory = UInt(result, columns, "itemlimitcategory"),
            HolidayId = UInt(result, columns, "holidayid"),
        };

        for (int index = 0; index < prototype.StatsData.Length; index++)
        {
            string column = index % 2 == 0 ? $"stat_type{index / 2 + 1}" : $"stat_value{index / 2 + 1}";
            prototype.StatsData[index] = UInt(result, columns, column);
        }
        for (int index = 0; index < prototype.DamageData.Length; index++)
        {
            int damageIndex = index / 3 + 1;
            string column = (index % 3) switch
            {
                0 => $"dmg_min{damageIndex}",
                1 => $"dmg_max{damageIndex}",
                _ => $"dmg_type{damageIndex}",
            };
            prototype.DamageData[index] = Single(result, columns, column);
        }
        for (int index = 0; index < prototype.SpellsData.Length; index++)
        {
            int spellIndex = index / 6 + 1;
            string column = (index % 6) switch
            {
                0 => $"spellid_{spellIndex}",
                1 => $"spelltrigger_{spellIndex}",
                2 => $"spellcharges_{spellIndex}",
                3 => $"spellcooldown_{spellIndex}",
                4 => $"spellcategory_{spellIndex}",
                _ => $"spellcategorycooldown_{spellIndex}",
            };
            prototype.SpellsData[index] = UInt(result, columns, column);
        }
        for (int index = 0; index < prototype.SocketsData.Length; index++)
        {
            int socketIndex = index / 3 + 1;
            string column = (index % 3) switch
            {
                0 => $"socket_color_{socketIndex}",
                1 => $"unk201_{index + 3}",
                _ => "socketbonus",
            };
            prototype.SocketsData[index] = UInt(result, columns, column);
        }

        return prototype;
    }

    private static int Find(IReadOnlyDictionary<string, int> columns, params string[] names)
    {
        foreach (string name in names)
            if (columns.TryGetValue(name, out int index))
                return index;
        return -1;
    }

    private static uint UInt(QueryResult result, IReadOnlyDictionary<string, int> columns, params string[] names)
    {
        int index = Find(columns, names);
        return index < 0 ? 0 : Convert.ToUInt32(result.GetValue(index), CultureInfo.InvariantCulture);
    }

    private static int Int(QueryResult result, IReadOnlyDictionary<string, int> columns, params string[] names)
    {
        int index = Find(columns, names);
        return index < 0 ? 0 : Convert.ToInt32(result.GetValue(index), CultureInfo.InvariantCulture);
    }

    private static float Single(QueryResult result, IReadOnlyDictionary<string, int> columns, params string[] names)
    {
        int index = Find(columns, names);
        return index < 0 ? 0 : Convert.ToSingle(result.GetValue(index), CultureInfo.InvariantCulture);
    }

    private static string Text(QueryResult result, IReadOnlyDictionary<string, int> columns, params string[] names)
    {
        int index = Find(columns, names);
        return index < 0 ? string.Empty : Convert.ToString(result.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
