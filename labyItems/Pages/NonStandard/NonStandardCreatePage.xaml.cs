using System.Linq;
using labyItems.Helpers;
using labyItems.Services;

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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        TabbedPageChromeHelper.ApplyHiddenNavigation(this);
        foreach (var page in Children)
            TabbedPageChromeHelper.ConfigureTabPageChrome(page);
    }

    private async void OnWalletEditRequested(NonStandardWalletEntry entry)
    {
        if (entry.EntityType == NonStandardEntityType.CharacterClass)
        {
            CurrentPage = ClassTab;
            await ClassTab.LoadFromWalletEntryAsync(entry);
            return;
        }

        CurrentPage = LegacyTab;
        await LegacyTab.LoadFromWalletEntryAsync(entry);
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
}
