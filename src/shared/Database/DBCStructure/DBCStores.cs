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
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

public static partial class DBCStores
{
    public static DBCStorage<AchievementEntry> dbcAchievement;
    public static DBCStorage<AchievementCategoryEntry> dbcAchievementCategory;
    public static DBCStorage<AchievementCriteriaEntry> dbcAchivementCriteria;
    public static DBCStorage<AreaGroup> dbcAreaGroup;
    public static DBCStorage<AreaTable> dbcArea;
    public static DBCStorage<AreaTableEntry> dbcWMOAreaTable; // Arcemu
    public static DBCStorage<AreaTriggerEntry> dbcAreaTrigger;
    public static DBCStorage<AuctionHouseDBC> dbcAuctionHouse;
    public static DBCStorage<BankSlotPrice> dbcBankSlotPrices;
    public static DBCStorage<BarberShopStyleEntry> dbcBarberShopStyle;
    public static DBCStorage<gtFloat> dbcBarberShopPrices;
    public static DBCStorage<CharClassEntry> dbcCharClass;
    public static DBCStorage<CharRaceEntry> dbcCharRace;
    public static DBCStorage<CharTitlesEntry> dbcCharTitlesEntry;
    public static DBCStorage<ChatChannelDBC> dbcChatChannels;
    public static DBCStorage<CombatRatingDBC> dbcCombatRating;
    public static DBCStorage<CreatureDisplayInfo> dbcCreatureDisplayInfo;
    public static DBCStorage<CreatureFamilyEntry> dbcCreatureFamily;
    public static DBCStorage<CreatureSpellDataEntry> dbcCreatureSpellData;
    public static DBCStorage<CurrencyTypesEntry> dbcCurrencyTypes;
    public static DBCStorage<DataStore> dbcDataStore;
    public static DBCStorage<DestructibleModelDataEntry> dbcDestructibleModelDataEntry;
    public static DBCStorage<DurabilityCostsEntry> dbcDurabilityCosts;
    public static DBCStorage<DurabilityQualityEntry> dbcDurabilityQuality;
    public static DBCStorage<EmoteTextEntry> dbcEmoteEntry;
    public static DBCStorage<EnchantEntry> dbcEnchant;
    public static DBCStorage<FactionDBC> dbcFaction;
    public static DBCStorage<FactionTemplateDBC> dbcFactionTemplate;
    public static DBCStorage<GemPropertyEntry> dbcGemProperty;
    public static DBCStorage<GlyphPropertyEntry> dbcGlyphProperty;
    public static DBCStorage<ItemExtendedCostEntry> dbcItemExtendedCost;
    public static DBCStorage<ItemRandomSuffixEntry> dbcItemRandomSuffix;
    public static DBCStorage<ItemSetEntry> dbcItemSet;
    public static DBCStorage<LockEntry> dbcLock;
    public static DBCStorage<LookingForGroup> dbcLookingForGroup;
    public static DBCStorage<MapEntry> dbcMap;
    public static DBCStorage<gtFloat> dbcManaRegen;
    public static DBCStorage<gtFloat> dbcManaRegenBase;
    public static DBCStorage<gtFloat> dbcMeleeCrit;
    public static DBCStorage<gtFloat> dbcMeleeCritBase;
    public static DBCStorage<QuestFactionRewardEntry> dbcQuestFactionReward;
    public static DBCStorage<QuestXPLevel> dbcQuestXPLevel;
    public static DBCStorage<RandomProps> dbcRandomProps;
    public static DBCStorage<RuneCostEntry> dbcSpellRuneCost;
    public static DBCStorage<ScalingStatDistributionEntry> dbcScalingStatDistribution;
    public static DBCStorage<ScalingStatValuesEntry> dbcScalingStatValues;
    public static DBCStorage<SpellCastTime> dbcSpellCastTime;
    public static DBCStorage<SpellDuration> dbcSpellDuration;
    public static DBCStorage<SpellEntry> dbcSpell;
    public static DBCStorage<SpellRadius> dbcSpellRadius;
    public static DBCStorage<SpellRange> dbcSpellRange;
    public static DBCStorage<SpellShapeshiftForm> dbcSpellShapeshiftForm;
    public static DBCStorage<gtFloat> dbcSpellCrit;
    public static DBCStorage<gtFloat> dbcSpellCritBase;
    public static DBCStorage<gtFloat> dbcHPRegen;
    public static DBCStorage<gtFloat> dbcHPRegenBase;
    public static DBCStorage<SkillLineEntry> dbcSkillLine;
    public static DBCStorage<SkillLineSpell> dbcSkillLineSpell;
    public static DBCStorage<BankSlotPrice> dbcStableSlotPrices;
    public static DBCStorage<SummonPropertiesEntry> dbcSummonProperties;
    public static DBCStorage<TalentEntry> dbcTalent;
    public static DBCStorage<TalentTabEntry> dbcTalentTab;
    public static DBCStorage<DBCTaxiNode> dbcTaxiNode;
    public static DBCStorage<DBCTaxiPath> dbcTaxiPath;
    public static DBCStorage<DBCTaxiPathNode> dbcTaxiPathNode;
    public static DBCStorage<VehicleEntry> dbcVehicle;
    public static DBCStorage<VehicleSeatEntry> dbcVehicleSeat;
    public static DBCStorage<WorldMapOverlayEntry> dbcWorldMapOverlay;
    public static DBCStorage<WorldSafeLocsStoreEntry> dbcWorldSafeLocsStore;

