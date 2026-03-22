#if ANDROID
using Android.Views;
using Android.Widget;
using Google.Android.Material.Tabs;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardCreatePage
{
    partial void ApplyPlatformTabLayoutTweaks()
    {
        if (Handler?.PlatformView is not ViewGroup rootView)
            return;

        var tabLayout = FindTabLayout(rootView);
        if (tabLayout == null)
            return;

        if (tabLayout.GetChildAt(0) is not LinearLayout tabStrip)
            return;

        var hasManyTabs = tabStrip.ChildCount > 5;
        tabLayout.TabMode = hasManyTabs ? TabLayout.ModeScrollable : TabLayout.ModeFixed;
        tabLayout.TabGravity = hasManyTabs ? TabLayout.GravityCenter : TabLayout.GravityFill;

        var density = tabLayout.Resources?.DisplayMetrics?.Density ?? 1f;
        for (var i = 0; i < tabStrip.ChildCount; i++)
        {
            var tabView = tabStrip.GetChildAt(i);
            tabView.SetMinimumWidth(0);
            tabView.LayoutParameters = hasManyTabs
                ? new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WrapContent,
                    ViewGroup.LayoutParams.MatchParent)
                : i == 0
                    ? new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WrapContent,
                        ViewGroup.LayoutParams.MatchParent)
                    : new LinearLayout.LayoutParams(
                        0,
                        ViewGroup.LayoutParams.MatchParent,
                        1f);

            var horizontalPaddingDp = hasManyTabs ? 10 : (i == 0 ? 1 : 0);
            var horizontalPaddingPx = (int)(horizontalPaddingDp * density);
            tabView.SetPadding(horizontalPaddingPx, tabView.PaddingTop, horizontalPaddingPx, tabView.PaddingBottom);
        }

        tabStrip.RequestLayout();
    }

    private static TabLayout? FindTabLayout(ViewGroup root)
    {
        if (root is TabLayout layout)
            return layout;

        for (var i = 0; i < root.ChildCount; i++)
        {
            if (root.GetChildAt(i) is not ViewGroup child)
                continue;

            var found = FindTabLayout(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
