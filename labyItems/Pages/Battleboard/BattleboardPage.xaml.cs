using labyItems.Models.Characters;
using labyItems.Pages.Battleboard.ViewModels;
using Microsoft.Maui.ApplicationModel;
using System.Linq;

namespace labyItems.Pages.Battleboard;

public partial class BattleboardPage : TabbedPage
{
    private readonly BattleboardViewModel _vm;
    private bool _isModalOpen;
    private bool _isBackTabNavigationInProgress;
    private Page? _lastNonBackTab;

    public BattleboardPage(CharacterDraft draft)
    {
        InitializeComponent();
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);
        Shell.SetBackButtonBehavior(this, CreateHiddenBackButtonBehavior());

        _vm = new BattleboardViewModel(draft);
        BindingContext = _vm;

        Title = string.Empty;
        CurrentPageChanged += OnCurrentPageChanged;
        ConfigureTabPageChrome(BackTab);
        ConfigureTabPageChrome(LifeTab);
        ConfigureTabPageChrome(InnatesTab);
        ConfigureTabPageChrome(ResistanceTab);
        ConfigureTabPageChrome(CastingTab);
        ConfigureTabPageChrome(DetailsTab);
        ApplyTabVisibility();
        QueuePlatformTabLayoutRefresh();
    }

    private void ApplyTabVisibility()
    {
        var desiredTabs = new List<Page> { BackTab, LifeTab, InnatesTab, ResistanceTab };
        if (_vm.HasCastingTab)
            desiredTabs.Add(CastingTab);
        desiredTabs.Add(DetailsTab);

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
                continue;
            }

            var currentIndex = Children.IndexOf(page);
            if (currentIndex == i)
                continue;

            Children.Remove(page);
            Children.Insert(i, page);
        }

        CurrentPage = Children.Contains(LifeTab) ? LifeTab : Children.FirstOrDefault();
        if (CurrentPage != null && !ReferenceEquals(CurrentPage, BackTab))
            _lastNonBackTab = CurrentPage;

        QueuePlatformTabLayoutRefresh();
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

    private void OnCurrentPageChanged(object? sender, EventArgs e)
    {
        if (CurrentPage == null)
            return;

        if (ReferenceEquals(CurrentPage, BackTab))
        {
            _ = NavigateBackFromTabAsync();
            return;
        }

        _lastNonBackTab = CurrentPage;
    }

    private async Task NavigateBackFromTabAsync()
    {
        if (_isBackTabNavigationInProgress)
            return;

        _isBackTabNavigationInProgress = true;
        try
        {
            var fallback = _lastNonBackTab != null && Children.Contains(_lastNonBackTab)
                ? _lastNonBackTab
                : (Children.Contains(LifeTab) ? LifeTab : Children.FirstOrDefault());

            if (fallback != null && !ReferenceEquals(CurrentPage, fallback))
                CurrentPage = fallback;

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
            _isBackTabNavigationInProgress = false;
        }
    }

    private async void OnLifeTapped(object sender, TappedEventArgs e)
    {
        if (_isModalOpen)
            return;

        object? target = null;
        if (sender is BindableObject bo)
            target = bo.BindingContext;

        if (target == null && e?.Parameter != null)
            target = e.Parameter;

        if (target == null)
            return;

        _isModalOpen = true;
        await Navigation.PushModalAsync(new BattleboardDamageModalPage(_vm, target));
        _isModalOpen = false;
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
