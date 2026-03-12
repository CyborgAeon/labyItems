using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Graphics;
using labyItems.Controls.Pickers;

namespace labyItems.Controls;

public partial class DictionarySearchBar<TValue>
{
    // Local overlay state
    private AbsoluteLayout? _overlay;
    private BoxView? _overlayBackdrop;
    private Grid? _overlayHost;
    private Border? _overlayContainer;
    private CollectionView? _overlayCollection;
    private ContentPage? _overlayPage;
    private EventHandler? _pageSizeChanged;
    private EventHandler<ScrolledEventArgs>? _scrollHandler;

    private Task ShowResultsAsync() => ShowOverlayAsync();

    private Task RepositionLocalOverlaySafeAsync() => RepositionLocalOverlayAsync();

    private async Task ShowOverlayAsync()
    {
        DismissLocalOverlay();

        if (_filteredResults == null || !_filteredResults.Any())
        {
            ShowInlineResults();
            return;
        }

        if (!UsePageOverlay)
        {
            ShowInlineResults();
            return;
        }

        var page = GetOwningPage();
        if (page == null)
        {
            ShowInlineResults();
            return;
        }

        var host = EnsureOverlayHost(page, out var createdHost);
        if (host == null)
        {
            ShowInlineResults();
            return;
        }

        _resultsView.IsVisible = false;
        _inlineResultsContainer.IsVisible = false;
        if (createdHost)
            _suppressNextUnfocus = true;

        _overlay = new AbsoluteLayout
        {
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = false,
            CascadeInputTransparent = false
        };

        _overlayBackdrop = new BoxView
        {
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.08f),
            InputTransparent = false
        };
        var dismissTap = new TapGestureRecognizer();
        dismissTap.Tapped += (_, _) =>
        {
            DismissLocalOverlay();
            _searchBar.Unfocus();
        };
        _overlayBackdrop.GestureRecognizers.Add(dismissTap);
        AbsoluteLayout.SetLayoutBounds(_overlayBackdrop, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(_overlayBackdrop, AbsoluteLayoutFlags.All);
        _overlay.Children.Add(_overlayBackdrop);

        _overlayContainer = new Border
        {
            Background = new SolidColorBrush(Colors.White),
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#ECECEC")),
            Padding = new Thickness(0),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            IsVisible = true
        };

        _overlayCollection = new CollectionView
        {
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 },
            SelectionMode = SelectionMode.Single,
            BackgroundColor = Colors.Transparent
        };

        _overlayCollection.ItemsSource = _filteredResults;
        _overlayCollection.SelectionChanged += OnResultSelected;
        _overlayCollection.ItemTemplate = BuildResultTemplate();
        _overlayContainer.Content = _overlayCollection;
        _overlayContainer.ZIndex = 1;

        _overlay.Children.Add(_overlayContainer);
        host.Children.Add(_overlay);
        _overlay.ZIndex = 1000;

        _overlayHost = host;
        _overlayPage = page;

        await RepositionLocalOverlaySafeAsync();

        _pageSizeChanged = async (_, _) =>
        {
            if (KeyboardAvoidanceEnabled)
            {
                ApplyKeyboardAvoidancePadding();
                await EnsureAnchorVisibleAsync(GetDesiredDropdownHeight());
            }

            await RepositionLocalOverlaySafeAsync();
        };
        page.SizeChanged += _pageSizeChanged;

