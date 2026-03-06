using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Services;

namespace labyItems.Pages.MiracleCard;

public partial class MiracleDetailCardView : ContentView
{
    private const int CollapsedLines = 3;
    private const int DescriptionChevronThresholdChars = 200;
    private const int VerbalChevronThresholdChars = 150;
    private const double ExpanderOverflowTolerance = 0.01;

    public static readonly BindableProperty MiracleProperty = BindableProperty.Create(
        nameof(Miracle),
        typeof(MiracleService.MiracRaw),
        typeof(MiracleDetailCardView),
        default(MiracleService.MiracRaw),
        propertyChanged: OnMiracleChanged);

    public MiracleService.MiracRaw? Miracle
    {
        get => (MiracleService.MiracRaw?)GetValue(MiracleProperty);
        set => SetValue(MiracleProperty, value);
    }

    private bool _isDescriptionExpanded;
    public bool IsDescriptionExpanded
    {
        get => _isDescriptionExpanded;
        set
        {
            if (_isDescriptionExpanded == value) return;
            _isDescriptionExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DescriptionMaxLines));
            OnPropertyChanged(nameof(DescriptionChevronText));
        }
    }

    private bool _isVerbalExpanded;
    public bool IsVerbalExpanded
    {
        get => _isVerbalExpanded;
        set
        {
            if (_isVerbalExpanded == value) return;
            _isVerbalExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VerbalMaxLines));
            OnPropertyChanged(nameof(VerbalChevronText));
        }
    }

    private bool _isPrereqExpanded;
    public bool IsPrereqExpanded
    {
        get => _isPrereqExpanded;
        set
        {
            if (_isPrereqExpanded == value) return;
            _isPrereqExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PrereqMaxLines));
            OnPropertyChanged(nameof(PrereqChevronText));
        }
    }

    private bool _isDamageExpanded;
    public bool IsDamageExpanded
    {
        get => _isDamageExpanded;
        set
        {
            if (_isDamageExpanded == value) return;
            _isDamageExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DamageMaxLines));
            OnPropertyChanged(nameof(DamageChevronText));
        }
    }

    private bool _isHealExpanded;
    public bool IsHealExpanded
    {
        get => _isHealExpanded;
        set
        {
            if (_isHealExpanded == value) return;
            _isHealExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HealMaxLines));
            OnPropertyChanged(nameof(HealChevronText));
        }
    }

    private bool _canExpandDescription;
    public bool CanExpandDescription
    {
        get => _canExpandDescription;
        private set
        {
            if (_canExpandDescription == value) return;
            _canExpandDescription = value;
            OnPropertyChanged();
        }
    }

    private bool _canExpandVerbal;
    public bool CanExpandVerbal
    {
        get => _canExpandVerbal;
        private set
        {
            if (_canExpandVerbal == value) return;
            _canExpandVerbal = value;
            OnPropertyChanged();
        }
    }

    private bool _canExpandPrereqs;
    public bool CanExpandPrereqs
    {
        get => _canExpandPrereqs;
        private set
        {
            if (_canExpandPrereqs == value) return;
            _canExpandPrereqs = value;
            OnPropertyChanged();
        }
    }

    private bool _canExpandDamage;
    public bool CanExpandDamage
    {
        get => _canExpandDamage;
        private set
        {
            if (_canExpandDamage == value) return;
            _canExpandDamage = value;
            OnPropertyChanged();
        }
    }

    private bool _canExpandHeal;
    public bool CanExpandHeal
    {
        get => _canExpandHeal;
        private set
        {
            if (_canExpandHeal == value) return;
            _canExpandHeal = value;
            OnPropertyChanged();
        }
    }

    public int DescriptionMaxLines => IsDescriptionExpanded ? -1 : CollapsedLines;
    public int VerbalMaxLines => IsVerbalExpanded ? -1 : CollapsedLines;
    public int PrereqMaxLines => IsPrereqExpanded ? -1 : CollapsedLines;
    public int DamageMaxLines => IsDamageExpanded ? -1 : CollapsedLines;
    public int HealMaxLines => IsHealExpanded ? -1 : CollapsedLines;

    public string DescriptionChevronText => IsDescriptionExpanded ? "▴" : "▾";
    public string VerbalChevronText => IsVerbalExpanded ? "▴" : "▾";
    public string PrereqChevronText => IsPrereqExpanded ? "▴" : "▾";
    public string DamageChevronText => IsDamageExpanded ? "▴" : "▾";
    public string HealChevronText => IsHealExpanded ? "▴" : "▾";

    public string MiracleName => ReadOrFallback(Miracle?.name, "Unnamed Miracle");
    public string SphereDisplayText => BuildSphereDisplay(Miracle?.sphere);
    public string SphereIconGlyph => FontAwesomeGlyphs.GetSphereIcon(SphereDisplayText);
    public string PowerDisplayText => $"P{Math.Max(0, Miracle?.power ?? 0)}";
    public string AlignmentDisplayText => BuildAlignmentDisplay(Miracle?.alignment);
    public bool HasAlignment => AlignmentDisplayText.Length > 0;

    public string DescriptionText => ReadOrFallback(Miracle?.description, "No description provided.");
    public string VerbalText => ReadOrFallback(Miracle?.verbal, "No verbal provided.");
    public string PrereqText => BuildPrereqText(Miracle);
    public bool HasPrereqs => PrereqText.Length > 0;
    public string DamageSummaryText => BuildDamageSummary(Miracle);
    public bool HasDamageSummary => DamageSummaryText.Length > 0;
    public string HealSummaryText => BuildHealSummary(Miracle);
    public bool HasHealSummary => HealSummaryText.Length > 0;

    private bool _isDescriptionAnimating;
    private bool _isVerbalAnimating;
    private bool _isPrereqAnimating;
    private bool _isDamageAnimating;
    private bool _isHealAnimating;
    private bool IsAnimatingExpand => _isDescriptionAnimating || _isVerbalAnimating || _isPrereqAnimating || _isDamageAnimating || _isHealAnimating;

    public MiracleDetailCardView()
    {
        InitializeComponent();
        SizeChanged += (_, __) => ScheduleExpandabilityRefresh();
    }

    private static void OnMiracleChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not MiracleDetailCardView view)
            return;

        view.HandleMiracleChanged();
    }

    private void HandleMiracleChanged()
    {
        IsDescriptionExpanded = false;
        IsVerbalExpanded = false;
        IsPrereqExpanded = false;
        IsDamageExpanded = false;
        IsHealExpanded = false;

        RaiseComputedProperties();
        ScheduleExpandabilityRefresh();
    }

    private void RaiseComputedProperties()
    {
        OnPropertyChanged(nameof(MiracleName));
        OnPropertyChanged(nameof(SphereDisplayText));
        OnPropertyChanged(nameof(SphereIconGlyph));
        OnPropertyChanged(nameof(PowerDisplayText));
        OnPropertyChanged(nameof(AlignmentDisplayText));
        OnPropertyChanged(nameof(HasAlignment));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(VerbalText));
        OnPropertyChanged(nameof(PrereqText));
        OnPropertyChanged(nameof(HasPrereqs));
        OnPropertyChanged(nameof(DamageSummaryText));
        OnPropertyChanged(nameof(HasDamageSummary));
        OnPropertyChanged(nameof(HealSummaryText));
        OnPropertyChanged(nameof(HasHealSummary));
        OnPropertyChanged(nameof(DescriptionChevronText));
        OnPropertyChanged(nameof(VerbalChevronText));
        OnPropertyChanged(nameof(PrereqChevronText));
        OnPropertyChanged(nameof(DamageChevronText));
        OnPropertyChanged(nameof(HealChevronText));
    }

    private async void OnDescriptionToggleClicked(object sender, EventArgs e)
    {
        if (_isDescriptionAnimating)
            return;

        _isDescriptionAnimating = true;
        try
        {
            await AnimateSectionToggleAsync(
                DescriptionTextContainer,
                DescriptionLabel,
                DescriptionText,
                "MiracleDescriptionExpand",
                () =>
                {
                    IsDescriptionExpanded = !IsDescriptionExpanded;
                    return IsDescriptionExpanded;
                });
        }
        finally
        {
            _isDescriptionAnimating = false;
        }
    }

    private async void OnVerbalToggleClicked(object sender, EventArgs e)
    {
        if (_isVerbalAnimating)
            return;

        _isVerbalAnimating = true;
        try
        {
            await AnimateSectionToggleAsync(
                VerbalTextContainer,
                VerbalLabel,
                VerbalText,
                "MiracleVerbalExpand",
                () =>
                {
                    IsVerbalExpanded = !IsVerbalExpanded;
                    return IsVerbalExpanded;
                });
        }
        finally
        {
            _isVerbalAnimating = false;
        }
    }

    private async void OnPrereqToggleClicked(object sender, EventArgs e)
    {
        if (_isPrereqAnimating)
            return;

        _isPrereqAnimating = true;
        try
        {
            await AnimateSectionToggleAsync(
                PrereqTextContainer,
                PrereqLabel,
                PrereqText,
                "MiraclePrereqExpand",
                () =>
                {
                    IsPrereqExpanded = !IsPrereqExpanded;
                    return IsPrereqExpanded;
                });
        }
        finally
        {
            _isPrereqAnimating = false;
        }
    }

    private async void OnDamageToggleClicked(object sender, EventArgs e)
    {
        if (_isDamageAnimating)
            return;

        _isDamageAnimating = true;
        try
        {
            await AnimateSectionToggleAsync(
                DamageTextContainer,
                DamageLabel,
                DamageSummaryText,
                "MiracleDamageExpand",
                () =>
                {
                    IsDamageExpanded = !IsDamageExpanded;
                    return IsDamageExpanded;
                });
        }
        finally
        {
            _isDamageAnimating = false;
        }
    }

    private async void OnHealToggleClicked(object sender, EventArgs e)
    {
        if (_isHealAnimating)
            return;

        _isHealAnimating = true;
        try
        {
            await AnimateSectionToggleAsync(
                HealTextContainer,
                HealLabel,
                HealSummaryText,
                "MiracleHealExpand",
                () =>
                {
                    IsHealExpanded = !IsHealExpanded;
                    return IsHealExpanded;
                });
        }
        finally
        {
            _isHealAnimating = false;
        }
    }

    private void OnExpandableLabelSizeChanged(object sender, EventArgs e)
    {
        ScheduleExpandabilityRefresh();
    }

    private void ScheduleExpandabilityRefresh()
    {
        Dispatcher.Dispatch(RefreshExpandability);
    }

    private void RefreshExpandability()
    {
        CanExpandDescription =
            ExceedsCharacterThreshold(DescriptionText, DescriptionChevronThresholdChars)
            || ShouldShowExpander(DescriptionLabel, DescriptionText);

        CanExpandVerbal =
            ExceedsCharacterThreshold(VerbalText, VerbalChevronThresholdChars)
            || ShouldShowExpander(VerbalLabel, VerbalText);

        CanExpandPrereqs = HasPrereqs && ShouldShowExpander(PrereqLabel, PrereqText);
        CanExpandDamage = HasDamageSummary && ShouldShowExpander(DamageLabel, DamageSummaryText);
        CanExpandHeal = HasHealSummary && ShouldShowExpander(HealLabel, HealSummaryText);

        if (IsAnimatingExpand)
            return;
    }

    private static bool ShouldShowExpander(Label label, string text)
    {
        if (label == null || string.IsNullOrWhiteSpace(text))
            return false;

        var width = ResolveMeasureWidth(label);
        if (width <= 0)
            return false;

        var fullHeight = MeasureHeight(label, text, width, maxLines: -1);
        var collapsedHeight = MeasureHeight(label, text, width, maxLines: CollapsedLines);
        return fullHeight > (collapsedHeight + ExpanderOverflowTolerance);
    }

    private static bool ExceedsCharacterThreshold(string text, int threshold)
        => (text ?? string.Empty).Trim().Length > threshold;

    private static double MeasureHeight(Label template, string text, double width, int maxLines)
    {
        var probe = new Label
        {
            Text = text,
            FontFamily = template.FontFamily,
            FontSize = template.FontSize,
            FontAttributes = template.FontAttributes,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = maxLines
        };

        return probe.Measure(width, double.PositiveInfinity).Height;
    }

    private static double ResolveMeasureWidth(Label label)
    {
        if (label.Parent is VisualElement parent)
            return CardExpandAnimationHelper.ResolveMeasureWidth(label, parent);

        return CardExpandAnimationHelper.ResolveMeasureWidth(label);
    }

    private async Task AnimateSectionToggleAsync(
        ContentView container,
        Label label,
        string text,
        string animationName,
        Func<bool> toggleAndGetExpandedState)
    {
        var width = ResolveMeasureWidth(label);
        if (width <= 0)
        {
            toggleAndGetExpandedState();
            ScheduleExpandabilityRefresh();
            return;
        }

        var beforeExpanded = label.MaxLines < 0;
        var beforeHeight = MeasureHeight(label, text, width, beforeExpanded ? -1 : CollapsedLines);
        container.HeightRequest = beforeHeight;
        var afterExpanded = toggleAndGetExpandedState();
        var afterHeight = MeasureHeight(label, text, width, afterExpanded ? -1 : CollapsedLines);

        if (Math.Abs(afterHeight - beforeHeight) < ExpanderOverflowTolerance)
        {
            container.HeightRequest = -1;
            ScheduleExpandabilityRefresh();
            return;
        }

        await CardExpandAnimationHelper.AnimateHeightAsync(
            owner: container,
            target: container,
            animationName: animationName,
            from: beforeHeight,
            to: afterHeight,
            length: 180,
            easing: Easing.CubicInOut);
        container.HeightRequest = -1;
        ScheduleExpandabilityRefresh();
    }

    private static string BuildSphereDisplay(string? rawSphere)
    {
        var token = (rawSphere ?? string.Empty).Trim();
        if (token.Length == 0)
            return "Unknown";

        var cleaned = StripMajorMinorPrefix(token);
        if (cleaned.Length == 0)
            return "Unknown";

        var withSpaces = Regex.Replace(cleaned, "([a-z])([A-Z])", "$1 $2");
        return withSpaces.Trim();
    }

    private static string BuildAlignmentDisplay(string? rawAlignment)
    {
        var token = (rawAlignment ?? string.Empty).Trim();
        if (token.Length == 0)
            return string.Empty;

        return token;
    }

    private static string BuildPrereqText(MiracleService.MiracRaw? miracle)
    {
        if (miracle?.preReqs == null || miracle.preReqs.Count == 0)
            return string.Empty;

        var cleaned = miracle.preReqs
            .Select(p => (p ?? string.Empty).Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cleaned.Count == 0 ? string.Empty : string.Join(", ", cleaned);
    }

    private static string BuildDamageSummary(MiracleService.MiracRaw? miracle)
    {
        if (miracle == null)
            return string.Empty;

        var damage = miracle.GetDamageAmounts();
        var types = miracle.GetDamageTypes();
        var armour = miracle.GetArmourApplies();
        var armourType = miracle.GetArmourType();
        var overrideText = FormatOverride(miracle.damageOverride);

        if (damage.Count == 0)
            return overrideText;

        var parts = new List<string>();
        for (var i = 0; i < damage.Count; i++)
        {
            var dmg = damage[i];
            var tblp = At(dmg, 0);
            var loc = At(dmg, 1);
            var type = ReadOrFallback(OrLast(types, i), "Missile");

            var text = $"{type}: {tblp}/{loc}";
            if (!string.IsNullOrWhiteSpace(armourType))
            {
                var armPair = OrDefault(armour, i, new[] { 0, 0 });
                var armTblp = At(armPair, 0);
                var armLoc = At(armPair, 1);
                if (armTblp > 0 || armLoc > 0)
                    text += $" [{armourType} {armTblp}/{armLoc}]";
            }

            parts.Add(text);
        }

        var summary = string.Join("; ", parts);
        if (overrideText.Length == 0)
            return summary;

        return summary.Length == 0 ? overrideText : $"{summary}; {overrideText}";
    }

    private static string BuildHealSummary(MiracleService.MiracRaw? miracle)
    {
        if (miracle == null)
            return string.Empty;

        var healing = miracle.GetHealAmounts();
        if (healing.Count == 0)
            return string.Empty;

        var types = miracle.GetHealTypes();
        var parts = new List<string>();
        for (var i = 0; i < healing.Count; i++)
        {
            var heal = healing[i];
            var tblp = At(heal, 0);
            var loc = At(heal, 1);
            var type = ReadOrFallback(OrLast(types, i), "Missile");
            parts.Add($"{type}: {tblp}/{loc}");
        }

        return string.Join("; ", parts);
    }

    private static string FormatOverride(string? raw)
    {
        var token = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return token switch
        {
            "" => string.Empty,
            "sever" => "Sever",
            "loc0" => "Loc 0",
            "soullance" => "Soul lance",
            _ => raw?.Trim() ?? string.Empty
        };
    }

    private static string ReadOrFallback(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }

    private static string StripMajorMinorPrefix(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var token = raw.Trim();
        if (token.StartsWith("Major ", StringComparison.OrdinalIgnoreCase))
            token = token[6..];
        if (token.StartsWith("Minor ", StringComparison.OrdinalIgnoreCase))
            token = token[6..];
        return token.Trim();
    }

    private static int At(int[]? arr, int index)
        => (arr != null && index >= 0 && index < arr.Length) ? arr[index] : 0;

    private static T? OrLast<T>(IReadOnlyList<T> list, int index)
        => list.Count == 0 ? default : (index < list.Count ? list[index] : list[^1]);

    private static T OrDefault<T>(IReadOnlyList<T> list, int index, T fallback)
        => list.Count == 0 ? fallback : (index < list.Count ? list[index] : list[^1]);
}
