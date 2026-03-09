#if IOS || MACCATALYST
using labyItems.Helpers;

namespace labyItems.Pages.Battleboard;

public partial class BattleboardPage
{
    partial void ApplyPlatformTabLayoutTweaks()
    {
        IosTabBarPlacementHelper.MoveToTop(this);
    }
}
#endif
