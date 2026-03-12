using Microsoft.Maui.Controls;
#if ANDROID
using Android.OS;
using Android.Views;
using Microsoft.Maui.ApplicationModel;
#endif
#if IOS || MACCATALYST
using Foundation;
using UIKit;
#endif

namespace labyItems.Controls;

public partial class DictionarySearchBar<TValue>
{
    private ScrollView? _keyboardAvoidanceScrollView;
    private Thickness _keyboardAvoidanceOriginalPadding;
    private bool _hasKeyboardAvoidancePadding;

#if IOS || MACCATALYST
    private static bool _iosKeyboardObserversInitialized;
    private static NSObject? _iosKeyboardWillShowObserver;
    private static NSObject? _iosKeyboardWillHideObserver;
    private static NSObject? _iosKeyboardWillChangeFrameObserver;
    private static double _iosKeyboardHeight;
#endif

    private void ApplyKeyboardAvoidancePadding()
    {
        if (!KeyboardAvoidanceEnabled)
            return;

        var scroll = FindAncestorOfType<ScrollView>(_searchBar);
        if (scroll == null)
            return;

        if (!_hasKeyboardAvoidancePadding || _keyboardAvoidanceScrollView != scroll)
        {
            _keyboardAvoidanceScrollView = scroll;
            _keyboardAvoidanceOriginalPadding = scroll.Padding;
            _hasKeyboardAvoidancePadding = true;
        }

        var keyboardGuard = ResolveKeyboardAvoidanceBottom();
        if (keyboardGuard <= 0)
        {
            RestoreKeyboardAvoidancePadding();
            return;
        }

        var targetBottom = Math.Max(_keyboardAvoidanceOriginalPadding.Bottom, keyboardGuard + 12);
        scroll.Padding = new Thickness(
            _keyboardAvoidanceOriginalPadding.Left,
            _keyboardAvoidanceOriginalPadding.Top,
            _keyboardAvoidanceOriginalPadding.Right,
            targetBottom);
    }

    private void RestoreKeyboardAvoidancePadding()
    {
        if (!_hasKeyboardAvoidancePadding || _keyboardAvoidanceScrollView == null)
            return;

        _keyboardAvoidanceScrollView.Padding = _keyboardAvoidanceOriginalPadding;
        _keyboardAvoidanceScrollView = null;
        _hasKeyboardAvoidancePadding = false;
    }

    private double ResolveKeyboardAvoidanceBottom()
    {
        var dynamicKeyboardHeight = GetSystemKeyboardHeight();
        if (dynamicKeyboardHeight > 0)
            return dynamicKeyboardHeight;

        if (dynamicKeyboardHeight == 0)
            return 0;

        var page = GetOwningPage();
        var pageHeight = page?.Height > 0 ? page.Height : (Application.Current?.MainPage?.Height ?? 0);
        if (pageHeight <= 0)
            return 0;

        return Math.Clamp(
            pageHeight * KeyboardGuardRatioFallback,
            KeyboardGuardMinFallback,
            KeyboardGuardMaxFallback);
    }

    private static double GetSystemKeyboardHeight()
    {
#if ANDROID
        try
        {
            var activity = Platform.CurrentActivity;
            var decor = activity?.Window?.DecorView;
            if (decor == null)
                return -1;

            var density = activity?.Resources?.DisplayMetrics?.Density ?? 1f;
            if (density <= 0)
                density = 1f;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                var insets = decor.RootWindowInsets;
                if (insets == null)
                    return -1;

                var imeBottom = insets.GetInsets(WindowInsets.Type.Ime()).Bottom;
                var navBottom = insets.GetInsets(WindowInsets.Type.NavigationBars()).Bottom;
                var keyboardPx = Math.Max(0, imeBottom - navBottom);
                return keyboardPx / density;
            }

            var visibleRect = new Android.Graphics.Rect();
            decor.GetWindowVisibleDisplayFrame(visibleRect);
            var keyboardPxLegacy = Math.Max(0, decor.Height - visibleRect.Bottom);
            return keyboardPxLegacy / density;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DictionarySearchBar] keyboard height probe failed: {ex}");
            return -1;
        }
#elif IOS || MACCATALYST
        return _iosKeyboardHeight;
#else
        return 0;
#endif
    }

    private static void EnsureKeyboardObserversInitialized()
    {
#if IOS || MACCATALYST
        if (_iosKeyboardObserversInitialized)
            return;

        _iosKeyboardObserversInitialized = true;
        _iosKeyboardWillShowObserver = UIKeyboard.Notifications.ObserveWillShow((_, args) =>
        {
            _iosKeyboardHeight = ResolveIosKeyboardHeight(args);
        });
        _iosKeyboardWillChangeFrameObserver = UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            _iosKeyboardHeight = ResolveIosKeyboardHeight(args);
        });
        _iosKeyboardWillHideObserver = UIKeyboard.Notifications.ObserveWillHide((_, __) =>
        {
            _iosKeyboardHeight = 0;
        });
#endif
    }

#if IOS || MACCATALYST
    private static double ResolveIosKeyboardHeight(UIKeyboardEventArgs args)
    {
        var window = GetKeyWindow();
        if (window == null)
            return Math.Max(0, args.FrameEnd.Height);

        var frameInWindow = window.ConvertRectFromWindow(args.FrameEnd, null);
        var overlap = Math.Max(0, window.Bounds.Bottom - frameInWindow.Top - window.SafeAreaInsets.Bottom);
        return overlap;
    }

    private static UIWindow? GetKeyWindow()
    {
        var app = UIApplication.SharedApplication;
        foreach (var scene in app.ConnectedScenes)
        {
            if (scene is not UIWindowScene windowScene)
                continue;

            foreach (var window in windowScene.Windows)
            {
                if (window.IsKeyWindow)
                    return window;
            }
        }

#pragma warning disable CS0618
        return app.Windows.FirstOrDefault(w => w.IsKeyWindow);
#pragma warning restore CS0618
    }
#endif
}
