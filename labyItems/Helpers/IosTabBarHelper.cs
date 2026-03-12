using Microsoft.Maui.Controls;

#if IOS || MACCATALYST
using System;
using Microsoft.Maui.Devices;
using UIKit;
#endif

namespace labyItems.Helpers;

internal static class IosTabBarHelper
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

        ConfigureTabController(page, tabBarController);
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

    private static void ConfigureTabController(TabbedPage page, UITabBarController controller)
    {
        controller.CustomizableViewControllers = Array.Empty<UIViewController>();

        var tabBar = controller.TabBar;
        tabBar.Hidden = false;
        tabBar.UserInteractionEnabled = true;
        tabBar.ItemPositioning = UITabBarItemPositioning.Fill;
        tabBar.ItemWidth = 0;
        tabBar.ItemSpacing = 0;

        if (controller.Editing)
            controller.SetEditing(false, false);

        if (controller.MoreNavigationController.Editing)
            controller.MoreNavigationController.SetEditing(false, false);

        ApplyReadableTabTitleAppearance(page, controller);
    }

    private static void ApplyReadableTabTitleAppearance(TabbedPage page, UITabBarController controller)
    {
        var tabBar = controller.TabBar;
        var items = tabBar.Items ?? Array.Empty<UITabBarItem>();
        if (items.Length == 0)
            return;

        var fontSize = CalculateReadableFontSize(page, items.Length);
        var normalAttributes = new UIStringAttributes
        {
            Font = UIFont.SystemFontOfSize(fontSize, UIFontWeight.Semibold),
        };
        var selectedAttributes = new UIStringAttributes
        {
            Font = UIFont.SystemFontOfSize(fontSize, UIFontWeight.Bold),
        };

        foreach (var item in items)
        {
            item.SetTitleTextAttributes(normalAttributes, UIControlState.Normal);
            item.SetTitleTextAttributes(selectedAttributes, UIControlState.Selected);
        }

        var appearance = tabBar.StandardAppearance?.Copy() as UITabBarAppearance
            ?? new UITabBarAppearance();
        ApplyTextAppearance(appearance.StackedLayoutAppearance, normalAttributes, selectedAttributes);
        ApplyTextAppearance(appearance.InlineLayoutAppearance, normalAttributes, selectedAttributes);
        ApplyTextAppearance(appearance.CompactInlineLayoutAppearance, normalAttributes, selectedAttributes);

        tabBar.StandardAppearance = appearance;
        if (OperatingSystem.IsIOSVersionAtLeast(15) || OperatingSystem.IsMacCatalystVersionAtLeast(15))
            tabBar.ScrollEdgeAppearance = appearance;
    }

    private static void ApplyTextAppearance(
        UITabBarItemAppearance itemAppearance,
        UIStringAttributes normalAttributes,
        UIStringAttributes selectedAttributes)
    {
        itemAppearance.Normal.TitleTextAttributes = normalAttributes;
        itemAppearance.Selected.TitleTextAttributes = selectedAttributes;
        itemAppearance.Focused.TitleTextAttributes = selectedAttributes;
    }

    private static nfloat CalculateReadableFontSize(TabbedPage page, int tabCount)
    {
        var width = page.Width > 0
            ? page.Width
            : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        var perTabWidth = width / Math.Max(1, tabCount);

        // Keep tab labels legible on iPhone while avoiding clipping on narrow layouts.
        const double minFont = 14;
        const double maxFont = 17;
        const double minWidth = 68;
        const double maxWidth = 140;
        var clampedWidth = Math.Clamp(perTabWidth, minWidth, maxWidth);
        var ratio = (clampedWidth - minWidth) / (maxWidth - minWidth);
        return (nfloat)Math.Round(minFont + ((maxFont - minFont) * ratio), 1);
    }
#endif
}
