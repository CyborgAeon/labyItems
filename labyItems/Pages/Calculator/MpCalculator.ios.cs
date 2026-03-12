#if IOS || MACCATALYST
using labyItems.Helpers;

namespace labyItems.Pages.Calculator;

public partial class MpCalculator
{
    partial void ApplyPlatformTabLayoutTweaks()
    {
        IosTabBarHelper.EnsurePinnedToTop(this);
    }
}
#endif
