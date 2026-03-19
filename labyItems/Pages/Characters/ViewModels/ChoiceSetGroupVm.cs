using System.Collections.ObjectModel;
using System.ComponentModel;
using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Pages.Characters.ViewModels;

public sealed class ChoiceSetGroupVm : INotifyPropertyChanged
{
    private readonly Dictionary<string, ChoiceOption> _optionsByLabel = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<ChoiceSetGroupVm> _onSelectionChanged;
    private string _selectedOptionDisplay = string.Empty;
    private string _selectedOptionKey = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChoiceSetGroupVm(
        string storageKey,
        string choiceSetRef,
        int unlockLevel,
        string title,
        SpecialisationChoiceSet choiceSet,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs,
        string? savedSelection,
        Action<ChoiceSetGroupVm> onSelectionChanged)
    {
        StorageKey = storageKey;
        ChoiceSetRef = choiceSetRef;
        UnlockLevel = unlockLevel;
        Title = title;
        _onSelectionChanged = onSelectionChanged;
        foreach (var option in choiceSet.Options ?? Array.Empty<ChoiceOption>())
        {
            var label = (option.Label ?? option.Key ?? string.Empty).Trim();
            if (label.Length == 0)
                continue;

            if (_optionsByLabel.ContainsKey(label))
                continue;

            _optionsByLabel[label] = option;
            Options.Add(label);
            SearchOptions[label] = label;
        }

        var initial = ResolveInitialSelectionLabel(savedSelection);
        _selectedOptionDisplay = initial;
        _selectedOptionKey = ResolveSelectedOptionKey(initial);
        RefreshAbilityRows(abilityRefs);
    }

    public string StorageKey { get; }
    public string ChoiceSetRef { get; }
    public int UnlockLevel { get; }
    public string UnlockLevelText => $"Level {UnlockLevel}";
    public string Title { get; }
    public ObservableCollection<string> Options { get; } = new();
    public Dictionary<string, string> SearchOptions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ObservableCollection<ChoiceSetAbilityRowVm> AbilityRows { get; } = new();
    public bool HasAbilityRows => AbilityRows.Count > 0;

    public string SelectedOptionDisplay
    {
        get => _selectedOptionDisplay;
        set
        {
            var next = (value ?? string.Empty).Trim();
            if (_selectedOptionDisplay.Equals(next, StringComparison.OrdinalIgnoreCase))
                return;

            _selectedOptionDisplay = next;
            _selectedOptionKey = ResolveSelectedOptionKey(next);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOptionDisplay)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOptionKey)));
            _onSelectionChanged(this);
        }
    }

    public string SelectedOptionKey => _selectedOptionKey;

    public void RefreshAbilityRows(IReadOnlyDictionary<string, AbilityDefinition> abilityRefs)
    {
        AbilityRows.Clear();

        if (!_optionsByLabel.TryGetValue(_selectedOptionDisplay, out var option))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAbilityRows)));
            return;
        }

        foreach (var grant in option.Grants ?? Array.Empty<AbilityGrant>())
        {
            var ability = grant?.Ability;
            var lookup = (ability?.Key ?? string.Empty).Trim();
            if (lookup.Length == 0)
                continue;

            var displayName = (ability?.Name ?? string.Empty).Trim();
            if (displayName.Length == 0
                && abilityRefs.TryGetValue(lookup, out var definition)
                && !string.IsNullOrWhiteSpace(definition.Name))
            {
                displayName = definition.Name.Trim();
            }

            if (displayName.Length == 0)
                displayName = lookup;

            AbilityRows.Add(new ChoiceSetAbilityRowVm(displayName, lookup));
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasAbilityRows)));
    }

    private string ResolveInitialSelectionLabel(string? savedSelection)
    {
        var saved = (savedSelection ?? string.Empty).Trim();
        if (saved.Length == 0)
            return string.Empty;

        foreach (var pair in _optionsByLabel)
        {
            if (pair.Key.Equals(saved, StringComparison.OrdinalIgnoreCase)
                || pair.Value.Key.Equals(saved, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return string.Empty;
    }

    private string ResolveSelectedOptionKey(string selectedLabel)
    {
        if (_optionsByLabel.TryGetValue(selectedLabel, out var option))
            return (option.Key ?? option.Label ?? string.Empty).Trim();

        return string.Empty;
    }
}

public sealed record ChoiceSetAbilityRowVm(string DisplayName, string LookupKey)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName) || !string.IsNullOrWhiteSpace(LookupKey);
}
