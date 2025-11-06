using System;
using labyItems.Helpers;
using labyItems.Pages.Configs;
using labyItems.Models.Enums;

namespace labyItems.Pages.Configs
{
    public enum AbilityType
    {
        Other,
        Immunity
    }

    public class GeneralConfig : ConfigBase
    {
        protected override string NoneSelectedText => "General (none selected)";

        // -------------------- Ability Calculation Core --------------------
        public int AbilityTable
        {
            get => _abilityTable;
            set
            {
                SetProperty(ref _abilityTable, value, true);
                Recalculate();
            }
        }
        private int _abilityTable;
        public bool IsFirstEffect
        {
            get => _isFirstEffect;
            set
            {
                SetProperty(ref _isFirstEffect, value, true);
                Recalculate();
            }
        }
        private bool _isFirstEffect;

        public int TotalIsp
        {
            get => _totalIsp;
            private set => SetProperty(ref _totalIsp, value, true);
        }
        private int _totalIsp;

        private void Recalculate()
        {
            if (Power <= 0 || Power <= 0)
            {
                TotalIsp = 0;
                return;
            }

            double rate = GetRate(AbilityTable, IsImmunity, IsFirstEffect);
            TotalIsp = (int)Math.Round(Power / 10.0 * rate);
        }

        private static double GetRate(int table, bool isImmunity, bool isFirstFx) =>
            isImmunity switch
            {
                true when isFirstFx && table is >= 1 and <= 9 => 3,
                true when table is >= 1 and <= 9 => 4,
                true when table is >= 10 and <= 12 => 8,

                false when table is >= 1 and <= 9 => 2,
                false when table == 10 => 3,
                false when table == 11 => 4,
                false when table == 12 => 6,

                _ => 0
            };

        // -------------------- Remaining existing config --------------------
        public bool IsImmunity { get => _isImm; set => SetProperty(ref _isImm, value, true); }
        private bool _isImm;

        // ---------- Resistance ----------
        public bool ResistAll1 { get => _resAll; set => SetProperty(ref _resAll, value, true); }
        private bool _resAll;
        public bool ResistMagic { get => _resMag; set => SetProperty(ref _resMag, value, true); }
        private bool _resMag;
        public bool ResistSpirit { get => _resSpi; set => SetProperty(ref _resSpi, value, true); }
        private bool _resSpi;
        public bool ResistEp { get => _resEp; set => SetProperty(ref _resEp, value, true); }
        private bool _resEp;
        public bool ResistNeuro { get => _resNeuro; set => SetProperty(ref _resNeuro, value, true); }
        private bool _resNeuro;
        public bool ResistAll2 { get => _resAll2; set => SetProperty(ref _resAll2, value, true); }
        private bool _resAll2;
        public bool ResistMagic2 { get => _resMag2; set => SetProperty(ref _resMag2, value, true); }
        private bool _resMag2;
        public bool ResistSpirit2 { get => _resSpi2; set => SetProperty(ref _resSpi2, value, true); }
        private bool _resSpi2;
        public bool ResistEp2 { get => _resEp2; set => SetProperty(ref _resEp2, value, true); }
        private bool _resEp2;
        public bool ResistNeuro2 { get => _resNeuro2; set => SetProperty(ref _resNeuro2, value, true); }
        private bool _resNeuro2;

        // ---------- Casting Levels ----------
        public MagicColours CastingLevelsColour { get => _castColour; set => SetProperty(ref _castColour, value, true); }
        private MagicColours _castColour;
        public int CastingLevelsCount { get => _castingLevelsCount; set => SetProperty(ref _castingLevelsCount, value, true); }
        private int _castingLevelsCount;

        // ---------- Strength ----------
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

        // ---------- Other small items ----------
        public int AdditionalLTMBlocks
        {
            get => _lifeBlocks;
            set => SetProperty(ref _lifeBlocks, Math.Clamp(value, 0, 4), true);
        }
        private int _lifeBlocks;

