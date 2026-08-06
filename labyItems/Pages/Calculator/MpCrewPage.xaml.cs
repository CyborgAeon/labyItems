using System;
using System.Collections.Generic;
using System.ComponentModel;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public partial class MpCrewPage : MpCalculatorPageBase
{
    private const int MacPlusTwoCost = 240;
    private const int SacPlusTwoCost = 180;
    private const int MagicWeaponUnitCost = 150;
    private const int SpiritualWeaponUnitCost = 200;
    private const int ApprenticeBagUnitCost = 100;
    private const int JourneymanBagUnitCost = 250;
    private const int IspMacPlusTwoCost = 24;
    private const int IspSacPlusTwoCost = 18;
    private const int IspMagicWeaponCost = 20;
    private const int IspSpiritWeaponCost = 25;
    private const int IspApprenticeStatusCost = 10;
    private const int IspApprenticeBagCost = 10;
    private const int IspJourneymanBagCost = 15;
    private static readonly string[] WeaponPowerChipOptions = { SupernaturalTypes.Magic, SupernaturalTypes.Spirit };
    private static readonly string[] BagChipOptions = { "🎒 Apprentice", "🛡️ Journeyman" };

    public MpCrewPage()
    {
        InitializeComponent();
        InitializeCalculatorPage();
        PropertyChanged += OnCrewPropertyChanged;
    }

    public bool Mac2Checked
    {
        get => _mac2Checked;
        set
        {
            if (SetProperty(ref _mac2Checked, value))
            {
                if (value && MacChecked)
                    MacChecked = false;
                Recalculate();
            }
        }
    }
    private bool _mac2Checked;

    public bool Sac2Checked
    {
        get => _sac2Checked;
        set
        {
            if (SetProperty(ref _sac2Checked, value))
            {
                if (value && SacChecked)
                    SacChecked = false;
                Recalculate();
            }
        }
    }
    private bool _sac2Checked;

    public IEnumerable<string> WeaponPowerOptions => WeaponPowerChipOptions;

    public string? SelectedWeaponPowerType
    {
        get => _selectedWeaponPowerType;
        set
        {
            if (SetProperty(ref _selectedWeaponPowerType, value))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    SelectedWeaponType = null;
                    SelectedWeaponColour = null;
                    SelectedWeaponAlignment = null;
                }
                OnPropertyChanged(nameof(IsWeaponSelectionVisible));
                OnPropertyChanged(nameof(IsWeaponColourVisible));
                OnPropertyChanged(nameof(IsWeaponAlignmentVisible));
                Recalculate();
            }
        }
    }
    private string? _selectedWeaponPowerType;

    public bool IsWeaponSelectionVisible => !string.IsNullOrWhiteSpace(SelectedWeaponPowerType);

    public WeaponType? SelectedWeaponType { get => _selectedWeaponType; set { if (SetProperty(ref _selectedWeaponType, value)) Recalculate(); } }
    private WeaponType? _selectedWeaponType;

    public MagicColours? SelectedWeaponColour { get => _selectedWeaponColour; set { if (SetProperty(ref _selectedWeaponColour, value)) Recalculate(); } }
    private MagicColours? _selectedWeaponColour;

    public Alignments? SelectedWeaponAlignment { get => _selectedWeaponAlignment; set { if (SetProperty(ref _selectedWeaponAlignment, value)) Recalculate(); } }
    private Alignments? _selectedWeaponAlignment;

    public bool IsWeaponColourVisible => IsWeaponKind("Magic");
    public bool IsWeaponAlignmentVisible => IsWeaponKind("Spirit");

    public bool WeaponApprenticeStatus { get => _weaponApprenticeStatus; set { if (SetProperty(ref _weaponApprenticeStatus, value)) Recalculate(); } }
    private bool _weaponApprenticeStatus;

    public IEnumerable<string> StatusBagOptions => BagChipOptions;

    public string? SelectedBagType
    {
        get => _selectedBagType;
        set
        {
            if (SetProperty(ref _selectedBagType, value))
            {
                Recalculate();
            }
        }
    }
    private string? _selectedBagType;

    protected override Dictionary<string, int> LifeOptions
    {
        get
        {
            var options = base.LifeOptions;
            options["12/4"] = 120;
            return options;
        }
    }

    protected override bool ShouldIncludeSpell(SpellService.SpellRaw spell)
    {
        var isAdvanced = spell.isAdvanced ?? false;
        if (isAdvanced)
            return spell.level <= 3;

        return spell.level <= 10;
    }

    protected override bool ShouldIncludeMiracle(MiracleService.MiracRaw miracle)
    {
        if (miracle.isAdvanced)
            return miracle.power >= 1 && miracle.power <= 3;

        return miracle.power >= 1 && miracle.power <= 5;
    }

    protected override int CalculateSpellCost(SpellOption option, int count)
    {
        int level = Math.Max(0, option.Level);
        int sanitizedCount = Math.Max(0, count);
        if (sanitizedCount == 0)
            return 0;

        if (option.IsAdvanced && level <= 3)
            return 10 + 15 * level * sanitizedCount;

        if (!option.IsAdvanced && level >= 7 && level <= 10)
            return 20 + 10 * level * sanitizedCount;

        return base.CalculateSpellCost(option, sanitizedCount);
    }

    protected override int CalculateMiracleCost(MiracleOption option, int count)
    {
        int power = Math.Max(0, option.Power);
        int sanitizedCount = Math.Max(0, count);
        if (power == 0 || sanitizedCount == 0)
            return 0;

        if (option.IsAdvanced && power <= 3)
            return 20 + 15 * power * sanitizedCount;

        return base.CalculateMiracleCost(option, sanitizedCount);
    }

    protected override void AddCustomContributions(List<ContributionRow> items, ref int running)
    {
        if (Mac2Checked)
            AddContribution(items, ref running, "mac2", "+2 MAC", MacPlusTwoCost);

        if (Sac2Checked)
            AddContribution(items, ref running, "sac2", "+2 SAC", SacPlusTwoCost);

        if (WeaponApprenticeStatus)
            AddContribution(items, ref running, "weapon-apprentice", "Weapon apprentice status", ApprenticeCost);

        if (!string.IsNullOrWhiteSpace(SelectedWeaponPowerType))
        {
            var trimmed = TrimChipLabel(SelectedWeaponPowerType) ?? SelectedWeaponPowerType;
            var unit = GetWeaponUnitCost(trimmed);
            if (unit > 0)
            {
                if (IsWeaponColourVisible && !SelectedWeaponColour.HasValue)
                    goto AfterWeapon;
                if (IsWeaponAlignmentVisible && !SelectedWeaponAlignment.HasValue)
                    goto AfterWeapon;

                var parts = new List<string>();
                if (SelectedWeaponType.HasValue)
                    parts.Add(EnumDisplayFormatter.FormatName(SelectedWeaponType.Value.ToString()));
                if (IsWeaponColourVisible && SelectedWeaponColour.HasValue)
                    parts.Add(EnumDisplayFormatter.FormatName(SelectedWeaponColour.Value.ToString()));
                if (IsWeaponAlignmentVisible && SelectedWeaponAlignment.HasValue)
                    parts.Add(EnumDisplayFormatter.FormatName(SelectedWeaponAlignment.Value.ToString()));

                var detail = string.Join(", ", parts);
                AddContribution(items, ref running, "crew-weapon", $"{trimmed} weapon ({detail})", unit);
            }
        }

AfterWeapon:
        if (!string.IsNullOrWhiteSpace(SelectedBagType))
        {
            var trimmed = TrimChipLabel(SelectedBagType) ?? SelectedBagType;
            var unit = GetBagUnitCost(trimmed);
            if (unit > 0)
            AddContribution(items, ref running, "status-bag", $"{trimmed} status bag", unit);
        }
    }

    protected override void AddCustomIspContributions(List<ContributionRow> items, ref int running)
    {
        if (Mac2Checked)
            AddIspContribution(items, ref running, "mac2", "+2 MAC", IspMacPlusTwoCost);

        if (Sac2Checked)
            AddIspContribution(items, ref running, "sac2", "+2 SAC", IspSacPlusTwoCost);

        if (WeaponApprenticeStatus)
            AddIspContribution(items, ref running, "weapon-apprentice", "Weapon apprentice status", IspApprenticeStatusCost);

        if (!string.IsNullOrWhiteSpace(SelectedWeaponPowerType))
        {
            var trimmed = TrimChipLabel(SelectedWeaponPowerType) ?? SelectedWeaponPowerType;
            var unit = GetWeaponIspCost(trimmed);
            if (unit > 0)
            {
                if (IsWeaponColourVisible && !SelectedWeaponColour.HasValue)
                    goto AfterWeapon;
                if (IsWeaponAlignmentVisible && !SelectedWeaponAlignment.HasValue)
                    goto AfterWeapon;

                var parts = new List<string>();
                if (SelectedWeaponType.HasValue)
                    parts.Add(EnumDisplayFormatter.FormatName(SelectedWeaponType.Value.ToString()));
                if (IsWeaponColourVisible && SelectedWeaponColour.HasValue)
                    parts.Add(EnumDisplayFormatter.FormatName(SelectedWeaponColour.Value.ToString()));
                if (IsWeaponAlignmentVisible && SelectedWeaponAlignment.HasValue)
                    parts.Add(EnumDisplayFormatter.FormatName(SelectedWeaponAlignment.Value.ToString()));

                var detail = string.Join(", ", parts);
                AddIspContribution(items, ref running, "crew-weapon", $"{trimmed} weapon ({detail})", unit);
            }
        }

    AfterWeapon:
        if (!string.IsNullOrWhiteSpace(SelectedBagType))
        {
            var trimmed = TrimChipLabel(SelectedBagType) ?? SelectedBagType;
            var unit = GetBagIspCost(trimmed);
            AddIspContribution(items, ref running, "status-bag", $"{trimmed} status bag", unit, includeWhenZero: unit == 0);
        }
    }

    private static int GetWeaponUnitCost(string? trimmedPower)
    {
        if (string.Equals(trimmedPower, "Magic", StringComparison.OrdinalIgnoreCase))
            return MagicWeaponUnitCost;
        if (string.Equals(trimmedPower, "Spirit", StringComparison.OrdinalIgnoreCase))
            return SpiritualWeaponUnitCost;
        return 0;
    }

    private static int GetBagUnitCost(string? trimmedType)
    {
        if (string.Equals(trimmedType, "Apprentice", StringComparison.OrdinalIgnoreCase))
            return ApprenticeBagUnitCost;
        if (string.Equals(trimmedType, "Journeyman", StringComparison.OrdinalIgnoreCase))
            return JourneymanBagUnitCost;
        return 0;
    }

    private static int GetWeaponIspCost(string? trimmedPower)
    {
        if (string.Equals(trimmedPower, "Magic", StringComparison.OrdinalIgnoreCase))
            return IspMagicWeaponCost;
        if (string.Equals(trimmedPower, "Spirit", StringComparison.OrdinalIgnoreCase))
            return IspSpiritWeaponCost;
        return 0;
    }

    private static int GetBagIspCost(string? trimmedType)
    {
        if (string.Equals(trimmedType, "Apprentice", StringComparison.OrdinalIgnoreCase))
            return IspApprenticeBagCost;
        if (string.Equals(trimmedType, "Journeyman", StringComparison.OrdinalIgnoreCase))
            return IspJourneymanBagCost;
        return 0;
    }

    private void OnCrewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MacChecked))
        {
            if (MacChecked && Mac2Checked)
                Mac2Checked = false;
        }
        else if (e.PropertyName == nameof(SacChecked))
        {
            if (SacChecked && Sac2Checked)
                Sac2Checked = false;
        }
    }

    private bool IsWeaponKind(string expected)
    {
        var trimmed = TrimChipLabel(SelectedWeaponPowerType) ?? SelectedWeaponPowerType;
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        return string.Equals(trimmed.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    protected override DictionarySearchBar<SpellOption> SpellSearchControl => SpellSearch;
    protected override DictionarySearchBar<MiracleOption> MiracleSearchControl => MiracleSearch;
    protected override DictionarySearchBar<EvocationOption> EvocationSearchControl => EvocationSearch;
    protected override DictionarySearchBar<NeuroOption> NeuroSearchControl => NeuroSearch;
    protected override DictionarySlider LifeSliderControl => LifeSlider;
    protected override DictionarySearchBar<WeaponType> WeaponSearchControl => CrewWeaponSearch;
    protected override IEnumerable<ILoadableSearchBar> AdditionalSearchBars => new ILoadableSearchBar[]
    {
        CrewWeaponColourSearch,
        CrewWeaponAlignmentSearch
    };
}
