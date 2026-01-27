using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using labyItems.Infrastructure;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

namespace labyItems.Controls;

public partial class MultiChipGroup : ContentView
{
    public MultiChipGroup()
    {
        InitializeComponent();
        Rebuild();
    }

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(IEnumerable<string>),
        typeof(MultiChipGroup),
        defaultValue: null,
        propertyChanged: OnItemsChanged
    );

    public IEnumerable<string> Items
    {
        get => (IEnumerable<string>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (MultiChipGroup)bindable;
        control.Rebuild();
    }

    public static readonly BindableProperty SelectedItemsProperty = BindableProperty.Create(
        nameof(SelectedItems),
        typeof(IList<string>),
        typeof(MultiChipGroup),
        default(IList<string>),
        BindingMode.TwoWay,
        propertyChanged: OnSelectedItemsChanged
    );

    public IList<string> SelectedItems
    {
        get => (IList<string>)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    private static void OnSelectedItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (MultiChipGroup)bindable;
        control.AttachSelectedCollection(oldValue as INotifyCollectionChanged, newValue as INotifyCollectionChanged);
        control.UpdateVisualState();
    }

    private INotifyCollectionChanged? _selectedCollection;

    private void AttachSelectedCollection(INotifyCollectionChanged? oldCollection, INotifyCollectionChanged? newCollection)
    {
        if (oldCollection != null)
            oldCollection.CollectionChanged -= OnSelectedCollectionChanged;
        if (newCollection != null)
            newCollection.CollectionChanged += OnSelectedCollectionChanged;
        _selectedCollection = newCollection;
    }

    private void OnSelectedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateVisualState();

    private void Rebuild()
    {
        Container.Children.Clear();

        if (Items == null)
            return;

        foreach (var item in Items)
        {
            var chip = CreateChip(item);
            Container.Children.Add(chip);
        }

        UpdateVisualState();
    }

    private Button CreateChip(string text)
    {
        var primary = GetPrimaryColor();
        var primaryText = Colors.Black;

        var button = new Button
        {
            Text = text,
            FontSize = 14,
            BackgroundColor = Colors.Transparent,
            TextColor = primary,
            Padding = new Thickness(10, 4),
            CornerRadius = 4,
            BorderColor = primary,
            BorderWidth = 1,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(4),
            MinimumHeightRequest = 28,
        };

        button.Clicked += (_, __) => ToggleSelection(text);

        return button;
    }

    private void ToggleSelection(string text)
    {
        if (SelectedItems == null)
        {
            SelectedItems = new ObservableCollection<string>();
            AttachSelectedCollection(null, SelectedItems as INotifyCollectionChanged);
        }

        var existing = SelectedItems.FirstOrDefault(x => string.Equals(x, text, System.StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            SelectedItems.Remove(existing);
        else
            SelectedItems.Add(text);

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        var primary = GetPrimaryColor();
        var primaryText = Colors.White;

        foreach (var child in Container.Children.OfType<Button>())
        {
            var isSelected = SelectedItems != null
                && SelectedItems.Any(x => string.Equals(x, child.Text, System.StringComparison.OrdinalIgnoreCase));

            child.BackgroundColor = isSelected ? primary : Colors.Transparent;
            child.TextColor = isSelected ? primaryText : primary;
            child.BorderColor = primary;
        }
    }

    private Color GetPrimaryColor()
    {
        if (
            Application.Current?.Resources != null
            && Application.Current.Resources.TryGetValue("Primary", out var value)
            && value is Color c
        )
        {
            return c;
        }

        return Colors.Black;
    }
}
