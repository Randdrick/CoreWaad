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
 */

using System;
using System.Collections.Generic;

namespace WaadShared;

/// <summary>
/// Classe utilitaire pour détecter les motifs binaires malveillants dans les données.
/// </summary>
public static class MaliciousPatternDetector
{
    /// <summary>
    /// Liste des motifs binaires suspects (en hexadécimal).
    /// </summary>
    private static readonly byte[][] MaliciousPatterns =
    [
        // Shellcodes
        [0x31, 0xC0], // xor eax, eax
        [0x31, 0xDB], // xor ebx, ebx
        [0x90, 0x90, 0x90], // NOP slide
        [0xCC], // int3
        [0xEB, 0x1A, 0x5E, 0x31, 0xC9], // Shellcode classique
        [0xCD, 0x80], // int 0x80 (appel système Linux/x86)
        [0x0F, 0x05], // syscall (appel système Linux/x86_64)
        [0xFF, 0xD0], // call eax
        [0xFF, 0xE4], // jmp esp

        // En-têtes de fichiers exécutables
        [0x4D, 0x5A], // MZ (PE)
        [0x7F, 0x45, 0x4C, 0x46], // ELF
        [0xCA, 0xFE, 0xBA, 0xBE], // Mach-O

        // Désérialisation
        [0xAC, 0xED, 0x00, 0x05], // Java serialized object
        [0x80, 0x03], // PHP serialized
        [0x00, 0x00, 0x00, 0x01], // .NET serialized

        // Buffer overflow
        [0x41, 0x41, 0x41, 0x41], // AAAA
        [0x90, 0x90, 0x90, 0x90], // NOP slide
        [0x00, 0x00, 0x00, 0x00], // Zeros
        [0xFF, 0xFF, 0xFF, 0xFF], // 0xFFFFFFFF

        // Injections SQL/NoSQL (en ASCII)
        [0x27, 0x20, 0x4F, 0x52, 0x20], // ' OR
        [0x3B, 0x20, 0x44, 0x52, 0x4F, 0x50], // ; DROP

        // XSS (en ASCII)
        [0x3C, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x3E], // <script>
        [0x6F, 0x6E, 0x65, 0x72, 0x72, 0x6F, 0x72, 0x3D], // onerror=

        // Inclusion de fichiers (en ASCII)
        [0x2E, 0x2E, 0x2F], // ../
        [0x66, 0x69, 0x6C, 0x65, 0x3A, 0x2F, 0x2F], // file://
    ];

    /// <summary>
    /// Vérifie si un tableau d'octets contient des motifs binaires malveillants.
    /// </summary>
    /// <param name="data">Tableau d'octets à analyser.</param>
    /// <returns>True si des motifs malveillants sont détectés, sinon False.</returns>
    public static bool ContainsMaliciousPatterns(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return false;
        }

        foreach (var pattern in MaliciousPatterns)
        {
            if (ContainsSequence(data, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Vérifie si un tableau d'octets contient une séquence spécifique.
    /// </summary>
    /// <param name="data">Tableau d'octets à analyser.</param>
    /// <param name="sequence">Séquence à rechercher.</param>
    /// <returns>True si la séquence est trouvée, sinon False.</returns>
    public static bool ContainsSequence(byte[] data, byte[] sequence)
    {
        if (data == null || sequence == null || sequence.Length == 0 || data.Length < sequence.Length)
        {
            return false;
        }

        for (int i = 0; i <= data.Length - sequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++)
            {
                if (data[i + j] != sequence[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ajoute un motif binaire à la liste des motifs malveillants.
    /// </summary>
    /// <param name="pattern">Motif à ajouter.</param>
    public static void AddMaliciousPattern(byte[] pattern)
    {
        if (pattern == null || pattern.Length == 0)
        {
            return;
        }

        // Créer une nouvelle liste avec le nouveau motif
        var newPatterns = new List<byte[]>(MaliciousPatterns) { pattern };
        // Mettre à jour la liste statique (note : cela ne fonctionne que si la liste est réassignée)
        // En pratique, il faudrait utiliser une liste mutable ou une approche différente pour les mises à jour dynamiques.
        // Ici, on se contente de documenter la méthode pour une utilisation future.
        throw new NotImplementedException("La modification dynamique de MaliciousPatterns n'est pas implémentée. Utilisez une liste mutable si nécessaire.");
    }
}
