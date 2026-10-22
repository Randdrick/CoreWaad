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
using WaadShared;

namespace WaadWorldServer;

internal readonly record struct CharacterCreateRequest(
    string Name,
    byte Race,
    byte Class,
    byte Gender,
    byte Skin,
    byte Face,
    byte HairStyle,
    byte HairColor,
    byte FacialHair,
    byte Outfit);

public sealed partial class Player
{
    private PlayerCreateInfo _createInfo;
    private uint _firstStartingItemGuid;

    internal static Player CreateNewCharacter(
        uint guid,
        uint accountId,
        uint firstStartingItemGuid,
        PlayerCreateInfo info,
        CharacterCreateRequest request)
    {
        var session = new ClusterPlayerSession(0, guid, accountId);
        var player = new Player(guid, session)
        {
            _createInfo = info,
            _firstStartingItemGuid = firstStartingItemGuid,
            Name = CapitalizeName(request.Name),
            Race = request.Race,
            Class = request.Class,
            Gender = request.Gender,
            Level = 1,
            Team = ObjectMgr.GetInstance().GetTeamForRace(request.Race),
            BgTeam = ObjectMgr.GetInstance().GetTeamForRace(request.Race),
            PositionX = info.PositionX,
            PositionY = info.PositionY,
            PositionZ = info.PositionZ,
            MapId = info.MapId,
            ZoneId = info.ZoneId,
            BindPositionX = info.PositionX,
            BindPositionY = info.PositionY,
            BindPositionZ = info.PositionZ,
            BindMapId = info.MapId,
            BindZoneId = info.ZoneId,
            FirstLogin = true,
            WatchedFactionIndex = 0xEEEEEEEE,
            PlayerBytes = request.Skin | ((uint)request.Face << 8) | ((uint)request.HairStyle << 16) | ((uint)request.HairColor << 24),
            PlayerBytes2 = request.FacialHair | (2u << 24),
            PlayerBytes3 = request.Gender,
            PlayerFieldBytes = 0x08,
            Height = 1.0f,
            CharacterPoints2 = 2,
        };

        player.SetUInt32Value(EPlayerFields.PLAYER_FIELD_MAX_LEVEL, (uint)Math.Max(1, WorldConfig.LevelCap));
        player.SetUInt32Value(EPlayerFields.PLAYER_NEXT_LEVEL_XP, 400);
        uint displayId = request.Race == 10 ? info.DisplayId - request.Gender : info.DisplayId + request.Gender;
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_DISPLAYID, displayId);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_NATIVEDISPLAYID, displayId);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_FACTIONTEMPLATE, info.FactionTemplate);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_HEALTH, info.Health);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_MAXHEALTH, info.Health);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_POWER1, info.Mana);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_MAXPOWER1, info.Mana);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_MAXPOWER2, info.Rage);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_MAXPOWER3, info.Focus);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_MAXPOWER4, info.Energy);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_MAXPOWER6, 8);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_MAXPOWER7, info.Runic);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_BASE_HEALTH, info.Health);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_BASE_MANA, info.Mana);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_ATTACK_POWER, info.AttackPower);
        player.SetFloatValue(EObjectFields.OBJECT_FIELD_SCALE_X, 1.0f);
        player.SetFloatValue(EUnitFields.UNIT_FIELD_BOUNDINGRADIUS, 0.388999998569489f);
        player.SetFloatValue(EUnitFields.UNIT_FIELD_COMBATREACH, 1.5f);

        player.SetByteValue(EUnitFields.UNIT_FIELD_BYTES_0, 3, GetPowerType(request.Class));
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_STAT0, info.Strength);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_STAT1, info.Agility);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_STAT2, info.Stamina);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_STAT3, info.Intellect);
        player.SetUInt32Value(EUnitFields.UNIT_FIELD_STAT4, info.Spirit);

        ObjectMgr objectMgr = ObjectMgr.GetInstance();
        foreach (CreateInfoSkill skill in info.Skills)
        {
            if (!objectMgr.TryGetSkillLine(skill.SkillId, out ObjectMgr.SkillLineLookupEntry skillLine)
                || skillLine.type == PlayerConstants.SKILL_TYPE_LANGUAGE)
                continue;

            player.Skills[skill.SkillId] = new PlayerSkill
            {
                Type = skillLine.type,
                CurrentValue = skill.CurrentValue,
                MaximumValue = skill.MaximumValue,
            };
            player.ApplySkillProficiency(skill.SkillId);
        }

        foreach (uint spell in info.Spells)
            player.Spells.Add(spell);

        foreach (CreateInfoActionBar action in info.ActionBars)
        {
            if (action.Button >= (uint)player.Actions.Length)
                continue;

            player.Actions[action.Button] = new ActionButton
            {
                Action = (ushort)action.Action,
                Type = (byte)action.Type,
                Misc = (byte)action.Misc,
            };
        }

        player.LoadInitialReputation(objectMgr);
        player.InitializeStartingTaxiMask();
        return player;
    }

    private static string CapitalizeName(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    private static byte GetPowerType(byte classId) => classId switch
    {
        1 => 1,
        4 => 3,
        _ => 0,
    };

    private void InitializeStartingTaxiMask()
    {
        if (Class == 6)
        {
            uint[] deathKnightMask =
            [
                0xFFFFFFFB, 0xF3FFFFFF, 0x317EFFFF, 0, 0x02004000, 0x001400E0,
                0xC1C02014, 0x00012018, 0x00000380, 0x7C000C10, 0,
            ];
            for (int index = 0; index < deathKnightMask.Length; index++)
                TaxiMask[index] |= deathKnightMask[index];
        }

        switch (Race)
        {
            case 1: SetTaximaskNode(2); break;
            case 2: SetTaximaskNode(23); break;
            case 3: SetTaximaskNode(6); break;
            case 4: SetTaximaskNode(26); SetTaximaskNode(27); break;
            case 5: SetTaximaskNode(11); break;
            case 6: SetTaximaskNode(22); break;
            case 7: SetTaximaskNode(6); break;
            case 8: SetTaximaskNode(23); break;
            case 10: SetTaximaskNode(82); break;
            case 11: SetTaximaskNode(94); break;
        }

        if (Team == PlayerTeam.FACTION_ALLY)
            SetTaximaskNode(100);
        else
            SetTaximaskNode(99);
    }
}
