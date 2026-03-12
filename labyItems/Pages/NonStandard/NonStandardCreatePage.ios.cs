#if IOS || MACCATALYST
using labyItems.Helpers;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardCreatePage
{
    partial void ApplyPlatformTabLayoutTweaks()
    {
        IosTabBarHelper.EnsurePinnedToTop(this);
    }
}
#endif
