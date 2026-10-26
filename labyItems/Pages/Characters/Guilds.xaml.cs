using System.Windows.Input;
using labyItems.Controls;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Helpers;

namespace labyItems.Pages.Characters;

public partial class Guilds : ContentView
{
    public static readonly BindableProperty ShowBackButtonProperty = BindableProperty.Create(
        nameof(ShowBackButton),
        typeof(bool),
        typeof(Guilds),
        false,
        propertyChanged: static (bindable, _, _) =>
        {
            if (bindable is Guilds guilds && guilds.HeaderLeadingView == null)
                guilds.UpdateHeaderLeadingView(null);
        });

    public static readonly BindableProperty BackCommandProperty = BindableProperty.Create(
        nameof(BackCommand),
        typeof(ICommand),
        typeof(Guilds),
        null,
        propertyChanged: static (bindable, _, _) =>
        {
            if (bindable is Guilds guilds && guilds.HeaderLeadingView == null)
                guilds.UpdateHeaderLeadingView(null);
        });

    public static readonly BindableProperty UseTypePillsProperty = BindableProperty.Create(
        nameof(UseTypePills),
        typeof(bool),
        typeof(Guilds),
        false);

    public static readonly BindableProperty ShowSelectedChipsProperty = BindableProperty.Create(
        nameof(ShowSelectedChips),
        typeof(bool),
        typeof(Guilds),
        true);

    public static readonly BindableProperty HeaderLeadingViewProperty = BindableProperty.Create(
        nameof(HeaderLeadingView),
        typeof(View),
        typeof(Guilds),
        null,
        propertyChanged: static (bindable, _, newValue) =>
        {
            if (bindable is Guilds guilds)
                guilds.UpdateHeaderLeadingView(newValue as View);
        });

    private readonly CollectionViewScrollPreserver _scrollPreserver;

    public Guilds(GuildsVm vm)
    {
        InitializeComponent();
        BindingContext = vm;
        UpdateHeaderLeadingView(HeaderLeadingView);
        _scrollPreserver = new CollectionViewScrollPreserver(GuildList);
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

    public bool ShowSelectedChips
    {
        get => (bool)GetValue(ShowSelectedChipsProperty);
        set => SetValue(ShowSelectedChipsProperty, value);
    }

    public View? HeaderLeadingView
    {
        get => (View?)GetValue(HeaderLeadingViewProperty);
        set => SetValue(HeaderLeadingViewProperty, value);
    }

    private void UpdateHeaderLeadingView(View? view)
    {
        if (HeaderLeadingHost == null)
            return;

        if (view == null && ShowBackButton)
        {
            view = new BackNavigationButton
            {
                Command = BackCommand
            };
        }

        HeaderLeadingHost.Content = view;
        HeaderLeadingHost.IsVisible = view != null;
    }

    private async Task OpenGuildDetailAsync(GuildCardVm? item)
    {
        if (item == null)
            return;

        _scrollPreserver.RecordItem(item);
        var page = new Pages.GuildCard.GuildCard(item);
        if (Navigation != null)
        {
            await Navigation.PushAsync(page);
        }
        else if (Shell.Current != null)
        {
            await Shell.Current.Navigation.PushAsync(page);
        }
    }

    private async void OnGuildTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not GuildCardVm item || BindingContext is not GuildsVm vm)
            return;
        if (vm.CanSelectGuilds && item.CanToggleSelection)
            vm.ToggleSelectedCommand.Execute(item);
        else if (!vm.CanSelectGuilds)
            await OpenGuildDetailAsync(item);
    }

    private async void OnGuildInfoTapped(object? sender, TappedEventArgs e)
        => await OpenGuildDetailAsync(e.Parameter as GuildCardVm);
}
