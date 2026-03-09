using Microsoft.Maui.Controls;

#if IOS || MACCATALYST
using UIKit;
#endif

namespace labyItems.Helpers;

internal static class IosTabBarPlacementHelper
{
    public static void EnsurePinnedToTop(TabbedPage page)
    {
        MoveToTop(page);
    }

    public static void MoveToTop(TabbedPage page)
    {
#if IOS || MACCATALYST
        if (page is null)
            return;

        var tabBarController = ResolveTabBarController(page);
        if (tabBarController is null)
            return;

        ConfigureTabController(tabBarController);
#else
        _ = page;
#endif
    }

#if IOS || MACCATALYST
    private static UITabBarController? ResolveTabBarController(TabbedPage page)
    {
        if (page.Handler?.PlatformView is UITabBarController direct)
            return direct;

        if (page.Handler?.PlatformView is not UIViewController viewController)
            return null;

        for (var current = viewController; current != null; current = current.ParentViewController)
        {
            if (current is UITabBarController tabBarController)
                return tabBarController;
        }

        return viewController.TabBarController;
    }

    private static void ConfigureTabController(UITabBarController controller)
    {
        controller.CustomizableViewControllers = Array.Empty<UIViewController>();

        var tabBar = controller.TabBar;
        tabBar.Hidden = false;
        tabBar.UserInteractionEnabled = true;

        if (controller.Editing)
            controller.SetEditing(false, false);

        if (controller.MoreNavigationController.Editing)
            controller.MoreNavigationController.SetEditing(false, false);
    }
#endif
}
