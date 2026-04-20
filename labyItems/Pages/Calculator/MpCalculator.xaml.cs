namespace labyItems.Pages.Calculator;

using System.Linq;
using labyItems.Helpers;
using Microsoft.Maui.ApplicationModel;

public partial class MpCalculator : TabbedPage
{
    private bool _isHandlingBackTabSelection;
    private Page? _lastNonBackTab;
    private bool _chromeConfigured;
    private readonly Dictionary<string, ContentPage> _tabPlaceholders = new(StringComparer.Ordinal);
    private readonly Func<MpSubmissionPayload, Task>? _onCharacterItemSubmit;
    private MpBasicPage? _basicTab;
    private MpCrewPage? _crewTab;
    private MpThemedayPage? _themeDayTab;

    public MpCalculator(Func<MpSubmissionPayload, Task>? onCharacterItemSubmit = null)
    {
        InitializeComponent();
        _onCharacterItemSubmit = onCharacterItemSubmit;

        ConfigureChromeIfNeeded();
        InitializeTabs();

        CurrentPageChanged += OnCurrentPageChanged;
        CurrentPageChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        SizeChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        HandlerChanged += (_, _) => QueuePlatformTabLayoutRefresh();
        QueuePlatformTabLayoutRefresh();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ConfigureChromeIfNeeded();
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
                    if (_basicTab != null && Children.Contains(_basicTab))
                        return _basicTab;

                    return Children.FirstOrDefault(page => !ReferenceEquals(page, BackTab));
                },
                getIsNavigationInProgress: () => _isHandlingBackTabSelection,
                setIsNavigationInProgress: value => _isHandlingBackTabSelection = value,
                lastNonBackTab: _lastNonBackTab);
            return;
        }

        if (TryRealizeLazyTab(CurrentPage, out var realizedPage))
        {
            _lastNonBackTab = realizedPage;
            return;
        }

        _lastNonBackTab = CurrentPage;
    }

    private void InitializeTabs()
    {
        Children.Clear();
        Children.Add(BackTab);

        var basicPage = GetOrCreateBasicTab();
        Children.Add(basicPage);
        Children.Add(CreateLazyTabPlaceholder("Crew"));
        Children.Add(CreateLazyTabPlaceholder("Theme Day"));

        CurrentPage = basicPage;
        _lastNonBackTab = basicPage;
    }

    private void ConfigureChromeIfNeeded()
    {
        if (_chromeConfigured)
            return;

        TabbedPageChromeHelper.ApplyHiddenNavigation(this);
        TabbedPageChromeHelper.ConfigureTabPageChrome(BackTab);
        _chromeConfigured = true;
    }

    private void ConfigureTabPage(Page page) => TabbedPageChromeHelper.ConfigureTabPageChrome(page);

    private ContentPage CreateLazyTabPlaceholder(string title)
    {
        var placeholder = new ContentPage
        {
            Title = title,
            Content = new Grid()
        };

        ConfigureTabPage(placeholder);
        _tabPlaceholders[title] = placeholder;
        return placeholder;
    }

    private bool TryRealizeLazyTab(Page? selectedPage, out Page realizedPage)
    {
        realizedPage = selectedPage ?? BackTab;
        if (selectedPage == null)
            return false;

        var title = selectedPage.Title ?? string.Empty;
        if (!_tabPlaceholders.TryGetValue(title, out var placeholder) || !ReferenceEquals(placeholder, selectedPage))
            return false;

        realizedPage = title switch
        {
            "Crew" => GetOrCreateCrewTab(),
            "Theme Day" => GetOrCreateThemeDayTab(),
            _ => selectedPage
        };

        var index = Children.IndexOf(placeholder);
        if (index < 0)
            return false;

        Children.RemoveAt(index);
        Children.Insert(index, realizedPage);
        _tabPlaceholders.Remove(title);
        CurrentPage = realizedPage;
        return true;
    }

    private MpBasicPage GetOrCreateBasicTab()
    {
        if (_basicTab != null)
            return _basicTab;

        var page = new MpBasicPage
        {
            Title = "Basic",
            CharacterItemSubmitHandler = _onCharacterItemSubmit
        };

        ConfigureTabPage(page);
        _basicTab = page;
        return page;
    }

    private MpCrewPage GetOrCreateCrewTab()
    {
        if (_crewTab != null)
            return _crewTab;

        var page = new MpCrewPage
        {
            Title = "Crew",
            CharacterItemSubmitHandler = _onCharacterItemSubmit
        };

        ConfigureTabPage(page);
        _crewTab = page;
        return page;
    }

    private MpThemedayPage GetOrCreateThemeDayTab()
    {
        if (_themeDayTab != null)
            return _themeDayTab;

        var page = new MpThemedayPage
        {
            Title = "Theme Day",
            CharacterItemSubmitHandler = _onCharacterItemSubmit
        };

        ConfigureTabPage(page);
        _themeDayTab = page;
        return page;
    }

    private void QueuePlatformTabLayoutRefresh()
    {
        UiDispatchHelper.RunFireAndForget(async () =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(ApplyPlatformTabLayoutTweaks);
            await Task.Delay(60).ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(ApplyPlatformTabLayoutTweaks);
        }, "MP_TAB_LAYOUT_REFRESH");
    }

    partial void ApplyPlatformTabLayoutTweaks();
}
