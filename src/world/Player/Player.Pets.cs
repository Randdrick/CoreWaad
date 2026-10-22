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

public sealed class PlayerPetInfo
{
    public uint Number { get; init; }
    public string Name { get; init; } = string.Empty;
    public uint Entry { get; init; }
    public string Fields { get; init; } = string.Empty;
    public uint Experience { get; init; }
    public byte ActiveState { get; init; }
    public uint Level { get; init; }
    public uint LoyaltyPoints { get; init; }
    public uint LoyaltyLevel { get; init; }
    public uint HappinessUpdate { get; init; }
    public bool Summon { get; init; }
    public uint[] ActionBarSpells { get; } = new uint[10];
    public uint[] ActionBarStates { get; } = new uint[10];

    public bool IsActive => ActiveState % 10 > 0;
    public byte StableState => (byte)(ActiveState / 10);
}

public sealed partial class Player
{
    public Dictionary<uint, PlayerPetInfo> Pets { get; } = [];
    public Dictionary<uint, HashSet<uint>> SummonSpells { get; } = [];
    public uint ActivePetNumber { get; private set; }
    public uint PetNumberMax { get; private set; }
    public PlayerPetInfo ActivePet =>
        ActivePetNumber != 0 && Pets.TryGetValue(ActivePetNumber, out PlayerPetInfo pet) ? pet : null;

    private void LoadPets(QueryResult result)
    {
        Pets.Clear();
        ActivePetNumber = 0;
        PetNumberMax = 0;
        if (result == null)
            return;

        while (result.NextRow())
        {
            if (result.FieldCount < 12)
            {
                CLog.Warning("[Player]", "Ligne playerpets incomplete pour le joueur {0}.", Guid);
                continue;
            }

            uint number = ToUInt32(result.GetValue(1));
            if (number == 0 || Pets.ContainsKey(number))
            {
                CLog.Warning("[Player]", "Familier playerpets invalide ou duplique pour le joueur {0}: {1}.", Guid, number);
                continue;
            }

            var pet = new PlayerPetInfo
            {
                Number = number,
                Name = ToStringValue(result.GetValue(2)),
                Entry = ToUInt32(result.GetValue(3)),
                Fields = ToStringValue(result.GetValue(4)),
                Experience = ToUInt32(result.GetValue(5)),
                ActiveState = (byte)ToUInt32(result.GetValue(6)),
                Level = ToUInt32(result.GetValue(7)),
                LoyaltyPoints = ToUInt32(result.GetValue(8)),
                LoyaltyLevel = ToUInt32(result.GetValue(9)),
                HappinessUpdate = ToUInt32(result.GetValue(10)),
                Summon = ToUInt32(result.GetValue(11)) != 0,
            };
            Pets.Add(number, pet);
            if (pet.IsActive && ActivePetNumber == 0)
                ActivePetNumber = number;
            if (number > PetNumberMax)
                PetNumberMax = number;
        }
    }

    private void LoadPetSpells(QueryResult result)
    {
        SummonSpells.Clear();
        if (result == null)
            return;

        while (result.NextRow())
        {
            if (result.FieldCount < 3)
                continue;

            uint entry = ToUInt32(result.GetValue(1));
            uint spell = ToUInt32(result.GetValue(2));
            if (entry == 0 || spell == 0)
                continue;
            if (!SummonSpells.TryGetValue(entry, out HashSet<uint> spells))
            {
                spells = [];
                SummonSpells.Add(entry, spells);
            }
            spells.Add(spell);
        }
    }

    private void LoadPetActionBars(QueryResult result)
    {
        if (result == null)
            return;

        while (result.NextRow())
        {
            if (result.FieldCount < 22)
                continue;

            uint number = ToUInt32(result.GetValue(1));
            if (!Pets.TryGetValue(number, out PlayerPetInfo pet))
                continue;

            for (int index = 0; index < pet.ActionBarSpells.Length; index++)
                pet.ActionBarSpells[index] = ToUInt32(result.GetValue(2 + index));
            for (int index = 0; index < pet.ActionBarStates.Length; index++)
                pet.ActionBarStates[index] = ToUInt32(result.GetValue(12 + index));
        }
    }

