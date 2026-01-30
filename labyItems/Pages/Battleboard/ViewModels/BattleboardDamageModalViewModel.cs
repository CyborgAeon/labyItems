using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System;
using labyItems.Infrastructure;
using labyItems.Services;

namespace labyItems.Pages.Battleboard.ViewModels;

public sealed class BattleboardDamageModalViewModel : ObservableObject
{
    private readonly BattleboardViewModel _board;
    private readonly object _target;
    private string _search = string.Empty;
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
        SelectedLocation = Locations.FirstOrDefault(l => l.Key.Equals(CurrentTargetKey, StringComparison.OrdinalIgnoreCase))
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
        ApplyPresetCommand = new Command<DamagePresetVm>(preset => ApplyPreset(preset));
        ApplySpellCommand = new Command<DamageSpellVm>(spell => ApplySpell(spell));
        ApplyManualHealingCommand = new Command(ApplyManualHealing);
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
        if (_isLoaded)
            return;

        var spells = await SpellDamageService.GetDamagingSpellsAsync();
        DamageSpells.Clear();
        foreach (var spell in spells.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            DamageSpells.Add(new DamageSpellVm(spell));

        _isLoaded = true;
        ApplyFilter();
    }

    private string? CurrentTargetKey
        => _target is LifeLocationVm loc ? loc.Key : null;

    private string? CurrentTargetName
        => _target is LifeLocationVm loc ? loc.Name : null;

    private void ApplyManualDamage()
    {
        var locationKey = SelectedLocation?.Key;
        if (_target is TotalLifeVm)
            locationKey = null;

        ApplyTypedDamage(TblpDamage, LocDamage, locationKey, applyToAllLocations: false);
    }

    private void ApplyPreset(DamagePresetVm? preset)
    {
        if (preset == null)
            return;

        var grade = Math.Max(1, preset.Value);
        var locationKey = SelectedLocation?.Key ?? "Chest";

        ApplyGradeDamage(grade, locationKey);
    }

    private void ApplyManualHealing()
    {
        var locationKey = SelectedLocation?.Key;
        if (_target is TotalLifeVm)
            locationKey = null;

        _board.ApplyHealing(TblpHeal, LocHeal, locationKey, applyToAllLocations: false);
    }

    private void ApplySpell(DamageSpellVm? spellVm)
    {
        if (spellVm == null)
            return;

        var spell = spellVm.Spell;
        var locationKey = SelectedLocation?.Key;
        if (string.IsNullOrWhiteSpace(locationKey))
            locationKey = "Chest";

        foreach (var part in spell.Parts)
        {
            var tblp = Math.Max(0, part.Tblp - (_board.Mac * Math.Max(0, part.MacTblp)));
            var loc = Math.Max(0, part.Loc - (_board.Mac * Math.Max(0, part.MacLoc)));

            var (targetKey, applyAll) = ResolveTarget(part.DamType, locationKey);
            _board.ApplyDamage(tblp, loc, targetKey, applyAll);

            if (part.PacDam > 0)
                _board.ApplyPacDamage(part.PacDam, targetKey, applyAll);
        }
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
        if (type.Length == 0)
            return Math.Max(0, _board.Pac + _board.Dac);

        if (type.Equals("Pure spirit", StringComparison.OrdinalIgnoreCase))
            return Math.Max(0, _board.Sac + _board.Dac);
        if (type.Equals("Pure magic", StringComparison.OrdinalIgnoreCase))
            return Math.Max(0, _board.Mac + _board.Dac);
        if (type.Equals("Through", StringComparison.OrdinalIgnoreCase))
            return Math.Max(0, _board.Dac);

        return Math.Max(0, _board.Pac + _board.Dac);
    }

    private void ApplyGradeDamage(int grade, string locationKey)
    {
        var baseDamage = grade * 6;
        var reduction = GetDamageReduction();
        var final = Math.Max(1, baseDamage - reduction);

        _board.ApplyDamage(final, final, locationKey, applyToAllLocations: false);
    }

    private static (string? TargetKey, bool ApplyAll) ResolveTarget(string damType, string? selectedLocationKey)
    {
        var key = (damType ?? string.Empty).Trim();
        if (key.Length == 0)
            return (selectedLocationKey, false);

        var lower = key.ToLowerInvariant();
        if (lower == "blast")
            return (selectedLocationKey, true);
        if (lower == "missile")
            return (selectedLocationKey, false);
        if (lower == "body")
            return (null, false);
        if (lower.Contains("head"))
            return ("Head", false);
        if (lower.Contains("chest"))
            return ("Chest", false);
        if (lower.Contains("abdomen"))
            return ("Abdomen", false);
        if (lower.Contains("left arm") || lower.Contains("leftarm"))
            return ("LeftArm", false);
        if (lower.Contains("right arm") || lower.Contains("rightarm"))
            return ("RightArm", false);
        if (lower.Contains("left leg") || lower.Contains("leftleg"))
            return ("LeftLeg", false);
        if (lower.Contains("right leg") || lower.Contains("rightleg"))
            return ("RightLeg", false);

        return (selectedLocationKey, false);
    }

    private void ApplyFilter()
    {
        var query = (_search ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            foreach (var s in DamageSpells)
                s.IsVisible = true;
            return;
        }

        foreach (var s in DamageSpells)
        {
            var hit = s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                      || s.Summary.Contains(query, StringComparison.OrdinalIgnoreCase);
            s.IsVisible = hit;
        }
    }

    private static IEnumerable<LocationOptionVm> BuildLocations(BattleboardViewModel board)
    {
        foreach (var loc in board.LifeLocations)
            yield return new LocationOptionVm(loc.Name, loc.Key);
    }

    private static IEnumerable<DamagePresetVm> BuildPresets()
    {
        return new[]
        {
            new DamagePresetVm("Single", 1),
            new DamagePresetVm("Double", 2),
            new DamagePresetVm("Triple", 3),
            new DamagePresetVm("Quad", 4),
            new DamagePresetVm("Quin", 5),
            new DamagePresetVm("Six", 6),
        };
    }
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

    public DamageSpellVm(DamageSpell spell)
    {
        Spell = spell;
    }

    public DamageSpell Spell { get; }
    public string Name => Spell.Name;
    public string Summary => Spell.Summary;

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

public sealed class HealingEntryVm
{
    public HealingEntryVm(string name, int tblp, int loc)
    {
        Name = name;
        Tblp = tblp;
        Loc = loc;
    }

    public string Name { get; }
    public int Tblp { get; }
    public int Loc { get; }
    public string Summary => $"{Tblp}/{Loc}";
}
