using System;
using System.Collections.ObjectModel;
// using System.Collections.Specialized; // Seems unused in this file
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Pages.Calculator;

// using labyItems.Pages.Configs; // Redundant: same namespace as this file

namespace labyItems.Pages.Configs
{
    public class GeneralConfig : ConfigBase
    {
        protected override string NoneSelectedText => "Ability (none selected)";

        // -------------------- Ability Calculation Core --------------------
        public int AbilityTable
        {
            get => _abilityTable;
            set { SetProperty(ref _abilityTable, value, true); }
        }
        private int _abilityTable;

        public bool IsFirstEffect
        {
            get => _isFirstEffect;
            set { SetProperty(ref _isFirstEffect, value, true); }
        }
        private bool _isFirstEffect;

        private int GetRate() =>
            IsImmunity switch
            {
                false when AbilityTable is >= 1 and <= 9 => 2,
                true when IsFirstEffect && AbilityTable is >= 1 and <= 9 => 3,
                false when AbilityTable == 10 => 3,
                false when AbilityTable == 11 => 4,
                true when AbilityTable is >= 1 and <= 9 => 4,
                false when AbilityTable == 12 => 6,
                true when AbilityTable is >= 10 and <= 12 => 8,
                _ => 0,
            };

        // -------------------- Remaining existing config --------------------
        public bool IsImmunity
        {
            get => _isImm;
            set => SetProperty(ref _isImm, value, true);
        }
        private bool _isImm;

        // ---------- Resistance ----------
        private GeneralResistanceTypes? _resistanceType;
        public GeneralResistanceTypes? ResistanceType
        {
            get => _resistanceType;
            set => SetProperty(ref _resistanceType, value, true);
        }

        private int _resistanceLevels;
        public int ResistanceLevels
        {
            get => _resistanceLevels;
            set => SetProperty(ref _resistanceLevels, value, true);
        }

        //--------- Undead touch fx ---------------
        private UndeadTouchEffects? _undeadTouchEffect;
        public UndeadTouchEffects? UndeadTouchEffect
        {
            get => _undeadTouchEffect;
            set => SetProperty(ref _undeadTouchEffect, value, true);
        }

        private int _undeadTouchEffectCount;
        public int UndeadTouchEffectCount
        {
            get => _undeadTouchEffectCount;
            set => SetProperty(ref _undeadTouchEffectCount, value, UndeadTouchEffect.HasValue);
        }

        //--------------Other undead bits-------------//
        private int _gaseousFormPerDayCount;
        public int GaseousFormPerDayCount
        {
            get => _gaseousFormPerDayCount;
            set {
                OnPropertyChanged();
            SetProperty(ref _gaseousFormPerDayCount, value, true);
            } 
        }
        private int _planeShiftPerDayCount;
        public int PlaneShiftPerDayCount
        {
            get => _planeShiftPerDayCount;
            set => SetProperty(ref _planeShiftPerDayCount, value, true);
        }
        private int _walkThroughWallsPerDayCount;
        public int WalkThroughWallsPerDayCount
        {
            get => _walkThroughWallsPerDayCount;
            set {
                OnPropertyChanged();
                SetProperty(ref _walkThroughWallsPerDayCount, value, true);
            }
        }

        // ---------- Casting Levels ----------
        public ExtendedMagicColours? CastingLevelsColour
        {
            get => _castColour;
            set => SetProperty(ref _castColour, value, true);
        }
        private ExtendedMagicColours? _castColour;

        public int CastingLevelsCount
        {
            get => _castingLevelsCount;
            set => SetProperty(ref _castingLevelsCount, value, true);
        }
        private int _castingLevelsCount;

        // --------- Elf innates -------------
        public IReadOnlyList<string> ElfInnateLevelLabels { get; } =
            new[] { "None", "4th", "6th", "8th", "8th (doubled)", "8th (tripled)" };

