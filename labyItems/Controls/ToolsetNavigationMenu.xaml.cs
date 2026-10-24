using System.Linq;
using System.Windows.Input;
using labyItems.Models.Search;
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
        false);

    public bool ItemsExpanded
    {
        get => (bool)GetValue(ItemsExpandedProperty);
        set => SetValue(ItemsExpandedProperty, value);
    }

    public static readonly BindableProperty SearchExpandedProperty = BindableProperty.Create(
        nameof(SearchExpanded),
        typeof(bool),
        typeof(ToolsetNavigationMenu),
        false);

    public bool SearchExpanded
    {
        get => (bool)GetValue(SearchExpandedProperty);
        set => SetValue(SearchExpandedProperty, value);
    }

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
    private async void OnHomeTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ItemRoutePage()));
    private async void OnOpenCalendarTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new UnifiedCalendarPage()));
    private async void OnMakeCharacterTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new CharacterWalletPage()));
    private async void OnCalculateIspTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new IspCalculator(0)));
    private async void OnCreateMpTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new MpCalculator()));
    private async void OnScrollBuilderTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ScrollBuilderPage()));
    private async void OnCraftingTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new MakeSheetPage()));

    private void OnSearchGroupTapped(object sender, TappedEventArgs e)
        => SearchExpanded = !SearchExpanded;
    private void OnItemsGroupTapped(object sender, TappedEventArgs e)
        => ItemsExpanded = !ItemsExpanded;
    private async void OnGlobalSearchTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage()));
    private async void OnSearchSpellsTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage(new GlobalSearchPickerOptions {
            Title = "Wizard's Grimoire",
            Kind = GlobalSearchKind.Spell,
            SelectionOptions = new SelectionOptions {SelectionType = SelectionType.None},
            IsFilterAvailable = _ => true,
            AllowAllKinds = false
        }, GlobalSearchKind.Spell)));
    private async void OnSearchMiraclesTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage(GlobalSearchKind.Miracle)));
    private async void OnSearchEvocationsTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage(GlobalSearchKind.Evocation)));
    private async void OnSearchNeuronicsTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage(GlobalSearchKind.Neuronic)));
    private async void OnSearchClassesTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ClassSearchPage()));
    private async void OnSearchRacesTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new RaceSearchPage()));
    private async void OnSearchAbilitiesTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage(GlobalSearchKind.Ability)));
    private async void OnSearchMultiClassesTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage(GlobalSearchKind.MultiClass)));
    private async void OnSearchMultiRacesTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GlobalSearchPage(GlobalSearchKind.MultiRace)));
    private async void OnSearchGuildsTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new GuildSearchPage()));
    private async void OnOpenNonStandardDashboardTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new NonStandardWalletPage()));
    private async void OnItemWalletTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new ItemWalletPage()));
    private async void OnInitiateTradeTapped(object sender, TappedEventArgs e)
        => await ExecuteNavigationAsync(() => Navigation.PushAsync(new TradePage()));

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
        SearchExpanded = IsSearchRoute(SelectedRoute);

        ApplyRowState(HomeRow, ToolsetRouteKeys.Home);
        ApplyRowState(CharactersRow, ToolsetRouteKeys.Characters);
        ApplyRowState(ItemsRow, SelectedRoute, IsItemsRoute);
        ApplyRowState(CalculateIspRow, ToolsetRouteKeys.ItemsIsp);
        ApplyRowState(CreateMpRow, ToolsetRouteKeys.ItemsMp);
        ApplyRowState(ScrollBuilderRow, ToolsetRouteKeys.ScrollBuilder);
        ApplyRowState(CraftingRow, ToolsetRouteKeys.ItemsCrafting);
        ApplyRowState(ItemWalletRow, ToolsetRouteKeys.ItemsWallet);
        ApplyRowState(CreationsRow, ToolsetRouteKeys.NonStandard);
        ApplyRowState(CalendarRow, ToolsetRouteKeys.Calendar);
        ApplyRowState(SearchRow, SelectedRoute, IsSearchRoute);
        ApplyRowState(SearchGlobalRow, ToolsetRouteKeys.SearchGlobal);
        ApplyRowState(SearchSpellsRow, ToolsetRouteKeys.SearchSpells);
        ApplyRowState(SearchMiraclesRow, ToolsetRouteKeys.SearchMiracles);
        ApplyRowState(SearchEvocationsRow, ToolsetRouteKeys.SearchEvocations);
        ApplyRowState(SearchNeuronicsRow, ToolsetRouteKeys.SearchNeuronics);
        ApplyRowState(SearchClassesRow, ToolsetRouteKeys.SearchClasses);
        ApplyRowState(SearchRacesRow, ToolsetRouteKeys.SearchRaces);
        ApplyRowState(SearchAbilitiesRow, ToolsetRouteKeys.SearchAbilities);
        ApplyRowState(SearchGuildsRow, ToolsetRouteKeys.SearchGuilds);
        ApplyRowState(SearchMultiClassesRow, ToolsetRouteKeys.SearchMultiClasses);
        ApplyRowState(SearchMultiRacesRow, ToolsetRouteKeys.SearchMultiRaces);
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
           || string.Equals(route, ToolsetRouteKeys.ItemsCrafting, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.ScrollBuilder, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.ItemsWallet, StringComparison.OrdinalIgnoreCase);

    private static bool IsSearchRoute(string? route)
        => string.Equals(route, ToolsetRouteKeys.Search, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchGlobal, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchSpells, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchMiracles, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchEvocations, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchNeuronics, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchClasses, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchRaces, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchAbilities, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchGuilds, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchMultiClasses, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.SearchMultiRaces, StringComparison.OrdinalIgnoreCase)
           || string.Equals(route, ToolsetRouteKeys.Guilds, StringComparison.OrdinalIgnoreCase);

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
