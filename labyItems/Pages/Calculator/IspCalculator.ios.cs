#if IOS || MACCATALYST
using labyItems.Helpers;
using UIKit;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator
{
    partial void ApplyPlatformTabFontSize(double fontSize)
    {
        IosTabBarHelper.EnsurePinnedToTop(this);

        var controller = ResolveTabBarController();
        if (controller is null)
            return;

        var items = controller.TabBar.Items;
        if (items is null)
            return;

        var attributes = new UIStringAttributes
        {
            Font = UIFont.SystemFontOfSize((nfloat)fontSize),
        };

        foreach (var item in items)
        {
            item.SetTitleTextAttributes(attributes, UIControlState.Normal);
            item.SetTitleTextAttributes(attributes, UIControlState.Selected);
        }
    }

    private UITabBarController? ResolveTabBarController()
    {
        if (Handler?.PlatformView is UITabBarController direct)
            return direct;

        if (Handler?.PlatformView is UIViewController viewController)
            return viewController.TabBarController;

        return null;
    }
}
#endif
