using System.Windows.Input;

namespace labyItems.Controls;

public partial class SearchFilterBar : ContentView
{
    public static readonly BindableProperty SearchTextProperty = BindableProperty.Create(
        nameof(SearchText),
        typeof(string),
        typeof(SearchFilterBar),
        string.Empty,
        BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(SearchFilterBar),
        "Search...");

    public static readonly BindableProperty OpenFiltersCommandProperty = BindableProperty.Create(
        nameof(OpenFiltersCommand),
        typeof(ICommand),
        typeof(SearchFilterBar));

    public SearchFilterBar() => InitializeComponent();

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public ICommand? OpenFiltersCommand
    {
        get => (ICommand?)GetValue(OpenFiltersCommandProperty);
        set => SetValue(OpenFiltersCommandProperty, value);
    }
}
