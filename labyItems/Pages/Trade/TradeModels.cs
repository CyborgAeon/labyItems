using System.Globalization;
using labyItems.Infrastructure;
using labyItems.Models;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Trade;

public enum TradeAbilityKind
{
    Spell,
    Miracle,
    Evocation,
    CustomItem
}

public enum TradeEntryEditAction
{
    Cancel,
    Save,
    Delete
}

public sealed record TradeEntryEditResult(
    TradeEntryEditAction Action,
    TradeEntryVm? Draft);

public sealed class TradeAbilityOption
{
    private TradeAbilityOption(
        TradeAbilityKind kind,
        string name,
        int power,
        bool isAdvanced,
        bool isNonStandard,
        SpellService.SpellRaw? spell,
        MiracleService.MiracRaw? miracle,
        DruidEvocationService.EvocRaw? evocation)
    {
        Kind = kind;
        Name = (name ?? string.Empty).Trim();
        Power = Math.Max(1, power);
        IsAdvanced = isAdvanced;
        IsNonStandard = isNonStandard;
        Spell = spell;
        Miracle = miracle;
        Evocation = evocation;
    }

    public TradeAbilityKind Kind { get; }
    public string Name { get; }
    public int Power { get; }
    public bool IsAdvanced { get; }
    public bool IsNonStandard { get; }

    public SpellService.SpellRaw? Spell { get; }
    public MiracleService.MiracRaw? Miracle { get; }
    public DruidEvocationService.EvocRaw? Evocation { get; }

    public string SearchLabel
    {
        get
        {
            var advanced = IsAdvanced ? "advanced" : "basic";
            var nonStandard = IsNonStandard ? "non-standard" : "standard";
            return Kind switch
            {
                TradeAbilityKind.Spell =>
                    $"{Name} (spell lvl {Power} · {advanced} · {nonStandard})",
                TradeAbilityKind.Miracle =>
                    $"{Name} (miracle P{Power} · {advanced} · {nonStandard})",
                TradeAbilityKind.Evocation =>
                    $"{Name} (evocation P{Power} · {advanced} · {nonStandard})",
                _ => Name
            };
        }
    }

    public static TradeAbilityOption FromSpell(SpellService.SpellRaw spell)
    {
        if (spell == null)
            return new TradeAbilityOption(TradeAbilityKind.Spell, string.Empty, 1, false, false, null, null, null);

        return new TradeAbilityOption(
            kind: TradeAbilityKind.Spell,
            name: spell.name,
            power: spell.level,
            isAdvanced: spell.isAdvanced == true || spell.IsAdvancedCompat == true,
            isNonStandard: spell.nonStandard || spell.NonStandardCompat == true,
            spell: spell,
            miracle: null,
            evocation: null);
    }

    public static TradeAbilityOption FromMiracle(MiracleService.MiracRaw miracle)
    {
        if (miracle == null)
            return new TradeAbilityOption(TradeAbilityKind.Miracle, string.Empty, 1, false, false, null, null, null);

        return new TradeAbilityOption(
            kind: TradeAbilityKind.Miracle,
            name: miracle.name,
            power: miracle.power,
            isAdvanced: miracle.isAdvanced,
            isNonStandard: miracle.nonStandard || miracle.NonStandardCompat == true,
            spell: null,
            miracle: miracle,
            evocation: null);
    }

    public static TradeAbilityOption FromEvocation(DruidEvocationService.EvocRaw evocation)
    {
        if (evocation == null)
            return new TradeAbilityOption(TradeAbilityKind.Evocation, string.Empty, 1, false, false, null, null, null);

        return new TradeAbilityOption(
            kind: TradeAbilityKind.Evocation,
            name: evocation.name,
            power: evocation.power,
            isAdvanced: evocation.isAdvanced,
            isNonStandard: evocation.nonStandard || evocation.NonStandardCompat == true,
            spell: null,
            miracle: null,
            evocation: evocation);
    }
}

public sealed class TradeEntryVm : ObservableObject
{
    private string _name = string.Empty;
    private TradeAbilityKind _kind;
    private int _power = 1;
    private bool _isAdvanced;
    private bool _isNonStandard;
    private int _usesPerDay = 1;
    private bool _isSpellTeachingScroll;
    private bool _isSpellScroll;
    private bool _isMiracleScripture;
    private bool _isMiracleScroll;
    private bool _isEvocationTalisman;
    private int? _costOverride;
    private int _customIspTotal;
    private string _customBreakdownText = string.Empty;
    private List<CalcResult> _customAbilities = new();