    public static Dictionary<uint, List<SpellEntry>> spellsByNameHash = new Dictionary<uint, List<SpellEntry>>();

    public const string AreaTriggerFormat = "uuffffffff";                           // 332.11403 - Idem 335.12340 Fr
    public const string AreaGroupFormat = "uuuuuuuu";                             // 332.11403 - Idem 335.12340 Fr
    public const string AreatableFormat = "uuuuuxxxuxuxxsxxxxxxxxxxxxxxuxxxxxxx"; // 335.12340 Fr
    public const string AuctionHouseDBCFormat = "uuuuxxxxxxxxxxxxxxxxx";               // 332.11403

    public const string CharTitlesEntryFmt = "uxxxsxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxu"; // 335.12340 Fr

    public const string CurrencyTypesFormat = "xuxu";

    public const string ItemSetFormat = "uxxsxxxxxxxxxxxxxuuuuuuuuuuuxxxxxxxuuuuuuuuuuuuuuuuuu"; // 335.12340 Fr
    public const string ItemExtendedCostFormat = "uuuxuuuuuuuuuuux";                                      // 322 - 332.11403 - 335.12340 Fr

    public const string LockFormat = "uuuuuuxxxuuuuuxxxuuuuuxxxxxxxxxxx"; // 335.12340 
    public const string LFGDungeonsFormat = "uxxxxxxxxxxxxxxxxxuuuuuiuxuxxuxuxxxxxxxxxxxxxxxxx"; // 335.12340 
    public const string EmoteTextEntryFormat = "uxuuuuxuxuxuuxxuxxx";     // 332.11403 - idem 335.12340 Fr

    public const string SkillLineSpellFormat = "uuuuuuuuuuuuuu";                                           // 332.11403 - idem 335.12340 Fr
    public const string SkillLineEntryFormat = "uuuxxsxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"; // 335.12340

    public const string EnchantEntryFormat = "uxuuuuuuuuuuuuxxsxxxxxxxxxxxxxxuuuuuuu"; // 335.12340 Fr

    public const string GemPropertyEntryFormat = "uuuuu"; // 335.12340
    public const string GlyphPropertyEntryFormat = "uuuu";  // 335.12340

    public const string TalentEntryFormat = "uuuuuuuuuxxxxuxxuxxuxxx";  // 332.11403 - idem 335.12340 Fr
    public const string TalentTabEntryFormat = "uxxxxxxxxxxxxxxxxxxuuxux"; // 332.11403 - idem 335.12340 Fr

    public const string SpellCastTimeFormat = "uuxx";                                     // 332 - 332.11403 - idem 335.12340 Fr
    public const string SpellRadiusFormat = "ufxf";                                     // 322 - 332.11403 - idem 335.12340 Fr
    public const string SpellRangeFormat = "uffffxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"; // 322 - 332.11403 - idem 335.12340 Fr
    public const string SpellDurationFormat = "uuuu";                                     // 322 - 332.11403 - idem 335.12340 Fr