        public int ElfInnateLevelIndex
        {
            get =>
                ElvenInnateLevel switch
                {
                    ElfInnateLevel.None => 0,
                    ElfInnateLevel.Four => 1,
                    ElfInnateLevel.Six => 2,
                    ElfInnateLevel.Eight => 3,
                    ElfInnateLevel.Sixteen => 4, // 8th doubled
                    ElfInnateLevel.TwentyFour => 5, // 8th tripled
                    _ => 0,
                };
            set
            {
                var newLevel = value switch
                {
                    0 => ElfInnateLevel.None,
                    1 => ElfInnateLevel.Four,
                    2 => ElfInnateLevel.Six,
                    3 => ElfInnateLevel.Eight,
                    4 => ElfInnateLevel.Sixteen,
                    5 => ElfInnateLevel.TwentyFour,
                    _ => ElfInnateLevel.None,
                };

                if (ElvenInnateLevel != newLevel)
                {
                    ElvenInnateLevel = newLevel;
                    OnPropertyChanged(nameof(ElvenInnateLevel));
                    OnPropertyChanged(nameof(ElfInnateLevelIndex));
                    OnPropertyChanged(nameof(ElfInnateSummary));
                }
            }
        }

        private string GetElfInnateLevelText() =>
            ElvenInnateLevel switch
            {
                ElfInnateLevel.None => "0",
                ElfInnateLevel.Four => "4th",
                ElfInnateLevel.Six => "6th",
                ElfInnateLevel.Eight => "8th",
                ElfInnateLevel.Sixteen => "8th (doubled)",
                ElfInnateLevel.TwentyFour => "8th (tripled)",
                _ => "0",
            };

        public string ElfInnateSummary =>
            $"{GetElfInnateLevelText()} {ElfInnateColour} Elf innates per day.";

        private ElfInnateLevel? _elfInnateLevel;
        public ElfInnateLevel? ElvenInnateLevel
        {
            get => _elfInnateLevel;
            set
            {
                SetProperty(ref _elfInnateLevel, value, true);
                OnPropertyChanged(nameof(ElfInnateSummary));
            }
        }

        private ElfColours? _elfInnColour;
        public ElfColours? ElfInnateColour
        {
            get => _elfInnColour;
            set
            {
                SetProperty(ref _elfInnColour, value, true);
                OnPropertyChanged(nameof(ElfInnateSummary));
            }
        }

        private int _strengthEnchantIndex = -1;
        public int StrengthEnchantIndex
        {
            get => _strengthEnchantIndex;
            set => SetProperty(ref _strengthEnchantIndex, value, true);
        }

        public int StrengthEnchantCost =>
            StrengthEnchantIndex switch
            {
                1 => 15,
                2 => 20,
                3 => 45,
                _ => 0,
            };

        public string StrengthEnchantDescription =>
            StrengthEnchantIndex switch
            {
                0 => "+1 Str (non-stacking)",
                1 => "+1 Str (stacking)",
                2 => "+2 Str (non-stacking)",
                _ => "No Strength enchantment",
            };

        public IList<string> StrengthLabels { get; } =
            new[]
            {
                "slide to add strength",
                "+1 strength (non-stacking)",
                "+1 strength (stacking)",
                "+2 strength (non-stacking)",
            };

        // ---------- Rages ----------
        private int _cold25;
        public int ColdRage25PerDayCount
        {
            get => _cold25;
            set => SetProperty(ref _cold25, value, true);
        }

        private int _ber50;
        public int BerserkRage50PerDayCount
        {
            get => _ber50;
            set => SetProperty(ref _ber50, value, true);
        }

        public ObservableCollection<string?> PermRageCategoryItems { get; } =
            new ObservableCollection<string?> { null };

        private string _permRageCategoriesSummary;
        public string PermRageCategoriesSummary
        {
            get => _permRageCategoriesSummary;
            set
            {
                _permRageCategoriesSummary = value;
                OnPropertyChanged();
            }
        }

