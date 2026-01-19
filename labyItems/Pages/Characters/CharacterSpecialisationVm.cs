using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using labyItems.Controls.Pickers;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public sealed class CharacterSpecialisationVm : INotifyPropertyChanged
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

    private readonly CharacterBuilderVm _builderVm;

    public ObservableCollection<SpecialisationGroupVm> Groups { get; } = new();

    private string _headerText = "";
    public string HeaderText
    {
        get => _headerText;
        private set => Set(ref _headerText, value);
    }

    private bool _hasChoices;
    public bool HasChoices
    {
        get => _hasChoices;
        private set
        {
            if (!Set(ref _hasChoices, value)) return;
            Raise(nameof(HasNoChoices));
        }
    }

    public bool HasNoChoices => !HasChoices;

    private bool _isCompleteForNavigation = true;
    public bool IsCompleteForNavigation
    {
        get => _isCompleteForNavigation;
        private set => Set(ref _isCompleteForNavigation, value);
    }

    public CharacterSpecialisationVm(CharacterBuilderVm builderVm)
    {
        _builderVm = builderVm;
        HeaderText = "Select any specialist skills granted by your race and/or class.";
        HasChoices = false;
        IsCompleteForNavigation = true;
    }

    public async Task ReloadAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Groups.Clear();
            HasChoices = false;
            IsCompleteForNavigation = true;
        });

        var draft = _builderVm.Draft;
        var className = (draft.Class ?? "").Trim();
        var raceName = (draft.Race ?? "").Trim();

        if (string.IsNullOrWhiteSpace(className) && string.IsNullOrWhiteSpace(raceName))
        {
            HeaderText = "Select a race and class first.";
            PersistToDraft(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            _builderVm.NotifyGatingChanged();
            return;
        }

        var specs = await SpecialisationService.GetAllAsync();
        var specKeys = specs.Keys.ToList();

        var requirementLevels = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(className))
            await AddRequirementLevelsFromClassAsync(className, specKeys, requirementLevels);

        if (!string.IsNullOrWhiteSpace(raceName))
            await AddRequirementLevelsFromRaceAsync(raceName, specKeys, requirementLevels);

        if (requirementLevels.Count == 0)
        {
            HeaderText = "No specialisation choices required for the selected race/class.";
            HasChoices = false;
            IsCompleteForNavigation = true;
            PersistToDraft(new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));
            _builderVm.NotifyGatingChanged();
            return;
        }

        var groupedSelections = ReadFromDraft();

        var selectionsByLevel = ReadFromDraftByLevel();

        var groupVms = new List<SpecialisationGroupVm>();
        foreach (var req in requirementLevels.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!specs.TryGetValue(req.Key, out var spec)) continue;

            var options = (spec.Abilities ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var initial = selectionsByLevel.TryGetValue(req.Key, out var byLvl)
                ? byLvl
                : new Dictionary<int, string>();
            var isWardPact = string.Equals(req.Key, "Ward pact", StringComparison.OrdinalIgnoreCase);
            var groupVm = new SpecialisationGroupVm(
                title: req.Key,
                levels: req.Value,
                optionNames: options,
                initiallySelectedByLevel: initial,
                onAnySelectionChanged: OnAnySelectionChanged,
                useWardPactEnum: isWardPact);


            groupVms.Add(groupVm);
        }


        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            foreach (var g in groupVms)
                Groups.Add(g);

            HasChoices = Groups.Count > 0;
            HeaderText = "Select the specialist skills granted by your race and/or class.";
            RecomputeCompletionAndPersist();
        });

        _builderVm.NotifyGatingChanged();
    }

    private Dictionary<string, Dictionary<int, string>> ReadFromDraftByLevel()
    {
        var draft = _builderVm.Draft;
        var t = draft.GetType();

        var prop = t.GetProperty("SpecialisationsByLevel")
                   ?? t.GetProperty("SpecializationsByLevel");

        if (prop?.GetValue(draft) is Dictionary<string, Dictionary<int, string>> d)
            return new Dictionary<string, Dictionary<int, string>>(d, StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
    }

    private void PersistToDraftByLevel(Dictionary<string, Dictionary<int, string>> groupedByLevel)
    {
        var draft = _builderVm.Draft;
        var t = draft.GetType();

        var byLevelProp = t.GetProperty("SpecialisationsByLevel")
                         ?? t.GetProperty("SpecializationsByLevel");

        if (byLevelProp != null && byLevelProp.CanWrite &&
            byLevelProp.PropertyType == typeof(Dictionary<string, Dictionary<int, string>>))
            byLevelProp.SetValue(draft, groupedByLevel);

        var simple = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in groupedByLevel)
            simple[kvp.Key] = kvp.Value.OrderBy(x => x.Key).Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        var simpleProp = t.GetProperty("Specialisations")
                       ?? t.GetProperty("Specializations");

        if (simpleProp != null && simpleProp.CanWrite &&
            simpleProp.PropertyType == typeof(Dictionary<string, List<string>>))
            simpleProp.SetValue(draft, simple);

        var flat = simple.Values.SelectMany(x => x).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var flatProp = t.GetProperty("SelectedAbilities")
                    ?? t.GetProperty("Abilities")
                    ?? t.GetProperty("ChosenAbilities");

        if (flatProp != null && flatProp.CanWrite && flatProp.PropertyType == typeof(List<string>))
            flatProp.SetValue(draft, flat);
    }

    private async Task AddRequirementLevelsFromClassAsync(
        string className,
        List<string> specKeys,
        Dictionary<string, List<int>> requirementLevels)
    {
        var all = await ClassService.GetAllAsync();
        if (!all.TryGetValue(className, out var record) || record == null) return;

        var bracket = record.Brackets?.FirstOrDefault() ?? "";

        for (var lvl = 1; lvl <= 8; lvl++)
        {
            if (record.Levels == null) continue;

            var key = lvl.ToString();
            if (!record.Levels.TryGetValue(key, out var abilities) || abilities == null) continue;

            foreach (var a in abilities)
            {
                var specKey = ResolveSpecialisationKey(a, className, bracket, specKeys);
                if (specKey == null) continue;

                if (!requirementLevels.TryGetValue(specKey, out var list))
                {
                    list = new List<int>();
                    requirementLevels[specKey] = list;
                }

                list.Add(lvl);
            }
        }
    }

    private async Task AddRequirementLevelsFromRaceAsync(
        string raceName,
        List<string> specKeys,
        Dictionary<string, List<int>> requirementLevels)
    {
        var all = await PeopleService.GetAllAsync();
        if (!all.TryGetValue(raceName, out var record) || record == null) return;

        var levels = record.LevelledAbilities;
        if (levels == null) return;

        foreach (var kvp in levels)
        {
            if (!int.TryParse(kvp.Key, out var lvl)) continue;
            if (kvp.Value == null) continue;

            foreach (var a in kvp.Value)
            {
                var specKey = ResolveSpecialisationKey(a, raceName, "", specKeys);
                if (specKey == null) continue;

                if (!requirementLevels.TryGetValue(specKey, out var list))
                {
                    list = new List<int>();
                    requirementLevels[specKey] = list;
                }

                list.Add(lvl);
            }
        }
    }

    private static string? ResolveSpecialisationKey(
        string? token,
        string ownerName,
        string bracket,
        List<string> specKeys)
    {
        var t = (token ?? "").Trim();
        if (t.Length == 0) return null;

        var direct = specKeys.FirstOrDefault(k => string.Equals(k, t, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return direct;

        if (string.Equals(t, "Scout Skill", StringComparison.OrdinalIgnoreCase))
            return specKeys.FirstOrDefault(k => string.Equals(k, "Standard Scout skill", StringComparison.OrdinalIgnoreCase));

        if (string.Equals(t, "Scout Specialist Skill", StringComparison.OrdinalIgnoreCase))
            return specKeys.FirstOrDefault(k => string.Equals(k, "Specialist Scout skill", StringComparison.OrdinalIgnoreCase));

        if (string.Equals(t, "Warrior specialist", StringComparison.OrdinalIgnoreCase))
            return specKeys.FirstOrDefault(k => string.Equals(k, "Warrior-Priest specialist", StringComparison.OrdinalIgnoreCase))
                   ?? specKeys.FirstOrDefault(k => string.Equals(k, "Warrior Specialist", StringComparison.OrdinalIgnoreCase));

        if (t.IndexOf("specialist", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var ownerCandidate = specKeys.FirstOrDefault(k =>
                Canon(k) == Canon(ownerName + " Specialist") ||
                Canon(k) == Canon(ownerName + " specialist"));

            if (ownerCandidate != null) return ownerCandidate;

            if (string.Equals(t, "Specialist Skill", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "Specialist skill", StringComparison.OrdinalIgnoreCase))
            {
                if (bracket.IndexOf("Warrior", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return specKeys.FirstOrDefault(k => string.Equals(k, "Warrior Specialist", StringComparison.OrdinalIgnoreCase))
                           ?? specKeys.FirstOrDefault(k => k.IndexOf("Warrior", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                                          k.IndexOf("Specialist", StringComparison.OrdinalIgnoreCase) >= 0);
                }

                var any = specKeys.FirstOrDefault(k => k.IndexOf("Specialist", StringComparison.OrdinalIgnoreCase) >= 0);
                return any;
            }

            var close = specKeys.FirstOrDefault(k => Canon(k) == Canon(t));
            if (close != null) return close;

            var contains = specKeys.FirstOrDefault(k =>
                k.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);

            return contains;
        }

        return null;
    }

    private static string Canon(string s)
    {
        var chars = s
            .Trim()
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray();
        return new string(chars);
    }

    private void OnAnySelectionChanged()
    {
        RecomputeCompletionAndPersist();
        _builderVm.NotifyGatingChanged();
    }
    private void RecomputeCompletionAndPersist()
    {
        var anyRequired = Groups.Count > 0;
        var complete = Groups.All(g => g.IsComplete);
        IsCompleteForNavigation = !anyRequired || complete;

        var byLevel = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var g in Groups)
        {
            var d = new Dictionary<int, string>();
            foreach (var s in g.Slots)
            {
                var chosen = (s.SelectedOption ?? "").Trim();
                if (chosen.Length > 0)
                    d[s.Level] = chosen;
            }

            byLevel[g.Title] = d;
        }

        PersistToDraftByLevel(byLevel);
    }

    private Dictionary<string, List<string>> ReadFromDraft()
    {
        var draft = _builderVm.Draft;
        var t = draft.GetType();

        var prop = t.GetProperty("Specialisations", BindingFlags.Instance | BindingFlags.Public)
                   ?? t.GetProperty("Specializations", BindingFlags.Instance | BindingFlags.Public);

        if (prop == null) return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var val = prop.GetValue(draft);
        if (val is Dictionary<string, List<string>> dict)
            return new Dictionary<string, List<string>>(dict, StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    }

    private void PersistToDraft(Dictionary<string, List<string>> grouped)
    {
        var draft = _builderVm.Draft;
        var t = draft.GetType();

        var prop = t.GetProperty("Specialisations", BindingFlags.Instance | BindingFlags.Public)
                   ?? t.GetProperty("Specializations", BindingFlags.Instance | BindingFlags.Public);

        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(Dictionary<string, List<string>>))
            prop.SetValue(draft, grouped);

        var flat = grouped.Values.SelectMany(x => x).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var flatProp = t.GetProperty("SelectedAbilities", BindingFlags.Instance | BindingFlags.Public)
                      ?? t.GetProperty("Abilities", BindingFlags.Instance | BindingFlags.Public)
                      ?? t.GetProperty("ChosenAbilities", BindingFlags.Instance | BindingFlags.Public);

        if (flatProp != null && flatProp.CanWrite && flatProp.PropertyType == typeof(List<string>))
            flatProp.SetValue(draft, flat);
    }
}

public sealed class SpecialisationGroupVm : INotifyPropertyChanged
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

    private readonly Action _onAnySelectionChanged;

    public string Title { get; }
    public int RequiredCount => Slots.Count;

    public ObservableCollection<SpecialisationSlotVm> Slots { get; } = new();

    private List<string> _allOptionNames;
    private List<string> _filteredOptionNames;

    public IReadOnlyList<string> FilteredOptionNames => _filteredOptionNames;

    private string _filterText = "";
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (UseWardPactEnum) return;
            if (!Set(ref _filterText, value)) return;
            ApplyFilter();
        }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public Command ToggleExpandedCommand { get; }

    public string Subtitle => RequiredCount == 1 ? "Pick 1 ability" : $"Pick {RequiredCount} abilities";

    public int SelectedCount => Slots.Count(s => !string.IsNullOrWhiteSpace(s.SelectedOption));

    public bool HasDuplicates
    {
        get
        {
            var picked = Slots
                .Select(s => (s.SelectedOption ?? "").Trim())
                .Where(x => x.Length > 0)
                .ToList();

            return picked.Count != picked.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }
    }

    public bool IsComplete => SelectedCount == RequiredCount && !HasDuplicates;

    public string StatusText => $"{SelectedCount}/{RequiredCount}";

    public string HelperText
    {
        get
        {
            if (SelectedCount == 0) return "Make your selections below.";
            if (HasDuplicates) return "Duplicate selections detected. Choose different abilities for each level.";
            if (IsComplete) return "Selection complete.";
            return "Continue selecting until all levels are filled.";
        }
    }

    public SpecialisationCardState CardState
    {
        get
        {
            if (IsComplete) return SpecialisationCardState.Success;
            if (HasDuplicates) return SpecialisationCardState.Error;
            return SpecialisationCardState.Neutral;
        }
    }
    public enum SpecialisationCardState
    {
        Neutral,
        Success,
        Error
    }
    public bool UseWardPactEnum { get; }

    public SpecialisationGroupVm(
        string title,
        IEnumerable<int> levels,
        List<string> optionNames,
        Dictionary<int, string> initiallySelectedByLevel,
        Action onAnySelectionChanged,
        bool useWardPactEnum)
    {
        Title = title;
        UseWardPactEnum = useWardPactEnum;
        _onAnySelectionChanged = onAnySelectionChanged;

        _allOptionNames = optionNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _filteredOptionNames = _allOptionNames.ToList();

        ToggleExpandedCommand = new Command(() => IsExpanded = !IsExpanded);

        foreach (var lvl in levels.OrderBy(x => x))
        {
            initiallySelectedByLevel.TryGetValue(lvl, out var pre);

            var slot = new SpecialisationSlotVm(
                lvl,
                useWardPactEnum,
                () => OnSlotChanged());

            slot.SetOptionsSource(() => FilteredOptionNames);

            if (useWardPactEnum)
            {
                if (!string.IsNullOrWhiteSpace(pre))
                {
                    var match = WardPactOptions.Standard.FirstOrDefault(k => string.Equals(k.Key, pre, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(match.Key))
                        slot.SelectedWardPact = match.Value;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(pre))
                    slot.SelectedOption = pre;
            }

            Slots.Add(slot);
        }

        RaiseComputed();
    }

    private void ApplyFilter()
    {
        var q = (_filterText ?? "").Trim();
        if (q.Length == 0)
            _filteredOptionNames = _allOptionNames.ToList();
        else
            _filteredOptionNames = _allOptionNames
                .Where(x => x.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Raise(nameof(FilteredOptionNames));

        foreach (var s in Slots)
            s.RaiseFilteredOptionsChanged();
    }

    private void OnSlotChanged()
    {
        RaiseComputed();
        _onAnySelectionChanged();
    }

    private void RaiseComputed()
    {
        Raise(nameof(SelectedCount));
        Raise(nameof(HasDuplicates));
        Raise(nameof(IsComplete));
        Raise(nameof(StatusText));
        Raise(nameof(HelperText));
        Raise(nameof(CardState));
    }
}