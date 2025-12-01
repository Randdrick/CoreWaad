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
 
namespace WaadRealmServer;

public enum ChannelType
{
    General = 1,
    Commerce = 2,
    DefenseLocal = 22,
    DefenseUniverselle = 23,
    RecrutementGuilde = 25,
    RechercheGroupe = 26
}

[System.Flags]
public enum ChannelFlags
{
    None = 0x00,
    Owner = 0x01,
    Moderator = 0x02,
    Voiced = 0x04,
    Muted = 0x08,
    Custom = 0x10,
    MicrophoneMute = 0x20
}

public enum ChannelNotifyFlags
{
    Joined = 0x00,
    Left = 0x01,
    YouJoined = 0x02,
    YouLeft = 0x03,
    WrongPass = 0x04,
    NotOn = 0x05,
    NotMod = 0x06,
    SetPass = 0x07,
    ChangeOwner = 0x08,
    NotOn2 = 0x09,
    NotOwner = 0x0A,
    WhoOwner = 0x0B,
    ModeChange = 0x0C,
    EnableAnnounce = 0x0D,
    DisableAnnounce = 0x0E,
    Moderated = 0x0F,
    Unmoderated = 0x10,
    YouCantSpeak = 0x11,
    Kicked = 0x12,
    YourBanned = 0x13,
    Banned = 0x14,
    Unbanned = 0x15,
    Unknown1 = 0x16,
    AlreadyOn = 0x17,
    Invited = 0x18,
    WrongFaction = 0x19,
    Unknown2 = 0x1A,
    Unknown3 = 0x1B,
    Unknown4 = 0x1C,
    YouInvited = 0x1D,
    Unknown5 = 0x1E,
    Unknown6 = 0x1F,
    Unknown7 = 0x20,
    NotInLFG = 0x21,
    VoiceOn = 0x22,
    VoiceOff = 0x23
}
