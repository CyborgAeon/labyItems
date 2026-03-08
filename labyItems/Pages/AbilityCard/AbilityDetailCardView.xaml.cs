using System.Collections.ObjectModel;
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
    public string TableDisplayText => $"Table: {Math.Max(0, Ability?.Table ?? 0)}";
    public string AvailabilityText => BuildAvailabilityDisplay(Ability?.Available);

    public bool ShowInfiniteCost => Ability?.CanBuyMultiple == true && Ability?.MaxAvailable is not > 0;
    public bool ShowCostText => !ShowInfiniteCost;
    public string CostDisplayText
    {
        get
        {
            var cost = Math.Max(0, Ability?.Cost ?? 0);
            if (Ability?.CanBuyMultiple == true && Ability?.MaxAvailable is { } max && max > 0)
                return $"Cost: ({cost}/{max})";

            return $"Cost: {cost}";
        }
    }

    public string InfiniteCostPrefixText => $"Cost: ({Math.Max(0, Ability?.Cost ?? 0)}/";
    public string InfinityGlyph => InfinityGlyphCode;

    public string DescriptionText => ReadOrFallback(Ability?.Description, "No description provided.");
    public string NotesText => BuildNotesText(Ability);
    public bool HasNotesText => NotesText.Length > 0;
    public bool HasPreReqs => PreReqEntries.Count > 0;
    public bool HasNotesSection => HasNotesText || HasPreReqs;

    public ObservableCollection<AbilityPreReqEntryVm> PreReqEntries { get; } = new();

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
            OnPropertyChanged(nameof(ShowDescriptionSeeMore));
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
            OnPropertyChanged(nameof(ShowDescriptionSeeMore));
        }
    }

    public int DescriptionMaxLines => IsDescriptionExpanded ? -1 : DescriptionCollapsedLines;
    public string DescriptionChevronText => IsDescriptionExpanded ? "▴" : "▾";
    public bool ShowDescriptionSeeMore => CanExpandDescription && !IsDescriptionExpanded;

    private bool _isDescriptionAnimating;
    private int _preReqRefreshVersion;

    public AbilityDetailCardView()
    {
        InitializeComponent();
        SizeChanged += (_, __) => ScheduleExpandabilityRefresh();
        PreReqEntries.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasPreReqs));
            OnPropertyChanged(nameof(HasNotesSection));
        };
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
        _ = RebuildPreReqEntriesAsync();
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
        OnPropertyChanged(nameof(NotesText));
        OnPropertyChanged(nameof(HasNotesText));
        OnPropertyChanged(nameof(HasNotesSection));
        OnPropertyChanged(nameof(DescriptionChevronText));
        OnPropertyChanged(nameof(ShowDescriptionSeeMore));
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

    private void OnDescriptionSectionTapped(object sender, TappedEventArgs e)
    {
        if (!CanExpandDescription)
            return;

        OnDescriptionToggleClicked(sender, EventArgs.Empty);
    }

    private async void OnPreReqInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.CommandParameter is not AbilityPreReqEntryVm entry || entry.Ability == null)
            return;

        if (Navigation == null)
            return;

        await Navigation.PushAsync(new AbilityCard(entry.Ability));
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
        var entries = ParseAvailabilityEntries(rawAvailability);
        if (entries.Count == 0)
            return "Available: unspecified";

        var lowered = entries
            .Select(e => e.ToLowerInvariant())
            .ToList();
        return $"Available: {string.Join(", ", lowered)}";
    }

    private static List<string> ParseAvailabilityEntries(string? rawAvailability)
    {
        var text = (rawAvailability ?? string.Empty).Trim();
        if (text.Length == 0)
            return new List<string>();

        if (TryParseAvailabilityJson(text, out var parsed))
            return parsed;

        return new List<string> { text };
    }

    private static bool TryParseAvailabilityJson(string raw, out List<string> entries)
    {
        entries = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
            {
                var single = (doc.RootElement.GetString() ?? string.Empty).Trim();
                if (single.Length == 0)
                    return false;

                entries.Add(single);
                return true;
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            entries = doc.RootElement
                .EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => (e.GetString() ?? string.Empty).Trim())
                .Where(e => e.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (entries.Count == 0)
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task RebuildPreReqEntriesAsync()
    {
        var refreshVersion = ++_preReqRefreshVersion;
        var names = (Ability?.PreReqs ?? Array.Empty<string>())
            .Select(p => (p ?? string.Empty).Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
        {
            if (refreshVersion != _preReqRefreshVersion)
                return;

            PreReqEntries.Clear();
            return;
        }

        var lookup = await AbilityDetailsLookupService.GetLookupAsync();
        if (refreshVersion != _preReqRefreshVersion)
            return;

        var entries = names
            .Select(name => new AbilityPreReqEntryVm(
                name,
                AbilityDetailsLookupService.FindByIndex(lookup, name)))
            .ToList();

        if (refreshVersion != _preReqRefreshVersion)
            return;

        PreReqEntries.Clear();
        foreach (var entry in entries)
            PreReqEntries.Add(entry);
    }

    private static string BuildNotesText(EvolutionService.AbilityResult? ability)
    {
        if (ability == null)
            return string.Empty;

        if (ability.CanBuyMultiple && ability.MaxAvailable is { } max && max > 0)
            return $"Can be purchased multiple times (maximum {max}).";

        if (ability.CanBuyMultiple)
            return "Can be purchased multiple times.";

        return string.Empty;
    }

    private static string ReadOrFallback(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }
}

public sealed class AbilityPreReqEntryVm
{
    public string Name { get; }
    public EvolutionService.AbilityResult? Ability { get; }
    public bool HasDetails => Ability != null;

    public AbilityPreReqEntryVm(string name, EvolutionService.AbilityResult? ability)
    {
        Name = name;
        Ability = ability;
    }
}
