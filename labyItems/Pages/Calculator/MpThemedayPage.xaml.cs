using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Calculator;

public readonly record struct LocationOption(string Name);

public partial class MpThemedayPage : MpCalculatorPageBase
{
    private const int ManticWeaponCost = 500;
    private const int PureMagicWeaponCost = 400;
    private const int PureSpiritWeaponCost = 500;
    private const int MagicShieldCost = 150;
    private const int SpiritShieldCost = 200;
    private const int MacPlusTwoCost = 240;
    private const int SacPlusTwoCost = 180;
    private const int ApprenticeBagUnitCost = 100;
    private const int JourneymanBagUnitCost = 250;
    private const int CpBenefitCost = 250;
    private const int PowerStoreUnitCost = 30;
    private const int PowerStoreMax = 5;

    private readonly IReadOnlyList<CpBenefitOption> _cpBenefits = new List<CpBenefitOption>
    {
        new("🃏 Dealer", false),
        new("🌀 Place of power (earth/magic/spirit)", false),
        new("🏦 Business", false),
        new("🚜 Farm", false),
        new("🌿 Tamarisk patch", false),
        new("🏠 Home", true),
        new("🏇 Day at the races", false),
        new("⚒️ Forge", false),
        new("⚰️ Crypt", false),
        new("🗿 Statue", false),
        new("🦄 Mystic mount", false),
        new("🛳️ Captain of a ship", false),
        new("🎓 Lecturer", false),
        new("🐑 Herd", false),
        new("🤝 Pair of hands", false),
        new("✒️ Apprentice scribe", false),
        new("🛡️ Apprentice squire", false),
        new("👁️ Blinkered eye", false),
        new("🛖 Haven", false),
        new("🛤️ Roadsmith", false),
        new("🏙️ City bound", true),
        new("🌍 Land bound", true),
        new("🧒 Children named after you", true),
        new("🏰 Guild hall", false),
        new("🌟 Renown", false)
    };

    private readonly Dictionary<string, LocationOption> _locationOptions;
    private static readonly string[] WeaponKindChipOptions = { SupernaturalTypes.PureMagic, SupernaturalTypes.PureSpirit, SupernaturalTypes.Mantic };
    private static readonly string[] ShieldChipOptions = { SupernaturalTypes.Magic, SupernaturalTypes.Spirit };
    private static readonly string[] BagChipOptions = { "🎒 Apprentice", "🛡️ Journeyman" };

    public MpThemedayPage()
    {
        _locationOptions = BuildLocationOptions();
        InitializeComponent();
        InitializeCalculatorPage();
    }

    public IEnumerable<string> WeaponTypeOptions => WeaponKindChipOptions;
    public IEnumerable<string> ShieldTypeOptions => ShieldChipOptions;
    public IEnumerable<string> StatusBagOptions => BagChipOptions;

    public string? SelectedWeaponKind
    {
        get => _selectedWeaponKind;
        set
        {
            if (SetProperty(ref _selectedWeaponKind, value))
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
    private string? _selectedWeaponKind;

    public WeaponType? SelectedWeaponType { get => _selectedWeaponType; set { if (SetProperty(ref _selectedWeaponType, value)) Recalculate(); } }
    private WeaponType? _selectedWeaponType;

    public MagicColours? SelectedWeaponColour { get => _selectedWeaponColour; set { if (SetProperty(ref _selectedWeaponColour, value)) Recalculate(); } }
    private MagicColours? _selectedWeaponColour;

    public Alignments? SelectedWeaponAlignment { get => _selectedWeaponAlignment; set { if (SetProperty(ref _selectedWeaponAlignment, value)) Recalculate(); } }
    private Alignments? _selectedWeaponAlignment;

    public bool WeaponApprenticeStatus { get => _weaponApprenticeStatus; set { if (SetProperty(ref _weaponApprenticeStatus, value)) Recalculate(); } }
    private bool _weaponApprenticeStatus;

    public bool IsWeaponSelectionVisible => !string.IsNullOrWhiteSpace(SelectedWeaponKind);
    public bool IsWeaponColourVisible => IsKind("Pure Magic") || IsKind("Mantic");
    public bool IsWeaponAlignmentVisible => IsKind("Pure Spirit") || IsKind("Mantic");

    public string? SelectedShieldType
    {
        get => _selectedShieldType;
        set
        {
            if (SetProperty(ref _selectedShieldType, value))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    SelectedShieldColour = null;
                    SelectedShieldAlignment = null;
                }
                OnPropertyChanged(nameof(IsShieldColourVisible));
                OnPropertyChanged(nameof(IsShieldAlignmentVisible));
                Recalculate();
            }
        }
    }
    private string? _selectedShieldType;

