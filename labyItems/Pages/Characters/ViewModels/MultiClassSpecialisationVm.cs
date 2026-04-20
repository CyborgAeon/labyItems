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

public sealed class MultiClassSpecialisationVm : INotifyPropertyChanged
{
    private readonly CharacterDraft _draft;
    private readonly string _focusMultiClassKey;
    private readonly Dictionary<string, SpecialisationChoiceSet> _choiceSetsByRef = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityDefinition> _abilityRefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MultiClassDefinition> _multiClassDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private bool _isRebuilding;

    private string _selectedClassName = string.Empty;
    private string _selectedLevelsText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Func<Task>? CloseRequested;

    public ObservableCollection<ChoiceSetGroupVm> Groups { get; } = new();

    public ICommand BackCommand { get; }
    public ICommand SaveCommand { get; }

    public string SelectedClassName
    {
        get => _selectedClassName;
        private set => Set(ref _selectedClassName, value);
    }

    public string SelectedLevelsText
    {
        get => _selectedLevelsText;
        private set => Set(ref _selectedLevelsText, value);
    }

    public bool HasGroups => Groups.Count > 0;
    public bool HasNoGroups => !HasGroups;

    public MultiClassSpecialisationVm(CharacterDraft draft, string? focusMultiClassKey = null)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _focusMultiClassKey = (focusMultiClassKey ?? string.Empty).Trim();
        _draft.MultiClassChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        var catalog = await MultiClassService.GetCatalogAsync();
        _multiClassDefinitions.Clear();
        foreach (var pair in catalog.MultiClasses)
        {
            var key = (pair.Key ?? string.Empty).Trim();
            if (key.Length > 0)
                _multiClassDefinitions[key] = pair.Value;
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
            SelectedClassName = string.Empty;
            SelectedLevelsText = string.Empty;
            _draft.MultiClassLevels ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _draft.MultiClassChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var selectedClasses = ResolveSelectedMultiClasses();
            if (selectedClasses.Count == 0)
            {
                Raise(nameof(HasGroups));
                Raise(nameof(HasNoGroups));
                return;
            }

            if (selectedClasses.Count == 1)
            {
                SelectedClassName = selectedClasses[0].DisplayName;
                SelectedLevelsText = $"{selectedClasses[0].Level}/{selectedClasses[0].MaxLevel}";
            }
            else
            {
                SelectedClassName = "Selected Multi-Classes";
                SelectedLevelsText = $"{selectedClasses.Count} classes";
            }

            var validStorageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var selectedClass in selectedClasses)
            {
                var unlocked = ResolveUnlockedChoiceSets(
                    selectedClass.Definition,
                    selectedClass.Level,
                    selectedClass.ResolvedKey,
                    _draft.MultiClassChoiceSelections);

                foreach (var item in unlocked)
                {
                    if (!_choiceSetsByRef.TryGetValue(item.ChoiceSetRef, out var choiceSet))
                        continue;

                    var storageKey = BuildChoiceSelectionStorageKey(selectedClass.ResolvedKey, item.ChoiceSetRef);
                    validStorageKeys.Add(storageKey);
                    _draft.MultiClassChoiceSelections.TryGetValue(storageKey, out var saved);

                    var setTitle = (choiceSet.Title ?? string.Empty).Trim();
                    if (setTitle.Length == 0)
                        setTitle = item.ChoiceSetRef;
                    var title = selectedClasses.Count == 1
                        ? setTitle
                        : $"{selectedClass.DisplayName}: {setTitle}";

                    var group = new ChoiceSetGroupVm(
                        storageKey: storageKey,
                        choiceSetRef: item.ChoiceSetRef,
                        unlockLevel: item.UnlockLevel,
                        title: title,
                        choiceSet: choiceSet,
                        abilityRefs: _abilityRefs,
                        savedSelection: saved,
                        onSelectionChanged: OnGroupSelectionChanged);
                    Groups.Add(group);
                }
            }

