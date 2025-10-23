using System;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Configs
{
    public class GeneralConfig : ConfigBase
    {
        protected override string NoneSelectedText => "General (none selected)";

        // ---------- Resistance ----------
        public int ResistanceAllLevels { get => _resAll; set => SetProperty(ref _resAll, value, true); }
        private int _resAll;
        public int ResistanceMagicOrSpiritLevels { get => _resMagSpi; set => SetProperty(ref _resMagSpi, value, true); }
        private int _resMagSpi;
        public int ResistanceEpOrNeuroLevels { get => _resEpNeuro; set => SetProperty(ref _resEpNeuro, value, true); }
        private int _resEpNeuro;

        // +2 Level of Resistance (from +1 above) = 3× the +1 cost for the same category
        public int ResistanceAllPlus2Count { get => _resAllP2; set => SetProperty(ref _resAllP2, value, true); }
        private int _resAllP2;
        public int ResistanceMagOrSpiritPlus2Count { get => _resMagSpiP2; set => SetProperty(ref _resMagSpiP2, value, true); }
        private int _resMagSpiP2;
        public int ResistanceEpOrNeuroPlus2Count { get => _resEpNeuroP2; set => SetProperty(ref _resEpNeuroP2, value, true); }
        private int _resEpNeuroP2;

        // ---------- Casting Levels ----------
        // “+N Casting Levels (All/One Colour of Magic) 14/7 per level”
        public int CastingLevelsAll { get => _castAll; set => SetProperty(ref _castAll, value, true); }
        private int _castAll;
        public int CastingLevelsOneColour { get => _castOne; set => SetProperty(ref _castOne, value, true); }
        private int _castOne;

        // ---------- Strength ----------
        // “+1 Strength (Non-Stacking/Stacking to +2) 15/20”, “+2 Strength (Non-Stacking) 45”
        public int StrengthPlus1NonStackingCount { get => _str1Non; set => SetProperty(ref _str1Non, value, true); }
        private int _str1Non;
        public int StrengthPlus1StackingTo2Count { get => _str1Stack; set => SetProperty(ref _str1Stack, value, true); }
        private int _str1Stack;
        public int StrengthPlus2NonStackingCount { get => _str2Non; set => SetProperty(ref _str2Non, value, true); }
        private int _str2Non;

        // ---------- Rages ----------
        public int ColdRage25PerDayCount { get => _cold25; set => SetProperty(ref _cold25, value, true); }
        private int _cold25;
        public int BerserkRage50PerDayCount { get => _ber50; set => SetProperty(ref _ber50, value, true); }
        private int _ber50;
        public int ColdRage25VsOneGroupAlwaysCount { get => _cold25Always; set => SetProperty(ref _cold25Always, value, true); }
        private int _cold25Always;

        // ---------- Repel/Attract ----------
        public int RepelAttractOneTypePerDayCount { get => _repType; set => SetProperty(ref _repType, value, true); }
        private int _repType;
        public int RepelAttractOneGroupPerDayCount { get => _repGroup; set => SetProperty(ref _repGroup, value, true); }
        private int _repGroup;
        public int RepelLifePerDayCount { get => _repLife; set => SetProperty(ref _repLife, value, true); }
        private int _repLife;

        // ---------- Abilities by Tier (per 10 CP) ----------
        public int AbilityTier1to9CpBlocks { get => _abT1_9; set => SetProperty(ref _abT1_9, value, true); }
        private int _abT1_9;
        public int AbilityTier10CpBlocks { get => _abT10; set => SetProperty(ref _abT10, value, true); }
        private int _abT10;
        public int AbilityTier11CpBlocks { get => _abT11; set => SetProperty(ref _abT11, value, true); }
        private int _abT11;
        public int AbilityTier12CpBlocks { get => _abT12; set => SetProperty(ref _abT12, value, true); }
        private int _abT12;

        // ---------- Immunities by Tier (per 10 CP) ----------
        public int ImmunityTier1to9CpBlocks { get => _imT1_9; set => SetProperty(ref _imT1_9, value, true); }
        private int _imT1_9;
        public int ImmunityTier10to12CpBlocks { get => _imT10_12; set => SetProperty(ref _imT10_12, value, true); }
        private int _imT10_12;
        // “Immunity T1–9 vs 1st effect each day & 5 minutes thereafter” — 3 per 10 CP
        public int ImmunityTier1to9FirstFxCpBlocks { get => _imT1_9First; set => SetProperty(ref _imT1_9First, value, true); }
        private int _imT1_9First;

        // ---------- Misc powers ----------
        public int DisciplinePerDayCount { get => _discipline; set => SetProperty(ref _discipline, value, true); }
        private int _discipline;
        public int WardPact8LevelsCount { get => _wardPact; set => SetProperty(ref _wardPact, value, true); }
        private int _wardPact;
        public int KiOrPrimalStrikePerDayCount { get => _ki; set => SetProperty(ref _ki, value, true); }
        private int _ki;

        // ---------- Weapon empowerments ----------
        public int EmpowerWeaponMagicCount { get => _empMagic; set => SetProperty(ref _empMagic, value, true); }
        private int _empMagic;
        public int EmpowerWeaponSpiritCount { get => _empSpirit; set => SetProperty(ref _empSpirit, value, true); }
        private int _empSpirit;
        public int EmpowerWeaponManticCount { get => _empMantic; set => SetProperty(ref _empMantic, value, true); }
        private int _empMantic;
        public int ExtraColoursForEmpowerments { get => _extraColours; set => SetProperty(ref _extraColours, value, true); }
        private int _extraColours;
        public int ExtraAlignmentsForEmpowerments { get => _extraAlignments; set => SetProperty(ref _extraAlignments, value, true); }
        private int _extraAlignments;

        // ---------- Undead-themed (require item types—ignored in cost rules here) ----------
        public int UndeadTouchPerDayCount { get => _undTouch; set => SetProperty(ref _undTouch, value, true); }
        private int _undTouch;
        public int GaseousFormPerDayCount { get => _gasForm; set => SetProperty(ref _gasForm, value, true); }
        private int _gasForm;
        public int PlaneShiftPerDayCount { get => _planeShift; set => SetProperty(ref _planeShift, value, true); }
        private int _planeShift;
        public int WalkThroughWallsPerDayCount { get => _walkWalls; set => SetProperty(ref _walkWalls, value, true); }
        private int _walkWalls;

        // ---------- Knowledge / Prayers ----------
        public int ScholarlyInterestPerDayCount { get => _scholarly; set => SetProperty(ref _scholarly, value, true); }
        private int _scholarly;
        public int KnowledgeOfArcanePerDayCount { get => _knowArcane; set => SetProperty(ref _knowArcane, value, true); }
        private int _knowArcane;
        public int MajorPrayerPerDayPowerbaseCount { get => _majPrayerPb; set => SetProperty(ref _majPrayerPb, value, true); }
        private int _majPrayerPb;
        public int MajorPrayerPerDayPowerbaseSubjectCount { get => _majPrayerPbs; set => SetProperty(ref _majPrayerPbs, value, true); }
        private int _majPrayerPbs;
        public int MinorPrayerPerDayPowerbaseCount { get => _minPrayer; set => SetProperty(ref _minPrayer, value, true); }
        private int _minPrayer;

        public int AdditionalLTMBlocks
        {
            get => _lifeBlocks;
            set
            {
                var v = Math.Clamp(value, 0, 4);
                SetProperty(ref _lifeBlocks, v, true);
            }
        }
        private int _lifeBlocks;

        public bool LTMKickInIsMagicOrSpirit { get => _LTMKick; set => SetProperty(ref _LTMKick, value, true); }
        private bool _LTMKick;

        public int ElfDraveInnateLevel4Count { get => _inn4; set => SetProperty(ref _inn4, value, true); }
        private int _inn4;
        public int ElfDraveInnateLevel6Count { get => _inn6; set => SetProperty(ref _inn6, value, true); }
        private int _inn6;
        public int ElfDraveInnateLevel8Count { get => _inn8; set => SetProperty(ref _inn8, value, true); }
        private int _inn8;
        public int ExtraInnatesX2From8thCount { get => _x2Inn; set => SetProperty(ref _x2Inn, value, true); }
        private int _x2Inn;
        public int ExtraInnatesX3FromX2Count { get => _x3Inn; set => SetProperty(ref _x3Inn, value, true); }
        private int _x3Inn;

        public bool ReadLanguages { get => _readLang; set => SetProperty(ref _readLang, value, true); }
        private bool _readLang;
        public bool DisarmTrapsAsScout { get => _disarm; set => SetProperty(ref _disarm, value, true); }
        private bool _disarm;
        public int PotionRecipesKnownCount { get => _recipes; set => SetProperty(ref _recipes, value, true); }
        private int _recipes;
        public bool Regeneration { get => _regen; set => SetProperty(ref _regen, value, true); }
        private bool _regen;
        public bool ForearmParry { get => _parry; set => SetProperty(ref _parry, value, true); }
        private bool _parry;

        protected override double ExtraTotal()
        {
            double t = 0;

            t += 20 * ResistanceAllLevels;
            t += 12 * ResistanceMagicOrSpiritLevels;
            t += 8 * ResistanceEpOrNeuroLevels;
            t += (3 * 20) * ResistanceAllPlus2Count;
            t += (3 * 12) * ResistanceMagOrSpiritPlus2Count;
            t += (3 * 8) * ResistanceEpOrNeuroPlus2Count;
            t += 14 * CastingLevelsAll;
            t += 7 * CastingLevelsOneColour;
            t += 15 * StrengthPlus1NonStackingCount;
            t += 20 * StrengthPlus1StackingTo2Count;
            t += 45 * StrengthPlus2NonStackingCount;
            t += 10 * ColdRage25PerDayCount;
            t += 25 * BerserkRage50PerDayCount;
            t += 40 * ColdRage25VsOneGroupAlwaysCount;
            t += 6 * RepelAttractOneTypePerDayCount;
            t += 8 * RepelAttractOneGroupPerDayCount;
            t += 10 * RepelLifePerDayCount;
            t += 2 * AbilityTier1to9CpBlocks;
            t += 3 * AbilityTier10CpBlocks;
            t += 4 * AbilityTier11CpBlocks;
            t += 6 * AbilityTier12CpBlocks;
            t += 4 * ImmunityTier1to9CpBlocks;
            t += 8 * ImmunityTier10to12CpBlocks;
            t += 3 * ImmunityTier1to9FirstFxCpBlocks;
            t += 15 * DisciplinePerDayCount;
            t += 20 * WardPact8LevelsCount;
            t += 15 * KiOrPrimalStrikePerDayCount;
            t += 8 * EmpowerWeaponMagicCount;
            t += 10 * EmpowerWeaponSpiritCount;
            t += 20 * EmpowerWeaponManticCount;
            t += 2 * ExtraColoursForEmpowerments;
            t += 3 * ExtraAlignmentsForEmpowerments;
            t += 15 * UndeadTouchPerDayCount;
            t += 35 * GaseousFormPerDayCount;
            t += 20 * PlaneShiftPerDayCount;
            t += 20 * WalkThroughWallsPerDayCount;
            t += 15 * ScholarlyInterestPerDayCount;
            t += 8 * KnowledgeOfArcanePerDayCount;
            t += 10 * MajorPrayerPerDayPowerbaseCount;
            t += 6 * MajorPrayerPerDayPowerbaseSubjectCount;
            t += 4 * MinorPrayerPerDayPowerbaseCount;

            var lifeCost = 5 * AdditionalLTMBlocks;
            if (LTMKickInIsMagicOrSpirit) lifeCost *= 2;
            t += lifeCost;
            t += 20 * ElfDraveInnateLevel4Count;
            t += 45 * ElfDraveInnateLevel6Count;
            t += 70 * ElfDraveInnateLevel8Count;
            t += 70 * ExtraInnatesX2From8thCount;
            t += 70 * ExtraInnatesX3FromX2Count;
            if (ReadLanguages) t += 6;
            if (DisarmTrapsAsScout) t += 10;
            t += 5 * PotionRecipesKnownCount;
            if (Regeneration) t += 40;
            if (ForearmParry) t += 25;

            return t;
        }

        protected override double ApplyMultipliers(double total)
            => total;
    }
}
