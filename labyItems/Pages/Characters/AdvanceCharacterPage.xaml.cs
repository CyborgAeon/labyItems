using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using System.Linq;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using AndroidConfig = Microsoft.Maui.Controls.PlatformConfiguration.Android;
using AndroidToolbarPlacement = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
namespace labyItems.Pages.Characters;

public partial class AdvanceCharacterPage : Microsoft.Maui.Controls.TabbedPage
{
    private readonly AdvanceCharacterVm _vm;
    private readonly AdvanceCharacterDetailsTabVm _detailsTabVm;
    private readonly AdvanceCharacterSpellsTabVm _spellsTabVm;
    private readonly AdvanceCharacterMiraclesTabVm _miraclesTabVm;
    private readonly AdvanceCharacterEvocationsTabVm _evocationsTabVm;
    private Page? _lastNonBackTab;
    private bool _isHandlingBackTabSelection;

    public AdvanceCharacterPage(Character character)
        : this(LiteDbService.ToDraft(character) ?? new CharacterDraft())
    {
        Title = string.Empty;
    }

    public AdvanceCharacterPage(CharacterDraft draft)
    {
        InitializeComponent();
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);
        Shell.SetBackButtonBehavior(this, CreateHiddenBackButtonBehavior());
        this.On<AndroidConfig>().SetIsSwipePagingEnabled(false);
        this.On<AndroidConfig>().SetToolbarPlacement(AndroidToolbarPlacement.Top);
        Title = string.Empty;
        var serviceProvider = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services;
        _vm = new AdvanceCharacterVm(
            draft,
            draftStore: new CharacterDraftStore(draft),
            domainService: serviceProvider?.GetService<ICharacterAdvancementDomainService>(),
            tabVisibilityService: serviceProvider?.GetService<IAdvancementTabVisibilityService>(),
            validationService: serviceProvider?.GetService<IAdvancementValidationService>(),
            exportService: serviceProvider?.GetService<IExportService>(),
            fileService: serviceProvider?.GetService<IFileService>());

        _detailsTabVm = new AdvanceCharacterDetailsTabVm(_vm);
        _spellsTabVm = new AdvanceCharacterSpellsTabVm(_vm);
        _miraclesTabVm = new AdvanceCharacterMiraclesTabVm(_vm);
        _evocationsTabVm = new AdvanceCharacterEvocationsTabVm(_vm);

        BindingContext = _vm;
        BindTabContexts();
        CurrentPageChanged += OnCurrentPageChanged;
        ConfigureTabPageChrome(BackTab);
        ConfigureTabPageChrome(DetailsTab);
        ConfigureTabPageChrome(SpellsTab);
        ConfigureTabPageChrome(MiraclesTab);
        ConfigureTabPageChrome(EvocsTab);

