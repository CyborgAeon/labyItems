using System.Collections.Generic;
using System.Linq;
using labyItems.Infrastructure;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

namespace labyItems.Controls;

public partial class ChipGroup : ContentView
{
    public ChipGroup()
    {
        InitializeComponent();
        Rebuild();
    }

    // -------- Items (labels for chips) --------
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
        control.Rebuild();
    }

    // -------- Selected item (single-choice, can be null) --------
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

    // -------- UI building --------
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
            FontSize = 14, // Slightly bigger looks better for chips
            BackgroundColor = Colors.Transparent,
            TextColor = primary,
            Padding = new Thickness(10, 4), // <-- 10 left/right, 4 top/bottom
            CornerRadius = 4, // rounder pill
            BorderColor = primary,
            BorderWidth = 1,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(4),
            MinimumHeightRequest = 28, // helps maintain a clean shape
        };

        // Do not force chips to grow — let them size to their content so the layout measures height correctly

        button.Clicked += (_, __) =>
        {
            // Clicking the same chip again clears the selection (null)
            if (SelectedItem == text)
                SelectedItem = null;
            else
                SelectedItem = text;
        };

        return button;
    }

    private void UpdateVisualState()
    {
        var primary = GetPrimaryColor();
        var primaryText = Colors.White;

        foreach (var child in Container.Children.OfType<Button>())
        {
            bool isSelected = child.Text == SelectedItem;

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

        return ColourScheme.Primary;
    }
}