    public TradeEntryVm(TradeAbilityOption option)
    {
        SetAbility(option, preserveExistingMode: false);
    }

    private TradeEntryVm()
    {
        // Used by Clone.
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        private set
        {
            var next = (value ?? string.Empty).Trim();
            if (!SetProperty(ref _name, next))
                return;
            Raise(nameof(TitleLine));
        }
    }

    public TradeAbilityKind Kind
    {
        get => _kind;
        private set
        {
            if (!SetProperty(ref _kind, value))
                return;

            Raise(nameof(IsCustom));
            Raise(nameof(ShowAbilitySearch));
            Raise(nameof(ShowSpellModeOptions));
            Raise(nameof(ShowMiracleModeOptions));
            Raise(nameof(ShowEvocationModeOptions));
            Raise(nameof(ShowUsesEditor));
            Raise(nameof(ShowCustomEditor));
            Raise(nameof(TitleLine));
            Raise(nameof(MetaLine));
            Raise(nameof(CustomBreakdownText));
            Raise(nameof(HasCustomBreakdown));
            Raise(nameof(ShowCustomBreakdownOnCard));
            RaiseCostAndCardProperties();
        }
    }

    public int Power
    {
        get => _power;
        private set
        {
            var next = Math.Max(1, value);
            if (!SetProperty(ref _power, next))
                return;
            Raise(nameof(TitleLine));
            RaiseCostAndCardProperties();
        }
    }

    public bool IsAdvanced
    {
        get => _isAdvanced;
        private set
        {
            if (!SetProperty(ref _isAdvanced, value))
                return;
            Raise(nameof(MetaLine));
            RaiseCostAndCardProperties();
        }
    }

    public bool IsNonStandard
    {
        get => _isNonStandard;
        private set
        {
            if (!SetProperty(ref _isNonStandard, value))
                return;
            Raise(nameof(MetaLine));
            RaiseCostAndCardProperties();
        }
    }

    public int UsesPerDay
    {
        get => _usesPerDay;
        set
        {
            var next = Math.Max(1, value);
            if (!SetProperty(ref _usesPerDay, next))
                return;
            Raise(nameof(MetaLine));
            RaiseCostAndCardProperties();
        }
    }

    public bool IsSpellTeachingScroll
    {
        get => _isSpellTeachingScroll;
        set
        {
            if (!SetProperty(ref _isSpellTeachingScroll, value))
                return;
            NormalizeModes();
            RaiseModeVisibilityProperties();
            Raise(nameof(MetaLine));
            RaiseCostAndCardProperties();
        }
    }

    public bool IsSpellScroll
    {
        get => _isSpellScroll;
        set
        {
            if (!SetProperty(ref _isSpellScroll, value))
                return;
            NormalizeModes();
            RaiseModeVisibilityProperties();
            Raise(nameof(MetaLine));
            RaiseCostAndCardProperties();
        }
    }

    public bool IsMiracleScripture
    {
        get => _isMiracleScripture;
        set
        {
            if (!SetProperty(ref _isMiracleScripture, value))
                return;
            NormalizeModes();
            RaiseModeVisibilityProperties();
            Raise(nameof(MetaLine));
            RaiseCostAndCardProperties();
        }
    }

    public bool IsMiracleScroll
    {
        get => _isMiracleScroll;
        set
        {
            if (!SetProperty(ref _isMiracleScroll, value))
                return;
            NormalizeModes();
            RaiseModeVisibilityProperties();
            Raise(nameof(MetaLine));
            RaiseCostAndCardProperties();
        }
    }

    public bool IsEvocationTalisman
    {
        get => _isEvocationTalisman;
        set
        {
            if (!SetProperty(ref _isEvocationTalisman, value))
                return;
            NormalizeModes();
            RaiseModeVisibilityProperties();
            Raise(nameof(MetaLine));
            RaiseCostAndCardProperties();
        }
    }

    public int? CostOverride
    {
        get => _costOverride;
        set
        {
            int? next = value.HasValue ? Math.Max(0, value.Value) : null;
            if (!SetProperty(ref _costOverride, next))
                return;

            Raise(nameof(HasCostOverride));
            Raise(nameof(ResolvedCost));
            Raise(nameof(ResolvedCostText));
            Raise(nameof(OverrideDisplay));
            Raise(nameof(CardBorderBrush));
            Raise(nameof(CardBackgroundBrush));
            Raise(nameof(CardBorderThickness));
        }
    }