        AbilitySearch.RemoteSearchProvider = _detailsTabVm.SearchAbilityOptionsAsync;
        ApplyTabVisibility();
        QueuePlatformTabLayoutRefresh();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await _vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ADVANCE][INIT] InitializeAsync failed: {ex}");
            }
            finally
            {
                ApplyTabVisibility();
            }
        });
    }

    private void ApplyTabVisibility()
    {
        var currentBeforeUpdate = CurrentPage;
        var desiredTabs = new List<Page> { BackTab, DetailsTab };
        if (_vm.ShowSpellsTab)
            desiredTabs.Add(SpellsTab);
        if (_vm.ShowMiraclesTab)
            desiredTabs.Add(MiraclesTab);
        if (_vm.ShowEvocationsTab)
            desiredTabs.Add(EvocsTab);

        foreach (var page in Children.ToList())
        {
            if (!desiredTabs.Contains(page))
                Children.Remove(page);
        }

        for (var i = 0; i < desiredTabs.Count; i++)
        {
            var page = desiredTabs[i];
            ConfigureTabPageChrome(page);
            if (!Children.Contains(page))
            {
                Children.Insert(i, page);
                BindTabContext(page);
                continue;
            }

            var currentIndex = Children.IndexOf(page);
            if (currentIndex != i)
            {
                Children.Remove(page);
                Children.Insert(i, page);
            }
        }

        var fallbackPage = ResolveFallbackPage(currentBeforeUpdate, desiredTabs);
        if (fallbackPage != null && !ReferenceEquals(CurrentPage, fallbackPage))
            CurrentPage = fallbackPage;

        if (CurrentPage != null && !ReferenceEquals(CurrentPage, BackTab))
            _lastNonBackTab = CurrentPage;

        QueuePlatformTabLayoutRefresh();
    }

    private void BindTabContexts()
    {
        BindTabContext(DetailsTab);
        BindTabContext(SpellsTab);
        BindTabContext(MiraclesTab);
        BindTabContext(EvocsTab);
    }

    private void BindTabContext(Page page)
    {
        if (ReferenceEquals(page, BackTab))
            return;

        if (ReferenceEquals(page, DetailsTab))
            page.BindingContext = _detailsTabVm;
        else if (ReferenceEquals(page, SpellsTab))
            page.BindingContext = _spellsTabVm;
        else if (ReferenceEquals(page, MiraclesTab))
            page.BindingContext = _miraclesTabVm;
        else if (ReferenceEquals(page, EvocsTab))
            page.BindingContext = _evocationsTabVm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PersistDraft();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);
        Shell.SetBackButtonBehavior(this, CreateHiddenBackButtonBehavior());

        foreach (var page in Children)
            ConfigureTabPageChrome(page);

        QueuePlatformTabLayoutRefresh();
    }

    private Page? ResolveFallbackPage(Page? currentBeforeUpdate, IReadOnlyCollection<Page> desiredTabs)
    {
        if (currentBeforeUpdate != null
            && desiredTabs.Contains(currentBeforeUpdate)
            && !ReferenceEquals(currentBeforeUpdate, BackTab))
        {
            return currentBeforeUpdate;
        }

        if (_lastNonBackTab != null && desiredTabs.Contains(_lastNonBackTab))
            return _lastNonBackTab;

        if (desiredTabs.Contains(DetailsTab))
            return DetailsTab;

        return desiredTabs.FirstOrDefault(page => !ReferenceEquals(page, BackTab))
               ?? desiredTabs.FirstOrDefault();
    }

    private void OnCurrentPageChanged(object? sender, EventArgs e)
    {
        if (CurrentPage == null)
            return;

        if (ReferenceEquals(CurrentPage, BackTab))
        {
            _ = HandleBackTabSelectionAsync();
            return;
        }

        _lastNonBackTab = CurrentPage;
    }

    private async Task HandleBackTabSelectionAsync()
    {
        if (_isHandlingBackTabSelection)
            return;

        _isHandlingBackTabSelection = true;
        try
        {
            var returnTab = _lastNonBackTab != null && Children.Contains(_lastNonBackTab)
                ? _lastNonBackTab
                : (Children.Contains(DetailsTab) ? DetailsTab : Children.FirstOrDefault());

            if (returnTab != null && !ReferenceEquals(CurrentPage, returnTab))
                CurrentPage = returnTab;

            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isHandlingBackTabSelection = false;
        }
    }

    private async void OnViewSpellDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button)
            return;

        if (button.CommandParameter is not SpellEntryVm entry)
            return;

        var spell = _vm.FindSpellByName(entry.Draft.Name);
        if (spell == null)
            return;

        await Navigation.PushAsync(new SpellCardPage(spell));
    }

    private async void OnViewMiracleDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button)
            return;

        if (button.CommandParameter is not MiracleEntryVm entry)
            return;

        var miracle = _vm.FindMiracleByName(entry.Draft.Name);
        if (miracle == null)
            return;

        await Navigation.PushAsync(new MiracleCardPage(miracle));
    }

    private async void OnViewEvocationDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button)
            return;

        if (button.CommandParameter is not EvocationEntryVm entry)
            return;

        var evocation = _vm.FindEvocationByName(entry.Draft.Name);
        if (evocation == null)
            return;

        await Navigation.PushAsync(new EvocationCardPage(evocation));
    }

    private async void OnViewAbilityDetailsClicked(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Button button)
            return;

        if (button.CommandParameter is not AbilityEntryVm entry)
            return;

        var ability = await _vm.FindAbilityByNameAsync(entry.Name);
        if (ability == null)
            return;

        await Navigation.PushAsync(new AbilityCardPage(ability));
    }

    private static void ConfigureTabPageChrome(Page page)
    {
        Shell.SetNavBarIsVisible(page, false);
        NavigationPage.SetHasNavigationBar(page, false);
        NavigationPage.SetHasBackButton(page, false);
        Shell.SetBackButtonBehavior(page, CreateHiddenBackButtonBehavior());
    }

    private static BackButtonBehavior CreateHiddenBackButtonBehavior()
        => new() { IsVisible = false };

    private void QueuePlatformTabLayoutRefresh()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(10);
            ApplyPlatformTabLayoutTweaks();
            await Task.Delay(60);
            ApplyPlatformTabLayoutTweaks();
        });
    }

    partial void ApplyPlatformTabLayoutTweaks();
}
