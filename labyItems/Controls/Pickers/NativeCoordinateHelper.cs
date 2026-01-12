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
        try
        {
            if (element.Handler?.PlatformView is Android.Views.View nativeView)
            {
                var loc = new int[2];
                nativeView.GetLocationOnScreen(loc);
                var density = nativeView.Resources.DisplayMetrics.Density;
                return new Point(loc[0] / density, loc[1] / density);
            }
        }
        catch { }
#elif IOS || MACCATALYST
        try
        {
            if (element.Handler?.PlatformView is UIKit.UIView nativeView)
            {
                var window = nativeView.Window ?? UIKit.UIApplication.SharedApplication.KeyWindow;
                var pt = nativeView.ConvertPointToView(CoreGraphics.CGPoint.Empty, window);
                return new Point(pt.X, pt.Y);
            }
        }
        catch { }
#elif WINDOWS
        try
        {
            if (element.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
            {
                var transform = fe.TransformToVisual(null);
                var pt = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                return new Point(pt.X, pt.Y);
            }
        }
        catch { }
#endif

        // Fallback: accumulate offsets
        try
        {
            double x = 0, y = 0;
            var current = (VisualElement?)element;
            while (current != null)
            {
                x += current.X;
                y += current.Y;
                current = current.Parent as VisualElement;
            }

            return new Point(x, y);
        }
        catch
        {
            return new Point(0, 0);
        }
    }
}
