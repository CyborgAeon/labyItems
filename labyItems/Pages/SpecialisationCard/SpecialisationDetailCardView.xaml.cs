using System.Collections.ObjectModel;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.SpecialisationCard;

public partial class SpecialisationDetailCardView : ContentView
{
    private const int DescriptionCollapsedLines = 4;
    private const double ExpanderOverflowTolerance = 0.01;
    private const int ChipValueMaxLength = 56;
    private const int DescriptionHeuristicCharsPerLine = 60;
    private const string StandardScoutSkillName = "Standard Scout skill";
    private const string StandardScoutHintText = "A scout calculates his skill level by counting the number of levels he has had it for. e.g.: If a scout gains a skill at 3rd level and is now 6th level they have 4 levels in it.";

    public static readonly BindableProperty SpecialisationNameProperty = BindableProperty.Create(
        nameof(SpecialisationName),
        typeof(string),
        typeof(SpecialisationDetailCardView),
        string.Empty,
        propertyChanged: OnSpecialisationDataChanged);

    public static readonly BindableProperty SelectedOptionProperty = BindableProperty.Create(
        nameof(SelectedOption),
        typeof(string),
        typeof(SpecialisationDetailCardView),
        string.Empty,
        propertyChanged: OnSpecialisationDataChanged);

    public static readonly BindableProperty SpecialisationProperty = BindableProperty.Create(
        nameof(Specialisation),
        typeof(SpecialisationRecord),
        typeof(SpecialisationDetailCardView),
        default(SpecialisationRecord),
        propertyChanged: OnSpecialisationDataChanged);

    public string SpecialisationName
    {
        get => (string)(GetValue(SpecialisationNameProperty) ?? string.Empty);
        set => SetValue(SpecialisationNameProperty, value);
    }

    public string SelectedOption
    {
        get => (string)(GetValue(SelectedOptionProperty) ?? string.Empty);
        set => SetValue(SelectedOptionProperty, value);
    }

    public SpecialisationRecord? Specialisation
    {
        get => (SpecialisationRecord?)GetValue(SpecialisationProperty);
        set => SetValue(SpecialisationProperty, value);
    }

    public string TitleText => ResolveTitleText();
    public string SubheadingText => ReadOrFallback(SpecialisationName, string.Empty);
    public bool HasSubheading => !string.IsNullOrWhiteSpace(SubheadingText);
    public string DescriptionText => ResolveDescriptionText();
    public string HintText => ResolveHintText();
    public bool HasHint => !string.IsNullOrWhiteSpace(HintText);

    public ObservableCollection<string> MetadataChips { get; } = new();
    public bool HasMetadataChips => MetadataChips.Count > 0;

    private AbilityDefinition? _resolvedAbility;
    private ColourAbilityRecord? _resolvedColourAbility;

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

    public SpecialisationDetailCardView()
    {
        InitializeComponent();
        SizeChanged += (_, __) => ScheduleExpandabilityRefresh();
        MetadataChips.CollectionChanged += (_, __) => OnPropertyChanged(nameof(HasMetadataChips));
    }

