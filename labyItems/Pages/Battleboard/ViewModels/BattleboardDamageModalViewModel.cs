using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Infrastructure;
using labyItems.Services;

namespace labyItems.Pages.Battleboard.ViewModels;

public sealed class BattleboardDamageModalViewModel : ObservableObject
{
    private readonly BattleboardViewModel _board;
    private readonly object _target;

    private string _search = string.Empty;
    private string _healingSearch = string.Empty;
    private bool _isLoaded;

    private int _tblpDamage;
    private int _locDamage;
    private int _tblpHeal;
    private int _locHeal;

    private LocationOptionVm? _selectedLocation;
    private string? _selectedDamageType;
    private bool _hasHealingEntries;

    public BattleboardDamageModalViewModel(BattleboardViewModel board, object target)
    {
        _board = board;
        _target = target;

        Locations = new ObservableCollection<LocationOptionVm>(BuildLocations(board));

        SelectedLocation =
            Locations.FirstOrDefault(l => l.Key.Equals(CurrentTargetKey, StringComparison.OrdinalIgnoreCase))
            ?? Locations.FirstOrDefault();

        DamagePresets = new ObservableCollection<DamagePresetVm>(BuildPresets());

        DamageTypeOptions = new ObservableCollection<string>(new[]
        {
            "Pure spirit",
            "Pure magic",
            "Through"
        });

        HealingEntries.CollectionChanged += (_, __) => HasHealingEntries = HealingEntries.Count > 0;
        HasHealingEntries = HealingEntries.Count > 0;

        ApplyManualDamageCommand = new Command(ApplyManualDamage);
        ApplyPresetCommand = new Command<DamagePresetVm>(ApplyPreset);
        ApplySpellCommand = new Command<DamageSpellVm>(ApplySpell);
        ApplyManualHealingCommand = new Command(ApplyManualHealing);
        ApplyHealingEntryCommand = new Command<HealingEntryVm>(ApplyHealingEntry);
        TotalHealCommand = new Command(() => _board.ResetAllLife());
    }

    public string TargetTitle => _target is TotalLifeVm ? "Total body" : (CurrentTargetName ?? "Location");

    public ObservableCollection<LocationOptionVm> Locations { get; }

    public LocationOptionVm? SelectedLocation
    {
        get => _selectedLocation;
        set => SetProperty(ref _selectedLocation, value);
    }

    public ObservableCollection<DamageSpellVm> DamageSpells { get; } = new();
    public ObservableCollection<HealingEntryVm> HealingEntries { get; } = new();
    public ObservableCollection<DamagePresetVm> DamagePresets { get; }
    public ObservableCollection<string> DamageTypeOptions { get; }

    public ICommand ApplyManualDamageCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand ApplySpellCommand { get; }
    public ICommand ApplyManualHealingCommand { get; }
    public ICommand ApplyHealingEntryCommand { get; }
    public ICommand TotalHealCommand { get; }

