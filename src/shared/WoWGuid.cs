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

public class WoWGuid
{
    private ulong oldguid;
    private byte guidmask;
    private readonly byte[] guidfields = new byte[8];
    private byte fieldcount;
    private bool compiled;

    public WoWGuid()
    {
        Clear();
    }

    public WoWGuid(ulong guid)
    {
        Clear();
        Init(guid);
    }

    public WoWGuid(byte mask)
    {
        Clear();
        Init(mask);
    }

    public WoWGuid(byte mask, byte[] fields)
    {
        Clear();
        Init(mask, fields);
    }

    ~WoWGuid()
    {
        Clear();
    }

    public void Clear()
    {
        oldguid = 0;
        guidmask = 0;
        Array.Clear(guidfields, 0, guidfields.Length);
        compiled = false;
        fieldcount = 0;
    }

    public void Init(ulong guid)
    {
        Clear();
        oldguid = guid;
        CompileByOld();
    }

    public void Init(byte mask)
    {
        Clear();
        guidmask = mask;
        if (guidmask == 0)
            CompileByNew();
    }

    public void Init(byte mask, byte[] fields)
    {
        Clear();
        guidmask = mask;
        if (BitCount8(guidmask) == 0)
            return;

        for (int i = 0; i < BitCount8(guidmask); i++)
            guidfields[i] = fields[i];

        fieldcount = (byte)BitCount8(guidmask);
        CompileByNew();
    }

    public ulong GetOldGuid() => oldguid;
    public byte[] GetNewGuid() => guidfields;
    public byte GetNewGuidLen() => (byte)BitCount8(guidmask);
    public byte GetNewGuidMask() => guidmask;

    public void AppendField(byte field)
    {
        if (compiled)
            throw new InvalidOperationException("Cannot append field when compiled.");
        if (fieldcount >= BitCount8(guidmask))
            throw new InvalidOperationException("Field count exceeds guid mask bit count.");

        guidfields[fieldcount++] = field;

        if (fieldcount == BitCount8(guidmask))
            CompileByNew();
    }

    private void CompileByOld()
    {
        if (compiled)
            throw new InvalidOperationException("Already compiled.");

        fieldcount = 0;

        for (uint x = 0; x < 8; x++)
        {
            byte p = BitConverter.GetBytes(oldguid)[x];
            if (p != 0)
            {
                guidfields[fieldcount++] = p;
                guidmask |= (byte)(1 << (int)x);
            }
        }
        compiled = true;
    }

    private void CompileByNew()
    {
        if (compiled && fieldcount != BitCount8(guidmask))
            throw new InvalidOperationException("Field count does not match guid mask bit count.");

        int j = 0;

        if ((guidmask & 0x01) != 0)
            oldguid |= (ulong)guidfields[j++];
        if ((guidmask & 0x02) != 0)
            oldguid |= (ulong)guidfields[j++] << 8;
        if ((guidmask & 0x04) != 0)
            oldguid |= (ulong)guidfields[j++] << 16;
        if ((guidmask & 0x08) != 0)
            oldguid |= (ulong)guidfields[j++] << 24;
        if ((guidmask & 0x10) != 0)
            oldguid |= (ulong)guidfields[j++] << 32;
        if ((guidmask & 0x20) != 0)
            oldguid |= (ulong)guidfields[j++] << 40;
        if ((guidmask & 0x40) != 0)
            oldguid |= (ulong)guidfields[j++] << 48;
        if ((guidmask & 0x80) != 0)
            oldguid |= (ulong)guidfields[j++] << 56;

        compiled = true;
    }

    private static int BitCount8(byte x)
    {
        x = (byte)((x & 0x55) + ((x >> 1) & 0x55));
        x = (byte)((x & 0x33) + ((x >> 2) & 0x33));
        x = (byte)((x & 0x0F) + ((x >> 4) & 0x0F));
        return x;
    }
}
