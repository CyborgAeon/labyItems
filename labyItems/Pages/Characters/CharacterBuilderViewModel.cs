using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Services;
using Command = Microsoft.Maui.Controls.Command;

namespace labyItems.Pages;

public readonly record struct RaceOption(string Name);
public readonly record struct ClassOption(string Name);
public sealed record LifeScaleSelection(LifeScaleService.LifeScaleEntry Entry, int LevelIndex, LifeScaleService.LifeScaleLevel Level);

public class CharacterBuilderViewModel : INotifyPropertyChanged
{
    private readonly List<LifeScaleService.LifeScaleEntry> _entries = new();
    private readonly Dictionary<string, string> _raceDescriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _classDescriptions = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    private bool _isUpdating;

    private RaceOption? _selectedRace;
    private ClassOption? _selectedClass;
    private IDictionary<string, RaceOption> _raceOptions = new Dictionary<string, RaceOption>(StringComparer.OrdinalIgnoreCase);
    private IDictionary<string, ClassOption> _classOptions = new Dictionary<string, ClassOption>(StringComparer.OrdinalIgnoreCase);
    private IDictionary<string, int> _lifeScaleLookup = new Dictionary<string, int>();
    private IList<string> _lifeScaleLabels = new List<string>();
    private LifeScaleService.LifeScaleEntry? _currentEntry;
    private bool _hasValidCombination;
    private string _availabilityMessage = "Pick a race and a class to see lifescales.";
    private bool _isErrorMessage;
    private bool _canContinue;
    private Command? _continueCommand;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Func<LifeScaleSelection, Task>? ContinueRequested;

    public IDictionary<string, RaceOption> RaceOptions
    {
        get => _raceOptions;
        private set
        {
            _raceOptions = value;
            OnPropertyChanged();
        }
    }

    public IDictionary<string, ClassOption> ClassOptions
    {
        get => _classOptions;
        private set
        {
            _classOptions = value;
            OnPropertyChanged();
        }
    }

    public RaceOption? SelectedRace
    {
        get => _selectedRace;
        set
        {
            if (SetProperty(ref _selectedRace, value))
                UpdateFilters();
        }
    }