    internal bool SpawnPet(uint petNumber)
    {
        if (!Pets.TryGetValue(petNumber, out PlayerPetInfo pet) || pet.Entry == 0)
            return false;

        ActivePetNumber = petNumber;
        SetUInt32Value(EUnitFields.UNIT_FIELD_PET_LGUID, pet.Number);
        SetUInt32Value(EUnitFields.UNIT_FIELD_PET_NAME_TIMESTAMP, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        SetUInt32Value(EUnitFields.UNIT_FIELD_PETEXPERIENCE, pet.Experience);
        SetUInt32Value(EUnitFields.UNIT_FIELD_PETNEXTLEVELEXP, 0);
        UpdateMask.SetBit(EUnitFields.UNIT_FIELD_PET_LGUID);
        UpdateMask.SetBit(EUnitFields.UNIT_FIELD_PET_NAME_TIMESTAMP);
        UpdateMask.SetBit(EUnitFields.UNIT_FIELD_PETEXPERIENCE);
        UpdateMask.SetBit(EUnitFields.UNIT_FIELD_PETNEXTLEVELEXP);
        SendPetSpells(pet);
        return true;
    }

    internal void SpawnActivePet()
    {
        if (ActivePetNumber != 0)
            SpawnPet(ActivePetNumber);
    }

    internal void DismissPet()
    {
        ActivePetNumber = 0;
        SetUInt32Value(EUnitFields.UNIT_FIELD_PET_LGUID, 0);
        SetUInt32Value(EUnitFields.UNIT_FIELD_PET_NAME_TIMESTAMP, 0);
        SetUInt32Value(EUnitFields.UNIT_FIELD_PETEXPERIENCE, 0);
        SetUInt32Value(EUnitFields.UNIT_FIELD_PETNEXTLEVELEXP, 0);
        UpdateMask.SetBit(EUnitFields.UNIT_FIELD_PET_LGUID);
        UpdateMask.SetBit(EUnitFields.UNIT_FIELD_PET_NAME_TIMESTAMP);
        UpdateMask.SetBit(EUnitFields.UNIT_FIELD_PETEXPERIENCE);
        UpdateMask.SetBit(EUnitFields.UNIT_FIELD_PETNEXTLEVELEXP);

        using var packet = new WorldPacket((ushort)Opcodes.SMSG_PET_SPELLS, 12);
        packet.WriteUInt64(0);
        packet.WriteUInt32(0);
        SendPacket(packet);
    }

    private void SendPetSpells(PlayerPetInfo pet)
    {
        HashSet<uint> spells = SummonSpells.TryGetValue(pet.Entry, out HashSet<uint> knownSpells)
            ? knownSpells
            : [];
        int spellCount = Math.Min(spells.Count, byte.MaxValue);
        using var packet = new WorldPacket((ushort)Opcodes.SMSG_PET_SPELLS, 80 + pet.ActionBarSpells.Length * 4 + spellCount * 4);
        packet.WriteUInt64(pet.Number);
        packet.WriteUInt16(0);
        packet.WriteUInt32(0);
        packet.WriteByte(0);
        packet.WriteByte(0);
        packet.WriteUInt16(0);

        foreach (uint action in pet.ActionBarSpells)
        {
            if ((action & 0x04000000) != 0)
                packet.WriteUInt32(action);
            else
            {
                packet.WriteUInt16((ushort)action);
                packet.WriteByte(0);
                packet.WriteByte(0);
            }
        }

        packet.WriteByte((byte)spellCount);
        foreach (uint spell in spells.OrderBy(spell => spell).Take(spellCount))
        {
            packet.WriteUInt16((ushort)spell);
            packet.WriteUInt16(0);
        }
        packet.WriteByte(0);
        SendPacket(packet);
    }

    private void AddPetStatements(List<DatabaseStatement> statements)
    {
        AddDelete("playerpets");
        AddDelete("playerpetactionbar");
        AddDelete("playersummonspells");

        foreach (PlayerPetInfo pet in Pets.Values.OrderBy(pet => pet.Number))
        {
            AddInsert("playerpets", [Guid, pet.Number, pet.Name, pet.Entry, pet.Fields, pet.Experience,
                pet.ActiveState, pet.Level, pet.LoyaltyPoints, pet.LoyaltyLevel, pet.HappinessUpdate, pet.Summon ? 1u : 0u]);
            AddInsert("playerpetactionbar", [Guid, pet.Number, .. pet.ActionBarSpells.Cast<object>(), .. pet.ActionBarStates.Cast<object>()]);
        }

        foreach (var entry in SummonSpells.OrderBy(entry => entry.Key))
            foreach (uint spell in entry.Value.OrderBy(spell => spell))
                AddInsert("playersummonspells", [Guid, entry.Key, spell]);

        void AddDelete(string table) => statements.Add(new DatabaseStatement(
            $"DELETE FROM {table} WHERE ownerguid=@guid",
            new Dictionary<string, object> { ["@guid"] = Guid }));

        void AddInsert(string table, object[] values)
        {
            var parameters = new Dictionary<string, object>();
            for (int index = 0; index < values.Length; index++)
                parameters["@value" + index] = values[index];
            statements.Add(new DatabaseStatement(
                $"INSERT INTO {table} VALUES ({string.Join(",", parameters.Keys)})", parameters));
        }
    }
}
