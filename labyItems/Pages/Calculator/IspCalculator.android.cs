#if ANDROID
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Tabs;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator
{
    partial void ApplyPlatformTabFontSize(double fontSize)
    {
        if (Handler?.PlatformView is not ViewGroup rootView)
            return;

        var tabLayout = FindTabLayout(rootView);
        if (tabLayout is null)
            return;

        tabLayout.TabMode = TabLayout.ModeFixed;
        tabLayout.TabGravity = TabLayout.GravityFill;

        if (tabLayout.GetChildAt(0) is LinearLayout tabStrip)
        {
            var density = tabLayout.Resources?.DisplayMetrics?.Density ?? 1f;

            for (int i = 0; i < tabStrip.ChildCount; i++)
            {
                if (tabStrip.GetChildAt(i) is ViewGroup tabView)
                {
                    tabView.SetMinimumWidth(0);

                    var horizontalPaddingDp = i == 0 ? 1 : 0;
                    var horizontalPaddingPx = (int)(horizontalPaddingDp * density);
                    tabView.SetPadding(horizontalPaddingPx, tabView.PaddingTop, horizontalPaddingPx, tabView.PaddingBottom);
                    ApplyFontSizeToChildren(tabView, fontSize);
                }
            }

            tabStrip.RequestLayout();
        }
    }

    private static void ApplyFontSizeToChildren(ViewGroup group, double fontSize)
    {
        for (int i = 0; i < group.ChildCount; i++)
        {
            var child = group.GetChildAt(i);
            switch (child)
            {
                case TextView textView:
                    textView.SetTextSize(ComplexUnitType.Sp, (float)fontSize);
                    textView.SetSingleLine(true);
                    textView.Ellipsize = TextUtils.TruncateAt.End;
                    break;
                case ViewGroup nested:
                    ApplyFontSizeToChildren(nested, fontSize);
                    break;
            }
        }
    }

    private static TabLayout? FindTabLayout(ViewGroup root)
    {
        if (root is TabLayout layout)
            return layout;

        for (int i = 0; i < root.ChildCount; i++)
        {
            if (root.GetChildAt(i) is ViewGroup child)
            {
                var found = FindTabLayout(child);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }
}
#endif
