using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Characters;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Controls.Pickers;

public partial class AlignmentGridPicker : ContentView
{
    public static readonly BindableProperty SelectedAlignmentProperty =
        BindableProperty.Create(
            nameof(SelectedAlignment),
            typeof(Alignment?),
            typeof(AlignmentGridPicker),
            null,
            BindingMode.TwoWay,
            propertyChanged: (_, __, ___) => { });

    public static readonly BindableProperty AvailableAlignmentsProperty =
        BindableProperty.Create(
            nameof(AvailableAlignments),
            typeof(IEnumerable<Alignment>),
            typeof(AlignmentGridPicker),
            Enumerable.Empty<Alignment>(),
            propertyChanged: (_, __, ___) => { });

    private readonly Dictionary<Alignment, CellVisual> _cells = new();

    public Alignment? SelectedAlignment
    {
        get => (Alignment?)GetValue(SelectedAlignmentProperty);
        set => SetValue(SelectedAlignmentProperty, value);
    }

    public IEnumerable<Alignment> AvailableAlignments
    {
        get => (IEnumerable<Alignment>)GetValue(AvailableAlignmentsProperty);
        set => SetValue(AvailableAlignmentsProperty, value);
    }

    public AlignmentGridPicker()
    {
        InitializeComponent();
        BuildCells();
        RefreshCells();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName is nameof(SelectedAlignment)
            or nameof(AvailableAlignments)
            or nameof(IsEnabled))
        {
            RefreshCells();
        }
    }

    private void BuildCells()
    {
        _cells.Clear();

        var grid = GridHost;
        var layout = new[]
        {
            (Row: 1, Col: 1, Alignment: new Alignment(OrderAxis.Lawful, MoralAxis.Good)),
            (Row: 1, Col: 2, Alignment: new Alignment(OrderAxis.Neutral, MoralAxis.Good)),
            (Row: 1, Col: 3, Alignment: new Alignment(OrderAxis.Chaotic, MoralAxis.Good)),

            (Row: 2, Col: 1, Alignment: new Alignment(OrderAxis.Lawful, MoralAxis.Neutral)),
            (Row: 2, Col: 2, Alignment: new Alignment(OrderAxis.Neutral, MoralAxis.Neutral)),
            (Row: 2, Col: 3, Alignment: new Alignment(OrderAxis.Chaotic, MoralAxis.Neutral)),

            (Row: 3, Col: 1, Alignment: new Alignment(OrderAxis.Lawful, MoralAxis.Evil)),
            (Row: 3, Col: 2, Alignment: new Alignment(OrderAxis.Neutral, MoralAxis.Evil)),
            (Row: 3, Col: 3, Alignment: new Alignment(OrderAxis.Chaotic, MoralAxis.Evil)),
        };

        foreach (var entry in layout)
        {
            var cell = CreateCell(entry.Alignment);
            Grid.SetRow(cell, entry.Row);
            Grid.SetColumn(cell, entry.Col);
            grid.Children.Add(cell);
        }
    }

    private View CreateCell(Alignment alignment)
    {
        var title = new Label
        {
            Text = FormatAlignmentName(alignment),
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var shortLabel = new Label
        {
            Text = FormatShortLabel(alignment),
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center,
            Opacity = 0.75
        };

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(6, 8),
            Spacing = 2,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children = { title, shortLabel }
        };

        var border = new Border
        {
            StrokeThickness = 1,
            Stroke = DisabledStroke,
            BackgroundColor = BaseBackground,
            Content = content,
            Padding = new Thickness(8),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        var overlay = new Grid
        {
            BackgroundColor = Colors.White.WithAlpha(0.55f),
            IsVisible = false,
            InputTransparent = true
        };

        overlay.Children.Add(new Label
        {
            Text = "🔒",
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        });

        var wrapper = new Grid { };
        wrapper.Children.Add(border);
        wrapper.Children.Add(overlay);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OnAlignmentTapped(alignment);
        wrapper.GestureRecognizers.Add(tap);

        _cells[alignment] = new CellVisual(border, title, shortLabel, overlay);
        return wrapper;
    }

    private void OnAlignmentTapped(Alignment alignment)
    {
        if (!IsEnabled)
            return;

        var allowed = IsAlignmentAllowed(alignment);
        if (!allowed)
            return;

        SelectedAlignment = alignment;
        RefreshCells();
    }

    private void RefreshCells()
    {
        var availableSet = new HashSet<Alignment>(AvailableAlignments ?? Enumerable.Empty<Alignment>());

        foreach (var kvp in _cells)
        {
            var alignment = kvp.Key;
            var visual = kvp.Value;

            var isAvailable = availableSet.Contains(alignment);
            var isCellEnabled = IsEnabled && isAvailable;
            var isSelected = SelectedAlignment.HasValue && SelectedAlignment.Value.Equals(alignment);

            var accent = AccentFor(alignment.Moral);
            visual.Border.Stroke = isSelected ? accent : isCellEnabled ? EnabledStroke : DisabledStroke;
            visual.Border.BackgroundColor = isSelected
                ? accent.WithAlpha(0.14f)
                : isAvailable ? BaseBackground : DisabledBackground;

            visual.Border.Opacity = isCellEnabled ? 1.0 : 0.45;
            visual.Title.TextColor = isSelected ? accent : Colors.Black;
            visual.ShortLabel.TextColor = isSelected ? accent : Color.FromArgb("#4B5563");

            visual.Overlay.IsVisible = !isCellEnabled;
            visual.Border.InputTransparent = !isCellEnabled;
        }

        SelectedLabel.Text = SelectedAlignment.HasValue
            ? $"Selected: {FormatAlignmentName(SelectedAlignment.Value)}"
            : "Select an alignment";
    }

    private static string FormatAlignmentName(Alignment alignment)
    {
        return alignment switch
        {
            { Moral: MoralAxis.Neutral, Order: OrderAxis.Neutral } => "True Neutral",
            _ => $"{alignment.Order} {alignment.Moral}"
        };
    }

    private static string FormatShortLabel(Alignment alignment)
        => $"{FormatAxis(alignment.Order)} / {FormatAxis(alignment.Moral)}";

    private static string FormatAxis(Enum axis)
        => axis.ToString();

    private bool IsAlignmentAllowed(Alignment alignment)
    {
        var availableSet = new HashSet<Alignment>(AvailableAlignments ?? Enumerable.Empty<Alignment>());
        return IsEnabled && availableSet.Contains(alignment);
    }

    private static Color AccentFor(MoralAxis moral) => moral switch
    {
        MoralAxis.Good => Color.FromArgb("#15803D"),
        MoralAxis.Neutral => Color.FromArgb("#334155"),
        MoralAxis.Evil => Color.FromArgb("#B91C1C"),
        _ => Color.FromArgb("#2563EB")
    };

    private static readonly Color BaseBackground = Color.FromArgb("#F8FAFC");
    private static readonly Color DisabledBackground = Color.FromArgb("#EEF2F7");
    private static readonly Color EnabledStroke = Color.FromArgb("#CBD5E1");
    private static readonly Color DisabledStroke = Color.FromArgb("#E5E7EB");

    private sealed record CellVisual(Border Border, Label Title, Label ShortLabel, Grid Overlay);
}
