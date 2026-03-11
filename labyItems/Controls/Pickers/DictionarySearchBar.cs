using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Graphics;
using labyItems.Controls.Pickers;
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

/// <summary>
/// SearchBar wrapper that displays dictionary-backed results in an overlay above the page content.
/// Works inside grids/inline layouts without affecting surrounding layout.
/// Example XAML: <controls:DictionarySearchBar x:TypeArguments="enums:MagicColours"
/// ItemsSource="{controls:EnumDictionary x:TypeArguments='enums:MagicColours' Exclude='Grey'}"
/// SelectedValue="{Binding MagicColour}"/>
/// </summary>
public class DictionarySearchBar<TValue> : ContentView
{
    private readonly Action _selfDismisser;
    private const double DefaultDropdownMaxHeight = 320;
    private const double MinimumEntryHeight = 44;
    private const double DropdownEdgeGap = 0;
    private const double KeyboardGuardRatioFallback = 0.35;
    private const double KeyboardGuardMinFallback = 200;
    private const double KeyboardGuardMaxFallback = 360;
    private readonly Entry _searchBar;
    private readonly CollectionView _resultsView;
    private readonly Border _inlineResultsContainer;
    private List<SearchResult> _filteredResults = new();
    private Dictionary<string, TValue> _defaultOptions;
    private bool _suppressTextChanged;
    private bool _suppressSelectedTextChanged;
    private bool _overlayHostInitialized;
    private bool _suppressNextUnfocus;
    private int _remoteRequestId;
    private bool _isUserEditing;
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

