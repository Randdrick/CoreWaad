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
using System.IO;
using System.Runtime.InteropServices;

namespace WaadShared.Database.DBCStructure;

public class DBC : IDisposable
{
    private uint[] tbl;
    private char[] db;
    private bool loaded;
    private string name;
    private uint rows;
    private uint cols;
    private uint weird2; // Weird2 = most probably line length
    private uint dblength;
    private DBCFmat[] format;

    public DBC()
    {
        cols = 0;
        dblength = 0;
        db = null;
        loaded = false;
        name = string.Empty;
        rows = 0;
        tbl = null;
        weird2 = 0;
        format = null;
    }

    public void Load(string filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            Log.Error("DBC", "Filename is null or empty!");
            return;
        }

        if (!File.Exists(filename))
        {
            Log.Error("DBC", $"DBC {filename} doesn't exist!");
            return;
        }

        try
        {
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                br.BaseStream.Seek(4, SeekOrigin.Begin);
                rows = br.ReadUInt32();
                cols = br.ReadUInt32();
                weird2 = br.ReadUInt32();
                dblength = br.ReadUInt32();

                tbl = new uint[rows * cols];
                db = new char[dblength];
                format = new DBCFmat[cols];
                name = filename;

                for (int i = 0; i < tbl.Length; i++)
                {
                    tbl[i] = br.ReadUInt32();
                }

                for (int i = 0; i < db.Length; i++)
                {
                    db[i] = br.ReadChar();
                }
            }