    public MagicColours? SelectedShieldColour { get => _selectedShieldColour; set { if (SetProperty(ref _selectedShieldColour, value)) Recalculate(); } }
    private MagicColours? _selectedShieldColour;

    public Alignments? SelectedShieldAlignment { get => _selectedShieldAlignment; set { if (SetProperty(ref _selectedShieldAlignment, value)) Recalculate(); } }
    private Alignments? _selectedShieldAlignment;

    public bool IsShieldColourVisible => IsShieldKind("Magic");
    public bool IsShieldAlignmentVisible => IsShieldKind("Spirit");

    public string? SelectedBagType
    {
        get => _selectedBagType;
        set { if (SetProperty(ref _selectedBagType, value)) Recalculate(); }
    }
    private string? _selectedBagType;

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

    public int SpiritPowerStorePoints { get => _spiritPowerStorePoints; set { if (SetProperty(ref _spiritPowerStorePoints, Math.Clamp(value, 0, PowerStoreMax))) Recalculate(); } }
    private int _spiritPowerStorePoints;
    public SpiritualSpheres? SelectedSpiritSphere { get => _selectedSpiritSphere; set { if (SetProperty(ref _selectedSpiritSphere, value)) Recalculate(); } }
    private SpiritualSpheres? _selectedSpiritSphere;

    public int MagicPowerStorePoints { get => _magicPowerStorePoints; set { if (SetProperty(ref _magicPowerStorePoints, Math.Clamp(value, 0, PowerStoreMax))) Recalculate(); } }
    private int _magicPowerStorePoints;
    public MagicColours? SelectedMagicStoreColour { get => _selectedMagicStoreColour; set { if (SetProperty(ref _selectedMagicStoreColour, value)) Recalculate(); } }
    private MagicColours? _selectedMagicStoreColour;

    public int EarthpowerStorePoints { get => _earthpowerStorePoints; set { if (SetProperty(ref _earthpowerStorePoints, Math.Clamp(value, 0, PowerStoreMax))) Recalculate(); } }
    private int _earthpowerStorePoints;
    public EvocationFields? SelectedEarthpowerField { get => _selectedEarthpowerField; set { if (SetProperty(ref _selectedEarthpowerField, value)) Recalculate(); } }
    private EvocationFields? _selectedEarthpowerField;

    public bool ShieldApprenticeStatus { get => _shieldApprenticeStatus; set { if (SetProperty(ref _shieldApprenticeStatus, value)) Recalculate(); } }
    private bool _shieldApprenticeStatus;

    public IEnumerable<string> CpOptions => _cpBenefits.Select(c => c.Label);

    public string? SelectedCpOption
    {
        get => _selectedCpOption;
        set
        {
            if (SetProperty(ref _selectedCpOption, value))
            {
                var requiresLocation = _cpBenefits.FirstOrDefault(c => c.Label.Equals(value, StringComparison.Ordinal))?.RequiresLocation ?? false;
                IsCpLocationRequired = requiresLocation;

                if (!requiresLocation)
                {
                    SelectedCpLocation = null;
                    CpLocationText = null;
                }
                else
                {
                    ScrollToLocationSelectorAsync();
                }

                Recalculate();
            }
        }
    }
    private string? _selectedCpOption;

    public bool IsCpLocationRequired
    {
        get => _isCpLocationRequired;
        private set => SetProperty(ref _isCpLocationRequired, value);
    }
    private bool _isCpLocationRequired;

    public Dictionary<string, LocationOption> CpLocationOptions => _locationOptions;

    public LocationOption? SelectedCpLocation
    {
        get => _selectedCpLocation;
        set
        {
            if (SetProperty(ref _selectedCpLocation, value))
            {
                if (value.HasValue)
                    CpLocationText = value.Value.Name;
                Recalculate();
            }
        }
    }
    private LocationOption? _selectedCpLocation;

    public string? CpLocationText { get => _cpLocationText; set { if (SetProperty(ref _cpLocationText, value)) Recalculate(); } }
    private string? _cpLocationText;

