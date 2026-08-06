using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Configs;

public sealed class NeuronicSelectionEntry : INotifyPropertyChanged, IConfigSelectionListItem
{
    private int _usesPerDay = 1;
    private bool _addToBaseList;
    private Color _rowBackgroundColor = Colors.White;

    public NeuronicSelectionEntry(NeuronicService.NeuronicRaw neuronic)
    {
        Neuronic = neuronic ?? new NeuronicService.NeuronicRaw();
        NeuronicName = Neuronic.name ?? string.Empty;
        Power = Math.Max(1, Neuronic.power);
        Type = Neuronic.Type;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public NeuronicService.NeuronicRaw Neuronic { get; }
    public string NeuronicName { get; }
    public int Power { get; }
    public NeuroOptionType Type { get; }

    public string DisplayName => Type switch
    {
        NeuroOptionType.Active => $"{NeuronicName} (active, {Power} TBLP)",
        NeuroOptionType.Passive => $"{NeuronicName} (passive, {Power} TBLP)",
        _ => $"{NeuronicName} ({Power} TBLP)"
    };

    public string TypeSummary => Type == NeuroOptionType.None ? "Unclassified neuronic" : $"{Type} neuronic";
    public bool HasTypeSummary => true;

    public int UsesPerDay
    {
        get => _usesPerDay;
        set
        {
            var next = Math.Max(0, value);
            if (_usesPerDay == next)
                return;

            _usesPerDay = next;
            Raise(nameof(UsesPerDay));
            Raise(nameof(InlineSummary));
        }
    }

    public bool AddToBaseList
    {
        get => _addToBaseList;
        set
        {
            if (_addToBaseList == value)
                return;

            _addToBaseList = value;
            Raise(nameof(AddToBaseList));
            Raise(nameof(InlineSummary));
        }
    }

    public Color RowBackgroundColor
    {
        get => _rowBackgroundColor;
        set
        {
            if (_rowBackgroundColor == value)
                return;

            _rowBackgroundColor = value;
            Raise(nameof(RowBackgroundColor));
        }
    }

    public string InlineSummary
    {
        get
        {
            var parts = new List<string>();
            if (UsesPerDay > 0)
                parts.Add($"uses x{UsesPerDay}/day");
            if (AddToBaseList)
                parts.Add("+base list");

            return parts.Count == 0
                ? "No per-neuronic modifiers selected."
                : string.Join(" - ", parts);
        }
    }

    public NeuronicSelectionEntry Clone()
    {
        return new NeuronicSelectionEntry(Neuronic)
        {
            UsesPerDay = UsesPerDay,
            AddToBaseList = AddToBaseList
        };
    }

    public void ApplyFrom(NeuronicSelectionEntry source)
    {
        if (source == null)
            return;

        UsesPerDay = source.UsesPerDay;
        AddToBaseList = source.AddToBaseList;
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class NeuronicConfig : ConfigBase
{
    private static readonly Color RowEvenColor = Colors.White;
    private static readonly Color RowOddColor = Color.FromArgb("#F6F6F6");

    public NeuronicConfig()
    {
        Name = "Neuronics";
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Breakdown) || e.PropertyName == nameof(BreakdownItems))
                return;
            RecalculateBreakdown();
        };

        SelectedNeuronics.CollectionChanged += OnSelectedNeuronicsChanged;
        RecalculateBreakdown();
    }

    protected override string NoneSelectedText => "Neuronics (none selected)";

    public ObservableCollection<NeuronicSelectionEntry> SelectedNeuronics { get; } = new();
    public bool HasSelectedNeuronics => SelectedNeuronics.Count > 0;

    public ObservableCollection<ContributionRow> BreakdownItems { get; } = new();

    private string _breakdown = string.Empty;
    public string Breakdown
    {
        get => _breakdown;
        private set
        {
            if (_breakdown == value)
                return;

            _breakdown = value;
            OnPropertyChanged();
        }
    }

    public bool TryAddNeuronic(NeuronicService.NeuronicRaw neuronic)
    {
        if (neuronic == null || string.IsNullOrWhiteSpace(neuronic.name))
            return false;

        var existing = SelectedNeuronics.FirstOrDefault(x =>
            string.Equals(x.NeuronicName, neuronic.name, StringComparison.OrdinalIgnoreCase)
            && x.Power == Math.Max(1, neuronic.power)
            && x.Type == neuronic.Type);

        if (existing != null)
            return false;

        SelectedNeuronics.Add(new NeuronicSelectionEntry(neuronic));
        return true;
    }

    public void RemoveNeuronic(NeuronicSelectionEntry entry)
    {
        if (entry == null)
            return;

        SelectedNeuronics.Remove(entry);
    }

    protected override int BaseTotal()
    {
        var total = 0;
        foreach (var entry in SelectedNeuronics)
            total += CalculateNeuronicEntryTotal(entry);

        return total;
    }

    protected override int ExtraTotal() => 0;

    private void OnSelectedNeuronicsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<NeuronicSelectionEntry>())
                item.PropertyChanged -= OnNeuronicEntryPropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<NeuronicSelectionEntry>())
                item.PropertyChanged += OnNeuronicEntryPropertyChanged;
        }

        RefreshNeuronicRowStyles();
        OnPropertyChanged(nameof(HasSelectedNeuronics));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void OnNeuronicEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NeuronicSelectionEntry.RowBackgroundColor))
            return;

        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void RefreshNeuronicRowStyles()
    {
        for (var i = 0; i < SelectedNeuronics.Count; i++)
            SelectedNeuronics[i].RowBackgroundColor = i % 2 == 0 ? RowEvenColor : RowOddColor;
    }

    private static int CalculateNeuronicEntryTotal(NeuronicSelectionEntry entry)
    {
        var power = Math.Max(0, entry.Power);
        var uses = Math.Max(0, entry.UsesPerDay);
        var total = 2 * power * uses;

        if (entry.AddToBaseList)
            total += 15;

        return total;
    }

    private void RecalculateBreakdown()
    {
        var builder = new BreakdownBuilder();
        BreakdownItems.Clear();

        foreach (var entry in SelectedNeuronics)
        {
            var prefix = entry.DisplayName;
            var useCost = 2 * Math.Max(0, entry.Power) * Math.Max(0, entry.UsesPerDay);
            if (useCost > 0)
                builder.Add($"{prefix} uses x{entry.UsesPerDay} @2xPower {entry.Power} = {useCost}", useCost);

            if (entry.AddToBaseList)
                builder.Add($"{prefix} add neuronic to base list = 15", 15, includeWhenZero: true);
        }

        foreach (var row in builder.Rows)
            BreakdownItems.Add(row);

        var header = SelectedNeuronics.Count switch
        {
            0 => "Neuronics",
            1 => SelectedNeuronics[0].DisplayName,
            _ => $"{SelectedNeuronics.Count} neuronics"
        };

        Breakdown = builder.BuildSummary(header, Total);
    }
}
