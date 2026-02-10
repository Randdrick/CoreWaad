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

using System;
using System.IO;
using System.Reflection;

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

        public DBCStorage()
        {
            m_heapBlock = null;
            m_entries = null;
            m_firstEntry = default;
            m_max = 0;
            m_numrows = 0;
            m_stringlength = 0;
            m_stringData = null;
        }

        public bool Load(string filename, string format, bool loadIndexed, bool loadStrings)
        {
            uint rows;
            uint cols;
            uint uselessShit;
            uint stringLength;
            uint header;
            long pos;

            m_entries = null;

            try
            {
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
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

                    for (uint i = 0; i < rows; ++i)
                    {
                        m_heapBlock[i] = new T();
                        ReadEntry(reader, ref m_heapBlock[i], format, cols, filename);

                        if (loadIndexed)
                        {
                            uint entry = ConvertEntryToId(m_heapBlock[i]);
                            if (entry > m_max)
                                m_max = entry;
                        }
                    }

                    if (loadIndexed)
                    {
                        m_entries = new T[m_max + 1];
                        for (uint i = 0; i < rows; ++i)
                        {
                            m_firstEntry = m_heapBlock[i];
                            uint entry = ConvertEntryToId(m_heapBlock[i]);
                            m_entries[entry] = m_heapBlock[i];
                        }
                    }

                    m_numrows = rows;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading DBC file {filename}: {ex.Message}");
                return false;
            }

            return true;
        }

        private static uint ConvertEntryToId(T entry)
        {
            // Utiliser la réflexion pour trouver le champ "Id" ou similaire
            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                    field.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                    field.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                {
                    return (uint)field.GetValue(entry);
                }
            }

            // Si aucun champ "Id" n'est trouvé, utiliser le premier champ uint
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(uint))
                {
                    return (uint)field.GetValue(entry);
                }
            }

            return 0;
        }

        public void ReadEntry(BinaryReader reader, ref T dest, string format, uint cols, string filename)
        {
            char[] formatChars = format.ToCharArray();
            uint c = 0;
            int index = 0;

            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            int fieldIndex = 0;

            while (index < formatChars.Length && formatChars[index] != 0 && fieldIndex < fields.Length)
            {
                if ((++c) > cols)
                {
                    index++;
                    Console.WriteLine($"!!! Read buffer overflow in DBC reading of file {filename}");
                    continue;
                }

                uint val = reader.ReadUInt32();

#if USING_BIG_ENDIAN
                val = Swap32(val);
#endif

                if (formatChars[index] == 'x')
                {
                    index++;
                    continue;
                }
                else if (formatChars[index] == 's')
                {
                    if (fields[fieldIndex].FieldType == typeof(string))
                    {
                        string str = (val < m_stringlength) ?
                            new string(m_stringData.ToCharArray(), (int)val, m_stringData.Length - (int)val) :
                            string.Empty;
                        fields[fieldIndex].SetValue(dest, str);
                    }
                }
                else if (formatChars[index] == 'f')
                {
                    if (fields[fieldIndex].FieldType == typeof(float))
                    {
                        float floatVal = BitConverter.ToSingle(BitConverter.GetBytes(val), 0);
                        fields[fieldIndex].SetValue(dest, floatVal);
                    }
                }
                else // 'u' ou autre type numérique
                {
                    if (fields[fieldIndex].FieldType == typeof(uint))
                    {
                        fields[fieldIndex].SetValue(dest, val);
                    }
                    else if (fields[fieldIndex].FieldType == typeof(int))
                    {
                        fields[fieldIndex].SetValue(dest, unchecked((int)val));
                    }
                }

                index++;
                fieldIndex++;
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
                    return default;
                else
                    return m_entries[i];
            }
            else
            {
                if (i >= m_numrows)
                    return default;
                else
                    return m_heapBlock[i];
            }
#endif
        }

        public T LookupEntryForced(uint i)
        {
            if (m_entries != null)
            {
                if (i > m_max || m_entries[i] == null)
                {
                    Console.WriteLine($"LookupEntryForced failed for entry {i}");
                    return default;
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
                    return default;
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

        public T LookupRow(uint i)
        {
#if SAFE_DBC_CODE_RETURNS
            if (i >= m_numrows)
                return m_heapBlock[0];
            else
                return m_heapBlock[i];
#else
            if (i < m_numrows)
                return m_heapBlock[i];
            else
                return default;
#endif
        }
    }
}
