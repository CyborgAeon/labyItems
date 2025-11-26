using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using labyItems.Helpers;
using labyItems.Models.Enums;
using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public class ListColourPickerControl : ContentView
{
    private readonly VerticalStackLayout _rowsHost;

    public ListColourPickerControl()
    {
        _rowsHost = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(0) };
        Content = _rowsHost;

        if (Items == null)
            Items = new ObservableCollection<MagicColours?> { null };

        HookItemsCollectionChanged(Items);
        RebuildRows();
        UpdateSummaryAndCount();
    }

    #region Bindable Properties

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(ObservableCollection<MagicColours?>),
        typeof(ListColourPickerControl),
        defaultValue: null,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnItemsChanged
    );

    public ObservableCollection<MagicColours?> Items
    {
        get => (ObservableCollection<MagicColours?>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (ListColourPickerControl)bindable;

        if (oldValue is ObservableCollection<MagicColours?> oldCollection)
            control.UnhookItemsCollectionChanged(oldCollection);

        if (newValue is ObservableCollection<MagicColours?> newCollection)
        {
            control.HookItemsCollectionChanged(newCollection);
            control.RebuildRows();
            control.UpdateSummaryAndCount();
        }
        else
        {
            control.Items = new ObservableCollection<MagicColours?> { null };
        }
    }

    public static readonly BindableProperty MaxItemsProperty = BindableProperty.Create(
        nameof(MaxItems),
        typeof(int),
        typeof(ListColourPickerControl),
        defaultValue: 10
    );

    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    public static readonly BindableProperty SummaryTextProperty = BindableProperty.Create(
        nameof(SummaryText),
        typeof(string),
        typeof(ListColourPickerControl),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public static readonly BindableProperty ValueCountProperty = BindableProperty.Create(
        nameof(ValueCount),
        typeof(int),
        typeof(ListColourPickerControl),
        defaultValue: 0,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public int ValueCount
    {
        get => (int)GetValue(ValueCountProperty);
        set => SetValue(ValueCountProperty, value);
    }

    public static readonly BindableProperty CountLabelFormatProperty = BindableProperty.Create(
        nameof(CountLabelFormat),
        typeof(string),
        typeof(ListColourPickerControl),
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
        var control = (ListColourPickerControl)bindable;
        control.UpdateSummaryAndCount();
    }

    public static readonly BindableProperty CountLabelTextProperty = BindableProperty.Create(
        nameof(CountLabelText),
        typeof(string),
        typeof(ListColourPickerControl),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public string CountLabelText
    {
        get => (string)GetValue(CountLabelTextProperty);
        set => SetValue(CountLabelTextProperty, value);
    }

    #endregion

    #region Collection handling

    private void HookItemsCollectionChanged(ObservableCollection<MagicColours?> collection)
    {
        if (collection == null)
            return;
        collection.CollectionChanged += Items_CollectionChanged;
    }

    private void UnhookItemsCollectionChanged(ObservableCollection<MagicColours?> collection)
    {
        if (collection == null)
            return;
        collection.CollectionChanged -= Items_CollectionChanged;
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Don't rebuild on Replace – keeps keyboard/focus stable
        if (
            e.Action == NotifyCollectionChangedAction.Add
            || e.Action == NotifyCollectionChangedAction.Remove
            || e.Action == NotifyCollectionChangedAction.Move
            || e.Action == NotifyCollectionChangedAction.Reset
        )
        {
            RebuildRows();
        }

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

    private View CreateRow(int index, MagicColours? initialValue)
    {
        if (initialValue == MagicColours.Grey && Items != null && index >= 0 && index < Items.Count)
            Items[index] = null;

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

        // Your existing picker for Colour
        var picker = new MagicColourPicker { HorizontalOptions = LayoutOptions.FillAndExpand };
        picker.SelectedValue = initialValue == MagicColours.Grey ? null : initialValue;
        picker.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MagicColourPicker.SelectedValue))
            {
                if (Items == null || index < 0 || index >= Items.Count)
                    return;

                Items[index] = picker.SelectedValue;
                UpdateSummaryAndCount();
            }
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
            if (Items == null)
                return;

            if (Items.Count == 0)
            {
                return;
            }

            if (Items.Count <= 1)
            {
                // Only one row: clear it if it has a value, otherwise do nothing
                if (Items[0].HasValue)
                {
                    Items[0] = null;
                    picker.SelectedValue = null;
                    UpdateSummaryAndCount();
                }
            }
            else if (index >= 0 && index < Items.Count)
            {
                Items.RemoveAt(index);
                UpdateSummaryAndCount();
            }
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

            var insertIndex = System.Math.Clamp(index + 1, 0, Items.Count);
            Items.Insert(insertIndex, null);
        };

        grid.Add(picker, 0, 0);
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

        var distinctSelected = Items
            .Where(x => x.HasValue && x.Value != MagicColours.Grey)
            .Select(x => x.Value)
            .Distinct()
            .ToList();

        ValueCount = distinctSelected.Count;
        SummaryText = string.Join(", ", distinctSelected); // relies on enum ToString()

        var format = CountLabelFormat ?? "{0}";
        CountLabelText = string.Format(format, ValueCount);
    }

    #endregion
}
