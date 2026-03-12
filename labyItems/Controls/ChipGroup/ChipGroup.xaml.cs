using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls;

public partial class ChipGroup : ContentView
{
    private INotifyCollectionChanged? _itemsCollection;

    public ChipGroup()
    {
        InitializeComponent();
        Rebuild();
    }

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items),
        typeof(IEnumerable<string>),
        typeof(ChipGroup),
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
        var control = (ChipGroup)bindable;
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

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem),
        typeof(string),
        typeof(ChipGroup),
        default(string),
        BindingMode.TwoWay,
        propertyChanged: OnSelectedItemChanged
    );

    public string SelectedItem
    {
        get => (string)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void OnSelectedItemChanged(
        BindableObject bindable,
        object oldValue,
        object newValue
    )
    {
        var control = (ChipGroup)bindable;
        control.UpdateVisualState();
    }

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
            InputTransparent = true,
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
            IsEnabled = true,
            InputTransparent = false,
            BindingContext = text,
        };

        ApplyPalette(border, palette.UnselectedBackground, palette.UnselectedBorder, palette.UnselectedText);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) =>
        {
            if (string.Equals(SelectedItem, text, System.StringComparison.OrdinalIgnoreCase))
                SelectedItem = null;
            else
                SelectedItem = text;
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private void UpdateVisualState()
    {
        var palette = ResolvePalette();

        foreach (var child in Container.Children.OfType<Border>())
        {
            if (child.BindingContext is not string text)
                continue;

            var isSelected = string.Equals(text, SelectedItem, System.StringComparison.OrdinalIgnoreCase);
            var background = isSelected ? palette.SelectedBackground : palette.UnselectedBackground;
            var textColor = isSelected ? palette.SelectedText : palette.UnselectedText;
            var border = isSelected ? palette.SelectedBorder : palette.UnselectedBorder;

            ApplyPalette(child, background, border, textColor);
        }
    }

    private static void ApplyPalette(Border border, Color background, Color borderColor, Color textColor)
    {
        border.Background = new SolidColorBrush(background);
        border.BackgroundColor = background;
        border.Stroke = new SolidColorBrush(borderColor);

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
