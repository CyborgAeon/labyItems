using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using labyItems.Models.Characters;

namespace labyItems.Controls.Pickers;

public partial class AlignmentToggleGridPicker : ContentView
{
    public static readonly BindableProperty SelectedAlignmentsProperty =
        BindableProperty.Create(
            nameof(SelectedAlignments),
            typeof(ObservableCollection<Alignment>),
            typeof(AlignmentToggleGridPicker),
            defaultValueCreator: _ => new ObservableCollection<Alignment>(),
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnSelectedAlignmentsChanged);

    private readonly Dictionary<Alignment, CellVisual> _cells = new();

    public ObservableCollection<Alignment> SelectedAlignments
    {
        get => (ObservableCollection<Alignment>)GetValue(SelectedAlignmentsProperty);
        set => SetValue(SelectedAlignmentsProperty, value ?? new ObservableCollection<Alignment>());
    }

    public AlignmentToggleGridPicker()
    {
        InitializeComponent();
        BuildCells();
        HookCollection(SelectedAlignments);
        RefreshCells();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled))
            RefreshCells();
    }

    private static void OnSelectedAlignmentsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var picker = (AlignmentToggleGridPicker)bindable;
        picker.UnhookCollection(oldValue as ObservableCollection<Alignment>);
        picker.HookCollection(newValue as ObservableCollection<Alignment>);
        picker.RefreshCells();
    }

    private void HookCollection(ObservableCollection<Alignment>? collection)
    {
        if (collection == null)
            return;

        collection.CollectionChanged += OnCollectionChanged;
    }

    private void UnhookCollection(ObservableCollection<Alignment>? collection)
    {
        if (collection == null)
            return;

        collection.CollectionChanged -= OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshCells();

    private void BuildCells()
    {
        _cells.Clear();
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
            (Row: 3, Col: 3, Alignment: new Alignment(OrderAxis.Chaotic, MoralAxis.Evil))
        };

        foreach (var entry in layout)
        {
            var cell = CreateCell(entry.Alignment);
            Grid.SetRow(cell, entry.Row);
            Grid.SetColumn(cell, entry.Col);
            GridHost.Children.Add(cell);
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
            Stroke = EnabledStroke,
            BackgroundColor = BaseBackground,
            Content = content,
            Padding = new Thickness(8),
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        var wrapper = new Grid();
        wrapper.Children.Add(border);

        var lockWatermark = new Label
        {
            Text = "\uf023",
            FontFamily = "FASolid",
            FontSize = 22,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb("#475569"),
            Opacity = 0.22,
            InputTransparent = true
        };
        wrapper.Children.Add(lockWatermark);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OnAlignmentTapped(alignment);
        wrapper.GestureRecognizers.Add(tap);

        _cells[alignment] = new CellVisual(border, title, shortLabel, lockWatermark);
        return wrapper;
    }

    private void OnAlignmentTapped(Alignment alignment)
    {
        if (!IsEnabled)
            return;

        var selected = SelectedAlignments ?? new ObservableCollection<Alignment>();
        var contains = selected.Any(entry => entry.Equals(alignment));
        if (contains)
        {
            var existing = selected.First(entry => entry.Equals(alignment));
            selected.Remove(existing);
        }
        else
            selected.Add(alignment);

        if (selected.Count == 0)
            selected.Add(alignment);

        RefreshCells();
    }

    private void RefreshCells()
    {
        var selected = new HashSet<Alignment>(SelectedAlignments ?? Enumerable.Empty<Alignment>());

        foreach (var kvp in _cells)
        {
            var alignment = kvp.Key;
            var visual = kvp.Value;
            var isSelected = selected.Contains(alignment);
            var accent = AccentFor(alignment.Moral);

            visual.Border.Stroke = isSelected ? accent : EnabledStroke;
            visual.Border.BackgroundColor = isSelected
                ? accent.WithAlpha(0.14f)
                : BaseBackground;

            visual.Title.TextColor = isSelected ? accent : Colors.Black;
            visual.ShortLabel.TextColor = isSelected ? accent : Color.FromArgb("#4B5563");
            visual.Border.Opacity = IsEnabled ? 1.0 : 0.45;
            visual.Border.InputTransparent = !IsEnabled;
            visual.LockWatermark.IsVisible = !isSelected;
            visual.LockWatermark.Opacity = IsEnabled ? 0.22 : 0.14;
        }

        SelectedLabel.Text = selected.Count == 0
            ? "Select allowed alignments"
            : $"Selected: {selected.Count}";
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
        => $"{alignment.Order} / {alignment.Moral}";

    private static Color AccentFor(MoralAxis moral) => moral switch
    {
        MoralAxis.Good => Color.FromArgb("#15803D"),
        MoralAxis.Neutral => Color.FromArgb("#334155"),
        MoralAxis.Evil => Color.FromArgb("#B91C1C"),
        _ => Color.FromArgb("#2563EB")
    };

    private static readonly Color BaseBackground = Color.FromArgb("#F8FAFC");
    private static readonly Color EnabledStroke = Color.FromArgb("#CBD5E1");

    private sealed record CellVisual(Border Border, Label Title, Label ShortLabel, Label LockWatermark);
}
