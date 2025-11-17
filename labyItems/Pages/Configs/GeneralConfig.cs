using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Pages.Calculator;
using labyItems.Pages.Configs;

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

        // ---------- Casting Levels ----------
        public MagicColours CastingLevelsColour
        {
            get => _castColour;
            set => SetProperty(ref _castColour, value, true);
        }
        private MagicColours _castColour;
        public int CastingLevelsCount
        {
            get => _castingLevelsCount;
            set => SetProperty(ref _castingLevelsCount, value, true);
        }
        private int _castingLevelsCount;

        // --------- Elf innates -------------
        public IReadOnlyList<string> ElfInnateLevelLabels { get; } =
            new[] { "None", "4th", "6th", "8th", "8th (doubled)", "8th (tripled)" };

        // Slider index 0..5 <-> enum
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

        // text used in the summary (matches labels)
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

        // -1 = none, 0 = +1 non-stack, 1 = +1 stack to +2, 2 = +2 non-stack
        private int _strengthEnchantIndex = -1;
        public int StrengthEnchantIndex
        {
            get => _strengthEnchantIndex;
            set => SetProperty(ref _strengthEnchantIndex, value, true);
        }
        public int StrengthEnchantCost =>
            StrengthEnchantIndex switch
            {
                1 => 15, // +1 Str (non-stacking)
                2 => 20, // +1 Str (stacking to +2)
                3 => 45, // +2 Str (non-stacking)
                _ => 0, // none selected
            };
        public string StrengthEnchantDescription =>
            StrengthEnchantIndex switch
            {
                0 => "+1 Str (non-stacking)",
                1 => "+1 Str (stacking to +2)",
                2 => "+2 Str (non-stacking)",
                _ => "No Strength enchantment",
            };
        public IList<string> StrengthLabels { get; } =
            new[]
            {
                "slide to add strength", // 0
                "+1 strength", // 1
                "+2 strength (not-stacking)", // 2
                "+2 strength", // 3
            };

        // ---------- Rages ----------
        public int ColdRage25PerDayCount
        {
            get => _cold25;
            set => SetProperty(ref _cold25, value, true);
        }
        private int _cold25;
        public int BerserkRage50PerDayCount
        {
            get => _ber50;
            set => SetProperty(ref _ber50, value, true);
        }
        private int _ber50;
        public int ColdRage25VsOneGroupAlwaysCount
        {
            get => _cold25Always;
            set => SetProperty(ref _cold25Always, value, true);
        }
        private int _cold25Always;

        // ---------- Repel/Attract ----------
        public string RepelOrAttractLabel =>
            $"{(string.IsNullOrEmpty(RepelOrAttract) ? "Repel/Attract" : RepelOrAttract)} {(string.IsNullOrEmpty(RepelAttractGroupName) ? "a group" : RepelAttractGroupName)} {RepelAttractOneTypePerDayCount} times per day.";
        public int RepelAttractOneTypePerDayCount
        {
            get => _repType;
            set
            {
                SetProperty(ref _repType, value, true);
                OnPropertyChanged(nameof(RepelOrAttractLabel));
            }
        }
        private int _repType;
        public int RepelAttractOneGroupPerDayCount
        {
            get => _repGroup;
            set => SetProperty(ref _repGroup, value, true);
        }
        private int _repGroup;
        public int RepelLifePerDayCount
        {
            get => _repLife;
            set => SetProperty(ref _repLife, value, true);
        }
        private int _repLife;

        private string _repelOrAttract;
        public string RepelOrAttract
        {
            get => _repelOrAttract;
            set
            {
                _repelOrAttract = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RepelOrAttractLabel));
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
                OnPropertyChanged(nameof(RepelOrAttractLabel));
            }
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
        public int KiOrPrimalStrikePerDayCount
        {
            get => _ki;
            set => SetProperty(ref _ki, value, true);
        }
        private int _ki;

        // ---------- Weapon empowerments ----------
        private int _empMagic;
        private int _empowerWeaponMagicCount;
        public int EmpowerWeaponMagicCount
        {
            get => _empowerWeaponMagicCount;
            set
            {
                _empowerWeaponMagicCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowExtraColours));
            }
        }

        public bool ShowExtraColours => EmpowerWeaponMagicCount > 0 || EmpowerWeaponManticCount > 0;

        public ObservableCollection<MagicColours?> ExtraColours { get; } =
            new ObservableCollection<MagicColours?> { null }; // start with one empty row

        private string _extraColoursSummary;
        public string ExtraColoursSummary
        {
            get => _extraColoursSummary;
            set
            {
                _extraColoursSummary = value;
                OnPropertyChanged();
            }
        }

        private int _extraColoursCount;
        public int ExtraColoursCount
        {
            get => _extraColoursCount;
            set
            {
                _extraColoursCount = value;
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
                _empowerWeaponSpiritCount = value;
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
                _empowerWeaponManticCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowExtraAlignments));
                OnPropertyChanged(nameof(ShowExtraColours));
            }
        }

        public bool ShowExtraAlignments =>
            EmpowerWeaponSpiritCount > 0 || EmpowerWeaponManticCount > 0;

        public ObservableCollection<Alignments?> ExtraAlignments { get; } =
            new ObservableCollection<Alignments?> { null }; // start with one empty row

        private string _extraAlignmentsSummary;
        public string ExtraAlignmentsSummary
        {
            get => _extraAlignmentsSummary;
            set
            {
                _extraAlignmentsSummary = value;
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

        // minor / major – reuse TwoOptionSwitch's string values
        public string PrayerTimesPerDayLabel =>
            $"{PrayerPowerbase} {(string.IsNullOrWhiteSpace(PrayerSize) ? "Minor" : char.ToUpper(PrayerSize[0]) + PrayerSize[1..])} prayer {PrayerTimesPerDay} times per day {(string.IsNullOrEmpty(PrayerSubject) ? string.Empty : "on " + PrayerSubject)}";
        private string _prayerSize; // "minor" or "major"
        public string PrayerSize
        {
            get => _prayerSize;
            set
            {
                _prayerSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerTimesPerDayLabel));
            }
        }

        // Powerbase: Earthpower, Neuro, Spirit, Magic, etc.
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

        // Times per day

        private int _prayerTimesPerDay;
        public int PrayerTimesPerDay
        {
            get => _prayerTimesPerDay;
            set
            {
                _prayerTimesPerDay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerTimesPerDayLabel));
            }
        }

        // Optional subject text
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

        // ---------- Other small items ----------
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
            new() { "🪄 Magic", "👻 Spirit" };

        public string LtmSummary =>
            $"{LtmValue} Additional {LtmType ?? string.Empty}{(LtmType == null ? string.Empty : " ")}Live-to-minus";
        private string _ltmType;
        public string LtmType
        {
            get => _ltmType;
            set
            {
                _ltmType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LtmSummary));
            }
        }

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

        public void AddCastingLevels(ref int total)
        {
            var addition =
                CastingLevelsColour == MagicColours.All
                    ? 14 * CastingLevelsCount
                    : 7 * CastingLevelsCount;
            total += addition;
        }

        // Collection feeding the control
        public ObservableCollection<string?> PermRageCategoryItems { get; } =
            new ObservableCollection<string?> { null }; // start with a single empty row

        // Summary text the control will fill
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

        // Count of fields with values
        private int _rageCategoriesCount;
        public int RageCategoriesCount
        {
            get => _rageCategoriesCount;
            set
            {
                _rageCategoriesCount = value;
                OnPropertyChanged();
            }
        }

        // Human-readable label, built by the control using CountLabelFormat
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

        // Collection feeding the control
        public ObservableCollection<string?> WardPacts { get; } =
            new ObservableCollection<string?> { null }; // start with a single empty row

        // Summary text the control will fill
        private string _wardPactsSummary;
        public string WardPactsSummary
        {
            get => _wardPactsSummary;
            set
            {
                _wardPactsSummary = value;
                OnPropertyChanged();
            }
        }

        // Count of fields with values
        private int _wardPactsCount;
        public int WardPactsCount
        {
            get => _wardPactsCount;
            set
            {
                _wardPactsCount = value;
                OnPropertyChanged();
            }
        }

        // Human-readable label, built by the control using CountLabelFormat
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

        public void AddElvenInnates(ref int total)
        {
            if (ElfInnateColour is not null && ElvenInnateLevel is not null)
            {
                var addition = ElvenInnateLevel switch
                {
                    ElfInnateLevel.Four => 20,
                    ElfInnateLevel.Six => 45,
                    ElfInnateLevel.Eight => 70,
                    ElfInnateLevel.Sixteen => 140,
                    ElfInnateLevel.TwentyFour => 210,
                    _ => 0,
                };
                total += addition;
            }
        }

        public void AddResistanceLevels(ref int total)
        {
            if (ResistanceType is not null)
            {
                var addition = ResistanceType switch
                {
                    GeneralResistanceTypes.All => 20 * ResistanceLevels,
                    GeneralResistanceTypes.Spirit => 12 * ResistanceLevels,
                    GeneralResistanceTypes.Magic => 12 * ResistanceLevels,
                    GeneralResistanceTypes.EarthPower => 8 * ResistanceLevels,
                    GeneralResistanceTypes.Neuronic => 8 * ResistanceLevels,
                    _ => 0,
                };
                total += addition;
            }
        }

        private int CalculateLtmCost() => LtmType == null ? 5 * LtmValue : (LtmValue * 5) * 2;

        protected override int ExtraTotal()
        {
            int t = 0;
            t += ((Power / 10) * GetRate());
            AddResistanceLevels(ref t);
            AddCastingLevels(ref t);

            t += StrengthEnchantCost;
            t += 10 * ColdRage25PerDayCount;
            t += 25 * BerserkRage50PerDayCount;
            t += 40 * RageCategoriesCount;

            t += 6 * RepelAttractOneTypePerDayCount;
            t += 8 * RepelAttractOneGroupPerDayCount;
            t += 10 * RepelLifePerDayCount;
            t += 15 * DisciplinePerDayCount;
            t += 20 * WardPactsCount;
            t += 15 * KiOrPrimalStrikePerDayCount;
            t += 8 * EmpowerWeaponMagicCount;
            t += 10 * EmpowerWeaponSpiritCount;
            t += 20 * EmpowerWeaponManticCount;
            // t += 2 * ExtraColoursForEmpowerments;
            // t += 3 * ExtraAlignmentsForEmpowerments;
            t += 15 * ScholarlyInterestPerDayCount;
            t += 8 * KnowledgeOfArcanePerDayCount;
            // t += 10 * MajorPrayerPerDayPowerbaseCount;
            // t += 6 * MajorPrayerPerDayPowerbaseSubjectCount;
            // t += 4 * MinorPrayerPerDayPowerbaseCount;

            t += CalculateLtmCost();

            AddElvenInnates(ref t);
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

        protected override int ApplyMultipliers(int total) => total;

        public void ApplyGeneral(General.Result picked)
        {
            Power = picked.Cost;
            IsImmune = picked.IsImmunity;
            AbilityTable = picked.Table;
            Name = picked.Index;
        }
    }
}
