using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class AdvanceAbilitySpecialisationVm : INotifyPropertyChanged
{
    private readonly CharacterDraft _draft;
    private readonly IReadOnlyList<AdvanceAbilitySpecialisationRequest> _requests;
    private readonly Dictionary<string, SpecialisationChoiceSet> _choiceSetsByRef = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AbilityDefinition> _abilityRefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _trackedStorageKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _originalSelections = new(StringComparer.OrdinalIgnoreCase);
    private bool _changesCommitted;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Func<bool, Task>? CloseRequested;

    public ObservableCollection<AdvanceAbilitySpecialisationAbilityVm> AbilityCards { get; } = new();

    public ICommand BackCommand { get; }
    public ICommand SaveCommand { get; }

    public bool HasCards => AbilityCards.Count > 0;
    public bool HasNoCards => !HasCards;

    public AdvanceAbilitySpecialisationVm(
        CharacterDraft draft,
        IReadOnlyList<AdvanceAbilitySpecialisationRequest> requests)
    {
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _requests = requests ?? Array.Empty<AdvanceAbilitySpecialisationRequest>();
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

        RebuildCards();
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

    public void RevertUnsavedSelections()
    {
        if (_changesCommitted)
            return;

        foreach (var storageKey in _trackedStorageKeys)
        {
            if (_originalSelections.TryGetValue(storageKey, out var original) && !string.IsNullOrWhiteSpace(original))
                _draft.SpecialisationSelections[storageKey] = original;
            else
                _draft.SpecialisationSelections.Remove(storageKey);
        }
    }

    private void RebuildCards()
    {
        AbilityCards.Clear();
        _trackedStorageKeys.Clear();
        _originalSelections.Clear();

        foreach (var request in _requests)
        {
            var abilityName = (request.AbilityName ?? string.Empty).Trim();
            if (abilityName.Length == 0)
                abilityName = (request.AbilityKey ?? string.Empty).Trim();
            if (abilityName.Length == 0)
                abilityName = "Ability";

            var card = new AdvanceAbilitySpecialisationAbilityVm(
                request.AbilityKey,
                abilityName,
                Math.Max(1, request.Occurrence));

            foreach (var rawChoiceSetRef in request.ChoiceSetRefs ?? Array.Empty<string>())
            {
                var choiceSetRef = (rawChoiceSetRef ?? string.Empty).Trim();
                if (choiceSetRef.Length == 0)
                    continue;

                if (!_choiceSetsByRef.TryGetValue(choiceSetRef, out var choiceSet))
                    continue;

                var storageKey = request.BuildStorageKey(choiceSetRef);
                _trackedStorageKeys.Add(storageKey);
                if (!_originalSelections.ContainsKey(storageKey))
                {
                    _originalSelections[storageKey] = _draft.SpecialisationSelections.TryGetValue(storageKey, out var existing)
                        ? existing
                        : null;
                }

                _draft.SpecialisationSelections.TryGetValue(storageKey, out var savedSelection);
                var setTitle = (choiceSet.Title ?? string.Empty).Trim();
                if (setTitle.Length == 0)
                    setTitle = choiceSetRef;

                var group = new ChoiceSetGroupVm(
                    storageKey: storageKey,
                    choiceSetRef: choiceSetRef,
                    unlockLevel: 1,
                    title: setTitle,
                    choiceSet: choiceSet,
                    abilityRefs: _abilityRefs,
                    savedSelection: savedSelection,
                    onSelectionChanged: OnGroupSelectionChanged);

                card.Groups.Add(group);
            }

            if (card.Groups.Count > 0)
                AbilityCards.Add(card);
        }

        Raise(nameof(HasCards));
        Raise(nameof(HasNoCards));
    }

    private void OnGroupSelectionChanged(ChoiceSetGroupVm group)
    {
        if (string.IsNullOrWhiteSpace(group.SelectedOptionKey))
            _draft.SpecialisationSelections.Remove(group.StorageKey);
        else
            _draft.SpecialisationSelections[group.StorageKey] = group.SelectedOptionKey;
    }

    private async Task OnBackAsync()
    {
        RevertUnsavedSelections();
        if (CloseRequested != null)
            await CloseRequested.Invoke(false);
    }

    private async Task OnSaveAsync()
    {
        _changesCommitted = true;
        if (CloseRequested != null)
            await CloseRequested.Invoke(true);
    }

    private void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class AdvanceAbilitySpecialisationAbilityVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AdvanceAbilitySpecialisationAbilityVm(string abilityKey, string abilityName, int occurrence)
    {
        AbilityKey = (abilityKey ?? string.Empty).Trim();
        AbilityName = (abilityName ?? string.Empty).Trim();
        Occurrence = Math.Max(1, occurrence);
    }

    public string AbilityKey { get; }
    public string AbilityName { get; }
    public int Occurrence { get; }
    public string Subtitle => Occurrence > 1 ? $"Selection {Occurrence}" : "Selection";
    public ObservableCollection<ChoiceSetGroupVm> Groups { get; } = new();
}
