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
    private static AbsoluteLayout? _activeOverlay;
    private static Grid? _activeHost;
    private static TaskCompletionSource<object?>? _currentTcs;

    public static async Task<T?> ShowAsync<T>(VisualElement anchor, IEnumerable<T> items, Func<T, string> displayFormatter, double preferredWidth, double x, double y, double maxHeight)
    {
        // Ensure only one overlay at a time
        if (_activeOverlay != null)
            return default;

        var page = GetTopPage();
        if (page == null)
            return default;

        // Ensure the page has a Grid root so we can layer children
        var host = EnsureOverlayHost(page);
        if (host == null)
            return default;

        // Create overlay
        var overlay = new AbsoluteLayout
        {
            BackgroundColor = Colors.Transparent,
            InputTransparent = false
        };

        _activeHost = host;

        // Full-screen transparent scrim to capture outside taps
        var scrim = new BoxView
        {
            BackgroundColor = Colors.Transparent,
            Opacity = 1
        };

        var scrimTap = new TapGestureRecognizer();
        scrimTap.Tapped += (s, e) =>
        {
            // dismiss
            Dismiss();
        };
        scrim.GestureRecognizers.Add(scrimTap);

        AbsoluteLayout.SetLayoutBounds(scrim, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(scrim, AbsoluteLayoutFlags.All);
        overlay.Children.Add(scrim);

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

        collection.ItemsSource = items;

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

        // Add container at absolute position (x,y)
        AbsoluteLayout.SetLayoutBounds(container, new Rect(x, y, preferredWidth, maxHeight));
        AbsoluteLayout.SetLayoutFlags(container, AbsoluteLayoutFlags.None);
        overlay.Children.Add(container);

        // Add overlay to host (top-most child)
        host.Children.Add(overlay);
        overlay.ZIndex = 999;

        _activeOverlay = overlay;

        // Wait for result via TaskCompletionSource
        _currentTcs = new TaskCompletionSource<object?>();

        return (T?)(await _currentTcs.Task.ConfigureAwait(false));
    }

    /// <summary>
    /// Dismiss the active overlay without selecting anything.
    /// </summary>
    public static void Dismiss()
    {
        try
        {
            if (_activeHost != null && _activeOverlay != null)
            {
                if (_activeHost.Children.Contains(_activeOverlay))
                    _activeHost.Children.Remove(_activeOverlay);
            }
        }
        catch { }
        finally
        {
            _activeOverlay = null;
            _activeHost = null;
            _currentTcs?.TrySetResult(null);
            _currentTcs = null;
        }
    }

    private static void DismissWithResult<T>(T value)
    {
        try
        {
            if (_activeHost != null && _activeOverlay != null)
            {
                if (_activeHost.Children.Contains(_activeOverlay))
                    _activeHost.Children.Remove(_activeOverlay);
            }
        }
        catch { }
        finally
        {
            _activeOverlay = null;
            _activeHost = null;
            _currentTcs?.TrySetResult(value as object);
            _currentTcs = null;
        }
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

    private static ContentPage? GetTopPage()
    {
        var main = Application.Current?.MainPage;
        if (main is NavigationPage nav && nav.CurrentPage is ContentPage navPage)
            return navPage;
        if (main is Shell shell && shell.CurrentPage is ContentPage shellPage)
            return shellPage;
        return main as ContentPage;
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