    public ClassOption? SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetProperty(ref _selectedClass, value))
                UpdateFilters();
        }
    }

    public IDictionary<string, int> LifeScaleLookup
    {
        get => _lifeScaleLookup;
        private set
        {
            _lifeScaleLookup = value;
            OnPropertyChanged();
        }
    }

    public IList<string> LifeScaleLabels
    {
        get => _lifeScaleLabels;
        private set
        {
            _lifeScaleLabels = value;
            OnPropertyChanged();
        }
    }

    public int SelectedLevelIndex
    {
        get => _selectedLevelIndex;
        set
        {
            var sanitized = Math.Max(0, value);
            if (SetProperty(ref _selectedLevelIndex, sanitized))
            {
                OnPropertyChanged(nameof(SelectedLevelNumber));
            }
        }
    }

    public int SelectedLevelNumber => HasValidCombination ? SelectedLevelIndex + 1 : 0;

    public bool HasValidCombination
    {
        get => _hasValidCombination;
        private set
        {
            if (SetProperty(ref _hasValidCombination, value))
                UpdateCanContinue();
        }
    }

    public string AvailabilityMessage
    {
        get => _availabilityMessage;
        private set => SetProperty(ref _availabilityMessage, value);
    }

    public bool IsErrorMessage
    {
        get => _isErrorMessage;
        private set => SetProperty(ref _isErrorMessage, value);
    }

    public bool CanContinue
    {
        get => _canContinue;
        private set
        {
            if (SetProperty(ref _canContinue, value))
                _continueCommand?.ChangeCanExecute();
        }
    }

    public ICommand ContinueCommand => _continueCommand ??= new Command(async () => await OnContinueAsync(), () => CanContinue);

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            var all = await LifeScaleService.GetAllAsync();
            _entries.Clear();
            _entries.AddRange(all);

            RaceOptions = BuildRaceOptions(null);
            ClassOptions = BuildClassOptions(null);
            UpdateFilters();
        }
        catch (Exception ex)
        {
            AvailabilityMessage = $"Could not load lifescales: {ex.Message}";
            IsErrorMessage = true;
            HasValidCombination = false;
            CanContinue = false;
        }
    }

    private async Task OnContinueAsync()
    {
        if (!CanContinue || _currentEntry is null)
            return;

        var clampedIndex = Math.Clamp(SelectedLevelIndex, 0, Math.Max(0, _currentEntry.Levels.Count - 1));
        var level = _currentEntry.Levels.ElementAtOrDefault(clampedIndex);
        var selection = new LifeScaleSelection(_currentEntry, clampedIndex, level);

        if (ContinueRequested is not null)
            await ContinueRequested.Invoke(selection);
    }

    private void UpdateFilters()
    {
        if (_isUpdating)
            return;

        _isUpdating = true;
        try
        {
            var raceFilter = _selectedRace?.Name;
            var classFilter = _selectedClass?.Name;

            RaceOptions = BuildRaceOptions(classFilter);
            ClassOptions = BuildClassOptions(raceFilter);

            if (_selectedRace is { } race && !RaceOptions.ContainsKey(race.Name))
            {
                _selectedRace = null;
                OnPropertyChanged(nameof(SelectedRace));
                raceFilter = null;
            }

            if (_selectedClass is { } cls && !ClassOptions.ContainsKey(cls.Name))
            {
                _selectedClass = null;
                OnPropertyChanged(nameof(SelectedClass));
                classFilter = null;
            }

            _currentEntry = FindEntry(raceFilter, classFilter);
            if (_currentEntry is not null)
            {
                AvailabilityMessage = $"{_currentEntry.Race} {_currentEntry.ClassName} lifescales";
                IsErrorMessage = false;
                HasValidCombination = true;
                PopulateLifeScaleValues(_currentEntry);
            }
            else
            {
                ClearLifeScales();
                HasValidCombination = false;
                IsErrorMessage = !string.IsNullOrWhiteSpace(raceFilter) && !string.IsNullOrWhiteSpace(classFilter);
                AvailabilityMessage = IsErrorMessage
                    ? "That race and class combination is not available."
                    : "Pick a race and a class to see lifescales.";
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private IDictionary<string, RaceOption> BuildRaceOptions(string? classFilter)
    {
        return _entries
            .Where(e => string.IsNullOrWhiteSpace(classFilter) || e.ClassName.Equals(classFilter, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Race)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(n => n, n => new RaceOption(n), StringComparer.OrdinalIgnoreCase);
    }

    private IDictionary<string, ClassOption> BuildClassOptions(string? raceFilter)
    {
        return _entries
            .Where(e => string.IsNullOrWhiteSpace(raceFilter) || e.Race.Equals(raceFilter, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.ClassName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(n => n, n => new ClassOption(n), StringComparer.OrdinalIgnoreCase);
    }

    private LifeScaleService.LifeScaleEntry? FindEntry(string? race, string? className)
    {
        if (string.IsNullOrWhiteSpace(race) || string.IsNullOrWhiteSpace(className))
            return null;

        return _entries.FirstOrDefault(e =>
            e.Race.Equals(race, StringComparison.OrdinalIgnoreCase) &&
            e.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
    }

    private void PopulateLifeScaleValues(LifeScaleService.LifeScaleEntry entry)
    {
        var lookup = new Dictionary<string, int>();
        var labels = new List<string>();

        for (int i = 0; i < entry.Levels.Count; i++)
        {
            var level = entry.Levels[i];
            var label = $"{level.Life}/{level.Spirit}";
            lookup[label] = i + 1; // store level number as the value
            labels.Add($"Level {i + 1}: {label}");
        }

        LifeScaleLookup = lookup;
        LifeScaleLabels = labels;
        SelectedLevelIndex = lookup.Count > 0
            ? Math.Clamp(SelectedLevelIndex, 0, lookup.Count - 1)
            : 0;
        OnPropertyChanged(nameof(SelectedLevelNumber));
    }

    private void ClearLifeScales()
    {
        LifeScaleLookup = new Dictionary<string, int>();
        LifeScaleLabels = new List<string>();
        SelectedLevelIndex = 0;
        _currentEntry = null;
        OnPropertyChanged(nameof(SelectedLevelNumber));
    }

    private void UpdateCanContinue()
    {
        var hasLevels = _currentEntry?.Levels?.Count > 0;
        CanContinue = HasValidCombination && hasLevels;
    }

    public string GetRaceDescription(string race)
    {
        if (_raceDescriptions.TryGetValue(race, out var desc) && !string.IsNullOrWhiteSpace(desc))
            return desc;
        return $"No description available for {race} yet.";
    }

    public string GetClassDescription(string className)
    {
        if (_classDescriptions.TryGetValue(className, out var desc) && !string.IsNullOrWhiteSpace(desc))
            return desc;
        return $"No description available for {className} yet.";
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
