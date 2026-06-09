using labyItems.Helpers;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls;

public partial class GlyphIconButtonView : ContentView
{
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph),
        typeof(string),
        typeof(GlyphIconButtonView),
        string.Empty);

    public static readonly BindableProperty GlyphFontFamilyProperty = BindableProperty.Create(
        nameof(GlyphFontFamily),
        typeof(string),
        typeof(GlyphIconButtonView),
        FontAwesomeGlyphs.SolidFamily);

    public static readonly BindableProperty GlyphColorProperty = BindableProperty.Create(
        nameof(GlyphColor),
        typeof(Color),
        typeof(GlyphIconButtonView),
        Colors.Black);

    public static readonly BindableProperty ButtonSizeProperty = BindableProperty.Create(
        nameof(ButtonSize),
        typeof(double),
        typeof(GlyphIconButtonView),
        40d);

    public static readonly BindableProperty GlyphSizeProperty = BindableProperty.Create(
        nameof(GlyphSize),
        typeof(double),
        typeof(GlyphIconButtonView),
        18d);

    public static readonly BindableProperty IconPaddingProperty = BindableProperty.Create(
        nameof(IconPadding),
        typeof(Thickness),
        typeof(GlyphIconButtonView),
        new Thickness(6));

    public event EventHandler? Clicked;

    public GlyphIconButtonView()
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

    public Color GlyphColor
    {
        get => (Color)GetValue(GlyphColorProperty);
        set => SetValue(GlyphColorProperty, value);
    }

    public double ButtonSize
    {
        get => (double)GetValue(ButtonSizeProperty);
        set => SetValue(ButtonSizeProperty, value);
    }

    public double GlyphSize
    {
        get => (double)GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    public Thickness IconPadding
    {
        get => (Thickness)GetValue(IconPaddingProperty);
        set => SetValue(IconPaddingProperty, value);
    }
}
