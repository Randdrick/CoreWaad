/*
 * Ascent MMORPG Server
 * Copyright (C) 2005-2008 Ascent Team <http://www.ascentcommunity.com/>
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

#define SAFE_DBC_CODE_RETURNS // undefine this to make out of range/nulls return null.
#undef USING_BIG_ENDIAN

using System;
using System.IO;
using System.Linq;

public static partial class DBCStores
{
    public class DBCStorage<T> where T : new() 
    {
        private T[] m_heapBlock;
        private T m_firstEntry;
        private T[] m_entries;
        private uint m_max;
        private uint m_numrows;
        private uint m_stringlength;
        private string m_stringData;

        public class Iterator(T ip = default)
        {
            private T p = ip;

            public Iterator Increment() { p = Add(p, 1); return this; }

            private static T Add(T p, int v)
            {
                dynamic dp = p;
                return dp + v;
            }

            public Iterator Decrement() {  p = Subtract(p, 1); return this; }

            private static T Subtract(T p, int v)
            {
                dynamic dp = p;
                return dp - v;
            }

            public bool NotEqual(Iterator i) { return !p.Equals(i.p); }
            public T Dereference() => p;
        }

        public Iterator Begin()
        {
        return new Iterator(m_heapBlock[0]);
        }

        public Iterator End()
        {
        return new Iterator(m_heapBlock[m_numrows]);
        }

        public DBCStorage()
        {
        m_heapBlock = null;
        m_entries = null;
        m_firstEntry = default(T);
        m_max = 0;
        m_numrows = 0;
        m_stringlength = 0;
        m_stringData = null;
        }

        ~DBCStorage()
        {
            if (m_heapBlock == null)
            {
            }
            else
                m_heapBlock = null;
            if (m_entries != null)
                m_entries = null;
            if (m_stringData == null)
                return;
            m_stringData = null;
        }

        public bool Load(
            string filename, string format, bool loadIndexed, bool loadStrings)
        {
            uint rows;
            uint cols;
            uint uselessShit;
            uint stringLength;
            uint header;
            long pos;
            m_entries = null;

            using FileStream fs = new(filename, FileMode.Open, FileAccess.Read);
            if (fs != null)
            {
                BinaryReader binaryReader = new(fs);
                using BinaryReader reader = binaryReader;
                // Read the number of rows, and allocate our block on the heap
                header = reader.ReadUInt32();
                rows = reader.ReadUInt32();
                cols = reader.ReadUInt32();
                uselessShit = reader.ReadUInt32();
                stringLength = reader.ReadUInt32();
                pos = fs.Position;

                if (loadStrings)
                {
                    fs.Seek(20 + (rows * cols * 4), SeekOrigin.Begin);
                    char[] stringDataChars = reader.ReadChars((int)stringLength);
                    m_stringData = new string(stringDataChars);
                    m_stringlength = stringLength;
                }

                fs.Seek(pos, SeekOrigin.Begin);

                m_heapBlock = new T[rows];

                // Read the data for each row
                for (uint i = 0; i < rows; ++i)
                {
                    m_heapBlock[i] = new T();
                    ReadEntry(reader, ref m_heapBlock[i], format, cols, filename);

                    if (loadIndexed)
                    {
                        // All the time the first field in the dbc is our unique entry
                        uint entry = System.Convert.ToUInt32(m_heapBlock[i]);
                        if (entry > m_max)
                        {
                            m_max = entry;
                        }
                    }
                }

                if (loadIndexed)
                {
                    m_entries = new T[m_max + 1];

                    for (uint i = 0; i < rows; ++i)
                    {
                        m_firstEntry ??= m_heapBlock[i];

                        uint entry = Convert.ToUInt32(m_heapBlock[i]);
                        m_entries[entry] = m_heapBlock[i];
                    }
                }

                m_numrows = rows;

                return true;
            }
            Console.WriteLine("!!! Failed to open file {0}", filename);
            return false;
        }
        public void ReadEntry(BinaryReader reader, ref T dest, string format, uint cols, string filename)
        {
            char[] t = format.ToCharArray();
            uint[] dest_ptr = (uint[])(object)dest;
            uint c = 0;
            uint val;
            int len = format.Length;
            if (len != cols)
                Console.WriteLine($"!!! possible invalid format in file {filename} (us: {len}, them: {cols})");

            int index = 0;
            while (index < t.Length && t[index] != 0)
            {
                if ((++c) > cols)
                {
                    index++;
                    Console.WriteLine($"!!! Read buffer overflow in DBC reading of file {filename}");
                    continue;
                }
                else
                {
                    val = reader.ReadUInt32();
                    if (t[index] == 'x')
                    {
                        index++;
                        continue; // skip!
                    }

#if USING_BIG_ENDIAN
                    val = Swap32(val);
#endif

                    if (t[index] == 's')
                    {
                        string[] new_ptr = (string[])(object)dest_ptr;
                        const string null_str = "";
                        string ptr;
                        if (val < m_stringlength)
                        {
                            ptr = new string(m_stringData.ToCharArray(), (int)val, m_stringData.Length - (int)val);
                            // Filtre Unicode vers utf8 (Lecture zone de texte des DBCs Fr)
                            // ChangeUnicode2ExtAscii(ptr); 
                            //------------
                        }
                        else
                            ptr = null_str;

                        new_ptr[0] = ptr;
                        new_ptr = [.. new_ptr.Skip(1)];
                        dest_ptr = (uint[])(object)new_ptr;
                    }
                    else
                    {
                        dest_ptr[0] = val;
                        dest_ptr = [.. dest_ptr.Skip(1)];
                    }

                    index++;
                }
            }
        }

#if USING_BIG_ENDIAN
        private static uint Swap32(uint val)
        {
            return ((val & 0x000000FF) << 24) |
           ((val & 0x0000FF00) << 8) |
           ((val & 0x00FF0000) >> 8) |
           ((val & 0xFF000000) >> 24);
        }
#endif
        public uint GetNumRows() => m_numrows;

        public T LookupEntryForced(uint i)
        {
            if (m_entries != null)
            {
                if (i > m_max || m_entries[i] == null)
                {
                    Console.WriteLine($"LookupEntryForced failed for entry {i}");
                    return default(T);
                }
                else
                {
                    return m_entries[i];
                }
            }
            else
            {
                if (i < m_numrows)
                {
                    return m_heapBlock[i];
                }
                else
                {
                    return default(T);
                }
            }
        }

        public void SetRow(uint i, T t)
        {
            if (i >= m_max || m_entries == null)
            {
                return;
            }
            m_entries[i] = t;
        }
        public T LookupEntry(uint i)
        {
#if SAFE_DBC_CODE_RETURNS
            if (m_entries != null)
            {
                if (i > m_max || m_entries[i] == null)
                    return m_firstEntry;
                else
                    return m_entries[i];
            }
            else
            {
                if (i >= m_numrows)
                    return m_heapBlock[0];
                else
                    return m_heapBlock[i];
            }
#else
            if (m_entries != null)
            {
                if (i > m_max || m_entries[i] == null)
                    return default(T);
                else
                    return m_entries[i];
            }
            else
            {
                if (i >= m_numrows)
                    return default(T);
                else
                    return m_heapBlock[i];
            }
#endif
        }
        public T LookupRow(uint i)
        {
#if SAFE_DBC_CODE_RETURNS
            if (i >= m_numrows)
            {
                return m_heapBlock[0];
            }
            else
            {
                return m_heapBlock[i];
            }
#else
            if (i < m_numrows)
            {
                return m_heapBlock[i];
            }
            else
            {
                return default(T);
            }
#endif
        }
    }   
};

