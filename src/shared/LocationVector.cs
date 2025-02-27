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

namespace WaadShared;

public class LocationVector
{
    // Constructors
    public LocationVector(float X, float Y, float Z)
    {
        x = X;
        y = Y;
        z = Z;
        o = 0;
    }

    public LocationVector(float X, float Y, float Z, float O)
    {
        x = X;
        y = Y;
        z = Z;
        o = O;
    }

    public LocationVector()
    {
        x = 0;
        y = 0;
        z = 0;
        o = 0;
    }

    // (dx * dx + dy * dy + dz * dz)
    public float DistanceSq(LocationVector comp)
    {
        float delta_x = comp.x - x;
        float delta_y = comp.y - y;
        float delta_z = comp.z - z;

        return (delta_x * delta_x + delta_y * delta_y + delta_z * delta_z);
    }

    public float DistanceSq(float X, float Y, float Z)
    {
        float delta_x = X - x;
        float delta_y = Y - y;
        float delta_z = Z - z;

        return (delta_x * delta_x + delta_y * delta_y + delta_z * delta_z);
    }

    // sqrt(dx * dx + dy * dy + dz * dz)
    public float Distance(LocationVector comp)
    {
        float delta_x = comp.x - x;
        float delta_y = comp.y - y;
        float delta_z = comp.z - z;

        return (float)Math.Sqrt(delta_x * delta_x + delta_y * delta_y + delta_z * delta_z);
    }

    public float Distance(float X, float Y, float Z)
    {
        float delta_x = X - x;
        float delta_y = Y - y;
        float delta_z = Z - z;

        return (float)Math.Sqrt(delta_x * delta_x + delta_y * delta_y + delta_z * delta_z);
    }

    public float Distance2DSq(LocationVector comp)
    {
        float delta_x = comp.x - x;
        float delta_y = comp.y - y;
        return (delta_x * delta_x + delta_y * delta_y);
    }

    public float Distance2DSq(float X, float Y)
    {
        float delta_x = X - x;
        float delta_y = Y - y;
        return (delta_x * delta_x + delta_y * delta_y);
    }

    public float Distance2D(LocationVector comp)
    {
        float delta_x = comp.x - x;
        float delta_y = comp.y - y;
        return (float)Math.Sqrt(delta_x * delta_x + delta_y * delta_y);
    }

    public float Distance2D(float X, float Y)
    {
        float delta_x = X - x;
        float delta_y = Y - y;
        return (float)Math.Sqrt(delta_x * delta_x + delta_y * delta_y);
    }

    // atan2(dx / dy)
    public float CalcAngTo(LocationVector dest)
    {
        float dx = dest.x - x;
        float dy = dest.y - y;
        if (dy != 0.0f)
            return (float)Math.Atan2(dy, dx);
        else
            return 0.0f;
    }

    public float CalcAngFrom(LocationVector src)
    {
        float dx = x - src.x;
        float dy = y - src.y;
        if (dy != 0.0f)
            return (float)Math.Atan2(dy, dx);
        else
            return 0.0f;
    }

    public void ChangeCoords(float X, float Y, float Z, float O)
    {
        x = X;
        y = Y;
        z = Z;
        o = O;
    }

    public void ChangeCoords(float X, float Y, float Z)
    {
        x = X;
        y = Y;
        z = Z;
    }

    // add/subtract/equality vectors
    public static LocationVector operator +(LocationVector a, LocationVector b)
    {
        return new LocationVector(a.x + b.x, a.y + b.y, a.z + b.z, a.o + b.o);
    }

    public static LocationVector operator -(LocationVector a, LocationVector b)
    {
        return new LocationVector(a.x - b.x, a.y - b.y, a.z - b.z, a.o - b.o);
    }

    public static bool operator ==(LocationVector a, LocationVector b)
    {
        return a.x == b.x && a.y == b.y && a.z == b.z;
    }

    public static bool operator !=(LocationVector a, LocationVector b)
    {
        return !(a == b);
    }

    public override bool Equals(object obj)
    {
        if (obj is LocationVector)
        {
            LocationVector other = (LocationVector)obj;
            return this == other;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
    }

    public float x;
    public float y;
    public float z;
    public float o;
}
