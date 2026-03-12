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

public sealed class EvocationSelectionEntry : INotifyPropertyChanged
{
    private int _basicPerDay;
    private int _advancedPerDay;
    private bool _addBasicToBaseList;
    private bool _addAdvancedToBaseList;
    private bool _addWithPrep30;
    private Color _rowBackgroundColor = Colors.White;

    public EvocationSelectionEntry(DruidEvocationService.EvocRaw evocation)
    {
        Evocation = evocation ?? new DruidEvocationService.EvocRaw();
        EvocationName = Evocation.name ?? string.Empty;
        Power = Math.Max(1, Evocation.power);
        IsAdvanced = Evocation.isAdvanced;
        Fields = (Evocation.fields ?? new List<string>())
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .ToList();

        if (IsAdvanced)
            _advancedPerDay = 1;
        else
            _basicPerDay = 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public DruidEvocationService.EvocRaw Evocation { get; }
    public string EvocationName { get; }
    public int Power { get; }
    public bool IsAdvanced { get; }
    public IReadOnlyList<string> Fields { get; }

    public string DisplayName =>
        IsAdvanced
            ? $"{EvocationName} (P{Power}, advanced)"
            : $"{EvocationName} (P{Power})";

    public string FieldsSummary =>
        Fields.Count == 0
            ? string.Empty
            : $"Fields: {string.Join(", ", Fields)}";

    public bool HasFieldsSummary => !string.IsNullOrWhiteSpace(FieldsSummary);

    public bool ShowBasicConfig => !IsAdvanced;
    public bool ShowAdvancedConfig => IsAdvanced;
    public bool ShowAddBasic => !IsAdvanced;
    public bool ShowAddAdvanced => IsAdvanced;

    public int BasicPerDay
    {
        get => _basicPerDay;
        set
        {
            var next = Math.Max(0, value);
            if (_basicPerDay == next)
                return;

            _basicPerDay = next;
            Raise(nameof(BasicPerDay));
            Raise(nameof(InlineSummary));
        }
    }

    public int AdvancedPerDay
    {
        get => _advancedPerDay;
        set
        {
            var next = Math.Max(0, value);
            if (_advancedPerDay == next)
                return;

            _advancedPerDay = next;
            Raise(nameof(AdvancedPerDay));
            Raise(nameof(InlineSummary));
        }
    }

    public bool AddBasicToBaseList
    {
        get => _addBasicToBaseList;
        set
        {
            if (_addBasicToBaseList == value)
                return;

            _addBasicToBaseList = value;
            if (value)
            {
                _addAdvancedToBaseList = false;
                _addWithPrep30 = false;
            }

            Raise(nameof(AddBasicToBaseList));
            Raise(nameof(AddAdvancedToBaseList));
            Raise(nameof(AddWithPrep30));
            Raise(nameof(InlineSummary));
        }
    }

    public bool AddAdvancedToBaseList
    {
        get => _addAdvancedToBaseList;
        set
        {
            if (_addAdvancedToBaseList == value)
                return;

            _addAdvancedToBaseList = value;
            if (value)
            {
                _addBasicToBaseList = false;
                _addWithPrep30 = false;
            }

            Raise(nameof(AddAdvancedToBaseList));
            Raise(nameof(AddBasicToBaseList));
            Raise(nameof(AddWithPrep30));
            Raise(nameof(InlineSummary));
        }
    }

    public bool AddWithPrep30
    {
        get => _addWithPrep30;
        set
        {
            if (_addWithPrep30 == value)
                return;

            _addWithPrep30 = value;
            if (value)
            {
                _addBasicToBaseList = false;
                _addAdvancedToBaseList = false;
            }

            Raise(nameof(AddWithPrep30));
            Raise(nameof(AddBasicToBaseList));
            Raise(nameof(AddAdvancedToBaseList));
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
            if (ShowBasicConfig && BasicPerDay > 0)
                parts.Add($"basic x{BasicPerDay}/day");
            if (ShowAdvancedConfig && AdvancedPerDay > 0)
                parts.Add($"advanced x{AdvancedPerDay}/day");
            if (AddBasicToBaseList)
                parts.Add("+basic list");
            if (AddAdvancedToBaseList)
                parts.Add("+advanced list");
            if (AddWithPrep30)
                parts.Add("+prep 30s");

            if (parts.Count == 0)
                return "No per-evocation modifiers selected.";

            return string.Join(" • ", parts);
        }
    }

    public EvocationSelectionEntry Clone()
    {
        return new EvocationSelectionEntry(Evocation)
        {
            BasicPerDay = BasicPerDay,
            AdvancedPerDay = AdvancedPerDay,
            AddBasicToBaseList = AddBasicToBaseList,
            AddAdvancedToBaseList = AddAdvancedToBaseList,
            AddWithPrep30 = AddWithPrep30
        };
    }

    public void ApplyFrom(EvocationSelectionEntry source)
    {
        if (source == null)
            return;

        BasicPerDay = source.BasicPerDay;
        AdvancedPerDay = source.AdvancedPerDay;
        AddBasicToBaseList = source.AddBasicToBaseList;
        AddAdvancedToBaseList = source.AddAdvancedToBaseList;
        AddWithPrep30 = source.AddWithPrep30;
        NormalizeForEvocationType();
    }

    public void NormalizeForEvocationType()
    {
        if (IsAdvanced)
        {
            if (BasicPerDay != 0)
                BasicPerDay = 0;
            if (AddBasicToBaseList)
                AddBasicToBaseList = false;
        }
        else
        {
            if (AdvancedPerDay != 0)
                AdvancedPerDay = 0;
            if (AddAdvancedToBaseList)
                AddAdvancedToBaseList = false;
        }
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class EvocationConfig : ConfigBase
{
    private static readonly Color RowEvenColor = Colors.White;
    private static readonly Color RowOddColor = Color.FromArgb("#F6F6F6");

    public EvocationConfig()
    {
        Name = "Evocations";
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Breakdown) || e.PropertyName == nameof(BreakdownItems))
                return;
            RecalculateBreakdown();
        };

        SelectedEvocations.CollectionChanged += OnSelectedEvocationsChanged;
        RecalculateBreakdown();
    }

