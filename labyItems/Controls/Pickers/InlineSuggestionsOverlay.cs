using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace labyItems.Controls.Pickers;

/// <summary>
/// Inline overlay manager that attaches an AbsoluteLayout overlay to the current page,
/// positions a suggestions container beneath an anchor element, and returns selection/dismiss results.
/// This avoids using CommunityToolkit popups and does not change page navigation.
/// </summary>
public static class InlineSuggestionsOverlay
{
    private const double ItemHeight = 48;
    private const double VerticalGap = 6;

    private static AbsoluteLayout? _activeOverlay;
    private static Grid? _activeHost;
    private static TaskCompletionSource<object?>? _currentTcs;

    // Active references for repositioning
    private static Border? _activeContainer;
    private static VisualElement? _activeAnchor;
    private static double _activePreferredWidth;
    private static double _activeMaxHeight;
    private static int _activeItemCount;
    private static ContentPage? _activePage;
    private static ScrollView? _activeScrollView;
    private static CollectionView? _activeCollection;
    private static Type? _activeItemType;

    private static EventHandler? _pageSizeChangedHandler;
    private static EventHandler<ScrolledEventArgs>? _scrollHandler;

    public static async Task<T?> ShowAsync<T>(VisualElement anchor, IEnumerable<T> items, Func<T, string> displayFormatter, double preferredWidth, double maxHeight, bool captureOutsideTap = true)
    {
        // Ensure only one overlay at a time
        if (_activeOverlay != null)
            return default;

        // Materialize the items once so we can measure height and reuse
        var itemList = items?.ToList() ?? new List<T>();

        var page = GetTopPage();
        if (page == null)
            return default;

        // Ensure the page has a Grid root so we can layer children
        var host = EnsureOverlayHost(page);
        if (host == null)
            return default;

        _activeItemCount = itemList.Count;
        _activePreferredWidth = preferredWidth;
        _activeMaxHeight = maxHeight;
        _activeAnchor = anchor;
        _activeHost = host;
        _activePage = page;

        var targetVisibleItems = Math.Min(4, Math.Max(1, _activeItemCount));
        var desiredDropdownHeight = Math.Min(_activeMaxHeight, targetVisibleItems * ItemHeight);

        // Ensure layout has dimensions before positioning
        await WaitForLayoutAsync(host);
        await WaitForLayoutAsync(anchor);

        // Try to scroll anchor into view if it's inside a ScrollView
        await EnsureAnchorVisibleAsync(anchor, desiredDropdownHeight);

        // Create overlay
        var overlay = new AbsoluteLayout
        {
            BackgroundColor = Colors.Transparent,
            InputTransparent = false
        };

        if (captureOutsideTap)
        {
            // Full-screen transparent scrim to capture outside taps
            var scrim = new BoxView
            {
                BackgroundColor = Colors.Transparent,
                Opacity = 1
            };

            var scrimTap = new TapGestureRecognizer();
            scrimTap.Tapped += (s, e) =>
            {
                try { _activeAnchor?.Unfocus(); } catch { }
                // dismiss
                Dismiss();
            };
            scrim.GestureRecognizers.Add(scrimTap);

            AbsoluteLayout.SetLayoutBounds(scrim, new Rect(0, 0, 1, 1));
            AbsoluteLayout.SetLayoutFlags(scrim, AbsoluteLayoutFlags.All);
            overlay.Children.Add(scrim);
        }

        // Suggestions container (simple borderless dropdown list - no card/shadow)
        var container = new Border
        {
            Background = GetThemeBrush(),
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#ECECEC")),
            Padding = new Thickness(0),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            IsVisible = true
        };

        var collection = new CollectionView
        {
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 },
            SelectionMode = SelectionMode.Single,
            BackgroundColor = Colors.Transparent
        };

        collection.ItemsSource = itemList;

