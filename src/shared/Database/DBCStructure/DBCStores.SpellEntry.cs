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
    public struct SpellEntry // 335.12340 Fr
    {
        public uint Id;                              // 1
        public uint Category;                        // 2
        public uint DispelType;                      // 3
        public uint Mechanic;                        // 4
        public uint Attributes;                      // 5    
        public uint AttributesEx;                    // 6
        public uint AttributesExB;                   // 7
        public uint AttributesExC;                   // 8   // Flags to
        public uint AttributesExD;                   // 9   // Flags....
        public uint AttributesExE;                   // 10
        public uint AttributesExF;                   // 11
        public uint AttributesExG;                   // 12
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] ShapeShiftMask;                // 13 - 14 // Flags BitMask for shapeshift spells, ShapeShiftMask[1] tjs = '0'
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] ShapeshiftExclude;             // 15 - 16 // Flags BitMask for which shapeshift forms this spell can NOT be used in.    
        public uint Targets;                         // 17
        public uint TargetCreatureType;              // 18
        public uint RequiresSpellFocus;              // 19
        public uint FacingCasterFlags;               // 20
        public uint CasterAuraState;                 // 21    
        public uint TargetAuraState;                 // 22
        public uint ExcludeCasterAuraState;          // 23
        public uint ExcludeTargetAuraState;          // 24
        public uint CasterAuraSpell;                 // 25 // you have to have this aura to cast this spell
        public uint TargetAuraSpell;                 // 26
        public uint ExcludeCasterAuraSpell;          // 27 // you can't cast the spell if the caster has this aura
        public uint ExcludeTargetAuraSpell;          // 28 // you can't cast the spell if the target has this aura
        public uint CastingTimeIndex;                // 29
        public uint RecoveryTime;                    // 30
        public uint CategoryRecoveryTime;            // 31 // recoverytime
        public uint InterruptFlags;                  // 32
        public uint AuraInterruptFlags;              // 33
        public uint ChannelInterruptFlags;           // 34
        public uint ProcTypeMask;                    // 35
        public uint ProcChance;                      // 36
        public int ProcCharges;                      // 37 // 0 ou 1
        public uint MaxLevel;                        // 38
        public uint BaseLevel;                       // 39
        public uint SpellLevel;                      // 40
        public uint DurationIndex;                   // 41
        public int PowerType;                        // 42
        public uint ManaCost;                        // 43
        public uint ManaCostPerLevel;                // 44
        public uint ManaPerSecond;                   // 45
        public uint ManaPerSecondPerLevel;           // 46
        public uint RangeIndex;                      // 47
        public float Speed;                          // 48
        public uint ModalNextSpell;                  // 49 // no need to load this, not used at all
        public uint CumulativeAura;                  // 50
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] Totem;                         // 51 - 52
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] Reagent;                       // 53 - 60
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] ReagentCount;                  // 61 - 68
        public int EquippedItemClass;                // 69
        public uint EquippedItemSubClass;            // 70
        public uint EquippedItemInvTypes;            // 71
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] Effect;                        // 72 - 74 // -> _SPELL_EFFECT_DBC_COL1_ = (col-1)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectDieSides;                // 75 - 77
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] EffectRealPointsPerLevel;     // 78 - 80
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] EffectBasePoints;               // 81 - 83
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] EffectMechanic;                 // 84 - 86 // Related to SpellMechanic.dbc
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] ImplicitTargetA;               // 87 - 89
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] ImplicitTargetB;               // 90 - 92
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectRadiusIndex;             // 93 - 95
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectAura;                    // 96 - 98 // -> _SPELL_EFFECTAPPLYAURA_DBC_COL1_ = (col-1)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectAuraPeriod;              // 99 - 101
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] EffectAmplitude;              // 102 - 104 // This value is the $ value from description
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectChainTargets;            // 105 - 107
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectItemType;                // 108 - 110 // Andy: This isn't the relation field, its the item type!
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectMiscValue;               // 111 - 113    
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectMiscValueB;              // 114 - 116
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectTriggerSpell;            // 117 - 119
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectPointsPerCombo;          // 120 - 122 // c'est un uint32, verifié ok 335.12340 (Branruz)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectSpellClassMaskA;         // 123 - 125
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectSpellClassMaskB;         // 126 - 128    
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] EffectSpellClassMaskC;         // 129 - 131
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] SpellVisualID;                 // 132 - 133
        public uint SpellIconID;                     // 134
        public uint ActiveIconID;                    // 135
        public uint SpellPriority;                   // 136
        // public string Name_lang0;                // 137
        // public string Name_lang1;                // 138
        public string Name;                          // 139 // Fr
        // public string Name_lang3;                // 140
        // public string Name_lang4;                // 141
        // public string Name_lang5;                // 142
        // public string Name_lang6;                // 143
        // public string Name_lang7;                // 144
        // public string Name_lang8;                // 145
        // public string Name_lang9;                // 146
        // public string Name_lang10;               // 147
        // public string Name_lang11;               // 148
        // public string Name_lang12;               // 149
        // public string Name_lang13;               // 150
        // public string Name_lang14;               // 151
        // public string Name_lang15;               // 152    
        // uint32 Name_lang_msk;                    // 153
        // public string NameSubtext_lang0;         // 154
        // public string NameSubtext_lang1;         // 155
        public string NameSubtext;                   // 156 // Fr
        // public string NameSubtext_lang3;         // 157
        // public string NameSubtext_lang4;         // 158
        // public string NameSubtext_lang5;         // 159
        // public string NameSubtext_lang6;         // 160
        // public string NameSubtext_lang7;         // 161
        // public string NameSubtext_lang8;         // 162
        // public string NameSubtext_lang9;         // 163
        // public string NameSubtext_lang10;        // 164
        // public string NameSubtext_lang11;        // 165
        // public string NameSubtext_lang12;        // 166
        // public string NameSubtext_lang13;        // 167
        // public string NameSubtext_lang14;        // 168
        // public string NameSubtext_lang15;        // 169
        // uint32 NameSubtext_lang_mask;            // 170
        // public string Description_lang0;         // 171
        // public string Description_lang1;         // 172
        public string Description;                   // 173 // Fr
        // public string Description_lang3;         // 174
        // public string Description_lang4;         // 175
        // public string Description_lang5;         // 176
        // public string Description_lang6;         // 177
        // public string Description_lang7;         // 178
        // public string Description_lang8;         // 179
        // public string Description_lang9;         // 180
        // public string Description_lang10;        // 181
        // public string Description_lang11;        // 182
        // public string Description_lang12;        // 183
        // public string Description_lang13;        // 184
        // public string Description_lang14;        // 185
        // public string Description_lang15;        // 186
        // uint32 Description_lang_mask;            // 187
        // public string AuraDescription_lang0;     // 188
        // public string AuraDescription_lang1;     // 189
        public string AuraDescription;               // 190 // Fr
        // public string AuraDescription_lang3;     // 191
        // public string AuraDescription_lang4;     // 192
        // public string AuraDescription_lang5;     // 193
        // public string AuraDescription_lang6;     // 194
        // public string AuraDescription_lang7;     // 195
        // public string AuraDescription_lang8;     // 196    
        // public string AuraDescription_lang9;     // 197
        // public string AuraDescription_lang10;    // 198
        // public string AuraDescription_lang11;    // 199
        // public string AuraDescription_lang12;    // 200
        // public string AuraDescription_lang13;    // 201
        // public string AuraDescription_lang14;    // 202
        // public string AuraDescription_lang15;    // 203
        // uint32 AuraDescription_lang_mask;        // 204
        public uint ManaCostPercentage;              // 205
        public uint StartRecoveryCategory;           // 206
        public uint StartRecoveryTime;               // 207
        public uint MaxTargetLevel;                  // 208
        public uint SpellClassSet;                   // 209
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] SpellClassMask;                // 210 - 212 // en fait il s'agirait de 2 uint16 (genre HighSpellGroupType et LowSpellGroupType) (Branruz) 332.11403
        public uint MaxTargets;                      // 213
        public uint DefenseType;                     // 214 // dmg_class Integer 0=None, 1=Magic, 2=Melee, 3=Ranged
        public uint PreventionType;                  // 215 // 0,1,2 related to defenseType I think
        public int StanceBarOrder;                   // 216 // related to paladin aura's 
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] EffectChainAmplitude;         // 217 - 219
        public uint MinFactionId;                    // 220 // only one spellid:6994 has this value = 369 UNUSED
        public uint MinReputation;                   // 221 // only one spellid:6994 has this value = 4 UNUSED
        public uint RequiredAuraVision;              // 222 // 3 spells 1 or 2 
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] RequiredTotemCategoryId;       // 223 - 224
        public int RequiredAreasId;                  // 225
        public uint SchoolMask;                      // 226
        public uint RuneCostId;                      // 227
        public uint SpellMissileId;                  // 228
        public uint PowerDisplayId;                  // 229
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] EffectBonusCoefficient;       // 230 - 232
        public uint DescriptionVariablesId;          // 233
        public uint Difficulty;                      // 234
        // Fin spell.dbc
        //---------------
        // CUSTOM: these fields are used for the modifications made in the world.cpp
        //---------------
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public int[] EffectBasePointCalculate;       // CUSTOM, EffectBasePoint recalculé avec la formule (Branruz)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ushort[] ExtendedSpellGroupType;      // CUSTOM, Extended SpellGroupType (Branruz)
        public ushort UniqueGroup;                   // Aura: unique group, Remplacement de l'aura
        public byte UniqueTargetBased;               // Aura: flags, 1=target based, 2=only one aura can be alive from the caster at any time
        public byte UniqueGroup2;                    // Aura: this is used for paladin blessings, flask/elixirs
        public uint School;                          // this is for the school fixes, keep mask for talents and procs, etc
        public uint DiminishStatus;                  //
        public uint proc_interval;                   //!!! CUSTOM, <Fill description for variable>
        public float ProcsPerMinute;                 //!!! CUSTOM, number of procs per minute
        public uint c_is_flags;                      //!!! CUSTOM, store spell checks in a static way : isdamageind,ishealing
        public uint buffIndexType;                   //!!! CUSTOM, <Fill description for variable>
        public uint buffType;                        //!!! CUSTOM, these are related to creating a item through a spell
        public uint RankNumber;                      //!!! CUSTOM, this protects players from having >1 rank of a spell
        public uint NameHash;                        //!!! CUSTOM, related to custom spells, summon spell quest related spells
        public float base_range_or_radius_sqr;       //!!! CUSTOM, needed for aoe spells most of the time
        public uint talent_tree;                     //!!! CUSTOM,
        public byte in_front_status;                 //!!! CUSTOM,
        public bool is_melee_spell;                  //!!! CUSTOM,
        public bool is_ranged_spell;                 //!!! CUSTOM,
        public bool spell_can_crit;                  //!!! CUSTOM,
        // uint32 EffectSpellGroupRelation_high[3];  //!!! this is not contained in client dbc but server must have it
        public uint ThreatForSpell;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] ProcOnNameHash;

        // love me or hate me, all "In a cone in front of the caster" spells don't necessarily mean "in front"
        public float cone_width;
        
        // Spell Coefficient
        public float casttime_coef;                  //!!! CUSTOM, faster spell bonus calculation
        public byte spell_coef_flags;                //!!! CUSTOM, store flags for spell coefficient calculations
        public float fixed_dddhcoef;                 //!!! CUSTOM, fixed DD-DH coefficient for some spells
        public float fixed_hotdotcoef;               //!!! CUSTOM, fixed HOT-DOT coefficient for some spells
        public float Dspell_coef_override;           //!!! CUSTOM, overrides any spell coefficient calculation and use this value in DD&DH
        public float OTspell_coef_override;          //!!! CUSTOM, overrides any spell coefficient calculation and use this value in HOT&DOT
        
        public bool self_cast_only;
        public bool apply_on_shapeshift_change;
        public bool always_apply;
        
        public bool Unique;
        public uint skilline;

        public uint logsId;                          // SpellId used to send log to client for this spell
        public uint AdditionalAura;
        public uint forced_creature_target;
        public uint AreaAuraTarget;
        public uint poison_type;                     // poisons type...
        public uint spell_signature;                 // Fix me "les memory-leak" (Branruz)
    }
}