using labyItems.Helpers;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls;

public partial class GlyphActionRowView : ContentView
{
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph),
        typeof(string),
        typeof(GlyphActionRowView),
        string.Empty);

    public static readonly BindableProperty GlyphFontFamilyProperty = BindableProperty.Create(
        nameof(GlyphFontFamily),
        typeof(string),
        typeof(GlyphActionRowView),
        FontAwesomeGlyphs.SolidFamily);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(GlyphActionRowView),
        string.Empty);

    public static readonly BindableProperty IsDestructiveProperty = BindableProperty.Create(
        nameof(IsDestructive),
        typeof(bool),
        typeof(GlyphActionRowView),
        false,
        propertyChanged: OnEffectiveColorPropertyChanged);

    public static readonly BindableProperty NormalTextColorProperty = BindableProperty.Create(
        nameof(NormalTextColor),
        typeof(Color),
        typeof(GlyphActionRowView),
        Color.FromArgb("#141414"),
        propertyChanged: OnEffectiveColorPropertyChanged);

    public static readonly BindableProperty DestructiveTextColorProperty = BindableProperty.Create(
        nameof(DestructiveTextColor),
        typeof(Color),
        typeof(GlyphActionRowView),
        Color.FromArgb("#B91C1C"),
        propertyChanged: OnEffectiveColorPropertyChanged);

    public static readonly BindableProperty ChevronGlyphProperty = BindableProperty.Create(
        nameof(ChevronGlyph),
        typeof(string),
        typeof(GlyphActionRowView),
        FontAwesomeGlyphs.Chevron);

    public static readonly BindableProperty ChevronFontFamilyProperty = BindableProperty.Create(
        nameof(ChevronFontFamily),
        typeof(string),
        typeof(GlyphActionRowView),
        FontAwesomeGlyphs.SolidFamily);

    public static readonly BindableProperty ShowChevronProperty = BindableProperty.Create(
        nameof(ShowChevron),
        typeof(bool),
        typeof(GlyphActionRowView),
        true);

    public static readonly BindableProperty ChevronColorProperty = BindableProperty.Create(
        nameof(ChevronColor),
        typeof(Color),
        typeof(GlyphActionRowView),
        Color.FromArgb("#6B7280"));

    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(
        nameof(BorderColor),
        typeof(Color),
        typeof(GlyphActionRowView),
        Color.FromArgb("#E5E7EB"));

    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness),
        typeof(double),
        typeof(GlyphActionRowView),
        1d);

    public static readonly BindableProperty RowHeightProperty = BindableProperty.Create(
        nameof(RowHeight),
        typeof(double),
        typeof(GlyphActionRowView),
        48d);

    public static readonly BindableProperty GlyphSizeProperty = BindableProperty.Create(
        nameof(GlyphSize),
        typeof(double),
        typeof(GlyphActionRowView),
        15d);

    public static readonly BindableProperty TextSizeProperty = BindableProperty.Create(
        nameof(TextSize),
        typeof(double),
        typeof(GlyphActionRowView),
        14d);

    public static readonly BindableProperty ChevronSizeProperty = BindableProperty.Create(
        nameof(ChevronSize),
        typeof(double),
        typeof(GlyphActionRowView),
        14d);

    public static readonly BindableProperty ContentPaddingProperty = BindableProperty.Create(
        nameof(ContentPadding),
        typeof(Thickness),
        typeof(GlyphActionRowView),
        new Thickness(16, 0, 12, 0));

    public event EventHandler? Clicked;

    public GlyphActionRowView()
    {
        InitializeComponent();
        GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => Clicked?.Invoke(this, EventArgs.Empty))
        });
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string GlyphFontFamily
    {
        get => (string)GetValue(GlyphFontFamilyProperty);
        set => SetValue(GlyphFontFamilyProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsDestructive
    {
        get => (bool)GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    public Color NormalTextColor
    {
        get => (Color)GetValue(NormalTextColorProperty);
        set => SetValue(NormalTextColorProperty, value);
    }

    public Color DestructiveTextColor
    {
        get => (Color)GetValue(DestructiveTextColorProperty);
        set => SetValue(DestructiveTextColorProperty, value);
    }

    public string ChevronGlyph
    {
        get => (string)GetValue(ChevronGlyphProperty);
        set => SetValue(ChevronGlyphProperty, value);
    }

    public string ChevronFontFamily
    {
        get => (string)GetValue(ChevronFontFamilyProperty);
        set => SetValue(ChevronFontFamilyProperty, value);
    }

    public bool ShowChevron
    {
        get => (bool)GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }

    public Color ChevronColor
    {
        get => (Color)GetValue(ChevronColorProperty);
        set => SetValue(ChevronColorProperty, value);
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public double GlyphSize
    {
        get => (double)GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    public double TextSize
    {
        get => (double)GetValue(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }

    public double ChevronSize
    {
        get => (double)GetValue(ChevronSizeProperty);
        set => SetValue(ChevronSizeProperty, value);
    }

    public Thickness ContentPadding
    {
        get => (Thickness)GetValue(ContentPaddingProperty);
        set => SetValue(ContentPaddingProperty, value);
    }

    public Color EffectiveTextColor => IsDestructive ? DestructiveTextColor : NormalTextColor;

    private static void OnEffectiveColorPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GlyphActionRowView row)
            row.OnPropertyChanged(nameof(EffectiveTextColor));
    }
}
