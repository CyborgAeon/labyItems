using Microsoft.Maui.Controls;

#if IOS || MACCATALYST
using CoreGraphics;
using UIKit;
#endif

namespace labyItems.Helpers;

internal static class IosTabBarPlacementHelper
{
    public static void MoveToTop(TabbedPage page)
    {
#if IOS || MACCATALYST
        if (page?.Handler?.PlatformView is not UITabBarController controller)
            return;

        var hostView = controller.View;
        var tabBar = controller.TabBar;
        if (hostView == null || tabBar == null)
            return;

        var bounds = hostView.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var safeTop = hostView.SafeAreaInsets.Top;
        var safeBottom = hostView.SafeAreaInsets.Bottom;
        var tabHeight = tabBar.Frame.Height > 0 ? tabBar.Frame.Height : 49;
        var contentTop = safeTop + tabHeight;
        var contentHeight = Math.Max(0, bounds.Height - contentTop - safeBottom);

        tabBar.Frame = new CGRect(0, safeTop, bounds.Width, tabHeight);

        foreach (var child in controller.ViewControllers ?? Array.Empty<UIViewController>())
        {
            if (child.View == null)
                continue;

            child.AdditionalSafeAreaInsets = new UIEdgeInsets(tabHeight, 0, 0, 0);
            child.View.Frame = new CGRect(0, contentTop, bounds.Width, contentHeight);
        }
#else
        _ = page;
#endif
    }
}
