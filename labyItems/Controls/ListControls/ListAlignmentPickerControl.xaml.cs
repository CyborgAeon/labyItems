using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using labyItems.Models.Enums;
using Microsoft.Maui.Controls;
using labyItems.Helpers;

namespace labyItems.Controls;

public class ListAlignmentPickerControl : ContentView
{
    private readonly VerticalStackLayout _rowsHost;

    public ListAlignmentPickerControl()
    {
        _rowsHost = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(0) };
        Content = _rowsHost;

        if (Items == null)
            Items = new ObservableCollection<Alignments?> { null };

        HookItemsCollectionChanged(Items);
        RebuildRows();
        UpdateSummaryAndCount();
    }

    #region Bindable Properties

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(ObservableCollection<Alignments?>),
        typeof(ListAlignmentPickerControl),
        defaultValue: null,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnItemsChanged);

    public ObservableCollection<Alignments?> Items
    {
        get => (ObservableCollection<Alignments?>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (ListAlignmentPickerControl)bindable;

        if (oldValue is ObservableCollection<Alignments?> oldCollection)
            control.UnhookItemsCollectionChanged(oldCollection);

        if (newValue is ObservableCollection<Alignments?> newCollection)
        {
            control.HookItemsCollectionChanged(newCollection);
            control.RebuildRows();
            control.UpdateSummaryAndCount();
        }
        else
        {
            control.Items = new ObservableCollection<Alignments?> { null };
        }
    }

    public static readonly BindableProperty MaxItemsProperty = BindableProperty.Create(
        nameof(MaxItems),
        typeof(int),
        typeof(ListAlignmentPickerControl),
        defaultValue: 10);

    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    public static readonly BindableProperty SummaryTextProperty = BindableProperty.Create(
        nameof(SummaryText),
        typeof(string),
        typeof(ListAlignmentPickerControl),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.OneWayToSource);

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public static readonly BindableProperty ValueCountProperty = BindableProperty.Create(
        nameof(ValueCount),
        typeof(int),
        typeof(ListAlignmentPickerControl),
        defaultValue: 0,
        defaultBindingMode: BindingMode.OneWayToSource);

    public int ValueCount
    {
        get => (int)GetValue(ValueCountProperty);
        set => SetValue(ValueCountProperty, value);
    }

    public static readonly BindableProperty CountLabelFormatProperty = BindableProperty.Create(
        nameof(CountLabelFormat),
        typeof(string),
        typeof(ListAlignmentPickerControl),
        defaultValue: "{0}",
        propertyChanged: OnCountLabelFormatChanged);

    public string CountLabelFormat
    {
        get => (string)GetValue(CountLabelFormatProperty);
        set => SetValue(CountLabelFormatProperty, value);
    }

    private static void OnCountLabelFormatChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (ListAlignmentPickerControl)bindable;
        control.UpdateSummaryAndCount();
    }

    public static readonly BindableProperty CountLabelTextProperty = BindableProperty.Create(
        nameof(CountLabelText),
        typeof(string),
        typeof(ListAlignmentPickerControl),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.OneWayToSource);

    public string CountLabelText
    {
        get => (string)GetValue(CountLabelTextProperty);
        set => SetValue(CountLabelTextProperty, value);
    }

    #endregion

    #region Collection handling

    private void HookItemsCollectionChanged(ObservableCollection<Alignments?> collection)
    {
        if (collection == null) return;
        collection.CollectionChanged += Items_CollectionChanged;
    }

    private void UnhookItemsCollectionChanged(ObservableCollection<Alignments?> collection)
    {
        if (collection == null) return;
        collection.CollectionChanged -= Items_CollectionChanged;
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Don't rebuild on Replace – keeps keyboard/focus stable
        if (e.Action == NotifyCollectionChangedAction.Add ||
            e.Action == NotifyCollectionChangedAction.Remove ||
            e.Action == NotifyCollectionChangedAction.Move ||
            e.Action == NotifyCollectionChangedAction.Reset)
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

    private View CreateRow(int index, Alignments? initialValue)
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

        // Your existing picker for alignment
        var picker = new AlignmentsPicker
        {
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        picker.SelectedValue = initialValue;
        picker.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AlignmentsPicker.SelectedValue))
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
            if (Items.Count <= 1)
            {
                Items[0] = null;
            }
            else
            {
                Items[index] = null;
            }

            picker.SelectedValue = null;
            UpdateSummaryAndCount();
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
            if (Items == null) return;
            if (Items.Count >= MaxItems) return;

            var currentValue = (index >= 0 && index < Items.Count) ? Items[index] : null;
            var insertIndex = System.Math.Clamp(index + 1, 0, Items.Count);
            Items.Insert(insertIndex, currentValue);
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
            .Where(x => x.HasValue)
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
