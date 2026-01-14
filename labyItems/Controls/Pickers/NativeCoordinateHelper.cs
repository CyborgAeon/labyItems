using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls.Pickers;

public static class NativeCoordinateHelper
{
    public static async Task<Point> GetAbsolutePositionAsync(VisualElement element)
    {
        if (element == null)
            return new Point(0, 0);

        // Wait for layout if needed
        if (element.Width <= 0 || element.Height <= 0)
        {
            var tcs = new TaskCompletionSource<bool>();
            void Handler(object? s, EventArgs e)
            {
                if (element.Width > 0 && element.Height > 0)
                {
                    element.SizeChanged -= Handler;
                    tcs.TrySetResult(true);
                }
            }

            element.SizeChanged += Handler;
            await Task.WhenAny(tcs.Task, Task.Delay(250));
        }

#if ANDROID
        if (element.Handler?.PlatformView is Android.Views.View nativeView)
        {
            var loc = new int[2];
            nativeView.GetLocationOnScreen(loc);
            var density = nativeView.Resources.DisplayMetrics.Density;
            return new Point(loc[0] / density, loc[1] / density);
        }
#elif IOS || MACCATALYST
        if (element.Handler?.PlatformView is UIKit.UIView nativeView)
        {
            var window = nativeView.Window ?? UIKit.UIApplication.SharedApplication.KeyWindow;
            var pt = nativeView.ConvertPointToView(CoreGraphics.CGPoint.Empty, window);
            return new Point(pt.X, pt.Y);
        }
#elif WINDOWS
        if (element.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
        {
            var transform = fe.TransformToVisual(null);
            var pt = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            return new Point(pt.X, pt.Y);
        }
#endif

        // Fallback: accumulate offsets
        double fallbackX = 0, fallbackY = 0;
        var current = (VisualElement?)element;
        while (current != null)
        {
            fallbackX += current.X;
            fallbackY += current.Y;
            current = current.Parent as VisualElement;
        }

        return new Point(fallbackX, fallbackY);
    }
}
