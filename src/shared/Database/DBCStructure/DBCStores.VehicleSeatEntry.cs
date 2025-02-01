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

using System.Runtime.InteropServices;

public static partial class DBCStores
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VehicleSeatEntry
    {
        public uint m_ID;                     // 0
        public uint m_flags;                  // 1
        public int m_attachmentID;            // 2
        public float m_attachmentOffsetX;     // 3
        public float m_attachmentOffsetY;     // 4
        public float m_attachmentOffsetZ;     // 5
        public float m_enterPreDelay;         // 6
        public float m_enterSpeed;            // 7
        public float m_enterGravity;          // 8
        public float m_enterMinDuration;      // 9
        public float m_enterMaxDuration;      // 10
        public float m_enterMinArcHeight;     // 11
        public float m_enterMaxArcHeight;     // 12
        public int m_enterAnimStart;          // 13
        public int m_enterAnimLoop;           // 14
        public int m_rideAnimStart;           // 15
        public int m_rideAnimLoop;            // 16
        public int m_rideUpperAnimStart;      // 17
        public int m_rideUpperAnimLoop;       // 18
        public float m_exitPreDelay;          // 19
        public float m_exitSpeed;             // 20
        public float m_exitGravity;           // 21
        public float m_exitMinDuration;       // 22
        public float m_exitMaxDuration;       // 23
        public float m_exitMinArcHeight;      // 24
        public float m_exitMaxArcHeight;      // 25
        public int m_exitAnimStart;           // 26
        public int m_exitAnimLoop;            // 27
        public int m_exitAnimEnd;             // 28
        public float m_passengerYaw;          // 29
        public float m_passengerPitch;        // 30
        public float m_passengerRoll;         // 31
        public int m_passengerAttachmentID;   // 32
        public int m_vehicleEnterAnim;        // 33
        public int m_vehicleExitAnim;         // 34
        public int m_vehicleRideAnimLoop;     // 35
        public int m_vehicleEnterAnimBone;    // 36
        public int m_vehicleExitAnimBone;     // 37
        public int m_vehicleRideAnimLoopBone; // 38
        public float m_vehicleEnterAnimDelay; // 39
        public float m_vehicleExitAnimDelay;  // 40
        public uint m_vehicleAbilityDisplay;  // 41
        public uint m_enterUISoundID;         // 42
        public uint m_exitUISoundID;          // 43
        public int m_uiSkin;                  // 44
        public uint m_flagsB;                 // 45

        public bool IsUsable()
        {
            return (m_flags & 0x2000000) != 0;
        }
    }
}