    public const string RandomPropsFormat = "uxuuuxxxxxxxxxxxxxxxxxxx";                                  // 322 - 332.11403 - idem 335.12340 Fr
    public const string FactionTemplateDBCFormat = "uuuuuuuuuuuuuu";                                            // 335.12340
    public const string FactionDBCFormat = "uiuuuuxxxxiiiixxxxuxxxxxxsxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"; // 335.12340

    public const string DBCTaxiNodeFormat = "uufffxxsxxxxxxxxxxxxxxuu"; // 335.12340 Fr
    public const string DBCTaxiPathFormat = "uuuu";                     // 335.12340
    public const string DBCTaxiPathNodeFormat = "uuuufffuuuu";              // 335.12340 + Gestion des events sur scripts

    public const string CreatureDisplayFormat = "uxxxfxxxxxxxxxxx";             // 322 - 332.11403 - idem 335.12340 Fr
    public const string CreatureSpellDataFormat = "uuuuuuuuu";                    // 309 - 322 - 332.11403 - idem 335.12340 Fr
    public const string CreatureFamilyFormat = "ufufuuuuuxxxsxxxxxxxxxxxxxxx"; // 335.12340 Fr

    public const string CharRaceFormat = "uxxxxxxxxxxxuuxxsxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"; // 335.12340 RaceName fr
    public const string CharClassFormat = "uxuxxxsxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";          // 335.12340 Fr

    public const string MapEntryFormat = "usuxuxxsxxxxxxxxxxxxxxuxxsxxxxxxxxxxxxxxxxsxxxxxxxxxxxxxxuxuffxuxu"; // 335.12340 Fr (Brz)

    public const string VehicleEntryFormat = "uuffffuuuuuuuufffffffffffffffssssfufuuuu";  // 3.3.2.11403 (Crash), idem 335.12340 Fr (Brz)
    public const string VehicleSeatEntryFormat = "uuuffffffffffuuuuuufffffffuuufffuuuuuuuffuuuuuxxxxxxxxxxxx"; // 3.3.2.11403 (Crash), idem 335.12340 Fr (Brz)

    public const string ItemRandomSuffixFormat = "uxxxxxxxxxxxxxxxxxxuuuuuuuuux"; // 332.11403 - idem 335.12340 Fr

    public const string DurabilityQualityFormat = "uf"; // 335.12340 (Crash)
    public const string BankSlotPriceFormat = "uu"; // 335.12340 (Crash)
    public const string DurabilityCostsFormat = "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuu"; // 332.11403 - idem 335.12340 Fr
    public const string BarberShopStyleFormat = "uuxxsxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxuuu";  // 335.12340 Fr
    public const string GTFloatFormat = "f"; // 335.12340 (Crash)
    public const string SpellShapeshiftFormFormat = "uxxxxxxxxxxxxxxxxxxxxxxxxxxxuuuuuuu"; // 332.11403 - idem 335.12340 Fr
    public const string DBCSummonPropertiesFormat = "uuuuuu"; // 335.12340
    public const string DBCSpellRuneCostFormat = "uuuuu"; // 332.11403 - idem 335.12340 Fr

    public const string AchievementFmt = "uuuuxxsxxxxxxxxxxxxxxxxsxxxxxxxxxxxxxxuuuuuxxsxxxxxxxxxxxxxxuu"; // 335.12340 Fr
    public const string AchievementCriteria = "uuuuuuuuuxxsxxxxxxxxxxxxxxuuxux"; // 335.12340 Fr
    public const string AchievementCategory = "uuxxsxxxxxxxxxxxxxxu"; // 335.12340 Fr
    public const string WorldMapOverlayFmt = "uxuxxxxxxxxxxxxxx"; // 332.11403 - idem 335.12340 Fr

    public const string WorldSafeLocsStoreFormat = "uufffxxsxxxxxxxxxxxxxx"; // 335.12340 Fr
    public const string ChatChannelFormat = "uuxxxsxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"; // 332.11403 - idem 335.12340 Fr

