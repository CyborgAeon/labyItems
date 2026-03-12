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
    private readonly Dictionary<string, string> _optionDisplayByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _optionKeyByDisplay = new(StringComparer.OrdinalIgnoreCase);
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
            var resolvedKey = ResolveOptionKey(value);
            if (resolvedKey.Length > 0 && !_optionMap.ContainsKey(resolvedKey))
                resolvedKey = string.Empty;

            if (!Set(ref _selectedOption, resolvedKey))
                return;

            UpdatePreview();
            RefreshIssue();
            RaiseComputed();

            if (!_suppressNotify)
                _onChanged();
        }
    }

    public string? SelectedOptionDisplay
    {
        get => ResolveDisplayLabel(_selectedOption);
        set => SelectedOption = value;
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
    public bool UseSingleOptionToggle => !_required && Options.Count == 1;
    public string SingleOptionLabel => Options.Count == 1 ? Options[0] : string.Empty;
    public bool SingleOptionEnabled
    {
        get => HasSelection;
        set
        {
            if (!UseSingleOptionToggle)
                return;

            var target = value ? SingleOptionLabel : string.Empty;
            if (string.IsNullOrWhiteSpace(target))
                target = string.Empty;

            SelectedOption = target;
        }
    }
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

        _optionMap = new Dictionary<string, ChoiceOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in options ?? Array.Empty<ChoiceOption>())
        {
            if (option == null)
                continue;

            var optionKey = ResolveCanonicalKey(option.Key, option.Label);
            if (optionKey.Length == 0 || _optionMap.ContainsKey(optionKey))
                continue;

            _optionMap[optionKey] = option;

            var label = (option.Label ?? string.Empty).Trim();
            if (label.Length == 0)
                label = optionKey;

            _optionDisplayByKey[optionKey] = label;
            if (!_optionKeyByDisplay.ContainsKey(label))
                _optionKeyByDisplay[label] = optionKey;
        }

        ToggleExpandedCommand = new Command(() => IsExpanded = !IsExpanded);

        foreach (var label in _optionDisplayByKey.Values.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            Options.Add(label);

        _suppressNotify = true;
        var initialKey = ResolveOptionKey(initialSelection);
        SelectedOption = initialKey.Length > 0 ? initialKey : null;
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
                SelectedOption = ResolveDisplayLabel(_selectedOption),
                SelectedAbility = grant.Ability.Name
            });
        }

        LevelsExpanded = AbilityRows.Count > 0;
    }

    private void RaiseComputed()
    {
        Raise(nameof(HasSelection));
        Raise(nameof(SelectedOptionDisplay));
        Raise(nameof(UseSingleOptionToggle));
        Raise(nameof(SingleOptionLabel));
        Raise(nameof(SingleOptionEnabled));
        Raise(nameof(HasIssue));
        Raise(nameof(IssueMessage));
        Raise(nameof(IsComplete));
        Raise(nameof(StatusText));
        Raise(nameof(CardState));
        Raise(nameof(CardStateText));
        Raise(nameof(DisplaySubtitle));
    }

    private static string ResolveCanonicalKey(string? key, string? label)
    {
        var canonical = (key ?? string.Empty).Trim();
        if (canonical.Length > 0)
            return canonical;

        return (label ?? string.Empty).Trim();
    }

    private string ResolveOptionKey(string? selectedToken)
    {
        var token = (selectedToken ?? string.Empty).Trim();
        if (token.Length == 0)
            return string.Empty;

        if (_optionMap.ContainsKey(token))
            return token;

        if (_optionKeyByDisplay.TryGetValue(token, out var displayMatch))
            return displayMatch;

        var keyMatch = _optionMap.Keys.FirstOrDefault(key =>
            string.Equals(key, token, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(keyMatch))
            return keyMatch;

        var labelMatch = _optionDisplayByKey.FirstOrDefault(pair =>
            string.Equals(pair.Value, token, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(labelMatch.Key) ? labelMatch.Key : token;
    }

    private string ResolveDisplayLabel(string? key)
    {
        var token = (key ?? string.Empty).Trim();
        if (token.Length == 0)
            return string.Empty;

        if (_optionDisplayByKey.TryGetValue(token, out var label))
            return label;

        return token;
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