    public DictionarySearchBar()
    {
        EnsureKeyboardObserversInitialized();
        _selfDismisser = DismissLocalOverlay;
        DictionaryOverlayRegistry.Register(_selfDismisser);
        _defaultOptions = BuildDefaultOptions();

        _searchBar = new Entry
        {
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Margin = new Thickness(0),
            ClearButtonVisibility = ClearButtonVisibility.Never,
            IsTextPredictionEnabled = false,
            IsSpellCheckEnabled = false,
            IsEnabled = IsEnabled,
            InputTransparent = !IsEnabled
        };

        _searchBar.Focused += OnSearchFocused;
        _searchBar.Unfocused += OnSearchUnfocused;
        _searchBar.TextChanged += OnSearchTextChanged;
        _searchBar.Completed += OnSearchCompleted;

        _resultsView = BuildResultsView();
        _inlineResultsContainer = BuildInlineResultsContainer(_resultsView);
        this.HorizontalOptions = LayoutOptions.FillAndExpand;

        var container = new AbsoluteLayout
        {
            IsClippedToBounds = false,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };

        _searchBar.SizeChanged += (s, e) =>
        {
            var measuredEntryHeight = _searchBar.Height > 0 ? _searchBar.Height : MinimumEntryHeight;
            container.HeightRequest = measuredEntryHeight;
            _inlineResultsContainer.TranslationY = measuredEntryHeight;

            AbsoluteLayout.SetLayoutBounds(_searchBar, new Rect(0, 0, 1, measuredEntryHeight));
            AbsoluteLayout.SetLayoutFlags(_searchBar, AbsoluteLayoutFlags.WidthProportional);

            AbsoluteLayout.SetLayoutBounds(_inlineResultsContainer, new Rect(0, measuredEntryHeight, 1, 0));
            AbsoluteLayout.SetLayoutFlags(_inlineResultsContainer, AbsoluteLayoutFlags.WidthProportional);
        };


        // Add the entry and results view to the overlay container. Results will be shown translated below the entry.
        container.Add(_searchBar);
        container.Add(_inlineResultsContainer);
        AbsoluteLayout.SetLayoutBounds(_searchBar, new Rect(0, 0, 1, AbsoluteLayout.AutoSize));
        AbsoluteLayout.SetLayoutFlags(_searchBar, AbsoluteLayoutFlags.WidthProportional);

        AbsoluteLayout.SetLayoutBounds(_inlineResultsContainer, new Rect(0, 0, 1, AbsoluteLayout.AutoSize));
        AbsoluteLayout.SetLayoutFlags(_inlineResultsContainer, AbsoluteLayoutFlags.WidthProportional);

        Content = container;
        UpdatePlaceholder();
        SyncTextToSelection();
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (UsePageOverlay)
        {
            // Build the overlay host early so first focus doesn't re-parent and steal focus.
            InitializeOverlayHostIfNeeded();
        }
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.NewHandler == null)
        {
            RestoreKeyboardAvoidancePadding();
            DictionaryOverlayRegistry.Unregister(_selfDismisser);
        }
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled))
        {
            _searchBar.IsEnabled = IsEnabled;
            _searchBar.InputTransparent = !IsEnabled;
        }
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

    public static readonly BindableProperty SelectionDisplayMemberPathProperty = BindableProperty.Create(
        nameof(SelectionDisplayMemberPath),
        typeof(string),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: default(string)
    );

    /// <summary>
    /// Optional property name on TValue used for the text shown in the entry after selection.
    /// Useful when the dictionary display label includes metadata you don't want in the textbox.
    /// </summary>
    public string? SelectionDisplayMemberPath
    {
        get => (string?)GetValue(SelectionDisplayMemberPathProperty);
        set => SetValue(SelectionDisplayMemberPathProperty, value);
    }

    public static readonly BindableProperty SelectedValueProperty = BindableProperty.Create(
        nameof(SelectedValue),
        typeof(object),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: null,
        BindingMode.TwoWay,
        propertyChanged: OnSelectedValueChanged
    );

    public TValue? SelectedValue
    {
        get => TryGetSelectedValue(out var value) ? value : default;
        set
        {
            if (value is TValue typedValue)
                SetValue(SelectedValueProperty, typedValue);
            else
                SetValue(SelectedValueProperty, null);
        }
    }

    public static readonly BindableProperty UsePageOverlayProperty = BindableProperty.Create(
        nameof(UsePageOverlay),
        typeof(bool),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: true,
        propertyChanged: OnUsePageOverlayChanged);

    /// <summary>
    /// When true, renders results in a page-level overlay; otherwise uses the inline dropdown.
    /// </summary>
    public bool UsePageOverlay
    {
        get => (bool)GetValue(UsePageOverlayProperty);
        set => SetValue(UsePageOverlayProperty, value);
    }

    public static readonly BindableProperty ResultSelectedCommandProperty = BindableProperty.Create(
        nameof(ResultSelectedCommand),
        typeof(ICommand),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: null);

    /// <summary>
    /// Optional command executed after a user clicks a result in the dropdown.
    /// Useful for one-click "select and add" workflows.
    /// </summary>
    public ICommand? ResultSelectedCommand
    {
        get => (ICommand?)GetValue(ResultSelectedCommandProperty);
        set => SetValue(ResultSelectedCommandProperty, value);
    }

    public static readonly BindableProperty KeepFocusOnResultSelectionProperty = BindableProperty.Create(
        nameof(KeepFocusOnResultSelection),
        typeof(bool),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: false);

    /// <summary>
    /// Keeps the entry focused after a result click so the keyboard stays open.
    /// </summary>
    public bool KeepFocusOnResultSelection
    {
        get => (bool)GetValue(KeepFocusOnResultSelectionProperty);
        set => SetValue(KeepFocusOnResultSelectionProperty, value);
    }

    public static readonly BindableProperty KeyboardAvoidanceEnabledProperty = BindableProperty.Create(
        nameof(KeyboardAvoidanceEnabled),
        typeof(bool),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: true);

    /// <summary>
    /// Adds temporary bottom padding to the parent ScrollView while focused so content can scroll above the keyboard.
    /// </summary>
    public bool KeyboardAvoidanceEnabled
    {
        get => (bool)GetValue(KeyboardAvoidanceEnabledProperty);
        set => SetValue(KeyboardAvoidanceEnabledProperty, value);
    }

    public static readonly BindableProperty ClearAfterResultSelectionProperty = BindableProperty.Create(
        nameof(ClearAfterResultSelection),
        typeof(bool),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: false);

    /// <summary>
    /// Clears selected value/text immediately after a result click command executes.
    /// Useful for add-and-continue workflows where the next search should start empty.
    /// </summary>
    public bool ClearAfterResultSelection
    {
        get => (bool)GetValue(ClearAfterResultSelectionProperty);
        set => SetValue(ClearAfterResultSelectionProperty, value);
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
            if (control.TryGetSelectedValue(out _))
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

    private static void OnUsePageOverlayChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (DictionarySearchBar<TValue>)bindable;
        if (newValue is bool useOverlay && useOverlay)
        {
            control.InitializeOverlayHostIfNeeded();
            control._inlineResultsContainer.IsVisible = false;
            return;
        }

        control.DismissLocalOverlay();
        control.UpdateResultsVisibility();
    }

    private async void OnSearchFocused(object? sender, FocusEventArgs e)
    {
        if (!IsEnabled)
            return;

        if (RemoteSearchProvider != null)
            await RefreshFromRemoteAsync(string.Empty);
        else
            RefreshFilteredResults(string.Empty);

        if (KeyboardAvoidanceEnabled)
        {
            ApplyKeyboardAvoidancePadding();
            await EnsureAnchorVisibleAsync(GetDesiredDropdownHeight());
        }

        // When focused, show the full list and present dropdown.
        await ShowResultsAsync();

        if (KeyboardAvoidanceEnabled)
        {
            _ = Device.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Delay(180);
                ApplyKeyboardAvoidancePadding();
                await EnsureAnchorVisibleAsync(GetDesiredDropdownHeight());
                await RepositionLocalOverlaySafeAsync();
            });
        }
    }

    private void OnSearchUnfocused(object? sender, FocusEventArgs e)
    {
        if (!IsEnabled)
            return;

        if (_suppressNextUnfocus)
        {
            _suppressNextUnfocus = false;
            _ = Device.InvokeOnMainThreadAsync(() => _searchBar.Focus());
            return;
        }

        // Give result taps a moment to complete before dismissing the overlay.
        if (_overlay != null)
        {
            _ = Device.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Delay(120);
                if (_searchBar.IsFocused)
                    return;

                DismissLocalOverlay();
                _inlineResultsContainer.IsVisible = false;

                if (KeyboardAvoidanceEnabled)
                    RestoreKeyboardAvoidancePadding();

                CommitTextSelection(_searchBar.Text);
            });
            return;
        }

        DismissLocalOverlay();
        _inlineResultsContainer.IsVisible = false;

        if (KeyboardAvoidanceEnabled)
            RestoreKeyboardAvoidancePadding();

        CommitTextSelection(_searchBar.Text);
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!IsEnabled)
            return;

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
                    _ = ShowResultsAsync();
                return;
            }

            RefreshFilteredResults(e.NewTextValue);

            // If an overlay is already shown, update it; otherwise show a new one
            if (_overlay != null)
                UpdateOverlayItems();
            else
                _ = ShowResultsAsync();
        }
        finally
        {
            _isUserEditing = false;
        }
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        if (!IsEnabled)
            return;

        if (_filteredResults == null || _filteredResults.Count == 0)
        {
            DismissLocalOverlay();
            _inlineResultsContainer.IsVisible = false;
            _searchBar.Unfocus();
            return;
        }

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
        _resultsView.ItemsSource = _filteredResults;
        _inlineResultsContainer.IsVisible = !UsePageOverlay
                                            && _searchBar.IsFocused
                                            && _filteredResults is { Count: > 0 };
    }

    private void UpdatePlaceholder()
    {
        _searchBar.Placeholder = PlaceholderText;
    }

    private void SyncTextToSelection()
    {
        _suppressTextChanged = true;

        if (TryGetSelectedValue(out var v))
        {
            if (TryFindLabelForValue(v, out var label))
            {
                var displayText = GetSelectionDisplayText(v, label);
                SetSelectedTextInternal(displayText);
                _searchBar.Text = displayText;
            }
            else if (EqualityComparer<TValue>.Default.Equals(v, default))
            {
                SetSelectedTextInternal(string.Empty);
                _searchBar.Text = string.Empty;
            }
            else
            {
                var fallback = FormatValue(v);
                var displayText = GetSelectionDisplayText(v, fallback);
                SetSelectedTextInternal(displayText);
                _searchBar.Text = displayText;
            }
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
            ClearSelectedValueInternal();
            _searchBar.Text = string.Empty;
            _suppressTextChanged = false;
            return;
        }

        var match = FindMatchingOption(text);
        if (match != null)
        {
            SelectedValue = match.Value;
            var displayText = GetSelectionDisplayText(match.Value, match.DisplayText);
            SetSelectedTextInternal(displayText);
            _searchBar.Text = displayText;
        }
        else
        {
            SetSelectedTextInternal(text);
            ClearSelectedValueInternal();
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

    private bool TryGetSelectedValue(out TValue value)
    {
        var raw = GetValue(SelectedValueProperty);
        if (raw is TValue typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    private void ClearSelectedValueInternal()
        => SetValue(SelectedValueProperty, null);

    private bool TryFindLabelForValue(TValue value, out string label)
    {
        var source = ItemsSource ?? _defaultOptions;

        foreach (var kvp in source)
        {
            if (EqualityComparer<TValue>.Default.Equals(kvp.Value, value))
            {
                label = kvp.Key;
                return true;
            }
        }

        label = string.Empty;
        return false;
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

    private static Border BuildInlineResultsContainer(CollectionView resultsView)
    {
        return new Border
        {
            Background = new SolidColorBrush(Colors.White),
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#ECECEC")),
            Padding = new Thickness(0),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            IsVisible = false,
            Content = resultsView
        };
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

        _inlineResultsContainer.IsVisible = false;
    }

    private void ApplySelection(SearchResult result)
    {
        _suppressTextChanged = true;

        if (result.IsCustom)
        {
            SetSelectedTextInternal(result.DisplayText);
            ClearSelectedValueInternal();
            _searchBar.Text = result.DisplayText;
        }
        else
        {
            SelectedValue = result.Value;
            var displayText = GetSelectionDisplayText(result.Value, result.DisplayText);
            SetSelectedTextInternal(displayText);
            _searchBar.Text = displayText;
        }

        _suppressTextChanged = false;
        ExecuteResultSelectedCommand(result);
        if (ClearAfterResultSelection)
            ClearSelectionForNextSearch();
        DismissLocalOverlay();

        if (KeepFocusOnResultSelection)
        {
            _ = Device.InvokeOnMainThreadAsync(() =>
            {
                if (!_searchBar.IsFocused)
                    _searchBar.Focus();
            });
        }
        else
        {
            _searchBar.Unfocus();
        }
    }

    private void ClearSelectionForNextSearch()
    {
        _suppressTextChanged = true;
        ClearSelectedValueInternal();
        SetSelectedTextInternal(string.Empty);
        _searchBar.Text = string.Empty;
        _suppressTextChanged = false;

        RefreshFilteredResults(string.Empty);
        UpdateResultsVisibility();
    }

    private void ExecuteResultSelectedCommand(SearchResult result)
    {
        var command = ResultSelectedCommand;
        if (command == null)
            return;

        var parameter = result.IsCustom ? result.DisplayText : (object?)result.Value;
        if (command.CanExecute(parameter))
            command.Execute(parameter);
        else if (command.CanExecute(null))
            command.Execute(null);
    }

    private string GetSelectionDisplayText(TValue value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(SelectionDisplayMemberPath))
        {
            var type = value?.GetType();
            if (type != null)
            {
                var prop = type.GetProperty(SelectionDisplayMemberPath);
                if (prop != null)
                {
                    var propValue = prop.GetValue(value);
                    if (propValue != null)
                        return propValue.ToString() ?? fallback;
                }

                var field = type.GetField(SelectionDisplayMemberPath);
                if (field != null)
                {
                    var fieldValue = field.GetValue(value);
                    if (fieldValue != null)
                        return fieldValue.ToString() ?? fallback;
                }
            }
        }

        return fallback;
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

        public static SearchResult Custom(string displayText) => new(displayText, default, true);
    }

    // Local overlay state
    private AbsoluteLayout? _overlay;
    private BoxView? _overlayBackdrop;
    private Grid? _overlayHost;
    private Border? _overlayContainer;
    private CollectionView? _overlayCollection;
    private ContentPage? _overlayPage;
    private EventHandler? _pageSizeChanged;
    private EventHandler<ScrolledEventArgs>? _scrollHandler;

    private async Task ShowResultsAsync()
    {
        try
        {
            await ShowOverlayAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DictionarySearchBar] dropdown fallback to inline: {ex}");
            DismissLocalOverlay();
            ShowInlineResults();
        }
    }

    private async Task RepositionLocalOverlaySafeAsync()
    {
        try
        {
            await RepositionLocalOverlayAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DictionarySearchBar] overlay reposition failed: {ex}");
            DismissLocalOverlay();
            ShowInlineResults();
        }
    }

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

        Grid? host;
        bool createdHost;
        try
        {
            host = EnsureOverlayHost(page, out createdHost);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DictionarySearchBar] overlay host setup failed: {ex}");
            ShowInlineResults();
            return;
        }

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
        dismissTap.Tapped += (s, e) =>
        {
            DismissLocalOverlay();
            _searchBar.Unfocus();
        };
        _overlayBackdrop.GestureRecognizers.Add(dismissTap);
        AbsoluteLayout.SetLayoutBounds(_overlayBackdrop, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(_overlayBackdrop, AbsoluteLayoutFlags.All);
        _overlay.Children.Add(_overlayBackdrop);

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
        _overlayContainer.ZIndex = 1;

        _overlay.Children.Add(_overlayContainer);

        host.Children.Add(_overlay);
        _overlay.ZIndex = 1000;

        _overlayHost = host;
        _overlayPage = page;

        // position
        await RepositionLocalOverlaySafeAsync();

        // hooks for repositioning
        _pageSizeChanged = async (s, e) =>
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
            _scrollHandler = async (s, e) => await RepositionLocalOverlaySafeAsync();
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

        // adjust height if visible
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
        var keyboardGuard = ResolveKeyboardAvoidanceBottom();
        var availableBelow = scroll.Height - (anchorTop + _searchBar.Height) - keyboardGuard;
        var requiredSpace = Math.Max(0, desiredDropdownHeight + DropdownEdgeGap);

        if (availableBelow >= requiredSpace)
            return;

        var deficit = requiredSpace - availableBelow;
        var target = Math.Max(0, scroll.ScrollY + deficit + (_searchBar.Height * 0.25));
        await scroll.ScrollToAsync(scroll.ScrollX, target, true);
    }

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
        catch
        {
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
        var root = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Star) },
            StyleId = "__overlay_host__"
        };

        // Re-parent safely: detach content from page before adding to new host.
        page.Content = null;
        root.Children.Add(original);
        page.Content = root;
        createdHost = true;
        return root;
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

            try
            {
                var host = EnsureOverlayHost(page, out _);
                if (host != null)
                    _overlayHostInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DictionarySearchBar] overlay host init skipped: {ex}");
            }
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
            if (current is T t)
                return t;
            current = current.Parent;
        }

        return null;
    }

    private ContentPage? GetOwningPage()
        => FindAncestorOfType<ContentPage>(this)
           ?? FindAncestorOfType<ContentPage>(_searchBar)
           ?? GetTopPage();
}

internal static class DictionaryOverlayRegistry
{
    private static readonly List<Action> _dismissors = new();
    private static readonly object _gate = new();

    public static void Register(Action dismissor)
    {
        if (dismissor == null) return;
        lock (_gate)
        {
            if (!_dismissors.Contains(dismissor))
                _dismissors.Add(dismissor);
        }
    }

    public static void Unregister(Action dismissor)
    {
        if (dismissor == null) return;
        lock (_gate)
        {
            _dismissors.Remove(dismissor);
        }
    }

    public static void DismissAll()
    {
        Action[] snapshot;
        lock (_gate)
        {
            snapshot = _dismissors.ToArray();
        }

        foreach (var dismiss in snapshot)
        {
            try { dismiss(); } catch { /* best effort */ }
        }
    }
}
