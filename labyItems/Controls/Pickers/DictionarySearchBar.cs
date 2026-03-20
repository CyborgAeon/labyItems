using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
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
public partial class DictionarySearchBar<TValue> : ContentView
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
            SelectionMode = SelectionMode.None,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 },
            IsVisible = false,
            BackgroundColor = Colors.White
        };

        resultsView.ItemTemplate = BuildResultTemplate();

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

    private DataTemplate BuildResultTemplate()
    {
        return new DataTemplate(() =>
        {
            var grid = new Grid
            {
                Padding = new Thickness(12, 10),
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star) }
            };

            var label = new Label { VerticalOptions = LayoutOptions.Center };
            label.SetBinding(Label.TextProperty, nameof(SearchResult.DisplayText));
            grid.Add(label);

            var tap = new TapGestureRecognizer();
            tap.Tapped += OnResultTapped;
            grid.GestureRecognizers.Add(tap);

            return grid;
        });
    }

    private void OnResultTapped(object? sender, TappedEventArgs _)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not SearchResult result)
            return;

        ApplySelection(result);
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

    // Overlay and keyboard logic are split into partial files to keep responsibilities focused.
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
            try
            {
                dismiss();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DictionarySearchBar] dismiss failed: {ex}");
            }
        }
    }
}