    public const string SpellEntryFormat = "uuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuiuuuuufuuuuuuuuuuuuuuuuuuuuiuuuuuuuufffiiiiiiuuuuuuuuuuuuuuufffuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuuxxsxxxxxxxxxxxxxxxxsxxxxxxxxxxxxxxxxsxxxxxxxxxxxxxxxxsxxxxxxxxxxxxxxuuuuuuuuuuuifffuuuuuiuuuufffuu";  //  335.12340 Fr

    public const string XpQuestFormat = "uxuuuuuuuux";
    public const string QuestFactRewFormat = "uuuuuuuuuuu";
    public const string ScalingStatDistributionFormat =
        "u" + // ID
        "iiiiiiiiii" + // Stat Mod
        "uuuuuuuuuu" + // Modifier
        "u"; // Max Level

    public const string ScalingStatValuesFormat =
        "x" + // Id
        "u" + // Level
        "uuuu" + // ScalingStatD modifier
        "uuuu" + // Armor Mod
        "uuuuuu" + // DPS mod
        "u" + // Spell Power
        "uux" + // Multipliers
        "uuuu"; // Armor Type[level]

    public const string DestructibleModelDataFormat = "uxxuxxxuxxxuxxxuxxx";
    // Arcemu
    public const string WMOAreaFormat = "uiiixxxxxuuxxxxxxxxxxxxxxxxx";

    public static string DBCPath = new string(new char[1024]);
    public static string RSDBCPath = new string(new char[1024]);

    public const int INVOC_DURATION = 0;
    public const int INSTANT_DURATION = 1;
    public const int COOLDOWN_DURATION = 2;

    public static float GetScale(CreatureDisplayInfo scale) => scale.Scale;

    public static float GetRadius(SpellRadius radius) => radius.Radius;

    public static uint GetCastTime(SpellCastTime time) => time.CastTime;

    public static float GetMaxRange(SpellRange range) => range.MaxRange;

    public static float GetMinRange(SpellRange range) => range.MinRange;

    public static int GetDuration(SpellDuration dur, uint type)
    {
        switch (type)
        {
            case INVOC_DURATION:
                return dur.Duration1;
            case INSTANT_DURATION:
                return dur.Duration2;
            case COOLDOWN_DURATION:
                return dur.Duration3;
            default:
                Console.WriteLine($"GetDuration: Unknown type {type} <- Report this to devs.");
                return 2000;
        }
    }

    public static uint GetDBCscalestatMultiplier(
        ScalingStatValuesEntry ssvrow,
        uint flags)
    {
        if ((flags & 0x4001F) != 0)
        {
            if ((flags & 0x00000001) != 0)
                return ssvrow.ssdMultiplier[0];
            if ((flags & 0x00000002) != 0)
                return ssvrow.ssdMultiplier[1];
            if ((flags & 0x00000004) != 0)
                return ssvrow.ssdMultiplier[2];
            if ((flags & 0x00000008) != 0)
                return ssvrow.ssdMultiplier2;
            if ((flags & 0x00000010) != 0)
                return ssvrow.ssdMultiplier[3];
            if ((flags & 0x00040000) != 0)
                return ssvrow.ssdMultiplier3;
        }
        return 0;
    }

    public static uint GetDBCscalestatArmorMod(ScalingStatValuesEntry ssvrow, uint flags)
    {
        if ((flags & 0x00F001E0) != 0)
        {
            if ((flags & 0x00000020) != 0)
                return ssvrow.armorMod[0];
            if ((flags & 0x00000040) != 0)
                return ssvrow.armorMod[1];
            if ((flags & 0x00000080) != 0)
                return ssvrow.armorMod[2];
            if ((flags & 0x00000100) != 0)
                return ssvrow.armorMod[3];

            if ((flags & 0x00100000) != 0)
                return ssvrow.armorMod2[0];
            if ((flags & 0x00200000) != 0)
                return ssvrow.armorMod2[1];
            if ((flags & 0x00400000) != 0)
                return ssvrow.armorMod2[2];
            if ((flags & 0x00800000) != 0)
                return ssvrow.armorMod2[3];
        }
        return 0;
    }

