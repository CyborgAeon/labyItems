namespace labyItems.Controls;

public partial class ClassBracketIconView : ContentView
{
    public static readonly BindableProperty AvatarImageSourceProperty = BindableProperty.Create(
        nameof(AvatarImageSource),
        typeof(ImageSource),
        typeof(ClassBracketIconView),
        default(ImageSource),
        propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(
        nameof(IconSize),
        typeof(double),
        typeof(ClassBracketIconView),
        40d,
        propertyChanged: OnSizePropertyChanged);

    public static readonly BindableProperty HasSplitIconProperty = BindableProperty.Create(
        nameof(HasSplitIcon),
        typeof(bool),
        typeof(ClassBracketIconView),
        false,
        propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty SingleGlyphProperty = BindableProperty.Create(
        nameof(SingleGlyph),
        typeof(string),
        typeof(ClassBracketIconView),
        string.Empty);

    public static readonly BindableProperty SingleIconFontFamilyProperty = BindableProperty.Create(
        nameof(SingleIconFontFamily),
        typeof(string),
        typeof(ClassBracketIconView),
        string.Empty);

    public static readonly BindableProperty SingleBackgroundProperty = BindableProperty.Create(
        nameof(SingleBackground),
        typeof(Color),
        typeof(ClassBracketIconView),
        Color.FromArgb("#F3F4F6"));

    public static readonly BindableProperty SplitLeftGlyphProperty = BindableProperty.Create(
        nameof(SplitLeftGlyph),
        typeof(string),
        typeof(ClassBracketIconView),
        string.Empty);

    public static readonly BindableProperty SplitRightGlyphProperty = BindableProperty.Create(
        nameof(SplitRightGlyph),
        typeof(string),
        typeof(ClassBracketIconView),
        string.Empty);

    public static readonly BindableProperty SplitLeftBackgroundProperty = BindableProperty.Create(
        nameof(SplitLeftBackground),
        typeof(Color),
        typeof(ClassBracketIconView),
        Color.FromArgb("#F3F4F6"));

    public static readonly BindableProperty SplitRightBackgroundProperty = BindableProperty.Create(
        nameof(SplitRightBackground),
        typeof(Color),
        typeof(ClassBracketIconView),
        Color.FromArgb("#F3F4F6"));

    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(
        nameof(BorderColor),
        typeof(Color),
        typeof(ClassBracketIconView),
        Color.FromArgb("#E5E7EB"));

    public ClassBracketIconView()
    {
        InitializeComponent();
    }

    public ImageSource? AvatarImageSource
    {
        get => (ImageSource?)GetValue(AvatarImageSourceProperty);
        set => SetValue(AvatarImageSourceProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public bool HasSplitIcon
    {
        get => (bool)GetValue(HasSplitIconProperty);
        set => SetValue(HasSplitIconProperty, value);
    }

    public string SingleGlyph
    {
        get => (string)GetValue(SingleGlyphProperty);
        set => SetValue(SingleGlyphProperty, value);
    }

    public string SingleIconFontFamily
    {
        get => (string)GetValue(SingleIconFontFamilyProperty);
        set => SetValue(SingleIconFontFamilyProperty, value);
    }

    public Color SingleBackground
    {
        get => (Color)GetValue(SingleBackgroundProperty);
        set => SetValue(SingleBackgroundProperty, value);
    }

    public string SplitLeftGlyph
    {
        get => (string)GetValue(SplitLeftGlyphProperty);
        set => SetValue(SplitLeftGlyphProperty, value);
    }

    public string SplitRightGlyph
    {
        get => (string)GetValue(SplitRightGlyphProperty);
        set => SetValue(SplitRightGlyphProperty, value);
    }

    public Color SplitLeftBackground
    {
        get => (Color)GetValue(SplitLeftBackgroundProperty);
        set => SetValue(SplitLeftBackgroundProperty, value);
    }

    public Color SplitRightBackground
    {
        get => (Color)GetValue(SplitRightBackgroundProperty);
        set => SetValue(SplitRightBackgroundProperty, value);
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public bool ShowImage => AvatarImageSource != null;
    public bool ShowSplitIcon => !ShowImage && HasSplitIcon;
    public bool ShowSingleIcon => !ShowImage && !HasSplitIcon;
    public double GlyphFontSize => Math.Max(12d, IconSize * 0.45d);

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ClassBracketIconView view)
            return;

        view.OnPropertyChanged(nameof(ShowImage));
        view.OnPropertyChanged(nameof(ShowSplitIcon));
        view.OnPropertyChanged(nameof(ShowSingleIcon));
    }

    private static void OnSizePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ClassBracketIconView view)
            return;

        view.OnPropertyChanged(nameof(GlyphFontSize));
    }
}
