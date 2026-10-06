using System.Collections.ObjectModel;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.SpellCard;

public partial class SpellDetailCardView : ContentView
{
    public event EventHandler<SpellFieldEditRequestedEventArgs>? FieldEditRequested;

    public static readonly BindableProperty IsEditingProperty = BindableProperty.Create(
        nameof(IsEditing), typeof(bool), typeof(SpellDetailCardView), false,
        propertyChanged: (bindable, _, _) => ((SpellDetailCardView)bindable).HandleSpellChanged());

    public bool IsEditing
    {
        get => (bool)GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }
    private const int DescriptionCollapsedLines = 5;
    private const int VerbalCollapsedLines = 3;
    private const int NotesCollapsedLines = 2;
    private const int DescriptionChevronThresholdChars = 200;
    private const int VerbalChevronThresholdChars = 150;
    private const double ExpanderOverflowTolerance = 0.01;

    public static readonly BindableProperty SpellProperty = BindableProperty.Create(
        nameof(Spell),
        typeof(SpellService.SpellRaw),
        typeof(SpellDetailCardView),
        default(SpellService.SpellRaw),
        propertyChanged: OnSpellChanged);

    public SpellService.SpellRaw? Spell
    {
        get => (SpellService.SpellRaw?)GetValue(SpellProperty);
        set => SetValue(SpellProperty, value);
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
            OnPropertyChanged(nameof(ShowDescriptionSeeMore));
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
            OnPropertyChanged(nameof(ShowVerbalSeeMore));
        }
    }

    private bool _isNotesExpanded;
    public bool IsNotesExpanded
    {
        get => _isNotesExpanded;
        set
        {
            if (_isNotesExpanded == value) return;
            _isNotesExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NotesMaxLines));
            OnPropertyChanged(nameof(ShowNotesSeeMore));
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
            OnPropertyChanged(nameof(ShowDescriptionChevron));
            OnPropertyChanged(nameof(ShowDescriptionSeeMore));
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
            OnPropertyChanged(nameof(ShowVerbalChevron));
            OnPropertyChanged(nameof(ShowVerbalSeeMore));
        }
    }

    private bool _canExpandNotes;
    public bool CanExpandNotes
    {
        get => _canExpandNotes;
        private set
        {
            if (_canExpandNotes == value) return;
            _canExpandNotes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowNotesSeeMore));
        }
    }

    public int DescriptionMaxLines => IsDescriptionExpanded ? -1 : DescriptionCollapsedLines;
    public int VerbalMaxLines => IsVerbalExpanded ? -1 : VerbalCollapsedLines;
    public int NotesMaxLines => IsNotesExpanded ? -1 : NotesCollapsedLines;
    public string DescriptionChevronText => FontAwesomeGlyphs.Chevron;
    public string VerbalChevronText => FontAwesomeGlyphs.Chevron;

    private bool _isDescriptionAnimating;
    private bool _isVerbalAnimating;
    private bool IsAnimatingExpand => _isDescriptionAnimating || _isVerbalAnimating;

    public string SpellName => ReadOrFallback(Spell?.name, IsEditing ? "Tap to add a name" : "Unnamed Spell");
    public string DescriptionText => ReadOrFallback(Spell?.description, IsEditing ? "Tap to add a description" : "No description provided.");
    public string VerbalText => ReadOrFallback(Spell?.verbal, IsEditing ? "Tap to set" : "No verbal provided.");
    public bool HasDescription => !string.IsNullOrWhiteSpace((Spell?.description ?? string.Empty).Trim());
    public bool HasVerbal => !string.IsNullOrWhiteSpace((Spell?.verbal ?? string.Empty).Trim());
    public string NotesText => ReadOrFallback(Spell?.notes, IsEditing ? "Tap to set" : string.Empty);
    public bool HasNotes => IsEditing || !string.IsNullOrWhiteSpace(Spell?.notes);
    public bool ShowDescriptionChevron => HasDescription && CanExpandDescription;
    public bool ShowVerbalChevron => HasVerbal && CanExpandVerbal;
    public bool ShowDescriptionSeeMore => CanExpandDescription && !IsDescriptionExpanded;
    public bool ShowVerbalSeeMore => CanExpandVerbal && !IsVerbalExpanded;
    public bool ShowNotesSeeMore => CanExpandNotes && !IsNotesExpanded;

    public string ColourDisplayText => WizardSpellRules.BuildColourDisplayText(Spell?.colour);
    public string ColourSubtitleText => $"{ColourDisplayText} Spell";
    public string LevelDisplayText => Math.Max(0, Spell?.level ?? 0).ToString();
    public string RangeDisplayText => ReadOrFallback(Spell?.range, IsEditing ? "Tap to set" : string.Empty);
    public string DurationDisplayText => ReadOrFallback(Spell?.duration, IsEditing ? "Tap to set" : string.Empty);
    public bool HasRange => IsEditing || !string.IsNullOrWhiteSpace(Spell?.range);
    public bool HasDuration => IsEditing || !string.IsNullOrWhiteSpace(Spell?.duration);
    public bool HasRangeOrDuration => HasRange || HasDuration;
    public bool HasBothRangeAndDuration => HasRange && HasDuration;
    public bool HasOnlyRange => HasRange && !HasDuration;
    public bool HasOnlyDuration => !HasRange && HasDuration;
    public bool IsAdvanced => Spell?.isAdvanced == true || Spell?.IsAdvancedCompat == true;
    public string ImmunityDisplayText => ReadOrFallback(
        ReadFirst(JoinValues(Spell?.immunityName), Spell?.immunity, Spell?.immunities, Spell?.ImmunityCompat, JoinValues(Spell?.immunityIndex)),
        IsEditing ? "Tap to set" : string.Empty);
    public string TierDisplayText => IsAdvanced ? "★ Advanced" : "Standard";
    public bool HasImmunity => IsEditing || ImmunityDisplayText.Length > 0;
    public bool HasImmunityLink => ImmunityNavigationHelper.HasIndex(Spell?.immunityIndex);
    public CombatQuickFactsVm DamageQuickFacts => CombatQuickFactsFormatter.Build(
        Spell?.Damage != null,
        Spell?.GetDamageAmounts() ?? new List<int[]>(),
        Spell?.GetDamageTypes() ?? new List<string>(),
        Spell?.GetDamageCategories(), Spell?.GetArmourApplies(), Spell?.GetArmourType());
    public bool HasDamageQuickFacts => IsEditing || DamageQuickFacts.HasValue;
    public CombatQuickFactsVm HealQuickFacts => CombatQuickFactsFormatter.Build(Spell?.Heal != null, Spell?.GetHealAmounts() ?? [], Spell?.GetHealTypes() ?? [], isHealing: true);
    public bool HasHealQuickFacts => IsEditing || HealQuickFacts.HasValue;
    public bool HasKeyInformation => HasRangeOrDuration || HasImmunity || HasDamageQuickFacts || HasHealQuickFacts;

    private static string JoinValues(IEnumerable<string>? values) =>
        string.Join(", ", values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()) ?? Array.Empty<string>());

    public Color ColourCircleColor => WizardSpellRules.ResolveColourCircleColor(Spell?.colour);
    public Color ColourCircleBorderColor => NeedsContrastBorder(ColourCircleColor)
        ? Color.FromArgb("#9CA3AF")
        : Color.FromArgb("#00000000");

    public ObservableCollection<SpellMetaChipVm> MetaChips { get; } = new();
    public bool HasMetaChips => MetaChips.Count > 0;

    public SpellDetailCardView()
    {
        InitializeComponent();
        SizeChanged += (_, __) => ScheduleExpandabilityRefresh();
    }

    private static void OnSpellChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not SpellDetailCardView view)
            return;

        view.HandleSpellChanged();
    }

    private void HandleSpellChanged()
    {
        IsDescriptionExpanded = false;
        IsVerbalExpanded = false;
        IsNotesExpanded = false;

        RebuildMetaChips();
        RaiseComputedProperties();
        ScheduleExpandabilityRefresh();
    }

    private void RaiseComputedProperties()
    {
        OnPropertyChanged(nameof(SpellName));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(VerbalText));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(HasVerbal));
        OnPropertyChanged(nameof(NotesText));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(ShowDescriptionChevron));
        OnPropertyChanged(nameof(ShowVerbalChevron));
        OnPropertyChanged(nameof(ShowDescriptionSeeMore));
        OnPropertyChanged(nameof(ShowVerbalSeeMore));
        OnPropertyChanged(nameof(ShowNotesSeeMore));
        OnPropertyChanged(nameof(DescriptionChevronText));
        OnPropertyChanged(nameof(VerbalChevronText));
        OnPropertyChanged(nameof(ColourDisplayText));
        OnPropertyChanged(nameof(ColourSubtitleText));
        OnPropertyChanged(nameof(LevelDisplayText));
        OnPropertyChanged(nameof(RangeDisplayText));
        OnPropertyChanged(nameof(DurationDisplayText));
        OnPropertyChanged(nameof(HasRange));
        OnPropertyChanged(nameof(HasDuration));
        OnPropertyChanged(nameof(HasRangeOrDuration));
        OnPropertyChanged(nameof(HasBothRangeAndDuration));
        OnPropertyChanged(nameof(HasOnlyRange));
        OnPropertyChanged(nameof(HasOnlyDuration));
        OnPropertyChanged(nameof(IsAdvanced));
        OnPropertyChanged(nameof(TierDisplayText));
        OnPropertyChanged(nameof(ImmunityDisplayText));
        OnPropertyChanged(nameof(HasImmunity));
        OnPropertyChanged(nameof(HasImmunityLink));
        OnPropertyChanged(nameof(DamageQuickFacts));
        OnPropertyChanged(nameof(HasDamageQuickFacts));
        OnPropertyChanged(nameof(HealQuickFacts));
        OnPropertyChanged(nameof(HasHealQuickFacts));
        OnPropertyChanged(nameof(HasKeyInformation));
        OnPropertyChanged(nameof(ColourCircleColor));
        OnPropertyChanged(nameof(ColourCircleBorderColor));
        OnPropertyChanged(nameof(HasMetaChips));
    }

    private async void OnImmunityTapped(object sender, TappedEventArgs e)
    {
        if (RequestEdit("immunity")) return;
        await ImmunityNavigationHelper.OpenFirstAvailableAsync(Navigation, Spell?.immunityIndex);
    }
    private async void OnImmunityInfoClicked(object sender, EventArgs e) => await ImmunityNavigationHelper.OpenFirstAvailableAsync(Navigation, Spell?.immunityIndex);

    private void RebuildMetaChips()
    {
        MetaChips.Clear();

        AddMetaChip("Range", Spell?.range, ResolveRangeIcon);
        AddMetaChip("Duration", Spell?.duration, ResolveDurationIcon);
        AddMetaChip("Gesture", Spell?.gesture, ResolveGestureIcon);

        if (Spell?.nonStandard == true)
            MetaChips.Add(new SpellMetaChipVm("✳", "Non-standard"));

        OnPropertyChanged(nameof(HasMetaChips));
    }

    private void AddMetaChip(string label, string? rawValue, Func<string, string> iconFactory)
    {
        var value = (rawValue ?? string.Empty).Trim();
        if (value.Length == 0)
            return;

        MetaChips.Add(new SpellMetaChipVm(iconFactory(value), $"{label}: {value}"));
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
                "SpellDescriptionExpand",
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
                VerbalCollapsedLines,
                "SpellVerbalExpand",
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

    private void OnNotesToggleClicked(object sender, EventArgs e)
    {
        IsNotesExpanded = !IsNotesExpanded;
    }

    private void OnDescriptionSectionTapped(object sender, TappedEventArgs e)
    {
        if (RequestEdit("description")) return;
        if (!CanExpandDescription)
            return;

        OnDescriptionToggleClicked(sender, EventArgs.Empty);
    }

    private void OnVerbalSectionTapped(object sender, TappedEventArgs e)
    {
        if (RequestEdit("verbal")) return;
        if (!CanExpandVerbal)
            return;

        OnVerbalToggleClicked(sender, EventArgs.Empty);
    }

    private void OnNotesSectionTapped(object sender, TappedEventArgs e)
    {
        if (RequestEdit("notes")) return;
        if (!CanExpandNotes)
            return;

        OnNotesToggleClicked(sender, EventArgs.Empty);
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
            || ShouldShowExpander(DescriptionLabel, DescriptionText, DescriptionCollapsedLines);

        CanExpandVerbal =
            HasVerbal && (ExceedsCharacterThreshold(VerbalText, VerbalChevronThresholdChars)
            || ShouldShowExpander(VerbalLabel, VerbalText, VerbalCollapsedLines));

        CanExpandNotes = HasNotes && ShouldShowExpander(NotesLabel, NotesText, NotesCollapsedLines);

        if (IsAnimatingExpand)
            return;

        if (!CanExpandNotes && IsNotesExpanded)
            IsNotesExpanded = false;
    }

    private static bool ExceedsCharacterThreshold(string text, int threshold)
        => (text ?? string.Empty).Trim().Length > threshold;

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

    private static bool NeedsContrastBorder(Color colour)
        => colour.Red > 0.90 && colour.Green > 0.90 && colour.Blue > 0.90;

    private static string ReadOrFallback(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }

    private static string ReadFirst(params string?[] values) => values
        .Select(value => (value ?? string.Empty).Trim())
        .FirstOrDefault(value => value.Length > 0) ?? string.Empty;

    private static string Capitalize(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;
        if (text.Length == 1)
            return text.ToUpperInvariant();

        return char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();
    }

    private static string ResolveRangeIcon(string value)
    {
        var token = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (token.Contains("self"))
            return "◎";
        if (token.Contains("touch"))
            return "✋";
        if (token.Any(char.IsDigit) || token.Contains("'"))
            return "📏";
        return "🎯";
    }

    private static string ResolveDurationIcon(string value)
    {
        var token = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (token.Contains("instant"))
            return "⚡";
        if (token.Contains("permanent") || token.Contains("perm"))
            return "∞";
        if (token.Contains("minute") || token.Contains("hour") || token.Contains("day") || token.Contains("second"))
            return "⏱";
        return "⌛";
    }

    private static string ResolveGestureIcon(string value)
    {
        var token = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (token.Contains("none"))
            return "⦸";
        if (token.Contains("point"))
            return "👉";
        if (token.Contains("touch"))
            return "✋";
        if (token.Contains("hands") || token.Contains("palm") || token.Contains("hand"))
            return "🤲";
        return "🖐";
    }

    private bool RequestEdit(string field)
    {
        if (!IsEditing) return false;
        FieldEditRequested?.Invoke(this, new SpellFieldEditRequestedEventArgs(field));
        return true;
    }

    private void OnNameTapped(object sender, TappedEventArgs e) => RequestEdit("name");
    private void OnColourTapped(object sender, TappedEventArgs e) => RequestEdit("colour");
    private void OnLevelTapped(object sender, TappedEventArgs e) => RequestEdit("level");
    private void OnTierTapped(object sender, TappedEventArgs e) => RequestEdit("isAdvanced");
    private void OnRangeTapped(object sender, TappedEventArgs e) => RequestEdit("range");
    private void OnDurationTapped(object sender, TappedEventArgs e) => RequestEdit("duration");
    private void OnDamageTapped(object sender, TappedEventArgs e) => RequestEdit("Damage");
    private void OnHealTapped(object sender, TappedEventArgs e) => RequestEdit("Heal");
}

public sealed class SpellFieldEditRequestedEventArgs(string field) : EventArgs
{
    public string Field { get; } = field;
}

public sealed class SpellMetaChipVm
{
    public string Icon { get; }
    public string Text { get; }

    public SpellMetaChipVm(string icon, string text)
    {
        Icon = icon;
        Text = text;
    }
}
