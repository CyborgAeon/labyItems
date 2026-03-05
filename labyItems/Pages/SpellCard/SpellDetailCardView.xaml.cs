using System.Collections.ObjectModel;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.SpellCard;

public partial class SpellDetailCardView : ContentView
{
    private const int DescriptionCollapsedLines = 5;
    private const int VerbalCollapsedLines = 3;
    private const int NotesCollapsedLines = 2;
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
        }
    }

    public int DescriptionMaxLines => IsDescriptionExpanded ? -1 : DescriptionCollapsedLines;
    public int VerbalMaxLines => IsVerbalExpanded ? -1 : VerbalCollapsedLines;
    public int NotesMaxLines => IsNotesExpanded ? -1 : NotesCollapsedLines;
    public string DescriptionChevronText => IsDescriptionExpanded ? "▴" : "▾";
    public string VerbalChevronText => IsVerbalExpanded ? "▴" : "▾";

    private bool _isDescriptionAnimating;
    private bool _isVerbalAnimating;
    private bool IsAnimatingExpand => _isDescriptionAnimating || _isVerbalAnimating;

    public string SpellName => ReadOrFallback(Spell?.name, fallback: "Unnamed Spell");
    public string DescriptionText => ReadOrFallback(Spell?.description, fallback: "No description provided.");
    public string VerbalText => ReadOrFallback(Spell?.verbal, fallback: "No verbal provided.");
    public bool HasDescription => !string.IsNullOrWhiteSpace((Spell?.description ?? string.Empty).Trim());
    public bool HasVerbal => !string.IsNullOrWhiteSpace((Spell?.verbal ?? string.Empty).Trim());
    public string NotesText => (Spell?.notes ?? string.Empty).Trim();
    public bool HasNotes => NotesText.Length > 0;
    public bool ShowDescriptionChevron => HasDescription && CanExpandDescription;
    public bool ShowVerbalChevron => HasVerbal && CanExpandVerbal;

    public string ColourDisplayText => BuildColourDisplayText(Spell?.colour);
    public string LevelDisplayText => $"Lvl {Math.Max(0, Spell?.level ?? 0)}";

    public Color ColourCircleColor => ResolveColourCircleColor(Spell?.colour);
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
        OnPropertyChanged(nameof(DescriptionChevronText));
        OnPropertyChanged(nameof(VerbalChevronText));
        OnPropertyChanged(nameof(ColourDisplayText));
        OnPropertyChanged(nameof(LevelDisplayText));
        OnPropertyChanged(nameof(ColourCircleColor));
        OnPropertyChanged(nameof(ColourCircleBorderColor));
        OnPropertyChanged(nameof(HasMetaChips));
    }

    private void RebuildMetaChips()
    {
        MetaChips.Clear();

        AddMetaChip("Range", Spell?.range, ResolveRangeIcon);
        AddMetaChip("Duration", Spell?.duration, ResolveDurationIcon);
        AddMetaChip("Gesture", Spell?.gesture, ResolveGestureIcon);

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
        CanExpandVerbal = ShouldShowExpander(VerbalLabel, VerbalText, VerbalCollapsedLines);
        CanExpandNotes = HasNotes && ShouldShowExpander(NotesLabel, NotesText, NotesCollapsedLines);

        if (IsAnimatingExpand)
            return;

        if (!CanExpandNotes && IsNotesExpanded)
            IsNotesExpanded = false;
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

    private static string BuildColourDisplayText(string? rawColour)
    {
        if (TryResolveMagicColour(rawColour, out var parsed))
            return parsed.ToString();

        var fallback = GetFirstColourToken(rawColour);
        if (fallback.Length == 0)
            return "Unknown";

        return Capitalize(fallback);
    }

    private static Color ResolveColourCircleColor(string? rawColour)
    {
        if (!TryResolveMagicColour(rawColour, out var parsed))
            return Color.FromArgb("#9CA3AF");

        return parsed switch
        {
            MagicColours.Red => Color.FromArgb("#EF4444"),
            MagicColours.Blue => Color.FromArgb("#3B82F6"),
            MagicColours.Green => Color.FromArgb("#10B981"),
            MagicColours.Brown => Color.FromArgb("#8B5E3C"),
            MagicColours.White => Color.FromArgb("#F3F4F6"),
            MagicColours.Black => Color.FromArgb("#111827"),
            MagicColours.Grey => Color.FromArgb("#9CA3AF"),
            MagicColours.Gold => Color.FromArgb("#D4AF37"),
            MagicColours.Bronze => Color.FromArgb("#CD7F32"),
            MagicColours.Silver => Color.FromArgb("#C0C0C0"),
            MagicColours.Ivory => Color.FromArgb("#F5F5DC"),
            MagicColours.Ebony => Color.FromArgb("#2F1B0C"),
            MagicColours.Jade => Color.FromArgb("#00A86B"),
            MagicColours.Onyx => Color.FromArgb("#353839"),
            _ => Color.FromArgb("#6B7280")
        };
    }

    private static bool NeedsContrastBorder(Color colour)
        => colour.Red > 0.90 && colour.Green > 0.90 && colour.Blue > 0.90;

    private static bool TryResolveMagicColour(string? rawColour, out MagicColours colour)
    {
        colour = default;

        var tokens = (rawColour ?? string.Empty)
            .Split(new[] { '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            if (TryParseMagicColour(token, out colour))
                return true;
        }

        return TryParseMagicColour(rawColour, out colour);
    }

    private static bool TryParseMagicColour(string? value, out MagicColours colour)
    {
        colour = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace(" ", string.Empty);
        return Enum.TryParse(normalized, ignoreCase: true, out colour);
    }

    private static string GetFirstColourToken(string? rawColour)
    {
        if (string.IsNullOrWhiteSpace(rawColour))
            return string.Empty;

        return rawColour
            .Split(new[] { '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string ReadOrFallback(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }

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
