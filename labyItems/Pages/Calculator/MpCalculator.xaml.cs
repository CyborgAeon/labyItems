namespace labyItems.Pages.Calculator;

using System.Linq;
using labyItems.Helpers;
using Microsoft.Maui.ApplicationModel;

public partial class MpCalculator : TabbedPage
{
    private bool _isHandlingBackTabSelection;
    private Page? _lastNonBackTab;

    public MpCalculator(Func<MpSubmissionPayload, Task>? onCharacterItemSubmit = null)
    {
        InitializeComponent();
        TabbedPageChromeHelper.ApplyHiddenNavigation(this);
        foreach (var page in Children)
            TabbedPageChromeHelper.ConfigureTabPageChrome(page);

        if (onCharacterItemSubmit != null)
        {
            BasicTab.CharacterItemSubmitHandler = onCharacterItemSubmit;
            CrewTab.CharacterItemSubmitHandler = onCharacterItemSubmit;
            ThemeDayTab.CharacterItemSubmitHandler = onCharacterItemSubmit;
        }

        CurrentPageChanged += OnCurrentPageChanged;
        CurrentPage = BasicTab;
        _lastNonBackTab = BasicTab;

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
                    if (Children.Contains(BasicTab))
                        return BasicTab;

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
}