        private int _rageCategoriesCount;
        public int RageCategoriesCount
        {
            get => _rageCategoriesCount;
            set
            {
                SetProperty(ref _rageCategoriesCount, value, true);
                OnPropertyChanged();
            }
        }

        private string _rageCategoriesCountLabel;
        public string RageCategoriesCountLabel
        {
            get => _rageCategoriesCountLabel;
            set
            {
                _rageCategoriesCountLabel = value;
                OnPropertyChanged();
            }
        }

        private int CalculatePermRage() => 40 * RageCategoriesCount;

        // ---------- Repel/Attract ----------

        private int _repGroup;
        public string RepelAttractGroupLabel =>
            $"{(string.IsNullOrEmpty(RepelAttractGroupName) ? "Repel/Attract" : RepelAttractGroup)} {(string.IsNullOrEmpty(RepelAttractGroupName) ? "one group" : RepelAttractGroupName)} {RepelAttractGroupCount} times per day.";

        public int RepelAttractGroupCount
        {
            get => _repGroup;
            set
            {
                SetProperty(ref _repGroup, value, !string.IsNullOrEmpty(RepelAttractGroup));
                OnPropertyChanged(nameof(RepelAttractGroupLabel));
            }
        }
        private string _repelAttractGroup;
        public string RepelAttractGroup
        {
            get => _repelAttractGroup;
            set
            {
                _repelAttractGroup = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RepelAttractGroupLabel));
            }
        }

        private string _repelAttractGroupName;
        public string RepelAttractGroupName
        {
            get => _repelAttractGroupName;
            set
            {
                _repelAttractGroupName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RepelAttractGroupCount));
                OnPropertyChanged(nameof(RepelAttractGroupLabel));
            }
        }

        // --------------------------- //
        private int _repType;
        public string RepelAttractTypeLabel =>
            $"{(string.IsNullOrEmpty(RepelAttractType) ? "Repel/Attract" : RepelAttractType)} {(string.IsNullOrEmpty(RepelAttractTypeName) ? "one type" : RepelAttractTypeName)} {RepelAttractTypeCount} times per day.";

        public int RepelAttractTypeCount
        {
            get => _repType;
            set
            {
                SetProperty(ref _repType, value, !string.IsNullOrEmpty(RepelAttractTypeName));
                OnPropertyChanged(nameof(RepelAttractTypeLabel));
            }
        }
        private string _repelAttractType;
        public string RepelAttractType
        {
            get => _repelAttractType;
            set
            {
                _repelAttractType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RepelAttractTypeLabel));
            }
        }

        private string _repelAttractTypeName;
        public string RepelAttractTypeName
        {
            get => _repelAttractTypeName;
            set
            {
                _repelAttractTypeName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RepelAttractTypeCount));
                OnPropertyChanged(nameof(RepelAttractTypeLabel));
            }
        }

        // ---------------------------
        private int _repLife;
        public int RepelLifeCount
        {
            get => _repLife;
            set => SetProperty(ref _repLife, value, true);
        }

        // ---------- Misc powers ----------
        public int DisciplinePerDayCount
        {
            get => _discipline;
            set => SetProperty(ref _discipline, value, true);
        }
        private int _discipline;

        public int WardPact8LevelsCount
        {
            get => _wardPact;
            set => SetProperty(ref _wardPact, value, true);
        }
        private int _wardPact;
        // ---------- Weapon empowerments ----------
        private int _empowerWeaponMagicCount;
        public int EmpowerWeaponMagicCount
        {
            get => _empowerWeaponMagicCount;
            set
            {
                SetProperty(ref _empowerWeaponMagicCount, value, true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowExtraColours));
            }
        }

        public bool ShowExtraColours => EmpowerWeaponMagicCount > 0 || EmpowerWeaponManticCount > 0;

        public ObservableCollection<ExtendedMagicColours?> ExtraColours { get; } =
            new ObservableCollection<ExtendedMagicColours?> { null };
        private string _extraColoursSummary;
        public string ExtraColoursSummary
        {
            get => _extraColoursSummary;
            set
            {
                SetProperty(ref _extraColoursSummary, value, true);
                OnPropertyChanged();
            }
        }

        private int _extraColoursCount;
        public int ExtraColoursCount
        {
            get => _extraColoursCount;
            set
            {
                SetProperty(ref _extraColoursCount, value, true);
                OnPropertyChanged();
            }
        }

        private string _extraColoursCountLabel;
        public string ExtraColoursCountLabel
        {
            get => _extraColoursCountLabel;
            set
            {
                _extraColoursCountLabel = value;
                OnPropertyChanged();
            }
        }

        private int _empowerWeaponSpiritCount;
        public int EmpowerWeaponSpiritCount
        {
            get => _empowerWeaponSpiritCount;
            set
            {
                SetProperty(ref _empowerWeaponSpiritCount, value, true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowExtraAlignments));
            }
        }

        private int _empowerWeaponManticCount;
        public int EmpowerWeaponManticCount
        {
            get => _empowerWeaponManticCount;
            set
            {
                SetProperty(ref _empowerWeaponManticCount, value, true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowExtraAlignments));
                OnPropertyChanged(nameof(ShowExtraColours));
            }
        }

        public bool ShowExtraAlignments =>
            EmpowerWeaponSpiritCount > 0 || EmpowerWeaponManticCount > 0;

        public ObservableCollection<Alignments?> ExtraAlignments { get; } =
            new ObservableCollection<Alignments?> { null };
        private string _extraAlignmentsSummary;
        public string ExtraAlignmentsSummary
        {
            get => _extraAlignmentsSummary;
            set
            {
                SetProperty(ref _extraAlignmentsSummary, value, true);
                OnPropertyChanged();
            }
        }

        private int _extraAlignmentsCount;
        public int ExtraAlignmentsCount
        {
            get => _extraAlignmentsCount;
            set
            {
                _extraAlignmentsCount = value;
                SetProperty(ref _extraAlignmentsCount, value, true);
                OnPropertyChanged();
            }
        }

        private string _extraAlignmentsCountLabel;
        public string ExtraAlignmentsCountLabel
        {
            get => _extraAlignmentsCountLabel;
            set
            {
                _extraAlignmentsCountLabel = value;
                OnPropertyChanged();
            }
        }

        // ---------- Knowledge / Prayers ----------
        public int ScholarlyInterestPerDayCount
        {
            get => _scholarly;
            set => SetProperty(ref _scholarly, value, true);
        }
        private int _scholarly;

        public int KnowledgeOfArcanePerDayCount
        {
            get => _knowArcane;
            set => SetProperty(ref _knowArcane, value, true);
        }
        private int _knowArcane;

        public string PrayerTimesPerDayLabel =>
            $"{PrayerPowerbase} {(string.IsNullOrWhiteSpace(PrayerSize) ? "Minor" : char.ToUpper(PrayerSize[0]) + PrayerSize[1..])} prayer {PrayerTimesPerDay} times per day {(string.IsNullOrEmpty(PrayerSubject) ? string.Empty : "on " + PrayerSubject)}";

        private string _prayerSize;
        public string PrayerSize
        {
            get => _prayerSize;
            set
            {
                SetProperty(ref _prayerSize, value, true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerTimesPerDayLabel));
            }
        }

        private PowerbaseEnum? _prayerPowerbase;
        public PowerbaseEnum? PrayerPowerbase
        {
            get => _prayerPowerbase;
            set
            {
                _prayerPowerbase = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerTimesPerDayLabel));
            }
        }

        private int _prayerTimesPerDay;
        public int PrayerTimesPerDay
        {
            get => _prayerTimesPerDay;
            set
            {
                SetProperty(ref _prayerTimesPerDay, value, true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerTimesPerDayLabel));
            }
        }

        private string _prayerSubject;
        public string PrayerSubject
        {
            get => _prayerSubject;
            set
            {
                _prayerSubject = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerTimesPerDayLabel));
            }
        }
        // ------- Ltm / Live to minus stuff
        public int LtmValue
        {
            get => _ltmValue;
            set
            {
                SetProperty(ref _ltmValue, Math.Clamp(value, 0, 24), true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLtm));
                OnPropertyChanged(nameof(LtmSummary));
            }
        }
        private int _ltmValue;
 
        public bool HasLtm => LtmValue > 0;

        public ObservableCollection<string> MagicSpiritOptions { get; } =
            new() { "🪄 Magic", "⽰ Spirit" };


        private string _ltmType;
        public string LtmType
        {
            get => _ltmType;
            set
            {
                SetProperty(ref _ltmType, value, true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(LtmSummary));
            }
        }
        public string LtmSummary =>
            $"{LtmValue} Additional {LtmType ?? string.Empty}{(LtmType == null ? string.Empty : " ")}Live-to-minus";
