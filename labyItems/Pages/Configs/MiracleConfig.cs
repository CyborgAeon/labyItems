using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using labyItems.Models.DTOs;

namespace labyItems.Pages.Configs;

public class MiracleConfig : ConfigBase
{
    
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

    private int _generalSpiritStore;   // 0..12
    public int GeneralSpiritStore
    {
        get => _generalSpiritStore;
        set => SetProperty(ref _generalSpiritStore, Math.Clamp(value, 0, 12), affectsTotal: true, nameof(AnySpiritStore));
    }

    private int _sphereSpiritStore;    // 0..12
    public int SphereSpiritStore
    {
        get => _sphereSpiritStore;
        set => SetProperty(ref _sphereSpiritStore, Math.Clamp(value, 0, 12), affectsTotal: true, nameof(AnySpiritStore));
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

    protected override int ExtraTotal()
    {
        int extra = 0;

        int innates = (2 * Power * Math.Max(0, BasicPerDay)) +
                      (3 * Power * Math.Max(0, AdvancedPerDay));

        if (InnateIsMantic && innates > 0)
            extra += 3 * innates;

        if (GeneralSpiritStore > 0) extra += 4 * GeneralSpiritStore;
        if (SphereSpiritStore  > 0) extra += 3 * SphereSpiritStore;
        if (AnySpiritStore && SpiritStoreRegenerates) extra += 25;

        bool isAdv = IsAdvanced == true;
        bool isBasic = IsAdvanced == false;

        if (isBasic  && AddBasicToList)   extra += 15;
        if (isAdv    && AddAdvancedToList) extra += 18;

        if (AddWithPrep30)
            extra += (int)Math.Round(Power / 2.0, MidpointRounding.AwayFromZero);

        if (TurnBasicUpTo5thMantic     > 0) extra += 40 * TurnBasicUpTo5thMantic;
        if (TurnBasicMantic            > 0) extra += 50 * TurnBasicMantic;
        if (TurnAdvancedUpTo6thMantic  > 0) extra += 60 * TurnAdvancedUpTo6thMantic;
        if (TurnAdvancedAbove6thMantic > 0) extra += 80 * TurnAdvancedAbove6thMantic;

        if (IsTeachingScroll) extra += 3 * Power;
        if (TrueBeliever > 0) extra += 16 * TrueBeliever;

        return extra;
    }

    public void ApplyMiracle(Miracle.Result picked)
    {
        Power = Math.Max(0, picked.Power);
        Name = string.IsNullOrWhiteSpace(picked.Name) ? "Miracle" : picked.Name;
        IsAdvanced = picked.IsAdvanced;
        Alignment  = picked.Alignment;
        OnPropertyChanged(nameof(Title));
    }
}
