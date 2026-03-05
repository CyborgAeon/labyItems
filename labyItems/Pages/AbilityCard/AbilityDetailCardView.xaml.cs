using System.Text.Json;
using labyItems.Helpers;
using labyItems.Services;

namespace labyItems.Pages.AbilityCard;

public partial class AbilityDetailCardView : ContentView
{
    private const int DescriptionCollapsedLines = 5;
    private const double ExpanderOverflowTolerance = 0.01;
    private const string InfinityGlyphCode = "\uf534";

    public static readonly BindableProperty AbilityProperty = BindableProperty.Create(
        nameof(Ability),
        typeof(EvolutionService.AbilityResult),
        typeof(AbilityDetailCardView),
        default(EvolutionService.AbilityResult),
        propertyChanged: OnAbilityChanged);

    public EvolutionService.AbilityResult? Ability
    {
        get => (EvolutionService.AbilityResult?)GetValue(AbilityProperty);
        set => SetValue(AbilityProperty, value);
    }

    public string AbilityIndex => ReadOrFallback(Ability?.Index, "Unnamed Ability");
    public string TableDisplayText => $"Table {Math.Max(0, Ability?.Table ?? 0)}";
    public string AvailabilityText => BuildAvailabilityDisplay(Ability?.Available);

    public bool ShowInfiniteCost => Ability?.CanBuyMultiple == true && Ability?.MaxAvailable is not > 0;
    public bool ShowCostText => !ShowInfiniteCost;
    public string CostDisplayText
    {
        get
        {
            var cost = Math.Max(0, Ability?.Cost ?? 0);
            if (Ability?.CanBuyMultiple == true && Ability?.MaxAvailable is { } max && max > 0)
                return $"({cost}/{max})";

            return cost.ToString();
        }
    }

    public string InfiniteCostPrefixText => $"({Math.Max(0, Ability?.Cost ?? 0)}/";
    public string InfinityGlyph => InfinityGlyphCode;

    public string DescriptionText => ReadOrFallback(Ability?.Description, "No description provided.");

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

    public int DescriptionMaxLines => IsDescriptionExpanded ? -1 : DescriptionCollapsedLines;
    public string DescriptionChevronText => IsDescriptionExpanded ? "▴" : "▾";

    private bool _isDescriptionAnimating;

    public AbilityDetailCardView()
    {
        InitializeComponent();
        SizeChanged += (_, __) => ScheduleExpandabilityRefresh();
    }

    private static void OnAbilityChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not AbilityDetailCardView view)
            return;

        view.HandleAbilityChanged();
    }

    private void HandleAbilityChanged()
    {
        IsDescriptionExpanded = false;
        RaiseComputedProperties();
        ScheduleExpandabilityRefresh();
    }

    private void RaiseComputedProperties()
    {
        OnPropertyChanged(nameof(AbilityIndex));
        OnPropertyChanged(nameof(TableDisplayText));
        OnPropertyChanged(nameof(AvailabilityText));
        OnPropertyChanged(nameof(CostDisplayText));
        OnPropertyChanged(nameof(ShowCostText));
        OnPropertyChanged(nameof(ShowInfiniteCost));
        OnPropertyChanged(nameof(InfiniteCostPrefixText));
        OnPropertyChanged(nameof(InfinityGlyph));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(DescriptionChevronText));
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
                DescriptionCollapsedLines,
                "AbilityDescriptionExpand",
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
        CanExpandDescription = ShouldShowExpander(DescriptionLabel, DescriptionText, DescriptionCollapsedLines);
    }

    private static bool ShouldShowExpander(Label label, string text, int collapsedLines)
    {
        if (label == null || string.IsNullOrWhiteSpace(text))
            return false;

        var width = ResolveMeasureWidth(label);
        if (width <= 0)
            return false;

        var fullHeight = MeasureHeight(label, text, width, maxLines: -1);
        var collapsedHeight = MeasureHeight(label, text, width, maxLines: collapsedLines);
        return fullHeight > (collapsedHeight + ExpanderOverflowTolerance);
    }

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
        int collapsedLines,
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
        var beforeHeight = MeasureHeight(label, text, width, beforeExpanded ? -1 : collapsedLines);
        container.HeightRequest = beforeHeight;
        var afterExpanded = toggleAndGetExpandedState();
        var afterHeight = MeasureHeight(label, text, width, afterExpanded ? -1 : collapsedLines);

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

    private static string BuildAvailabilityDisplay(string? rawAvailability)
    {
        var text = (rawAvailability ?? string.Empty).Trim();
        if (text.Length == 0)
            return "Availability unspecified.";

        if (TryParseAvailabilityJson(text, out var parsed))
            return parsed;

        return text;
    }

    private static bool TryParseAvailabilityJson(string raw, out string display)
    {
        display = string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                var single = (doc.RootElement.GetString() ?? string.Empty).Trim();
                if (single.Length == 0)
                    return false;

                display = single;
                return true;
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            var items = doc.RootElement
                .EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => (e.GetString() ?? string.Empty).Trim())
                .Where(e => e.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (items.Count == 0)
                return false;

            display = string.Join(", ", items);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadOrFallback(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }
}
