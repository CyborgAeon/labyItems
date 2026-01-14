using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Graphics;
using labyItems.Controls.Pickers;

namespace labyItems.Controls;

/// <summary>
/// SearchBar wrapper that displays dictionary-backed results in an overlay above the page content.
/// Works inside grids/inline layouts without affecting surrounding layout.
/// Example XAML: <controls:DictionarySearchBar x:TypeArguments="enums:MagicColours"
/// ItemsSource="{controls:EnumDictionary x:TypeArguments='enums:MagicColours' Exclude='Grey'}"
/// SelectedValue="{Binding MagicColour}"/>
/// </summary>
public class DictionarySearchBar<TValue> : ContentView
    where TValue : struct
{
    private const double DefaultDropdownMaxHeight = 320;
    private string Exclude = string.Empty;
    private readonly Entry _searchBar;
    private readonly CollectionView _resultsView;
    private List<SearchResult> _filteredResults = new();
    private Dictionary<string, TValue> _defaultOptions;
    private bool _suppressTextChanged;
    private bool _suppressSelectedTextChanged;
    private bool _overlayHostInitialized;
    private bool _suppressNextUnfocus;
    private int _remoteRequestId;
    private bool _isUserEditing;
    private bool Enabled = false;

    public DictionarySearchBar()
    {
        _defaultOptions = BuildDefaultOptions();

        _searchBar = new Entry
        {
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Margin = new Thickness(0),
            ClearButtonVisibility = ClearButtonVisibility.Never
        };

        _searchBar.Focused += OnSearchFocused;
        _searchBar.Unfocused += OnSearchUnfocused;
        _searchBar.TextChanged += OnSearchTextChanged;
        _searchBar.Completed += OnSearchCompleted;

        _resultsView = BuildResultsView();

        var container = new AbsoluteLayout
        {
            IsClippedToBounds = false,
            HorizontalOptions = LayoutOptions.Fill
        };

        // Keep the control's measured height equal to the entry height so it can sit inline (e.g., beside a slider)
        _searchBar.SizeChanged += (s, e) =>
        {
            container.HeightRequest = _searchBar.Height;
            _resultsView.TranslationY = _searchBar.Height;

            // Ensure the entry fills the available container width (AbsoluteLayout requires explicit layout bounds)
            AbsoluteLayout.SetLayoutBounds(_searchBar, new Rect(0, 0, 1, _searchBar.Height));
            AbsoluteLayout.SetLayoutFlags(_searchBar, AbsoluteLayoutFlags.WidthProportional);

            // Position the internal (fallback) results view directly under the entry and make it width-proportional
            AbsoluteLayout.SetLayoutBounds(_resultsView, new Rect(0, _searchBar.Height, 1, 0));
            AbsoluteLayout.SetLayoutFlags(_resultsView, AbsoluteLayoutFlags.WidthProportional);
        };

        // Add the entry and results view to the overlay container. Results will be shown translated below the entry.
        container.Add(_searchBar);
        container.Add(_resultsView);

        Content = container;

        UpdatePlaceholder();
        SyncTextToSelection();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        // Build the overlay host early so first focus doesn't re-parent and steal focus
        InitializeOverlayHostIfNeeded();
    }

    /// <summary>
    /// Optional custom formatter for default enum display text.
    /// Ignored when a custom ItemsSource is provided.
    /// </summary>
    public Func<TValue, string>? DisplayFormatter { get; set; }

    public static readonly BindableProperty AllowCustomOptionsProperty = BindableProperty.Create(
        nameof(AllowCustomOptions),
        typeof(bool),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: false);

    /// <summary>
    /// When true, the search bar will surface a custom option based on the user's text entry
    /// and allow binding to that free-form text via SelectedText.
    /// </summary>
    public bool AllowCustomOptions
    {
        get => (bool)GetValue(AllowCustomOptionsProperty);
        set => SetValue(AllowCustomOptionsProperty, value);
    }

    public static readonly BindableProperty SelectedTextProperty = BindableProperty.Create(
        nameof(SelectedText),
        typeof(string),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: default(string),
        BindingMode.TwoWay,
        propertyChanged: OnSelectedTextChanged);

    /// <summary>
    /// Current text selected or entered by the user. When AllowCustomOptions is true this will
    /// include custom free-form values; otherwise it mirrors the current enum selection.
    /// </summary>
    public string? SelectedText
    {
        get => (string?)GetValue(SelectedTextProperty);
        set => SetValue(SelectedTextProperty, value);
    }

    public static readonly BindableProperty DropdownWidthProperty = BindableProperty.Create(
        nameof(DropdownWidth),
        typeof(double),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: 0.0);

    /// <summary>
    /// If set to a value > 0, the dropdown will use this width in device independent units.
    /// Otherwise it will use the measured width of the entry (reactive to layout).
    /// </summary>
    public double DropdownWidth
    {
        get => (double)GetValue(DropdownWidthProperty);
        set => SetValue(DropdownWidthProperty, value);
    }

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(Dictionary<string, TValue>),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: default(Dictionary<string, TValue>),
        propertyChanged: OnItemsSourceChanged
    );

    /// <summary>
    /// Dictionary of display text to values used for filtering and selection.
    /// </summary>
    public Dictionary<string, TValue>? ItemsSource
    {
        get => (Dictionary<string, TValue>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty PlaceholderTextProperty = BindableProperty.Create(
        nameof(PlaceholderText),
        typeof(string),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: "Pick a colour",
        propertyChanged: OnPlaceholderChanged
    );

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly BindableProperty SelectedValueProperty = BindableProperty.Create(
        nameof(SelectedValue),
        typeof(TValue?),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: default(TValue?),
        BindingMode.TwoWay,
        propertyChanged: OnSelectedValueChanged
    );

    public TValue? SelectedValue
    {
        get => (TValue?)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>
    /// Optional async provider that will be called when the search text changes to fetch results.
    /// When set, the control will use the returned dictionary instead of local filtering.
    /// </summary>
    public Func<string, Task<Dictionary<string, TValue>>>? RemoteSearchProvider { get; set; }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (DictionarySearchBar<TValue>)bindable;
        control.RefreshFilteredResults(control._searchBar.Text);

        // Only sync text when we have a selection to reflect and the user is not actively editing.
        if (!control._isUserEditing && !control._searchBar.IsFocused)
        {
            if (control.SelectedValue is TValue)
            {
                control.SyncTextToSelection();
            }
            else if (control.AllowCustomOptions && !string.IsNullOrWhiteSpace(control.SelectedText))
            {
                control.SyncEntryToSelectedText(control.SelectedText);
            }
        }

        control.UpdateResultsVisibility();
    }

    private static void OnPlaceholderChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (DictionarySearchBar<TValue>)bindable;
        control.UpdatePlaceholder();
    }

    private static void OnSelectedValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (DictionarySearchBar<TValue>)bindable;
        control.SyncTextToSelection();
    }

    private static void OnSelectedTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (DictionarySearchBar<TValue>)bindable;
        control.SyncEntryToSelectedText(newValue as string);
    }

    private async void OnSearchFocused(object? sender, FocusEventArgs e)
    {
        if (RemoteSearchProvider != null)
            await RefreshFromRemoteAsync(string.Empty);
        else
            RefreshFilteredResults(string.Empty);

        // When focused, show the full list then present inline overlay
        _ = ShowOverlayAsync();
    }

    private void OnSearchUnfocused(object? sender, FocusEventArgs e)
    {
        if (_suppressNextUnfocus)
        {
            _suppressNextUnfocus = false;
            _ = Device.InvokeOnMainThreadAsync(() => _searchBar.Focus());
            return;
        }

        // Dismiss any active inline overlay when losing focus
        DismissLocalOverlay();
        _resultsView.IsVisible = false;
        CommitTextSelection(_searchBar.Text);
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged)
            return;

        try
        {
            _isUserEditing = true;

            if (RemoteSearchProvider != null)
            {
                await RefreshFromRemoteAsync(e.NewTextValue);
                if (_overlay != null)
                    UpdateOverlayItems();
                else
                    _ = ShowOverlayAsync();
                return;
            }

            RefreshFilteredResults(e.NewTextValue);

            // If an overlay is already shown, update it; otherwise show a new one
            if (_overlay != null)
                UpdateOverlayItems();
            else
                _ = ShowOverlayAsync();
        }
        finally
        {
            _isUserEditing = false;
        }
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        CommitTextSelection(_searchBar.Text);
    }

    private void RefreshFilteredResults(string? query)
    {
        var text = query?.Trim() ?? string.Empty;
        var source = ItemsSource ?? _defaultOptions ?? new Dictionary<string, TValue>();

        _filteredResults = source
            .Where(kvp => string.IsNullOrWhiteSpace(text) || kvp.Key.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => SearchResult.FromDictionary(kvp.Key, kvp.Value))
            .ToList();

        if (AllowCustomOptions && !string.IsNullOrWhiteSpace(text))
        {
            var hasExactMatch = _filteredResults.Any(r => string.Equals(r.DisplayText, text, StringComparison.OrdinalIgnoreCase));
            if (!hasExactMatch)
                _filteredResults.Insert(0, SearchResult.Custom(text));
        }
    }

    private void UpdateResultsVisibility()
    {
        // Kept for backwards compatibility; we now prefer the page-level overlay to ensure
        // results appear above other page content. Keep internal results view hidden.
        _resultsView.IsVisible = false;
        _resultsView.ItemsSource = _filteredResults;
    }

    private void UpdatePlaceholder()
    {
        _searchBar.Placeholder = PlaceholderText;
    }

    private void SyncTextToSelection()
    {
        _suppressTextChanged = true;

        if (SelectedValue is TValue v)
        {
            var label = FindLabelForValue(v);
            SetSelectedTextInternal(label);
            _searchBar.Text = label;
        }
        else if (AllowCustomOptions && !string.IsNullOrWhiteSpace(SelectedText))
        {
            _searchBar.Text = SelectedText;
        }
        else
        {
            SetSelectedTextInternal(string.Empty);
            _searchBar.Text = string.Empty;
        }

        _suppressTextChanged = false;
    }

    private void SyncEntryToSelectedText(string? newText)
    {
        if (_suppressSelectedTextChanged)
            return;

        _suppressTextChanged = true;
        _searchBar.Text = newText ?? string.Empty;
        _suppressTextChanged = false;

        if (AllowCustomOptions)
            CommitTextSelection(newText);
    }

    private void CommitTextSelection(string? rawText)
    {
        if (!AllowCustomOptions)
            return;

        var text = rawText?.Trim() ?? string.Empty;
        _suppressTextChanged = true;

        if (string.IsNullOrWhiteSpace(text))
        {
            SetSelectedTextInternal(string.Empty);
            SelectedValue = null;
            _searchBar.Text = string.Empty;
            _suppressTextChanged = false;
            return;
        }

        var match = FindMatchingOption(text);
        if (match != null)
        {
            SelectedValue = match.Value;
            SetSelectedTextInternal(match.DisplayText);
            _searchBar.Text = match.DisplayText;
        }
        else
        {
            SetSelectedTextInternal(text);
            SelectedValue = null;
            _searchBar.Text = text;
        }

        _suppressTextChanged = false;
    }

    private SearchResult? FindMatchingOption(string text)
    {
        var source = ItemsSource ?? _defaultOptions;
        if (source == null)
            return null;

        foreach (var kvp in source)
        {
            if (string.Equals(kvp.Key, text, StringComparison.OrdinalIgnoreCase))
                return SearchResult.FromDictionary(kvp.Key, kvp.Value);
        }

        return null;
    }

    private void SetSelectedTextInternal(string? text)
    {
        try
        {
            _suppressSelectedTextChanged = true;
            SetValue(SelectedTextProperty, text);
        }
        finally
        {
            _suppressSelectedTextChanged = false;
        }
    }

    private string FindLabelForValue(TValue value)
    {
        var source = ItemsSource ?? _defaultOptions;

        foreach (var kvp in source)
        {
            if (EqualityComparer<TValue>.Default.Equals(kvp.Value, value))
                return kvp.Key;
        }

        return FormatValue(value);
    }

    private Dictionary<string, TValue> BuildDefaultOptions()
    {
        if (typeof(TValue).IsEnum)
        {
            return Enum
                .GetValues(typeof(TValue))
                .Cast<TValue>()
                .ToDictionary(FormatValue, v => v);
        }

        return new Dictionary<string, TValue>();
    }

    private string FormatValue(TValue value)
    {
        if (DisplayFormatter != null)
            return DisplayFormatter(value);

        if (value is Enum enumVal)
            return EnumDisplayFormatter.FormatName(enumVal.ToString());

        return value.ToString() ?? string.Empty;
    }

    private CollectionView BuildResultsView()
    {
        var resultsView = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 },
            IsVisible = false,
            BackgroundColor = Colors.White
        };

        resultsView.SelectionChanged += OnResultSelected;
        resultsView.ItemTemplate = new DataTemplate(() =>
        {
            var grid = new Grid
            {
                Padding = new Thickness(12, 10),
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star) }
            };
            var label = new Label { VerticalOptions = LayoutOptions.Center };
            label.SetBinding(Label.TextProperty, nameof(SearchResult.DisplayText));
            grid.Add(label);
            return grid;
        });

        return resultsView;
    }

    private void OnResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is SearchResult result)
        {
            ApplySelection(result);
        }

        if (sender is CollectionView cv)
        {
            cv.SelectedItem = null;
        }

        _resultsView.IsVisible = false;
    }

    private void ApplySelection(SearchResult result)
    {
        _suppressTextChanged = true;

        if (result.IsCustom)
        {
            SetSelectedTextInternal(result.DisplayText);
            SelectedValue = null;
            _searchBar.Text = result.DisplayText;
        }
        else
        {
            SelectedValue = result.Value;
            SetSelectedTextInternal(result.DisplayText);
            _searchBar.Text = result.DisplayText;
        }

        _suppressTextChanged = false;
        _searchBar.Unfocus();
        DismissLocalOverlay();
    }

    private class SearchResult
    {
        private SearchResult(string displayText, TValue? value, bool isCustom)
        {
            DisplayText = displayText;
            Value = value;
            IsCustom = isCustom;
        }

        public string DisplayText { get; }

        public TValue? Value { get; }

        public bool IsCustom { get; }

        public static SearchResult FromDictionary(string displayText, TValue value) => new(displayText, value, false);

        public static SearchResult Custom(string displayText) => new(displayText, null, true);
    }

    // Local overlay state
    private AbsoluteLayout? _overlay;
    private Grid? _overlayHost;
    private Border? _overlayContainer;
    private CollectionView? _overlayCollection;
    private ContentPage? _overlayPage;
    private EventHandler? _pageSizeChanged;
    private EventHandler<ScrolledEventArgs>? _scrollHandler;

    private async Task ShowOverlayAsync()
    {
        DismissLocalOverlay();

        if (_filteredResults == null || !_filteredResults.Any())
            return;

        var page = GetTopPage();
        if (page == null)
            return;

        var host = EnsureOverlayHost(page, out var createdHost);
        if (host == null)
            return;

        if (createdHost)
            _suppressNextUnfocus = true;

        _overlay = new AbsoluteLayout { BackgroundColor = Colors.Transparent, InputTransparent = false };

        // scrim to capture outside taps and dismiss
        var scrim = new BoxView { BackgroundColor = Colors.Transparent, Opacity = 1 };
        var scrimTap = new TapGestureRecognizer();
        scrimTap.Tapped += (s, e) => DismissLocalOverlay();
        scrim.GestureRecognizers.Add(scrimTap);
        AbsoluteLayout.SetLayoutBounds(scrim, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(scrim, AbsoluteLayoutFlags.All);
        _overlay.Children.Add(scrim);

        // container
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
        _overlayCollection.ItemTemplate = new DataTemplate(() =>
        {
            var grid = new Grid { Padding = new Thickness(12, 10), ColumnDefinitions = { new ColumnDefinition(GridLength.Star) } };
            var label = new Label { VerticalOptions = LayoutOptions.Center };
            label.SetBinding(Label.TextProperty, nameof(SearchResult.DisplayText));
            grid.Add(label);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                if (s is VisualElement ve && ve.BindingContext is SearchResult sr)
                {
                    ApplySelection(sr);
                }
            };
            grid.GestureRecognizers.Add(tap);

            return grid;
        });

        _overlayContainer.Content = _overlayCollection;

        _overlay.Children.Add(_overlayContainer);

        host.Children.Add(_overlay);
        _overlay.ZIndex = 1000;

        _overlayHost = host;
        _overlayPage = page;

        // position
        await RepositionLocalOverlayAsync();

        // hooks for repositioning
        _pageSizeChanged = async (s, e) => await RepositionLocalOverlayAsync();
        page.SizeChanged += _pageSizeChanged;

        var scrollParent = FindAncestorOfType<ScrollView>(_searchBar);
        if (scrollParent != null)
        {
            _scrollHandler = async (s, e) => await RepositionLocalOverlayAsync();
            scrollParent.Scrolled += _scrollHandler;
        }
    }

    private void UpdateOverlayItems()
    {
        if (_overlayCollection == null)
            return;

        _overlayCollection.ItemsSource = _filteredResults;

        // adjust height if visible
        _ = RepositionLocalOverlayAsync();
    }

    private async Task RepositionLocalOverlayAsync()
    {
        if (_overlayHost == null || _overlay == null || _overlayContainer == null || _searchBar == null)
            return;

        var hostPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(_overlayHost);
        var anchorPos = await NativeCoordinateHelper.GetAbsolutePositionAsync(_searchBar);
        var anchorHeight = _searchBar.Height;

        var localX = Math.Max(8, anchorPos.X - hostPos.X);
        var localY = anchorPos.Y - hostPos.Y + anchorHeight + 6; // small gap

        var pageHeight = _overlayHost.Height > 0 ? _overlayHost.Height : (Application.Current?.MainPage?.Height ?? 0);
        var availableBelow = Math.Max(0, pageHeight - localY - 8);
        var availableAbove = Math.Max(0, anchorPos.Y - hostPos.Y - 8);

        var visibleItems = Math.Min(4, Math.Max(1, _filteredResults.Count));
        var desiredHeight = Math.Min(DefaultDropdownMaxHeight, visibleItems * 48);
        double finalHeight = desiredHeight;

        if (availableBelow < desiredHeight && availableAbove >= desiredHeight)
        {
            localY = Math.Max(8, anchorPos.Y - hostPos.Y - desiredHeight - 6);
        }
        else if (availableBelow < desiredHeight && availableAbove < desiredHeight)
        {
            if (availableBelow >= availableAbove)
            {
                finalHeight = Math.Max(48, availableBelow);
            }
            else
            {
                finalHeight = Math.Max(48, availableAbove);
                localY = Math.Max(8, anchorPos.Y - hostPos.Y - finalHeight - 6);
            }
        }

        var pageWidth = _overlayHost.Width > 0 ? _overlayHost.Width : (Application.Current?.MainPage?.Width ?? 0);

        // Determine dropdown width with this priority:
        // 1) explicit DropdownWidth (if > 0)
        // 2) measured entry width (reactive to layout)
        // 3) page width fallback (pageWidth - padding)
        double width;
        if (DropdownWidth > 0)
            width = DropdownWidth;
        else if (_searchBar.Width > 0)
            width = _searchBar.Width;
        else
            width = Math.Max(120, pageWidth - 16); // fallback when measurements aren't ready

        // Clamp to page width with small margins
        width = Math.Min(width, Math.Max(0, pageWidth - 16));

        if (localX + width + 8 > pageWidth)
            localX = Math.Max(8, pageWidth - width - 8);

        AbsoluteLayout.SetLayoutBounds(_overlayContainer, new Rect(localX, localY, width, finalHeight));
        AbsoluteLayout.SetLayoutFlags(_overlayContainer, AbsoluteLayoutFlags.None);

        if (_overlayContainer?.Content is CollectionView cv)
        {
            cv.HeightRequest = finalHeight;
        }
    }

    private void DismissLocalOverlay()
    {
        if (_overlayHost != null && _overlay != null && _overlayHost.Children.Contains(_overlay))
        {
            _overlayHost.Children.Remove(_overlay);
        }

        if (_overlayPage != null && _pageSizeChanged != null)
        {
            _overlayPage.SizeChanged -= _pageSizeChanged;
        }
        if (_overlayPage != null && _scrollHandler != null)
        {
            var sc = FindAncestorOfType<ScrollView>(_searchBar);
            if (sc != null)
                sc.Scrolled -= _scrollHandler;
        }

        _overlay = null;
        _overlayHost = null;
        _overlayCollection = null;
        _overlayContainer = null;
        _overlayPage = null;
        _pageSizeChanged = null;
        _scrollHandler = null;
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
                ContentPage cp => cp,
                _ => null
            };
    }

    private Grid? EnsureOverlayHost(ContentPage page, out bool createdHost)
    {
        createdHost = false;
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
        createdHost = true;
        return root;
    }

    private void InitializeOverlayHostIfNeeded()
    {
        if (_overlayHostInitialized)
            return;

        var page = GetTopPage();
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

    private static T? FindAncestorOfType<T>(Element? start) where T : VisualElement
    {
        var current = start?.Parent;
        while (current != null)
        {
            if (current is T t)
                return t;
            current = current.Parent;
        }

        return null;
    }
}