    protected override string NoneSelectedText => "Evocations (none selected)";

    public ObservableCollection<EvocationSelectionEntry> SelectedEvocations { get; } = new();
    public bool HasSelectedEvocations => SelectedEvocations.Count > 0;

    private int _drawOnEpPerDay;
    public int DrawOnEpPerDay
    {
        get => _drawOnEpPerDay;
        set => SetProperty(ref _drawOnEpPerDay, Math.Max(0, value), affectsTotal: true);
    }

    private int _generalEpStore;
    public int GeneralEpStore
    {
        get => _generalEpStore;
        set =>
            SetProperty(
                ref _generalEpStore,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(AnyEpStore));
    }

    private int _fieldEpStore;
    public int FieldEpStore
    {
        get => _fieldEpStore;
        set =>
            SetProperty(
                ref _fieldEpStore,
                Math.Clamp(value, 0, 12),
                affectsTotal: SelectedField.HasValue,
                nameof(AnyEpStore));
    }

    private EvocationFields? _selectedField;
    public EvocationFields? SelectedField
    {
        get => _selectedField;
        set => SetProperty(ref _selectedField, value, affectsTotal: FieldEpStore > 0);
    }

    public bool AnyEpStore => GeneralEpStore > 0 || (FieldEpStore > 0 && SelectedField.HasValue);

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

    public bool TryAddEvocation(DruidEvocationService.EvocRaw evocation)
    {
        if (evocation == null || string.IsNullOrWhiteSpace(evocation.name))
            return false;

        var existing = SelectedEvocations.FirstOrDefault(x =>
            string.Equals(x.EvocationName, evocation.name, StringComparison.OrdinalIgnoreCase)
            && x.Power == Math.Max(1, evocation.power)
            && x.IsAdvanced == evocation.isAdvanced);

        if (existing != null)
            return false;

        var entry = new EvocationSelectionEntry(evocation);
        entry.NormalizeForEvocationType();
        SelectedEvocations.Add(entry);
        return true;
    }

    public void RemoveEvocation(EvocationSelectionEntry entry)
    {
        if (entry == null)
            return;

        SelectedEvocations.Remove(entry);
    }

    protected override int BaseTotal()
    {
        var total = 0;
        foreach (var entry in SelectedEvocations)
            total += CalculateEvocationEntryTotal(entry);

        return total;
    }

