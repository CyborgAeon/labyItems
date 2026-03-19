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
using labyItems.Helpers;
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
    private readonly HashSet<string> _selectedClassFilterKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _abilityRefreshLock = new(1, 1);
    private CancellationTokenSource? _selectionPipelineCts;
    private LifeScalePoint? _humanLifeForSelectedClass;
    private ArmourTier _armourTier = ArmourTier.None;
    private AlignmentRule? _raceAlignmentRule;
    private AlignmentRule? _classAlignmentRule;

    private static readonly Regex _armourValueRegex = new(
        @"([+-]?\d+)\s*(PAC|DAC|MAC|SAC)",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

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
    private readonly ICharacterCreationDataService _creationDataService;

    public CharacterDraft Draft => _draft;

    public void NotifyGatingChanged()
        => _notifyWizardGatingChanged();

    public CharacterSpecialisationVm SpecialisationVm { get; }

    public CharacterBuilderVm(
        CharacterDraft draft,
        Action notifyWizardGatingChanged,
        ICharacterCreationDataService? creationDataService = null)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;
        _creationDataService = creationDataService
            ?? ServiceHelper.ResolveService<ICharacterCreationDataService>()
            ?? new CharacterCreationDataService();

        SpecialisationVm = new CharacterSpecialisationVm(this);

        ToggleClassExpandedCommand = new Command<ClassCardVm>(item => _ = ToggleExpandedCommandAsync(item));
        SelectClassCommand = new Command<ClassCardVm>(SelectClass);
        SelectRaceCommand = new Command<RaceCardVm>(SelectRace);
        ToggleRaceExpandedCommand = new Command<RaceCardVm>(ToggleRaceExpandedCommandImpl);
        ToggleClassFilterChipCommand = new Command<ClassFilterChipVm>(ToggleClassFilterChip);
        ShowClassSelectionCommand = new Command(() => SelectedTabIndex = 0);

        SelectRaceFilterCommand = new Command<RaceFilterChipVm>(chip =>
        {
            SelectedRaceFilter = string.IsNullOrWhiteSpace(chip?.Label) ? "All" : chip.Label;
        });

        ClassFilterChips = new ObservableCollection<ClassFilterChipVm>();
        RaceFilterChips = new ObservableCollection<RaceFilterChipVm> { new("All", true) };
        _selectedRaceFilter = "All";

        AllClasses = new ObservableCollection<ClassCardVm>();
        AllRaces = new ObservableCollection<RaceCardVm>();
        SelectedTabIndex = string.IsNullOrWhiteSpace(_draft.Class) ? 0 : 1;

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
        var all = await _creationDataService.GetPeopleAsync();

        var list = new List<RaceCardVm>();
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var idx = 0; idx < ordered.Count; idx++)
        {
            var name = ordered[idx].Key;
            var record = ordered[idx].Value;
            if (record == null) continue;

            var peopleTypes = NormalizePeopleTypes(record.PeopleType);
            types.UnionWith(peopleTypes);

            var displayPeopleType = FormatPeopleTypes(peopleTypes);
            var primaryPeopleType = SelectPrimaryPeopleType(peopleTypes);

            var vm = new RaceCardVm
            {
                Id = idx + 1,
                Name = name,
                PeopleTypes = peopleTypes,
                PeopleType = displayPeopleType,
                IsNonStandard = record.NonStandard,
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

        RebuildRaceFilterChips(types);

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

    private void ToggleClassFilterChip(ClassFilterChipVm? chip)
    {
        if (chip == null)
            return;

        var shouldSelect = !chip.IsSelected;
        chip.IsSelected = shouldSelect;

        if (shouldSelect)
            _selectedClassFilterKeys.Add(chip.Key);
        else
            _selectedClassFilterKeys.Remove(chip.Key);

        RefilterClasses();
    }

    private void RebuildClassFilterChips()
    {
        var labelsByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var classVm in AllClasses)
        {
            foreach (var bracket in classVm.Brackets)
            {
                var label = (bracket ?? string.Empty).Trim();
                var key = NormalizeClassFilterKey(label);
                if (key.Length == 0 || labelsByKey.ContainsKey(key))
                    continue;

                labelsByKey[key] = label;
            }
        }

        _selectedClassFilterKeys.RemoveWhere(key => !labelsByKey.ContainsKey(key));

        ClassFilterChips.Clear();
        foreach (var entry in labelsByKey
                     .OrderBy(kvp => NormalizeClassFilterSortLabel(kvp.Value), StringComparer.OrdinalIgnoreCase))
        {
            ClassFilterChips.Add(new ClassFilterChipVm(
                entry.Key,
                entry.Value,
                _selectedClassFilterKeys.Contains(entry.Key)));
        }
    }

    private static string NormalizeClassFilterKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static string NormalizeClassFilterSortLabel(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        var firstSpace = text.IndexOf(' ');
        if (firstSpace <= 0 || firstSpace >= text.Length - 1)
            return text;

        var prefix = text[..firstSpace];
        var hasEmojiPrefix = prefix.Any(ch => !char.IsLetterOrDigit(ch));
        return hasEmojiPrefix ? text[(firstSpace + 1)..].Trim() : text;
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            var next = value == 1 && !CanSelectRace ? 0 : value;
            if (!Set(ref _selectedTabIndex, next)) return;
            Raise(nameof(IsClassTabSelected));
            Raise(nameof(IsRaceTabSelected));
        }
    }

    public bool IsClassTabSelected => SelectedTabIndex == 0;
    public bool IsRaceTabSelected => SelectedTabIndex == 1;
    public bool CanSelectRace => !string.IsNullOrWhiteSpace((_draft.Class ?? string.Empty).Trim());
    public bool HasRaceSelection => !string.IsNullOrWhiteSpace((_draft.Race ?? string.Empty).Trim());

    public bool TryMoveToRaceSelection()
    {
        if (!CanSelectRace)
            return false;

        SelectedTabIndex = 1;
        return IsRaceTabSelected;
    }

    public bool TryMoveToClassSelection()
    {
        SelectedTabIndex = 0;
        return IsClassTabSelected;
    }

    public ICommand ShowClassSelectionCommand { get; }
    public ICommand SelectRaceFilterCommand { get; }
    public ICommand ToggleClassFilterChipCommand { get; }

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

    public ObservableCollection<ClassFilterChipVm> ClassFilterChips { get; }
    public ObservableCollection<RaceFilterChipVm> RaceFilterChips { get; }

    private string? _selectedRaceFilter;
    public string? SelectedRaceFilter
    {
        get => _selectedRaceFilter;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "All" : value;
            if (!Set(ref _selectedRaceFilter, normalized))
                return;

            SyncRaceFilterChipSelection();
            RefilterRaces();
        }
    }

    public ObservableCollection<ClassCardVm> AllClasses { get; }
    public ObservableCollection<RaceCardVm> AllRaces { get; }

    public ObservableCollection<ClassCardVm> FilteredClasses { get; } = new();
    public ObservableCollection<RaceCardVm> FilteredRaces { get; } = new();

    public ICommand ToggleClassExpandedCommand { get; }
    public ICommand SelectClassCommand { get; }
    public ICommand SelectRaceCommand { get; }

    private async Task ToggleExpandedCommandAsync(ClassCardVm? item)
    {
        if (item == null) return;

        foreach (var c in AllClasses)
        {
            if (!ReferenceEquals(c, item) && c.IsExpanded)
                c.IsExpanded = false;
        }

        var shouldExpand = !item.IsExpanded;
        if (shouldExpand)
            await item.EnsureProgressionLoadedAsync();

        item.IsExpanded = shouldExpand;
    }

    private void SelectClass(ClassCardVm? item)
    {
        if (item == null) return;

        var wasSelected = item.IsSelected;
        var shouldSelect = !wasSelected;
        var previousClass = (_draft.Class ?? string.Empty).Trim();

        item.IsSelected = shouldSelect;
        foreach (var c in AllClasses)
        {
            if (ReferenceEquals(c, item))
                continue;

            c.IsSelected = false;
        }

        if (wasSelected)
        {
            _draft.Class = string.Empty;
            _draft.TBLP = 0;
            _draft.Loc = 0;
            _humanLifeForSelectedClass = null;
            _allowedRaceKeysForSelectedClass = null;
            _allowedRaceKeysForClass = null;
            SelectedTabIndex = 0;
            ResetDependentDraftSelections();
        }
        else
        {
            _humanLifeForSelectedClass = null;
            _draft.Class = item.Name;
            if (!string.Equals(previousClass, item.Name, StringComparison.OrdinalIgnoreCase))
                ResetDependentDraftSelections();
        }

        Raise(nameof(CanSelectRace));
        Raise(nameof(HasRaceSelection));
        _notifyWizardGatingChanged();

        RunSelectionPipeline(async cancellationToken =>
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            await CaptureHumanLifeForSelectedClassAsync();
            cancellationToken.ThrowIfCancellationRequested();

            await RefreshAllowedRacesForSelectedClassAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var raceStillValid = EnsureSelectedRaceAllowedForClass();
            RefilterRaces();
            cancellationToken.ThrowIfCancellationRequested();

            if (!raceStillValid)
            {
                await RefreshAllowedClassesForSelectedRaceAsync();
                cancellationToken.ThrowIfCancellationRequested();
                RefilterClasses();
            }

            await UpdateDraftLifeAsync(expandIfChanged: true);
            cancellationToken.ThrowIfCancellationRequested();

            await SpecialisationVm.ReloadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await RefreshDraftAbilitiesAsync();
            _notifyWizardGatingChanged();
        });
    }

    private void SelectRace(RaceCardVm? item)
    {
        if (item == null) return;

        var wasSelected = item.IsSelected;
        var shouldSelect = !wasSelected;
        var previousRace = (_draft.Race ?? string.Empty).Trim();

        // Apply selection state to the tapped card first so visual selection updates immediately.
        item.IsSelected = shouldSelect;
        foreach (var r in AllRaces)
        {
            if (ReferenceEquals(r, item))
                continue;

            r.IsSelected = false;
        }

        if (wasSelected)
        {
            _draft.Race = string.Empty;
            _draft.RaceSubtype = null;
            _draft.RaceSubtypeKey = string.Empty;
            _draft.RaceSubtypeValue = string.Empty;
            _draft.LifeScaleKeyOverride = string.Empty;
            _draft.ArmourAvailabilityOverride = string.Empty;
            _draft.ColourChoiceOverride.Clear();
            _allowedClassKeysForSelectedRace = null;
            _allowedClassKeysForRace = null;
            ResetDependentDraftSelections();
        }
        else
        {
            _draft.ArmourAvailabilityOverride = string.Empty;
            _draft.ColourChoiceOverride.Clear();
            _draft.Race = item.Name;
            if (!string.Equals(previousRace, item.Name, StringComparison.OrdinalIgnoreCase))
                ResetDependentDraftSelections();
        }

        Raise(nameof(HasRaceSelection));
        Raise(nameof(CanSelectRace));
        _notifyWizardGatingChanged();

        RunSelectionPipeline(async cancellationToken =>
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            await RefreshAllowedClassesForSelectedRaceAsync();
            cancellationToken.ThrowIfCancellationRequested();

            EnsureSelectedClassAllowedForRace();
            RefilterClasses();
            cancellationToken.ThrowIfCancellationRequested();

            await ApplyRaceToClassesAsync(_draft.Race);
            cancellationToken.ThrowIfCancellationRequested();

            await UpdateDraftLifeAsync(expandIfChanged: true);
            cancellationToken.ThrowIfCancellationRequested();

            RefilterRaces();
            cancellationToken.ThrowIfCancellationRequested();

            await SpecialisationVm.ReloadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            await RefreshDraftAbilitiesAsync();
            _notifyWizardGatingChanged();
        });
    }

    private void RunSelectionPipeline(Func<CancellationToken, Task> pipeline)
    {
        _selectionPipelineCts?.Cancel();
        _selectionPipelineCts?.Dispose();

        var cts = new CancellationTokenSource();
        _selectionPipelineCts = cts;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await pipeline(cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CharacterBuilder selection pipeline failed: {ex}");
            }
            finally
            {
                if (ReferenceEquals(_selectionPipelineCts, cts))
                    _selectionPipelineCts = null;

                cts.Dispose();
            }
        });
    }

    private void ResetDependentDraftSelections()
    {
        _draft.RaceSubtype = null;
        _draft.RaceSubtypeKey = string.Empty;
        _draft.RaceSubtypeValue = string.Empty;
        _draft.LifeScaleKeyOverride = string.Empty;
        _draft.ArmourAvailabilityOverride = string.Empty;
        _draft.ColourChoiceOverride.Clear();
        _draft.SpecialisationSelections.Clear();
        _draft.Guilds.Clear();
        _draft.GuildBenefitSelections.Clear();
        _draft.GuildOverrideRules = new GuildOverrideRules();
        _draft.Alignment = null;
        _draft.Abilities.Clear();
        _draft.Innates.Clear();
    }

    private bool EnsureSelectedRaceAllowedForClass()
    {
        var selectedRace = (_draft.Race ?? string.Empty).Trim();
        if (selectedRace.Length == 0 || _allowedRaceKeysForSelectedClass == null || _allowedRaceKeysForSelectedClass.Count == 0)
            return true;

        var normalizedRace = _creationDataService.NormalizeLifeScaleKey(selectedRace);
        if (_allowedRaceKeysForSelectedClass.Contains(normalizedRace))
            return true;

        _draft.Race = string.Empty;
        _allowedClassKeysForSelectedRace = null;
        _allowedClassKeysForRace = null;
        ResetDependentDraftSelections();
        SyncRaceSelectionFromDraft();
        Raise(nameof(HasRaceSelection));
        return false;
    }

    private bool EnsureSelectedClassAllowedForRace()
    {
        var selectedClass = (_draft.Class ?? string.Empty).Trim();
        if (selectedClass.Length == 0 || _allowedClassKeysForSelectedRace == null || _allowedClassKeysForSelectedRace.Count == 0)
            return true;

        var normalizedClass = _creationDataService.NormalizeLifeScaleKey(selectedClass);
        if (_allowedClassKeysForSelectedRace.Contains(normalizedClass))
            return true;

        _draft.Class = string.Empty;
        _draft.TBLP = 0;
        _draft.Loc = 0;
        _humanLifeForSelectedClass = null;
        _allowedRaceKeysForSelectedClass = null;
        _allowedRaceKeysForClass = null;
        SelectedTabIndex = 0;
        ResetDependentDraftSelections();
        SyncClassSelectionFromDraft();
        Raise(nameof(CanSelectRace));
        return false;
    }

    private void SyncClassSelectionFromDraft()
    {
        var selected = (_draft.Class ?? string.Empty).Trim();
        foreach (var classVm in AllClasses)
            classVm.IsSelected = selected.Length > 0 && classVm.Name.Equals(selected, StringComparison.OrdinalIgnoreCase);
    }

    private void SyncRaceSelectionFromDraft()
    {
        var selected = (_draft.Race ?? string.Empty).Trim();
        foreach (var raceVm in AllRaces)
            raceVm.IsSelected = selected.Length > 0 && raceVm.Name.Equals(selected, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadClassesAsync()
    {
        var all = await _creationDataService.GetClassesAsync();

        var list = new List<ClassCardVm>();

        var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var idx = 0; idx < ordered.Count; idx++)
        {
            var classKey = ordered[idx].Key;
            var record = ordered[idx].Value;

            var (icon, category, bracketTags) = ClassCardVm.ParseBrackets(record.Brackets);

            var maxAc = ParseInt(record.MaxAC);
            var powerBase = ExtractPowerBase(record);

            var raceKeyForLife = ResolveRaceKeyForLifeScale(_draft);

            var points = await _creationDataService.GetLifeScaleAsync(raceKeyForLife, classKey);
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
                IsNonStandard = record.NonStandard,
                BracketTags = bracketTags,
                IsSelected = string.Equals(classKey, _draft.Class, StringComparison.OrdinalIgnoreCase)
            });
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AllClasses.Clear();
            foreach (var vm in list)
                AllClasses.Add(vm);

            RebuildClassFilterChips();
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

        var classes = await _creationDataService.GetClassesForRaceAsync(raceForFiltering);
        if (classes.Count == 0)
        {
            _allowedClassKeysForSelectedRace = null;
            _allowedClassKeysForRace = null;
            return;
        }

        _allowedClassKeysForSelectedRace = classes
            .Select(_creationDataService.NormalizeLifeScaleKey)
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

        var races = await _creationDataService.GetRacesForClassAsync(selectedClass);

        _allowedRaceKeysForSelectedClass = races
            .Select(_creationDataService.NormalizeLifeScaleKey)
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allowedRaceKeysForClass = selectedClass;
    }

    private async Task ApplyRaceToClassesAsync(string? raceName)
    {
        var race = string.IsNullOrWhiteSpace(raceName) ? "" : raceName;

        var tasks = new List<Task>();
        foreach (var c in AllClasses)
        {
            c.RaceName = race;
            c.MarkProgressionDirty();

            if (c.IsExpanded)
                tasks.Add(c.EnsureProgressionLoadedAsync());
        }

        if (tasks.Count > 0)
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

        _humanLifeForSelectedClass = await GetLifePointAsync("Human", className)
                                     ?? await GetLifePointAsync(string.Empty, className);
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

        _humanLifeForSelectedClass = await GetLifePointAsync("Human", className)
                                     ?? await GetLifePointAsync(string.Empty, className);
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

    private async Task<LifeScalePoint?> GetLifePointAsync(string raceName, string className)
    {
        var points = await _creationDataService.GetLifeScaleAsync(raceName, className);
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

            abilities.AddRange(raceAbilities);
            abilities.AddRange(classAbilities);
            abilities.AddRange(specAbilities);
            abilities.AddRange(guildAbilities);

            abilities = ConsolidateAbilities(abilities);

            await UpdateDraftLifeAsync(expandIfChanged: false);
            ApplyLifeBonuses(abilities);

            UpdateArmourStats(classRecord, classAbilities, raceAbilities, specAbilities, abilities);
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

                    CollectionHelper.AddDistinct(existing.PreReqs, ability.PreReqs);
                    CollectionHelper.AddDistinct(existing.GuildOverrides, ability.GuildOverrides);

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
            AddUpdateLookupKey(lookup, ability, ability.Name);
            AddUpdateLookupKey(lookup, ability, ability.UpdateKey);
            AddUpdateLookupKey(lookup, ability, ability.BattleboardNameOverride);
        }

        foreach (var update in updates)
        {
            var key = GetUpdateKey(update);
            if (key.Length == 0)
                continue;

            if (!lookup.TryGetValue(key, out var list))
                continue;

            ApplyAbilityUpdates(list, update);
        }
    }

    private static void AddUpdateLookupKey(
        Dictionary<string, List<AbilityDraft>> lookup,
        AbilityDraft ability,
        string? rawKey)
    {
        var key = (rawKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return;

        if (!lookup.TryGetValue(key, out var list))
        {
            list = new List<AbilityDraft>();
            lookup[key] = list;
        }

        if (!list.Contains(ability))
            list.Add(ability);
    }

    private static string GetUpdateKey(AbilityDraft update)
    {
        var key = (update.UpdateKey ?? string.Empty).Trim();
        if (key.Length > 0)
            return key;

        return (update.Name ?? string.Empty).Trim();
    }

    private static void ApplyAbilityUpdates(IEnumerable<AbilityDraft> targets, AbilityDraft update)
    {
        foreach (var target in targets)
            ApplyAbilityUpdate(target, update);
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

        if (!string.IsNullOrWhiteSpace(update.BattleboardNameOverride))
            target.BattleboardNameOverride = update.BattleboardNameOverride;

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

            UpdateArmourMaxForAbility(map, source, values);
        }

        return map;
    }

    private static void UpdateArmourMaxForAbility(
        Dictionary<(string Source, string Stat), int> map,
        string source,
        Dictionary<string, int> values)
    {
        foreach (var kvp in values)
        {
            if (kvp.Value <= 0)
                continue;

            var key = (source, kvp.Key);
            if (!map.TryGetValue(key, out var current) || kvp.Value > current)
                map[key] = kvp.Value;
        }
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
        return AbilityDraftBuilder.ResolveInnateRank(ability, achievedLevel: 8);
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

    private static void ApplyAbilitySource(IEnumerable<AbilityDraft> abilities, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            if (string.IsNullOrWhiteSpace(ability.Source))
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

        var all = await _creationDataService.GetPeopleAsync();
        if (_creationDataService.TryGetByName(all, raceName, out var rec) && rec != null)
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

        var all = await _creationDataService.GetClassesAsync();
        if (_creationDataService.TryGetByName(all, className, out var rec) && rec?.Levels != null)
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

        var all = await _creationDataService.GetGuildsAsync();
        foreach (var guild in Draft.Guilds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_creationDataService.TryGetByName(all, guild, out var rec) || rec?.Benefits?.Basic == null)
                continue;

            AppendGuildBenefits(list, rec.Benefits.Basic, guild, "Basic");
        }

        return list;
    }

    private void AppendGuildBenefits(
        List<AbilityDraft> list,
        IEnumerable<GuildBenefitEntry>? benefits,
        string guildName,
        string tier)
    {
        var optionIndex = 0;
        foreach (var entry in benefits ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            if (entry?.Ability != null)
            {
                var benefit = entry.Ability;
                if (!string.IsNullOrWhiteSpace(benefit.Name))
                {
                    var parsed = AbilityDraftBuilder.ParseAbility(benefit, null);
                    ApplyAbilitySource(parsed, $"Guild:{guildName}");
                    list.AddRange(parsed);
                }
                continue;
            }

            if (entry?.Options == null || entry.Options.Count == 0)
                continue;

            optionIndex++;
            var key = GuildBenefitKeys.BuildSelectionKey(guildName, tier, optionIndex);
            if (!Draft.GuildBenefitSelections.TryGetValue(key, out var selectionIndex))
                continue;

            var idx = selectionIndex - 1;
            if (idx < 0 || idx >= entry.Options.Count)
                continue;

            var option = entry.Options[idx];
            AppendGuildBenefitOptionAbilities(list, option, guildName);
        }
    }

    private static void AppendGuildBenefitOptionAbilities(
        List<AbilityDraft> list,
        GuildBenefitOption option,
        string guildName)
    {
        foreach (var ability in option.Abilities ?? new List<AbilityDefinition>())
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.Name))
                continue;

            var parsed = AbilityDraftBuilder.ParseAbility(ability, null);
            ApplyAbilitySource(parsed, $"Guild:{guildName}");
            list.AddRange(parsed);
        }
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
            var guildMap = await _creationDataService.GetGuildsAsync();
            foreach (var guild in Draft.Guilds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (guildMap.TryGetValue(guild, out var rec))
                {
                    var rule = _creationDataService.GetGuildAlignmentRule(rec);
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

    private void UpdateArmourStats(
        ServiceCharacterClassRecord? classRecord,
        List<AbilityDraft> classAbilities,
        List<AbilityDraft> raceAbilities,
        List<AbilityDraft> specAbilities,
        List<AbilityDraft> allAbilities)
    {
        var classValues = ExtractArmourValues(classAbilities);
        var raceValues = ExtractArmourValues(raceAbilities);
        var specValues = ExtractArmourValues(specAbilities);
        var totalValues = ArmourBonusResolver.ResolveMaxPerSourceTotals(allAbilities);

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

        _draft.ClassRaceArmour = totalValues.Pac;
        _draft.DAC = totalValues.Dac;

        var macTotal = totalValues.Mac;
        var sacTotal = totalValues.Sac;
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

            ApplyParsedArmourValues(parsed, ref pac, ref dac, ref mac, ref sac);
        }

        return new ArmourValues(pac, dac, mac, sac, disallow);
    }

    private static void ApplyParsedArmourValues(
        Dictionary<string, int> parsed,
        ref int pac,
        ref int dac,
        ref int mac,
        ref int sac)
    {
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
            _draft.CasterLevel = classRecord.CasterLevel ?? 8;
            if (classRecord.PowerCalculations is { Count: > 0 })
            {
                foreach (var calc in classRecord.PowerCalculations)
                {
                    var key = (calc.PowerBase ?? string.Empty).Trim();
                    if (key.Length == 0) continue;
                    pools[key] = EvaluatePowerCalculation(calc.Calculation, _draft.CasterLevel);
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

    private static int ParseInt(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)) return n;
        if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out n)) return n;
        return 0;
    }

    private static string ExtractPowerBase(labyItems.Services.CharacterClassRecord? record)
    {
        if (record == null)
            return "";

        var powerBase = record.Powerbase?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(powerBase))
            return powerBase;

        return record.PowerCalculations?.FirstOrDefault()?.PowerBase ?? "";
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

        var allowed = _allowedClassKeysForSelectedRace;
        var selectedFilters = _selectedClassFilterKeys;

        var list = AllClasses
            .Where(c =>
                (selectedFilters.Count == 0 || ClassMatchesSelectedBracketFilter(c, selectedFilters)) &&
                (allowed == null || allowed.Contains(_creationDataService.NormalizeLifeScaleKey(c.Name ?? c.Key ?? ""))) &&
                (q.Length == 0 ||
                 (c.Name ?? "").ToLowerInvariant().Contains(q) ||
                 (c.Summary ?? "").ToLowerInvariant().Contains(q)))
            .ToList();

        ReplaceItems(FilteredClasses, list);
    }

    private static bool ClassMatchesSelectedBracketFilter(ClassCardVm classVm, HashSet<string> selectedFilters)
    {
        if (selectedFilters.Count == 0)
            return true;

        foreach (var bracket in classVm.Brackets)
        {
            var key = NormalizeClassFilterKey(bracket);
            if (key.Length > 0 && selectedFilters.Contains(key))
                return true;
        }

        foreach (var label in classVm.BracketLabels)
        {
            var key = NormalizeClassFilterKey(label);
            if (key.Length > 0 && selectedFilters.Contains(key))
                return true;
        }

        return false;
    }

    private void RefilterRaces()
    {
        var q = (RaceSearchText ?? "").Trim().ToLowerInvariant();
        var filter = string.IsNullOrWhiteSpace(SelectedRaceFilter) ? "All" : SelectedRaceFilter!;

        var allowed = _allowedRaceKeysForSelectedClass;

        var list = AllRaces
            .Where(r =>
                (filter == "All" || r.PeopleTypes.Any(t => string.Equals(t, filter, StringComparison.OrdinalIgnoreCase))) &&
                (allowed == null || allowed.Contains(_creationDataService.NormalizeLifeScaleKey(r.Name))) &&
                (q.Length == 0 ||
                 r.Name.ToLowerInvariant().Contains(q) ||
                 (r.Description ?? "").ToLowerInvariant().Contains(q) ||
                 (r.SearchText ?? string.Empty).Contains(q)))
            .ToList();

        ReplaceItems(FilteredRaces, list);
    }

    private void RebuildRaceFilterChips(IEnumerable<string> raceTypes)
    {
        var options = new List<string> { "All" };
        options.AddRange((raceTypes ?? Enumerable.Empty<string>())
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase));

        var selected = string.IsNullOrWhiteSpace(SelectedRaceFilter) ? "All" : SelectedRaceFilter!;
        if (!options.Contains(selected, StringComparer.OrdinalIgnoreCase))
            selected = "All";

        _selectedRaceFilter = selected;
        Raise(nameof(SelectedRaceFilter));

        RaceFilterChips.Clear();
        foreach (var option in options)
        {
            RaceFilterChips.Add(new RaceFilterChipVm(
                option,
                string.Equals(option, selected, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private void SyncRaceFilterChipSelection()
    {
        var selected = string.IsNullOrWhiteSpace(SelectedRaceFilter) ? "All" : SelectedRaceFilter!;
        foreach (var chip in RaceFilterChips)
            chip.IsSelected = string.Equals(chip.Label, selected, StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IList<T> items)
    {
        target.Clear();
        foreach (var i in items)
            target.Add(i);
    }

    public sealed class ClassFilterChipVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ClassFilterChipVm(string key, string label, bool isSelected)
        {
            Key = (key ?? string.Empty).Trim();
            Label = (label ?? string.Empty).Trim();
            _isSelected = isSelected;
        }

        public string Key { get; }
        public string Label { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public sealed class RaceFilterChipVm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public RaceFilterChipVm(string label, bool isSelected)
        {
            Label = (label ?? string.Empty).Trim();
            _isSelected = isSelected;
        }

        public string Label { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
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
