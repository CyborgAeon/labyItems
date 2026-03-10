using System.Linq;

namespace labyItems.Helpers;

public static class TabbedPageChromeHelper
{
    public static void ApplyHiddenNavigation(TabbedPage page)
    {
        Shell.SetNavBarIsVisible(page, false);
        NavigationPage.SetHasNavigationBar(page, false);
        NavigationPage.SetHasBackButton(page, false);
        Shell.SetBackButtonBehavior(page, CreateHiddenBackButtonBehavior());
    }

    public static void ConfigureTabPageChrome(Page page)
    {
        Shell.SetNavBarIsVisible(page, false);
        NavigationPage.SetHasNavigationBar(page, false);
        NavigationPage.SetHasBackButton(page, false);
        Shell.SetBackButtonBehavior(page, CreateHiddenBackButtonBehavior());
    }

    public static async Task HandleBackTabSelectionAsync(
        TabbedPage owner,
        Page backTab,
        Func<Page?> fallbackFactory,
        Func<bool> getIsNavigationInProgress,
        Action<bool> setIsNavigationInProgress,
        Page? lastNonBackTab)
    {
        if (getIsNavigationInProgress())
            return;

        setIsNavigationInProgress(true);
        try
        {
            var fallback = lastNonBackTab != null && owner.Children.Contains(lastNonBackTab)
                ? lastNonBackTab
                : fallbackFactory();

            if (fallback != null && !ReferenceEquals(owner.CurrentPage, fallback))
                owner.CurrentPage = fallback;

            if (owner.Navigation.NavigationStack.Count > 1)
            {
                await owner.Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            setIsNavigationInProgress(false);
        }
    }

    private static BackButtonBehavior CreateHiddenBackButtonBehavior()
        => new() { IsVisible = false };
}
