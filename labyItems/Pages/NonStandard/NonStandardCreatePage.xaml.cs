using System.Linq;
using labyItems.Helpers;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardCreatePage : TabbedPage
{
    private bool _isHandlingBackTabSelection;
    private Page? _lastNonBackTab;

    public NonStandardCreatePage()
    {
        InitializeComponent();
        TabbedPageChromeHelper.ApplyHiddenNavigation(this);
        foreach (var page in Children)
            TabbedPageChromeHelper.ConfigureTabPageChrome(page);

        CurrentPageChanged += OnCurrentPageChanged;
        CurrentPage = ClassTab;
        _lastNonBackTab = ClassTab;

        WalletTab.EditRequested += OnWalletEditRequested;

        CurrentPageChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        SizeChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        HandlerChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        QueuePlatformTabLayoutRefresh();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        TabbedPageChromeHelper.ApplyHiddenNavigation(this);
        foreach (var page in Children)
            TabbedPageChromeHelper.ConfigureTabPageChrome(page);

        QueuePlatformTabLayoutRefresh();
    }

    private async void OnWalletEditRequested(NonStandardWalletEntry entry)
    {
        if (entry.EntityType == NonStandardEntityType.CharacterClass)
        {
            CurrentPage = ClassTab;
            await ClassTab.LoadFromWalletEntryAsync(entry);
            return;
        }

        var target = ResolveLegacyTab(entry.EntityType);
        if (target == null)
            return;

        CurrentPage = target;
        await target.LoadFromWalletEntryAsync(entry);
    }

    private void OnCurrentPageChanged(object? sender, EventArgs e)
    {
        if (CurrentPage == null)
            return;

        if (ReferenceEquals(CurrentPage, BackTab))
        {
            _ = TabbedPageChromeHelper.HandleBackTabSelectionAsync(
                owner: this,
                backTab: BackTab,
                fallbackFactory: () =>
                {
                    if (Children.Contains(ClassTab))
                        return ClassTab;

                    return Children.FirstOrDefault(page => !ReferenceEquals(page, BackTab));
                },
                getIsNavigationInProgress: () => _isHandlingBackTabSelection,
                setIsNavigationInProgress: value => _isHandlingBackTabSelection = value,
                lastNonBackTab: _lastNonBackTab);
            return;
        }

        _lastNonBackTab = CurrentPage;
    }

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

    private NonStandardLegacyCreatePage? ResolveLegacyTab(NonStandardEntityType entityType)
    {
        return entityType switch
        {
            NonStandardEntityType.CharacterRace => RaceTab,
            NonStandardEntityType.Spell => SpellTab,
            NonStandardEntityType.Miracle => MiracleTab,
            NonStandardEntityType.Evocation => EvocationTab,
            NonStandardEntityType.Ability => AbilityTab,
            _ => null
        };
    }
}
