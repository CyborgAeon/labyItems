namespace labyItems.Controls;

public partial class AdvanceSectionActionRow : ContentView
{
    public static readonly BindableProperty IconGlyphProperty = BindableProperty.Create(
        nameof(IconGlyph),
        typeof(string),
        typeof(AdvanceSectionActionRow),
        defaultValue: "&#xF0C0;");

    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(
        nameof(Heading),
        typeof(string),
        typeof(AdvanceSectionActionRow),
        defaultValue: string.Empty);

    public static readonly BindableProperty SubheadingProperty = BindableProperty.Create(
        nameof(Subheading),
        typeof(string),
        typeof(AdvanceSectionActionRow),
        defaultValue: string.Empty,
        propertyChanged: OnSubheadingChanged);

    public static readonly BindableProperty ActionGlyphProperty = BindableProperty.Create(
        nameof(ActionGlyph),
        typeof(string),
        typeof(AdvanceSectionActionRow),
        defaultValue: "&#xF067;");

    public static readonly BindableProperty ShowActionButtonProperty = BindableProperty.Create(
        nameof(ShowActionButton),
        typeof(bool),
        typeof(AdvanceSectionActionRow),
        defaultValue: true,
        propertyChanged: OnActionVisibilityChanged);

    public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
        nameof(IsLoading),
        typeof(bool),
        typeof(AdvanceSectionActionRow),
        defaultValue: false,
        propertyChanged: OnActionVisibilityChanged);

    public event EventHandler? ActionClicked;

    public AdvanceSectionActionRow()
    {
        InitializeComponent();
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public string Subheading
    {
        get => (string)GetValue(SubheadingProperty);
        set => SetValue(SubheadingProperty, value);
    }

    public string ActionGlyph
    {
        get => (string)GetValue(ActionGlyphProperty);
        set => SetValue(ActionGlyphProperty, value);
    }

    public bool ShowActionButton
    {
        get => (bool)GetValue(ShowActionButtonProperty);
        set => SetValue(ShowActionButtonProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public bool HasSubheading => !string.IsNullOrWhiteSpace(Subheading);
    public bool ShowActionGlyph => ShowActionButton && !IsLoading;

    private static void OnSubheadingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AdvanceSectionActionRow row)
            row.OnPropertyChanged(nameof(HasSubheading));
    }

    private static void OnActionVisibilityChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AdvanceSectionActionRow row)
            row.OnPropertyChanged(nameof(ShowActionGlyph));
    }

    private void OnRowTapped(object? sender, TappedEventArgs e)
        => RaiseActionClicked();

    private void OnActionButtonClicked(object? sender, EventArgs e)
        => RaiseActionClicked();

    private void RaiseActionClicked()
    {
        if (IsLoading)
            return;

        ActionClicked?.Invoke(this, EventArgs.Empty);
    }
}
