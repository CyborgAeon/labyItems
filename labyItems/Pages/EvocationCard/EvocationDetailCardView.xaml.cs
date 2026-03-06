using System.Collections.ObjectModel;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.EvocationCard;

public partial class EvocationDetailCardView : ContentView
{
    private const int CollapsedLines = 3;
    private const int DescriptionChevronThresholdChars = 200;
    private const int VerbalChevronThresholdChars = 150;
    private const double ExpanderOverflowTolerance = 0.01;

    public static readonly BindableProperty EvocationProperty = BindableProperty.Create(
        nameof(Evocation),
        typeof(DruidEvocationService.EvocRaw),
        typeof(EvocationDetailCardView),
        default(DruidEvocationService.EvocRaw),
        propertyChanged: OnEvocationChanged);

    public DruidEvocationService.EvocRaw? Evocation
    {
        get => (DruidEvocationService.EvocRaw?)GetValue(EvocationProperty);
        set => SetValue(EvocationProperty, value);
    }

    public string EvocationName => ReadOrFallback(Evocation?.name, "Unnamed Evocation");
    public string PowerDisplayText => $"{Math.Max(0, Evocation?.power ?? 0)} EP";
    public string FieldSummaryText => BuildFieldSummary(Evocation);

    public string DescriptionText => ReadOrFallback(Evocation?.description, "No description provided.");
    public bool HasVerbal => !string.IsNullOrWhiteSpace((Evocation?.verbal ?? string.Empty).Trim());
    public string VerbalText => (Evocation?.verbal ?? string.Empty).Trim();
    public bool HasPrereqs => !string.IsNullOrWhiteSpace(PrereqText);
    public string PrereqText => BuildPrereqText(Evocation);
    public bool HasDamageSummary => !string.IsNullOrWhiteSpace(DamageSummaryText);
    public string DamageSummaryText => BuildDamageSummary(Evocation);
    public bool HasHealSummary => !string.IsNullOrWhiteSpace(HealSummaryText);
    public string HealSummaryText => BuildHealSummary(Evocation);

    public ObservableCollection<EvocationMetaChipVm> MetaChips { get; } = new();
    public bool HasMetaChips => MetaChips.Count > 0;

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

    private bool _isDescriptionAnimating;
    private bool _isVerbalAnimating;
    private bool _isPrereqAnimating;
    private bool _isDamageAnimating;
    private bool _isHealAnimating;
    private bool IsAnimatingExpand =>
        _isDescriptionAnimating || _isVerbalAnimating || _isPrereqAnimating || _isDamageAnimating || _isHealAnimating;

    public EvocationDetailCardView()
    {
        InitializeComponent();
        SizeChanged += (_, __) => ScheduleExpandabilityRefresh();
    }