public ObservableCollection<string> KiOrPrimalStrikeItems { get; } =
            new() { "🐉 Ki", "🧸 Primal" };

        private string _kiOrPrimalStrike;
        public string KiOrPrimalStrike
        {
            get => _kiOrPrimalStrike;
            set
            {
                _kiOrPrimalStrike = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KiStrikeSummary));
            }
        }
        public int KiOrPrimalStrikePerDayCount
        {
            get => _kiOrPrimalStrikePerDayCount;
            set
            {
                SetProperty(ref _kiOrPrimalStrikePerDayCount, value, true);
                OnPropertyChanged();
                OnPropertyChanged(nameof(KiStrikeSummary));
            }
        }
        private int _kiOrPrimalStrikePerDayCount;
        public string KiStrikeSummary =>
            $"{KiOrPrimalStrike} strike ({KiOrPrimalStrikePerDayCount}/day)";


        public bool ReadLanguages
        {
            get => _readLang;
            set => SetProperty(ref _readLang, value, true);
        }
        private bool _readLang;

        public bool DisarmTrapsAsScout
        {
            get => _disarm;
            set => SetProperty(ref _disarm, value, true);
        }
        private bool _disarm;

        public int PotionRecipesKnownCount
        {
            get => _recipes;
            set => SetProperty(ref _recipes, value, true);
        }
        private int _recipes;

        public bool Regeneration
        {
            get => _regen;
            set => SetProperty(ref _regen, value, true);
        }
        private bool _regen;

        public bool ForearmParry
        {
            get => _parry;
            set => SetProperty(ref _parry, value, true);
        }
        private bool _parry;

        public int AddCastingLevels()
        {
            if (!CastingLevelsColour.HasValue || CastingLevelsCount <= 0)
                return 0;

            return CastingLevelsColour == ExtendedMagicColours.All
                ? 14 * CastingLevelsCount
                : 7 * CastingLevelsCount;
        }

        public ObservableCollection<string?> WardPacts { get; } =
            new ObservableCollection<string?> { null };

        private string _wardPactsSummary;
        public string WardPactsSummary
        {
            get => _wardPactsSummary;
            set
            {
                SetProperty(ref _wardPactsSummary, value, true);
                OnPropertyChanged();
            }
        }

        private int _wardPactsCount;
        public int WardPactsCount
        {
            get => _wardPactsCount;
            set
            {
                SetProperty(ref _wardPactsCount, value, true);
                OnPropertyChanged();
            }
        }

        private string _wardPactsCountLabel;
        public string WardPactsCountLabel
        {
            get => _wardPactsCountLabel;
            set
            {
                _wardPactsCountLabel = value;
                OnPropertyChanged();
            }
        }

        public int AddElvenInnates()
        {
            int addition = 0;
            if (ElfInnateColour is not null && ElvenInnateLevel is not null)
            {
                addition = ElvenInnateLevel switch
                {
                    ElfInnateLevel.Four => 20,
                    ElfInnateLevel.Six => 45,
                    ElfInnateLevel.Eight => 70,
                    ElfInnateLevel.Sixteen => 140,
                    ElfInnateLevel.TwentyFour => 210,
                    _ => 0,
                };
            }
            return addition;
        }

        public int AddResistanceLevels()
        {
            int addition = 0;
            if (ResistanceType is not null && ResistanceLevels > 0)
            {
                addition = ResistanceType switch
                {
                    GeneralResistanceTypes.All => 20 * ResistanceLevels,
                    GeneralResistanceTypes.Spirit => 12 * ResistanceLevels,
                    GeneralResistanceTypes.Magic => 12 * ResistanceLevels,
                    GeneralResistanceTypes.EarthPower => 8 * ResistanceLevels,
                    GeneralResistanceTypes.Neuronic => 8 * ResistanceLevels,
                    _ => 0,
                };
            }
            return addition;
        }

        private int CalculateLtmCost() => LtmType == null ? 5 * LtmValue : (LtmValue * 5) * 2;

        private int CalculatePrayerCost()
        {
            if (string.IsNullOrEmpty(PrayerSubject) && PrayerTimesPerDay > 0)
            {
                return PrayerSize == "Major" ? 10 * PrayerTimesPerDay : 6 * PrayerTimesPerDay;
            }
            return 4 * PrayerTimesPerDay;
        }

        private int CalculateExtraColours()
        {
            var colours = ExtraColours.Where(e => e.HasValue).ToList();
            if (colours.Count() == 0)
                return 0;
            if (colours.Any(e => e == ExtendedMagicColours.All))
                return 30;

            return 2 * (Math.Max(colours.Count(), 1) - 1);
        }

        private int CalculateExtraAligns() => 
        3 * (Math.Max(ExtraAlignments.Count(c => c.HasValue), 1) - 1);

        private int CalculateUndeadTouchFx() =>
            (UndeadTouchEffect.HasValue && UndeadTouchEffectCount > 0)
                ? 15 * UndeadTouchEffectCount
                : 0;

        protected override int ExtraTotal()
        {
            int t = 0;
            t += ((Power / 10) * GetRate());
            t += AddCastingLevels();
            t += AddResistanceLevels();
            t += CalculatePermRage();
            t += CalculateExtraColours();
            t += CalculateExtraAligns();
            t += CalculatePrayerCost();
            t += CalculateLtmCost();
            t += AddElvenInnates();
            t += CalculateUndeadTouchFx();
            t += StrengthEnchantCost;
            t += 10 * ColdRage25PerDayCount;
            t += 25 * BerserkRage50PerDayCount;
            t += 6 * RepelAttractGroupCount;
            t += 8 * RepelAttractTypeCount;
            t += 10 * RepelLifeCount;
            t += 15 * DisciplinePerDayCount;
            t += 20 * WardPactsCount;
            t += 15 * KiOrPrimalStrikePerDayCount;
            t += 8 * EmpowerWeaponMagicCount;
            t += 10 * EmpowerWeaponSpiritCount;
            t += 20 * EmpowerWeaponManticCount;
            t += 15 * ScholarlyInterestPerDayCount;
            t += 8 * KnowledgeOfArcanePerDayCount;
            if (ReadLanguages)
                t += 6;
            if (DisarmTrapsAsScout)
                t += 10;
            if (Regeneration)
                t += 40;
            if (ForearmParry)
                t += 25;

            t += 5 * PotionRecipesKnownCount;

            return t;
        }

        public void ApplyGeneral(General.Result picked)
        {
            Power = picked.Cost;
            IsImmune = picked.IsImmunity;
            AbilityTable = picked.Table;
            Name = picked.Index;
        }
    }
}