    public int CustomIspTotal
    {
        get => _customIspTotal;
        private set
        {
            var next = Math.Max(0, value);
            if (!SetProperty(ref _customIspTotal, next))
                return;
            RaiseCostAndCardProperties();
            Raise(nameof(MetaLine));
            Raise(nameof(CustomBreakdownText));
            Raise(nameof(HasCustomBreakdown));
            Raise(nameof(ShowCustomBreakdownOnCard));
        }
    }

    public IReadOnlyList<CalcResult> CustomAbilities => _customAbilities;

    public string CustomBreakdownText
    {
        get => _customBreakdownText;
        private set
        {
            var next = (value ?? string.Empty).Trim();
            if (!SetProperty(ref _customBreakdownText, next))
                return;
            Raise(nameof(HasCustomBreakdown));
            Raise(nameof(ShowCustomBreakdownOnCard));
        }
    }

    public bool IsCustom => Kind == TradeAbilityKind.CustomItem;
    public bool ShowAbilitySearch => !IsCustom;
    public bool ShowSpellModeOptions => Kind == TradeAbilityKind.Spell;
    public bool ShowMiracleModeOptions => Kind == TradeAbilityKind.Miracle;
    public bool ShowEvocationModeOptions => Kind == TradeAbilityKind.Evocation;
    public bool ShowCustomEditor => IsCustom;

    public bool ShowUsesEditor =>
        !IsCustom
        && !IsSpellTeachingScroll
        && !IsSpellScroll
        && !IsMiracleScripture
        && !IsMiracleScroll
        && !IsEvocationTalisman;

    public string TitleLine => Name;

    public string MetaLine
    {
        get
        {
            var typeText = Kind switch
            {
                TradeAbilityKind.Spell => "Spell",
                TradeAbilityKind.Miracle => "Miracle",
                TradeAbilityKind.Evocation => "Evocation",
                _ => "Custom item"
            };

            if (IsCustom)
                return $"{typeText} • ISP {CustomIspTotal}";

            var mode = ResolveModeText();
            var tier = IsNonStandard
                ? "non-standard"
                : (IsAdvanced ? "advanced" : "basic");
            return $"{typeText} • {tier} • {mode}";
        }
    }

    public int CalculatedCost => Kind switch
    {
        TradeAbilityKind.Spell => ResolveSpellCost(),
        TradeAbilityKind.Miracle => ResolveMiracleCost(),
        TradeAbilityKind.Evocation => ResolveEvocationCost(),
        TradeAbilityKind.CustomItem => ResolveCustomCost().Cost,
        _ => 0
    };

    public int ResolvedCost => CostOverride ?? CalculatedCost;
    public bool HasCostOverride => CostOverride.HasValue;

    public string ResolvedCostText => $"{ResolvedCost.ToString("N0", CultureInfo.InvariantCulture)} grulls";

    public string CostFormulaDisplay
    {
        get
        {
            if (Kind == TradeAbilityKind.CustomItem)
            {
                var customCost = ResolveCustomCost();
                return customCost.Formula.Length == 0
                    ? "At-cost formula unavailable."
                    : $"At-cost: {customCost.Cost.ToString("N0", CultureInfo.InvariantCulture)} ({customCost.Formula})";
            }

            var formula = Kind switch
            {
                TradeAbilityKind.Spell => ResolveSpellFormulaText(),
                TradeAbilityKind.Miracle => ResolveMiracleFormulaText(),
                TradeAbilityKind.Evocation => ResolveEvocationFormulaText(),
                _ => string.Empty
            };

            return formula.Length == 0
                ? "At-cost formula unavailable."
                : $"At-cost: {CalculatedCost.ToString("N0", CultureInfo.InvariantCulture)} ({formula})";
        }
    }

    private TradeCustomCostResolution ResolveCustomCost()
        => TradePublishedMakeCostResolver.Resolve(CustomAbilities, CustomIspTotal);

    public string OverrideDisplay => HasCostOverride
        ? $"Manual override: {ResolvedCost.ToString("N0", CultureInfo.InvariantCulture)} grulls"
        : string.Empty;

    public bool HasIspEstimateFallback => IsCustom && ResolveCustomCost().HasIspEstimatePortion;
    public bool ShowEstimateWarning => HasIspEstimateFallback;
    public string EstimateWarningText => ShowEstimateWarning ? "Estimate based on ISP value" : string.Empty;