    private static void OnEvocationChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not EvocationDetailCardView view)
            return;

        view.HandleEvocationChanged();
    }

    private void HandleEvocationChanged()
    {
        IsDescriptionExpanded = false;
        IsVerbalExpanded = false;
        IsPrereqExpanded = false;
        IsDamageExpanded = false;
        IsHealExpanded = false;

        RebuildMetaChips();
        RaiseComputedProperties();
        ScheduleExpandabilityRefresh();
    }

    private void RebuildMetaChips()
    {
        MetaChips.Clear();

        AddMetaChip("\uf256", "Gesture", Evocation?.verbal);
        AddMetaChip("\uf124", "Range", Evocation?.range);
        AddMetaChip("\uf017", "Duration", Evocation?.duration);

        if (Evocation?.fields != null)
        {
            foreach (var field in Evocation.fields
                         .Select(f => (f ?? string.Empty).Trim())
                         .Where(f => f.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                MetaChips.Add(new EvocationMetaChipVm("\uf02c", $"Field: {field}"));
            }
        }

        if (Evocation?.isAdvanced == true)
            MetaChips.Add(new EvocationMetaChipVm("\uf005", "Advanced"));

        OnPropertyChanged(nameof(HasMetaChips));
    }

    private void AddMetaChip(string iconGlyph, string label, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return;

        MetaChips.Add(new EvocationMetaChipVm(iconGlyph, $"{label}: {text}"));
    }

    private void RaiseComputedProperties()
    {
        OnPropertyChanged(nameof(EvocationName));
        OnPropertyChanged(nameof(PowerDisplayText));
        OnPropertyChanged(nameof(FieldSummaryText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(VerbalText));
        OnPropertyChanged(nameof(HasVerbal));
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
                "EvocationDescriptionExpand",
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
                "EvocationVerbalExpand",
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
                "EvocationPrereqExpand",
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
                "EvocationDamageExpand",
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
                "EvocationHealExpand",
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

        CanExpandVerbal = HasVerbal && (
            ExceedsCharacterThreshold(VerbalText, VerbalChevronThresholdChars)
            || ShouldShowExpander(VerbalLabel, VerbalText));

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

    private static string BuildFieldSummary(DruidEvocationService.EvocRaw? evocation)
    {
        if (evocation?.fields == null || evocation.fields.Count == 0)
            return "No fields";

        return string.Join(", ", evocation.fields
            .Select(f => (f ?? string.Empty).Trim())
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildPrereqText(DruidEvocationService.EvocRaw? evocation)
    {
        if (evocation?.preReqs == null || evocation.preReqs.Count == 0)
            return string.Empty;

        var cleaned = evocation.preReqs
            .Select(p => (p ?? string.Empty).Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cleaned.Count == 0 ? string.Empty : string.Join(", ", cleaned);
    }

    private static string BuildDamageSummary(DruidEvocationService.EvocRaw? evocation)
    {
        if (evocation == null)
            return string.Empty;

        var damage = evocation.GetDamageAmounts();
        var armours = evocation.GetArmourApplies();
        var armourType = evocation.GetArmourType();
        if (damage.Count == 0)
            return string.Empty;

        var types = evocation.GetDamageTypes();
        var parts = new List<string>();
        for (var i = 0; i < damage.Count; i++)
        {
            var dmg = damage[i];
            var tblp = At(dmg, 0);
            var loc = At(dmg, 1);
            var type = DamTypeParser.NormalizeOrFallback(OrLast(types, i), "Missile");
            var text = $"{type}: {tblp}/{loc}";

            if (!string.IsNullOrWhiteSpace(armourType))
            {
                var armourPair = OrDefault(armours, i, new[] { 0, 0 });
                var armTblp = At(armourPair, 0);
                var armLoc = At(armourPair, 1);
                if (armTblp > 0 || armLoc > 0)
                    text += $" [{armourType} {armTblp}/{armLoc}]";
            }

            parts.Add(text);
        }

        return string.Join("; ", parts);
    }

    private static string BuildHealSummary(DruidEvocationService.EvocRaw? evocation)
    {
        if (evocation == null)
            return string.Empty;

        var healing = evocation.GetHealAmounts();
        if (healing.Count == 0)
            return string.Empty;

        var types = evocation.GetHealTypes();
        var parts = new List<string>();
        for (var i = 0; i < healing.Count; i++)
        {
            var heal = healing[i];
            var tblp = At(heal, 0);
            var loc = At(heal, 1);
            var type = DamTypeParser.NormalizeOrFallback(OrLast(types, i), "Worst");
            parts.Add($"{type}: {tblp}/{loc}");
        }

        return string.Join("; ", parts);
    }

    private static string ReadOrFallback(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }

    private static int At(int[]? arr, int index)
        => (arr != null && index >= 0 && index < arr.Length) ? arr[index] : 0;

    private static T? OrLast<T>(IReadOnlyList<T> list, int index)
        => list.Count == 0 ? default : (index < list.Count ? list[index] : list[^1]);

    private static T OrDefault<T>(IReadOnlyList<T> list, int index, T fallback)
        => list.Count == 0 ? fallback : (index < list.Count ? list[index] : list[^1]);
}

public sealed class EvocationMetaChipVm
{
    public string IconGlyph { get; }
    public string Text { get; }

    public EvocationMetaChipVm(string iconGlyph, string text)
    {
        IconGlyph = iconGlyph;
        Text = text;
    }
}
