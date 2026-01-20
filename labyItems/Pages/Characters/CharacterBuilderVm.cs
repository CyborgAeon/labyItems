using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Threading;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Models.DTOs;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public sealed class CharacterBuilderVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private HashSet<string>? _allowedRaceKeysForSelectedClass;
    private string? _allowedRaceKeysForClass;

    private HashSet<string>? _allowedClassKeysForSelectedRace;
    private string? _allowedClassKeysForRace;
    private readonly SemaphoreSlim _abilityRefreshLock = new(1, 1);

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

    public CharacterDraft Draft => _draft;

    public void NotifyGatingChanged()
        => _notifyWizardGatingChanged();

    public CharacterSpecialisationVm SpecialisationVm { get; }

    public CharacterBuilderVm(CharacterDraft draft, Action notifyWizardGatingChanged)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;

        SpecialisationVm = new CharacterSpecialisationVm(this);

        SelectTabCommand = new Command<object>(p =>
        {
            if (p == null) return;

            if (p is int i) SelectedTabIndex = i;
            else if (int.TryParse(p.ToString(), out var j)) SelectedTabIndex = j;
        });

        ToggleClassExpandedCommand = new Command<ClassCardVm>(ToggleExpandedCommand);
        SelectClassCommand = new Command<ClassCardVm>(SelectClass);
        SelectRaceCommand = new Command<RaceCardVm>(SelectRace);
        ToggleRaceExpandedCommand = new Command<RaceCardVm>(ToggleRaceExpandedCommandImpl);

        SelectRaceFilterCommand = new Command<string>(s =>
        {
            SelectedRaceFilter = string.IsNullOrWhiteSpace(s) ? "All" : s;
        });

        ClassFilters = new ObservableCollection<string> { "All" };
        RaceFilters = new ObservableCollection<string> { "All" };

        _selectedClassFilter = "All";
        _selectedRaceFilter = "All";

        AllClasses = new ObservableCollection<ClassCardVm>();
        AllRaces = new ObservableCollection<RaceCardVm>();

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

            await SpecialisationVm.ReloadAsync();

            await RefreshDraftAbilitiesAsync();
        });
    }

    public ICommand ToggleRaceExpandedCommand { get; }

    private void ToggleRaceExpandedCommandImpl(RaceCardVm? item)
    {
        if (item == null) return;

        foreach (var r in FilteredRaces)
        {
            if (!ReferenceEquals(r, item) && r.IsExpanded)
                r.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
        RefilterRaces();
    }

    private async Task LoadRacesAsync()
    {
        var all = await PeopleService.GetAllAsync();

        var list = new List<RaceCardVm>();
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var idx = 0; idx < ordered.Count; idx++)
        {
            var name = ordered[idx].Key;
            var record = ordered[idx].Value;
            if (record == null) continue;

            var peopleType = record.PeopleType ?? "";
            if (!string.IsNullOrWhiteSpace(peopleType)) types.Add(peopleType);

            var vm = new RaceCardVm
            {
                Id = idx + 1,
                Name = name,
                PeopleType = peopleType,
                Description = record.Description ?? "",
                BuyAsRaw = record.BuyAs ?? "",
                Icon = IconForPeopleType(peopleType),
                IsSelected = string.Equals(name, _draft.Race, StringComparison.OrdinalIgnoreCase)
            };

            vm.BuildRowsAndChips(record.LevelledAbilities ?? new Dictionary<string, List<string>>(), record.BuyAs);
            list.Add(vm);
        }

        AllRaces.Clear();
        foreach (var vm in list)
            AllRaces.Add(vm);

        RaceFilters.Clear();
        RaceFilters.Add("All");
        foreach (var t in types.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            RaceFilters.Add(t);

        if (string.IsNullOrWhiteSpace(SelectedRaceFilter) || !RaceFilters.Contains(SelectedRaceFilter))
            SelectedRaceFilter = "All";

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

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (!Set(ref _selectedTabIndex, value)) return;
            Raise(nameof(IsClassTabSelected));
            Raise(nameof(IsRaceTabSelected));
        }
    }

    public bool IsClassTabSelected => SelectedTabIndex == 0;
    public bool IsRaceTabSelected => SelectedTabIndex == 1;

    public ICommand SelectTabCommand { get; }
    public ICommand SelectRaceFilterCommand { get; }

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

    public ObservableCollection<string> ClassFilters { get; }
    public ObservableCollection<string> RaceFilters { get; }

    private string? _selectedClassFilter;
    public string? SelectedClassFilter
    {
        get => _selectedClassFilter;
        set { if (Set(ref _selectedClassFilter, value)) RefilterClasses(); }
    }

    private string? _selectedRaceFilter;
    public string? SelectedRaceFilter
    {
        get => _selectedRaceFilter;
        set { if (Set(ref _selectedRaceFilter, value)) RefilterRaces(); }
    }

    public ObservableCollection<ClassCardVm> AllClasses { get; }
    public ObservableCollection<RaceCardVm> AllRaces { get; }

    public ObservableCollection<ClassCardVm> FilteredClasses { get; } = new();
    public ObservableCollection<RaceCardVm> FilteredRaces { get; } = new();

    public ICommand ToggleClassExpandedCommand { get; }
    public ICommand SelectClassCommand { get; }
    public ICommand SelectRaceCommand { get; }

    private void ToggleExpandedCommand(ClassCardVm? item)
    {
        if (item == null) return;

        foreach (var c in FilteredClasses)
        {
            if (!ReferenceEquals(c, item) && c.IsExpanded)
                c.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
        RefilterClasses();
    }

    private void SelectClass(ClassCardVm? item)
    {
        if (item == null) return;

        foreach (var c in AllClasses)
            c.IsSelected = ReferenceEquals(c, item);

        _draft.Class = item.Name;
        _notifyWizardGatingChanged();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await RefreshAllowedRacesForSelectedClassAsync();
            RefilterRaces();

            await SpecialisationVm.ReloadAsync();
            await RefreshDraftAbilitiesAsync();
        });
    }

    private void SelectRace(RaceCardVm? item)
    {
        if (item == null) return;

        foreach (var r in AllRaces)
            r.IsSelected = ReferenceEquals(r, item);

        _draft.Race = item.Name;
        _notifyWizardGatingChanged();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await RefreshAllowedClassesForSelectedRaceAsync();
            RefilterClasses();

            await ApplyRaceToClassesAsync(item.Name);
            RefilterRaces();

            await SpecialisationVm.ReloadAsync();
            await RefreshDraftAbilitiesAsync();
        });
    }

    private async Task LoadClassesAsync()
    {
        var all = await ClassService.GetAllAsync();

        var list = new List<ClassCardVm>();
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var idx = 0; idx < ordered.Count; idx++)
        {
            var classKey = ordered[idx].Key;
            var record = ordered[idx].Value;

            var bracket = record.Brackets?.FirstOrDefault() ?? "";
            var icon = ExtractIcon(bracket);
            var category = ExtractCategory(bracket);

            if (!string.IsNullOrWhiteSpace(category))
                categories.Add(category);

            var maxAc = ParseInt(record.MaxAC);
            var powerBase = record.Powerbase?.FirstOrDefault() ?? "";

            var raceKeyForLife = ResolveRaceKeyForLifeScale(_draft);

            var points = await LifeScalesService.GetLifeScaleAsync(raceKeyForLife, classKey);
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
                IsSelected = string.Equals(classKey, _draft.Class, StringComparison.OrdinalIgnoreCase)
            });
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AllClasses.Clear();
            foreach (var vm in list)
                AllClasses.Add(vm);

            ClassFilters.Clear();
            ClassFilters.Add("All");
            foreach (var c in categories.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                ClassFilters.Add(c);

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

        var classes = await LifeScalesService.GetClassesForRaceAsync(raceForFiltering);

        _allowedClassKeysForSelectedRace = classes
            .Select(LifeScalesService.NormalizeKey)
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

        var races = await LifeScalesService.GetRacesForClassAsync(selectedClass);

        _allowedRaceKeysForSelectedClass = races
            .Select(LifeScalesService.NormalizeKey)
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allowedRaceKeysForClass = selectedClass;
    }

    private async Task ApplyRaceToClassesAsync(string? raceName)
    {
        var race = string.IsNullOrWhiteSpace(raceName) ? "" : raceName;

        var tasks = new List<Task>(AllClasses.Count);
        foreach (var c in AllClasses)
        {
            c.RaceName = race;
            tasks.Add(c.ReloadProgressionAsync());
        }

        await Task.WhenAll(tasks);
    }

    public async Task RefreshDraftAbilitiesAsync()
    {
        await _abilityRefreshLock.WaitAsync();
        try
        {
            var abilities = new List<AbilityDraft>();

            abilities.AddRange(await BuildRaceAbilitiesAsync());
            abilities.AddRange(await BuildClassAbilitiesAsync());
            abilities.AddRange(SpecialisationVm.BuildSelectedAbilityDrafts());

            Draft.Abilities = abilities
                .OrderBy(a => a.LevelGained ?? int.MaxValue)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
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

    private async Task<List<AbilityDraft>> BuildRaceAbilitiesAsync()
    {
        var list = new List<AbilityDraft>();

        var raceName = (Draft.Race ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return list;

        var all = await PeopleService.GetAllAsync();
        if (TryGetRecord(all, raceName, out var rec) && rec?.LevelledAbilities != null)
            list.AddRange(AbilityDraftBuilder.BuildFromLevels(rec.LevelledAbilities));

        return list;
    }

    private async Task<List<AbilityDraft>> BuildClassAbilitiesAsync()
    {
        var list = new List<AbilityDraft>();

        var className = (Draft.Class ?? string.Empty).Trim();
        if (className.Length == 0)
            return list;

        var all = await ClassService.GetAllAsync();
        if (TryGetRecord(all, className, out var rec) && rec?.Levels != null)
            list.AddRange(AbilityDraftBuilder.BuildFromLevels(rec.Levels));

        return list;
    }

    private static bool TryGetRecord<T>(Dictionary<string, T> map, string key, out T? value)
    {
        if (map.TryGetValue(key, out value))
            return true;

        foreach (var kvp in map)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static int ParseInt(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)) return n;
        if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out n)) return n;
        return 0;
    }

    private static string ExtractIcon(string bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket)) return "🛡️";
        var parts = bracket.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "🛡️";
    }

    private static string ExtractCategory(string bracket)
    {
        if (string.IsNullOrWhiteSpace(bracket)) return "";
        var parts = bracket.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1] : bracket;
    }

    private void RefilterClasses()
    {
        var q = (ClassSearchText ?? "").Trim().ToLowerInvariant();
        var filter = SelectedClassFilter ?? "All";

        var allowed = _allowedClassKeysForSelectedRace;

        var list = AllClasses
            .Where(c =>
                (filter == "All" || string.Equals(c.Category, filter, StringComparison.OrdinalIgnoreCase)) &&
                (allowed == null || allowed.Contains(LifeScalesService.NormalizeKey(c.Name ?? c.Key ?? ""))) &&
                (q.Length == 0 ||
                 (c.Name ?? "").ToLowerInvariant().Contains(q) ||
                 (c.Summary ?? "").ToLowerInvariant().Contains(q)))
            .OrderByDescending(c => c.IsSelected)
            .ToList();

        ReplaceItems(FilteredClasses, list);
    }

    private void RefilterRaces()
    {
        var q = (RaceSearchText ?? "").Trim().ToLowerInvariant();
        var filter = SelectedRaceFilter ?? "All";

        var allowed = _allowedRaceKeysForSelectedClass;

        var list = AllRaces
            .Where(r =>
                (filter == "All" || string.Equals(r.PeopleType, filter, StringComparison.OrdinalIgnoreCase)) &&
                (allowed == null || allowed.Contains(LifeScalesService.NormalizeKey(r.Name))) &&
                (q.Length == 0 ||
                 r.Name.ToLowerInvariant().Contains(q) ||
                 (r.Description ?? "").ToLowerInvariant().Contains(q)))
            .OrderByDescending(r => r.IsSelected)
            .ToList();

        ReplaceItems(FilteredRaces, list);
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IList<T> items)
    {
        target.Clear();
        foreach (var i in items)
            target.Add(i);
    }
}