    private static void OnSpecialisationDataChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not SpecialisationDetailCardView view)
            return;

        view.HandleSpecialisationChanged();
    }

    private void HandleSpecialisationChanged()
    {
        _resolvedAbility = ResolveSelectedAbility();
        _resolvedColourAbility = ResolveSelectedColourAbility();
        RebuildMetadataChips();
        IsDescriptionExpanded = false;
        RaiseComputedProperties();
        ScheduleExpandabilityRefresh();
    }

    private void RebuildMetadataChips()
    {
        MetadataChips.Clear();
        if (_resolvedAbility == null)
        {
            if (_resolvedColourAbility == null)
                return;

            AddChip("Life scale", _resolvedColourAbility.LifeScaleOverride);
            AddChip("Armour", _resolvedColourAbility.ArmourAvailabilityOverride);

            if (_resolvedColourAbility.ColourChoiceOverride is { Count: > 0 })
                AddChip("Colours", string.Join(", ", _resolvedColourAbility.ColourChoiceOverride.Where(x => !string.IsNullOrWhiteSpace(x))));

            if (_resolvedColourAbility.ClassRestriction is { Count: > 0 })
                AddChip("Class", string.Join(", ", _resolvedColourAbility.ClassRestriction.Where(x => !string.IsNullOrWhiteSpace(x))));

            if (_resolvedColourAbility.HedgeOrCircle is { Count: > 0 })
                AddChip("Options", string.Join(", ", _resolvedColourAbility.HedgeOrCircle.Where(x => !string.IsNullOrWhiteSpace(x))));

            return;
        }

        AddChip("Type", _resolvedAbility.Type);
        AddChip("Count", _resolvedAbility.Count?.ToString());
        AddChip("Source", _resolvedAbility.Source);
        AddChip("Frequency", _resolvedAbility.Frequency);

        if (_resolvedAbility.Amount is { Count: > 0 })
            AddChip("Amount", string.Join("/", _resolvedAbility.Amount));

        if (_resolvedAbility.PreReqs is { Count: > 0 })
            AddChip("Prereqs", string.Join(", ", _resolvedAbility.PreReqs.Where(x => !string.IsNullOrWhiteSpace(x))));

        if (_resolvedAbility.GuildOverrides is { Count: > 0 })
            AddChip("Guild", string.Join(", ", _resolvedAbility.GuildOverrides.Where(x => !string.IsNullOrWhiteSpace(x))));

        if (_resolvedAbility.Customisation != null)
        {
            AddChip("Custom", _resolvedAbility.Customisation.OptionEnum);
            if (_resolvedAbility.Customisation.CustomValuesPermitted)
                MetadataChips.Add("Custom values allowed");
        }

        var description = ResolveDescriptionFromAbility(_resolvedAbility);
        if (!string.IsNullOrWhiteSpace(_resolvedAbility.Effect)
            && !string.Equals(_resolvedAbility.Effect.Trim(), description, StringComparison.OrdinalIgnoreCase))
        {
            AddChip("Effect", _resolvedAbility.Effect);
        }
    }

    private void AddChip(string label, string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return;

        if (normalized.Length > ChipValueMaxLength)
            normalized = $"{normalized[..ChipValueMaxLength]}...";

        MetadataChips.Add($"{label}: {normalized}");
    }

    private string ResolveTitleText()
    {
        var selected = ReadOrFallback(SelectedOption, string.Empty);
        if (_resolvedAbility != null)
            return ReadOrFallback(_resolvedAbility.Name, selected.Length > 0 ? selected : "Specialisation");

        if (selected.Length > 0)
            return selected;

        return ReadOrFallback(SpecialisationName, "Specialisation");
    }

    private string ResolveDescriptionText()
    {
        if (_resolvedAbility != null)
            return ResolveDescriptionFromAbility(_resolvedAbility);

        if (_resolvedColourAbility != null)
            return ResolveDescriptionFromColourAbility(_resolvedColourAbility);

        var fromRecord = ReadOrFallback(Specialisation?.Description, string.Empty);
        if (fromRecord.Length > 0)
            return fromRecord;

        return "No description provided.";
    }

    private string ResolveHintText()
    {
        if (!string.Equals((SpecialisationName ?? string.Empty).Trim(), StandardScoutSkillName, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var hint = ReadOrFallback(Specialisation?.Description, StandardScoutHintText);
        if (hint.Length == 0)
            hint = StandardScoutHintText;

        return hint;
    }

    private static string ResolveDescriptionFromAbility(AbilityDefinition ability)
    {
        var effect = ReadOrFallback(ability.Effect, string.Empty);
        return effect.Length > 0 ? effect : "No description provided.";
    }

    private static string ResolveDescriptionFromColourAbility(ColourAbilityRecord colourAbility)
    {
        var fromDescription = ReadOrFallback(colourAbility.Description, string.Empty);
        if (fromDescription.Length > 0)
            return fromDescription;

        if (TryGetFirstAbilityFromLevels(colourAbility.Levels, out var ability) && ability != null)
            return ResolveDescriptionFromAbility(ability);

        return "No description provided.";
    }

    private AbilityDefinition? ResolveSelectedAbility()
    {
        var targetName = (SelectedOption ?? string.Empty).Trim();
        if (targetName.Length == 0 || Specialisation == null)
            return null;

        var fromAbilities = FindByName(Specialisation.Abilities, targetName);
        if (fromAbilities != null)
            return fromAbilities;

        var fromOptions = FindByName(Specialisation.Options, targetName);
        if (fromOptions != null)
            return fromOptions;

        if (Specialisation.ColourAbilities != null)
        {
            foreach (var colour in Specialisation.ColourAbilities.Values)
            {
                if (colour?.Levels == null)
                    continue;

                foreach (var bucket in colour.Levels.Values)
                {
                    var found = FindByName(bucket, targetName);
                    if (found != null)
                        return found;
                }
            }

            if (Specialisation.ColourAbilities.TryGetValue(targetName, out var selectedColour))
            {
                if (TryGetFirstAbilityFromLevels(selectedColour?.Levels, out var firstAbility))
                    return firstAbility;
            }
        }

        return null;
    }

    private ColourAbilityRecord? ResolveSelectedColourAbility()
    {
        var targetName = (SelectedOption ?? string.Empty).Trim();
        if (targetName.Length == 0 || Specialisation?.ColourAbilities == null)
            return null;

        return Specialisation.ColourAbilities.TryGetValue(targetName, out var matched)
            ? matched
            : null;
    }

    private static bool TryGetFirstAbilityFromLevels(
        Dictionary<string, List<AbilityDefinition>>? levels,
        out AbilityDefinition? ability)
    {
        ability = null;
        if (levels == null || levels.Count == 0)
            return false;

        var first = levels
            .Select(kvp => new
            {
                Level = int.TryParse(kvp.Key, out var parsed) ? parsed : int.MaxValue,
                Abilities = kvp.Value ?? new List<AbilityDefinition>()
            })
            .OrderBy(x => x.Level)
            .SelectMany(x => x.Abilities)
            .FirstOrDefault(a => a != null && !string.IsNullOrWhiteSpace(a.Name));

        if (first == null)
            return false;

        ability = first;
        return true;
    }

    private static AbilityDefinition? FindByName(IEnumerable<AbilityDefinition>? list, string targetName)
    {
        if (list == null)
            return null;

        return list.FirstOrDefault(a =>
            a != null
            && (
                string.Equals((a.Name ?? string.Empty).Trim(), targetName, StringComparison.OrdinalIgnoreCase)
                || string.Equals((a.Key ?? string.Empty).Trim(), targetName, StringComparison.OrdinalIgnoreCase)));
    }

    private static string ReadOrFallback(string? value, string fallback)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? fallback : trimmed;
    }

    private void RaiseComputedProperties()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubheadingText));
        OnPropertyChanged(nameof(HasSubheading));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(HintText));
        OnPropertyChanged(nameof(HasHint));
        OnPropertyChanged(nameof(DescriptionChevronText));
        OnPropertyChanged(nameof(ShowDescriptionSeeMore));
        OnPropertyChanged(nameof(HasMetadataChips));
    }

    private void OnExpandableLabelSizeChanged(object sender, EventArgs e)
    {
        ScheduleExpandabilityRefresh();
    }

    private void OnDescriptionSectionTapped(object sender, TappedEventArgs e)
    {
        if (!CanExpandDescription && !IsDescriptionExpanded && !LikelyNeedsExpansion(DescriptionText, DescriptionCollapsedLines))
            return;

        OnDescriptionToggleClicked();
    }

    private async void OnDescriptionToggleClicked()
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
                "SpecialisationDescriptionExpand",
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
            return LikelyNeedsExpansion(text, collapsedLines);

        var fullHeight = MeasureHeight(label, text, width, maxLines: -1);
        var collapsedHeight = MeasureHeight(label, text, width, maxLines: collapsedLines);
        if (fullHeight > (collapsedHeight + ExpanderOverflowTolerance))
            return true;

        // iOS can under-report in probe measurement for some layouts; keep a safe fallback.
        return LikelyNeedsExpansion(text, collapsedLines);
    }

    private static bool LikelyNeedsExpansion(string text, int collapsedLines)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Replace("\r\n", "\n");
        var explicitLines = normalized.Split('\n').Length;
        if (explicitLines > collapsedLines)
            return true;

        return normalized.Length > (collapsedLines * DescriptionHeuristicCharsPerLine);
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
}
