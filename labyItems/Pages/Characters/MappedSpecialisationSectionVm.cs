using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Pages.Characters;

public sealed class MappedSpecialisationSectionVm : ISpecialisationSectionVm
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private readonly Action _onChanged;
    private readonly Dictionary<string, ChoiceOption> _optionMap;
    private readonly bool _required;
    private readonly Func<ChoiceOption?, string>? _selectionIssueResolver;
    private bool _suppressNotify;

    public string SectionId { get; }
    public string Key { get; }
    public string DetailKey { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public SpecialisationSectionType SectionType { get; }

    public ObservableCollection<string> Options { get; } = new();
    public ObservableCollection<SpecialisationAbilityRow> AbilityRows { get; } = new();

    private string? _selectedOption;
    public string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (!Set(ref _selectedOption, normalized)) return;

            UpdatePreview();
            RefreshIssue();
            RaiseComputed();

            if (!_suppressNotify)
                _onChanged();
        }
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    private bool _levelsExpanded;
    public bool LevelsExpanded
    {
        get => _levelsExpanded;
        set => Set(ref _levelsExpanded, value);
    }

    public ICommand ToggleExpandedCommand { get; }

    public bool HasSelection => !string.IsNullOrWhiteSpace(_selectedOption);
    public bool HasIssue => !string.IsNullOrWhiteSpace(IssueMessage);
    public bool IsComplete => (!_required || HasSelection) && !HasIssue;
    public string StatusText => HasIssue ? "Issue" : (HasSelection ? "Selected" : (_required ? "Required" : "Optional"));
    public string CardState => HasIssue ? "Issue" : (HasSelection ? "Success" : (_required ? "Error" : "Neutral"));
    public string CardStateText => CardState;
    public string DisplaySubtitle => HasIssue ? IssueMessage : Subtitle;

    private string _issueMessage = string.Empty;
    public string IssueMessage
    {
        get => _issueMessage;
        private set => Set(ref _issueMessage, value);
    }

    public MappedSpecialisationSectionVm(
        string sectionId,
        string key,
        string detailKey,
        string title,
        string subtitle,
        IEnumerable<ChoiceOption> options,
        string? initialSelection,
        bool required,
        Action onSelectionChanged,
        SpecialisationSectionType sectionType,
        Func<ChoiceOption?, string>? selectionIssueResolver = null)
    {
        SectionId = sectionId;
        Key = key;
        DetailKey = detailKey;
        Title = title;
        Subtitle = (subtitle ?? string.Empty).Trim();
        _onChanged = onSelectionChanged;
        _required = required;
        _selectionIssueResolver = selectionIssueResolver;
        SectionType = sectionType;

        _optionMap = (options ?? Array.Empty<ChoiceOption>())
            .Where(o => o != null && !string.IsNullOrWhiteSpace(o.Key))
            .ToDictionary(o => o.Key, o => o, StringComparer.OrdinalIgnoreCase);

        ToggleExpandedCommand = new Command(() => IsExpanded = !IsExpanded);

        foreach (var name in _optionMap.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            Options.Add(name);

        _suppressNotify = true;
        var initial = (initialSelection ?? string.Empty).Trim();
        if (initial.Length > 0)
        {
            var match = Options.FirstOrDefault(o => string.Equals(o, initial, StringComparison.OrdinalIgnoreCase));
            SelectedOption = match ?? null;
        }
        else
        {
            SelectedOption = null;
        }
        _suppressNotify = false;

        UpdatePreview();
        RefreshIssue();
        RaiseComputed();
    }

    public void RefreshIssue()
    {
        ChoiceOption? selected = null;
        if (!string.IsNullOrWhiteSpace(_selectedOption)
            && _optionMap.TryGetValue(_selectedOption, out var option)
            && option != null)
        {
            selected = option;
        }

        IssueMessage = (_selectionIssueResolver?.Invoke(selected) ?? string.Empty).Trim();
        RaiseComputed();
    }

    public IEnumerable<(int? Level, string Ability, AbilityDefinition? AbilityDef)> GetSelectedAbilities()
    {
        if (string.IsNullOrWhiteSpace(_selectedOption))
            yield break;

        if (!_optionMap.TryGetValue(_selectedOption, out var entry) || entry == null)
            yield break;

        foreach (var grant in entry.Grants ?? Array.Empty<AbilityGrant>())
        {
            var abilityName = (grant.Ability?.Name ?? string.Empty).Trim();
            if (abilityName.Length == 0)
                continue;

            yield return (grant.Level, abilityName, grant.Ability);
        }
    }

    private void UpdatePreview()
    {
        AbilityRows.Clear();
        LevelsExpanded = false;

        if (string.IsNullOrWhiteSpace(_selectedOption) || !_optionMap.TryGetValue(_selectedOption, out var entry))
            return;

        var ordered = (entry.Grants ?? Array.Empty<AbilityGrant>())
            .Where(g => g != null && !string.IsNullOrWhiteSpace(g.Ability?.Name))
            .OrderBy(g => g.Level ?? int.MaxValue)
            .ThenBy(g => g.Ability.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var grant in ordered)
        {
            AbilityRows.Add(new SpecialisationAbilityRow
            {
                Level = grant.Level,
                Ability = grant.Ability.Name,
                AbilityKey = grant.Ability.Key ?? string.Empty,
                SpecialisationKey = DetailKey,
                SelectedOption = _selectedOption ?? string.Empty,
                SelectedAbility = grant.Ability.Name
            });
        }

        LevelsExpanded = AbilityRows.Count > 0;
    }

    private void RaiseComputed()
    {
        Raise(nameof(HasSelection));
        Raise(nameof(HasIssue));
        Raise(nameof(IssueMessage));
        Raise(nameof(IsComplete));
        Raise(nameof(StatusText));
        Raise(nameof(CardState));
        Raise(nameof(CardStateText));
        Raise(nameof(DisplaySubtitle));
    }
}

public sealed class SpecialisationAbilityRow
{
    public int? Level { get; init; }
    public string LevelText => Level.HasValue ? $"Lvl {Level.Value}" : string.Empty;
    public string Ability { get; init; } = string.Empty;
    public string AbilityKey { get; init; } = string.Empty;
    public string SpecialisationKey { get; init; } = string.Empty;
    public string SelectedOption { get; init; } = string.Empty;
    public string SelectedAbility { get; init; } = string.Empty;
}