        collection.ItemTemplate = new DataTemplate(() =>
        {
            var grid = new Grid { Padding = new Thickness(12, 10), ColumnDefinitions = { new ColumnDefinition(GridLength.Star) } };
            var label = new Label { VerticalOptions = LayoutOptions.Center, TextColor = (Color)Application.Current.Resources["Gray900"] };
            label.SetBinding(Label.TextProperty, new Binding(".", BindingMode.Default, new FuncValueConverter<T>(displayFormatter)));
            grid.Add(label);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                if (s is VisualElement ve && ve.BindingContext is T value)
                {
                    // return selection via task completion
                    DismissWithResult(value);
                }
            };
            grid.GestureRecognizers.Add(tap);

            return grid;
        });

        container.Content = collection;

        _activeContainer = container;
        _activeCollection = collection;
        _activeItemType = typeof(T);

        // Initial positioning
        await RepositionOverlayAsync();

        // Add container at absolute position; RepositionOverlayAsync will set correct bounds
        overlay.Children.Add(container);

        // Add overlay to host (top-most child)
        host.Children.Add(overlay);
        overlay.ZIndex = 999;

        // Re-run positioning now that overlay is in the visual tree
        _ = Device.InvokeOnMainThreadAsync(RepositionOverlayAsync);

        _activeOverlay = overlay;

        // Ensure the anchor stays focused so keyboard input continues to work while the overlay is visible
        _ = Device.InvokeOnMainThreadAsync(() => anchor.Focus());

        // Subscribe to page size changes (keyboard show/hide) to reposition and keep anchor visible
        _pageSizeChangedHandler = async (s, e) =>
        {
            try
            {
                if (_activeAnchor != null)
                {
                    var desired = Math.Min(_activeMaxHeight, Math.Min(4, Math.Max(1, _activeItemCount)) * ItemHeight);
                    await EnsureAnchorVisibleAsync(_activeAnchor, desired);
                }
            }
            catch { }

            _ = RepositionOverlayAsync();
        };
        page.SizeChanged += _pageSizeChangedHandler;
        _activePage = page;

        // If anchor is within a ScrollView, attach a scrolled handler so overlay follows
        var scrollParent = FindAncestorOfType<ScrollView>(anchor);
        _activeScrollView = scrollParent;
        if (scrollParent != null)
        {
            _scrollHandler = (sender, args) => _ = RepositionOverlayAsync();
            scrollParent.Scrolled += _scrollHandler;
        }

        // Wait for result via TaskCompletionSource
        _currentTcs = new TaskCompletionSource<object?>();

        var result = (T?)(await _currentTcs.Task.ConfigureAwait(false));
        return result;
    }

    /// <summary>
    /// Dismiss the active overlay without selecting anything.
    /// </summary>
    public static void Dismiss()
    {
        CompleteOverlay(null);
    }

    private static void DismissWithResult<T>(T value)
    {
        CompleteOverlay(value);
    }

    private static void CompleteOverlay(object? result)
    {
        RemoveOverlayFromHost();
        DetachHandlers();
        try { _activeAnchor?.Unfocus(); } catch { }
        ResetState();

        _currentTcs?.TrySetResult(result);
        _currentTcs = null;
    }

    private static void RemoveOverlayFromHost()
    {
        try
        {
            if (_activeHost != null && _activeOverlay != null && _activeHost.Children.Contains(_activeOverlay))
            {
                _activeHost.Children.Remove(_activeOverlay);
            }
        }
        catch { }
    }

    private static void DetachHandlers()
    {
        try
        {
            if (_activePage != null && _pageSizeChangedHandler != null)
            {
                _activePage.SizeChanged -= _pageSizeChangedHandler;
            }

            if (_activeScrollView != null && _scrollHandler != null)
            {
                _activeScrollView.Scrolled -= _scrollHandler;
            }
        }
        catch { }

        _pageSizeChangedHandler = null;
        _scrollHandler = null;
        _activePage = null;
        _activeScrollView = null;
    }

    private static void ResetState()
    {
        _activeOverlay = null;
        _activeHost = null;
        _activeContainer = null;
        _activeAnchor = null;
        _activePreferredWidth = 0;
        _activeMaxHeight = 0;
        _activeItemCount = 0;
        _activePage = null;
        _activeScrollView = null;
        _activeCollection = null;
        _activeItemType = null;
    }

    private static async Task WaitForLayoutAsync(VisualElement element)
    {
        if (element.Width > 0 && element.Height > 0)
            return;

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
        await Task.WhenAny(tcs.Task, Task.Delay(200));
        element.SizeChanged -= Handler;
    }

    private static Brush GetThemeBrush()
    {
        try
        {
            if (Application.Current?.RequestedTheme == AppTheme.Dark && Application.Current?.Resources.ContainsKey("OffBlack") == true)
                return new SolidColorBrush((Color)Application.Current.Resources["OffBlack"]);
            return new SolidColorBrush((Color)Application.Current.Resources["White"]);
        }
        catch
        {
            return new SolidColorBrush(Colors.White);
        }
    }

    private static async Task EnsureAnchorVisibleAsync(VisualElement anchor, double desiredDropdownHeight)
    {
        var scroll = FindAncestorOfType<ScrollView>(anchor);
        if (scroll == null) return;

        try
        {
            var anchorPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(anchor);
            var scrollPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(scroll);

            var anchorTop = anchorPos.Y - scrollPos.Y;
            var availableBelow = scroll.Height - (anchorTop + anchor.Height);
            var requiredSpace = Math.Max(0, desiredDropdownHeight + VerticalGap);

            if (availableBelow >= requiredSpace)
                return;

            var deficit = requiredSpace - availableBelow;
            // Nudge the scroll a bit more than the deficit so the entry isn't glued to the top edge
            var target = Math.Max(0, scroll.ScrollY + deficit + (anchor.Height * 0.25));
            await scroll.ScrollToAsync(scroll.ScrollX, target, true);
        }
        catch { }
    }

    private static T? FindAncestorOfType<T>(VisualElement? element) where T : VisualElement
    {
        var current = element?.Parent as VisualElement;
        while (current != null)
        {
            if (current is T t) return t;
            current = current.Parent as VisualElement;
        }

        return default;
    }

    private static ContentPage? GetTopPage()
    {
        var main = Application.Current?.MainPage;
        if (main is NavigationPage nav && nav.CurrentPage is ContentPage navPage)
            return navPage;
        if (main is Shell shell && shell.CurrentPage is ContentPage shellPage)
            return shellPage;
        return main as ContentPage;
    }

    /// <summary>
    /// Update the active overlay's items in-place. Safe no-op when no overlay is active.
    /// </summary>
    public static void UpdateItems<T>(IEnumerable<T> items)
    {
        if (_activeOverlay == null || _activeCollection == null)
            return;

        // Ensure we only update overlays created for the same item type
        if (_activeItemType != null && _activeItemType != typeof(T))
            return;

        var list = items?.ToList() ?? new List<T>();
        _activeItemCount = list.Count;

        _ = Device.InvokeOnMainThreadAsync(() =>
        {
            _activeCollection.ItemsSource = list;
            if (_activeContainer?.Content is CollectionView cv)
            {
                cv.HeightRequest = Math.Min(_activeMaxHeight, Math.Max(ItemHeight, list.Count * ItemHeight));
            }

            _ = RepositionOverlayAsync();
        });
    }

    private static async Task RepositionOverlayAsync()
    {
        if (_activeHost == null || _activeOverlay == null || _activeContainer == null || _activeAnchor == null)
            return;

        try
        {
            var hostPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(_activeHost);
            var anchorPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(_activeAnchor);
            var anchorHeight = _activeAnchor.Height;

            var localX = Math.Max(8, anchorPos.X - hostPos.X);
            // Start at the anchor's bottom with a small gap so the entry remains visible
            var localY = anchorPos.Y - hostPos.Y + anchorHeight + VerticalGap;

            // compute available space below and above (relative to host)
            var pageHeight = _activeHost.Height > 0 ? _activeHost.Height : (Application.Current?.MainPage?.Height ?? 0);
            var availableBelow = Math.Max(0, pageHeight - localY - 8);
            var availableAbove = Math.Max(0, anchorPos.Y - hostPos.Y - 8);

            // Target showing the top 4 results when possible
            var visibleItems = Math.Min(4, Math.Max(1, _activeItemCount));
            var desiredHeight = Math.Min(_activeMaxHeight, visibleItems * ItemHeight);

            double finalHeight = desiredHeight;

            // Prefer showing below if enough space; otherwise try above, otherwise shrink to fit
            if (availableBelow >= desiredHeight)
            {
                // OK keep below
            }
            else if (availableAbove >= desiredHeight)
            {
                // Put above the anchor
                localY = Math.Max(8, anchorPos.Y - hostPos.Y - desiredHeight - VerticalGap);
            }
            else
            {
                // Not enough space either side - pick the larger available area and fit
                if (availableBelow >= availableAbove)
                {
                    finalHeight = Math.Max(48, availableBelow);
                }
                else
                {
                    finalHeight = Math.Max(48, availableAbove);
                    localY = Math.Max(8, anchorPos.Y - hostPos.Y - finalHeight - VerticalGap);
                }
            }

            // Clamp X so container doesn't overflow to the right and set width
            var pageWidth = _activeHost.Width > 0 ? _activeHost.Width : (Application.Current?.MainPage?.Width ?? 0);
            var width = _activePreferredWidth;
            if (_activeAnchor.Width > 0)
                width = Math.Max(width, _activeAnchor.Width);
            width = Math.Min(width, pageWidth - 16);
            if (localX + width + 8 > pageWidth)
            {
                localX = Math.Max(8, pageWidth - width - 8);
            }

            // Apply layout bounds
            AbsoluteLayout.SetLayoutBounds(_activeContainer, new Rect(localX, localY, width, finalHeight));
            AbsoluteLayout.SetLayoutFlags(_activeContainer, AbsoluteLayoutFlags.None);

            // Ensure internal CollectionView height respects container
            if (_activeContainer.Content is CollectionView cv)
            {
                cv.HeightRequest = finalHeight;
            }
        }
        catch { }
    }

    private static int FilteredCountEstimate(Border container)
    {
        if (container.Content is CollectionView cv && cv.ItemsSource is System.Collections.IEnumerable en)
        {
            int count = 0;
            foreach (var _ in en) count++;
            return count;
        }

        return 0;
    }

    // Ensures the page content is wrapped by a Grid so we can safely add an overlay on top without changing layout.
    private static Grid? EnsureOverlayHost(ContentPage page)
    {
        if (page.Content == null)
            return null;

        if (page.Content is Grid g && g.StyleId == "__overlay_host__")
            return g;

        var original = page.Content;
        var root = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star) }, RowDefinitions = { new RowDefinition(GridLength.Star) } };
        root.Children.Add(original);
        // Mark the grid so we don't re-wrap repeatedly
        root.StyleId = "__overlay_host__";
        page.Content = root;
        return root;
    }

    // Minimal converter wrapper
    private class FuncValueConverter<T> : IValueConverter
    {
        private readonly Func<T, string> _func;
        public FuncValueConverter(Func<T, string> func) => _func = func;
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is T t ? _func(t) : string.Empty;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
