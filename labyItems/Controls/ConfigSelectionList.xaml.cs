using System.Collections;
using System.Windows.Input;

namespace labyItems.Controls;

public partial class ConfigSelectionList : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(ConfigSelectionList),
            default(IEnumerable));

    public static readonly BindableProperty EditCommandProperty =
        BindableProperty.Create(
            nameof(EditCommand),
            typeof(ICommand),
            typeof(ConfigSelectionList),
            default(ICommand));

    public static readonly BindableProperty ShowEditButtonProperty =
        BindableProperty.Create(
            nameof(ShowEditButton),
            typeof(bool),
            typeof(ConfigSelectionList),
            true);

    public static readonly BindableProperty InfoCommandProperty =
        BindableProperty.Create(
            nameof(InfoCommand),
            typeof(ICommand),
            typeof(ConfigSelectionList),
            default(ICommand));

    public static readonly BindableProperty DeleteCommandProperty =
        BindableProperty.Create(
            nameof(DeleteCommand),
            typeof(ICommand),
            typeof(ConfigSelectionList),
            default(ICommand));

    public static readonly BindableProperty ShowInfoButtonProperty =
        BindableProperty.Create(
            nameof(ShowInfoButton),
            typeof(bool),
            typeof(ConfigSelectionList),
            true);

    public ConfigSelectionList()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public bool ShowEditButton
    {
        get => (bool)GetValue(ShowEditButtonProperty);
        set => SetValue(ShowEditButtonProperty, value);
    }

    public ICommand? InfoCommand
    {
        get => (ICommand?)GetValue(InfoCommandProperty);
        set => SetValue(InfoCommandProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public bool ShowInfoButton
    {
        get => (bool)GetValue(ShowInfoButtonProperty);
        set => SetValue(ShowInfoButtonProperty, value);
    }
}
