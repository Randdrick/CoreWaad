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

public enum SessionStatus
{
    STATUS_AUTHED = 0,
    STATUS_LOGGEDIN
}

[System.Flags]
public enum AccountFlagsEnum : uint
{
    ACCOUNT_FLAG_VIP = 0x1,
    ACCOUNT_FLAG_NO_AUTOJOIN = 0x2,
    ACCOUNT_FLAG_XPACK_01 = 0x8,
    ACCOUNT_FLAG_XPACK_02 = 0x10,
}
