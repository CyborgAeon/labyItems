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

    public Guilds(GuildsVm vm)
    {
        InitializeComponent();
        BindingContext = vm;
        ToggleExpandedCommand = new Command<GuildCardVm>(item => _ = HandleToggleExpandedAsync(item));
        UpdateHeaderLeadingView(HeaderLeadingView);
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

    public View? HeaderLeadingView
    {
        get => (View?)GetValue(HeaderLeadingViewProperty);
        set => SetValue(HeaderLeadingViewProperty, value);
    }

    public ICommand ToggleExpandedCommand { get; }

    private void UpdateHeaderLeadingView(View? view)
    {
        if (HeaderLeadingHost == null)
            return;

        HeaderLeadingHost.Content = view;
        HeaderLeadingHost.IsVisible = view != null;
    }

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
