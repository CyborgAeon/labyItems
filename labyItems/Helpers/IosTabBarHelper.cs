using Microsoft.Maui.Controls;

#if IOS || MACCATALYST
using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using UIKit;
#endif

namespace labyItems.Helpers;

internal static class IosTabBarHelper
{
    public static void EnsurePinnedToTop(TabbedPage page)
    {
#if IOS || MACCATALYST
        if (page is null)
            return;

        MainThread.BeginInvokeOnMainThread(() => MoveToTop(page));
#else
        _ = page;
#endif
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

        if (page.Handler?.PlatformView is UIViewController handlerController)
        {
            var found = FindTabBarController(handlerController);
            if (found != null)
                return found;
        }

        if (page.Window?.Handler?.PlatformView is UIWindow window)
            return FindTabBarController(window.RootViewController);

        return null;
    }

    private static UITabBarController? FindTabBarController(UIViewController? root)
    {
        if (root is null)
            return null;

        if (root is UITabBarController direct)
            return direct;

        for (var current = root; current != null; current = current.ParentViewController)
        {
            if (current is UITabBarController tabBarController)
                return tabBarController;
        }

        if (root.TabBarController is UITabBarController fromTabBarProperty)
            return fromTabBarProperty;

        if (root.PresentedViewController is UIViewController presented)
        {
            var presentedFound = FindTabBarController(presented);
            if (presentedFound != null)
                return presentedFound;
        }

        foreach (var child in root.ChildViewControllers ?? Array.Empty<UIViewController>())
        {
            var found = FindTabBarController(child);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void ConfigureTabController(TabbedPage page, UITabBarController controller)
    {
        controller.CustomizableViewControllers = Array.Empty<UIViewController>();

        var tabBar = controller.TabBar;
        tabBar.Hidden = false;
        tabBar.UserInteractionEnabled = true;
        tabBar.Translucent = false;
        tabBar.ItemPositioning = UITabBarItemPositioning.Fill;
        tabBar.ItemWidth = 0;
        tabBar.ItemSpacing = 0;

        if (controller.Editing)
            controller.SetEditing(false, false);

        if (controller.MoreNavigationController.Editing)
            controller.MoreNavigationController.SetEditing(false, false);

        ApplyReadableTabTitleAppearance(page, controller);
        controller.View.SetNeedsLayout();
        controller.View.LayoutIfNeeded();
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
