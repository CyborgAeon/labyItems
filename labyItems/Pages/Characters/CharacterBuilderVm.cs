using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Threading;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Models.DTOs;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using ServiceCharacterClassRecord = labyItems.Services.CharacterClassRecord;

namespace labyItems.Pages.Characters;

public sealed class CharacterBuilderVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private HashSet<string>? _allowedRaceKeysForSelectedClass;
    private string? _allowedRaceKeysForClass;

    private HashSet<string>? _allowedClassKeysForSelectedRace;
    private string? _allowedClassKeysForRace;
    private readonly SemaphoreSlim _abilityRefreshLock = new(1, 1);
    private LifeScalePoint? _humanLifeForSelectedClass;
    private ArmourTier _armourTier = ArmourTier.None;
    private const string BaronialTraditionKey = "BaronialTradition";
    private AlignmentRule? _raceAlignmentRule;
    private AlignmentRule? _classAlignmentRule;

    private static readonly Regex _armourValueRegex = new(@"([+-]?\d+)\s*(PAC|DAC|MAC|SAC)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private readonly CharacterDraft _draft;
    private readonly Action _notifyWizardGatingChanged;

    public CharacterDraft Draft => _draft;

    public void NotifyGatingChanged()
        => _notifyWizardGatingChanged();

    public CharacterSpecialisationVm SpecialisationVm { get; }

    public CharacterBuilderVm(CharacterDraft draft, Action notifyWizardGatingChanged)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;

        SpecialisationVm = new CharacterSpecialisationVm(this);

        SelectTabCommand = new Command<object>(p =>
        {
            if (p == null) return;

            if (p is int i) SelectedTabIndex = i;
            else if (int.TryParse(p.ToString(), out var j)) SelectedTabIndex = j;
        });

        ToggleClassExpandedCommand = new Command<ClassCardVm>(ToggleExpandedCommand);
        SelectClassCommand = new Command<ClassCardVm>(SelectClass);
        SelectRaceCommand = new Command<RaceCardVm>(SelectRace);
        ToggleRaceExpandedCommand = new Command<RaceCardVm>(ToggleRaceExpandedCommandImpl);

        SelectRaceFilterCommand = new Command<string>(s =>
        {
            SelectedRaceFilter = string.IsNullOrWhiteSpace(s) ? "All" : s;
        });

        ClassFilters = new ObservableCollection<string> { "All" };
        RaceFilters = new ObservableCollection<string> { "All" };

        _selectedClassFilter = "All";
        _selectedRaceFilter = "All";

        AllClasses = new ObservableCollection<ClassCardVm>();
        AllRaces = new ObservableCollection<RaceCardVm>();

        RefilterClasses();
        RefilterRaces();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadRacesAsync();
            await LoadClassesAsync();

            await RefreshAllowedRacesForSelectedClassAsync();
            RefilterRaces();

            await RefreshAllowedClassesForSelectedRaceAsync();
            RefilterClasses();

            await ApplyRaceToClassesAsync(_draft.Race);

            await CaptureHumanLifeForSelectedClassAsync();
            await UpdateDraftLifeAsync(expandIfChanged: false);

            await SpecialisationVm.ReloadAsync();

            await RefreshDraftAbilitiesAsync();
        });
    }

    public ICommand ToggleRaceExpandedCommand { get; }

    private void ToggleRaceExpandedCommandImpl(RaceCardVm? item)
    {
        if (item == null) return;

        foreach (var r in AllRaces)
        {
            if (!ReferenceEquals(r, item) && r.IsExpanded)
                r.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
        RefilterRaces();
    }

    private async Task LoadRacesAsync()
    {
        var all = await PeopleService.GetAllAsync();

        var list = new List<RaceCardVm>();
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var idx = 0; idx < ordered.Count; idx++)
        {
            var name = ordered[idx].Key;
            var record = ordered[idx].Value;
            if (record == null) continue;

            var peopleTypes = NormalizePeopleTypes(record.PeopleType);
            foreach (var t in peopleTypes)
                types.Add(t);

            var displayPeopleType = FormatPeopleTypes(peopleTypes);
            var primaryPeopleType = SelectPrimaryPeopleType(peopleTypes);

            var vm = new RaceCardVm
            {
                Id = idx + 1,
                Name = name,
                PeopleTypes = peopleTypes,
                PeopleType = displayPeopleType,
                Description = record.Description ?? "",
                BuyAsRaw = record.BuyAs ?? "",
                Icon = IconForPeopleType(primaryPeopleType),
                IsSelected = string.Equals(name, _draft.Race, StringComparison.OrdinalIgnoreCase)
            };

            vm.SearchText = BuildRaceSearchText(name, record);
            vm.BuildRowsAndChips(record.LevelledAbilities ?? new Dictionary<string, List<AbilityDefinition>>(), record.BuyAs);
            list.Add(vm);
        }

        AllRaces.Clear();
        foreach (var vm in list)
            AllRaces.Add(vm);

        RaceFilters.Clear();
        RaceFilters.Add("All");
        foreach (var t in types.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            RaceFilters.Add(t);

        if (string.IsNullOrWhiteSpace(SelectedRaceFilter) || !RaceFilters.Contains(SelectedRaceFilter))
            SelectedRaceFilter = "All";

        RefilterRaces();
    }

    private static string IconForPeopleType(string peopleType)
    {
        var t = (peopleType ?? "").Trim().ToLowerInvariant();
        if (t == "tribal") return "🪓";
        if (t.Contains("magic")) return "✨";
        if (t == "ishmaic") return "🏜️";
        if (t == "baronial") return "🏰";
        return "👤";
    }

    private static List<string> NormalizePeopleTypes(IEnumerable<string>? raw)
    {
        return (raw ?? Array.Empty<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatPeopleTypes(IEnumerable<string> peopleTypes)
    {
        var list = NormalizePeopleTypes(peopleTypes);
        return list.Count == 0 ? "" : string.Join(", ", list);
    }

    private static string SelectPrimaryPeopleType(IReadOnlyList<string> peopleTypes)
    {
        if (peopleTypes == null || peopleTypes.Count == 0)
            return "";

        var nonDemon = peopleTypes.FirstOrDefault(t => !string.Equals(t, "Demon", StringComparison.OrdinalIgnoreCase));
        return nonDemon ?? peopleTypes[0];
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (!Set(ref _selectedTabIndex, value)) return;
            Raise(nameof(IsClassTabSelected));
            Raise(nameof(IsRaceTabSelected));
        }
    }

    public bool IsClassTabSelected => SelectedTabIndex == 0;
    public bool IsRaceTabSelected => SelectedTabIndex == 1;

    public ICommand SelectTabCommand { get; }
    public ICommand SelectRaceFilterCommand { get; }

    private string _classSearchText = "";
    public string ClassSearchText
    {
        get => _classSearchText;
        set { if (Set(ref _classSearchText, value)) RefilterClasses(); }
    }

    private string _raceSearchText = "";
    public string RaceSearchText
    {
        get => _raceSearchText;
        set { if (Set(ref _raceSearchText, value)) RefilterRaces(); }
    }

    public ObservableCollection<string> ClassFilters { get; }
    public ObservableCollection<string> RaceFilters { get; }

    private string? _selectedClassFilter;
    public string? SelectedClassFilter
    {
        get => _selectedClassFilter;
        set { if (Set(ref _selectedClassFilter, value)) RefilterClasses(); }
    }

    private string? _selectedRaceFilter;
    public string? SelectedRaceFilter
    {
        get => _selectedRaceFilter;
        set { if (Set(ref _selectedRaceFilter, value)) RefilterRaces(); }
    }

    public ObservableCollection<ClassCardVm> AllClasses { get; }
    public ObservableCollection<RaceCardVm> AllRaces { get; }

    public ObservableCollection<ClassCardVm> FilteredClasses { get; } = new();
    public ObservableCollection<RaceCardVm> FilteredRaces { get; } = new();

    public ICommand ToggleClassExpandedCommand { get; }
    public ICommand SelectClassCommand { get; }
    public ICommand SelectRaceCommand { get; }

    private void ToggleExpandedCommand(ClassCardVm? item)
    {
        if (item == null) return;

        foreach (var c in AllClasses)
        {
            if (!ReferenceEquals(c, item) && c.IsExpanded)
                c.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
        RefilterClasses();
    }

    private void SelectClass(ClassCardVm? item)
    {
        if (item == null) return;

        var wasSelected = item.IsSelected;

        foreach (var c in AllClasses)
            c.IsSelected = ReferenceEquals(c, item);

        if (wasSelected)
        {
            foreach (var c in AllClasses)
                c.IsSelected = false;

            _draft.Class = string.Empty;
            _draft.TBLP = 0;
            _draft.Loc = 0;
            _humanLifeForSelectedClass = null;
            _allowedRaceKeysForSelectedClass = null;
            _allowedRaceKeysForClass = null;
        }
        else
        {
            _humanLifeForSelectedClass = null;
            _draft.Class = item.Name;
        }
        _notifyWizardGatingChanged();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await CaptureHumanLifeForSelectedClassAsync();
            await RefreshAllowedRacesForSelectedClassAsync();
            RefilterRaces();

            await UpdateDraftLifeAsync(expandIfChanged: true);

            await SpecialisationVm.ReloadAsync();
            await RefreshDraftAbilitiesAsync();
        });
    }

    private void SelectRace(RaceCardVm? item)
    {
        if (item == null) return;

        var wasSelected = item.IsSelected;

        foreach (var r in AllRaces)
            r.IsSelected = ReferenceEquals(r, item);

        if (wasSelected)
        {
            foreach (var r in AllRaces)
                r.IsSelected = false;

            _draft.Race = string.Empty;
            _draft.RaceSubtype = null;
            _draft.RaceSubtypeKey = string.Empty;
            _draft.RaceSubtypeValue = string.Empty;
            _draft.LifeScaleKeyOverride = string.Empty;
            _draft.ArmourAvailabilityOverride = string.Empty;
            _draft.ColourChoiceOverride.Clear();
            _draft.SpecialisationSelections.Clear();
            _allowedClassKeysForSelectedRace = null;
            _allowedClassKeysForRace = null;
        }
        else
        {
            _draft.ArmourAvailabilityOverride = string.Empty;
            _draft.ColourChoiceOverride.Clear();
            _draft.Race = item.Name;
        }
        _notifyWizardGatingChanged();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await RefreshAllowedClassesForSelectedRaceAsync();
            RefilterClasses();

            await ApplyRaceToClassesAsync(_draft.Race);
            await UpdateDraftLifeAsync(expandIfChanged: true);
            RefilterRaces();

            await SpecialisationVm.ReloadAsync();
            await RefreshDraftAbilitiesAsync();
        });
    }

    private async Task LoadClassesAsync()
    {
        var all = await ClassService.GetAllAsync();

        var list = new List<ClassCardVm>();
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var idx = 0; idx < ordered.Count; idx++)
        {
            var classKey = ordered[idx].Key;
            var record = ordered[idx].Value;

            var bracket = record.Brackets?.FirstOrDefault() ?? "";
            var icon = ExtractIcon(bracket);
            var category = ExtractCategory(bracket);

            if (!string.IsNullOrWhiteSpace(category))
                categories.Add(category);

            var maxAc = ParseInt(record.MaxAC);
            var powerBase = ExtractPowerBase(record);

            var raceKeyForLife = ResolveRaceKeyForLifeScale(_draft);

            var points = await LifeScalesService.GetLifeScaleAsync(raceKeyForLife, classKey);
            var tblp = points.Count >= 8 ? points[7].Body : 0;

            list.Add(new ClassCardVm
            {
                Id = idx + 1,
                Key = classKey,
                Name = classKey,
                Category = category,
                Icon = icon,
                Summary = "",
                MaxAc = maxAc,
                TBLP = tblp,
                PowerBase = powerBase,
                IsSelected = string.Equals(classKey, _draft.Class, StringComparison.OrdinalIgnoreCase)
            });
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AllClasses.Clear();
            foreach (var vm in list)
                AllClasses.Add(vm);

            ClassFilters.Clear();
            ClassFilters.Add("All");
            foreach (var c in categories.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                ClassFilters.Add(c);

            RefilterClasses();
        });
    }

    private static string ResolveRaceKeyForLifeScale(CharacterDraft draft)
    {
        var race = (draft.Race ?? "").Trim();
        if (!string.Equals(race, "Elf", StringComparison.OrdinalIgnoreCase))
            return race;

        // If you have not added RaceSubtype yet, delete this block until you do.
        var subtype = (draft.RaceSubtype ?? "").Trim();

        if (string.Equals(subtype, "Winter", StringComparison.OrdinalIgnoreCase))
            return "Winter Elf";

        if (string.Equals(subtype, "Summer", StringComparison.OrdinalIgnoreCase))
            return "Drowe";

        return "Elf";
    }

    private async Task RefreshAllowedClassesForSelectedRaceAsync()
    {
        var selectedRace = (_draft.Race ?? "").Trim();

        if (selectedRace.Length == 0)
        {
            _allowedClassKeysForSelectedRace = null;
            _allowedClassKeysForRace = null;
            return;
        }

        if (string.Equals(_allowedClassKeysForRace, selectedRace, StringComparison.OrdinalIgnoreCase)
            && _allowedClassKeysForSelectedRace != null)
        {
            return;
        }

        // IMPORTANT: class availability is based on base race identity, not the life-scale override.
        var raceForFiltering = selectedRace;

        var classes = await LifeScalesService.GetClassesForRaceAsync(raceForFiltering);

        _allowedClassKeysForSelectedRace = classes
            .Select(LifeScalesService.NormalizeKey)
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allowedClassKeysForRace = selectedRace;
    }

    private async Task RefreshAllowedRacesForSelectedClassAsync()
    {
        var selectedClass = (_draft.Class ?? "").Trim();

        if (selectedClass.Length == 0)
        {
            _allowedRaceKeysForSelectedClass = null;
            _allowedRaceKeysForClass = null;
            return;
        }

        if (string.Equals(_allowedRaceKeysForClass, selectedClass, StringComparison.OrdinalIgnoreCase)
            && _allowedRaceKeysForSelectedClass != null)
        {
            return;
        }

        var races = await LifeScalesService.GetRacesForClassAsync(selectedClass);

        _allowedRaceKeysForSelectedClass = races
            .Select(LifeScalesService.NormalizeKey)
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allowedRaceKeysForClass = selectedClass;
    }

    private async Task ApplyRaceToClassesAsync(string? raceName)
    {
        var race = string.IsNullOrWhiteSpace(raceName) ? "" : raceName;

        var tasks = new List<Task>(AllClasses.Count);
        foreach (var c in AllClasses)
        {
            c.RaceName = race;
            tasks.Add(c.ReloadProgressionAsync());
        }

        await Task.WhenAll(tasks);
    }

    private async Task CaptureHumanLifeForSelectedClassAsync()
    {
        var className = (_draft.Class ?? string.Empty).Trim();
        if (className.Length == 0)
        {
            _humanLifeForSelectedClass = null;
            return;
        }

        _humanLifeForSelectedClass = await GetLifePointAsync("Human", className);
        if (_humanLifeForSelectedClass is LifeScalePoint humanLife)
        {
            _draft.TBLP = humanLife.Body;
            _draft.Loc = humanLife.Loc;
        }
        else
        {
            _draft.TBLP = 0;
            _draft.Loc = 0;
        }
    }

    private async Task EnsureHumanLifeCachedAsync()
    {
        if (_humanLifeForSelectedClass.HasValue)
            return;

        var className = (_draft.Class ?? string.Empty).Trim();
        if (className.Length == 0) return;

        _humanLifeForSelectedClass = await GetLifePointAsync("Human", className);
    }

    private string GetEffectiveLifeScaleRaceKey()
    {
        if (!string.IsNullOrWhiteSpace(_draft.LifeScaleKeyOverride))
            return _draft.LifeScaleKeyOverride;

        return ResolveRaceKeyForLifeScale(_draft);
    }

    private async Task UpdateDraftLifeAsync(bool expandIfChanged)
    {
        var className = (_draft.Class ?? string.Empty).Trim();
        if (className.Length == 0)
        {
            _draft.TBLP = 0;
            _draft.Loc = 0;
            return;
        }

        await EnsureHumanLifeCachedAsync();

        var raceKey = GetEffectiveLifeScaleRaceKey();
        var lifePoint = await GetLifePointAsync(raceKey, className);

        if (lifePoint is null)
        {
            _draft.TBLP = 0;
            _draft.Loc = 0;
            return;
        }

        var newLife = lifePoint.Value;
        var differsFromHuman = _humanLifeForSelectedClass.HasValue &&
                               (_humanLifeForSelectedClass.Value.Body != newLife.Body ||
                                _humanLifeForSelectedClass.Value.Loc != newLife.Loc);

        _draft.TBLP = newLife.Body;
        _draft.Loc = newLife.Loc;

        if (expandIfChanged && differsFromHuman)
            ExpandSelectedClassCard();
    }

    private void ExpandSelectedClassCard()
    {
        var selected = AllClasses.FirstOrDefault(c => c.IsSelected);
        if (selected == null) return;

        foreach (var c in AllClasses)
        {
            if (!ReferenceEquals(c, selected) && c.IsExpanded)
                c.IsExpanded = false;
        }

        if (!selected.IsExpanded)
            selected.IsExpanded = true;

        RefilterClasses();
    }

    private static async Task<LifeScalePoint?> GetLifePointAsync(string raceName, string className)
    {
        var points = await LifeScalesService.GetLifeScaleAsync(raceName, className);
        var idx = PickLifeIndex(points);
        return idx >= 0 ? points[idx] : null;
    }

    private static int PickLifeIndex(IReadOnlyList<LifeScalePoint> life)
    {
        if (life == null || life.Count == 0)
            return -1;

        var idx = life.Count >= 8 ? 7 : life.Count - 1;
        return Math.Clamp(idx, 0, life.Count - 1);
    }

    public Task SyncDraftLifeAsync(bool expandIfChanged = false)
        => UpdateDraftLifeAsync(expandIfChanged);

    public async Task RefreshDraftAbilitiesAsync()
    {
        await _abilityRefreshLock.WaitAsync();
        try
        {
            var abilities = new List<AbilityDraft>();

            var (raceAbilities, raceGuildRules) = await BuildRaceAbilitiesAsync();
            var (classAbilities, classRecord, classGuildRules) = await BuildClassAbilitiesAsync();
            var (specAbilities, specGuildRules) = SpecialisationVm.BuildSelectedAbilityDraftsWithRules();
            var guildAbilities = await BuildGuildAbilitiesAsync();
            var baronialAbilities = BuildBaronialTraditionAbilities(classRecord);

            abilities.AddRange(raceAbilities);
            abilities.AddRange(classAbilities);
            abilities.AddRange(specAbilities);
            abilities.AddRange(guildAbilities);
            abilities.AddRange(baronialAbilities);

            abilities = ConsolidateAbilities(abilities);

            await UpdateDraftLifeAsync(expandIfChanged: false);
            ApplyLifeBonuses(abilities);

            UpdateArmourStats(classRecord, classAbilities, raceAbilities, specAbilities);
            UpdatePowerPools(classRecord, abilities);
            UpdateResistanceLevels(abilities, classRecord);
            _draft.GuildOverrideRules = GuildOverrideRules.Merge(
                raceGuildRules,
                classGuildRules,
                specGuildRules,
                GuildOverrideRules.FromLegacyStrings(abilities.SelectMany(a => a?.GuildOverrides ?? Enumerable.Empty<string>())));

            Draft.Innates = abilities
                .Where(a => a.AbilityType == AbilityType.Innate)
                .Select(BuildInnateDraft)
                .Where(d => d != null)
                .Cast<InnateAbilityDraft>()
                .ToList();

            Draft.Abilities = abilities
                .OrderBy(a => a.LevelGained ?? int.MaxValue)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await UpdateAvailableAlignmentsAsync();
            await SpecialisationVm.RefreshPrereqOptionsAsync();

            MainThread.BeginInvokeOnMainThread(_notifyWizardGatingChanged);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh abilities: {ex}");
        }
        finally
        {
            _abilityRefreshLock.Release();
        }
    }

    private static List<AbilityDraft> ConsolidateAbilities(IEnumerable<AbilityDraft> abilities)
    {
        var baseAbilities = new List<AbilityDraft>();
        var updates = new List<AbilityDraft>();

        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            if (ability.AbilityType == AbilityType.Update)
                updates.Add(ability);
            else
                baseAbilities.Add(ability);
        }

        ApplyAbilityUpdates(baseAbilities, updates);

        var ordered = new List<AbilityDraft>();
        var innateLookup = new Dictionary<string, AbilityDraft>(StringComparer.OrdinalIgnoreCase);
        var armourMaxBySource = BuildArmourMaxBySource(baseAbilities);
        var lifeAbilities = new List<AbilityDraft>();

        foreach (var ability in baseAbilities)
        {
            var name = (ability.Name ?? string.Empty).Trim();
            if (name.Length == 0) continue;

            var isArmour = IsArmourAbility(ability, out var armourValues);
            if (isArmour && !IsMaxArmourForSource(ability, armourValues, armourMaxBySource))
                continue;

            if (ability.AbilityType == AbilityType.Life)
            {
                lifeAbilities.Add(ability);
                continue;
            }

            if (ability.AbilityType == AbilityType.Innate)
            {
                if (innateLookup.TryGetValue(name, out var existing))
                {
                    existing.Count = (existing.Count ?? 0) + (ability.Count ?? 0);
                    if (ability.LevelGained.HasValue && (!existing.LevelGained.HasValue || ability.LevelGained.Value < existing.LevelGained.Value))
                        existing.LevelGained = ability.LevelGained;

                    if (string.IsNullOrWhiteSpace(existing.Effect) && !string.IsNullOrWhiteSpace(ability.Effect))
                        existing.Effect = ability.Effect;

                    foreach (var prereq in ability.PreReqs)
                        if (!existing.PreReqs.Contains(prereq))
                            existing.PreReqs.Add(prereq);

                    foreach (var g in ability.GuildOverrides)
                        if (!existing.GuildOverrides.Contains(g))
                            existing.GuildOverrides.Add(g);

                    continue;
                }

                innateLookup[name] = ability;
                ordered.Add(ability);
                continue;
            }

            if (!isArmour && ordered.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            ordered.Add(ability);
        }

        if (lifeAbilities.Count > 0)
            ordered.AddRange(ConsolidateLifeAbilities(lifeAbilities));

        return ordered;
    }

    private static List<AbilityDraft> ConsolidateLifeAbilities(IEnumerable<AbilityDraft> abilities)
    {
        var bestBySource = new Dictionary<string, LifeAmount>(StringComparer.OrdinalIgnoreCase);
        var bestDraftBySource = new Dictionary<string, AbilityDraft>(StringComparer.OrdinalIgnoreCase);
        int tablesTblp = 0;
        int tablesLoc = 0;

        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            if (!TryGetLifeAmount(ability, out var tblp, out var loc))
                continue;

            var source = (ability.Source ?? string.Empty).Trim();
            if (string.Equals(source, "Tables", StringComparison.OrdinalIgnoreCase))
            {
                tablesTblp += tblp;
                tablesLoc += loc;
                continue;
            }

            if (bestBySource.TryGetValue(source, out var existing))
            {
                var candidate = new LifeAmount(tblp, loc);
                if (candidate.IsHigherThan(existing))
                {
                    bestBySource[source] = candidate;
                    bestDraftBySource[source] = ability;
                }
                continue;
            }

            bestBySource[source] = new LifeAmount(tblp, loc);
            bestDraftBySource[source] = ability;
        }

        var list = new List<AbilityDraft>();
        foreach (var kvp in bestBySource)
        {
            var source = kvp.Key;
            var amount = kvp.Value;
            var baseDraft = bestDraftBySource.TryGetValue(source, out var draft) ? draft : null;
            list.Add(BuildLifeDraft(amount, source, baseDraft?.LevelGained));
        }

        if (tablesTblp > 0 || tablesLoc > 0)
            list.Add(BuildLifeDraft(new LifeAmount(tablesTblp, tablesLoc), "Tables", null));

        return list;
    }

    private static AbilityDraft BuildLifeDraft(LifeAmount amount, string source, int? levelGained)
    {
        var name = $"+{amount.Tblp}/{amount.Loc} stamina";
        return new AbilityDraft
        {
            Name = name,
            AbilityType = AbilityType.Life,
            Source = source,
            Amount = new List<int> { amount.Tblp, amount.Loc },
            LevelGained = levelGained,
            ShortStringValue = name
        };
    }

    private static bool TryGetLifeAmount(AbilityDraft ability, out int tblp, out int loc)
    {
        tblp = 0;
        loc = 0;
        if (ability == null)
            return false;

        if (ability.Amount is { Count: > 0 })
        {
            tblp = ability.Amount.Count > 0 ? ability.Amount[0] : 0;
            loc = ability.Amount.Count > 1 ? ability.Amount[1] : 0;
            return tblp != 0 || loc != 0;
        }

        var parsed = TryParseLifeAmount(ability.Effect);
        if (parsed.HasValue)
        {
            (tblp, loc) = parsed.Value;
            return tblp != 0 || loc != 0;
        }

        parsed = TryParseLifeAmount(ability.Name);
        if (parsed.HasValue)
        {
            (tblp, loc) = parsed.Value;
            return tblp != 0 || loc != 0;
        }

        return false;
    }

    private static (int Tblp, int Loc)? TryParseLifeAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = Regex.Match(text, @"(\d+)\s*/\s*(\d+)");
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups[1].Value, out var tblp))
            return null;
        if (!int.TryParse(match.Groups[2].Value, out var loc))
            return null;

        return (tblp, loc);
    }

    private void ApplyLifeBonuses(IEnumerable<AbilityDraft> abilities)
    {
        if (abilities == null)
            return;

        var tblp = 0;
        var loc = 0;

        foreach (var ability in abilities)
        {
            if (ability?.AbilityType != AbilityType.Life)
                continue;

            if (!TryGetLifeAmount(ability, out var addTblp, out var addLoc))
                continue;

            tblp += addTblp;
            loc += addLoc;
        }

        if (tblp != 0)
            _draft.TBLP += tblp;
        if (loc != 0)
            _draft.Loc += loc;
    }

    private readonly record struct LifeAmount(int Tblp, int Loc)
    {
        public bool IsHigherThan(LifeAmount other)
            => Tblp > other.Tblp || (Tblp == other.Tblp && Loc > other.Loc);
    }

    private static void ApplyAbilityUpdates(List<AbilityDraft> abilities, List<AbilityDraft> updates)
    {
        if (abilities.Count == 0 || updates.Count == 0)
            return;

        var lookup = new Dictionary<string, List<AbilityDraft>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in abilities)
        {
            var key = (ability.Name ?? string.Empty).Trim();
            if (key.Length == 0)
                continue;

            if (!lookup.TryGetValue(key, out var list))
            {
                list = new List<AbilityDraft>();
                lookup[key] = list;
            }

            list.Add(ability);
        }

        foreach (var update in updates)
        {
            var key = (update.Name ?? string.Empty).Trim();
            if (key.Length == 0)
                continue;

            if (!lookup.TryGetValue(key, out var list))
                continue;

            foreach (var target in list)
                ApplyAbilityUpdate(target, update);
        }
    }

    private static void ApplyAbilityUpdate(AbilityDraft target, AbilityDraft update)
    {
        if (!string.IsNullOrWhiteSpace(update.Effect))
        {
            target.Effect = update.Effect;
            target.ShortStringValue = update.Effect;
        }

        if (!string.IsNullOrWhiteSpace(update.Source))
            target.Source = update.Source;

        if (update.Count.HasValue)
            target.Count = update.Count;

        if (update.Amount is { Count: > 0 })
            target.Amount = new List<int>(update.Amount);

        if (!string.IsNullOrWhiteSpace(update.Frequency))
            target.Frequency = update.Frequency;

        if (!string.IsNullOrWhiteSpace(update.OverwriteKey))
            target.OverwriteKey = update.OverwriteKey;

        if (update.LevelGained.HasValue)
            target.LevelGained = update.LevelGained;

        if (update.PreReqs.Count > 0)
        {
            target.PreReqs.Clear();
            target.PreReqs.AddRange(update.PreReqs);
        }

        if (update.GuildOverrides.Count > 0)
        {
            target.GuildOverrides.Clear();
            target.GuildOverrides.AddRange(update.GuildOverrides);
        }
    }

    private static Dictionary<(string Source, string Stat), int> BuildArmourMaxBySource(IEnumerable<AbilityDraft> abilities)
    {
        var map = new Dictionary<(string, string), int>();

        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            var source = (ability.Source ?? string.Empty).Trim();
            if (source.Length == 0)
                continue;

            if (!IsArmourAbility(ability, out var values))
                continue;

            foreach (var kvp in values)
            {
                if (kvp.Value <= 0)
                    continue;

                var key = (source, kvp.Key);
                if (!map.TryGetValue(key, out var current) || kvp.Value > current)
                    map[key] = kvp.Value;
            }
        }

        return map;
    }

    private static bool IsMaxArmourForSource(
        AbilityDraft ability,
        Dictionary<string, int> armourValues,
        Dictionary<(string Source, string Stat), int> maxBySource)
    {
        var source = (ability.Source ?? string.Empty).Trim();
        if (source.Length == 0 || armourValues.Count == 0)
            return true;

        foreach (var kvp in armourValues)
        {
            if (!maxBySource.TryGetValue((source, kvp.Key), out var max))
                continue;

            if (kvp.Value == max)
                return true;
        }

        return false;
    }

    private static bool IsArmourAbility(AbilityDraft ability, out Dictionary<string, int> armourValues)
    {
        armourValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (ability == null)
            return false;

        if (TryAddArmourValueFromType(ability, armourValues))
            return true;

        var effect = ability.Effect ?? string.Empty;
        var name = ability.Name ?? string.Empty;

        var parsed = ParseArmourTokens(effect);
        if (parsed.Count == 0)
            parsed = ParseArmourTokens(name);

        foreach (var kvp in parsed)
            armourValues[kvp.Key] = kvp.Value;

        return armourValues.Count > 0;
    }

    private static bool TryAddArmourValueFromType(AbilityDraft ability, Dictionary<string, int> armourValues)
    {
        string? stat = ability.AbilityType switch
        {
            AbilityType.Pac => "PAC",
            AbilityType.Dac => "DAC",
            AbilityType.Mac => "MAC",
            AbilityType.Sac => "SAC",
            _ => null
        };

        if (stat == null)
            return false;

        var value = ResolveArmourCount(ability, stat);
        if (value == 0)
            return false;

        armourValues[stat] = value;
        return true;
    }

    private static int ResolveArmourCount(AbilityDraft ability, string stat)
    {
        if (ability.Count.HasValue)
            return ability.Count.Value;

        var parsed = ParseArmourTokens(ability.Effect ?? string.Empty);
        if (parsed.TryGetValue(stat, out var value))
            return value;

        parsed = ParseArmourTokens(ability.Name ?? string.Empty);
        if (parsed.TryGetValue(stat, out value))
            return value;

        return 0;
    }

    private static Dictionary<string, int> ParseArmourTokens(string text)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (Match m in _armourValueRegex.Matches(text))
        {
            if (!int.TryParse(m.Groups[1].Value, out var value))
                continue;

            var key = m.Groups[2].Value.ToUpperInvariant();
            if (value <= 0)
                continue;

            result[key] = result.TryGetValue(key, out var current) ? current + value : value;
        }

        return result;
    }

    private static int ComputeInnateRank(AbilityDraft ability)
    {
        if (ability == null)
            return 0;

        var baseCount = Math.Max(ability.Count ?? 1, 0);
        var total = baseCount;

        if (TryParseFrequency(ability.Frequency, out var freq) && freq > 0 && ability.LevelGained.HasValue)
        {
            var remaining = Math.Max(0, 8 - ability.LevelGained.Value);
            var additional = remaining / freq;
            total = baseCount + additional;
        }

        return Math.Clamp(total, 0, 8);
    }

    private static InnateAbilityDraft? BuildInnateDraft(AbilityDraft ability)
    {
        if (ability == null)
            return null;

        var displayName = GetInnateDisplayName(ability);
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        return new InnateAbilityDraft
        {
            Name = displayName,
            Rank = ComputeInnateRank(ability)
        };
    }

    private static string? GetInnateDisplayName(AbilityDraft ability)
    {
        var rawName = (ability?.Name ?? string.Empty).Trim();
        if (rawName.Length == 0)
            return null;

        if (TryExtractSpellInnateName(rawName, out var spellName, out var isPlaceholder))
        {
            if (isPlaceholder)
                return null;
            return spellName;
        }

        return rawName;
    }

    private static bool TryExtractSpellInnateName(string rawName, out string spellName, out bool isPlaceholder)
    {
        spellName = string.Empty;
        isPlaceholder = false;

        var text = (rawName ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var match = Regex.Match(text, @"^(?:Lvl|Level)\s*\d+\s*Spell\b(?<rest>.*)$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        var rest = (match.Groups["rest"].Value ?? string.Empty).Trim();
        if (rest.Length == 0)
        {
            isPlaceholder = true;
            return true;
        }

        var paren = Regex.Match(rest, @"\(([^)]+)\)");
        if (paren.Success)
        {
            var inner = (paren.Groups[1].Value ?? string.Empty).Trim();
            if (inner.Length > 0)
            {
                spellName = inner;
                return true;
            }
        }

        if (rest.StartsWith(":", StringComparison.Ordinal) || rest.StartsWith("-", StringComparison.Ordinal))
            rest = rest.Substring(1).Trim();

        if (rest.Length == 0)
        {
            isPlaceholder = true;
            return true;
        }

        spellName = rest;
        return true;
    }

    private static bool TryParseFrequency(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();
        return int.TryParse(text, out value);
    }

    private static void ApplyAbilitySource(IEnumerable<AbilityDraft> abilities, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            ability.Source = source;
        }
    }

    private async Task<(List<AbilityDraft> Abilities, GuildOverrideRules? GuildRules)> BuildRaceAbilitiesAsync()
    {
        var list = new List<AbilityDraft>();
        GuildOverrideRules? guildRules = null;

        var raceName = (Draft.Race ?? string.Empty).Trim();
        if (raceName.Length == 0)
        {
            _raceAlignmentRule = null;
            return (list, guildRules);
        }

        var all = await PeopleService.GetAllAsync();
        if (TryGetRecord(all, raceName, out var rec) && rec != null)
        {
            _raceAlignmentRule = rec.AlignmentRule;
            if (rec.LevelledAbilities != null)
                list.AddRange(AbilityDraftBuilder.BuildFromLevels(rec.LevelledAbilities));
            guildRules = rec.GuildOverrides;
        }
        else
        {
            _raceAlignmentRule = null;
        }

        ApplyAbilitySource(list, "Race");
        return (list, guildRules);
    }

    private async Task<(List<AbilityDraft> Abilities, ServiceCharacterClassRecord? Record, GuildOverrideRules? GuildRules)> BuildClassAbilitiesAsync()
    {
        var list = new List<AbilityDraft>();
        ServiceCharacterClassRecord? record = null;
        GuildOverrideRules? guildRules = null;

        var className = (Draft.Class ?? string.Empty).Trim();
        if (className.Length == 0)
        {
            _classAlignmentRule = null;
            return (list, record, guildRules);
        }

        var all = await ClassService.GetAllAsync();
        if (TryGetRecord(all, className, out var rec) && rec?.Levels != null)
        {
            record = rec;
            _classAlignmentRule = rec.AlignmentRule ?? BuildPaladinFallbackRule(className);
            list.AddRange(AbilityDraftBuilder.BuildFromLevels(rec.Levels));
            guildRules = rec.GuildOverrides;
        }
        else
        {
            _classAlignmentRule = BuildPaladinFallbackRule(className);
        }

        ApplyAbilitySource(list, "Class");
        return (list, record, guildRules);
    }

    private async Task<List<AbilityDraft>> BuildGuildAbilitiesAsync()
    {
        var list = new List<AbilityDraft>();

        if (Draft.Guilds.Count == 0)
            return list;

        var all = await GuildsService.GetAllAsync();
        foreach (var guild in Draft.Guilds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!TryGetRecord(all, guild, out var rec) || rec?.Benefits?.Basic == null)
                continue;

            foreach (var benefit in rec.Benefits.Basic)
            {
                if (benefit == null || string.IsNullOrWhiteSpace(benefit.Name))
                    continue;
                var parsed = AbilityDraftBuilder.ParseAbility(benefit, null);
                ApplyAbilitySource(parsed, $"Guild:{guild}");
                list.AddRange(parsed);
            }
        }

        return list;
    }

    private static AlignmentRule? BuildPaladinFallbackRule(string className)
    {
        if (!className.Equals("Paladin", StringComparison.OrdinalIgnoreCase))
            return null;

        return new AlignmentRule
        {
            Mode = "restrict",
            Allowed = new AllowedAxes
            {
                Moral = new List<MoralAxis> { MoralAxis.Good },
                Order = new List<OrderAxis> { OrderAxis.Lawful }
            },
            AllowedPairs = new List<string> { "Lawful Good" }
        };
    }

    private async Task UpdateAvailableAlignmentsAsync()
    {
        var rules = GetNonGuildAlignmentRules()
            .Where(r => r != null)
            .ToList();

        try
        {
            var guildMap = await GuildsService.GetAllAsync();
            foreach (var guild in Draft.Guilds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (guildMap.TryGetValue(guild, out var rec))
                {
                    var rule = GuildsService.GetAlignmentRule(rec);
                    if (rule != null)
                        rules.Add(rule);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ALIGNMENTS] Failed to load guild alignment rules: {ex}");
        }

        Draft.SetAvailableAlignmentsFromRules(rules!);
    }

    public IEnumerable<AlignmentRule?> GetNonGuildAlignmentRules()
    {
        yield return _raceAlignmentRule;
        yield return _classAlignmentRule;
    }

    private List<AbilityDraft> BuildBaronialTraditionAbilities(ServiceCharacterClassRecord? classRecord)
    {
        var list = new List<AbilityDraft>();

        var race = (Draft.Race ?? string.Empty).Trim();
        if (!string.Equals(race, "Human", StringComparison.OrdinalIgnoreCase))
            return list;

        if (!string.Equals(Draft.RaceSubtype?.Trim(), "Baronial", StringComparison.OrdinalIgnoreCase))
            return list;

        if (!Draft.SpecialisationSelections.TryGetValue(BaronialTraditionKey, out var selection) || string.IsNullOrWhiteSpace(selection))
            return list;

        var choice = NormalizeBaronialChoice(selection);
        var powerBase = (classRecord?.Powerbase?.FirstOrDefault() ?? string.Empty).Trim();
        var powerBaseLower = powerBase.ToLowerInvariant();
        var isWizard = classRecord?.Brackets?.Any(b => b.Contains("wizard", StringComparison.OrdinalIgnoreCase)) == true;

        if (choice.Equals("circle", StringComparison.OrdinalIgnoreCase))
        {
            if (powerBaseLower.Contains("magic"))
            {
                list.Add(new AbilityDraft
                {
                    Name = "Reduce casting damage by 1",
                    AbilityType = AbilityType.Static,
                    ShortStringValue = "-1 casting damage"
                });
            }

            if (powerBaseLower.Contains("spirit"))
            {
                list.Add(new AbilityDraft
                {
                    Name = "May increase a miracle's level of effect by 1",
                    AbilityType = AbilityType.Static,
                    ShortStringValue = "+1 miracle effect level"
                });
            }
        }
        else if (choice.Equals("hedge", StringComparison.OrdinalIgnoreCase))
        {
            if (isWizard)
            {
                list.Add(new AbilityDraft { Name = "Disguise skill", AbilityType = AbilityType.Static });
                list.Add(new AbilityDraft
                {
                    Name = $"Immunity to informational effects ({powerBase})",
                    AbilityType = AbilityType.Immunity,
                    ShortStringValue = $"Immunity to informational effects ({powerBase})"
                });
                list.Add(new AbilityDraft
                {
                    Name = $"+1 resistance ({powerBase})",
                    AbilityType = AbilityType.Resistance,
                    ShortStringValue = $"+1 {powerBase} resistance"
                });
                list.Add(new AbilityDraft
                {
                    Name = "+1 level of life (race)",
                    AbilityType = AbilityType.Static,
                    ShortStringValue = "+1 life (race)"
                });
            }

            var ov = new AbilityDraft
            {
                Name = "Guild override: Hedge",
                AbilityType = AbilityType.GuildOverride,
                ShortStringValue = "pr:"
            };
            ov.GuildOverrides.Add("pr:");
            list.Add(ov);
        }

        ApplyAbilitySource(list, "Baronial");
        return list;
    }

    private static string NormalizeBaronialChoice(string raw)
        => (raw ?? string.Empty).Replace("Ⓞ", string.Empty).Trim();

    private void UpdateArmourStats(
        ServiceCharacterClassRecord? classRecord,
        List<AbilityDraft> classAbilities,
        List<AbilityDraft> raceAbilities,
        List<AbilityDraft> specAbilities)
    {
        var classValues = ExtractArmourValues(classAbilities);
        var raceValues = ExtractArmourValues(raceAbilities);
        var specValues = ExtractArmourValues(specAbilities);

        var hasClassArmour = classRecord?.Armour?.Wearable is { Count: > 0 };
        var hasNoArmour = !hasClassArmour || classValues.DisallowArmour || raceValues.DisallowArmour || specValues.DisallowArmour;

        var classTier = hasClassArmour
            ? DetermineArmourTierFromClassArmour(classRecord)
            : ArmourTier.None;
        var raceTier = DetermineArmourTierFromAbilities(raceAbilities);
        var specTier = DetermineArmourTierFromAbilities(specAbilities);
        var overrideTier = hasClassArmour ? ParseArmourTier(_draft.ArmourAvailabilityOverride) : null;
        var combinedTier = classTier;
        if (raceTier != ArmourTier.None)
            combinedTier = combinedTier == ArmourTier.None ? raceTier : (ArmourTier)Math.Min((int)combinedTier, (int)raceTier);
        if (specTier != ArmourTier.None)
            combinedTier = combinedTier == ArmourTier.None ? specTier : (ArmourTier)Math.Min((int)combinedTier, (int)specTier);

        var finalTier = hasClassArmour
            ? ResolveArmourTier(combinedTier, overrideTier, hasNoArmour)
            : ArmourTier.None;

        _armourTier = finalTier;
        _draft.ArmourAvailability = finalTier.ToString();

        var maxPac = GetMaxTotalPacForTier(finalTier);
        if (_draft.WornArmour > maxPac)
            _draft.WornArmour = maxPac;

        _draft.ClassRaceArmour = classValues.Pac + raceValues.Pac + specValues.Pac;
        _draft.DAC = classValues.Dac + raceValues.Dac + specValues.Dac;

        var macTotal = classValues.Mac + raceValues.Mac + specValues.Mac;
        var sacTotal = classValues.Sac + raceValues.Sac + specValues.Sac;
        _draft.MAC = macTotal > 0 ? macTotal : null;
        _draft.SAC = sacTotal > 0 ? sacTotal : null;

        _draft.MaxAC = classRecord != null ? ParseInt(classRecord.MaxAC) : 0;
    }

    private static ArmourTier? ParseArmourTier(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return null;

        if (Enum.TryParse<ArmourTier>(text, ignoreCase: true, out var parsed))
            return parsed;

        return text.ToLowerInvariant() switch
        {
            "light armour" or "light armor" => ArmourTier.Light,
            "medium armour" or "medium armor" => ArmourTier.Medium,
            "heavy armour" or "heavy armor" => ArmourTier.Heavy,
            _ => null
        };
    }

    private ArmourTier ResolveArmourTier(ArmourTier classTier, ArmourTier? overrideTier, bool noArmour)
    {
        if (noArmour)
            return ArmourTier.None;

        if (overrideTier.HasValue)
            return overrideTier.Value;

        return classTier;
    }

    private static ArmourTier DetermineArmourTierFromClassArmour(ServiceCharacterClassRecord? classRecord)
    {
        var wearable = classRecord?.Armour?.Wearable;
        if (wearable == null || wearable.Count == 0)
            return ArmourTier.None;

        var normalized = wearable
            .Select(w => (w ?? string.Empty).Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .ToList();

        bool hasHeavy = normalized.Any(s => s.Contains("heavy"));
        bool hasMedium = normalized.Any(s => s.Contains("medium"));
        bool hasLight = normalized.Any(s => s.Contains("light"));

        if (hasHeavy)
            return ArmourTier.Heavy;
        if (hasMedium)
            return ArmourTier.Medium;
        if (hasLight)
            return ArmourTier.Light;

        return ArmourTier.None;
    }

    private static ArmourTier DetermineArmourTierFromAbilities(IEnumerable<AbilityDraft> abilities)
    {
        var tier = ArmourTier.None;

        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            var name = ability?.Name ?? string.Empty;
            if (name.Length == 0) continue;

            var lower = name.ToLowerInvariant();

            if (lower.Contains("heavy armour") || lower.Contains("heavy armor"))
            {
                tier = ArmourTier.Heavy;
                break;
            }

            if (tier < ArmourTier.Medium && (lower.Contains("medium armour") || lower.Contains("medium armor")))
            {
                tier = ArmourTier.Medium;
                continue;
            }

            if (tier < ArmourTier.Light && (lower.Contains("light armour") || lower.Contains("light armor")))
                tier = ArmourTier.Light;
        }

        return tier;
    }

    private static ArmourValues ExtractArmourValues(IEnumerable<AbilityDraft> abilities)
    {
        int pac = 0, dac = 0, mac = 0, sac = 0;
        bool disallow = false;

        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            var name = ability.Name ?? string.Empty;
            var effect = ability.Effect ?? string.Empty;
            if (name.Length == 0 && effect.Length == 0) continue;

            var lower = name.ToLowerInvariant();
            if (lower.Contains("cannot wear armour") || lower.Contains("cannot wear armor") || lower.Contains("may not wear armour") || lower.Contains("no armour"))
                disallow = true;

            if (!IsArmourAbility(ability, out var parsed))
            {
                parsed = ParseArmourTokens(effect);
                if (parsed.Count == 0)
                    parsed = ParseArmourTokens(name);
            }

            foreach (var kvp in parsed)
            {
                switch (kvp.Key.ToUpperInvariant())
                {
                    case "PAC":
                        pac = Math.Max(pac, kvp.Value);
                        break;
                    case "DAC":
                        dac = Math.Max(dac, kvp.Value);
                        break;
                    case "MAC":
                        mac = Math.Max(mac, kvp.Value);
                        break;
                    case "SAC":
                        sac = Math.Max(sac, kvp.Value);
                        break;
                }
            }
        }

        return new ArmourValues(pac, dac, mac, sac, disallow);
    }

    private static int GetMaxPacForTier(ArmourTier tier)
        => tier switch
        {
            ArmourTier.Light => 4,
            ArmourTier.Medium => 6,
            ArmourTier.Heavy => 8,
            _ => 0
        };

    private static int GetMaxTotalPacForTier(ArmourTier tier)
        => tier switch
        {
            ArmourTier.Light => GetMaxPacForTier(tier),
            ArmourTier.Medium => GetMaxPacForTier(tier) + 1,
            ArmourTier.Heavy => GetMaxPacForTier(tier) + 3,
            _ => 0
        };

    private void UpdateResistanceLevels(IEnumerable<AbilityDraft> abilities, ServiceCharacterClassRecord? classRecord)
    {
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Physical", 0 },
            { "Magic", 0 },
            { "Neuro", 0 },
            { "Spirit", 0 }
        };

        var powerBase = ExtractPowerBase(classRecord);
        if (string.IsNullOrWhiteSpace(powerBase) && Draft.PowerPools != null && Draft.PowerPools.Count > 0)
            powerBase = Draft.PowerPools.Keys.FirstOrDefault();

        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            var text = $"{ability.Name} {ability.ShortStringValue} {ability.Effect}".ToLowerInvariant();
            if (ability.AbilityType != AbilityType.Resistance && !text.Contains("resistance"))
                continue;

            var type = DetectResistanceType(ability, powerBase);
            if (type == null && ability.AbilityType == AbilityType.Resistance)
                type = MapPowerBaseToResistanceType(powerBase);
            if (type == null)
                continue;

            var delta = ability.Count ?? ParseFirstInt(ability.ShortStringValue, ability.Name);
            if (delta == 0)
                delta = 1; // default increment when a resistance is granted but no explicit count

            levels[type] = levels.TryGetValue(type, out var current)
                ? current + delta
                : delta;
        }

        _draft.ResistanceLevels = levels;
    }

    private void UpdatePowerPools(ServiceCharacterClassRecord? classRecord, IEnumerable<AbilityDraft> abilities)
    {
        var pools = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (classRecord != null)
        {
            var casterLevel = classRecord.CasterLevel ?? 8;
            if (classRecord.PowerCalculations is { Count: > 0 })
            {
                foreach (var calc in classRecord.PowerCalculations)
                {
                    var key = (calc.PowerBase ?? string.Empty).Trim();
                    if (key.Length == 0) continue;
                    pools[key] = EvaluatePowerCalculation(calc.Calculation, casterLevel);
                }
            }
            else if (classRecord.Powerbase is { Count: > 0 })
            {
                foreach (var pb in classRecord.Powerbase)
                {
                    var key = (pb ?? string.Empty).Trim();
                    if (key.Length == 0) continue;
                    pools[key] = Math.Max(pools.TryGetValue(key, out var existing) ? existing : 0, 0);
                }
            }
        }

        var monkLocCount = abilities?.Count(a => a?.Name?.IndexOf("monk locational curing", StringComparison.OrdinalIgnoreCase) >= 0) ?? 0;
        if (monkLocCount > 0 && _draft.TBLP > 0)
        {
            var bonus = (int)Math.Floor(_draft.TBLP / 3.0);
            if (bonus > 0)
            {
                if (pools.TryGetValue("MonkLoc", out var existing))
                    pools["MonkLoc"] = existing + bonus * monkLocCount;
                else
                    pools["MonkLoc"] = bonus * monkLocCount;
            }
        }

        _draft.PowerPools = pools;
    }

    private static int ParseFirstInt(params string?[] candidates)
    {
        foreach (var c in candidates ?? Array.Empty<string>())
        {
            var text = (c ?? string.Empty).Trim();
            if (text.Length == 0) continue;

            var m = Regex.Match(text, @"-?\d+");
            if (m.Success && int.TryParse(m.Value, out var n))
                return n;
        }

        return 0;
    }

    private static string? DetectResistanceType(AbilityDraft ability, string? powerBase)
    {
        var text = $"{ability.Name} {ability.ShortStringValue} {ability.Effect}".ToLowerInvariant();

        if (text.Contains("physical"))
            return "Physical";
        if (text.Contains("magic") || text.Contains("magical"))
            return "Magic";
        if (text.Contains("neuro") || text.Contains("neuronic"))
            return "Neuro";
        if (text.Contains("spirit"))
            return "Spirit";

        if (text.Contains("powerbase") || text.Contains("power base"))
        {
            var mapped = MapPowerBaseToResistanceType(powerBase);
            if (mapped != null)
                return mapped;
        }

        return null;
    }

    private static string? MapPowerBaseToResistanceType(string? powerBase)
    {
        var pb = (powerBase ?? string.Empty).Trim().ToLowerInvariant();
        if (pb.Length == 0)
            return null;

        if (pb.Contains("magic"))
            return "Magic";
        if (pb.Contains("spirit"))
            return "Spirit";
        if (pb.Contains("neuro") || pb.Contains("neuronic"))
            return "Neuro";

        return null;
    }

    private static int EvaluatePowerCalculation(string? calculation, int casterLevel)
    {
        var expr = (calculation ?? string.Empty).Trim().ToUpperInvariant();
        expr = expr.Replace("TM", "1");

        if (expr == "CL^2+CL")
            return casterLevel * casterLevel + casterLevel;
        if (expr == "(CL^2+CL)/2")
            return (int)Math.Round((casterLevel * casterLevel + casterLevel) / 2.0);
        if (expr.StartsWith("CL*"))
        {
            var rest = expr.Substring("CL*".Length);
            if (int.TryParse(rest, out var factor))
                return casterLevel * factor;

            if (rest.Contains("*"))
            {
                var parts = rest.Split('*', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                double product = casterLevel;
                foreach (var p in parts)
                {
                    if (int.TryParse(p, out var n))
                        product *= n;
                }
                return (int)Math.Round(product);
            }
        }

        return 0;
    }

    private static bool TryGetRecord<T>(Dictionary<string, T> map, string key, out T? value)
    {
        if (map.TryGetValue(key, out value))
            return true;

        foreach (var kvp in map)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static int ParseInt(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)) return n;
        if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out n)) return n;
        return 0;
    }

    private static string ExtractPowerBase(labyItems.Services.CharacterClassRecord record)
    {
        var powerBase = record.Powerbase?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(powerBase))
            return powerBase;

        return record.PowerCalculations?.FirstOrDefault()?.PowerBase ?? "";
    }

    private static string ExtractIcon(string bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket)) return "🛡️";
        var parts = bracket.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "🛡️";
    }

    private static string ExtractCategory(string bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket)) return "";
        var parts = bracket.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1] : bracket;
    }

    private static string BuildRaceSearchText(string name, PeopleRecord record)
    {
        var parts = new List<string>
        {
            name,
            FormatPeopleTypes(NormalizePeopleTypes(record.PeopleType)),
            record.Description ?? "",
            record.AdditionalInfo ?? "",
            record.BuyAs ?? "",
            record.Subtype?.OptionsSource ?? "",
            record.Subtype?.DisplayName ?? ""
        };

        if (string.Equals(name, "Human", StringComparison.OrdinalIgnoreCase))
            parts.Add("baronial ishmaic standard tribal barbarian human");

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)))
            .ToLowerInvariant();
    }

    private void RefilterClasses()
    {
        var q = (ClassSearchText ?? "").Trim().ToLowerInvariant();
        var filter = SelectedClassFilter ?? "All";

        var allowed = _allowedClassKeysForSelectedRace;

        var list = AllClasses
            .Where(c =>
                (filter == "All" || string.Equals(c.Category, filter, StringComparison.OrdinalIgnoreCase)) &&
                (allowed == null || allowed.Contains(LifeScalesService.NormalizeKey(c.Name ?? c.Key ?? ""))) &&
                (q.Length == 0 ||
                 (c.Name ?? "").ToLowerInvariant().Contains(q) ||
                 (c.Summary ?? "").ToLowerInvariant().Contains(q)))
            .OrderByDescending(c => c.IsSelected)
            .ToList();

        ReplaceItems(FilteredClasses, list);
    }

    private void RefilterRaces()
    {
        var q = (RaceSearchText ?? "").Trim().ToLowerInvariant();
        var filter = SelectedRaceFilter ?? "All";

        var allowed = _allowedRaceKeysForSelectedClass;

        var list = AllRaces
            .Where(r =>
                (filter == "All" || r.PeopleTypes.Any(t => string.Equals(t, filter, StringComparison.OrdinalIgnoreCase))) &&
                (allowed == null || allowed.Contains(LifeScalesService.NormalizeKey(r.Name))) &&
                (q.Length == 0 ||
                 r.Name.ToLowerInvariant().Contains(q) ||
                 (r.Description ?? "").ToLowerInvariant().Contains(q) ||
                 (r.SearchText ?? string.Empty).Contains(q)))
            .OrderByDescending(r => r.IsSelected)
            .ToList();

        ReplaceItems(FilteredRaces, list);
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IList<T> items)
    {
        target.Clear();
        foreach (var i in items)
            target.Add(i);
    }

    private enum ArmourTier
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3
    }

    private sealed record ArmourValues(int Pac, int Dac, int Mac, int Sac, bool DisallowArmour);
}