    public static uint GetDBCscalestatDPSMod(ScalingStatValuesEntry ssvrow, uint flags)
    {
        if ((flags & 0x7E00) != 0 && (flags & 0x00000200) == 0)
        {
            if ((flags & 0x00000400) != 0)
                return ssvrow.dpsMod[1];
            if ((flags & 0x00000800) != 0)
                return ssvrow.dpsMod[2];
            if ((flags & 0x00001000) != 0)
                return ssvrow.dpsMod[3];
            if ((flags & 0x00002000) != 0)
                return ssvrow.dpsMod[4];
            if ((flags & 0x00004000) != 0) // not used?
                return ssvrow.dpsMod[5];
        }
    }

    private static bool LoaderStub<T>(string dbcPath, string filename, string format, bool ind, ref DBCStorage<T> storage, bool loadStrs)
    {
        string dbcPathFile = Path.Combine(dbcPath, filename);
        Console.WriteLine($"Loading {dbcPathFile}.");
        return storage.Load(dbcPathFile, format, ind, loadStrs);
    }

    public static bool LoadRSDBCs()
    {
        string rsdbcPath = RSDBCPath;
        if (!LoaderStub(rsdbcPath, "AreaTable.dbc", AreatableFormat, true, ref dbcArea, true)) return false;
        if (!LoaderStub(rsdbcPath, "ChatChannels.dbc", ChatChannelFormat, true, ref dbcChatChannels, false)) return false;
        return true;
    }

