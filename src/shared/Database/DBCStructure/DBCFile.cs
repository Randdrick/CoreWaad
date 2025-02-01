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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// DBCFile class is a simple database file reader. It reads the database file into memory and provides
public class DBCFile
{
    public DBCFile() { }
    ~DBCFile() { }

    // Open database. It must be opened before it can be used.
    public bool Open(string filePath)
    {
        // Implementation for opening the database file
        return true;
    }

    // Dump the buffer to file
    public bool DumpBufferToFile(string fileName)
    {
        // Implementation for dumping buffer to file
        return true;
    }

    // Database exceptions
    public class Exception(string message) : System.Exception(message), System.Exception
    {
    }

    public class NotFound : Exception
    {
        public NotFound() : base("Key was not found") { }
    }

    // Iteration over database
    public class Iterator { }

    public class Record(DBCFile file, IntPtr offset)
    {
        private DBCFile file = file;
        private IntPtr offset = offset;

        public float GetFloat(int field)
        {
            Debug.Assert(field < file.fieldCount);
            return Marshal.PtrToStructure<float>(offset + field * 4);
        }

        public uint GetUInt(int field)
        {
            Debug.Assert(field < file.fieldCount);
            return Marshal.PtrToStructure<uint>(offset + field * 4);
        }

        public int GetInt(int field)
        {
            Debug.Assert(field < file.fieldCount);
            return Marshal.PtrToStructure<int>(offset + field * 4);
        }

        public string GetString(int field)
        {
            Debug.Assert(field < file.fieldCount);
            uint stringOffset = GetUInt(field);
            Debug.Assert(stringOffset < file.stringSize);
            return Marshal.PtrToStringAnsi(file.stringTable + (int)stringOffset);
        }

        // Used by external tool
        public IntPtr GetRowStart()
        {
            return offset;
        }

        public void SetFloat(int field, float value)
        {
            Debug.Assert(field < file.fieldCount);
            Marshal.StructureToPtr(value, offset + field * 4, false);
        }

        public void SetUInt(int field, uint value)
        {
            Debug.Assert(field < file.fieldCount);
            Marshal.StructureToPtr(value, offset + field * 4, false);
        }

        public void SetInt(int field, int value)
        {
            Debug.Assert(field < file.fieldCount);
            Marshal.StructureToPtr(value, offset + field * 4, false);
        }

        public void SetString(int field, string value)
        {
            Debug.Assert(field < file.fieldCount);
            uint stringOffset = GetUInt(field);
            Debug.Assert(stringOffset < file.stringSize);
            Marshal.Copy(value.ToCharArray(), 0, file.stringTable + (int)stringOffset, value.Length);
        }

        public Record Assign(Record src)
        {
            this.file = src.file;
            this.offset = src.offset;
            return this;
        }
    }

    private readonly int fieldCount;
    private readonly int stringSize;
    private IntPtr stringTable;
}
