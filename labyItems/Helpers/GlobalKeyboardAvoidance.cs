using System;
using System.Collections.Generic;
using System.Linq;
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

namespace labyItems.Helpers;

public static class GlobalKeyboardAvoidance
{
    private const double ExtraBottomPadding = 12;
    private const double FallbackRatio = 0.40;
    private const double FallbackMin = 240;
    private const double FallbackMax = 480;

    private sealed class PaddingState
    {
        public Thickness OriginalPadding { get; set; }
        public int ActiveFocusCount { get; set; }
    }

    private static readonly HashSet<VisualElement> AttachedViews = new();
    private static readonly Dictionary<ScrollView, PaddingState> ScrollStates = new();
    private static readonly Dictionary<Page, PaddingState> PageStates = new();
    private static WeakReference<VisualElement>? _currentFocusedView;
    private static bool _keyboardMonitorStarted;
    private static bool _lastKeyboardVisible;
#if IOS || MACCATALYST
    private static bool _iosKeyboardObserversInitialized;
    private static NSObject? _iosKeyboardWillShowObserver;
    private static NSObject? _iosKeyboardWillHideObserver;
    private static NSObject? _iosKeyboardWillChangeFrameObserver;
    private static double _iosKeyboardHeight;
#endif

    public static void Attach(VisualElement view)
    {
        if (view == null)
            return;

        if (!AttachedViews.Add(view))
            return;

        view.Focused += OnViewFocused;
        view.Unfocused += OnViewUnfocused;
        view.HandlerChanging += OnViewHandlerChanging;
        EnsureKeyboardMonitorStarted();
    }

    private static void OnViewHandlerChanging(object? sender, HandlerChangingEventArgs e)
    {
        if (sender is not VisualElement view || e.NewHandler != null)
            return;

        view.Focused -= OnViewFocused;
        view.Unfocused -= OnViewUnfocused;
        view.HandlerChanging -= OnViewHandlerChanging;
        AttachedViews.Remove(view);
        if (_currentFocusedView != null && _currentFocusedView.TryGetTarget(out var focused) && ReferenceEquals(focused, view))
            _currentFocusedView = null;
        RestoreForView(view);
    }

    private static async void OnViewFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not VisualElement view)
            return;

        if (IsInsideDictionarySearchBar(view))
            return;

        _currentFocusedView = new WeakReference<VisualElement>(view);
        var keyboardHeight = ResolveKeyboardHeight(view);
        if (keyboardHeight <= 0)
            return;

        var scroll = FindAncestorOfType<ScrollView>(view.Parent);
        if (scroll != null)
        {
            ApplyScrollPadding(scroll, keyboardHeight);
            try
            {
                await scroll.ScrollToAsync(view, ScrollToPosition.MakeVisible, true);
            }
            catch
            {
                // Best-effort; some templates may not be directly scrollable.
            }

            return;
        }

        var page = GetOwningPage(view);
        if (page != null)
            ApplyPagePadding(page, keyboardHeight);
    }

    private static void OnViewUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not VisualElement view)
            return;

        if (IsInsideDictionarySearchBar(view))
            return;

        if (_currentFocusedView != null && _currentFocusedView.TryGetTarget(out var focused) && ReferenceEquals(focused, view))
            _currentFocusedView = null;
        var scroll = FindAncestorOfType<ScrollView>(view.Parent);
        if (scroll != null)
        {
            ReleaseScrollPadding(scroll);
            return;
        }

        var page = GetOwningPage(view);
        if (page != null)
            ReleasePagePadding(page);
    }

    private static void RestoreForView(VisualElement view)
    {
        var scroll = FindAncestorOfType<ScrollView>(view.Parent);
        if (scroll != null)
            ReleaseScrollPadding(scroll, force: true);

        var page = GetOwningPage(view);
        if (page != null)
            ReleasePagePadding(page, force: true);
    }

    private static void RestoreAllPadding()
    {
        foreach (var (scroll, state) in ScrollStates)
            scroll.Padding = state.OriginalPadding;
        ScrollStates.Clear();

        foreach (var (page, state) in PageStates)
            page.Padding = state.OriginalPadding;
        PageStates.Clear();
    }

    private static void ApplyScrollPadding(ScrollView scroll, double keyboardHeight)
    {
        if (!ScrollStates.TryGetValue(scroll, out var state))
        {
            state = new PaddingState { OriginalPadding = scroll.Padding };
            ScrollStates[scroll] = state;
        }

        state.ActiveFocusCount++;
        var targetBottom = Math.Max(state.OriginalPadding.Bottom, keyboardHeight + ExtraBottomPadding);
        scroll.Padding = new Thickness(
            state.OriginalPadding.Left,
            state.OriginalPadding.Top,
            state.OriginalPadding.Right,
            targetBottom);
    }

    private static void ReleaseScrollPadding(ScrollView scroll, bool force = false)
    {
        if (!ScrollStates.TryGetValue(scroll, out var state))
            return;

        if (!force)
            state.ActiveFocusCount = Math.Max(0, state.ActiveFocusCount - 1);

        if (force || state.ActiveFocusCount == 0)
        {
            scroll.Padding = state.OriginalPadding;
            ScrollStates.Remove(scroll);
        }
    }

    private static void ApplyPagePadding(Page page, double keyboardHeight)
    {
        if (!PageStates.TryGetValue(page, out var state))
        {
            state = new PaddingState { OriginalPadding = page.Padding };
            PageStates[page] = state;
        }

        state.ActiveFocusCount++;
        var targetBottom = Math.Max(state.OriginalPadding.Bottom, keyboardHeight + ExtraBottomPadding);
        page.Padding = new Thickness(
            state.OriginalPadding.Left,
            state.OriginalPadding.Top,
            state.OriginalPadding.Right,
            targetBottom);
    }

    private static void ReleasePagePadding(Page page, bool force = false)
    {
        if (!PageStates.TryGetValue(page, out var state))
            return;

        if (!force)
            state.ActiveFocusCount = Math.Max(0, state.ActiveFocusCount - 1);

        if (force || state.ActiveFocusCount == 0)
        {
            page.Padding = state.OriginalPadding;
            PageStates.Remove(page);
        }
    }

    private static bool IsInsideDictionarySearchBar(VisualElement view)
    {
        Element? current = view.Parent;
        while (current != null)
        {
            if (current.GetType().Name.StartsWith("DictionarySearchBar`1", StringComparison.Ordinal))
                return true;
            current = current.Parent;
        }

        return false;
    }

    private static T? FindAncestorOfType<T>(Element? start) where T : Element
    {
        var current = start;
        while (current != null)
        {
            if (current is T typed)
                return typed;

            current = current.Parent;
        }

        return null;
    }

    private static Page? GetOwningPage(VisualElement view)
    {
        Element? current = view;
        while (current != null)
        {
            if (current is Page page)
                return page;

            current = current.Parent;
        }

        return ResolveTopPage(Application.Current?.MainPage);
    }

    private static Page? ResolveTopPage(Page? page) =>
        page switch
        {
            NavigationPage nav => ResolveTopPage(nav.CurrentPage),
            TabbedPage tab => ResolveTopPage(tab.CurrentPage),
            FlyoutPage flyout => ResolveTopPage(flyout.Detail),
            Shell shell => ResolveTopPage(shell.CurrentPage),
            _ => page
        };

    private static double ResolveKeyboardHeight(VisualElement view)
    {
#if ANDROID
        var dynamic = GetAndroidKeyboardHeight();
        if (dynamic >= 0)
            return dynamic;

        var page = GetOwningPage(view);
        var pageHeight = page?.Height > 0 ? page.Height : (Application.Current?.MainPage?.Height ?? 0);
        if (pageHeight <= 0)
            return 0;

        return Math.Clamp(pageHeight * FallbackRatio, FallbackMin, FallbackMax);
#elif IOS || MACCATALYST
        return Math.Max(0, _iosKeyboardHeight);
#else
        return 0;
#endif
    }

