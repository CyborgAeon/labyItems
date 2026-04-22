using System.Linq;
using System.Windows.Input;
using labyItems.Pages;
using labyItems.Pages.Characters;
using labyItems.Pages.Calendar;
using labyItems.Pages.Calculator;
using labyItems.Pages.NonStandard;
using labyItems.Pages.Search;
using labyItems.Pages.Trade;

namespace labyItems.Controls;

public partial class ToolsetNavigationMenu : ContentView
{
    private bool _isMenuAnimating;
    private readonly Color _selectedBackground = Color.FromRgba(127, 29, 29, 22);
    private readonly Color _selectedText = Color.FromArgb("#111827");
    private readonly Color _defaultBackground = Colors.Transparent;
    private readonly Color _defaultText = Color.FromArgb("#111827");

    public ToolsetNavigationMenu()
    {
        OpenMenuCommand = new Command(async () => await SetMenuOpenAsync(true));
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ICommand OpenMenuCommand { get; }

    public static readonly BindableProperty SelectedRouteProperty = BindableProperty.Create(
        nameof(SelectedRoute),
        typeof(string),
        typeof(ToolsetNavigationMenu),
        string.Empty,
        propertyChanged: static (bindable, _, _) =>
        {
            if (bindable is ToolsetNavigationMenu menu)
                menu.ApplySelectionState();
        });

    public string SelectedRoute
    {
        get => (string)GetValue(SelectedRouteProperty);
        set => SetValue(SelectedRouteProperty, value);
    }

    public static readonly BindableProperty ItemsExpandedProperty = BindableProperty.Create(
        nameof(ItemsExpanded),
        typeof(bool),
        typeof(ToolsetNavigationMenu),
        false,
        propertyChanged: static (bindable, _, _) =>
        {
            if (bindable is ToolsetNavigationMenu menu)
                menu.OnPropertyChanged(nameof(ItemsChevronGlyph));
        });

    public bool ItemsExpanded
    {
        get => (bool)GetValue(ItemsExpandedProperty);
        set => SetValue(ItemsExpandedProperty, value);
    }

    public string ItemsChevronGlyph => ItemsExpanded ? "\uf077" : "\uf078";

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await SetMenuOpenAsync(false, immediate: true);
        ApplySelectionState();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private async void OnCloseMenuClicked(object sender, EventArgs e)
        => await SetMenuOpenAsync(false);

    private async void OnCloseMenuTapped(object sender, TappedEventArgs e)
        => await SetMenuOpenAsync(false);

    private async void OnHomeClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ItemRoutePage()));

    private async void OnHomeTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ItemRoutePage()));

    private async void OnOpenCalendarClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new UnifiedCalendarPage()));

    private async void OnOpenCalendarTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new UnifiedCalendarPage()));

    private async void OnMakeCharacterClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new CharacterWalletPage()));

    private async void OnMakeCharacterTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new CharacterWalletPage()));

    private async void OnCalculateIspClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new IspCalculator(0)));

    private async void OnCalculateIspTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new IspCalculator(0)));

    private async void OnCreateMpClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new MpCalculator()));

    private async void OnCreateMpTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new MpCalculator()));

    private async void OnScrollBuilderClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ScrollBuilderPage()));

    private async void OnScrollBuilderTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ScrollBuilderPage()));

    private async void OnSearchClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage()));

    private async void OnSearchTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage()));

    private async void OnGuildSearchClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GuildSearchPage()));

    private async void OnGuildSearchTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GuildSearchPage()));

    private async void OnOpenNonStandardDashboardClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new NonStandardWalletPage()));

    private async void OnOpenNonStandardDashboardTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new NonStandardWalletPage()));

    private async void OnItemWalletClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ItemWalletPage()));

    private async void OnItemWalletTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ItemWalletPage()));

    private async void OnInitiateTradeClicked(object sender, EventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new TradePage()));

    private async void OnInitiateTradeTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new TradePage()));

    private void OnItemsGroupClicked(object sender, EventArgs e)
        => ItemsExpanded = !ItemsExpanded;

    private void OnItemsGroupTapped(object sender, TappedEventArgs e)
        => ItemsExpanded = !ItemsExpanded;

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page ?? Application.Current?.MainPage;
        if (page != null)
            await page.DisplayAlert("Settings", "Settings is not wired in yet.", "OK");
    }

    private async void OnSettingsTapped(object sender, TappedEventArgs e)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page ?? Application.Current?.MainPage;
        if (page != null)
            await page.DisplayAlert("Settings", "Settings is not wired in yet.", "OK");
    }

    private async Task ExecuteNavigationAsync(Func<Task> navigationAction, string errorTitle = "Navigation failed")
    {
        try
        {
            await SetMenuOpenAsync(false);
            await navigationAction();
        }
        catch (Exception ex)
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page ?? Application.Current?.MainPage;
            if (page != null)
                await page.DisplayAlert(errorTitle, ex.Message, "OK");
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

                return;
            }

            if (!MenuOverlay.IsVisible)
                return;

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
        }
        finally
        {
            _isMenuAnimating = false;
        }
    }

    private void ApplySelectionState()
    {
        ItemsExpanded = IsItemsRoute(SelectedRoute);

        ApplyRowState(HomeRow, ToolsetRouteKeys.Home);
        ApplyRowState(CharactersRow, ToolsetRouteKeys.Characters);
        ApplyRowState(ItemsRow, SelectedRoute, IsItemsRoute);
        ApplyRowState(CalculateIspRow, ToolsetRouteKeys.ItemsIsp);
        ApplyRowState(CreateMpRow, ToolsetRouteKeys.ItemsMp);
        ApplyRowState(ScrollBuilderRow, ToolsetRouteKeys.ScrollBuilder);
        ApplyRowState(ItemWalletRow, ToolsetRouteKeys.ItemsWallet);
        ApplyRowState(CreationsRow, ToolsetRouteKeys.NonStandard);
        ApplyRowState(CalendarRow, ToolsetRouteKeys.Calendar);
        ApplyRowState(SearchRow, ToolsetRouteKeys.Search);
        ApplyRowState(GuildsRow, ToolsetRouteKeys.Guilds);
        ApplyRowState(TradeRow, ToolsetRouteKeys.Trade);
        ApplyRowState(SettingsRow, ToolsetRouteKeys.Settings);
    }

    private void ApplyRowState(Border? border, string routeKey)
        => ApplyRowState(border, SelectedRoute, key => string.Equals(key, routeKey, StringComparison.OrdinalIgnoreCase));

    private void ApplyRowState(Border? border, string selectedRoute, Func<string, bool> matcher)
    {
        if (border == null)
            return;

        var isSelected = matcher(selectedRoute ?? string.Empty);
        border.BackgroundColor = isSelected ? _selectedBackground : _defaultBackground;

        foreach (var button in GetDescendants<Button>(border))
            button.TextColor = isSelected ? _selectedText : _defaultText;

        foreach (var label in GetDescendants<Label>(border))
            label.TextColor = isSelected ? _selectedText : _defaultText;
    }

    private static bool IsItemsRoute(string? route)
        => string.Equals(route, ToolsetRouteKeys.ItemsIsp, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.ItemsMp, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.ScrollBuilder, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.ItemsWallet, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<T> GetDescendants<T>(Element root) where T : Element
    {
        foreach (var child in root.LogicalChildren.OfType<Element>())
        {
            if (child is T match)
                yield return match;

            foreach (var nested in GetDescendants<T>(child))
                yield return nested;
        }
    }
}
