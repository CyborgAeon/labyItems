using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Microsoft.Maui.Controls;

namespace labyItems.Controls;

public class ArmourLayerControl : ContentView
{
    private readonly VerticalStackLayout _rowsHost;
    private bool _suppressUpdates;

    private record ArmourOption(int Pac, string ShortLabel);

    private static readonly ArmourOption[] Options =
    {
        new(0, string.Empty),
        new(3, "Robes/Padding"),
        new(4, "Rigid leather"),
        new(5, "Studded leather"),
        new(6, "Chainmail"),
        new(7, "Heavy Chainmail"),
        new(8, "Plate mail"),
    };

    public ArmourLayerControl()
    {
        _rowsHost = new VerticalStackLayout { Spacing = 6, Padding = new Thickness(0) };
        Content = _rowsHost;

        if (Layers == null)
            Layers = new ObservableCollection<int?> { 0 };

        HookCollection(Layers);
        RebuildRows();
        Recalculate();
    }

    #region Bindable properties

    public static readonly BindableProperty LayersProperty = BindableProperty.Create(
        nameof(Layers),
        typeof(ObservableCollection<int?>),
        typeof(ArmourLayerControl),
        defaultValue: null,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnLayersChanged
    );

    public ObservableCollection<int?> Layers
    {
        get => (ObservableCollection<int?>)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    private static void OnLayersChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (ArmourLayerControl)bindable;

        if (oldValue is ObservableCollection<int?> oldCol)
            control.UnhookCollection(oldCol);
        if (newValue is ObservableCollection<int?> newCol)
        {
            control.HookCollection(newCol);
            control.RebuildRows();
            control.Recalculate();
        }
        else
        {
            control.Layers = new ObservableCollection<int?> { 0 };
        }
    }

    public static readonly BindableProperty TotalPacProperty = BindableProperty.Create(
        nameof(TotalPac),
        typeof(int),
        typeof(ArmourLayerControl),
        defaultValue: 0,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public int TotalPac
    {
        get => (int)GetValue(TotalPacProperty);
        set => SetValue(TotalPacProperty, value);
    }

    public static readonly BindableProperty SummaryTextProperty = BindableProperty.Create(
        nameof(SummaryText),
        typeof(string),
        typeof(ArmourLayerControl),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public static readonly BindableProperty BreakdownTextProperty = BindableProperty.Create(
        nameof(BreakdownText),
        typeof(string),
        typeof(ArmourLayerControl),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.OneWayToSource
    );

    public string BreakdownText
    {
        get => (string)GetValue(BreakdownTextProperty);
        set => SetValue(BreakdownTextProperty, value);
    }

    #endregion

    #region Collection handling

    private void HookCollection(ObservableCollection<int?> collection)
    {
        collection.CollectionChanged += OnCollectionChanged;
    }

    private void UnhookCollection(ObservableCollection<int?> collection)
    {
        collection.CollectionChanged -= OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressUpdates)
            return;

        EnforceLayerLimit();
        RebuildRows();
        Recalculate();
    }

    #endregion

    #region UI building

    private void RebuildRows()
    {
        _rowsHost.Children.Clear();
        if (Layers == null || Layers.Count == 0)
            return;

        EnforceLayerLimit();

        for (int i = 0; i < Layers.Count; i++)
        {
            _rowsHost.Children.Add(CreateRow(i, Layers[i]));
        }
    }

    private View CreateRow(int index, int? currentPac)
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

        var allowedOptions = GetAllowedOptions(index).ToList();
        var labels = allowedOptions
            .Select(o =>
            {
                var label = string.IsNullOrWhiteSpace(o.ShortLabel) ? "None" : o.ShortLabel;
                return $"{o.Pac} PAC, {label}";
            })
            .ToList();

        var itemsDict = new Dictionary<string, int>();
        foreach (var opt in allowedOptions)
            itemsDict[opt.Pac.ToString()] = opt.Pac;

        var slider = new DictionarySlider
        {
            ItemsSource = itemsDict,
            Labels = labels,
            KeyFormat = "PAC {0}",
            ValueFormat = string.Empty,
            SnapToStep = true,
            StepSize = 1,
        };

        int selectedPac =
            currentPac.HasValue && allowedOptions.Any(o => o.Pac == currentPac.Value)
                ? currentPac.Value
                : allowedOptions.First().Pac;

        slider.SelectedIndex = allowedOptions.FindIndex(o => o.Pac == selectedPac);

        slider.SelectionChanged += (_, args) =>
        {
            if (_suppressUpdates || Layers == null)
                return;

            int selected = allowedOptions[
                Math.Clamp(args.NewIndex, 0, allowedOptions.Count - 1)
            ].Pac;
            Layers[index] = selected;
            Recalculate();
            RebuildRows();
        };

        var deleteButton = new Button
        {
            Text = "🗑",
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = new Thickness(0),
            BackgroundColor = (Color?)Application.Current?.Resources["Primary"] ?? Colors.Purple,
        };
        deleteButton.Clicked += (_, __) =>
        {
            if (Layers == null)
                return;

            if (Layers.Count <= 1)
            {
                Layers[0] = 0;
            }
            else
            {
                Layers.RemoveAt(index);
            }

            Recalculate();
            RebuildRows();
        };

        var addButton = new Button
        {
            Text = "+",
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = new Thickness(0),
            BackgroundColor = (Color?)Application.Current?.Resources["Primary"] ?? Colors.Purple,
            TextColor = Colors.White,
            IsEnabled = CanAddLayer(),
        };
        addButton.Clicked += (_, __) =>
        {
            if (Layers == null || !CanAddLayer())
                return;

            int insertIndex = Math.Clamp(index + 1, 0, Layers.Count);
            Layers.Insert(insertIndex, null);
            RebuildRows();
            Recalculate();
        };

        grid.Add(slider, 0, 0);
        grid.Add(deleteButton, 1, 0);
        grid.Add(addButton, 2, 0);

        return grid;
    }