    protected override int ExtraTotal()
    {
        var total = 0;
        total += DrawOnEpPerDay * 16;
        total += 4 * GeneralEpStore;
        if (FieldEpStore > 0 && SelectedField.HasValue)
            total += 3 * FieldEpStore;
        return total;
    }

    private void OnSelectedEvocationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<EvocationSelectionEntry>())
                item.PropertyChanged -= OnEvocationEntryPropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<EvocationSelectionEntry>())
                item.PropertyChanged += OnEvocationEntryPropertyChanged;
        }

        RefreshEvocationRowStyles();
        OnPropertyChanged(nameof(HasSelectedEvocations));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void OnEvocationEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is EvocationSelectionEntry entry)
            entry.NormalizeForEvocationType();

        if (e.PropertyName == nameof(EvocationSelectionEntry.RowBackgroundColor))
            return;

        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalWithBase));
        RecalculateBreakdown();
    }

    private void RefreshEvocationRowStyles()
    {
        for (int i = 0; i < SelectedEvocations.Count; i++)
            SelectedEvocations[i].RowBackgroundColor = i % 2 == 0 ? RowEvenColor : RowOddColor;
    }

    private static int CalculateEvocationEntryTotal(EvocationSelectionEntry entry)
    {
        var basicCost = 2 * entry.Power * Math.Max(0, entry.BasicPerDay);
        var advancedCost = 3 * entry.Power * Math.Max(0, entry.AdvancedPerDay);
        var total = basicCost + advancedCost;

        if (entry.AddBasicToBaseList)
            return total + 15;

        if (entry.AddAdvancedToBaseList)
            return total + 18;

        if (entry.AddWithPrep30)
            return total + (total / 2);

        return total;
    }

    private void RecalculateBreakdown()
    {
        var builder = new BreakdownBuilder();
        BreakdownItems.Clear();

        foreach (var entry in SelectedEvocations)
        {
            var prefix = entry.DisplayName;

            var basicCost = 2 * entry.Power * Math.Max(0, entry.BasicPerDay);
            if (basicCost > 0)
                builder.Add($"{prefix} basic casts x{entry.BasicPerDay} @2×Power {entry.Power} = {basicCost}", basicCost);

            var advancedCost = 3 * entry.Power * Math.Max(0, entry.AdvancedPerDay);
            if (advancedCost > 0)
                builder.Add($"{prefix} advanced casts x{entry.AdvancedPerDay} @3×Power {entry.Power} = {advancedCost}", advancedCost);

            var castTotal = basicCost + advancedCost;

            if (entry.AddBasicToBaseList)
            {
                builder.Add($"{prefix} add basic evocation to base list = 15", 15, includeWhenZero: true);
            }
            else if (entry.AddAdvancedToBaseList)
            {
                builder.Add($"{prefix} add advanced evocation to base list = 18", 18, includeWhenZero: true);
            }
            else if (entry.AddWithPrep30)
            {
                var prepCost = castTotal / 2;
                builder.Add($"{prefix} add to base list with 30s prep (50%) = {prepCost}", prepCost, includeWhenZero: castTotal > 0);
            }
        }

        if (GeneralEpStore > 0)
        {
            var storeCost = 4 * GeneralEpStore;
            builder.Add($"General EP store +{GeneralEpStore} @4 each = {storeCost}", storeCost);
        }

        if (FieldEpStore > 0 && SelectedField.HasValue)
        {
            var fieldCost = 3 * FieldEpStore;
            builder.Add($"Field EP store {SelectedField.Value} +{FieldEpStore} @3 each = {fieldCost}", fieldCost);
        }

        if (DrawOnEpPerDay > 0)
        {
            var drawCost = DrawOnEpPerDay * 16;
            builder.Add($"Draw on EP x{DrawOnEpPerDay} @16 = {drawCost}", drawCost);
        }

        foreach (var row in builder.Rows)
            BreakdownItems.Add(row);

        var header = SelectedEvocations.Count switch
        {
            0 => "Evocations",
            1 => SelectedEvocations[0].DisplayName,
            _ => $"{SelectedEvocations.Count} evocations"
        };

        Breakdown = builder.BuildSummary(header, Total);
    }
}
