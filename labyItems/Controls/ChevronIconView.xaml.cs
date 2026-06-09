using labyItems.Helpers;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls;

public partial class ChevronIconView : ContentView
{
    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded),
        typeof(bool),
        typeof(ChevronIconView),
        false,
        propertyChanged: OnIsExpandedChanged);

    public static readonly BindableProperty AnimateRotationProperty = BindableProperty.Create(
        nameof(AnimateRotation),
        typeof(bool),
        typeof(ChevronIconView),
        false);

    public static readonly BindableProperty CollapsedRotationProperty = BindableProperty.Create(
        nameof(CollapsedRotation),
        typeof(double),
        typeof(ChevronIconView),
        0d,
        propertyChanged: OnRotationPropertyChanged);

    public static readonly BindableProperty ExpandedRotationProperty = BindableProperty.Create(
        nameof(ExpandedRotation),
        typeof(double),
        typeof(ChevronIconView),
        90d,
        propertyChanged: OnRotationPropertyChanged);

    public static readonly BindableProperty GlyphColorProperty = BindableProperty.Create(
        nameof(GlyphColor),
        typeof(Color),
        typeof(ChevronIconView),
        Color.FromArgb("#6B7280"));

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(
        nameof(IconSize),
        typeof(double),
        typeof(ChevronIconView),
        40d);

    public static readonly BindableProperty GlyphSizeProperty = BindableProperty.Create(
        nameof(GlyphSize),
        typeof(double),
        typeof(ChevronIconView),
        16d);

    public ChevronIconView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public bool AnimateRotation
    {
        get => (bool)GetValue(AnimateRotationProperty);
        set => SetValue(AnimateRotationProperty, value);
    }

    public double CollapsedRotation
    {
        get => (double)GetValue(CollapsedRotationProperty);
        set => SetValue(CollapsedRotationProperty, value);
    }

    public double ExpandedRotation
    {
        get => (double)GetValue(ExpandedRotationProperty);
        set => SetValue(ExpandedRotationProperty, value);
    }

    public Color GlyphColor
    {
        get => (Color)GetValue(GlyphColorProperty);
        set => SetValue(GlyphColorProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public double GlyphSize
    {
        get => (double)GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    public string Glyph => FontAwesomeGlyphs.Chevron;

    private double TargetRotation => IsExpanded ? ExpandedRotation : CollapsedRotation;

    private void OnLoaded(object? sender, EventArgs e)
    {
        ChevronLabel.Rotation = TargetRotation;
    }

    private static void OnIsExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ChevronIconView chevron)
            _ = chevron.ApplyRotationAsync();
    }

    private static void OnRotationPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ChevronIconView chevron)
            chevron.ChevronLabel.Rotation = chevron.TargetRotation;
    }

    private async Task ApplyRotationAsync()
    {
        var target = TargetRotation;
        if (!AnimateRotation || !IsLoaded)
        {
            ChevronLabel.Rotation = target;
            return;
        }

        await CardExpandAnimationHelper.RotateAsync(ChevronLabel, target);
    }
}