    public bool HasCustomBreakdown => CustomBreakdownText.Length > 0;
    public bool ShowCustomBreakdownOnCard => IsCustom && HasCustomBreakdown;

    public Brush CardBorderBrush => new SolidColorBrush(
        HasCostOverride
            ? Color.FromArgb("#16A34A")
            : (HasIspEstimateFallback ? Color.FromArgb("#D97706") : Colors.Transparent));

    public Brush CardBackgroundBrush => new SolidColorBrush(
        HasCostOverride
            ? Color.FromArgb("#ECFDF3")
            : (HasIspEstimateFallback ? Color.FromArgb("#FFFBEB") : Colors.White));

    public double CardBorderThickness => (HasCostOverride || HasIspEstimateFallback) ? 3 : 0;

    public static TradeEntryVm CreateCustom(IspCalculationResult result)
    {
        var vm = new TradeEntryVm
        {
            Kind = TradeAbilityKind.CustomItem,
            Name = "Custom item",
            Power = 1,
            IsAdvanced = false,
            IsNonStandard = false,
            UsesPerDay = 1
        };
        vm.ApplyCustomIspResult(result);
        return vm;
    }

    public void SetAbility(TradeAbilityOption option, bool preserveExistingMode)
    {
        if (option == null)
            return;

        var previousKind = Kind;
        Kind = option.Kind;
        Name = option.Name;
        Power = option.Power;
        IsAdvanced = option.IsAdvanced;
        IsNonStandard = option.IsNonStandard;

        var shouldResetModes = !preserveExistingMode || previousKind != option.Kind;
        if (shouldResetModes)
        {
            _isSpellTeachingScroll = false;
            _isSpellScroll = false;
            _isMiracleScripture = false;
            _isMiracleScroll = false;
            _isEvocationTalisman = false;
            _usesPerDay = 1;

            Raise(nameof(IsSpellTeachingScroll));
            Raise(nameof(IsSpellScroll));
            Raise(nameof(IsMiracleScripture));
            Raise(nameof(IsMiracleScroll));
            Raise(nameof(IsEvocationTalisman));
            Raise(nameof(UsesPerDay));
        }

        if (!IsCustom)
        {
            CustomIspTotal = 0;
            CustomBreakdownText = string.Empty;
            _customAbilities = new List<CalcResult>();
            Raise(nameof(CustomAbilities));
        }

        NormalizeModes();
        RaiseModeVisibilityProperties();
        Raise(nameof(MetaLine));
        RaiseCostAndCardProperties();
    }

    public void ApplyCustomIspResult(IspCalculationResult result)
    {
        var payload = result ?? new IspCalculationResult();
        Kind = TradeAbilityKind.CustomItem;
        Name = "Custom item";
        Power = 1;
        IsAdvanced = false;
        IsNonStandard = false;

        _isSpellTeachingScroll = false;
        _isSpellScroll = false;
        _isMiracleScripture = false;
        _isMiracleScroll = false;
        _isEvocationTalisman = false;
        UsesPerDay = 1;

        CustomIspTotal = Math.Max(0, payload.TotalIsp);
        _customAbilities = (payload.Abilities ?? new List<CalcResult>())
            .Where(ability => ability != null)
            .ToList();
        Raise(nameof(CustomAbilities));

        var breakdownLines = BuildCustomBreakdownLines(payload);
        CustomBreakdownText = string.Join("\n", breakdownLines);

        Raise(nameof(IsSpellTeachingScroll));
        Raise(nameof(IsSpellScroll));
        Raise(nameof(IsMiracleScripture));
        Raise(nameof(IsMiracleScroll));
        Raise(nameof(IsEvocationTalisman));
        RaiseModeVisibilityProperties();
        Raise(nameof(MetaLine));
        RaiseCostAndCardProperties();
    }

    public bool MatchesAbility(TradeAbilityOption option)
    {
        if (option == null || IsCustom)
            return false;

        return Kind == option.Kind
               && Power == option.Power
               && IsAdvanced == option.IsAdvanced
               && Name.Equals(option.Name, StringComparison.OrdinalIgnoreCase);
    }

    public TradeEntryVm Clone()
    {
        var clone = new TradeEntryVm();
        clone.ApplyFrom(this);
        return clone;
    }

