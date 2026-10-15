using System.Windows.Input;
using labyItems.Helpers;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls;

public partial class ListItemRowView : ContentView
{
    public static readonly BindableProperty PrimaryTextProperty = Create(nameof(PrimaryText), string.Empty);
    public static readonly BindableProperty SecondaryTextProperty = Create(nameof(SecondaryText), string.Empty);
    public static readonly BindableProperty ShowSecondaryTextProperty = Create(nameof(ShowSecondaryText), false);
    public static readonly BindableProperty TertiaryTextProperty = Create(nameof(TertiaryText), string.Empty);
    public static readonly BindableProperty ShowTertiaryTextProperty = Create(nameof(ShowTertiaryText), false);
    public static readonly BindableProperty ShowInfoProperty = Create(nameof(ShowInfo), false);
    public static readonly BindableProperty InfoGlyphProperty = Create(nameof(InfoGlyph), FontAwesomeGlyphs.InfoCircle);
    public static readonly BindableProperty ShowDeleteProperty = Create(nameof(ShowDelete), false);
    public static readonly BindableProperty InfoCommandProperty = Create<ICommand?>(nameof(InfoCommand), null);
    public static readonly BindableProperty DeleteCommandProperty = Create<ICommand?>(nameof(DeleteCommand), null);
    public static readonly BindableProperty ActionCommandParameterProperty = BindableProperty.Create(
        nameof(ActionCommandParameter), typeof(object), typeof(ListItemRowView));
    public static readonly BindableProperty RowBackgroundColorProperty = Create(nameof(RowBackgroundColor), Colors.Transparent);
    public static readonly BindableProperty BorderColorProperty = Create(nameof(BorderColor), Colors.Transparent);
    public static readonly BindableProperty BorderThicknessProperty = Create(nameof(BorderThickness), 0d);
    public static readonly BindableProperty ContentPaddingProperty = Create(nameof(ContentPadding), new Thickness(10, 8));
    public static readonly BindableProperty PrimaryTextSizeProperty = Create(nameof(PrimaryTextSize), 14d);
    public static readonly BindableProperty SecondaryTextSizeProperty = Create(nameof(SecondaryTextSize), 14d);
    public static readonly BindableProperty PrimaryTextMaxLinesProperty = Create(nameof(PrimaryTextMaxLines), 1);
    public static readonly BindableProperty PrimaryTextAttributesProperty = Create(nameof(PrimaryTextAttributes), FontAttributes.None);
    public static readonly BindableProperty PrimaryColumnWidthProperty = Create(nameof(PrimaryColumnWidth), GridLength.Star);
    public static readonly BindableProperty SecondaryColumnWidthProperty = Create(nameof(SecondaryColumnWidth), GridLength.Auto);
    public static readonly BindableProperty TertiaryColumnWidthProperty = Create(nameof(TertiaryColumnWidth), GridLength.Auto);
    public static readonly BindableProperty InfoColumnWidthProperty = Create(nameof(InfoColumnWidth), GridLength.Auto);
    public static readonly BindableProperty SecondaryTextAlignmentProperty = Create(nameof(SecondaryTextAlignment), TextAlignment.End);
    public static readonly BindableProperty PrimaryTextAlignmentProperty = Create(nameof(PrimaryTextAlignment), TextAlignment.Start);
    public static readonly BindableProperty CustomContentProperty = BindableProperty.Create(
        nameof(CustomContent), typeof(View), typeof(ListItemRowView), null,
        propertyChanged: static (bindable, _, _) =>
        {
            var row = (ListItemRowView)bindable;
            row.OnPropertyChanged(nameof(HasCustomContent));
            row.OnPropertyChanged(nameof(UsesStandardContent));
        });

    public event EventHandler? InfoClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler? RowClicked;