        var scrollParent = FindAncestorOfType<ScrollView>(_searchBar);
        if (scrollParent != null)
        {
            _scrollHandler = async (_, _) => await RepositionLocalOverlaySafeAsync();
            scrollParent.Scrolled += _scrollHandler;
        }
    }

    private void UpdateOverlayItems()
    {
        if (_overlayCollection == null)
        {
            ShowInlineResults();
            return;
        }

        _overlayCollection.ItemsSource = _filteredResults;
        _ = RepositionLocalOverlaySafeAsync();
    }

    private async Task RepositionLocalOverlayAsync()
    {
        if (_overlayHost == null || _overlay == null || _overlayContainer == null || _searchBar == null)
            return;

        var hostPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(_overlayHost);
        var anchorPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(_searchBar);
        var anchorHeight = _searchBar.Height;

        var localX = Math.Max(8, anchorPos.X - hostPos.X);
        var localY = anchorPos.Y - hostPos.Y + anchorHeight + DropdownEdgeGap;

        var pageHeight = _overlayHost.Height > 0 ? _overlayHost.Height : (Application.Current?.MainPage?.Height ?? 0);
        var availableBelow = Math.Max(0, pageHeight - localY - 8);
        var availableAbove = Math.Max(0, anchorPos.Y - hostPos.Y - 8);
        var keyboardGuard = ResolveKeyboardAvoidanceBottom();
        var effectiveBelow = Math.Max(0, availableBelow - keyboardGuard);
        var effectiveAbove = availableAbove;

        var visibleItems = Math.Min(4, Math.Max(1, _filteredResults.Count));
        var desiredHeight = Math.Min(DefaultDropdownMaxHeight, visibleItems * 48);
        double finalHeight = desiredHeight;

        if (effectiveBelow < desiredHeight && effectiveAbove >= desiredHeight)
        {
            localY = Math.Max(8, anchorPos.Y - hostPos.Y - desiredHeight - DropdownEdgeGap);
        }
        else if (effectiveBelow < desiredHeight && effectiveAbove < desiredHeight)
        {
            if (effectiveBelow >= effectiveAbove)
            {
                finalHeight = Math.Max(48, effectiveBelow);
            }
            else
            {
                finalHeight = Math.Max(48, effectiveAbove);
                localY = Math.Max(8, anchorPos.Y - hostPos.Y - finalHeight - DropdownEdgeGap);
            }
        }

        var pageWidth = _overlayHost.Width > 0 ? _overlayHost.Width : (Application.Current?.MainPage?.Width ?? 0);

        double width;
        if (DropdownWidth > 0)
            width = DropdownWidth;
        else if (_searchBar.Width > 0)
            width = _searchBar.Width;
        else
            width = Math.Max(120, pageWidth - 16);

        width = Math.Min(width, Math.Max(0, pageWidth - 16));
        if (localX + width + 8 > pageWidth)
            localX = Math.Max(8, pageWidth - width - 8);

        AbsoluteLayout.SetLayoutBounds(_overlayContainer, new Rect(localX, localY, width, finalHeight));
        AbsoluteLayout.SetLayoutFlags(_overlayContainer, AbsoluteLayoutFlags.None);

        if (_overlayContainer.Content is CollectionView collectionView)
            collectionView.HeightRequest = finalHeight;
    }

    private void DismissLocalOverlay()
    {
        if (_overlayCollection != null)
            _overlayCollection.SelectionChanged -= OnResultSelected;

        if (_overlayHost != null && _overlay != null && _overlayHost.Children.Contains(_overlay))
            _overlayHost.Children.Remove(_overlay);

        if (_overlayPage != null && _pageSizeChanged != null)
            _overlayPage.SizeChanged -= _pageSizeChanged;

        if (_overlayPage != null && _scrollHandler != null)
        {
            var scrollView = FindAncestorOfType<ScrollView>(_searchBar);
            if (scrollView != null)
                scrollView.Scrolled -= _scrollHandler;
        }

        _overlay = null;
        _overlayBackdrop = null;
        _overlayHost = null;
        _overlayCollection = null;
        _overlayContainer = null;
        _overlayPage = null;
        _pageSizeChanged = null;
        _scrollHandler = null;
    }

    private double GetDesiredDropdownHeight()
    {
        var visibleItems = Math.Min(4, Math.Max(1, _filteredResults.Count));
        return Math.Min(DefaultDropdownMaxHeight, visibleItems * 48);
    }

    private async Task EnsureAnchorVisibleAsync(double desiredDropdownHeight)
    {
        var scroll = FindAncestorOfType<ScrollView>(_searchBar);
        if (scroll == null)
            return;

        var anchorPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(_searchBar);
        var scrollPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(scroll);

        var anchorTop = anchorPos.Y - scrollPos.Y;
        var anchorHeight = _searchBar.Height > 0 ? _searchBar.Height : MinimumEntryHeight;
        var keyboardGuard = ResolveKeyboardAvoidanceBottom();
        var viewportHeight = Math.Max(0, scroll.Height - keyboardGuard);
        if (viewportHeight <= 0)
            return;

        var requiredSpace = Math.Max(0, desiredDropdownHeight + DropdownEdgeGap);
        var availableAbove = Math.Max(0, anchorTop - DropdownEdgeGap);
        var availableBelow = Math.Max(0, viewportHeight - (anchorTop + anchorHeight + DropdownEdgeGap));
        var openDown = availableBelow >= requiredSpace || availableBelow >= availableAbove;
        var breathingRoom = 8d;

        if (openDown)
        {
            if (availableBelow >= requiredSpace)
                return;

            var deficit = requiredSpace - availableBelow;
            var target = Math.Max(0, scroll.ScrollY + deficit + breathingRoom);
            await scroll.ScrollToAsync(scroll.ScrollX, target, true);
            return;
        }

        if (availableAbove >= requiredSpace)
            return;

        var upwardDeficit = requiredSpace - availableAbove;
        var upwardTarget = Math.Max(0, scroll.ScrollY - upwardDeficit - breathingRoom);
        await scroll.ScrollToAsync(scroll.ScrollX, upwardTarget, true);
    }

    private async Task RefreshFromRemoteAsync(string? query)
    {
        if (RemoteSearchProvider == null)
            return;

        var requestId = ++_remoteRequestId;
        var results = await RemoteSearchProvider(query ?? string.Empty);
        if (_remoteRequestId != requestId)
            return;

        ItemsSource = results ?? new Dictionary<string, TValue>();
    }

    private ContentPage? GetTopPage()
    {
        return ResolveContentPage(Application.Current?.MainPage);

        static ContentPage? ResolveContentPage(Page? page) =>
            page switch
            {
                NavigationPage nav => ResolveContentPage(nav.CurrentPage),
                TabbedPage tab => ResolveContentPage(tab.CurrentPage),
                FlyoutPage flyout => ResolveContentPage(flyout.Detail),
                Shell shell => ResolveContentPage(shell.CurrentPage),
                ContentPage contentPage => contentPage,
                _ => null
            };
    }

    private Grid? EnsureOverlayHost(ContentPage page, out bool createdHost)
    {
        createdHost = false;
        if (page.Content == null)
            return null;

        if (page.Content is Grid existing && existing.StyleId == "__overlay_host__")
            return existing;

        var original = page.Content;
        var host = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Star) },
            StyleId = "__overlay_host__"
        };

        page.Content = null;
        host.Children.Add(original);
        page.Content = host;
        createdHost = true;
        return host;
    }

    private void InitializeOverlayHostIfNeeded()
    {
        if (_overlayHostInitialized)
            return;

        var page = GetOwningPage();
        if (page == null)
            return;

        _ = Device.InvokeOnMainThreadAsync(() =>
        {
            if (_overlayHostInitialized)
                return;

            var host = EnsureOverlayHost(page, out _);
            if (host != null)
                _overlayHostInitialized = true;
        });
    }

    private void ShowInlineResults()
    {
        var dropdownHeight = GetDesiredDropdownHeight();
        var entryHeight = _searchBar.Height > 0 ? _searchBar.Height : MinimumEntryHeight;
        _resultsView.ItemsSource = _filteredResults;
        _resultsView.HeightRequest = dropdownHeight;
        _inlineResultsContainer.HeightRequest = dropdownHeight;
        _inlineResultsContainer.IsVisible = _filteredResults is { Count: > 0 };
        _inlineResultsContainer.TranslationY = entryHeight;
    }

    private static T? FindAncestorOfType<T>(Element? start) where T : VisualElement
    {
        var current = start?.Parent;
        while (current != null)
        {
            if (current is T typed)
                return typed;

            current = current.Parent;
        }

        return null;
    }

    private ContentPage? GetOwningPage()
        => FindAncestorOfType<ContentPage>(this)
           ?? FindAncestorOfType<ContentPage>(_searchBar)
           ?? GetTopPage();
}