    public static bool LoadDBCs()
    {
        string dbcPath = DBCPath;
        if (!LoaderStub(dbcPath, "Achievement.dbc", AchievementFmt, true, ref dbcAchievement, true)) return false;
        if (!LoaderStub(dbcPath, "Achievement_Category.dbc", AchievementCategory, true, ref dbcAchievementCategory, true)) return false;
        if (!LoaderStub(dbcPath, "Achievement_Criteria.dbc", AchievementCriteria, true, ref dbcAchivementCriteria, true)) return false;
        if (!LoaderStub(dbcPath, "AreaGroup.dbc", AreaGroupFormat, true, ref dbcAreaGroup, true)) return false;
        if (!LoaderStub(dbcPath, "AreaTable.dbc", AreatableFormat, true, ref dbcArea, true)) return false;
        if (!LoaderStub(dbcPath, "AreaTrigger.dbc", AreaTriggerFormat, true, ref dbcAreaTrigger, true)) return false;
        if (!LoaderStub(dbcPath, "AuctionHouse.dbc", AuctionHouseDBCFormat, true, ref dbcAuctionHouse, false)) return false;
        if (!LoaderStub(dbcPath, "BankBagSlotPrices.dbc", BankSlotPriceFormat, true, ref dbcBankSlotPrices, false)) return false;
        if (!LoaderStub(dbcPath, "BarberShopStyle.dbc", BarberShopStyleFormat, true, ref dbcBarberShopStyle, true)) return false;
        if (!LoaderStub(dbcPath, "CharClasses.dbc", CharClassFormat, true, ref dbcCharClass, true)) return false;
        if (!LoaderStub(dbcPath, "CharRaces.dbc", CharRaceFormat, true, ref dbcCharRace, true)) return false;
        if (!LoaderStub(dbcPath, "CharTitles.dbc", CharTitlesEntryFmt, true, ref dbcCharTitlesEntry, false)) return false;
        if (!LoaderStub(dbcPath, "ChatChannels.dbc", ChatChannelFormat, true, ref dbcChatChannels, false)) return false;
        if (!LoaderStub(dbcPath, "CreatureDisplayInfo.dbc", CreatureDisplayFormat, true, ref dbcCreatureDisplayInfo, true)) return false;
        if (!LoaderStub(dbcPath, "CreatureFamily.dbc", CreatureFamilyFormat, true, ref dbcCreatureFamily, true)) return false;
        if (!LoaderStub(dbcPath, "CreatureSpellData.dbc", CreatureSpellDataFormat, true, ref dbcCreatureSpellData, false)) return false;
        if (!LoaderStub(dbcPath, "CurrencyTypes.dbc", CurrencyTypesFormat, true, ref dbcCurrencyTypes, false)) return false;
        if (!LoaderStub(dbcPath, "DestructibleModelData.dbc", DestructibleModelDataFormat, true, ref dbcDestructibleModelDataEntry, false)) return false;
        if (!LoaderStub(dbcPath, "DurabilityCosts.dbc", DurabilityCostsFormat, true, ref dbcDurabilityCosts, false)) return false;
        if (!LoaderStub(dbcPath, "DurabilityQuality.dbc", DurabilityQualityFormat, true, ref dbcDurabilityQuality, false)) return false;
        if (!LoaderStub(dbcPath, "EmotesText.dbc", EmoteTextEntryFormat, true, ref dbcEmoteEntry, false)) return false;
        if (!LoaderStub(dbcPath, "Faction.dbc", FactionDBCFormat, true, ref dbcFaction, true)) return false;
        if (!LoaderStub(dbcPath, "FactionTemplate.dbc", FactionTemplateDBCFormat, true, ref dbcFactionTemplate, false)) return false;
        if (!LoaderStub(dbcPath, "GemProperties.dbc", GemPropertyEntryFormat, true, ref dbcGemProperty, false)) return false;
        if (!LoaderStub(dbcPath, "GlyphProperties.dbc", GlyphPropertyEntryFormat, true, ref dbcGlyphProperty, false)) return false;
        if (!LoaderStub(dbcPath, "gtBarberShopCostBase.dbc", GTFloatFormat, false, ref dbcBarberShopPrices, false)) return false;
        if (!LoaderStub(dbcPath, "gtChanceToMeleeCrit.dbc", GTFloatFormat, false, ref dbcMeleeCrit, false)) return false;
        if (!LoaderStub(dbcPath, "gtChanceToMeleeCritBase.dbc", GTFloatFormat, false, ref dbcMeleeCritBase, false)) return false;
        if (!LoaderStub(dbcPath, "gtChanceToSpellCrit.dbc", GTFloatFormat, false, ref dbcSpellCrit, false)) return false;
        if (!LoaderStub(dbcPath, "gtChanceToSpellCritBase.dbc", GTFloatFormat, false, ref dbcSpellCritBase, false)) return false;
        if (!LoaderStub(dbcPath, "gtCombatRatings.dbc", GTFloatFormat, false, ref dbcCombatRating, false)) return false;
        if (!LoaderStub(dbcPath, "gtManaRegenBase.dbc", GTFloatFormat, false, ref dbcManaRegenBase, false)) return false;
        if (!LoaderStub(dbcPath, "gtManaRegenMPPerSpt.dbc", GTFloatFormat, false, ref dbcManaRegen, false)) return false;
        if (!LoaderStub(dbcPath, "gtRegenHPPerSpt.dbc", GTFloatFormat, false, ref dbcHPRegen, false)) return false;
        if (!LoaderStub(dbcPath, "gtRegenHPPerSptBase.dbc", GTFloatFormat, false, ref dbcHPRegenBase, false)) return false;
        if (!LoaderStub(dbcPath, "ItemExtendedCost.dbc", ItemExtendedCostFormat, true, ref dbcItemExtendedCost, false)) return false;
        if (!LoaderStub(dbcPath, "ItemRandomProperties.dbc", RandomPropsFormat, true, ref dbcRandomProps, false)) return false;
        if (!LoaderStub(dbcPath, "ItemRandomSuffix.dbc", ItemRandomSuffixFormat, true, ref dbcItemRandomSuffix, false)) return false;
        if (!LoaderStub(dbcPath, "ItemSet.dbc", ItemSetFormat, true, ref dbcItemSet, true)) return false;
        if (!LoaderStub(dbcPath, "LFGDungeons.dbc", LFGDungeonsFormat, true, ref dbcLookingForGroup, false)) return false;
        if (!LoaderStub(dbcPath, "Lock.dbc", LockFormat, true, ref dbcLock, false)) return false;
        if (!LoaderStub(dbcPath, "Map.dbc", MapEntryFormat, true, ref dbcMap, true)) return false;
        if (!LoaderStub(dbcPath, "QuestFactionReward.dbc", QuestFactRewFormat, true, ref dbcQuestFactionReward, false)) return false;
        if (!LoaderStub(dbcPath, "QuestXP.dbc", XpQuestFormat, true, ref dbcQuestXPLevel, false)) return false;
        if (!LoaderStub(dbcPath, "ScalingStatDistribution.dbc", ScalingStatDistributionFormat, true, ref dbcScalingStatDistribution, false)) return false;
        if (!LoaderStub(dbcPath, "ScalingStatValues.dbc", ScalingStatValuesFormat, true, ref dbcScalingStatValues, false)) return false;
        if (!LoaderStub(dbcPath, "SkillLine.dbc", SkillLineEntryFormat, true, ref dbcSkillLine, true)) return false;
        if (!LoaderStub(dbcPath, "SkillLineAbility.dbc", SkillLineSpellFormat, false, ref dbcSkillLineSpell, false)) return false;
        if (!LoaderStub(dbcPath, "Spell.dbc", SpellEntryFormat, true, ref dbcSpell, true)) return false;
        if (!LoaderStub(dbcPath, "SpellCastTimes.dbc", SpellCastTimeFormat, true, ref dbcSpellCastTime, false)) return false;
        if (!LoaderStub(dbcPath, "SpellDuration.dbc", SpellDurationFormat, true, ref dbcSpellDuration, false)) return false;
        if (!LoaderStub(dbcPath, "SpellItemEnchantment.dbc", EnchantEntryFormat, true, ref dbcEnchant, true)) return false;
        if (!LoaderStub(dbcPath, "SpellRadius.dbc", SpellRadiusFormat, true, ref dbcSpellRadius, false)) return false;
        if (!LoaderStub(dbcPath, "SpellRange.dbc", SpellRangeFormat, true, ref dbcSpellRange, false)) return false;
        if (!LoaderStub(dbcPath, "SpellRuneCost.dbc", DBCSpellRuneCostFormat, true, ref dbcSpellRuneCost, false)) return false;
        if (!LoaderStub(dbcPath, "SpellShapeshiftForm.dbc", SpellShapeshiftFormFormat, true, ref dbcSpellShapeshiftForm, false)) return false;
        if (!LoaderStub(dbcPath, "StableSlotPrices.dbc", BankSlotPriceFormat, true, ref dbcStableSlotPrices, false)) return false;
        if (!LoaderStub(dbcPath, "SummonProperties.dbc", DBCSummonPropertiesFormat, true, ref dbcSummonProperties, false)) return false;
        if (!LoaderStub(dbcPath, "Talent.dbc", TalentEntryFormat, true, ref dbcTalent, false)) return false;
        if (!LoaderStub(dbcPath, "TalentTab.dbc", TalentTabEntryFormat, true, ref dbcTalentTab, false)) return false;
        if (!LoaderStub(dbcPath, "TaxiNodes.dbc", DBCTaxiNodeFormat, false, ref dbcTaxiNode, true)) return false;
        if (!LoaderStub(dbcPath, "TaxiPath.dbc", DBCTaxiPathFormat, false, ref dbcTaxiPath, false)) return false;
        if (!LoaderStub(dbcPath, "TaxiPathNode.dbc", DBCTaxiPathNodeFormat, false, ref dbcTaxiPathNode, false)) return false;
        if (!LoaderStub(dbcPath, "Vehicle.dbc", VehicleEntryFormat, true, ref dbcVehicle, true)) return false;
        if (!LoaderStub(dbcPath, "VehicleSeat.dbc", VehicleSeatEntryFormat, true, ref dbcVehicleSeat, true)) return false;
        if (!LoaderStub(dbcPath, "WMOAreaTable.dbc", WMOAreaFormat, true, ref dbcWMOAreaTable, false)) return false;
        if (!LoaderStub(dbcPath, "WorldMapOverlay.dbc", WorldMapOverlayFmt, true, ref dbcWorldMapOverlay, true)) return false;
        if (!LoaderStub(dbcPath, "WorldSafeLocs.dbc", WorldSafeLocsStoreFormat, true, ref dbcWorldSafeLocsStore, true)) return false;
        return true;
    }    
}