    public bool HasHealingEntries
    {
        get => _hasHealingEntries;
        private set => SetProperty(ref _hasHealingEntries, value);
    }

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty))
                ApplyFilter();
        }
    }

    public string HealingSearch
    {
        get => _healingSearch;
        set
        {
            if (SetProperty(ref _healingSearch, value ?? string.Empty))
                ApplyHealingFilter();
        }
    }

    public int TblpDamage
    {
        get => _tblpDamage;
        set => SetProperty(ref _tblpDamage, Math.Max(0, value));
    }

    public int LocDamage
    {
        get => _locDamage;
        set => SetProperty(ref _locDamage, Math.Max(0, value));
    }

    public int TblpHeal
    {
        get => _tblpHeal;
        set => SetProperty(ref _tblpHeal, Math.Max(0, value));
    }

    public int LocHeal
    {
        get => _locHeal;
        set => SetProperty(ref _locHeal, Math.Max(0, value));
    }

    public string? SelectedDamageType
    {
        get => _selectedDamageType;
        set => SetProperty(ref _selectedDamageType, value);
    }

    public async Task LoadAsync()
    {
        if (_isLoaded) return;

        var spells = await SpellDamageService.GetDamagingSpellsAsync();
        var miracles = await MiracleDamageService.GetDamagingMiraclesAsync();

        DamageSpells.Clear();
        DamageSpells.AddRange(spells
            .Concat(miracles)
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Kind, StringComparer.OrdinalIgnoreCase)
            .Select(s => new DamageSpellVm(s)));

        var healing = await MiracleHealingService.GetHealingMiraclesAsync();
        HealingEntries.Clear();
        HealingEntries.AddRange(healing
            .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .Select(h => new HealingEntryVm(h)));

        _isLoaded = true;
        ApplyFilter();
        ApplyHealingFilter();
    }

    private string? CurrentTargetKey => _target is LifeLocationVm loc ? loc.Key : null;
    private string? CurrentTargetName => _target is LifeLocationVm loc ? loc.Name : null;

    private void ApplyManualDamage()
    {
        var locationKey = _target is TotalLifeVm ? null : SelectedLocation?.Key;
        ApplyTypedDamage(TblpDamage, LocDamage, locationKey, applyToAllLocations: false);
    }

    private void ApplyPreset(DamagePresetVm? preset)
    {
        if (preset == null) return;

        var grade = Math.Max(1, preset.Value);
        var locationKey = SelectedLocation?.Key ?? "Chest";
        ApplyGradeDamage(grade, locationKey);
    }

    private void ApplyManualHealing()
    {
        var locationKey = _target is TotalLifeVm ? null : SelectedLocation?.Key;
        _board.ApplyHealing(TblpHeal, LocHeal, locationKey, applyToAllLocations: false);
    }

    private void ApplySpell(DamageSpellVm? spellVm)
    {
        if (spellVm == null) return;

        var selectedKey = SelectedLocation?.Key;
        if (string.IsNullOrWhiteSpace(selectedKey))
            selectedKey = "Chest";

        foreach (var part in spellVm.Spell.Parts)
            ApplySpellPart(part, selectedKey);
    }

    private void ApplySpellPart(DamagePart part, string selectedLocationKey)
    {
        if (TryApplyDamageOverride(part, selectedLocationKey))
            return;

        var (tblp, loc) = ApplyMacReduction(part);

        var (targetKey, applyAll) = ResolveTarget(part.DamType, selectedLocationKey);

        _board.ApplyDamage(tblp, loc, targetKey, applyAll);

        if (part.PacDam > 0)
            _board.ApplyPacDamage(part.PacDam, targetKey, applyAll);
    }

    private (int Tblp, int Loc) ApplyMacReduction(DamagePart part)
    {
        var ac = part.UseSac ? _board.Sac : _board.Mac;
        var tblp = Math.Max(0, part.Tblp - (ac * Math.Max(0, part.MacTblp)));
        var loc = Math.Max(0, part.Loc - (ac * Math.Max(0, part.MacLoc)));
        return (tblp, loc);
    }

    private void ApplyTypedDamage(int tblp, int loc, string? locationKey, bool applyToAllLocations)
    {
        var reduction = GetDamageReduction();
        if (reduction > 0)
        {
            tblp = Math.Max(0, tblp - reduction);
            loc = Math.Max(0, loc - reduction);
        }

        _board.ApplyDamage(tblp, loc, locationKey, applyToAllLocations);
    }

    private int GetDamageReduction()
    {
        var type = (SelectedDamageType ?? string.Empty).Trim();

        return type switch
        {
            "" => Math.Max(0, _board.Pac + _board.Dac),
            _ when type.Equals("Pure spirit", StringComparison.OrdinalIgnoreCase) => Math.Max(0, _board.Sac + _board.Dac),
            _ when type.Equals("Pure magic", StringComparison.OrdinalIgnoreCase) => Math.Max(0, _board.Mac + _board.Dac),
            _ when type.Equals("Through", StringComparison.OrdinalIgnoreCase) => Math.Max(0, _board.Dac),
            _ => Math.Max(0, _board.Pac + _board.Dac),
        };
    }

    private void ApplyGradeDamage(int grade, string locationKey)
    {
        var baseDamage = grade * 6;
        var final = Math.Max(1, baseDamage - GetDamageReduction());
        _board.ApplyDamage(final, final, locationKey, applyToAllLocations: false);
    }

    private void ApplyHealingEntry(HealingEntryVm? entryVm)
    {
        if (entryVm == null) return;

        var selectedKey = SelectedLocation?.Key;
        if (string.IsNullOrWhiteSpace(selectedKey))
            selectedKey = "Chest";

        foreach (var part in entryVm.Entry.Parts)
            ApplyHealingPart(part, selectedKey);
    }

    private void ApplyHealingPart(HealingPart part, string selectedLocationKey)
    {
        var (targetKey, applyAll) = ResolveHealingTarget(part.HealType, selectedLocationKey);
        _board.ApplyHealing(part.Tblp, part.Loc, targetKey, applyAll);
    }

    private static readonly IReadOnlyDictionary<string, string> FixedLocationMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["head"] = "Head",
            ["chest"] = "Chest",
            ["abdomen"] = "Abdomen",
        };

    private static (string? TargetKey, bool ApplyAll) ResolveTarget(string damType, string selectedLocationKey)
    {
        var token = NormalizeDamType(damType);

        return token switch
        {
            "" => (selectedLocationKey, false),

            "blast" => (selectedLocationKey, true),
            "missile" => (selectedLocationKey, false),
            "body" => (null, false),

            _ when FixedLocationMap.TryGetValue(token, out var fixedKey) => (fixedKey, false),

            _ => (selectedLocationKey, false)
        };
    }

    private static string NormalizeDamType(string? damType)
    {
        if (string.IsNullOrWhiteSpace(damType)) return string.Empty;

        var s = damType.Trim().ToLowerInvariant();
        s = string.Join(' ', s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        return s;
    }

    private (string? TargetKey, bool ApplyAll) ResolveHealingTarget(string healType, string selectedLocationKey)
    {
        var token = NormalizeDamType(healType);

        return token switch
        {
            "" => (selectedLocationKey, false),
            "body" => (null, false),
            "worst" => (FindWorstLocationKey() ?? selectedLocationKey, false),
            _ => (selectedLocationKey, false)
        };
    }

    private string? FindWorstLocationKey()
    {
        var worst = _board.LifeLocations
            .OrderBy(loc => loc.Current)
            .FirstOrDefault();
        return worst?.Key;
    }

    private void ApplyFilter()
    {
        var q = (_search ?? string.Empty).Trim();

        foreach (var s in DamageSpells)
            s.IsVisible =
                q.Length == 0
                || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || s.Kind.Contains(q, StringComparison.OrdinalIgnoreCase)
                || s.Summary.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyHealingFilter()
    {
        var q = (_healingSearch ?? string.Empty).Trim();

        foreach (var h in HealingEntries)
            h.IsVisible =
                q.Length == 0
                || h.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || h.Summary.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryApplyDamageOverride(DamagePart part, string selectedLocationKey)
    {
        var token = (part.DamageOverride ?? string.Empty).Trim().ToLowerInvariant();
        if (token.Length == 0)
            return false;

        var (targetKey, applyAll) = ResolveTarget(part.DamType, selectedLocationKey);

        switch (token)
        {
            case "sever":
            case "loc0":
                ApplyLocToZero(targetKey, applyAll);
                return true;
            case "soullance":
                ApplySoulLance();
                return true;
        }

        return false;
    }

    private void ApplyLocToZero(string? targetKey, bool applyAll)
    {
        if (applyAll)
        {
            foreach (var loc in _board.LifeLocations)
                ApplyLocToZero(loc);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetKey))
            return;

        var location = FindLocation(targetKey);
        if (location != null)
            ApplyLocToZero(location);
    }

    private void ApplyLocToZero(LifeLocationVm location)
    {
        var current = Math.Max(0, location.Current);
        if (current == 0)
            return;

        _board.ApplyDamage(current, current, location.Key, applyToAllLocations: false);
    }

    private void ApplySoulLance()
    {
        var max = _board.TotalLife.Max;
        if (max <= 0)
            return;

        var damage = (int)Math.Ceiling(max * 0.2);
        if (damage <= 0)
            return;

        _board.ApplyDamage(damage, 0, null, applyToAllLocations: false);
    }

    private LifeLocationVm? FindLocation(string key)
    {
        return _board.LifeLocations
            .FirstOrDefault(loc => loc.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? _board.LifeLocations
                .FirstOrDefault(loc => loc.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<LocationOptionVm> BuildLocations(BattleboardViewModel board)
        => board.LifeLocations.Select(loc => new LocationOptionVm(loc.Name, loc.Key));

    private static IEnumerable<DamagePresetVm> BuildPresets()
        => new[]
        {
            new DamagePresetVm("Single", 1),
            new DamagePresetVm("Double", 2),
            new DamagePresetVm("Triple", 3),
            new DamagePresetVm("Quad", 4),
            new DamagePresetVm("Quin", 5),
            new DamagePresetVm("Six", 6),
        };
}

public sealed class LocationOptionVm
{
    public LocationOptionVm(string name, string key)
    {
        Name = name;
        Key = key;
    }

    public string Name { get; }
    public string Key { get; }
}

public sealed class DamageSpellVm : ObservableObject
{
    private bool _isVisible = true;

    public DamageSpellVm(DamageSpell spell) => Spell = spell;

    public DamageSpell Spell { get; }
    public string Name => Spell.Name;
    public string Summary => Spell.Summary;
    public string Kind => Spell.Kind;

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}

public sealed class DamagePresetVm
{
    public DamagePresetVm(string label, int value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public int Value { get; }
}

public sealed class HealingEntryVm : ObservableObject
{
    private bool _isVisible = true;

    public HealingEntryVm(HealingEntry entry) => Entry = entry;

    public HealingEntry Entry { get; }
    public string Name => Entry.Name;
    public string Summary => Entry.Summary;

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}

internal static class ObservableCollectionExtensions
{
    public static void AddRange<T>(this ObservableCollection<T> col, IEnumerable<T> items)
    {
        foreach (var i in items)
            col.Add(i);
    }
}
