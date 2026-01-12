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

        int selectedPac;
        if (currentPac.HasValue && allowedOptions.Any(o => o.Pac == currentPac.Value))
            selectedPac = currentPac.Value;
        else if (allowedOptions.Any(o => o.Pac == 0))
            selectedPac = 0;
        else
            selectedPac = allowedOptions.First().Pac;

        // If an existing explicit selection is no longer allowed, reset it to 'None' (0) and persist.
        if (currentPac.HasValue && currentPac.Value != selectedPac && Layers != null)
        {
            _suppressUpdates = true;
            Layers[index] = selectedPac;
            _suppressUpdates = false;
            Recalculate();
        }

        slider.SelectedIndex = allowedOptions.FindIndex(o => o.Pac == selectedPac);

        slider.SelectionChanged += (_, args) =>
        {
            if (_suppressUpdates || Layers == null)
                return;

            // Guard against the row being removed/reduced concurrently.
            if (index < 0 || index >= Layers.Count)
                return;

            // Recompute allowed options for current state to avoid stale lists.
            var currentAllowed = GetAllowedOptions(index).ToList();
            if (currentAllowed.Count == 0)
                return;

            int newIdx = Math.Clamp(args.NewIndex, 0, currentAllowed.Count - 1);
            int selected = currentAllowed[newIdx].Pac;

            // Persist change without triggering nested OnCollectionChanged handling.
            _suppressUpdates = true;
            Layers[index] = selected;
            _suppressUpdates = false;

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

        // Per-row add-button rules:
        // - only enabled when overall layer count allows adding
        // - only enabled on the last visible row (index == Layers.Count - 1)
        // - the current row's category must be Medium (2) or Heavy (3)
        // - third row (index >= 2) cannot add
        var overallCanAdd = CanAddLayer();
        bool isLastRow = Layers != null && index == Layers.Count - 1;
        int thisPac = Layers != null && Layers.Count > index && Layers[index].HasValue ? Layers[index]!.Value : 0;
        bool thisHasAddCategory = CategoryForPac(thisPac) >= 2; // medium or heavy
        bool rowCanAdd = overallCanAdd && isLastRow && thisHasAddCategory && index < 2;

        var addButton = new Button
        {
            Text = "+",
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = new Thickness(0),
            IsEnabled = rowCanAdd,
            BackgroundColor = rowCanAdd ? (Color?)Application.Current?.Resources["Primary"] ?? Colors.Purple : Colors.Gray,
            TextColor = Colors.White,
            Opacity = rowCanAdd ? 1.0 : 0.5,
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
        {
            // Base layer: allow any option except values already taken by other rows
            var takenBase = Layers?.Where((v, idx) => idx != forIndex && v.HasValue).Select(v => v!.Value).ToHashSet() ?? new HashSet<int>();
            var baseAllowed = Options.Where(o => !takenBase.Contains(o.Pac)).OrderBy(o => o.Pac).ToList();
            if (!baseAllowed.Any(o => o.Pac == 0))
                baseAllowed.Insert(0, Options.First(o => o.Pac == 0));
            return baseAllowed;
        }

        // Taken values in other rows (exclude this row)
        var taken = Layers.Where((v, idx) => idx != forIndex && v.HasValue).Select(v => v!.Value).ToHashSet();

        // The previous layer (the row just below this one) determines the category ceiling.
        int prevPac = (forIndex - 1) >= 0 && Layers.Count > (forIndex - 1) && Layers[forIndex - 1].HasValue ? Layers[forIndex - 1]!.Value : 0;
        int prevCategory = CategoryForPac(prevPac);

        // If previous is None (0) or undefined, only allow None to avoid nonsensical stacking.
        if (prevCategory == 0)
        {
            return new List<ArmourOption> { Options.First(o => o.Pac == 0) };
        }

        // Allow only options that are strictly lighter category than the previous layer, and not already taken.
        var allowed = Options
            .Where(o => !taken.Contains(o.Pac))
            .Where(o => CategoryForPac(o.Pac) < prevCategory)
            .OrderBy(o => o.Pac)
            .ToList();

        // Ensure None (0) is always available as a choice
        if (!allowed.Any(o => o.Pac == 0))
            allowed.Insert(0, Options.First(o => o.Pac == 0));

        // If nothing else, at least offer None
        if (!allowed.Any())
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
            int added = (layer == 5 || layer == 6) ? 2
                      : (layer == 3 || layer == 4) ? 1
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
            $"{pacText} {(Layers.Count() > 1 ? $" (layered) {layeredPhrase}" : string.Empty)}";

        var sb = new StringBuilder();
        sb.AppendLine($" AC {SummaryText}");
        for (int i = 0; i < names.Count; i++)
        {
            sb.AppendLine($"| {names[i]} ({contributions[i]})");
        }

        BreakdownText = sb.ToString().TrimEnd();
    }

    private static int CategoryForPac(int pac) =>
        pac == 0 ? 0
        : (pac >= 3 && pac <= 4) ? 1
        : (pac >= 5 && pac <= 6) ? 2
        : (pac >= 7) ? 3
        : 0;

    private static string GetShortName(int pac) =>
        Options.FirstOrDefault(o => o.Pac == pac)?.ShortLabel ?? $"PAC {pac}";

    #endregion
}