    public void ApplyFrom(TradeEntryVm source)
    {
        if (source == null)
            return;

        _name = source.Name;
        _kind = source.Kind;
        _power = source.Power;
        _isAdvanced = source.IsAdvanced;
        _isNonStandard = source.IsNonStandard;
        _usesPerDay = source.UsesPerDay;
        _isSpellTeachingScroll = source.IsSpellTeachingScroll;
        _isSpellScroll = source.IsSpellScroll;
        _isMiracleScripture = source.IsMiracleScripture;
        _isMiracleScroll = source.IsMiracleScroll;
        _isEvocationTalisman = source.IsEvocationTalisman;
        _costOverride = source.CostOverride;
        _customIspTotal = source.CustomIspTotal;
        _customBreakdownText = source.CustomBreakdownText;
        _customAbilities = source.CustomAbilities.ToList();

        Raise(nameof(Name));
        Raise(nameof(Kind));
        Raise(nameof(Power));
        Raise(nameof(IsAdvanced));
        Raise(nameof(IsNonStandard));
        Raise(nameof(UsesPerDay));
        Raise(nameof(IsSpellTeachingScroll));
        Raise(nameof(IsSpellScroll));
        Raise(nameof(IsMiracleScripture));
        Raise(nameof(IsMiracleScroll));
        Raise(nameof(IsEvocationTalisman));
        Raise(nameof(CostOverride));
        Raise(nameof(CustomIspTotal));
        Raise(nameof(CustomAbilities));
        Raise(nameof(CustomBreakdownText));
        Raise(nameof(IsCustom));
        Raise(nameof(ShowAbilitySearch));
        Raise(nameof(ShowSpellModeOptions));
        Raise(nameof(ShowMiracleModeOptions));
        Raise(nameof(ShowEvocationModeOptions));
        Raise(nameof(ShowUsesEditor));
        Raise(nameof(ShowCustomEditor));
        Raise(nameof(TitleLine));
        Raise(nameof(MetaLine));
        RaiseCostAndCardProperties();
    }

    private void NormalizeModes()
    {
        var raiseSpellTeaching = false;
        var raiseSpellScroll = false;
        var raiseMiracleScripture = false;
        var raiseMiracleScroll = false;
        var raiseEvocationTalisman = false;
        var raiseUses = false;

        if (Kind != TradeAbilityKind.Spell)
        {
            if (_isSpellTeachingScroll)
            {
                _isSpellTeachingScroll = false;
                raiseSpellTeaching = true;
            }

            if (_isSpellScroll)
            {
                _isSpellScroll = false;
                raiseSpellScroll = true;
            }
        }
        else
        {
            if (_isSpellTeachingScroll && _isSpellScroll)
            {
                _isSpellScroll = false;
                raiseSpellScroll = true;
            }
        }

        if (Kind != TradeAbilityKind.Miracle)
        {
            if (_isMiracleScripture)
            {
                _isMiracleScripture = false;
                raiseMiracleScripture = true;
            }

            if (_isMiracleScroll)
            {
                _isMiracleScroll = false;
                raiseMiracleScroll = true;
            }
        }
        else
        {
            if (_isMiracleScripture && _isMiracleScroll)
            {
                _isMiracleScroll = false;
                raiseMiracleScroll = true;
            }
        }

        if (Kind != TradeAbilityKind.Evocation && _isEvocationTalisman)
        {
            _isEvocationTalisman = false;
            raiseEvocationTalisman = true;
        }

        var specialModeEnabled =
            _isSpellTeachingScroll
            || _isSpellScroll
            || _isMiracleScripture
            || _isMiracleScroll
            || _isEvocationTalisman;

        if (specialModeEnabled && _usesPerDay != 1)
        {
            _usesPerDay = 1;
            raiseUses = true;
        }

        if (raiseSpellTeaching)
            Raise(nameof(IsSpellTeachingScroll));
        if (raiseSpellScroll)
            Raise(nameof(IsSpellScroll));
        if (raiseMiracleScripture)
            Raise(nameof(IsMiracleScripture));
        if (raiseMiracleScroll)
            Raise(nameof(IsMiracleScroll));
        if (raiseEvocationTalisman)
            Raise(nameof(IsEvocationTalisman));
        if (raiseUses)
            Raise(nameof(UsesPerDay));
    }

    private void RaiseModeVisibilityProperties()
    {
        Raise(nameof(ShowSpellModeOptions));
        Raise(nameof(ShowMiracleModeOptions));
        Raise(nameof(ShowEvocationModeOptions));
        Raise(nameof(ShowUsesEditor));
        Raise(nameof(ShowCustomEditor));
    }

