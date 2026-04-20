using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services;
using labyItems.Services.Specialisations;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class MultiRaceSpecialisationVm : INotifyPropertyChanged
{
    private readonly CharacterDraft _draft;
    private readonly Dictionary<string, SpecialisationChoiceSet> _choiceSetsByRef = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityDefinition> _abilityRefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MultiRaceDefinition> _multiRaceDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private bool _isRebuilding;

    private string _selectedRaceName = string.Empty;
    private string _selectedLevelsText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Func<Task>? CloseRequested;

    public ObservableCollection<ChoiceSetGroupVm> Groups { get; } = new();

    public ICommand BackCommand { get; }
    public ICommand SaveCommand { get; }

    public string SelectedRaceName
    {
        get => _selectedRaceName;
        private set => Set(ref _selectedRaceName, value);
    }

    public string SelectedLevelsText
    {
        get => _selectedLevelsText;
        private set => Set(ref _selectedLevelsText, value);
    }

    public bool HasGroups => Groups.Count > 0;
    public bool HasNoGroups => !HasGroups;

    public MultiRaceSpecialisationVm(CharacterDraft draft)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _draft.MultiRaceChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        BackCommand = new Command(() => _ = OnBackAsync());
        SaveCommand = new Command(() => _ = OnSaveAsync());
    }

    public async Task LoadAsync()
    {
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        _choiceSetsByRef.Clear();
        _abilityRefs.Clear();
        foreach (var pair in index.AbilityReferences)
            _abilityRefs[pair.Key] = pair.Value;

        foreach (var pair in index.ChoiceSetTemplates)
        {
            var key = (pair.Key ?? string.Empty).Trim();
            if (key.Length == 0)
                continue;

            var set = pair.Value;
            var id = (set.Id ?? string.Empty).Trim();
            if (id.Length > 0)
                _choiceSetsByRef[id] = set;
            _choiceSetsByRef[key] = set;
        }

        var catalog = await MultiRaceService.GetCatalogAsync();
        _multiRaceDefinitions.Clear();
        foreach (var pair in catalog.MultiRaces)
        {
            var key = (pair.Key ?? string.Empty).Trim();
            if (key.Length > 0)
                _multiRaceDefinitions[key] = pair.Value;
        }

        RebuildGroups();
    }

    public IReadOnlyList<MultiClassAbilityLinkVm> GetAbilityDetailsForRow(ChoiceSetAbilityRowVm? row)
    {
        if (row == null || !row.IsValid)
            return Array.Empty<MultiClassAbilityLinkVm>();

        return new[]
        {
            new MultiClassAbilityLinkVm(row.DisplayName, row.LookupKey)
        };
    }

    private void RebuildGroups()
    {
        _isRebuilding = true;
        try
        {
            Groups.Clear();
            SelectedRaceName = string.Empty;
            SelectedLevelsText = string.Empty;

            var raceKey = (_draft.MultiRaceKey ?? string.Empty).Trim();
            if (raceKey.Length == 0 || _draft.MultiRaceLevel <= 0)
            {
                Raise(nameof(HasGroups));
                Raise(nameof(HasNoGroups));
                return;
            }

            if (!TryResolveMultiRaceDefinition(raceKey, out var resolvedKey, out var definition))
            {
                Raise(nameof(HasGroups));
                Raise(nameof(HasNoGroups));
                return;
            }

            var maxLevel = ResolveMaxLevel(definition);
            var selectedLevel = Math.Clamp(_draft.MultiRaceLevel, 0, maxLevel);
            if (selectedLevel <= 0)
            {
                Raise(nameof(HasGroups));
                Raise(nameof(HasNoGroups));
                return;
            }

            SelectedRaceName = !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName.Trim()
                : resolvedKey;
            SelectedLevelsText = $"{selectedLevel}/{maxLevel}";

            _draft.MultiRaceChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var unlocked = ResolveUnlockedChoiceSets(definition, selectedLevel, _draft.MultiRaceChoiceSelections);

            foreach (var item in unlocked)
            {
                if (!_choiceSetsByRef.TryGetValue(item.ChoiceSetRef, out var choiceSet))
                    continue;

                _draft.MultiRaceChoiceSelections.TryGetValue(item.ChoiceSetRef, out var saved);
                var setTitle = (choiceSet.Title ?? string.Empty).Trim();
                if (setTitle.Length == 0)
                    setTitle = item.ChoiceSetRef;
                var group = new ChoiceSetGroupVm(
                    storageKey: item.ChoiceSetRef,
                    choiceSetRef: item.ChoiceSetRef,
                    unlockLevel: item.UnlockLevel,
                    title: setTitle,
                    choiceSet: choiceSet,
                    abilityRefs: _abilityRefs,
                    savedSelection: saved,
                    onSelectionChanged: OnGroupSelectionChanged);

                Groups.Add(group);
            }

            var stale = _draft.MultiRaceChoiceSelections.Keys
                .Where(key => !unlocked.Any(item => item.ChoiceSetRef.Equals(key, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (var key in stale)
                _draft.MultiRaceChoiceSelections.Remove(key);

            Raise(nameof(HasGroups));
            Raise(nameof(HasNoGroups));
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private void OnGroupSelectionChanged(ChoiceSetGroupVm group)
    {
        if (_isRebuilding)
            return;

        _draft.MultiRaceChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(group.SelectedOptionKey))
            _draft.MultiRaceChoiceSelections.Remove(group.ChoiceSetRef);
        else
            _draft.MultiRaceChoiceSelections[group.ChoiceSetRef] = group.SelectedOptionKey;

        if (RequiresGroupRebuild())
        {
            MainThread.BeginInvokeOnMainThread(RebuildGroups);
            return;
        }

        group.RefreshAbilityRows(_abilityRefs);
    }

    private bool RequiresGroupRebuild()
    {
        var raceKey = (_draft.MultiRaceKey ?? string.Empty).Trim();
        if (raceKey.Length == 0 || _draft.MultiRaceLevel <= 0)
            return false;

        if (!TryResolveMultiRaceDefinition(raceKey, out _, out var definition))
            return false;

        var maxLevel = ResolveMaxLevel(definition);
        var selectedLevel = Math.Clamp(_draft.MultiRaceLevel, 0, maxLevel);
        if (selectedLevel <= 0)
            return false;

        var selections = _draft.MultiRaceChoiceSelections ?? EmptySelections;
        var unlockedKeys = ResolveUnlockedChoiceSets(definition, selectedLevel, selections)
            .Select(item => item.ChoiceSetRef)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentKeys = Groups
            .Select(item => item.ChoiceSetRef)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return !unlockedKeys.SetEquals(currentKeys);
    }

    private List<UnlockedChoiceSet> ResolveUnlockedChoiceSets(
        MultiRaceDefinition definition,
        int selectedLevel,
        IReadOnlyDictionary<string, string> savedSelections)
    {
        var grantedAbilityRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unlockedRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unlocked = new List<UnlockedChoiceSet>();

        for (var level = 1; level <= selectedLevel; level++)
        {
            if (!definition.Levels.TryGetValue(level.ToString(), out var abilities) || abilities == null)
                continue;

            foreach (var ability in abilities)
            {
                if (ability == null)
                    continue;

                if (!ArePreReqsSatisfied(ability.PreReqs, grantedAbilityRefs))
                    continue;

                var abilityRef = (ability.AbilityRef ?? string.Empty).Trim();
                if (abilityRef.Length > 0)
                    grantedAbilityRefs.Add(abilityRef);

                foreach (var choiceSetRefRaw in ability.ChoiceSetRefs ?? new List<string>())
                {
                    var choiceSetRef = (choiceSetRefRaw ?? string.Empty).Trim();
                    if (choiceSetRef.Length == 0)
                        continue;

                    if (unlockedRefs.Add(choiceSetRef))
                        unlocked.Add(new UnlockedChoiceSet(choiceSetRef, level));

                    if (!savedSelections.TryGetValue(choiceSetRef, out var savedSelection))
                        continue;

                    if (!_choiceSetsByRef.TryGetValue(choiceSetRef, out var choiceSet))
                        continue;

                    var selectedOption = choiceSet.Options.FirstOrDefault(option =>
                        option.Key.Equals(savedSelection, StringComparison.OrdinalIgnoreCase)
                        || option.Label.Equals(savedSelection, StringComparison.OrdinalIgnoreCase));
                    if (selectedOption == null)
                        continue;

                    foreach (var grantRef in ResolveGrantAbilityRefs(selectedOption))
                        grantedAbilityRefs.Add(grantRef);
                }
            }
        }

        return unlocked;
    }

    private static bool ArePreReqsSatisfied(IEnumerable<string>? preReqs, IReadOnlySet<string> grantedAbilityRefs)
    {
        var list = preReqs?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();
        if (list == null || list.Count == 0)
            return true;

        return list.All(grantedAbilityRefs.Contains);
    }

    private static IEnumerable<string> ResolveGrantAbilityRefs(ChoiceOption option)
    {
        foreach (var grant in option?.Grants ?? Array.Empty<AbilityGrant>())
        {
            var ability = grant?.Ability;
            var key = (ability?.Key ?? string.Empty).Trim();
            if (key.Length > 0)
                yield return key;
        }
    }

    private bool TryResolveMultiRaceDefinition(string key, out string resolvedKey, out MultiRaceDefinition definition)
    {
        resolvedKey = string.Empty;
        definition = null!;

        var trimmed = (key ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return false;

        if (_multiRaceDefinitions.TryGetValue(trimmed, out definition))
        {
            resolvedKey = trimmed;
            return true;
        }

        var normalized = NormalizeToken(trimmed);
        foreach (var pair in _multiRaceDefinitions)
        {
            if (NormalizeToken(pair.Key).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = pair.Key;
                definition = pair.Value;
                return true;
            }

            var display = (pair.Value.DisplayName ?? string.Empty).Trim();
            if (display.Length > 0
                && NormalizeToken(display).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = pair.Key;
                definition = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static int ResolveMaxLevel(MultiRaceDefinition definition)
    {
        if (definition.MaxLevel > 0)
            return definition.MaxLevel;

        var parsed = definition.Levels.Keys
            .Select(level => int.TryParse(level, out var parsedLevel) ? parsedLevel : 0)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(1, parsed);
    }

    private static string NormalizeToken(string? raw)
        => new string((raw ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private async Task OnBackAsync()
    {
        if (CloseRequested != null)
            await CloseRequested.Invoke();
    }

    private async Task OnSaveAsync()
    {
        if (CloseRequested != null)
            await CloseRequested.Invoke();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        Raise(propertyName);
        return true;
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static readonly IReadOnlyDictionary<string, string> EmptySelections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private sealed record UnlockedChoiceSet(string ChoiceSetRef, int UnlockLevel);
}