            loaded = true;
            Log.Notice("DBC", $"Loaded {name} ({rows} rows)");
        }
        catch (Exception ex)
        {
            Log.Error("DBC", $"Error loading DBC file: {ex.Message}");
        }
    }

    public void Lookup(out string result, int row, int col, bool isStr = false, bool onlyStr = false)
    {
        result = string.Empty;

        if (row < 0 || row >= rows || col < 0 || col >= cols)
        {
            Log.Error("DBC", "Row or column index out of bounds!");
            return;
        }

        int fst = (int)tbl[row * cols + col];
        string str = new(db, fst, db.Length - fst);

        if ((fst > 0 && fst < dblength && col > 0 && !onlyStr) || isStr)
        {
            char bla = db[fst - 1];
            if (bla == '\0' && fst != 1)
            {
                result = str;
                return;
            }
        }

        result = tbl[row * cols + col].ToString();
    }

    public void CSV(string filename, bool info)
    {
        if (string.IsNullOrEmpty(filename))
        {
            Log.Error("DBC", "Filename is null or empty!");
            return;
        }

        if (weird2 != cols * 4)
        {
            filename += "-NOT.csv";
        }

        try
        {
            using var fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using var sw = new StreamWriter(fs);
            TextWriter outWriter = info ? sw : Console.Out;

            outWriter.WriteLine($"Rows:{rows}");
            outWriter.WriteLine($"Cols:{cols}");
            outWriter.WriteLine($"Weird:{weird2}");
            outWriter.WriteLine($"Theory:{(weird2 == cols * 4)}");
            outWriter.WriteLine($"DBlength:{dblength}");
            outWriter.WriteLine();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    Lookup(out string str, i, j);
                    sw.Write($"{str},");
                }
                sw.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Log.Error("DBC", $"Error writing CSV file: {ex.Message}");
        }
    }

    public void FormatCSV(string filename, bool info)
    {
        if (string.IsNullOrEmpty(filename))
        {
            Log.Error("DBC", "Filename is null or empty!");
            return;
        }

        if (weird2 != cols * 4)
        {
            filename += "-NOT.csv";
        }

        try
        {
            using var fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using var sw = new StreamWriter(fs);
            TextWriter outWriter = info ? sw : Console.Out;

            outWriter.WriteLine($"Rows:{rows}");
            outWriter.WriteLine($"Cols:{cols}");
            outWriter.WriteLine($"Weird:{weird2}");
            outWriter.WriteLine($"Theory:{(weird2 == cols * 4)}");
            outWriter.WriteLine($"DBlength:{dblength}");
            outWriter.WriteLine();

            Console.WriteLine($"Writing file ({name}): 0%");
            int percent = 0, npercent;
            int fst;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    fst = (int)tbl[i * cols + j];
                    if (format[j] == DBCFmat.F_STRING)
                        sw.Write($"\"{new string(db, fst, db.Length - fst)}\",");
                    else if (format[j] == DBCFmat.F_FLOAT)
                        sw.Write($"{BitConverter.ToSingle(BitConverter.GetBytes(fst), 0)},");
                    else
                        sw.Write($"{fst},");

                    npercent = (int)((float)(i * cols + j) / (rows * cols) * 100);
                    if (npercent > percent)
                    {
                        Console.Write($"\rWriting file ({name}): {npercent}%");
                        percent = npercent;
                    }
                }
                sw.WriteLine();
            }
            Console.WriteLine($"\rWriting file ({name}): 100% - Done!");
        }
        catch (Exception ex)
        {
            Log.Error("DBC", $"Error writing formatted CSV file: {ex.Message}");
        }
    }

    public void GuessFormat()
    {
        int[] ints = new int[cols];
        int[] floats = new int[cols];
        int[] strings = new int[cols];
        Console.WriteLine($"Guessing format ({name}): 0%");
        int percent = 0, npercent;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                DBCFmat f = GuessFormat(i, j);
                if (f == DBCFmat.F_STRING) strings[j]++;
                else if (f == DBCFmat.F_INT) ints[j]++;
                else if (f == DBCFmat.F_FLOAT) floats[j]++;
                npercent = (int)((float)(i * cols + j) / (rows * cols + cols) * 100);
                if (npercent > percent)
                {
                    Console.Write($"\rGuessing format ({name}): {npercent}%");
                    percent = npercent;
                }
            }
        }

        for (int j = 0; j < cols; j++)
        {
            if (strings[j] > ints[j])
            {
                if (strings[j] > floats[j])
                    format[j] = DBCFmat.F_STRING;
                else
                    format[j] = DBCFmat.F_FLOAT;
            }
            else
            {
                if (ints[j] > floats[j])
                    format[j] = DBCFmat.F_INT;
                else
                    format[j] = DBCFmat.F_FLOAT;
            }
            npercent = (int)((float)(rows * cols + j) / (rows * cols + cols) * 100);
            if (npercent > percent)
            {
                Console.Write($"\r{npercent}%");
                percent = npercent;
            }
        }
        Console.WriteLine($"\rGuessing format ({name}): 100% - Done!");
    }

    private DBCFmat GuessFormat(int row, int col)
    {
        if (row < 0 || row >= rows || col < 0 || col >= cols)
        {
            Log.Error("DBC", "Row or column index out of bounds!");
            return DBCFmat.F_NADA;
        }

        uint fst = tbl[row * cols + col];
        if (fst == 0) return DBCFmat.F_NADA;
        else if (fst == 1) return DBCFmat.F_INT;
        if (fst > 0 && fst < dblength && col > 0 && db[fst - 1] == '\0') return DBCFmat.F_STRING;
        if (fst > 100000000) return DBCFmat.F_FLOAT;
        return DBCFmat.F_INT;
    }

    public void LookupFormat(out string result, int row, int col)
    {
        result = string.Empty;

        if (row < 0 || row >= rows || col < 0 || col >= cols)
        {
            Log.Error("DBC", "Row or column index out of bounds!");
            return;
        }

        int fst = (int)tbl[row * cols + col];
        if (format[col] == DBCFmat.F_STRING)
            result = new string(db, fst, db.Length - fst);
        else if (format[col] == DBCFmat.F_FLOAT)
            result = BitConverter.ToSingle(BitConverter.GetBytes(fst), 0).ToString();
        else
            result = fst.ToString();
    }

    public void RowToStruct<T>(out T result, int row) where T : struct
    {
        result = default;

        if (row < 0 || row >= rows)
        {
            Log.Error("DBC", "Row index out of bounds!");
            return;
        }

        int size = Marshal.SizeOf(typeof(T));
        IntPtr ptr = Marshal.AllocHGlobal(size);

        try
        {
            byte[] buffer = new byte[size];
            Buffer.BlockCopy(tbl, row * (int)cols * sizeof(uint), buffer, 0, size);
            Marshal.Copy(buffer, 0, ptr, size);
            result = Marshal.PtrToStructure<T>(ptr);
        }
        catch (Exception ex)
        {
            Log.Error("DBC", $"Error converting row to struct: {ex.Message}");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public bool IsLoaded() => loaded;

    public IntPtr GetRow(uint index)
    {
        if (index >= rows)
        {
            Log.Error("DBC", "Row index out of bounds!");
            return IntPtr.Zero;
        }

        int size = Marshal.SizeOf(typeof(uint)) * (int)cols;
        IntPtr ptr = Marshal.AllocHGlobal(size);

        try
        {
            // Convert uint[] to byte[]
            byte[] byteArray = new byte[tbl.Length * sizeof(uint)];
            Buffer.BlockCopy(tbl, 0, byteArray, 0, byteArray.Length);

            // Copy the relevant portion of the byte array to the unmanaged memory
            Marshal.Copy(byteArray, (int)(index * cols * sizeof(uint)), ptr, size);
        }
        catch (Exception ex)
        {
            Log.Error("DBC", $"Error copying row data: {ex.Message}");
            Marshal.FreeHGlobal(ptr);
            return IntPtr.Zero;
        }

        return ptr;
    }
    public string LookupString(uint offset)
    {
        if (offset >= dblength)
        {
            Log.Error("DBC", "Offset out of bounds!");
            return string.Empty;
        }

        return new string(db, (int)offset, db.Length - (int)offset);
    }

    public int GetRows() => (int)rows;
    public int GetCols() => (int)cols;
    public int GetDBSize() => (int)dblength;

    public void Dispose()
    {
        tbl = null;
        db = null;
        format = null;
    }
}

public enum DBCFmat
{
    F_STRING = 0,
    F_INT = 1,
    F_FLOAT = 2,
    F_NADA = 3
}

public static class Log
{
    public static void Notice(string source, string message)
    {
        Console.WriteLine($"[{source}] {message}");
    }

    public static void Error(string source, string message)
    {
        Console.WriteLine($"[{source}] ERROR: {message}");
    }
}