using System.Windows.Input;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Helpers;

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

    public static readonly BindableProperty UseTypePillsProperty = BindableProperty.Create(
        nameof(UseTypePills),
        typeof(bool),
        typeof(Guilds),
        false);

    public Guilds(GuildsVm vm)
    {
        InitializeComponent();
        BindingContext = vm;
        ToggleExpandedCommand = new Command<GuildCardVm>(item => _ = HandleToggleExpandedAsync(item));
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

    public bool UseTypePills
    {
        get => (bool)GetValue(UseTypePillsProperty);
        set => SetValue(UseTypePillsProperty, value);
    }

    public ICommand ToggleExpandedCommand { get; }

    private async Task HandleToggleExpandedAsync(GuildCardVm? item)
    {
        if (item == null || BindingContext is not GuildsVm vm)
            return;

        var shouldScrollToTop = !item.IsExpanded;
        vm.ToggleExpandedCommand.Execute(item);

        if (!shouldScrollToTop)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
            GuildList?.ScrollTo(item, position: ScrollToPosition.Start, animate: true));
    }
}
