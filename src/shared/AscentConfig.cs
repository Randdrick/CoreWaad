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

#define STICKY_NPC_MOVEMENT

// Enable/disable 3D geometry calculations.
// Warning: This may be resource-heavy.
// As of the last update, it was nowhere near complete. Only enable for development use.
// #define ENABLE_COLLISION

#if ENABLE_COLLISION
# define COLLISION
#endif

// Use memory mapping for map files for faster access (let OS take care of caching).
// (Currently only available under Windows).
// Only recommended under X64 builds, X86 builds will most likely run out of address space.
#define USE_MEMORY_MAPPING_FOR_MAPS

// Enable/disable movement compression.
// This allows the server to compress long-range creature movement into a buffer and then flush
// it periodically, compressed with deflate. This can make a large difference to server bandwidth.
// Currently, this sort of compression is only used for player and creature movement, although
// it may be expanded in the future.
// Comment if you want to disable it.
#define ENABLE_COMPRESSED_MOVEMENT
#define ENABLE_COMPRESSED_MOVEMENT_FOR_PLAYERS
#define ENABLE_COMPRESSED_MOVEMENT_FOR_CREATURES

// Allow loading of unused test maps.
// #define EXCLUDE_TEST_MAPS

// DATABASE LAYER SETUP

// Enable/disable the use of prepared statements.
// Prepared statements are used to speed up queries.
// Enable if using MySQL, disable if using PostgreSQL or SQLite.
#if !NO_DBLAYER_MYSQL
#define ENABLE_DATABASE_MYSQL
#endif

// #define ENABLE_DATABASE_POSTGRES
// #define ENABLE_DATABASE_SQLITE

// Optimize the server for MySQL usage.
// This may give a small boost to performance.
// Enable it if you do not plan on using Ascent with PostgreSQL or SQLite.
#define OPTIMIZE_SERVER_FOR_MYSQL
