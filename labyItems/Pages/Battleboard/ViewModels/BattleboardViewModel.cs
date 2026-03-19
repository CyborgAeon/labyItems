using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using labyItems.Infrastructure;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Battleboard.ViewModels;

public sealed class BattleboardViewModel : ObservableObject
{
    private readonly CharacterDraft _draft;
    private readonly List<Item> _assignedItems;
    private readonly Dictionary<string, int> _resistanceMultipliers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _infiniteResistanceTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CastingEntryVm> _allCastingEntries = new();
    private string _castingSearch = string.Empty;
    private bool _hasCastingEntries;
    private bool _hasCastingPools;
    private bool _hasCastingTab;
    private bool _hasImmunities;
    private Alignment _alignment;
    private int _whiteMarks;
    private int _blackMarks;
    private string _alignmentDisplay = string.Empty;
    private BattleboardSnapshot _snapshot;
    private string _innateSearch = string.Empty;
    private bool _hasFilteredInnates;
    private bool _showLifeMissing;
    private bool _hasFatalOvercast;
    private bool _deathAlertShown;

    public BattleboardViewModel(CharacterDraft draft)
    {
        _draft = draft ?? new CharacterDraft();
        _assignedItems = BattleboardInnateCalculator.ResolveAssignedItems(_draft).ToList();
        var lifeTotals = BattleboardLifeCalculator.Calculate(_draft, _assignedItems);
        var itemArmour = BattleboardArmourCalculator.Calculate(_draft, _assignedItems);
        var resolvedInnates = BattleboardInnateCalculator.Calculate(_draft, _assignedItems);
        var abilityEffects = BattleboardAbilityEffectResolver.ResolveFallback(_draft.Abilities);
        var advancementEffects = BattleboardAdvancementEffectResolver.ResolveFallback(_draft.AdvancementAbilities);
        var itemEffects = BattleboardItemEffectResolver.ResolveFallback(_draft, _assignedItems);
        var fallbackResistanceOverrides = BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(
            abilityEffects.ResistanceOverrides,
            advancementEffects.ResistanceOverrides);
        fallbackResistanceOverrides = BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(
            fallbackResistanceOverrides,
            itemEffects.ResistanceOverrides);
        var fallbackResistanceMultipliers = BattleboardAdvancementEffectResolver.ApplyResistanceMultipliers(
            BattleboardAdvancementEffectResolver.ApplyResistanceMultipliers(
                abilityEffects.ResistanceMultipliers,
                advancementEffects.ResistanceMultipliers),
            itemEffects.ResistanceMultipliers);
        var fallbackInfiniteResistanceTypes = BattleboardAdvancementEffectResolver.MergeInfiniteResistanceTypes(
            BattleboardAdvancementEffectResolver.MergeInfiniteResistanceTypes(
                abilityEffects.InfiniteResistanceTypes,
                advancementEffects.InfiniteResistanceTypes),
            itemEffects.InfiniteResistanceTypes);
        MergeResistanceMultipliers(fallbackResistanceMultipliers);
        MergeInfiniteResistanceTypes(fallbackInfiniteResistanceTypes);
        var fallbackImmunities = (abilityEffects.Immunities ?? Array.Empty<string>())
            .Concat(advancementEffects.Immunities ?? Array.Empty<string>())
            .Concat(itemEffects.Immunities ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        CharacterName = _draft.Name ?? string.Empty;
        PlayerName = _draft.PlayerName ?? string.Empty;
        RaceDisplay = BuildRaceDisplayName(_draft, _draft.Abilities);
        ClassDisplay = BuildClassDisplayName(_draft);
        _alignment = _draft.Alignment ?? new Alignment(OrderAxis.Neutral, MoralAxis.Neutral);
        _alignmentDisplay = _alignment.ToString();
        Points = _draft.Points;
        if (!_draft.HasSetCurrentVitae)
        {
            _draft.CurrentVitae = 100;
            _draft.HasSetCurrentVitae = true;
        }
        CurrentVitae = _draft.CurrentVitae;
        Level = 8;
        WhiteMarks = 0;
        BlackMarks = 0;
        MarksMax = 3;
        AddWhiteMarkCommand = new Command(() => AddMark(isWhite: true));
        AddBlackMarkCommand = new Command(() => AddMark(isWhite: false));

        Guilds = new ObservableCollection<string>((_draft.Guilds ?? new List<string>())
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim()));

        Innates = new ObservableCollection<InnateRowVm>(
            (resolvedInnates ?? new List<InnateAbilityDraft>())
            .Where(i => !string.IsNullOrWhiteSpace(i?.Name))
            .GroupBy(i => i.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new InnateRowVm(g.Key, g.Sum(i => Math.Max(0, i.Rank))))
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
        );

        var atWill = (_draft.Abilities ?? new List<AbilityDraft>())
            .Where(a => a?.AbilityType == AbilityType.AtWill)
            .Select(FormatAbilityText)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
        AtWillAbilities = atWill;
        HasAtWillAbilities = AtWillAbilities.Count > 0;
        HasInnates = Innates.Count > 0;

        Tblp = Math.Max(0, lifeTotals.TotalTblp);
        Loc = Math.Max(0, lifeTotals.TotalLoc);
        InnatePac = Math.Max(0, _draft.ClassRaceArmour);
        Pac = Math.Max(0, itemArmour.WornPac + _draft.ClassRaceArmour);
        Dac = Math.Max(0, _draft.DAC + itemArmour.ItemDac);
        Sac = Math.Max(0, (_draft.SAC ?? 0) + itemArmour.ItemSac);
        Mac = Math.Max(0, (_draft.MAC ?? 0) + itemArmour.ItemMac);
        MaxAc = Math.Max(0, _draft.MaxAC);

        TotalLife = new TotalLifeVm(Tblp);
        Head = new LifeLocationVm("Head", "Head", Loc, Pac, Dac, MaxAc);
        Chest = new LifeLocationVm("Chest", "Chest", Loc, Pac, Dac, MaxAc);
        Abdomen = new LifeLocationVm("Abdomen", "Abdomen", Loc, Pac, Dac, MaxAc);
        LeftArm = new LifeLocationVm("Left arm", "LeftArm", Loc, Pac, Dac, MaxAc);
        RightArm = new LifeLocationVm("Right arm", "RightArm", Loc, Pac, Dac, MaxAc);
        LeftLeg = new LifeLocationVm("Left leg", "LeftLeg", Loc, Pac, Dac, MaxAc);
        RightLeg = new LifeLocationVm("Right leg", "RightLeg", Loc, Pac, Dac, MaxAc);
        LifeLocations = new ReadOnlyCollection<LifeLocationVm>(new[]
        {
            Head, Chest, Abdomen, LeftArm, RightArm, LeftLeg, RightLeg
        });
        ShowLifeMissing = false;

        ResistanceLevels = new ObservableCollection<ResistanceLevelVm>(
            BuildResistanceLevels(_draft, fallbackResistanceOverrides));
        Immunities = new ObservableCollection<ImmunityRowVm>();
        UpdateImmunityRows(BuildImmunities(_draft, fallbackImmunities));

        TotalLife.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TotalLifeVm.Current))
                _ = CheckDeathAndAlertAsync();
        };

        HookResistanceLevelSubscriptions();

        IncreaseResistanceCommand = new Command<ResistanceLevelVm>(level => AdjustResistance(level, 1));
        DecreaseResistanceCommand = new Command<ResistanceLevelVm>(level => AdjustResistance(level, -1));
        DrainAllResistanceCommand = new Command(() => AdjustAllResistance(-1));
        RegainAllResistanceCommand = new Command(() => AdjustAllResistance(1));

        BuildCastingEntries();
        ApplyCastingFilter();

        CastingPools = new ObservableCollection<CastingPoolVm>(BuildCastingPools(_draft));
        HasCastingPools = CastingPools.Count > 0;
        CastSpellCommand = new Command<CastingEntryVm>(entry => ApplyCastingCost(entry));
        foreach (var pool in CastingPools)
        {
            pool.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CastingPoolVm.Current))
                    _ = CheckDeathAndAlertAsync();
            };
        }

        HasCastingTab = _allCastingEntries.Count > 0 || HasCastingPools;

        CancelCommand = new Command(() => RestoreSnapshot());
        SubmitCommand = new Command(async () => await SubmitAsync());

        _snapshot = CaptureSnapshot();
        ApplyInnateFilter();
        BuildCastingSections();

        if (ShouldIncludeWizardBaseList())
            _ = LoadWizardBaseListAsync();

        _ = LoadResolvedBattleboardEffectsAsync();
    }

    public string CharacterName { get; }
    public string PlayerName { get; }
    public string RaceDisplay { get; }
    public string ClassDisplay { get; }
    public string AlignmentDisplay
    {
        get => _alignmentDisplay;
        private set => SetProperty(ref _alignmentDisplay, value);
    }
    public int Points { get; }
    public int Level { get; }
    public int CurrentVitae { get; }

    public int WhiteMarks
    {
        get => _whiteMarks;
        private set => SetProperty(ref _whiteMarks, value);
    }

    public int BlackMarks
    {
        get => _blackMarks;
        private set => SetProperty(ref _blackMarks, value);
    }
    public int MarksMax { get; }
    public string WhiteMarksLabel => $"White: {WhiteMarks}/{MarksMax}";
    public string BlackMarksLabel => $"Black: {BlackMarks}/{MarksMax}";
    public ICommand AddWhiteMarkCommand { get; }
    public ICommand AddBlackMarkCommand { get; }

    public ObservableCollection<string> Guilds { get; }

    public ObservableCollection<InnateRowVm> Innates { get; }
    public bool HasInnates { get; }
    public ObservableCollection<InnateRowVm> FilteredInnates { get; } = new();
    public bool HasFilteredInnates
    {
        get => _hasFilteredInnates;
        private set => SetProperty(ref _hasFilteredInnates, value);
    }
    public string InnateSearch
    {
        get => _innateSearch;
        set
        {
            if (SetProperty(ref _innateSearch, value ?? string.Empty))
                ApplyInnateFilter();
        }
    }

    public bool ShowLifeMissing
    {
        get => _showLifeMissing;
        set
        {
            if (!SetProperty(ref _showLifeMissing, value))
                return;

            foreach (var location in LifeLocations)
                location.SetShowLifeMissing(value);
        }
    }

    public List<string> AtWillAbilities { get; }
    public bool HasAtWillAbilities { get; }

    public int Tblp { get; }
    public int Loc { get; }

    public int InnatePac { get; }
    public int Pac { get; }
    public int Dac { get; }
    public int Sac { get; }
    public int Mac { get; }
    public int MaxAc { get; }

    public TotalLifeVm TotalLife { get; }
    public LifeLocationVm Head { get; }
    public LifeLocationVm Chest { get; }
    public LifeLocationVm Abdomen { get; }
    public LifeLocationVm LeftArm { get; }
    public LifeLocationVm RightArm { get; }
    public LifeLocationVm LeftLeg { get; }
    public LifeLocationVm RightLeg { get; }
    public ReadOnlyCollection<LifeLocationVm> LifeLocations { get; }

    public ObservableCollection<ResistanceLevelVm> ResistanceLevels { get; }
    public ObservableCollection<ImmunityRowVm> Immunities { get; }
    public bool HasImmunities
    {
        get => _hasImmunities;
        private set => SetProperty(ref _hasImmunities, value);
    }
    public ICommand IncreaseResistanceCommand { get; }
    public ICommand DecreaseResistanceCommand { get; }
    public ICommand DrainAllResistanceCommand { get; }
    public ICommand RegainAllResistanceCommand { get; }

    public ObservableCollection<CastingEntryVm> CastingEntries { get; } = new();
    public ObservableCollection<CastingPoolVm> CastingPools { get; }
    public ObservableCollection<CastingSectionVm> CastingSections { get; } = new();
    public ICommand CastSpellCommand { get; }
    public bool HasCastingTab
    {
        get => _hasCastingTab;
        private set => SetProperty(ref _hasCastingTab, value);
    }
    public bool HasCastingPools
    {
        get => _hasCastingPools;
        private set => SetProperty(ref _hasCastingPools, value);
    }

    public bool HasCastingEntries
    {
        get => _hasCastingEntries;
        private set => SetProperty(ref _hasCastingEntries, value);
    }

    public string CastingSearch
    {
        get => _castingSearch;
        set
        {
            if (SetProperty(ref _castingSearch, value ?? string.Empty))
                ApplyCastingFilter();
        }
    }

    public int GetResistanceMultiplier(string resistanceType)
    {
        var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(resistanceType);
        if (key.Length == 0)
            return 1;

        return _resistanceMultipliers.TryGetValue(key, out var multiplier)
            ? Math.Max(1, multiplier)
            : 1;
    }

    public bool HasInfiniteResistance(string resistanceType)
    {
        var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(resistanceType);
        return key.Length > 0 && _infiniteResistanceTypes.Contains(key);
    }

    private void AdjustResistance(ResistanceLevelVm? level, int delta)
    {
        if (level == null || delta == 0)
            return;

        var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(level.Name);
        if (key.Length == 0 || _infiniteResistanceTypes.Contains(key))
            return;

        _draft.ResistanceLevels ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var currentRaw = _draft.ResistanceLevels.TryGetValue(key, out var current)
            ? Math.Max(0, current)
            : 8;
        var nextRaw = Math.Max(0, currentRaw + delta);
        _draft.ResistanceLevels[key] = nextRaw;

        level.IsInfinite = false;
        level.Level = CalculateDisplayedResistanceLevel(key, nextRaw);
    }

    private void AdjustAllResistance(int delta)
    {
        foreach (var level in ResistanceLevels)
            AdjustResistance(level, delta);
    }

    public void ApplyDamage(int tblpDamage, int locDamage, string? locationKey, bool applyToAllLocations)
    {
        if (tblpDamage != 0)
            TotalLife.ApplyDamage(tblpDamage);

        if (locDamage != 0)
        {
            if (applyToAllLocations)
            {
                foreach (var loc in LifeLocations)
                    loc.ApplyDamage(locDamage);
            }
            else if (!string.IsNullOrWhiteSpace(locationKey))
            {
                var loc = FindLocation(locationKey);
                loc?.ApplyDamage(locDamage);
            }
        }
    }

    public void ApplyHealing(int tblpHealing, int locHealing, string? locationKey, bool applyToAllLocations)
    {
        if (tblpHealing > 0)
            TotalLife.ApplyHealing(tblpHealing);

        if (locHealing > 0)
        {
            if (applyToAllLocations)
            {
                foreach (var loc in LifeLocations)
                    loc.ApplyHealing(locHealing);
            }
            else if (!string.IsNullOrWhiteSpace(locationKey))
            {
                var loc = FindLocation(locationKey);
                loc?.ApplyHealing(locHealing);
            }
        }
    }

    public void ResetAllLife()
    {
        TotalLife.Reset();
        foreach (var loc in LifeLocations)
            loc.Reset();
    }

    public void ApplyPacDamage(int amount, string? locationKey, bool applyToAllLocations)
    {
        if (amount == 0)
            return;

        if (applyToAllLocations)
        {
            foreach (var loc in LifeLocations)
                loc.ApplyPacDamage(amount);
        }
        else if (!string.IsNullOrWhiteSpace(locationKey))
        {
            var loc = FindLocation(locationKey);
            loc?.ApplyPacDamage(amount);
        }
    }

    private void AddMark(bool isWhite)
    {
        if (isWhite)
        {
            WhiteMarks += 1;
            if (WhiteMarks >= MarksMax)
            {
                WhiteMarks = 0;
                ShiftMoral(towardsGood: true);
            }
        }
        else
        {
            BlackMarks += 1;
            if (BlackMarks >= MarksMax)
            {
                BlackMarks = 0;
                ShiftMoral(towardsGood: false);
            }
        }

        Raise(nameof(WhiteMarksLabel));
        Raise(nameof(BlackMarksLabel));
    }

    private void ShiftMoral(bool towardsGood)
    {
        var moral = _alignment.Moral;
        if (towardsGood)
        {
            if (moral == MoralAxis.Evil)
                moral = MoralAxis.Neutral;
            else if (moral == MoralAxis.Neutral)
                moral = MoralAxis.Good;
        }
        else
        {
            if (moral == MoralAxis.Good)
                moral = MoralAxis.Neutral;
            else if (moral == MoralAxis.Neutral)
                moral = MoralAxis.Evil;
        }

        _alignment = new Alignment(_alignment.Order, moral);
        _draft.Alignment = _alignment;
        AlignmentDisplay = _alignment.ToString();
    }

    public ICommand CancelCommand { get; }
    public ICommand SubmitCommand { get; }

    private BattleboardSnapshot CaptureSnapshot()
    {
        return new BattleboardSnapshot
        {
            TotalLife = TotalLife.Current,
            LocationLife = LifeLocations.ToDictionary(l => l.Key, l => l.Current, StringComparer.OrdinalIgnoreCase),
            LocationPacPenalty = LifeLocations.ToDictionary(l => l.Key, l => l.PacPenalty, StringComparer.OrdinalIgnoreCase),
            ResistanceLevels = (_draft.ResistanceLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            CastingPools = CastingPools.ToDictionary(p => p.Name, p => p.Current, StringComparer.OrdinalIgnoreCase),
            InnateUsed = Innates.ToDictionary(i => i.Name, i => i.Used, StringComparer.OrdinalIgnoreCase),
            Alignment = _alignment,
            WhiteMarks = WhiteMarks,
            BlackMarks = BlackMarks,
            HasFatalOvercast = _hasFatalOvercast
        };
    }

    private void RestoreSnapshot()
    {
        TotalLife.SetCurrent(_snapshot.TotalLife);
        foreach (var loc in LifeLocations)
        {
            if (_snapshot.LocationLife.TryGetValue(loc.Key, out var value))
                loc.SetCurrent(value);
            if (_snapshot.LocationPacPenalty.TryGetValue(loc.Key, out var pen))
                loc.SetPacPenalty(pen);
        }

        _draft.ResistanceLevels ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var level in ResistanceLevels)
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(level.Name);
            if (key.Length == 0)
                continue;

            var rawLevel = _snapshot.ResistanceLevels.TryGetValue(key, out var value) ? value : 8;
            _draft.ResistanceLevels[key] = rawLevel;
            level.IsInfinite = _infiniteResistanceTypes.Contains(key);
            level.Level = CalculateDisplayedResistanceLevel(key, rawLevel);
        }

        foreach (var pool in CastingPools)
        {
            if (_snapshot.CastingPools.TryGetValue(pool.Name, out var value))
                pool.SetCurrent(value);
        }

        foreach (var innate in Innates)
        {
            if (_snapshot.InnateUsed.TryGetValue(innate.Name, out var value))
                innate.SetUsed(value);
        }

        _alignment = _snapshot.Alignment;
        AlignmentDisplay = _alignment.ToString();
        WhiteMarks = _snapshot.WhiteMarks;
        BlackMarks = _snapshot.BlackMarks;
        Raise(nameof(WhiteMarksLabel));
        Raise(nameof(BlackMarksLabel));
        _hasFatalOvercast = _snapshot.HasFatalOvercast;
        _deathAlertShown = false;
    }

    private async Task SubmitAsync()
    {
        _snapshot = CaptureSnapshot();
        var isDead = IsDead();
        if (!isDead)
            return;

        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert("You have died", "You have died. Speak to a ref.", "OK");
    }

    private void BuildCastingEntries()
    {
        _allCastingEntries.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var list in _draft.MiracleLists ?? new List<MiracleListDraft>())
        {
            foreach (var entry in list.Entries ?? new List<MiracleListEntryDraft>())
            {
                var name = (entry.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;

                var key = $"miracle::{name}";
                if (!seen.Add(key))
                    continue;

                _allCastingEntries.Add(new CastingEntryVm(
                    name,
                    "Miracle",
                    entry.Power,
                    list.Name));
            }
        }

        if (_draft.EvilStairwayList != null)
        {
            var listName = string.IsNullOrWhiteSpace(_draft.EvilStairwayList.Name)
                ? "Evil Stairway"
                : _draft.EvilStairwayList.Name;
            foreach (var entry in _draft.EvilStairwayList.Entries ?? new List<MiracleListEntryDraft>())
            {
                var name = (entry.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;

                var key = $"miracle::{name}";
                if (!seen.Add(key))
                    continue;

                _allCastingEntries.Add(new CastingEntryVm(
                    name,
                    "Miracle",
                    entry.Power,
                    listName));
            }
        }

        foreach (var list in _draft.SpellLists ?? new List<SpellListDraft>())
        {
            foreach (var entry in list.Entries ?? new List<SpellListEntryDraft>())
            {
                var name = (entry.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;

                var key = $"spell::{name}";
                if (!seen.Add(key))
                    continue;

                _allCastingEntries.Add(new CastingEntryVm(
                    name,
                    "Spell",
                    entry.Level,
                    list.Name));
            }
        }

        _allCastingEntries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    private bool ShouldIncludeWizardBaseList()
    {
        return _draft.Class?.Contains("wizard", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task LoadWizardBaseListAsync()
    {
        try
        {
            var spells = await SpellService.GetAllAsync();
            var baseSpells = spells
                .Where(s => s.isAdvanced != true)
                .OrderBy(s => s.level)
                .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (baseSpells.Count == 0)
                return;

            var seen = new HashSet<string>(_allCastingEntries.Select(e => $"spell::{e.Name}"),
                StringComparer.OrdinalIgnoreCase);

            foreach (var spell in baseSpells)
            {
                var name = (spell.name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;
                var key = $"spell::{name}";
                if (!seen.Add(key))
                    continue;

                _allCastingEntries.Add(new CastingEntryVm(
                    name,
                    "Spell",
                    spell.level,
                    "Base list"));
            }

            _allCastingEntries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            ApplyCastingFilter();
            BuildCastingSections();
            HasCastingTab = _allCastingEntries.Count > 0 || HasCastingPools;
        }
        catch
        {
            // Ignore failures; spell data may not be present yet.
        }
    }

    private void ApplyCastingCost(CastingEntryVm? entry)
    {
        if (entry == null || CastingPools.Count == 0)
            return;

        var target = FindPoolForEntry(entry);
        if (target == null)
            return;

        var cost = Math.Max(0, entry.Power);
        if (cost == 0)
            return;

        if (cost > target.Current)
        {
            _hasFatalOvercast = true;
            _ = CheckDeathAndAlertAsync();
        }

        target.Spend(cost);
    }

    private bool IsDead()
    {
        return TotalLife.Current <= 0
               || ResistanceLevels.Any(r => r.Level <= 0)
               || _hasFatalOvercast;
    }

    private async Task CheckDeathAndAlertAsync()
    {
        if (_deathAlertShown || !IsDead())
            return;

        _deathAlertShown = true;
        if (Application.Current?.MainPage != null)
            await Application.Current.MainPage.DisplayAlert("You have died", "You have died. Speak to a ref.", "OK");
    }

    private void ApplyCastingFilter()
    {
        var query = (_castingSearch ?? string.Empty).Trim();
        CastingEntries.Clear();

        IEnumerable<CastingEntryVm> filtered = _allCastingEntries;
        if (query.Length > 0)
        {
            filtered = filtered.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                           || e.Detail.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in filtered)
            CastingEntries.Add(entry);

        HasCastingEntries = CastingEntries.Count > 0;
    }

    private void ApplyInnateFilter()
    {
        var query = (_innateSearch ?? string.Empty).Trim();
        FilteredInnates.Clear();

        IEnumerable<InnateRowVm> filtered = Innates;
        if (query.Length > 0)
            filtered = filtered.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        var rowIndex = 0;
        foreach (var innate in filtered)
        {
            innate.SetRowIndex(rowIndex++);
            FilteredInnates.Add(innate);
        }

        HasFilteredInnates = FilteredInnates.Count > 0;
    }

    private void BuildCastingSections()
    {
        CastingSections.Clear();
        if (CastingPools.Count == 0)
            return;

        foreach (var pool in CastingPools)
        {
            var kind = GuessKind(pool.Name);
            var entries = _allCastingEntries
                .Where(e => e.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var display = CastingPoolVm.FormatDisplayName(pool.Name);
            if (display.Equals("Magic", StringComparison.OrdinalIgnoreCase))
                display = "Mana";
            if (display.Equals("Earthpower", StringComparison.OrdinalIgnoreCase))
                display = "EP";

            CastingSections.Add(new CastingSectionVm(display, pool, entries, CastSpellCommand));
        }
    }

    private static bool HasPowerPools(CharacterDraft draft)
    {
        if (draft.PowerPools == null || draft.PowerPools.Count == 0)
            return false;

        return draft.PowerPools.Values.Any(v => v > 0);
    }

    private List<CastingPoolVm> BuildCastingPools(CharacterDraft draft)
    {
        var pools = new List<CastingPoolVm>();
        foreach (var kvp in draft.PowerPools ?? new Dictionary<string, int>())
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value <= 0)
                continue;

            pools.Add(new CastingPoolVm(kvp.Key.Trim(), kvp.Value));
        }

        return pools
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<ResistanceLevelVm> BuildResistanceLevels(
        CharacterDraft draft,
        IReadOnlyDictionary<string, int>? overrides)
    {
        var list = new List<ResistanceLevelVm>();
        var values = BattleboardResistanceLevelService.BuildBaselineRawLevels(
            draft.ResistanceLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            overrides);
        draft.ResistanceLevels = new Dictionary<string, int>(values, StringComparer.OrdinalIgnoreCase);

        var preferred = new[] { "Physical", "Magic", "Neuro", "Spirit" };
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in preferred)
        {
            if (!values.TryGetValue(key, out var level))
                level = 8;
            if (!used.Add(key))
                continue;
            var vm = new ResistanceLevelVm(
                key,
                CalculateDisplayedResistanceLevel(key, level),
                _infiniteResistanceTypes.Contains(BattleboardAdvancementEffectResolver.NormalizeResistanceType(key)));
            list.Add(vm);
        }

        foreach (var kvp in values.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (used.Contains(kvp.Key))
                continue;
            var canonical = BattleboardAdvancementEffectResolver.NormalizeResistanceType(kvp.Key);
            var displayName = canonical.Length > 0 ? canonical : kvp.Key;
            if (used.Contains(displayName))
                continue;

            var vm = new ResistanceLevelVm(
                displayName,
                CalculateDisplayedResistanceLevel(displayName, kvp.Value),
                _infiniteResistanceTypes.Contains(BattleboardAdvancementEffectResolver.NormalizeResistanceType(displayName)));
            list.Add(vm);
            used.Add(displayName);
        }

        return list;
    }

    private static List<string> BuildImmunities(CharacterDraft draft, IReadOnlyList<string>? resolvedImmunities)
    {
        var fromAbilities = (draft.Abilities ?? new List<AbilityDraft>())
            .Where(IsImmunityAbility)
            .Select(FormatAbilityText)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(EnsureImmunityPrefix)
            .ToList();

        return fromAbilities
            .Concat(resolvedImmunities ?? Array.Empty<string>())
            .Select(name => (name ?? string.Empty).Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void UpdateImmunityRows(IReadOnlyList<string>? names)
    {
        Immunities.Clear();
        var rowIndex = 0;
        foreach (var name in names ?? Array.Empty<string>())
            Immunities.Add(new ImmunityRowVm(name, rowIndex++));

        HasImmunities = Immunities.Count > 0;
    }

    private async Task LoadResolvedBattleboardEffectsAsync()
    {
        try
        {
            var abilityEffectsTask = BattleboardAbilityEffectResolver.ResolveAsync(_draft.Abilities);
            var advancementEffectsTask = BattleboardAdvancementEffectResolver.ResolveAsync(_draft.AdvancementAbilities);
            var itemEffectsTask = BattleboardItemEffectResolver.ResolveAsync(_draft, _assignedItems);
            await Task.WhenAll(abilityEffectsTask, advancementEffectsTask, itemEffectsTask);

            var resolvedAbilityEffects = abilityEffectsTask.Result;
            var resolvedAdvancementEffects = advancementEffectsTask.Result;
            var resolvedItemEffects = itemEffectsTask.Result;

            var mergedResistance = BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(
                resolvedAbilityEffects?.ResistanceOverrides,
                resolvedAdvancementEffects?.ResistanceOverrides);
            mergedResistance = BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(
                mergedResistance,
                resolvedItemEffects?.ResistanceOverrides);
            var mergedResistanceMultipliers = BattleboardAdvancementEffectResolver.ApplyResistanceMultipliers(
                BattleboardAdvancementEffectResolver.ApplyResistanceMultipliers(
                    resolvedAbilityEffects?.ResistanceMultipliers,
                    resolvedAdvancementEffects?.ResistanceMultipliers),
                resolvedItemEffects?.ResistanceMultipliers);
            var mergedInfiniteResistanceTypes = BattleboardAdvancementEffectResolver.MergeInfiniteResistanceTypes(
                BattleboardAdvancementEffectResolver.MergeInfiniteResistanceTypes(
                    resolvedAbilityEffects?.InfiniteResistanceTypes,
                    resolvedAdvancementEffects?.InfiniteResistanceTypes),
                resolvedItemEffects?.InfiniteResistanceTypes);

            MergeResistanceMultipliers(mergedResistanceMultipliers);
            MergeInfiniteResistanceTypes(mergedInfiniteResistanceTypes);

            if ((mergedResistance?.Count ?? 0) > 0)
                ApplyResolvedResistanceOverrides(mergedResistance);
            else
                RefreshResistanceDisplayLevels();

            var mergedImmunities = (resolvedAbilityEffects?.Immunities ?? Array.Empty<string>())
                .Concat(resolvedAdvancementEffects?.Immunities ?? Array.Empty<string>())
                .Concat(resolvedItemEffects?.Immunities ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var nextImmunities = BuildImmunities(_draft, mergedImmunities);
            UpdateImmunityRows(nextImmunities);
        }
        catch
        {
            // Keep fallback values when lookup data is unavailable.
        }
    }

    private void ApplyResolvedResistanceOverrides(IReadOnlyDictionary<string, int> overrides)
    {
        _draft.ResistanceLevels ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in overrides ?? new Dictionary<string, int>())
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var incoming = Math.Max(0, pair.Value);
            if (incoming <= 0)
                continue;

            if (_draft.ResistanceLevels.TryGetValue(key, out var existing) && incoming <= Math.Max(0, existing))
                continue;

            _draft.ResistanceLevels[key] = incoming;

            var target = ResistanceLevels.FirstOrDefault(level =>
                BattleboardAdvancementEffectResolver.NormalizeResistanceType(level.Name)
                    .Equals(key, StringComparison.OrdinalIgnoreCase));

            if (target == null)
            {
                var vm = new ResistanceLevelVm(
                    key,
                    CalculateDisplayedResistanceLevel(key, incoming),
                    _infiniteResistanceTypes.Contains(key));
                ResistanceLevels.Add(vm);
                AttachResistanceLevelSubscription(vm);
                continue;
            }

            target.IsInfinite = _infiniteResistanceTypes.Contains(key);
            target.Level = CalculateDisplayedResistanceLevel(key, incoming);
        }

        RefreshResistanceDisplayLevels();
    }

    private void HookResistanceLevelSubscriptions()
    {
        foreach (var level in ResistanceLevels)
            AttachResistanceLevelSubscription(level);
    }

    private void AttachResistanceLevelSubscription(ResistanceLevelVm level)
    {
        level.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ResistanceLevelVm.Level))
                _ = CheckDeathAndAlertAsync();
        };
    }

    private int CalculateDisplayedResistanceLevel(string resistanceType, int rawLevel)
    {
        var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(resistanceType);
        if (key.Length == 0)
            return Math.Max(0, rawLevel);

        if (_infiniteResistanceTypes.Contains(key))
            return int.MaxValue;

        var multiplier = _resistanceMultipliers.TryGetValue(key, out var resolvedMultiplier)
            ? Math.Max(1, resolvedMultiplier)
            : 1;

        return Math.Max(0, rawLevel) * multiplier;
    }

    private void RefreshResistanceDisplayLevels()
    {
        _draft.ResistanceLevels = BattleboardResistanceLevelService.BuildBaselineRawLevels(
            _draft.ResistanceLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        foreach (var level in ResistanceLevels)
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(level.Name);
            if (key.Length == 0)
                continue;

            var rawLevel = _draft.ResistanceLevels.TryGetValue(key, out var resolved)
                ? Math.Max(8, resolved)
                : 8;
            level.IsInfinite = _infiniteResistanceTypes.Contains(key);
            level.Level = CalculateDisplayedResistanceLevel(key, rawLevel);
        }
    }

    private void MergeResistanceMultipliers(IReadOnlyDictionary<string, int>? multipliers)
    {
        foreach (var pair in multipliers ?? new Dictionary<string, int>())
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(pair.Key);
            if (key.Length == 0)
                continue;

            var incoming = Math.Max(1, pair.Value);
            if (!_resistanceMultipliers.TryGetValue(key, out var existing) || incoming > existing)
                _resistanceMultipliers[key] = incoming;
        }
    }

    private void MergeInfiniteResistanceTypes(IEnumerable<string>? types)
    {
        foreach (var raw in types ?? Array.Empty<string>())
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(raw);
            if (key.Length > 0)
                _infiniteResistanceTypes.Add(key);
        }
    }

    private LifeLocationVm? FindLocation(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return LifeLocations.FirstOrDefault(loc => loc.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
               ?? LifeLocations.FirstOrDefault(loc => loc.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private CastingPoolVm? FindPoolForEntry(CastingEntryVm entry)
    {
        var name = (entry.Kind ?? string.Empty).Trim();
        if (name.Equals("Spell", StringComparison.OrdinalIgnoreCase))
            return FindPoolByKeyword("Magic") ?? CastingPools.FirstOrDefault();

        if (name.Equals("Miracle", StringComparison.OrdinalIgnoreCase))
            return FindPoolByKeyword("Spirit") ?? CastingPools.FirstOrDefault();

        if (name.Equals("Evocation", StringComparison.OrdinalIgnoreCase))
            return FindPoolByKeyword("Earth") ?? CastingPools.FirstOrDefault();

        return CastingPools.FirstOrDefault();
    }

    private CastingPoolVm? FindPoolByKeyword(string keyword)
    {
        return CastingPools.FirstOrDefault(p =>
            p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string GuessKind(string poolName)
    {
        if (poolName.Contains("Spirit", StringComparison.OrdinalIgnoreCase))
            return "Miracle";
        if (poolName.Contains("Earth", StringComparison.OrdinalIgnoreCase))
            return "Evocation";
        return "Spell";
    }

    private static string FormatAbilityText(AbilityDraft ability)
    {
        if (ability == null) return string.Empty;

        var overrideName = ability.BattleboardNameOverride ?? string.Empty;
        var text = !string.IsNullOrWhiteSpace(overrideName) ? overrideName : (ability.Name ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text))
            text = ability.ShortStringValue ?? string.Empty;

        return text;
    }

    private static bool IsImmunityAbility(AbilityDraft ability)
    {
        if (ability == null)
            return false;

        if (ability.AbilityType == AbilityType.Immunity)
            return true;

        var text = (ability.Name ?? ability.ShortStringValue ?? string.Empty).Trim();
        return text.StartsWith("Immunity to ", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("Total Immunity to ", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureImmunityPrefix(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
            return value;

        if (value.StartsWith("Immunity to ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Total Immunity to ", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return $"Immunity to {value}";
    }

    private static string BuildClassDisplayName(CharacterDraft draft)
    {
        var cls = (draft?.Class ?? string.Empty).Trim();
        if (cls.Length == 0)
            return cls;

        if (!IsWizardClassName(cls))
            return cls;

        var colour = TryGetWizardColour(draft);
        if (!colour.HasValue)
            return cls;

        var colourName = colour.Value.ToString();
        if (cls.StartsWith(colourName, StringComparison.OrdinalIgnoreCase))
            return cls;

        return $"{colourName} {cls}";
    }

    private static bool IsWizardClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;

        return className.Equals("Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("High-Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("High Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("Warlock", StringComparison.OrdinalIgnoreCase)
               || className.Equals("Rogue", StringComparison.OrdinalIgnoreCase);
    }

    private static MagicColours? TryGetWizardColour(CharacterDraft draft)
    {
        if (draft?.SpecialisationSelections != null)
        {
            var kvp = draft.SpecialisationSelections.FirstOrDefault(x =>
                x.Key.Contains("Wizard Colour", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(x.Value));

            if (!string.IsNullOrWhiteSpace(kvp.Value)
                && Enum.TryParse<MagicColours>(kvp.Value.Trim().Replace(" ", string.Empty), true, out var colour))
                return colour;
        }

        if (draft?.Abilities != null)
        {
            var ability = draft.Abilities.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a?.Source)
                && a.Source.Contains("Specialisation:Wizard Colour", StringComparison.OrdinalIgnoreCase));

            var name = ability?.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name)
                && Enum.TryParse<MagicColours>(name.Trim().Replace(" ", string.Empty), true, out var fromAbility))
                return fromAbility;
        }

        return null;
    }

    private static string BuildRaceDisplayName(CharacterDraft draft, IEnumerable<AbilityDraft> abilities)
    {
        var race = (draft?.Race ?? string.Empty).Trim();
        var suffixes = new List<string>();

        var subtype = (draft?.RaceSubtypeValue ?? draft?.RaceSubtype ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(subtype) && !string.Equals(subtype, "Standard", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = TrimSubtypeLabel(subtype);
            if (!string.IsNullOrWhiteSpace(trimmed))
                suffixes.Add(trimmed);
        }

        if (string.Equals(race, "Faerie", StringComparison.OrdinalIgnoreCase))
        {
            var faerieColours = GetFaerieColourSelections(abilities);
            foreach (var colour in faerieColours)
            {
                if (!suffixes.Any(s => string.Equals(s, colour, StringComparison.OrdinalIgnoreCase)))
                    suffixes.Add(colour);
            }
        }

        if (suffixes.Count == 0)
            return race;

        if (string.IsNullOrWhiteSpace(race))
            return string.Join(", ", suffixes);

        return $"{race} ({string.Join(", ", suffixes)})";
    }

    private static List<string> GetFaerieColourSelections(IEnumerable<AbilityDraft> abilities)
    {
        return (abilities ?? Enumerable.Empty<AbilityDraft>())
            .Where(IsFaerieColourSelection)
            .Select(a => (a?.Name ?? string.Empty).Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsFaerieColourSelection(AbilityDraft ability)
    {
        var source = (ability?.Source ?? string.Empty).Trim();
        return string.Equals(source, "Specialisation:Faerie Colour", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimSubtypeLabel(string subtype)
    {
        var value = (subtype ?? string.Empty).Trim();
        if (value.Length == 0)
            return value;

        var parenIndex = value.IndexOf('(');
        if (parenIndex >= 0)
            value = value[..parenIndex].Trim();

        if (value.Length == 0)
            return value;

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return value;

        var end = parts.Length;
        while (end > 1 && IsAllLower(parts[end - 1]))
            end--;

        return string.Join(' ', parts.Take(end));
    }

    private static bool IsAllLower(string token)
    {
        var hasLetter = false;
        foreach (var ch in token)
        {
            if (!char.IsLetter(ch))
                continue;

            hasLetter = true;
            if (!char.IsLower(ch))
                return false;
        }

        return hasLetter;
    }
}

internal sealed class BattleboardSnapshot
{
    public required int TotalLife;
    public required Dictionary<string, int> LocationLife;
    public required Dictionary<string, int> LocationPacPenalty;
    public required Dictionary<string, int> ResistanceLevels;
    public required Dictionary<string, int> CastingPools;
    public required Dictionary<string, int> InnateUsed;
    public required Alignment Alignment;
    public required int WhiteMarks;
    public required int BlackMarks;
    public required bool HasFatalOvercast;
}

public sealed class InnateRowVm : ObservableObject
{
    private int _rowIndex;

    public InnateRowVm(string name, int rank)
    {
        Name = name;
        Rank = Math.Max(0, rank);
        MaxUses = Rank;
        Used = 0;
        _rowIndex = 0;
        UseCommand = new Command(() => Use());
    }

    public string Name { get; }
    public int Rank { get; }
    public string RankLabel => $"Rank {Rank}";
    public int MaxUses { get; }
    private int _used;

    public int Used
    {
        get => _used;
        private set => SetProperty(ref _used, value);
    }

    public Color RowBackgroundColor => (_rowIndex % 2) == 0
        ? Colors.White
        : Color.FromArgb("#F9FAFB");

    public string UsesLabel => $"{Used}/{MaxUses} used";
    public ICommand UseCommand { get; }
    public event Action<InnateRowVm>? PulseRequested;

    private void Use()
    {
        if (Used >= MaxUses)
            return;

        Used += 1;
        Raise(nameof(UsesLabel));
        PulseRequested?.Invoke(this);
    }

    public void SetUsed(int value)
    {
        Used = Math.Clamp(value, 0, MaxUses);
        Raise(nameof(UsesLabel));
    }

    public void SetRowIndex(int value)
    {
        var next = Math.Max(0, value);
        if (_rowIndex == next)
            return;

        _rowIndex = next;
        Raise(nameof(RowBackgroundColor));
    }
}

public sealed class ImmunityRowVm : ObservableObject
{
    private int _rowIndex;

    public ImmunityRowVm(string name, int rowIndex)
    {
        Name = (name ?? string.Empty).Trim();
        _rowIndex = Math.Max(0, rowIndex);
    }

    public string Name { get; }
    public Color RowBackgroundColor => (_rowIndex % 2) == 0
        ? Colors.White
        : Color.FromArgb("#F9FAFB");
}

public sealed class TotalLifeVm : ObservableObject
{
    private int _current;
    private Color _backgroundColor = Colors.White;
    private Color _textColor = Colors.Black;

    public TotalLifeVm(int max)
    {
        Max = Math.Max(0, max);
        _current = Max;
        UpdateVisuals();
    }

    public int Max { get; }

    public int Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public string CurrentLabel => $"{Current}/{Max}";
    public Color BackgroundColor
    {
        get => _backgroundColor;
        private set => SetProperty(ref _backgroundColor, value);
    }
    public Color TextColor
    {
        get => _textColor;
        private set => SetProperty(ref _textColor, value);
    }
    public bool SkullVisible => Current <= 0;

    public void ApplyDamage(int amount)
    {
        if (amount == 0)
            return;

        Current = Current - amount;
        if (Current < -Max)
            Current = -Max;
        Raise(nameof(CurrentLabel));
        Raise(nameof(SkullVisible));
        UpdateVisuals();
    }

    public void ApplyHealing(int amount)
    {
        if (amount <= 0)
            return;

        Current = Math.Min(Max, Current + amount);
        Raise(nameof(CurrentLabel));
        Raise(nameof(SkullVisible));
        UpdateVisuals();
    }

    public void Reset()
    {
        Current = Max;
        Raise(nameof(CurrentLabel));
        Raise(nameof(SkullVisible));
        UpdateVisuals();
    }

    public void SetCurrent(int value)
    {
        Current = Math.Clamp(value, -Max, Max);
        Raise(nameof(CurrentLabel));
        Raise(nameof(SkullVisible));
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        var ratio = Max > 0 ? (double)Current / Max : 0;
        ratio = Math.Clamp(ratio, -1, 1);
        var t = (ratio + 1) / 2.0;
        var intensity = (int)Math.Round(80 + (175 * t));
        intensity = Math.Clamp(intensity, 60, 255);
        BackgroundColor = Color.FromRgb(intensity, intensity, intensity);
        TextColor = intensity < 140 ? Colors.White : Colors.Black;
    }
}

public sealed class LifeLocationVm : ObservableObject
{
    private int _current;
    private int _pacPenalty;
    private bool _showLifeMissing;
    private Color _backgroundColor = Colors.White;
    private Color _textColor = Colors.Black;

    public LifeLocationVm(string name, string key, int max, int pac, int dac, int maxAc)
    {
        Name = name;
        Key = key;
        Max = Math.Max(0, max);
        BasePac = Math.Max(0, pac);
        BaseDac = Math.Max(0, dac);
        MaxAc = Math.Max(0, maxAc);
        _current = Max;
        _pacPenalty = 0;
        _showLifeMissing = false;
        UpdateVisuals();
    }

    public string Name { get; }
    public string Key { get; }
    public int Max { get; }
    public int BasePac { get; }
    public int BaseDac { get; }
    public int MaxAc { get; }

    public int Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public int PacPenalty
    {
        get => _pacPenalty;
        private set => SetProperty(ref _pacPenalty, value);
    }

    public int ArmourShown => Math.Max(0, Math.Min(BasePac + PacPenalty + BaseDac, MaxAc));
    public string CurrentLabel => $"{Current}/{Max}";
    public string DisplayLabel => ShowLifeMissing
        ? $"-{Math.Max(0, Max - Current)}"
        : Current.ToString();
    public string ArmourLabel => ArmourShown.ToString();
    public bool BoneVisible => Current <= 0 && Current > -Max;
    public bool ShowLifeMissing
    {
        get => _showLifeMissing;
        private set => SetProperty(ref _showLifeMissing, value);
    }

    public Color BackgroundColor
    {
        get => _backgroundColor;
        private set => SetProperty(ref _backgroundColor, value);
    }
    public Color TextColor
    {
        get => _textColor;
        private set => SetProperty(ref _textColor, value);
    }

    public void ApplyDamage(int amount)
    {
        if (amount == 0)
            return;

        Current = Current - amount;
        if (Current < -Max)
            Current = -Max;
        Raise(nameof(CurrentLabel));
        Raise(nameof(DisplayLabel));
        Raise(nameof(BoneVisible));
        UpdateVisuals();
    }

    public void ApplyPacDamage(int amount)
    {
        if (amount <= 0)
            return;

        PacPenalty = PacPenalty - amount;
        Raise(nameof(ArmourShown));
        Raise(nameof(ArmourLabel));
    }

    public void ApplyHealing(int amount)
    {
        if (amount <= 0)
            return;

        Current = Math.Min(Max, Current + amount);
        Raise(nameof(CurrentLabel));
        Raise(nameof(DisplayLabel));
        Raise(nameof(BoneVisible));
        UpdateVisuals();
    }

    public void Reset()
    {
        Current = Max;
        Raise(nameof(CurrentLabel));
        Raise(nameof(DisplayLabel));
        Raise(nameof(BoneVisible));
        UpdateVisuals();
    }

    public void SetCurrent(int value)
    {
        Current = Math.Clamp(value, -Max, Max);
        Raise(nameof(CurrentLabel));
        Raise(nameof(DisplayLabel));
        Raise(nameof(BoneVisible));
        UpdateVisuals();
    }

    public void SetPacPenalty(int value)
    {
        PacPenalty = value;
        Raise(nameof(ArmourShown));
        Raise(nameof(ArmourLabel));
    }

    public void SetShowLifeMissing(bool value)
    {
        if (ShowLifeMissing == value)
            return;

        ShowLifeMissing = value;
        Raise(nameof(DisplayLabel));
    }

    private void UpdateVisuals()
    {
        var ratio = Max > 0 ? (double)Current / Max : 0;
        ratio = Math.Clamp(ratio, -1, 1);
        var t = (ratio + 1) / 2.0;
        var intensity = (int)Math.Round(80 + (175 * t));
        intensity = Math.Clamp(intensity, 60, 255);
        BackgroundColor = Color.FromRgb(intensity, intensity, intensity);
        TextColor = intensity < 140 ? Colors.White : Colors.Black;
    }
}

public sealed class ResistanceLevelVm : ObservableObject
{
    private int _level;
    private bool _isInfinite;

    public ResistanceLevelVm(string name, int level, bool isInfinite = false)
    {
        Name = name;
        _level = level;
        _isInfinite = isInfinite;
    }

    public string Name { get; }

    public bool IsInfinite
    {
        get => _isInfinite;
        set
        {
            if (!SetProperty(ref _isInfinite, value))
                return;

            Raise(nameof(DisplayLevelText));
            Raise(nameof(CanAdjust));
        }
    }

    public int Level
    {
        get => _level;
        set
        {
            if (!SetProperty(ref _level, value))
                return;

            Raise(nameof(DisplayLevelText));
        }
    }

    public string DisplayLevelText => IsInfinite ? "\u221E" : Level.ToString();
    public bool CanAdjust => !IsInfinite;
}

public sealed class CastingEntryVm
{
    public CastingEntryVm(string name, string kind, int power, string source)
    {
        Name = name;
        Kind = kind;
        Power = power;
        Source = source;
    }

    public string Name { get; }
    public string Kind { get; }
    public int Power { get; }
    public string Source { get; }

    public string Detail
    {
        get
        {
            var levelText = Kind.Equals("Spell", StringComparison.OrdinalIgnoreCase)
                ? $"Level {Power}"
                : $"Power {Power}";

            var sourceText = string.IsNullOrWhiteSpace(Source) ? string.Empty : $" - {Source}";
            return $"{Kind} {levelText}{sourceText}";
        }
    }
}

public sealed class CastingPoolVm : ObservableObject
{
    private int _current;

    public CastingPoolVm(string name, int total)
    {
        Name = name;
        Total = Math.Max(0, total);
        _current = Total;
        IncreaseCommand = new Command(() => Adjust(1));
        DecreaseCommand = new Command(() => Adjust(-1));
    }

    public string Name { get; }
    public string DisplayName => FormatDisplayName(Name);
    public int Total { get; }

    public int Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public string CurrentLabel => $"{Current}/{Total}";

    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }

    public event Action<CastingPoolVm>? PulseRequested;

    public void Spend(int cost)
    {
        if (cost <= 0)
            return;

        Current = Math.Max(0, Current - cost);
        Raise(nameof(CurrentLabel));
        PulseRequested?.Invoke(this);
    }

    public void SetCurrent(int value)
    {
        Current = Math.Clamp(value, 0, Total);
        Raise(nameof(CurrentLabel));
    }

    private void Adjust(int delta)
    {
        Current = Math.Clamp(Current + delta, 0, Total);
        Raise(nameof(CurrentLabel));
        PulseRequested?.Invoke(this);
    }

    public static string FormatDisplayName(string name)
    {
        if (name.Contains("Magic", StringComparison.OrdinalIgnoreCase))
            return "Mana";
        if (name.Contains("Earth", StringComparison.OrdinalIgnoreCase))
            return "EP";
        return name;
    }
}

public sealed class CastingSectionVm : ObservableObject
{
    private string _search = string.Empty;
    private bool _isExpanded = true;
    public ICommand ToggleCommand { get; }

    public CastingSectionVm(string title, CastingPoolVm pool, List<CastingEntryVm> entries, ICommand castCommand)
    {
        Title = title;
        Pool = pool;
        _allEntries = entries ?? new List<CastingEntryVm>();
        CastCommand = castCommand;
        ToggleCommand = new Command(() => IsExpanded = !IsExpanded);
        ApplyFilter();
    }

    public string Title { get; }
    public CastingPoolVm Pool { get; }
    public ICommand CastCommand { get; }
    public ObservableCollection<CastingEntryVm> Entries { get; } = new();
    private readonly List<CastingEntryVm> _allEntries;

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value ?? string.Empty))
                ApplyFilter();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
                Raise(nameof(ToggleLabel));
        }
    }

    public string ToggleLabel => IsExpanded ? "˄" : "˅";

    private void ApplyFilter()
    {
        Entries.Clear();
        var query = (_search ?? string.Empty).Trim();
        IEnumerable<CastingEntryVm> filtered = _allEntries;
        if (query.Length > 0)
            filtered = filtered.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in filtered)
            Entries.Add(entry);
    }
}