    public ListItemRowView()
    {
        InitializeComponent();
        GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => RowClicked?.Invoke(this, EventArgs.Empty))
        });
    }

    public string PrimaryText { get => (string)GetValue(PrimaryTextProperty); set => SetValue(PrimaryTextProperty, value); }
    public string SecondaryText { get => (string)GetValue(SecondaryTextProperty); set => SetValue(SecondaryTextProperty, value); }
    public bool ShowSecondaryText { get => (bool)GetValue(ShowSecondaryTextProperty); set => SetValue(ShowSecondaryTextProperty, value); }
    public string TertiaryText { get => (string)GetValue(TertiaryTextProperty); set => SetValue(TertiaryTextProperty, value); }
    public bool ShowTertiaryText { get => (bool)GetValue(ShowTertiaryTextProperty); set => SetValue(ShowTertiaryTextProperty, value); }
    public bool ShowInfo { get => (bool)GetValue(ShowInfoProperty); set => SetValue(ShowInfoProperty, value); }
    public string InfoGlyph { get => (string)GetValue(InfoGlyphProperty); set => SetValue(InfoGlyphProperty, value); }
    public bool ShowDelete { get => (bool)GetValue(ShowDeleteProperty); set => SetValue(ShowDeleteProperty, value); }
    public ICommand? InfoCommand { get => (ICommand?)GetValue(InfoCommandProperty); set => SetValue(InfoCommandProperty, value); }
    public ICommand? DeleteCommand { get => (ICommand?)GetValue(DeleteCommandProperty); set => SetValue(DeleteCommandProperty, value); }
    public object? ActionCommandParameter { get => GetValue(ActionCommandParameterProperty); set => SetValue(ActionCommandParameterProperty, value); }
    public Color RowBackgroundColor { get => (Color)GetValue(RowBackgroundColorProperty); set => SetValue(RowBackgroundColorProperty, value); }
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    public double BorderThickness { get => (double)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public Thickness ContentPadding { get => (Thickness)GetValue(ContentPaddingProperty); set => SetValue(ContentPaddingProperty, value); }
    public double PrimaryTextSize { get => (double)GetValue(PrimaryTextSizeProperty); set => SetValue(PrimaryTextSizeProperty, value); }
    public double SecondaryTextSize { get => (double)GetValue(SecondaryTextSizeProperty); set => SetValue(SecondaryTextSizeProperty, value); }
    public int PrimaryTextMaxLines { get => (int)GetValue(PrimaryTextMaxLinesProperty); set => SetValue(PrimaryTextMaxLinesProperty, value); }
    public FontAttributes PrimaryTextAttributes { get => (FontAttributes)GetValue(PrimaryTextAttributesProperty); set => SetValue(PrimaryTextAttributesProperty, value); }
    public GridLength PrimaryColumnWidth { get => (GridLength)GetValue(PrimaryColumnWidthProperty); set => SetValue(PrimaryColumnWidthProperty, value); }
    public GridLength SecondaryColumnWidth { get => (GridLength)GetValue(SecondaryColumnWidthProperty); set => SetValue(SecondaryColumnWidthProperty, value); }
    public GridLength TertiaryColumnWidth { get => (GridLength)GetValue(TertiaryColumnWidthProperty); set => SetValue(TertiaryColumnWidthProperty, value); }
    public GridLength InfoColumnWidth { get => (GridLength)GetValue(InfoColumnWidthProperty); set => SetValue(InfoColumnWidthProperty, value); }
    public TextAlignment SecondaryTextAlignment { get => (TextAlignment)GetValue(SecondaryTextAlignmentProperty); set => SetValue(SecondaryTextAlignmentProperty, value); }
    public TextAlignment PrimaryTextAlignment { get => (TextAlignment)GetValue(PrimaryTextAlignmentProperty); set => SetValue(PrimaryTextAlignmentProperty, value); }
    public View? CustomContent { get => (View?)GetValue(CustomContentProperty); set => SetValue(CustomContentProperty, value); }
    public bool HasCustomContent => CustomContent != null;
    public bool UsesStandardContent => !HasCustomContent;

    private void OnInfoClicked(object? sender, EventArgs e) => InfoClicked?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClicked(object? sender, EventArgs e) => DeleteClicked?.Invoke(this, EventArgs.Empty);

    private static BindableProperty Create<T>(string name, T defaultValue) =>
        BindableProperty.Create(name, typeof(T), typeof(ListItemRowView), defaultValue);
}
