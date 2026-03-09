#if IOS || MACCATALYST
using labyItems.Helpers;

namespace labyItems.Pages.Characters;

public partial class AdvanceCharacterPage
{
    partial void ApplyPlatformTabLayoutTweaks()
    {
        IosTabBarPlacementHelper.EnsurePinnedToTop(this);
    }
}
#endif
