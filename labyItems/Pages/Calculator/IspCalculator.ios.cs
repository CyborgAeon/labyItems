#if IOS || MACCATALYST
using labyItems.Helpers;
using UIKit;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator
{
    partial void ApplyPlatformTabFontSize(double fontSize)
    {
        if (Handler?.PlatformView is not UITabBarController controller)
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

        IosTabBarPlacementHelper.EnsurePinnedToTop(this);
    }
}
#endif
