using System.Windows.Input;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls;

public partial class ListItemRowView : ContentView
{
    public static readonly BindableProperty PrimaryTextProperty = Create(nameof(PrimaryText), string.Empty);
    public static readonly BindableProperty SecondaryTextProperty = Create(nameof(SecondaryText), string.Empty);
    public static readonly BindableProperty ShowSecondaryTextProperty = Create(nameof(ShowSecondaryText), false);
    public static readonly BindableProperty ShowInfoProperty = Create(nameof(ShowInfo), false);
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

    public event EventHandler? InfoClicked;
    public event EventHandler? DeleteClicked;

    public ListItemRowView() => InitializeComponent();

    public string PrimaryText { get => (string)GetValue(PrimaryTextProperty); set => SetValue(PrimaryTextProperty, value); }
    public string SecondaryText { get => (string)GetValue(SecondaryTextProperty); set => SetValue(SecondaryTextProperty, value); }
    public bool ShowSecondaryText { get => (bool)GetValue(ShowSecondaryTextProperty); set => SetValue(ShowSecondaryTextProperty, value); }
    public bool ShowInfo { get => (bool)GetValue(ShowInfoProperty); set => SetValue(ShowInfoProperty, value); }
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

    private void OnInfoClicked(object? sender, EventArgs e) => InfoClicked?.Invoke(this, EventArgs.Empty);
    private void OnDeleteClicked(object? sender, EventArgs e) => DeleteClicked?.Invoke(this, EventArgs.Empty);

    private static BindableProperty Create<T>(string name, T defaultValue) =>
        BindableProperty.Create(name, typeof(T), typeof(ListItemRowView), defaultValue);
}
