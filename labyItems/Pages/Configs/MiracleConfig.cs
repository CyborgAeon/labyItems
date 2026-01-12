using System;
using System.Collections.ObjectModel;
using labyItems.Models.DTOs;
using labyItems.Models.Enums;
using labyItems.Controls;
using labyItems.Helpers;

namespace labyItems.Pages.Configs;

public class MiracleConfig : ConfigBase
{
    public MiracleConfig()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsAdvanced) || e.PropertyName == nameof(Name))
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

    private SpiritualSpheres? _additionalSphereSelected = null;
    public SpiritualSpheres? AdditionalSphereSelected
    {
        get => _additionalSphereSelected;
        set =>
            SetProperty(
                ref _additionalSphereSelected,
                value,
                affectsTotal: false,
                nameof(IsPowerStore)
            );
    }
    public bool IsPowerStore =>
        (GeneralSpiritStore > 0 || (SphereSpiritStore > 0 && AdditionalSphereSelected.HasValue));
    public bool HasSelection => !string.IsNullOrWhiteSpace(Name);
    public bool ShowBasicPerDay => HasSelection && IsAdvanced == false;
    public bool ShowAdvancedPerDay => HasSelection && IsAdvanced == true;
    private string _alignment = "";
    public string Alignment
    {
        get => _alignment;
        set => SetProperty(ref _alignment, value, affectsTotal: false);
    }

    public bool HasInnates => (BasicPerDay > 0) || (AdvancedPerDay > 0);

    private bool _innateIsMantic;
    public bool InnateIsMantic
    {
        get => _innateIsMantic;
        set => SetProperty(ref _innateIsMantic, value, affectsTotal: true);
    }

    private int _generalSpiritStore; // 0..12
    public int GeneralSpiritStore
    {
        get => _generalSpiritStore;
        set =>
            SetProperty(
                ref _generalSpiritStore,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(AnySpiritStore)
            );
    }

    private int _sphereSpiritStore; // 0..12
    public int SphereSpiritStore
    {
        get => _sphereSpiritStore;
        set =>
            SetProperty(
                ref _sphereSpiritStore,
                Math.Clamp(value, 0, 12),
                affectsTotal: true,
                nameof(AnySpiritStore)
            );
    }

    public bool AnySpiritStore => (GeneralSpiritStore > 0) || (SphereSpiritStore > 0);

    private bool _spiritStoreRegenerates;
    public bool SpiritStoreRegenerates
    {
        get => _spiritStoreRegenerates;
        set => SetProperty(ref _spiritStoreRegenerates, value, affectsTotal: true);
    }

    private bool _addBasicToList;
    public bool AddBasicToList
    {
        get => _addBasicToList;
        set => SetProperty(ref _addBasicToList, value, affectsTotal: true);
    }

    private bool _addAdvancedToList;
    public bool AddAdvancedToList
    {
        get => _addAdvancedToList;
        set => SetProperty(ref _addAdvancedToList, value, affectsTotal: true);
    }

    private bool _addWithPrep30;
    public bool AddWithPrep30
    {
        get => _addWithPrep30;
        set => SetProperty(ref _addWithPrep30, value, affectsTotal: true);
    }

    private int _turnBasicUpTo5thMantic;
    public int TurnBasicUpTo5thMantic
    {
        get => _turnBasicUpTo5thMantic;
        set => SetProperty(ref _turnBasicUpTo5thMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnBasicMantic;
    public int TurnBasicMantic
    {
        get => _turnBasicMantic;
        set => SetProperty(ref _turnBasicMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnAdvancedUpTo6thMantic;
    public int TurnAdvancedUpTo6thMantic
    {
        get => _turnAdvancedUpTo6thMantic;
        set => SetProperty(ref _turnAdvancedUpTo6thMantic, Math.Max(0, value), affectsTotal: true);
    }

    private int _turnAdvancedAbove6thMantic;
    public int TurnAdvancedAbove6thMantic
    {
        get => _turnAdvancedAbove6thMantic;
        set => SetProperty(ref _turnAdvancedAbove6thMantic, Math.Max(0, value), affectsTotal: true);
    }

    private bool _isTeachingScroll;
    public bool IsTeachingScroll
    {
        get => _isTeachingScroll;
        set => SetProperty(ref _isTeachingScroll, value, affectsTotal: true);
    }

    private int _trueBeliever;
    public int TrueBeliever
    {
        get => _trueBeliever;
        set => SetProperty(ref _trueBeliever, Math.Max(0, value), affectsTotal: true);
    }

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

    protected override int ExtraTotal()
    {
        int extra = 0;

        int innates =
            (2 * Power * Math.Max(0, BasicPerDay)) + (3 * Power * Math.Max(0, AdvancedPerDay));

        if (InnateIsMantic && innates > 0)
            extra += 3 * innates;

        if (GeneralSpiritStore > 0)
            extra += 4 * GeneralSpiritStore;
        if (SphereSpiritStore > 0)
            extra += 3 * SphereSpiritStore;
        if (AnySpiritStore && SpiritStoreRegenerates)
            extra += 25;

        bool isAdv = IsAdvanced == true;
        bool isBasic = IsAdvanced == false;

        if (isBasic && AddBasicToList)
            extra += 15;
        if (isAdv && AddAdvancedToList)
            extra += 18;

        if (AddWithPrep30)
            extra += (int)Math.Round(Power / 2.0, MidpointRounding.AwayFromZero);

        if (TurnBasicUpTo5thMantic > 0)
            extra += 40 * TurnBasicUpTo5thMantic;
        if (TurnBasicMantic > 0)
            extra += 50 * TurnBasicMantic;
        if (TurnAdvancedUpTo6thMantic > 0)
            extra += 60 * TurnAdvancedUpTo6thMantic;
        if (TurnAdvancedAbove6thMantic > 0)
            extra += 80 * TurnAdvancedAbove6thMantic;

        if (IsTeachingScroll)
            extra += 3 * Power;
        if (TrueBeliever > 0)
            extra += 16 * TrueBeliever;

        return extra;
    }

    public void ApplyMiracle(Miracle.Result picked)
    {
        Power = picked.Power;
        Name = string.IsNullOrWhiteSpace(picked.Name) ? "Miracle" : picked.Name;
        IsAdvanced = picked.IsAdvanced;
        Alignment = picked.Alignment;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowBasicPerDay));
        OnPropertyChanged(nameof(ShowAdvancedPerDay));
    }

    private void RecalculateBreakdown()
    {
        var builder = new BreakdownBuilder();
        BreakdownItems.Clear();

        int basicCost = 2 * Power * Math.Max(0, BasicPerDay);
        if (basicCost > 0)
            builder.Add($"Basic miracles x{BasicPerDay} @2×Power {Power} = {basicCost}", basicCost);

        int advancedCost = 3 * Power * Math.Max(0, AdvancedPerDay);
        if (advancedCost > 0)
            builder.Add($"Advanced miracles x{AdvancedPerDay} @3×Power {Power} = {advancedCost}", advancedCost);

        int innateCost = basicCost + advancedCost;

        if (AddBasicToList)
        {
            builder.Add("Add basic miracle to base list = 15", 15, includeWhenZero: true);
            // BaseTotal accounts for this once; extra handles conditional add later.
        }
        else if (AddAdvancedToList)
        {
            builder.Add("Add advanced miracle to base list = 18", 18, includeWhenZero: true);
        }
        else if (AddWithPrep30)
        {
            int prepBase = innateCost / 2;
            builder.Add($"Add miracle with 30s prep (50%) = {prepBase}", prepBase, includeWhenZero: innateCost > 0);
        }

        if (InnateIsMantic && innateCost > 0)
            builder.Add($"Innates are mantic ×4 = {innateCost * 3}", innateCost * 3);

        if (GeneralSpiritStore > 0)
        {
            int generalCost = 4 * GeneralSpiritStore;
            builder.Add($"General spirit store +{GeneralSpiritStore} @4 each = {generalCost}", generalCost);
        }

        if (SphereSpiritStore > 0)
        {
            var sphereLabel = AdditionalSphereSelected?.ToString() ?? "Sphere";
            int sphereCost = 3 * SphereSpiritStore;
            builder.Add($"{sphereLabel} spirit store +{SphereSpiritStore} @3 each = {sphereCost}", sphereCost);
        }

        if (AnySpiritStore && SpiritStoreRegenerates)
            builder.Add("Spirit store regenerates = 25", 25);

        bool isAdv = IsAdvanced == true;
        bool isBasic = IsAdvanced == false;

        if (isBasic && AddBasicToList)
            builder.Add("Add basic miracle to list (basic) = 15", 15, includeWhenZero: true);

        if (isAdv && AddAdvancedToList)
            builder.Add("Add advanced miracle to list (advanced) = 18", 18, includeWhenZero: true);

        if (AddWithPrep30)
        {
            int prepCost = (int)Math.Round(Power / 2.0, MidpointRounding.AwayFromZero);
            builder.Add($"Add to list with 30s prep (Power/2) = {prepCost}", prepCost, includeWhenZero: Power > 0);
        }

        if (TurnBasicUpTo5thMantic > 0)
        {
            int cost = 40 * TurnBasicUpTo5thMantic;
            builder.Add($"Turn basic to 5th mantic x{TurnBasicUpTo5thMantic} = {cost}", cost);
        }

        if (TurnBasicMantic > 0)
        {
            int cost = 50 * TurnBasicMantic;
            builder.Add($"Turn basic miracle mantic x{TurnBasicMantic} = {cost}", cost);
        }

        if (TurnAdvancedUpTo6thMantic > 0)
        {
            int cost = 60 * TurnAdvancedUpTo6thMantic;
            builder.Add($"Turn advanced to 6th mantic x{TurnAdvancedUpTo6thMantic} = {cost}", cost);
        }

        if (TurnAdvancedAbove6thMantic > 0)
        {
            int cost = 80 * TurnAdvancedAbove6thMantic;
            builder.Add($"Turn advanced above 6th mantic x{TurnAdvancedAbove6thMantic} = {cost}", cost);
        }

        if (IsTeachingScroll)
        {
            int teachingCost = 3 * Power;
            builder.Add($"Teaching scripture (3×Power) = {teachingCost}", teachingCost);
        }

        if (TrueBeliever > 0)
        {
            int cost = 16 * TrueBeliever;
            builder.Add($"True Believer x{TrueBeliever} @16 = {cost}", cost);
        }

        foreach (var row in builder.Rows)
            BreakdownItems.Add(row);

        var headerBase = string.IsNullOrWhiteSpace(Name) ? "Miracle" : Name;
        var header = Power > 0 ? $"{headerBase} ({Power} Power)" : headerBase;
        Breakdown = builder.BuildSummary(header, Total);
    }
}