    protected override Dictionary<string, int> LifeOptions
    {
        get
        {
            var options = base.LifeOptions;
            options["12/4"] = 120;
            options["15/5"] = 200;
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

    protected override void AddCustomContributions(List<ContributionRow> items, ref int running)
    {
        AddWeaponContribution(items, ref running);
        AddShieldContributions(items, ref running);
        AddPowerStores(items, ref running);
        AddCpContribution(items, ref running);
        if (Mac2Checked)
            AddContribution(items, ref running, "mac2", "+2 MAC", MacPlusTwoCost);
        if (Sac2Checked)
            AddContribution(items, ref running, "sac2", "+2 SAC", SacPlusTwoCost);
        AddStatusBagContribution(items, ref running);
    }

    private void AddWeaponContribution(List<ContributionRow> items, ref int running)
    {
        if (WeaponApprenticeStatus)
            AddContribution(items, ref running, "weapon-apprentice", "Weapon apprentice status", ApprenticeCost);

        if (string.IsNullOrWhiteSpace(SelectedWeaponKind) || !SelectedWeaponType.HasValue)
            return;

        var trimmed = TrimChipLabel(SelectedWeaponKind) ?? SelectedWeaponKind;
        var cost = GetWeaponCost(trimmed);
        if (cost <= 0)
            return;

        if (IsWeaponColourVisible && !SelectedWeaponColour.HasValue)
            return;
        if (IsWeaponAlignmentVisible && !SelectedWeaponAlignment.HasValue)
            return;

        var parts = new List<string> { FormatWeapon(SelectedWeaponType.Value) };
        if (IsWeaponColourVisible && SelectedWeaponColour.HasValue)
            parts.Add(FormatColour(SelectedWeaponColour.Value));
        if (IsWeaponAlignmentVisible && SelectedWeaponAlignment.HasValue)
            parts.Add(FormatAlignment(SelectedWeaponAlignment.Value));

        var detail = string.Join(", ", parts);
        AddContribution(items, ref running, "themeday-weapon", $"{trimmed} weapon ({detail})", cost);
    }

    private void AddShieldContributions(List<ContributionRow> items, ref int running)
    {
        if (ShieldApprenticeStatus)
            AddContribution(items, ref running, "shield-apprentice", "Shield apprentice status", ApprenticeCost);

        var trimmed = TrimChipLabel(SelectedShieldType) ?? SelectedShieldType;
        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        if (string.Equals(trimmed, "Magic", StringComparison.OrdinalIgnoreCase))
        {
            if (!SelectedShieldColour.HasValue)
                return;
            AddContribution(items, ref running, "magic-shield", $"Magic shield ({FormatColour(SelectedShieldColour.Value)})", MagicShieldCost);
        }
        else if (string.Equals(trimmed, "Spirit", StringComparison.OrdinalIgnoreCase))
        {
            if (!SelectedShieldAlignment.HasValue)
                return;
            AddContribution(items, ref running, "spirit-shield", $"Spirit shield ({FormatAlignment(SelectedShieldAlignment.Value)})", SpiritShieldCost);
        }
    }

    private void AddStatusBagContribution(List<ContributionRow> items, ref int running)
    {
        if (string.IsNullOrWhiteSpace(SelectedBagType))
            return;

        var trimmed = TrimChipLabel(SelectedBagType) ?? SelectedBagType;
        var unit = GetBagUnitCost(trimmed);
        if (unit <= 0)
            return;

        AddContribution(items, ref running, "status-bag", $"{trimmed} status bag", unit);
    }

    private void AddPowerStores(List<ContributionRow> items, ref int running)
    {
        if (SpiritPowerStorePoints > 0 && SelectedSpiritSphere.HasValue)
        {
            var cost = SpiritPowerStorePoints * PowerStoreUnitCost;
            var label = $"Spirit power store ({FormatEnum(SelectedSpiritSphere.Value)}) x{SpiritPowerStorePoints}";
            AddContribution(items, ref running, "spirit-store", label, cost);
        }

        if (MagicPowerStorePoints > 0 && SelectedMagicStoreColour.HasValue)
        {
            var cost = MagicPowerStorePoints * PowerStoreUnitCost;
            var label = $"Magic power store ({FormatColour(SelectedMagicStoreColour.Value)}) x{MagicPowerStorePoints}";
            AddContribution(items, ref running, "magic-store", label, cost);
        }

        if (EarthpowerStorePoints > 0 && SelectedEarthpowerField.HasValue)
        {
            var cost = EarthpowerStorePoints * PowerStoreUnitCost;
            var label = $"Earthpower store ({FormatEnum(SelectedEarthpowerField.Value)}) x{EarthpowerStorePoints}";
            AddContribution(items, ref running, "earthpower-store", label, cost);
        }
    }

    private void AddCpContribution(List<ContributionRow> items, ref int running)
    {
        if (string.IsNullOrWhiteSpace(SelectedCpOption))
            return;

        var selected = _cpBenefits.FirstOrDefault(c => c.Label.Equals(SelectedCpOption, StringComparison.Ordinal));
        if (selected == null)
            return;

        if (selected.RequiresLocation && string.IsNullOrWhiteSpace(GetLocationLabel()))
            return;

        var label = selected.Label;
        var location = GetLocationLabel();
        if (!string.IsNullOrWhiteSpace(location))
            label += $" ({location})";

        AddContribution(items, ref running, "cp-benefit", label, CpBenefitCost);
    }

    private static int GetWeaponCost(string? kind)
    {
        if (string.Equals(kind, "Pure Magic", StringComparison.OrdinalIgnoreCase))
            return PureMagicWeaponCost;
        if (string.Equals(kind, "Pure Spirit", StringComparison.OrdinalIgnoreCase))
            return PureSpiritWeaponCost;
        if (string.Equals(kind, "Mantic", StringComparison.OrdinalIgnoreCase))
            return ManticWeaponCost;
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

    private bool IsKind(string expected)
    {
        var trimmed = TrimChipLabel(SelectedWeaponKind) ?? SelectedWeaponKind;
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        return string.Equals(trimmed.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsShieldKind(string expected)
    {
        var trimmed = TrimChipLabel(SelectedShieldType) ?? SelectedShieldType;
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        return string.Equals(trimmed.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetLocationLabel()
    {
        if (SelectedCpLocation.HasValue && !string.IsNullOrWhiteSpace(SelectedCpLocation.Value.Name))
            return SelectedCpLocation.Value.Name;

        return string.IsNullOrWhiteSpace(CpLocationText) ? null : CpLocationText.Trim();
    }

    private Dictionary<string, LocationOption> BuildLocationOptions()
    {
        var locations = new List<(string Display, string Name)>
        {
            ("Deci - Northern province", "Deci"),
            ("Keys - Northern province", "Keys"),
            ("Eartholme - Northern province", "Eartholme"),
            ("Alguz - Northern province", "Alguz"),
            ("Bildteve - Western province", "Bildteve"),
            ("Halgar - Heartlands", "Halgar"),
            ("Mordred's Rest - Heartlands", "Mordred's Rest"),
            ("Gothiel - Heartlands", "Gothiel"),
            ("Scarlene - Eastern province", "Scarlene"),
            ("Port Miere - Eastern province", "Port Miere"),
            ("Sellaville - Southern province", "Sellaville"),
            ("Thimon - Southern province", "Thimon"),
            ("Rodanne - Southern province", "Rodanne"),
            ("Buloslavia - Baronies", "Buloslavia"),
            ("Shtoy - Baronies", "Shtoy"),
            ("Khaniabad - Ishma", "Khaniabad"),
            ("Galahbhad - Ishma", "Galahbhad"),
            ("Jade city - Amlas", "Jade city"),
            ("The Broken Lands - Western province (rural)", "The Broken Lands"),
            ("The Far North - Rural", "The Far North")
        };

        return locations
            .GroupBy(l => l.Display, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new LocationOption(g.First().Name), StringComparer.OrdinalIgnoreCase);
    }

    private void ScrollToLocationSelectorAsync()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(50);
            if (CpLocationSearch != null && ThemedayScroll != null)
                await ThemedayScroll.ScrollToAsync(CpLocationSearch, ScrollToPosition.Center, true);
        });
    }

    private static string FormatWeapon(WeaponType value) => EnumDisplayFormatter.Format(value);
    private static string FormatAlignment(Alignments value) => EnumDisplayFormatter.Format(value);
    private static string FormatColour(MagicColours value) => EnumDisplayFormatter.Format(value);
    private static string FormatEnum<T>(T value) where T : struct, Enum => EnumDisplayFormatter.Format(value);

    private sealed record CpBenefitOption(string Label, bool RequiresLocation);

    protected override DictionarySearchBar<SpellOption> SpellSearchControl => SpellSearch;
    protected override DictionarySearchBar<MiracleOption> MiracleSearchControl => MiracleSearch;
    protected override DictionarySearchBar<EvocationOption> EvocationSearchControl => EvocationSearch;
    protected override DictionarySlider LifeSliderControl => LifeSlider;
    protected override DictionarySearchBar<WeaponType> WeaponSearchControl => WeaponTypeSearch;
}
