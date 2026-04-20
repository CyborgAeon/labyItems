using System.Linq;
using labyItems.Pages.Calendar;
using labyItems.Pages.Calculator;
using labyItems.Pages.NonStandard;
using labyItems.Pages.Search;
using labyItems.Pages.Trade;
using labyItems.Services;

namespace labyItems.Pages;

public partial class ItemRoutePage : ContentPage
{
    private bool _isBusy;
    private bool _isMenuOpen;
    private bool _isMenuAnimating;
    private bool _walletButtonLoaded;

    public ItemRoutePage()
    {
        InitializeComponent();
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await SetMenuOpenAsync(false, immediate: true);

        if (!_walletButtonLoaded)
        {
            _walletButtonLoaded = true;
            await RefreshWalletButtonAsync();
        }
    }

    private async void OnToggleMenuClicked(object sender, EventArgs e)
    {
        await SetMenuOpenAsync(!_isMenuOpen);
    }

    private async void OnCloseMenuClicked(object sender, EventArgs e)
    {
        await SetMenuOpenAsync(false);
    }

    private async void OnCloseMenuTapped(object sender, TappedEventArgs e)
    {
        await SetMenuOpenAsync(false);
    }

    private async void OnOpenCalendarClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new UnifiedCalendarPage()));
    }

    private async void OnOpenCalendarTapped(object sender, TappedEventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new UnifiedCalendarPage()));
    }

    private async void OnMakeCharacterClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new labyItems.Pages.Characters.Wizard(null, async () => await Navigation.PopToRootAsync())), "Wizard failed");
    }

    private async void OnCalculateIspClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new IspCalculator(0)));
    }

    private async void OnCreateMpClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new MpCalculator()));
    }

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage()));
    }

    private async void OnGuildSearchClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new GuildSearchPage()));
    }

    private async void OnCreateNonStandardClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new NonStandardCreatePage()));
    }

    private async void OnCharacterWalletClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new Characters.CharacterWalletPage()));
    }

    private async void OnItemWalletClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new ItemWalletPage()));
    }

    private async void OnInitiateTradeClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new TradePage()));
    }

    private async void OnBuildScrollClicked(object sender, EventArgs e)
    {
        await ExecuteNavigationAsync(() => Navigation.PushAsync(new ScrollBuilderPage()));
    }

    private async Task RefreshWalletButtonAsync()
    {
        try
        {
            var any = await Task.Run(() => LiteDbService.GetCharacters().Any());
            if (CharacterWalletButton != null)
                CharacterWalletButton.IsEnabled = any;
        }
        catch
        {
            if (CharacterWalletButton != null)
                CharacterWalletButton.IsEnabled = false;
        }
    }

    private async Task ExecuteNavigationAsync(Func<Task> navigationAction, string errorTitle = "Navigation failed")
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            await SetMenuOpenAsync(false);
            await navigationAction();
        }
        catch (Exception ex)
        {
            await DisplayAlert(errorTitle, ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SetMenuOpenAsync(bool shouldOpen, bool immediate = false)
    {
        if (MenuOverlay == null || MenuPanel == null)
            return;

        if (_isMenuAnimating && !immediate)
            return;

        _isMenuAnimating = true;
        try
        {
            if (shouldOpen)
            {
                MenuPanel.TranslationX = -320;
                MenuOverlay.Opacity = 0;
                MenuOverlay.IsVisible = true;

                if (immediate)
                {
                    MenuPanel.TranslationX = 0;
                    MenuOverlay.Opacity = 1;
                }
                else
                {
                    await Task.WhenAll(
                        MenuPanel.TranslateTo(0, 0, 180, Easing.CubicOut),
                        MenuOverlay.FadeTo(1, 180, Easing.CubicOut));
                }

                _isMenuOpen = true;
                return;
            }

            if (!MenuOverlay.IsVisible)
            {
                _isMenuOpen = false;
                return;
            }

            if (immediate)
            {
                MenuPanel.TranslationX = -320;
                MenuOverlay.Opacity = 0;
            }
            else
            {
                await Task.WhenAll(
                    MenuPanel.TranslateTo(-320, 0, 140, Easing.CubicIn),
                    MenuOverlay.FadeTo(0, 140, Easing.CubicIn));
            }

            MenuOverlay.IsVisible = false;
            _isMenuOpen = false;
        }
        finally
        {
            _isMenuAnimating = false;
        }
    }
}
