using System.Collections;

namespace labyItems.Controls;

public partial class LevelValueStripView : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(LevelValueStripView), string.Empty);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle), typeof(string), typeof(LevelValueStripView), string.Empty,
        propertyChanged: (bindable, _, _) => ((LevelValueStripView)bindable).OnPropertyChanged(nameof(HasSubtitle)));

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(LevelValueStripView));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public event EventHandler? CellTapped;

    public LevelValueStripView()
    {
        InitializeComponent();
    }

    private void OnCellTapped(object? sender, TappedEventArgs e) => CellTapped?.Invoke(this, EventArgs.Empty);
}
