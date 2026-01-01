using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls;

/// <summary>
/// SearchBar wrapper that displays dictionary-backed results in an overlay above the page content.
/// Works inside grids/inline layouts without affecting surrounding layout.
/// Example XAML: <controls:DictionarySearchBar x:TypeArguments="enums:MagicColours"
/// ItemsSource="{controls:EnumDictionary x:TypeArguments='enums:MagicColours' Exclude='Grey'}"
/// SelectedValue="{Binding MagicColour}"/>
/// </summary>
public class DictionarySearchBar<TValue> : ContentView
    where TValue : struct, Enum
{
    private const double DefaultDropdownMaxHeight = 320;
    private string Exclude = string.Empty;
    private readonly SearchBar _searchBar;
    private readonly CollectionView _resultsView;
    private List<SearchResult> _filteredResults = new();
    private Dictionary<string, TValue> _defaultOptions;
    private bool _suppressTextChanged;

    public DictionarySearchBar()
    {
        _defaultOptions = BuildDefaultOptions();

        _searchBar = new SearchBar
        {
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Margin = new Thickness(0)
        };
        
        _searchBar.SearchIconColor = Colors.Transparent;

        _searchBar.Focused += OnSearchFocused;
        _searchBar.Unfocused += OnSearchUnfocused;
        _searchBar.TextChanged += OnSearchTextChanged;
        _searchBar.SearchButtonPressed += OnSearchButtonPressed;

        _resultsView = BuildResultsView();

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };

        layout.Add(_searchBar);
        Grid.SetRow(_resultsView, 1);
        layout.Add(_resultsView);

        Content = layout;

        UpdatePlaceholder();
        SyncTextToSelection();
    }

    /// <summary>
    /// Optional custom formatter for default enum display text.
    /// Ignored when a custom ItemsSource is provided.
    /// </summary>
    public Func<TValue, string>? DisplayFormatter { get; set; }

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IDictionary<string, TValue>),
        typeof(DictionarySearchBar<TValue>),
        defaultValue: default(IDictionary<string, TValue>),
        propertyChanged: OnItemsSourceChanged
    );

    /// <summary>
    /// Dictionary of display text to values used for filtering and selection.
    /// </summary>
    public IDictionary<string, TValue>? ItemsSource
    {
        get => (IDictionary<string, TValue>?)GetValue(ItemsSourceProperty);
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

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (DictionarySearchBar<TValue>)bindable;
        control.RefreshFilteredResults(control._searchBar.Text);
        control.SyncTextToSelection();
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

    private void OnSearchFocused(object? sender, FocusEventArgs e)
    {
        // When focused, show the full list then filter as the user types
        RefreshFilteredResults(string.Empty);
        UpdateResultsVisibility();
    }

    private void OnSearchUnfocused(object? sender, FocusEventArgs e)
    {
        _resultsView.IsVisible = false;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged)
            return;

        RefreshFilteredResults(e.NewTextValue);

        UpdateResultsVisibility();
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        RefreshFilteredResults(_searchBar.Text);
        UpdateResultsVisibility();
    }

    private void RefreshFilteredResults(string? query)
    {
        var text = query?.Trim() ?? string.Empty;
        var source = ItemsSource ?? _defaultOptions;

        _filteredResults = source
            .Where(kvp => string.IsNullOrWhiteSpace(text) || kvp.Key.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => new SearchResult(kvp.Key, kvp.Value))
            .ToList();
    }

    private void UpdateResultsVisibility()
    {
        var hasResults = _filteredResults.Any();
        _resultsView.IsVisible = hasResults && _searchBar.IsFocused;
        _resultsView.HeightRequest = hasResults
            ? Math.Min(DefaultDropdownMaxHeight, Math.Max(44, _filteredResults.Count * 44))
            : 0;

        _resultsView.ItemsSource = _filteredResults;
    }

    private void UpdatePlaceholder()
    {
        _searchBar.Placeholder = PlaceholderText;
    }

    private void SyncTextToSelection()
    {
        _suppressTextChanged = true;

        if (SelectedValue.HasValue)
        {
            _searchBar.Text = FindLabelForValue(SelectedValue.Value);
        }
        else
        {
            _searchBar.Text = string.Empty;
        }

        _suppressTextChanged = false;
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
        return Enum
            .GetValues(typeof(TValue))
            .Cast<TValue>()
            .ToDictionary(FormatValue, v => v);
    }

    private string FormatValue(TValue value)
    {
        return DisplayFormatter?.Invoke(value) ?? value.ToString();
    }

    private CollectionView BuildResultsView()
    {
        var resultsView = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 },
            IsVisible = false,
            BackgroundColor = Colors.Transparent
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
            _suppressTextChanged = true;
            SelectedValue = result.Value;
            _searchBar.Text = result.DisplayText;
            _suppressTextChanged = false;
            _searchBar.Unfocus();
        }

        if (sender is CollectionView cv)
        {
            cv.SelectedItem = null;
        }

        _resultsView.IsVisible = false;
    }

    private class SearchResult
    {
        public SearchResult(string displayText, TValue value)
        {
            DisplayText = displayText;
            Value = value;
        }

        public string DisplayText { get; }

        public TValue Value { get; }
    }
}