    private void RaiseCostAndCardProperties()
    {
        Raise(nameof(CalculatedCost));
        Raise(nameof(ResolvedCost));
        Raise(nameof(ResolvedCostText));
        Raise(nameof(CostFormulaDisplay));
        Raise(nameof(OverrideDisplay));
        Raise(nameof(HasCostOverride));
        Raise(nameof(HasIspEstimateFallback));
        Raise(nameof(ShowEstimateWarning));
        Raise(nameof(EstimateWarningText));
        Raise(nameof(CardBorderBrush));
        Raise(nameof(CardBackgroundBrush));
        Raise(nameof(CardBorderThickness));
    }

    private string ResolveModeText()
    {
        if (IsCustom)
            return "custom item";

        if (IsSpellTeachingScroll)
            return "teaching scroll";
        if (IsSpellScroll || IsMiracleScroll)
            return "scroll";
        if (IsMiracleScripture)
            return "scripture of faith";
        if (IsEvocationTalisman)
            return "talisman";

        return $"uses {UsesPerDay}/day";
    }

    private int ResolveSpellCost()
    {
        if (IsSpellScroll)
            return 300 * Math.Max(1, Power);

        if (IsSpellTeachingScroll)
        {
            var rollCost = ResolveTeachingScrollRollCost();
            var rolls = Math.Max(1, Power);
            return rollCost + (rollCost * rolls);
        }

        var scaledPower = Math.Max(1, Power) * Math.Max(1, UsesPerDay);
        return 2000 + (2000 * scaledPower);
    }

    private int ResolveMiracleCost()
    {
        if (IsMiracleScroll)
            return 300 * Math.Max(1, Power);

        if (IsMiracleScripture)
        {
            var rollCost = ResolveScriptureRollCost();
            var rolls = Math.Max(1, Power);
            return rollCost + (rollCost * rolls);
        }

        var scaledPower = Math.Max(1, Power) * Math.Max(1, UsesPerDay);
        return 3000 + (3000 * scaledPower);
    }

    private int ResolveEvocationCost()
    {
        if (IsEvocationTalisman)
            return 250 * Math.Max(1, Power);

        var scaledPower = Math.Max(1, Power) * Math.Max(1, UsesPerDay);
        return 2000 + (2000 * scaledPower);
    }

    private int ResolveTeachingScrollRollCost()
    {
        if (IsNonStandard)
            return 1500;
        if (IsAdvanced)
            return 1000;
        return 100;
    }

    private int ResolveScriptureRollCost()
    {
        if (IsNonStandard)
            return 2000;
        if (IsAdvanced)
            return 1500;
        return 500;
    }

    private string ResolveSpellFormulaText()
    {
        if (IsSpellScroll)
            return $"300 × lvl {Power}";

        if (IsSpellTeachingScroll)
        {
            var rollCost = ResolveTeachingScrollRollCost();
            return $"{rollCost} + ({rollCost} × lvl {Power})";
        }

        return $"2000 + (2000 × lvl {Power} × uses {UsesPerDay})";
    }

    private string ResolveMiracleFormulaText()
    {
        if (IsMiracleScroll)
            return $"300 × P{Power}";

        if (IsMiracleScripture)
        {
            var rollCost = ResolveScriptureRollCost();
            return $"{rollCost} + ({rollCost} × P{Power})";
        }

        return $"3000 + (3000 × P{Power} × uses {UsesPerDay})";
    }

    private string ResolveEvocationFormulaText()
    {
        if (IsEvocationTalisman)
            return $"250 × P{Power}";

        return $"2000 + (2000 × P{Power} × uses {UsesPerDay})";
    }

    private static List<string> BuildCustomBreakdownLines(IspCalculationResult result)
    {
        var lines = new List<string>();
        lines.Add($"ISP total: {Math.Max(0, result?.TotalIsp ?? 0)}");

        var fromSummary = (result?.SummaryText ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (fromSummary.Count > 0)
        {
            foreach (var line in fromSummary)
            {
                if (line.StartsWith("Total ISP", StringComparison.OrdinalIgnoreCase))
                    continue;

                lines.Add(line);
            }

            return lines;
        }

        foreach (var ability in result?.Abilities ?? new List<CalcResult>())
        {
            var summary = (ability?.Summary ?? string.Empty).Trim();
            if (summary.Length > 0)
                lines.Add(summary);
        }

        return lines;
    }
}
