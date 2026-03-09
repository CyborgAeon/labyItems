using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace labyItems.Controls;

public partial class MultiChipGroup : ContentView
{
    private INotifyCollectionChanged? _itemsCollection;

    public MultiChipGroup()
    {
        InitializeComponent();
        Container.Wrap = WrapMode;
        Rebuild();
    }

    public static readonly BindableProperty WrapModeProperty = BindableProperty.Create(
        nameof(WrapMode),
        typeof(FlexWrap),
        typeof(MultiChipGroup),
        FlexWrap.Wrap,
        propertyChanged: OnWrapModeChanged
    );

    public FlexWrap WrapMode
    {
        get => (FlexWrap)GetValue(WrapModeProperty);
        set => SetValue(WrapModeProperty, value);
    }

    private static void OnWrapModeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (MultiChipGroup)bindable;
        control.Container.Wrap = (FlexWrap)newValue;
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
        control.AttachItemsCollection(oldValue as INotifyCollectionChanged, newValue as INotifyCollectionChanged);
        control.Rebuild();
    }

    private void AttachItemsCollection(INotifyCollectionChanged? oldCollection, INotifyCollectionChanged? newCollection)
    {
        if (ReferenceEquals(_itemsCollection, oldCollection) && oldCollection != null)
            oldCollection.CollectionChanged -= OnItemsCollectionChanged;

        if (oldCollection != null && !ReferenceEquals(oldCollection, newCollection))
            oldCollection.CollectionChanged -= OnItemsCollectionChanged;

        _itemsCollection = newCollection;
        if (_itemsCollection != null)
            _itemsCollection.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Rebuild();

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

    private void AttachSelectedCollection(INotifyCollectionChanged? oldCollection, INotifyCollectionChanged? newCollection)
    {
        if (oldCollection != null)
            oldCollection.CollectionChanged -= OnSelectedCollectionChanged;
        if (newCollection != null)
            newCollection.CollectionChanged += OnSelectedCollectionChanged;
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

    private Border CreateChip(string text)
    {
        var palette = ResolvePalette();

        var label = new Label
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = palette.UnselectedText,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap,
        };

        var border = new Border
        {
            Content = label,
            Padding = new Thickness(12, 6),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            StrokeThickness = 1,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(4),
            MinimumHeightRequest = 34,
            MinimumWidthRequest = 0,
            BindingContext = text,
        };

        ApplyPalette(border, palette.UnselectedBackground, palette.UnselectedBorder, palette.UnselectedText);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) => ToggleSelection(text);
        border.GestureRecognizers.Add(tap);

        return border;
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
        var palette = ResolvePalette();

        foreach (var child in Container.Children.OfType<Border>())
        {
            if (child.BindingContext is not string text)
                continue;

            var isSelected = SelectedItems != null
                && SelectedItems.Any(x => string.Equals(x, text, System.StringComparison.OrdinalIgnoreCase));

            var background = isSelected ? palette.SelectedBackground : palette.UnselectedBackground;
            var textColor = isSelected ? palette.SelectedText : palette.UnselectedText;
            var border = isSelected ? palette.SelectedBorder : palette.UnselectedBorder;

            ApplyPalette(child, background, border, textColor);
        }
    }

    private static void ApplyPalette(Border border, Color background, Color strokeColor, Color textColor)
    {
        border.Background = new SolidColorBrush(background);
        border.BackgroundColor = background;
        border.Stroke = new SolidColorBrush(strokeColor);
        if (border.Content is Label label)
            label.TextColor = textColor;
    }

    private static ChipPalette ResolvePalette()
    {
        var unselectedBackground = ResolveColor("Gray500", Color.FromArgb("#6E6E6E"));
        var unselectedText = ResolveColor("White", Colors.White);
        var unselectedBorder = ResolveColor("Gray600", Color.FromArgb("#404040"));
        var selectedBackground = ResolveColor("AccentMaroonColor", Color.FromArgb("#7F1D1D"));
        var selectedText = ResolveColor("White", Colors.White);
        var selectedBorder = ResolveColor("Primary", Color.FromArgb("#530000"));
        return new ChipPalette(unselectedBackground, unselectedText, unselectedBorder, selectedBackground, selectedText, selectedBorder);
    }

    private static Color ResolveColor(string key, Color fallback)
    {
        if (Application.Current?.Resources != null
            && Application.Current.Resources.TryGetValue(key, out var value))
        {
            if (value is Color color)
                return color;

            if (value is SolidColorBrush brush)
                return brush.Color;
        }

        return fallback;
    }

    private readonly record struct ChipPalette(
        Color UnselectedBackground,
        Color UnselectedText,
        Color UnselectedBorder,
        Color SelectedBackground,
        Color SelectedText,
        Color SelectedBorder);
}