#if ANDROID
    private static double GetAndroidKeyboardHeight()
    {
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
        catch
        {
            return -1;
        }
    }
#endif

    private static void EnsureKeyboardMonitorStarted()
    {
#if IOS || MACCATALYST
        EnsureIosKeyboardObservers();
#endif

        if (_keyboardMonitorStarted)
            return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        _keyboardMonitorStarted = true;
        dispatcher.StartTimer(TimeSpan.FromMilliseconds(120), () =>
        {
            var keyboardHeight = GetRawKeyboardHeight();
            var keyboardVisible = keyboardHeight > 0;

            if (!keyboardVisible)
            {
                if (_lastKeyboardVisible || ScrollStates.Count > 0 || PageStates.Count > 0)
                    RestoreAllPadding();

                _lastKeyboardVisible = false;
                return true;
            }

            _lastKeyboardVisible = true;

            if ((ScrollStates.Count > 0 || PageStates.Count > 0) || _currentFocusedView == null)
                return true;

            if (!_currentFocusedView.TryGetTarget(out var focused) || focused == null)
                return true;

            if (!focused.IsFocused)
                return true;

            var scroll = FindAncestorOfType<ScrollView>(focused.Parent);
            if (scroll != null)
            {
                ApplyScrollPadding(scroll, keyboardHeight);
                return true;
            }

            var page = GetOwningPage(focused);
            if (page != null)
                ApplyPagePadding(page, keyboardHeight);

            return true;
        });
    }

    private static double GetRawKeyboardHeight()
    {
#if ANDROID
        var raw = GetAndroidKeyboardHeight();
        return raw < 0 ? 0 : raw;
#elif IOS || MACCATALYST
        return Math.Max(0, _iosKeyboardHeight);
#else
        return 0;
#endif
    }

#if IOS || MACCATALYST
    private static void EnsureIosKeyboardObservers()
    {
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
    }

    private static double ResolveIosKeyboardHeight(UIKeyboardEventArgs args)
    {
        var window = GetKeyWindow();
        if (window == null)
            return Math.Max(0, args.FrameEnd.Height);

        var frameInWindow = window.ConvertRectFromWindow(args.FrameEnd, null);
        return Math.Max(0, window.Bounds.Bottom - frameInWindow.Top - window.SafeAreaInsets.Bottom);
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
