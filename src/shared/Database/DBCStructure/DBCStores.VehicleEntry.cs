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
    public struct VehicleEntry
    {
        public uint m_ID;                     // 1
        public uint m_flags;                  // 2
        public float m_turnSpeed;             // 3
        public float m_pitchSpeed;            // 4
        public float m_pitchMin;              // 5
        public float m_pitchMax;              // 6
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] m_seatID;               // 7-14
        public float m_mouseLookOffsetPitch;  // 15
        public float m_cameraFadeDistScalarMin; // 16
        public float m_cameraFadeDistScalarMax; // 17
        public float m_cameraPitchOffset;     // 18
        public float m_facingLimitRight;      // 19
        public float m_facingLimitLeft;       // 20
        public float m_msslTrgtTurnLingering; // 21
        public float m_msslTrgtPitchLingering; // 22
        public float m_msslTrgtMouseLingering; // 23
        public float m_msslTrgtEndOpacity;    // 24
        public float m_msslTrgtArcSpeed;      // 25
        public float m_msslTrgtArcRepeat;     // 26
        public float m_msslTrgtArcWidth;      // 27
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public float[] m_msslTrgtImpactRadius; // 28-29
        public string m_msslTrgtArcTexture;   // 30
        public string m_msslTrgtImpactTexture; // 31
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public string[] m_msslTrgtImpactModel; // 32-33
        public float m_cameraYawOffset;       // 34
        public uint m_uiLocomotionType;       // 35
        public float m_msslTrgtImpactTexRadius; // 36
        public uint m_uiSeatIndicatorType;    // 37
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] m_powerType;             // 38-40
    }
}