        public bool LTMKickInIsMagicOrSpirit { get => _LTMKick; set => SetProperty(ref _LTMKick, value, true); }
        private bool _LTMKick;

        public ElfColours? ElfInnateColour { get => _elfInnColour; set => SetProperty(ref _elfInnColour, value, true); }
        private ElfColours? _elfInnColour;
        public ElfInnateLevel? ElvenInnateLevel { get => _elfInnateLevel; set => SetProperty(ref _elfInnateLevel, value, true); }
        private ElfInnateLevel? _elfInnateLevel;
        
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

        public void AddCastingLevels(int total) {
            var addition = CastingLevelsColour == MagicColours.All
                                ? 14 * CastingLevelsCount
                                : 7 * CastingLevelsCount;
            total += addition;
        }

        public int AddElvenInnates(int total) {
          var addition = (ElvenInnateLevel ?? ElfInnateLevel.None) switch
    {
        ElfInnateLevel.Four => 20,
        ElfInnateLevel.Six => 45,
        ElfInnateLevel.Eight => 70,
        ElfInnateLevel.Sixteen => 140,
        ElfInnateLevel.TwentyFour => 210,
        _ => 0
    };
    return addition + total;
        }

        protected override int ExtraTotal()
        {
            int t = 0;

            // Existing maths
            ResistAll1.AddIf(t, 20);
            ResistMagic.AddIf(t, 12);
            ResistSpirit.AddIf(t, 12);
            ResistEp.AddIf(t, 8);
            ResistNeuro.AddIf(t, 8);
            ResistAll2.AddIf(t, 60);
            ResistMagic2.AddIf(t, 36);
            ResistSpirit2.AddIf(t, 36);
            ResistEp2.AddIf(t, 24);
            ResistNeuro2.AddIf(t, 24);
            
            AddCastingLevels(t);

            t += 15 * StrengthPlus1NonStackingCount;
            t += 20 * StrengthPlus1StackingTo2Count;
            t += 45 * StrengthPlus2NonStackingCount;
            t += 10 * ColdRage25PerDayCount;
            t += 25 * BerserkRage50PerDayCount;
            t += 40 * ColdRage25VsOneGroupAlwaysCount;
            t += 6 * RepelAttractOneTypePerDayCount;
            t += 8 * RepelAttractOneGroupPerDayCount;
            t += 10 * RepelLifePerDayCount;

            // New: automatic ability/immunity ISP
            // t += TotalIsp;

            t += 15 * DisciplinePerDayCount;
            t += 20 * WardPact8LevelsCount;
            t += 15 * KiOrPrimalStrikePerDayCount;
            t += 8 * EmpowerWeaponMagicCount;
            t += 10 * EmpowerWeaponSpiritCount;
            t += 20 * EmpowerWeaponManticCount;
            t += 2 * ExtraColoursForEmpowerments;
            t += 3 * ExtraAlignmentsForEmpowerments;
            t += 15 * ScholarlyInterestPerDayCount;
            t += 8 * KnowledgeOfArcanePerDayCount;
            t += 10 * MajorPrayerPerDayPowerbaseCount;
            t += 6 * MajorPrayerPerDayPowerbaseSubjectCount;
            t += 4 * MinorPrayerPerDayPowerbaseCount;

            var lifeCost = 5 * AdditionalLTMBlocks;
            if (LTMKickInIsMagicOrSpirit) lifeCost *= 2;
            t += lifeCost;

            AddElvenInnates(t);
            if (ReadLanguages) t += 6;
            if (DisarmTrapsAsScout) t += 10;
            if (Regeneration) t += 40;
            if (ForearmParry) t += 25;

            t += 5 * PotionRecipesKnownCount;

            return t;
        }

        protected override int ApplyMultipliers(int total) => total;

        public void ApplyGeneral(General.Result picked)
        {
            Name = picked.Index;
            Power = picked.Cost;
            IsImmune = picked.IsImmunity;
            AbilityTable = picked.Table;
        }
    }
}
