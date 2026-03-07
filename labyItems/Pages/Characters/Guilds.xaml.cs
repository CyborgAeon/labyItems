using System.Windows.Input;
using labyItems.Pages.Characters.ViewModels;

namespace labyItems.Pages.Characters;

public partial class Guilds : ContentView
{
    public static readonly BindableProperty ShowBackButtonProperty = BindableProperty.Create(
        nameof(ShowBackButton),
        typeof(bool),
        typeof(Guilds),
        false);

    public static readonly BindableProperty BackCommandProperty = BindableProperty.Create(
        nameof(BackCommand),
        typeof(ICommand),
        typeof(Guilds),
        null);

    public Guilds(GuildsVm vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    public bool ShowBackButton
    {
        get => (bool)GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    public ICommand? BackCommand
    {
        get => (ICommand?)GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }
}
