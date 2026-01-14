using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using labyItems.Helpers;
using labyItems.Controls.Pickers;
using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public class EnumPicker<TEnum> : ContentView
    where TEnum : struct, Enum
{
    protected Picker? InnerPicker { get; private set; }
    protected Entry? SearchEntry { get; private set; }
    protected CollectionView? SuggestionsView { get; private set; }
    private EnumDisplayConverter<TEnum>? _displayConverter;
    private bool _suppressTextEvents;
    private bool _areSuggestionsVisible;
    private object? _activeOverlay;

    public ObservableCollection<TEnum> FilteredOptions { get; } = new();

    public static readonly BindableProperty IsCompactProperty = BindableProperty.Create(
        nameof(IsCompact),
        typeof(bool),
        typeof(EnumPicker<TEnum>),
        defaultValue: false
    );

    public EnumPicker()
    {
        Options = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToList();
        VerticalOptions = LayoutOptions.Start;
    }

    // Show suggestions using an inline overlay attached to the page root
    private async System.Threading.Tasks.Task ShowInlineSuggestionsAsync()
    {
        // Prevent multiple overlays
        if (_activeOverlay != null)
            return;

        var items = FilteredOptions.ToList();
        if (!items.Any())
            return;

        if (SearchEntry != null)
        {
            var pos = await NativeCoordinateHelper.GetAbsolutePositionAsync(SearchEntry);

            // Set width to match the entry if available
            var entryWidth = SearchEntry.Width > 0 ? SearchEntry.Width : 200;

            // Determine a suitable max height for the list
            double maxHeight = Math.Min(320, (FilteredOptions.Count * 56) + 16);
            var pageHeight = Application.Current?.MainPage?.Height ?? double.PositiveInfinity;
            maxHeight = Math.Min(maxHeight, pageHeight * 0.5);

            // Compute initial x/y relative to page coordinates and clamp to page
            double x = pos.X;
            // Place popup just below the search entry with a small gap so the search bar remains visible
            double y = pos.Y + (SearchEntry?.Height ?? 0) + 6; // 6px gap to avoid edge overlap

            var pageWidth = Application.Current?.MainPage?.Width ?? double.PositiveInfinity;
            if (x + entryWidth + 12 > pageWidth)
            {
                x = Math.Max(8, pageWidth - entryWidth - 12);
            }

            if (y + maxHeight + 12 > pageHeight)
            {
                // Not enough space below; open above the entry if possible
                var aboveY = pos.Y - maxHeight;
                if (aboveY > 8)
                    y = aboveY;
                else
                    y = Math.Max(8, pageHeight - maxHeight - 12);
            }

            _activeOverlay = new object(); // marker
            try
            {
                TEnum? result = await InlineSuggestionsOverlay.ShowAsync(SearchEntry, items, FormatOption, entryWidth, maxHeight);

                if (result.HasValue)
                {
                    _suppressTextEvents = true;
                    var selected = result.Value;
                    SelectedValue = selected;
                    SearchEntry.Text = FormatOption(selected);
                    SearchEntry.Unfocus();
                    _suppressTextEvents = false;
                }
                else
                {
                    // Dismissed without selection - clear entry
                    _suppressTextEvents = true;
                    SearchEntry.Text = string.Empty;
                    SearchEntry.Unfocus();
                    _suppressTextEvents = false;
                }
            }
            finally
            {
                _activeOverlay = null;
            }
        }
    }

    // Expose the enum options to bind to the inner Picker's ItemsSource
    public List<TEnum> Options { get; }

    // NEW: Optional custom formatter for displaying enum values
    public Func<TEnum, string>? DisplayFormatter { get; set; }

    public IValueConverter DisplayConverter =>
        _displayConverter ??= new EnumDisplayConverter<TEnum>(this);

    public static readonly BindableProperty PlaceholderTextProperty = BindableProperty.Create(
        nameof(PlaceholderText),
        typeof(string),
        typeof(EnumPicker<TEnum>),
        defaultValue: string.Empty,
        propertyChanged: OnPlaceholderTextChanged
    );

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    private static void OnPlaceholderTextChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        var control = (EnumPicker<TEnum>)bindable;
        control.UpdatePlaceholder();
    }

    private void UpdatePlaceholder()
    {
        if (InnerPicker != null)
        {
            InnerPicker.Title = PlaceholderText;
        }

        if (SearchEntry != null)
        {
            SearchEntry.Placeholder = PlaceholderText;
        }
    }

    public static readonly BindableProperty LabelTextProperty = BindableProperty.Create(
        nameof(LabelText),
        typeof(string),
        typeof(EnumPicker<TEnum>),
        defaultValue: string.Empty
    );

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public static readonly BindableProperty SelectedValueProperty = BindableProperty.Create(
        nameof(SelectedValue),
        typeof(TEnum?),
        typeof(EnumPicker<TEnum>),
        default(TEnum?),
        BindingMode.TwoWay,
        propertyChanged: OnSelectedValueChanged
    );

    public TEnum? SelectedValue
    {
        get => (TEnum?)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    public bool AreSuggestionsVisible
    {
        get => _areSuggestionsVisible;
        private set
        {
            if (_areSuggestionsVisible == value)
                return;
            _areSuggestionsVisible = value;
            OnPropertyChanged(nameof(AreSuggestionsVisible));
        }
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    protected void RegisterInnerPicker(Picker picker)
    {
        InnerPicker = picker;
        UpdatePlaceholder();
        AndroidPickerHelper.PreventTypingOpeningPicker(picker);

        // Hook up a single generic converter that uses DisplayFormatter
        InnerPicker.ItemDisplayBinding = new Binding(".") { Converter = DisplayConverter, };
    }

    protected void RegisterSearchEntry(Entry entry, CollectionView suggestionsView)
    {
        SearchEntry = entry;
        SuggestionsView = suggestionsView;

        SuggestionsView.ItemsSource = FilteredOptions;
        SuggestionsView.SelectionChanged += OnSuggestionSelected;

        entry.TextChanged += OnSearchTextChanged;
        entry.Focused += OnSearchEntryFocused;
        entry.Unfocused += OnSearchEntryUnfocused;

        UpdatePlaceholder();
        RefreshFilteredOptions(entry.Text);
        SyncSearchTextToSelection();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (InnerPicker != null)
        {
            AndroidPickerHelper.PreventTypingOpeningPicker(InnerPicker);
        }
    }

    private static void OnSelectedValueChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        var control = (EnumPicker<TEnum>)bindable;
        control.SyncSearchTextToSelection();
    }

    private void OnSearchEntryFocused(object? sender, FocusEventArgs e)
    {
        RefreshFilteredOptions(SearchEntry?.Text);

        // Show Popup overlay for suggestions
        if ((SearchEntry?.IsFocused ?? false) && FilteredOptions.Any())
        {
            _ = ShowInlineSuggestionsAsync();
            // keep inline suggestions hidden
            AreSuggestionsVisible = false;
        }
    }

    private void OnSearchEntryUnfocused(object? sender, FocusEventArgs e)
    {
        AreSuggestionsVisible = false;

        // Dismiss any inline overlay if open
        try
        {
            InlineSuggestionsOverlay.Dismiss();
        }
        catch
        {
            // ignore
        }
    }

    protected void HideSuggestions()
    {
        AreSuggestionsVisible = false;
        if (SearchEntry?.IsFocused == true)
        {
            SearchEntry.Unfocus();
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTextEvents)
            return;

        RefreshFilteredOptions(e.NewTextValue);

        if (!MatchesSelectedValue(e.NewTextValue))
        {
            SelectedValue = null;
        }
    }

    private void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TEnum selected)
        {
            _suppressTextEvents = true;
            SelectedValue = selected;
            if (SearchEntry != null)
            {
                SearchEntry.Text = FormatOption(selected);
                SearchEntry.Unfocus();
            }

            _suppressTextEvents = false;
        }

        if (SuggestionsView != null)
        {
            SuggestionsView.SelectedItem = null;
        }

        AreSuggestionsVisible = false;
    }

    private void RefreshFilteredOptions(string? searchText)
    {
        var query = searchText?.Trim() ?? string.Empty;
        var matches = Options
            .Where(option =>
                string.IsNullOrEmpty(query)
                    || FormatOption(option).Contains(query, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        FilteredOptions.Clear();
        foreach (var match in matches)
        {
            FilteredOptions.Add(match);
        }

        // Keep any active overlay list in sync with the latest filter
        InlineSuggestionsOverlay.UpdateItems(FilteredOptions);

        // If the user is focused in the search entry, show a top-level popup overlay
        if ((SearchEntry?.IsFocused ?? false) && FilteredOptions.Any())
        {
            // Show inline overlay (async fire-and-forget)
            _ = ShowInlineSuggestionsAsync();
            AreSuggestionsVisible = false; // keep inline suggestions hidden
        }
        else
        {
            AreSuggestionsVisible = false;
        }
    }

    private void SyncSearchTextToSelection()
    {
        if (SearchEntry == null)
            return;

        _suppressTextEvents = true;
        SearchEntry.Text = SelectedValue.HasValue ? FormatOption(SelectedValue.Value) : string.Empty;
        _suppressTextEvents = false;

        RefreshFilteredOptions(SearchEntry.Text);
    }

    private bool MatchesSelectedValue(string? text)
    {
        if (!SelectedValue.HasValue)
            return string.IsNullOrWhiteSpace(text);

        return string.Equals(
            FormatOption(SelectedValue.Value),
            text?.Trim(),
            StringComparison.OrdinalIgnoreCase
        );
    }

    protected string FormatOption(TEnum value)
    {
        var formatter = DisplayFormatter;
        return formatter != null ? formatter(value) : value.ToString();
    }
}

// Single generic converter for all EnumPicker<TEnum>
public class EnumDisplayConverter<TEnum> : IValueConverter
    where TEnum : struct, Enum
{
    private readonly EnumPicker<TEnum> _owner;

    public EnumDisplayConverter(EnumPicker<TEnum> owner)
    {
        _owner = owner;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TEnum enumValue)
        {
            // Use custom formatter if provided; fall back to ToString()
            var formatter = _owner.DisplayFormatter;
            return formatter != null ? formatter(enumValue) : enumValue.ToString();
        }

        return string.Empty;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
