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
using System.Threading;

namespace WaadShared;

// Enumération pour les types d'arguments
public enum AscentArgument
{
    NoArgument = 0,
    RequiredArgument = 1,
    OptionalArgument = 2
}

// Structure pour représenter une option
public struct AscentOption
{
    public string Name;
    public int HasArg;
    public int? Flag;
    public int Val;
}

// Classe statique pour les fonctions d'AscentGetOpt
public static class AscentGetOpt
{
    private static int argCounter = 1;
    private static string ascentOptarg = new(' ', 514);

    // Méthode pour analyser les options longues
    public static int AscentGetOptLongOnly(string[] args, string shortOpts, AscentOption[] longOpts, ref int longIndex)
    {
        if (args.Length == 1 || argCounter == args.Length)
            return -1;

        string opt = args[argCounter];

        if (!opt.StartsWith("--"))
            return 1;
        else
            opt = opt[2..];

        for (int i = 0; i < longOpts.Length; i++)
        {
            if (longOpts[i].Name == null)
                break;

            if (opt.StartsWith(longOpts[i].Name))
            {
                string par = null;
                if ((argCounter + 1) != args.Length)
                {
                    if (!args[argCounter + 1].StartsWith("--"))
                    {
                        argCounter++;
                        par = args[argCounter];
                    }
                }

                argCounter++;

                if (longOpts[i].HasArg == (int)AscentArgument.RequiredArgument)
                {
                    if (par == null)
                        return 1;

                    if (longOpts[i].Flag != null)
                    {
                        longOpts[i].Flag = int.Parse(par);
                        return 0;
                    }
                }

                if (par != null)
                    ascentOptarg = par.PadRight(514).Substring(0, 514);

                if (longOpts[i].Flag != null)
                {
                    longOpts[i].Flag = 1;
                    return 0;
                }
                else
                {
                    if (longOpts[i].Val == -1 || par == null)
                        return 1;

                    return longOpts[i].Val;
                }
            }
        }

        return 1;
    }

    // Méthode pour remplacer des sous-chaînes dans une chaîne
    public static void Replace(ref string str, string find, string rep, uint limit = 0)
    {
        uint i = 0;
        int pos = 0;
        while ((pos = str.IndexOf(find, pos)) != -1)
        {
            str = str.Remove(pos, find.Length).Insert(pos, rep);
            pos += rep.Length;

            i++;
            if (limit != 0 && i == limit)
                break;
        }
    }

    // Méthodes pour des opérations atomiques
    public static long SyncAdd(ref long value)
    {
        return Interlocked.Increment(ref value);
    }

    public static long SyncSub(ref long value)
    {
        return Interlocked.Decrement(ref value);
    }

    // Méthodes pour générer des valeurs aléatoires
    public static bool Rand(float chance)
    {
        Random rand = new();
        int val = rand.Next(10000);
        int p = (int)(chance * 100.0f);
        return p >= val;
    }

    public static bool Rand(uint chance)
    {
        Random rand = new();
        int val = rand.Next(10000);
        int p = (int)(chance * 100);
        return p >= val;
    }

    public static bool Rand(int chance)
    {
        Random rand = new();
        int val = rand.Next(10000);
        int p = chance * 100;
        return p >= val;
    }

    // Méthode pour vérifier la validité d'un nom
    public static bool VerifyName(string name, bool limitNames = true)
    {
        const string bannedCharacters = "\t\v\b\f\a\n\r\\\"\'? <>[](){}_=+-|/!@#$%^&*~`.,0123456789";
        const string allowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        if (limitNames)
        {
            foreach (char c in name)
            {
                if (!allowedCharacters.Contains(c))
                    return false;
            }
        }
        else
        {
            foreach (char c in name)
            {
                if (bannedCharacters.Contains(c))
                    return false;
            }
        }

        return true;
    }

    // Méthodes pour obtenir des valeurs de temps
    public static long GetTimerValue()
    {
        return Stopwatch.GetTimestamp();
    }

    public static uint GetNanoSeconds(long t1, long t2)
    {
        double val = (t1 - t2) * 1000000;
        val /= Stopwatch.Frequency;
        return (uint)val;
    }
}
