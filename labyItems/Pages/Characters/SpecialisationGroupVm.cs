
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using labyItems.Controls.Pickers;

namespace labyItems.Pages.Characters;

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