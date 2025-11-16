using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui.Controls;
using labyItems.Helpers;

namespace labyItems.Controls;

public partial class RepeatingEntryList : ContentView
{
    private readonly VerticalStackLayout _rowsHost;

    public RepeatingEntryList()
    {
        _rowsHost = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(0) };

        Content = _rowsHost;

        // Ensure we always have a collection instance
        if (Items == null)
            Items = new ObservableCollection<string?>();

        HookItemsCollectionChanged(Items);
        RebuildRows();
        UpdateSummaryAndCount();
    }

    #region Bindable Properties

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(ObservableCollection<string?>),
        typeof(RepeatingEntryList),
        defaultValue: null,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnItemsChanged
    );

    public ObservableCollection<string?> Items
    {
        get => (ObservableCollection<string?>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (RepeatingEntryList)bindable;

        if (oldValue is ObservableCollection<string?> oldCollection)
            control.UnhookItemsCollectionChanged(oldCollection);

        if (newValue is ObservableCollection<string?> newCollection)
        {
            control.HookItemsCollectionChanged(newCollection);
            control.RebuildRows();
            control.UpdateSummaryAndCount();
        }
        else
        {
            // Ensure we always have something usable
            control.Items = new ObservableCollection<string?>();
        }
    }

    public static readonly BindableProperty MaxItemsProperty = BindableProperty.Create(
        nameof(MaxItems),
        typeof(int),
        typeof(RepeatingEntryList),
        defaultValue: 10
    );

    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    // Expose summary of all non-empty values (e.g. joined by ", ")
    public static readonly BindableProperty SummaryTextProperty = BindableProperty.Create(
        nameof(SummaryText),
        typeof(string),
        typeof(RepeatingEntryList),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    // Expose count of non-empty values
    public static readonly BindableProperty ValueCountProperty = BindableProperty.Create(
        nameof(ValueCount),
        typeof(int),
        typeof(RepeatingEntryList),
        defaultValue: 0,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public int ValueCount
    {
        get => (int)GetValue(ValueCountProperty);
        set => SetValue(ValueCountProperty, value);
    }

    #endregion

    #region Collection change handling

    private void HookItemsCollectionChanged(ObservableCollection<string?> collection)
    {
        if (collection == null)
            return;
        collection.CollectionChanged += Items_CollectionChanged;
    }

    private void UnhookItemsCollectionChanged(ObservableCollection<string?> collection)
    {
        if (collection == null)
            return;
        collection.CollectionChanged -= Items_CollectionChanged;
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildRows();
        UpdateSummaryAndCount();
    }

    #endregion

    #region UI building

    private void RebuildRows()
    {
        _rowsHost.Children.Clear();

        if (Items == null || Items.Count == 0)
            return;

        for (int i = 0; i < Items.Count; i++)
        {
            _rowsHost.Children.Add(CreateRow(i, Items[i]));
        }
    }

    public static readonly BindableProperty CountLabelFormatProperty = BindableProperty.Create(
        nameof(CountLabelFormat),
        typeof(string),
        typeof(RepeatingEntryList),
        defaultValue: "{0}",
        propertyChanged: OnCountLabelFormatChanged
    );

    public string CountLabelFormat
    {
        get => (string)GetValue(CountLabelFormatProperty);
        set => SetValue(CountLabelFormatProperty, value);
    }

    private static void OnCountLabelFormatChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        var control = (RepeatingEntryList)bindable;
        control.UpdateSummaryAndCount(); // recompute label when format changes
    }

    public static readonly BindableProperty CountLabelTextProperty = BindableProperty.Create(
        nameof(CountLabelText),
        typeof(string),
        typeof(RepeatingEntryList),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public string CountLabelText
    {
        get => (string)GetValue(CountLabelTextProperty);
        set => SetValue(CountLabelTextProperty, value);
    }

    private View CreateRow(int index, string? initialValue)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing = 4,
        };

        var entry = new Entry
        {
            Text = initialValue,
            HorizontalOptions = LayoutOptions.FillAndExpand,
        };

        entry.TextChanged += (s, e) =>
        {
            if (Items == null || index < 0 || index >= Items.Count)
                return;

            // Treat empty/whitespace as null
            var text = string.IsNullOrWhiteSpace(e.NewTextValue) ? null : e.NewTextValue;

            Items[index] = text;
            UpdateSummaryAndCount();
        };

        var deleteButton = new Button
        {
            Text = "🗑",
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = new Thickness(0),
            BackgroundColor = "Primary".GetColorByKey(),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        deleteButton.Clicked += (s, e) =>
        {
            if (Items == null || (Items.Count - 1) == 0)
                return;

            Items.RemoveAt(index);
        };

        var addButton = new Button
        {
            Text = "+",
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = new Thickness(0),
            BackgroundColor = "Primary".GetColorByKey(),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        addButton.Clicked += (s, e) =>
        {
            if (Items == null)
                return;
            if (Items.Count >= MaxItems)
                return;

            var currentValue = (index >= 0 && index < Items.Count) ? Items[index] : null;

            var insertIndex = Math.Clamp(index + 1, 0, Items.Count);
            Items.Insert(insertIndex, currentValue);
        };

        grid.Add(entry, 0, 0);
        grid.Add(deleteButton, 1, 0);
        grid.Add(addButton, 2, 0);

        return grid;
    }

    #endregion

    #region Summary / Count

    private void UpdateSummaryAndCount()
    {
        if (Items == null || Items.Count == 0)
        {
            ValueCount = 0;
            SummaryText = string.Empty;
            CountLabelText = string.Format(CountLabelFormat ?? "{0}", 0);
            return;
        }

        var nonEmpty = Items.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        ValueCount = nonEmpty.Count;
        SummaryText = string.Join(", ", nonEmpty);

        var format = CountLabelFormat ?? "{0}";
        CountLabelText = string.Format(format, ValueCount);
    }

    #endregion
}