            var focusPrefix = selectedClasses.Count == 1 ? $"{selectedClasses[0].ResolvedKey}::" : string.Empty;
            var staleKeys = _draft.MultiClassChoiceSelections.Keys
                .Where(key =>
                    !validStorageKeys.Contains(key)
                    && (focusPrefix.Length == 0 || key.StartsWith(focusPrefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (var staleKey in staleKeys)
                _draft.MultiClassChoiceSelections.Remove(staleKey);

            Raise(nameof(HasGroups));
            Raise(nameof(HasNoGroups));
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private List<SelectedMultiClassVm> ResolveSelectedMultiClasses()
    {
        var selected = new Dictionary<string, SelectedMultiClassVm>(StringComparer.OrdinalIgnoreCase);
        var focusNormalized = NormalizeToken(_focusMultiClassKey);

        foreach (var pair in _draft.MultiClassLevels)
        {
            var rawKey = (pair.Key ?? string.Empty).Trim();
            if (rawKey.Length == 0 || pair.Value <= 0)
                continue;

            if (!TryResolveMultiClassDefinition(rawKey, out var resolvedKey, out var definition))
                continue;

            var displayName = ResolveMultiClassDisplayName(definition, resolvedKey);
            if (focusNormalized.Length > 0)
            {
                var resolvedNormalized = NormalizeToken(resolvedKey);
                var displayNormalized = NormalizeToken(displayName);
                if (!focusNormalized.Equals(resolvedNormalized, StringComparison.OrdinalIgnoreCase)
                    && !focusNormalized.Equals(displayNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var maxLevel = ResolveMaxLevel(definition);
            var level = Math.Clamp(pair.Value, 0, maxLevel);
            if (level <= 0)
                continue;

            if (selected.TryGetValue(resolvedKey, out var existing) && existing.Level >= level)
                continue;

            selected[resolvedKey] = new SelectedMultiClassVm(
                resolvedKey,
                displayName,
                definition,
                level,
                maxLevel);
        }

        return selected.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void OnGroupSelectionChanged(ChoiceSetGroupVm group)
    {
        if (_isRebuilding)
            return;

        _draft.MultiClassChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(group.SelectedOptionKey))
            _draft.MultiClassChoiceSelections.Remove(group.StorageKey);
        else
            _draft.MultiClassChoiceSelections[group.StorageKey] = group.SelectedOptionKey;

        if (RequiresGroupRebuild())
        {
            MainThread.BeginInvokeOnMainThread(RebuildGroups);
            return;
        }

        group.RefreshAbilityRows(_abilityRefs);
    }

    private bool RequiresGroupRebuild()
    {
        var selectedClasses = ResolveSelectedMultiClasses();
        if (selectedClasses.Count == 0)
            return false;

        var selections = _draft.MultiClassChoiceSelections ?? EmptySelections;
        var unlockedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selectedClass in selectedClasses)
        {
            foreach (var unlocked in ResolveUnlockedChoiceSets(
                         selectedClass.Definition,
                         selectedClass.Level,
                         selectedClass.ResolvedKey,
                         selections))
            {
                unlockedKeys.Add(BuildChoiceSelectionStorageKey(selectedClass.ResolvedKey, unlocked.ChoiceSetRef));
            }
        }

        var currentKeys = Groups
            .Select(item => item.StorageKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return !unlockedKeys.SetEquals(currentKeys);
    }

    private List<UnlockedChoiceSet> ResolveUnlockedChoiceSets(
        MultiClassDefinition definition,
        int selectedLevel,
        string multiClassKey,
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

                    var storageKey = BuildChoiceSelectionStorageKey(multiClassKey, choiceSetRef);
                    if (!savedSelections.TryGetValue(storageKey, out var savedSelection))
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

    private bool TryResolveMultiClassDefinition(string key, out string resolvedKey, out MultiClassDefinition definition)
    {
        resolvedKey = string.Empty;
        definition = null!;

        var trimmed = (key ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return false;

        if (_multiClassDefinitions.TryGetValue(trimmed, out definition))
        {
            resolvedKey = trimmed;
            return true;
        }

        var normalized = NormalizeToken(trimmed);
        foreach (var pair in _multiClassDefinitions)
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

    private static int ResolveMaxLevel(MultiClassDefinition definition)
    {
        if (definition.MaxLevel > 0)
            return definition.MaxLevel;

        var parsed = definition.Levels.Keys
            .Select(level => int.TryParse(level, out var parsedLevel) ? parsedLevel : 0)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(1, parsed);
    }

    private static string ResolveMultiClassDisplayName(MultiClassDefinition definition, string fallbackKey)
    {
        var name = (definition.DisplayName ?? string.Empty).Trim();
        return name.Length > 0 ? name : fallbackKey;
    }

    private static string BuildChoiceSelectionStorageKey(string multiClassKey, string choiceSetRef)
        => $"{(multiClassKey ?? string.Empty).Trim()}::{(choiceSetRef ?? string.Empty).Trim()}";

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
    private sealed record SelectedMultiClassVm(
        string ResolvedKey,
        string DisplayName,
        MultiClassDefinition Definition,
        int Level,
        int MaxLevel);
}