    private IEnumerable<ArmourOption> GetAllowedOptions(int forIndex)
    {
        if (Layers == null || forIndex == 0)
            return Options.OrderBy(o => o.Pac);

        var selectedOthers = Layers
            .Where((v, idx) => idx != forIndex && v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        int minOther = selectedOthers.Count > 0 ? selectedOthers.Min() : int.MaxValue;
        var taken = selectedOthers.ToHashSet();

        var allowed = Options
            .Where(o => !taken.Contains(o.Pac))
            .Where(o => minOther == int.MaxValue || o.Pac < minOther)
            .OrderBy(o => o.Pac)
            .ToList();

        // If everything is filtered out, fall back to the smallest available option.
        if (allowed.Count == 0)
            allowed.Add(Options.First(o => o.Pac == 0));

        return allowed;
    }

    private void EnforceLayerLimit()
    {
        if (Layers == null || Layers.Count == 0)
            return;

        int max = AllowedLayerCount();
        if (Layers.Count <= max)
            return;

        _suppressUpdates = true;
        while (Layers.Count > max)
            Layers.RemoveAt(Layers.Count - 1);
        _suppressUpdates = false;
    }

    private int AllowedLayerCount()
    {
        if (Layers == null || Layers.Count == 0 || Layers[0] is null)
            return 1;

        int first = Layers[0] ?? 0;
        if (first < 6)
            return 1;

        int max = 2;
        if (first == 8 && Layers.Count >= 2 && Layers[1] > 5)
            max = 3;

        return max;
    }

    private bool CanAddLayer() => Layers != null && Layers.Count < AllowedLayerCount();

    #endregion

    #region Totals / summary

    private void Recalculate()
    {
        if (Layers == null || Layers.Count == 0)
        {
            TotalPac = 0;
            SummaryText = string.Empty;
            BreakdownText = string.Empty;
            return;
        }

        var selected = Layers.Where(v => v.HasValue && v.Value > 0).Select(v => v!.Value).ToList();
        if (selected.Count == 0)
        {
            TotalPac = 0;
            SummaryText = string.Empty;
            BreakdownText = string.Empty;
            return;
        }

        int basePac = selected[0];
        var contributions = new List<int> { basePac };
        int bonus = 0;

        for (int i = 1; i < selected.Count; i++)
        {
            int layer = selected[i];
            int added =
                layer > 6 ? 2
                : layer > 3 && layer < 7 ? 1
                : 0;
            bonus += added;
            contributions.Add(added);
        }

        TotalPac = basePac + bonus;

        var names = selected
            .Select(GetShortName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        if (names.Count == 0)
        {
            TotalPac = 0;
            SummaryText = string.Empty;
            BreakdownText = string.Empty;
            return;
        }
        var pacParts = new List<string> { basePac.ToString() };
        pacParts.AddRange(contributions.Skip(1).Select(c => $"+{c}"));
        string pacText = string.Join(string.Empty, pacParts);

        string layeredPhrase = names.Count switch
        {
            1 => names[0],
            2 => $"{names[0]} over {names[1]}",
            _ => $"{names[0]} over {string.Join(" with ", names.Skip(1))}",
        };

        SummaryText =
            $"{pacText} {(Layers.Count() > 1 ? $"(layered) {layeredPhrase}" : string.Empty)}.";

        var sb = new StringBuilder();
        sb.AppendLine($"AC {TotalPac} (layered)");
        for (int i = 0; i < names.Count; i++)
        {
            sb.AppendLine($"| {names[i]} ({contributions[i]})");
        }

        BreakdownText = sb.ToString().TrimEnd();
    }

    private static string GetShortName(int pac) =>
        Options.FirstOrDefault(o => o.Pac == pac)?.ShortLabel ?? $"PAC {pac}";

    #endregion
}
