using System.Collections.ObjectModel;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Pages.Calculator;
using labyItems.Services;

namespace labyItems.Pages.Configs;

public class EvocationConfig : ConfigBase
{
    public ObservableCollection<ContributionRow> BreakdownItems { get; } = new();

    private string _breakdown = string.Empty;
    public string Breakdown
    {
        get => _breakdown;
        private set
        {
            if (_breakdown != value)
            {
                _breakdown = value;
                OnPropertyChanged();
            }
        }
    }

    public EvocationConfig()
    {
        PropertyChanged += (_, e) =>
        {
            if (
                e.PropertyName == nameof(IsAdvanced)
                || e.PropertyName == nameof(EvocationName)
                || e.PropertyName == nameof(Name)
            )
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(ShowBasicPerDay));
                OnPropertyChanged(nameof(ShowAdvancedPerDay));
            }

            if (e.PropertyName != nameof(Breakdown) && e.PropertyName != nameof(BreakdownItems))
                RecalculateBreakdown();
        };

        RecalculateBreakdown();
    }

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
                nameof(AnyEpStore)
            );
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
                nameof(AnyEpStore)
            );
    }

    private EvocationFields? _selectedField;
    public EvocationFields? SelectedField
    {
        get => _selectedField;
        set => SetProperty(ref _selectedField, value, affectsTotal: FieldEpStore > 0);
    }

    public string EvocationName { get; set; } = "";
    protected override string NoneSelectedText => "Evocation (none selected)";
    public bool HasSelection => !string.IsNullOrWhiteSpace(EvocationName);
    public bool ShowBasicPerDay => HasSelection && IsAdvanced == false;
    public bool ShowAdvancedPerDay => HasSelection && IsAdvanced == true;

    public bool AnyEpStore => (GeneralEpStore > 0) || (FieldEpStore > 0);

    protected override int ExtraTotal()
    {
        int total = 0;
        total += DrawOnEpPerDay * 16;
        total += 4 * GeneralEpStore;
        total += 3 * FieldEpStore;
        return total;
    }

    public void ApplyEvocation(Evocation.Result picked)
    {
        Name = $"{picked.Name} ({picked.Power} EP)";
        EvocationName = picked.Name;
        Power = picked.Power;
        IsAdvanced = picked.IsAdvanced;
    }

    private void RecalculateBreakdown()
    {
        var builder = new BreakdownBuilder();
        BreakdownItems.Clear();

        int basicCost = 2 * Power * Math.Max(0, BasicPerDay);
        if (basicCost > 0)
            builder.Add($"{BasicPerDay}/Basic casts @2 × {Power} = {basicCost}", basicCost);

        int advancedCost = 3 * Power * Math.Max(0, AdvancedPerDay);
        if (advancedCost > 0)
            builder.Add($"{AdvancedPerDay}/Advanced casts @3 × {Power} = {advancedCost}", advancedCost);

        int baseTotal = basicCost + advancedCost;

        if (AddBasic)
        {
            builder.Add("Add basic evocation to base list = 15", 15, includeWhenZero: true);
            baseTotal += 15;
        }
        else if (AddAdvanced)
        {
            builder.Add("Add advanced evocation to base list = 18", 18, includeWhenZero: true);
            baseTotal += 18;
        }
        else if (AddPrep)
        {
            int prep = baseTotal / 2;
            builder.Add($"Add to base list with 30s prep (50%) = {prep}", prep, includeWhenZero: baseTotal > 0);
            baseTotal += prep;
        }

        if (GeneralEpStore > 0)
        {
            int storeCost = 4 * GeneralEpStore;
            builder.Add($"General EP store +{GeneralEpStore} @4 each = {storeCost}", storeCost);
        }

        if (FieldEpStore > 0)
        {
            int fieldCost = 3 * FieldEpStore;
            builder.Add($"Field EP store {(SelectedField?.ToString() ?? "Field")} +{FieldEpStore} @3 each = {fieldCost}", fieldCost);
        }

        if (DrawOnEpPerDay > 0)
        {
            int drawCost = DrawOnEpPerDay * 16;
            builder.Add($"{DrawOnEpPerDay}/Draw on EP @16 = {drawCost}", drawCost);
        }

        foreach (var row in builder.Rows)
            BreakdownItems.Add(row);

        Breakdown = builder.BuildSummary(Title, Total);
    }
}
