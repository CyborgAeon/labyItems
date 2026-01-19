using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Services;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public sealed class CharacterSpecialisationVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly CharacterBuilderVm _builder;
    private CharacterDraft Draft => _builder.Draft;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        Raise(name);
        return true;
    }

    public ObservableCollection<SpecialisationGroupVm> Groups { get; } = new();
    public ObservableCollection<MappedSpecialisationVm> MappedSpecialisations { get; } = new();

    public ObservableCollection<string> RaceSubtypeOptions { get; } = new();
    public ObservableCollection<RaceSubtypePreviewLine> RaceSubtypeAbilitiesPreview { get; } = new();
    public ObservableCollection<RaceSubtypeLevelRow> RaceSubtypeLevelRows { get; } = new();

    private readonly Dictionary<string, SpecialisationDefinition> _specialisationIndex = new(StringComparer.OrdinalIgnoreCase);
    private bool _isSyncingRaceSubtype;

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
    public bool HasMappedSpecialisations => MappedSpecialisations.Count > 0;
    private bool _hasRaceSubtypeChoice;
    public bool HasRaceSubtypeChoice
    {
        get => _hasRaceSubtypeChoice;
        private set
        {
            if (!Set(ref _hasRaceSubtypeChoice, value)) return;
            Raise(nameof(HasNoChoices));
        }
    }

    private string? _selectedRaceSubtype;
    public string? SelectedRaceSubtype
    {
        get => _selectedRaceSubtype;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (!Set(ref _selectedRaceSubtype, normalized)) return;

            Raise(nameof(HasRaceSubtypeSelection));

            if (_raceSubtypeSlot != null && !_isSyncingRaceSubtype)
            {
                _isSyncingRaceSubtype = true;
                _raceSubtypeSlot.SelectedOption = normalized.Length == 0 ? null : normalized;
                _isSyncingRaceSubtype = false;
            }

            SyncRaceSubtypeDraftAndPreview();
        }
    }

    public bool HasRaceSubtypeSelection => !string.IsNullOrWhiteSpace(_selectedRaceSubtype);

    private string _raceSubtypeTitle = "Race subtype";
    public string RaceSubtypeTitle
    {
        get => _raceSubtypeTitle;
        private set => Set(ref _raceSubtypeTitle, value);
    }

    private string _raceSubtypeStatusText = string.Empty;
    public string RaceSubtypeStatusText
    {
        get => _raceSubtypeStatusText;
        private set => Set(ref _raceSubtypeStatusText, value);
    }

    private string _raceSubtypeSubtitle = string.Empty;
    public string RaceSubtypeSubtitle
    {
        get => _raceSubtypeSubtitle;
        private set => Set(ref _raceSubtypeSubtitle, value);
    }

    private string _raceSubtypeCardState = "Neutral";
    public string RaceSubtypeCardState
    {
        get => _raceSubtypeCardState;
        private set => Set(ref _raceSubtypeCardState, value);
    }

    private SpecialisationSlotVm? _raceSubtypeSlot;
    private string _raceSubtypeKey = "";
    private string _raceSubtypeAbilityMapKey = "";
    private bool _raceSubtypeRequired;
    private string _raceSubtypeTitleBase = "Race subtype";
    private string _raceSubtypeDescription = string.Empty;
    private string _raceSubtypeLifeScaleOverride = string.Empty;
    private string _currentRaceForSubtype = string.Empty;
    private bool _raceSubtypeLevelsExpanded;
    private bool _showRaceSubtypeLifeScale;

    public bool HasNoChoices => !HasChoices;
    public bool RaceSubtypeLevelsExpanded
    {
        get => _raceSubtypeLevelsExpanded;
        private set => Set(ref _raceSubtypeLevelsExpanded, value);
    }

    public ICommand ToggleRaceSubtypeLevelsCommand { get; }

    public bool ShowRaceSubtypeLifeScale
    {
        get => _showRaceSubtypeLifeScale;
        private set
        {
            if (!Set(ref _showRaceSubtypeLifeScale, value)) return;
            Raise(nameof(HideRaceSubtypeLifeScale));
        }
    }

    public bool HideRaceSubtypeLifeScale => !ShowRaceSubtypeLifeScale;

    private string _headerText = "Make your selections below.";
    public string HeaderText
    {
        get => _headerText;
        private set => Set(ref _headerText, value);
    }

    private bool _isComplete;
    public bool IsComplete
    {
        get => _isComplete;
        private set => Set(ref _isComplete, value);
    }

    public CharacterSpecialisationVm(CharacterBuilderVm builder)
    {
        _builder = builder;
        ToggleRaceSubtypeLevelsCommand = new Command(() => RaceSubtypeLevelsExpanded = !RaceSubtypeLevelsExpanded);

        MappedSpecialisations.CollectionChanged += (_, __) => Raise(nameof(HasMappedSpecialisations));
    }

    public async Task ReloadAsync()
    {
        Groups.Clear();
        MappedSpecialisations.Clear();
        RaceSubtypeOptions.Clear();
        RaceSubtypeAbilitiesPreview.Clear();
        RaceSubtypeLevelRows.Clear();
        HasRaceSubtypeChoice = false;
        RaceSubtypeLevelsExpanded = false;

        _raceSubtypeSlot = null;
        _raceSubtypeKey = "";
        _raceSubtypeAbilityMapKey = "";
        _raceSubtypeRequired = false;
        _raceSubtypeTitleBase = "Race subtype";
        _raceSubtypeDescription = string.Empty;
        _raceSubtypeLifeScaleOverride = string.Empty;
        _currentRaceForSubtype = string.Empty;

        _isSyncingRaceSubtype = true;
        Set(ref _selectedRaceSubtype, string.Empty, nameof(SelectedRaceSubtype));
        Raise(nameof(HasRaceSubtypeSelection));
        _isSyncingRaceSubtype = false;

        RaceSubtypeTitle = _raceSubtypeTitleBase;
        RaceSubtypeStatusText = string.Empty;
        RaceSubtypeSubtitle = string.Empty;
        RaceSubtypeCardState = "Neutral";

        var race = (Draft.Race ?? string.Empty).Trim();
        var cls = (Draft.Class ?? string.Empty).Trim();

        if (race.Length == 0 && cls.Length == 0)
        {
            HasChoices = false;
            HeaderText = "Select a race and class first.";
            RecomputeCompletion();
            return;
        }

        var specialisationIndex = await LoadSpecialisationIndexAsync();
        _specialisationIndex.Clear();
        foreach (var kvp in specialisationIndex)
            _specialisationIndex[kvp.Key] = kvp.Value;

        var required = new List<RequiredChoice>();

        // --------------------------
        // CLASS REQUIRED CHOICES
        // --------------------------
        if (cls.Length > 0)
        {
            var allClasses = await ClassService.GetAllAsync();
            if (allClasses.TryGetValue(cls, out var classRec) && classRec != null)
            {
                foreach (var kvp in classRec.Levels ?? new Dictionary<string, List<string>>())
                {
                    if (!int.TryParse(kvp.Key, out var level))
                        continue;

                    foreach (var abilityToken in kvp.Value ?? new List<string>())
                    {
                        var key = FindSpecialisationKey(abilityToken, specialisationIndex.Keys);
                        if (key == null)
                            continue;

                        required.Add(new RequiredChoice
                        {
                            Source = ChoiceSource.Class,
                            SourceName = cls,
                            SpecialisationKey = key,
                            Level = level
                        });
                    }
                }
            }
        }

        // --------------------------
        // RACE: SUBTYPE + REQUIRED CHOICES
        // --------------------------
        if (race.Length > 0)
        {
            var allPeople = await PeopleService.GetAllAsync(); // uses PeopleRecord.Subtype now :contentReference[oaicite:2]{index=2}
            if (allPeople.TryGetValue(race, out var raceRec) && raceRec != null)
            {
                // Subtype group (data-driven)
                var subtype = raceRec.Subtype;
                if (subtype != null)
                {
                    var options = ResolveSubtypeOptions(subtype.OptionsSource);
                    _raceSubtypeRequired = (subtype.SelectionMode ?? "")
                        .Contains("Required", StringComparison.OrdinalIgnoreCase);

                    _raceSubtypeKey = (subtype.Key ?? string.Empty).Trim();
                    _raceSubtypeAbilityMapKey = (subtype.AbilityMapKey ?? string.Empty).Trim();
                    _raceSubtypeTitleBase = string.IsNullOrWhiteSpace(subtype.DisplayName)
                        ? $"{race} subtype"
                        : subtype.DisplayName.Trim();
                    _raceSubtypeDescription = subtype.Description?.Trim() ?? string.Empty;
                    _currentRaceForSubtype = race;

                    RaceSubtypeOptions.Clear();
                    foreach (var o in options)
                        RaceSubtypeOptions.Add(o);

                    HasRaceSubtypeChoice = options.Count > 0;

                    var initialSelection = BuildSubtypeInitialSelection();
                    if (initialSelection.TryGetValue(0, out var pre) && !string.IsNullOrWhiteSpace(pre))
                        SelectedRaceSubtype = pre;
                    else
                        SyncRaceSubtypeDraftAndPreview();
                }

                foreach (var kvp in raceRec.LevelledAbilities ?? new Dictionary<string, List<string>>())
                {
                    if (!int.TryParse(kvp.Key, out var level))
                        continue;

                    foreach (var abilityToken in kvp.Value ?? new List<string>())
                    {
                        var key = FindSpecialisationKey(abilityToken, specialisationIndex.Keys);
                        if (key == null)
                            continue;

                        required.Add(new RequiredChoice
                        {
                            Source = ChoiceSource.Race,
                            SourceName = race,
                            SpecialisationKey = key,
                            Level = level
                        });
                    }
                }
            }
        }

        var byKey = required
            .GroupBy(r => r.SpecialisationKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var g in byKey)
        {
            if (!specialisationIndex.TryGetValue(g.Key, out var def) || def == null)
                continue;

            var title = g.Key;
            var levels = g.Select(x => x.Level).Distinct().OrderBy(x => x).ToList();

            if (def.ColourAbilities != null && def.ColourAbilities.Count > 0)
            {
                var subtitle = BuildSubtitle(g);
                var mapped = new MappedSpecialisationVm(
                    key: title,
                    subtitle: string.IsNullOrWhiteSpace(subtitle) ? "Select a subtype to unlock its benefits." : subtitle,
                    levels: levels,
                    optionMap: def.ColourAbilities,
                    initialSelection: GetSavedSpecialisationSelection(title),
                    required: true,
                    onSelectionChanged: OnMappedSpecialisationChanged);

                MappedSpecialisations.Add(mapped);
                continue;
            }

            var groupVm = new SpecialisationGroupVm(
                title: title,
                levels: levels,
                optionNames: def.Abilities ?? new List<string>(),
                initiallySelectedByLevel: new Dictionary<int, string>(),
                onAnySelectionChanged: OnAnySelectionChanged,
                useWardPactEnum: UsesInlineDictionarySearch(title));

            groupVm.IsExpanded = true;

            Groups.Add(groupVm);
        }


        SyncMappedSelectionsToDraft();

        HasChoices = HasRaceSubtypeChoice || Groups.Count > 0 || HasMappedSpecialisations;
        HeaderText = HasChoices ? "Make your selections below." : "No specialisation choices required.";

        RecomputeCompletion();
    }

    private void OnAnySelectionChanged()
    {
        SyncRaceSubtypeDraftAndPreview();
    }

    private void OnMappedSpecialisationChanged()
    {
        SyncMappedSelectionsToDraft();
        RecomputeCompletion();
        _builder.NotifyGatingChanged();
    }

    private void SyncMappedSelectionsToDraft()
    {
        var currentKeys = MappedSpecialisations
            .Select(m => m.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toRemove = Draft.SpecialisationSelections.Keys
            .Where(k => !currentKeys.Contains(k))
            .ToList();

        foreach (var key in toRemove)
            Draft.SpecialisationSelections.Remove(key);

        foreach (var m in MappedSpecialisations)
        {
            var picked = (m.SelectedOption ?? string.Empty).Trim();
            if (picked.Length == 0)
                Draft.SpecialisationSelections.Remove(m.Key);
            else
                Draft.SpecialisationSelections[m.Key] = picked;
        }
    }

    private async void SyncRaceSubtypeDraftAndPreview()
    {
        if (_isSyncingRaceSubtype)
            return;

        _isSyncingRaceSubtype = true;

        try
        {
            var picked = (_raceSubtypeSlot?.SelectedOption ?? _selectedRaceSubtype ?? string.Empty).Trim();

            if (Set(ref _selectedRaceSubtype, picked, nameof(SelectedRaceSubtype)))
                Raise(nameof(HasRaceSubtypeSelection));

            var effectiveKey = HasRaceSubtypeChoice ? _raceSubtypeKey : string.Empty;
            var effectivePicked = HasRaceSubtypeChoice ? picked : string.Empty;

            Draft.RaceSubtypeKey = effectiveKey;
            Draft.RaceSubtypeValue = effectivePicked;
            Draft.RaceSubtype = effectivePicked;

            await UpdateRaceSubtypePreviewAsync(effectivePicked);
            UpdateRaceSubtypeCardState(effectivePicked);

            RecomputeCompletion();
            _builder.NotifyGatingChanged();
        }
        finally
        {
            _isSyncingRaceSubtype = false;
        }
    }

    private void RecomputeCompletion()
    {
        var complete = true;

        foreach (var g in Groups)
        {
            if (!g.IsComplete)
            {
                complete = false;
                break;
            }
        }

        if (complete)
        {
            foreach (var m in MappedSpecialisations)
            {
                if (!m.IsComplete)
                {
                    complete = false;
                    break;
                }
            }
        }

        // If subtype is required, enforce it explicitly (since IsComplete already does for the 1-slot group,
        // this is mostly belt-and-braces if subtype group isn't created for some reason)
        if (_raceSubtypeRequired && string.IsNullOrWhiteSpace(_selectedRaceSubtype))
            complete = false;

        IsComplete = complete;
        HasChoices = HasRaceSubtypeChoice || Groups.Count > 0 || HasMappedSpecialisations;
        Raise(nameof(HasNoChoices));
    }

    private async Task UpdateRaceSubtypePreviewAsync(string picked)
    {
        RaceSubtypeAbilitiesPreview.Clear();
        RaceSubtypeLevelRows.Clear();
        _raceSubtypeLifeScaleOverride = string.Empty;
        Draft.LifeScaleKeyOverride = string.Empty;
        RaceSubtypeLevelsExpanded = false;
        ShowRaceSubtypeLifeScale = false;

        if (!HasRaceSubtypeChoice)
            return;

        if (string.IsNullOrWhiteSpace(_raceSubtypeAbilityMapKey) || string.IsNullOrWhiteSpace(picked))
            return;

        if (!_specialisationIndex.TryGetValue(_raceSubtypeAbilityMapKey, out var mapDef) || mapDef?.ColourAbilities == null)
            return;

        if (!mapDef.ColourAbilities.TryGetValue(picked, out var entry) || entry == null)
            return;

        if (!string.IsNullOrWhiteSpace(entry.LifeScaleOverride))
        {
            _raceSubtypeLifeScaleOverride = entry.LifeScaleOverride.Trim();
            Draft.LifeScaleKeyOverride = _raceSubtypeLifeScaleOverride;

            RaceSubtypeAbilitiesPreview.Add(new RaceSubtypePreviewLine
            {
                Level = "Life",
                Ability = $"Life scale override: {_raceSubtypeLifeScaleOverride}"
            });
        }

        var levels = entry.Levels ?? new Dictionary<string, List<string>>();
        var abilityByLevel = new Dictionary<int, List<string>>();
        var hasAbilities = false;

        var ordered = levels
            .Select(kvp => new
            {
                Key = kvp.Key ?? string.Empty,
                Level = int.TryParse(kvp.Key, out var n) ? n : int.MaxValue,
                Abilities = (kvp.Value ?? new List<string>()).Select(x => (x ?? string.Empty).Trim()).Where(x => x.Length > 0).ToList()
            })
            .Where(x => x.Abilities.Count > 0)
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var lvl in ordered)
        {
            var label = lvl.Level == int.MaxValue ? lvl.Key : $"Lv {lvl.Level}";
            foreach (var ability in lvl.Abilities)
            {
                RaceSubtypeAbilitiesPreview.Add(new RaceSubtypePreviewLine
                {
                    Level = label,
                    Ability = ability
                });
            }

            if (lvl.Level != int.MaxValue)
            {
                if (!abilityByLevel.TryGetValue(lvl.Level, out var list))
                {
                    list = new List<string>();
                    abilityByLevel[lvl.Level] = list;
                }

                list.AddRange(lvl.Abilities);
                hasAbilities = true;
            }
        }

        var className = (Draft.Class ?? string.Empty).Trim();
        var hasOverride = !string.IsNullOrWhiteSpace(_raceSubtypeLifeScaleOverride);
        var lifeScaleRace = hasOverride ? _raceSubtypeLifeScaleOverride : string.Empty;

        IReadOnlyList<LifeScalePoint> life = Array.Empty<LifeScalePoint>();
        if (hasOverride && className.Length > 0)
        {
            life = await LifeScalesService.GetLifeScaleAsync(lifeScaleRace, className);

            if ((life == null || life.Count == 0) && !string.Equals(lifeScaleRace, _currentRaceForSubtype, StringComparison.OrdinalIgnoreCase))
            {
                // Fallback to base race if override is missing in lifescales
                life = await LifeScalesService.GetLifeScaleAsync(_currentRaceForSubtype, className);
            }
        }

        var hasLife = hasOverride && life != null && life.Count > 0;

        for (var level = 1; level <= 8; level++)
        {
            var body = hasLife && life.Count >= level ? life[level - 1].Body.ToString() : "";
            var loc = hasLife && life.Count >= level ? life[level - 1].Loc.ToString() : "";
            var abilities = abilityByLevel.TryGetValue(level, out var list)
                ? string.Join(", ", list.Distinct(StringComparer.OrdinalIgnoreCase))
                : "";

            RaceSubtypeLevelRows.Add(new RaceSubtypeLevelRow
            {
                Level = level,
                Body = body,
                Loc = loc,
                Abilities = abilities
            });
        }

        ShowRaceSubtypeLifeScale = hasLife;
        RaceSubtypeLevelsExpanded = hasLife || hasAbilities;
    }

    private void UpdateRaceSubtypeCardState(string picked)
    {
        var hasSelection = !string.IsNullOrWhiteSpace(picked);

        RaceSubtypeTitle = hasSelection
            ? $"{picked} benefits"
            : _raceSubtypeTitleBase;

        RaceSubtypeStatusText = hasSelection
            ? "Selected"
            : (_raceSubtypeRequired ? "Required" : "Optional");

        RaceSubtypeCardState = hasSelection
            ? "Success"
            : (_raceSubtypeRequired ? "Error" : "Neutral");

        var subtitle = _raceSubtypeDescription;
        if (!string.IsNullOrWhiteSpace(_raceSubtypeLifeScaleOverride))
        {
            subtitle = subtitle.Length > 0
                ? $"{subtitle} Life scale override: {_raceSubtypeLifeScaleOverride}."
                : $"Life scale override: {_raceSubtypeLifeScaleOverride}.";
        }

        if (string.IsNullOrWhiteSpace(subtitle))
            subtitle = hasSelection
                ? "Preview your racial abilities by level."
                : "Choose a subtype to preview its abilities.";

        RaceSubtypeSubtitle = subtitle;
    }

    private static bool UsesInlineDictionarySearch(string groupTitle)
        => string.Equals(groupTitle.Trim(), "Ward pact", StringComparison.OrdinalIgnoreCase);

    private static string? FindSpecialisationKey(string rawAbilityToken, IEnumerable<string> knownKeys)
    {
        var token = (rawAbilityToken ?? string.Empty).Trim();
        if (token.Length == 0)
            return null;

        foreach (var k in knownKeys)
        {
            if (string.Equals(k, token, StringComparison.OrdinalIgnoreCase))
                return k;
        }

        var lowered = token.ToLowerInvariant();
        foreach (var k in knownKeys)
        {
            var kk = k.ToLowerInvariant();
            if (kk == lowered)
                return k;
        }

        return null;
    }

    private static string BuildSubtitle(IEnumerable<RequiredChoice> grouped)
    {
        var sources = grouped
            .Select(x => x.SourceName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var levels = grouped
            .Select(x => x.Level)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var srcText = sources.Count > 0 ? string.Join(", ", sources) : "Race/Class";
        var lvlText = levels.Count > 0 ? string.Join(", ", levels.Select(l => $"Lv {l}")) : "Levels";

        return $"{srcText} • {lvlText}";
    }

    // --------------------------
    // SUBTYPE: OPTIONS RESOLUTION
    // --------------------------
    private static List<string> ResolveSubtypeOptions(string? optionsSource)
    {
        var src = (optionsSource ?? string.Empty).Trim();
        if (src.Length == 0)
            return new List<string>();

        // Format: "Enum:ElfColours"
        if (src.StartsWith("Enum:", StringComparison.OrdinalIgnoreCase))
        {
            var enumName = src.Substring("Enum:".Length).Trim();
            if (enumName.Length == 0)
                return new List<string>();

            var enumType = FindEnumTypeByName(enumName);
            if (enumType == null)
                return new List<string>();

            return Enum.GetNames(enumType).ToList();
        }

        // Future-proofing: allow comma-separated list as fallback
        if (src.Contains(',', StringComparison.Ordinal))
        {
            return src.Split(',')
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<string>();
    }

    private static Type? FindEnumTypeByName(string enumName)
    {
        // Search loaded assemblies for a matching enum type name (namespace-agnostic)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).Cast<Type>().ToArray(); }

            foreach (var t in types)
            {
                if (t.IsEnum && string.Equals(t.Name, enumName, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
        }

        return null;
    }


    private SpecialisationGroupVm BuildRaceSubtypeGroup(
        string raceName,
        PeopleSubtypeRecord subtype,
        List<string> options)
    {
        // If you want to respect SelectionMode:
        _raceSubtypeRequired = (subtype.SelectionMode ?? "")
            .Contains("Required", StringComparison.OrdinalIgnoreCase);

        // If subtype is optional and you *do not* want it to block progression,
        // you can set _raceSubtypeRequired = false, and handle that in RecomputeCompletion (see note below).

        _raceSubtypeKey = subtype.Key ?? "";
        _raceSubtypeAbilityMapKey = subtype.AbilityMapKey ?? "";

        var title = string.IsNullOrWhiteSpace(subtype.DisplayName)
            ? $"{raceName} subtype"
            : subtype.DisplayName.Trim();

        // Your Group VM computes Subtitle itself (“Pick 1 ability” etc.).
        // If you need a richer subtitle text for subtype groups, you’d have to extend SpecialisationGroupVm.
        // For now, use the title only.

        var group = new SpecialisationGroupVm(
            title: title,
            levels: new[] { 0 },                       // single “slot”
            optionNames: options,                      // chip list source
            initiallySelectedByLevel: BuildSubtypeInitialSelection(),
            onAnySelectionChanged: OnAnySelectionChanged,
            useWardPactEnum: false);

        group.IsExpanded = true;

        // Capture a reference to the slot so we can sync draft + preview on changes
        _raceSubtypeSlot = group.Slots.FirstOrDefault();

        return group;
    }

    private Dictionary<int, string> BuildSubtypeInitialSelection()
    {
        // Canonical first; fallback to legacy if needed
        var picked = (Draft.RaceSubtypeValue ?? "").Trim();
        if (picked.Length == 0)
            picked = (Draft.RaceSubtype ?? "").Trim();

        return picked.Length == 0
            ? new Dictionary<int, string>()
            : new Dictionary<int, string> { [0] = picked };
    }

    private string? GetSavedSpecialisationSelection(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (Draft.SpecialisationSelections.TryGetValue(key, out var saved)
            && !string.IsNullOrWhiteSpace(saved))
        {
            return saved.Trim();
        }

        return null;
    }


    // --------------------------
    // SPECIALISATION INDEX (JSON-driven)
    // --------------------------
    private static async Task<Dictionary<string, SpecialisationDefinition>> LoadSpecialisationIndexAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("specialisation/specialisation.json");
        using var doc = await JsonDocument.ParseAsync(stream);

        var root = doc.RootElement;
        var dict = new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object)
                continue;

            var def = new SpecialisationDefinition();

            // Standard specialisations: { "Abilities": [ ... ] }
            if (prop.Value.TryGetProperty("Abilities", out var abilitiesEl) && abilitiesEl.ValueKind == JsonValueKind.Array)
            {
                def.Abilities = abilitiesEl.EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .ToList();
            }

            // If it looks like an ability table (colour -> { Levels: { "1": [..] } })
            // or (colour -> { "1": [..] }) — support both shapes robustly.
            if (def.Abilities == null)
            {
                var parsed = ParseColourAbilities(prop.Value);
                if (parsed != null && parsed.Count > 0)
                    def.ColourAbilities = parsed;
            }

            dict[prop.Name] = def;
        }

        return dict;
    }

    private static Dictionary<string, ColourAbilityDefinition>? ParseColourAbilities(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var outer = new Dictionary<string, ColourAbilityDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var colourProp in element.EnumerateObject())
        {
            if (colourProp.Value.ValueKind != JsonValueKind.Object)
                continue;

            var entry = new ColourAbilityDefinition();

            if (colourProp.Value.TryGetProperty("LifeScaleOverride", out var lsEl) && lsEl.ValueKind == JsonValueKind.String)
                entry.LifeScaleOverride = lsEl.GetString() ?? string.Empty;

            // Preferred shape: colour -> { Levels: { "1": [..], ... } }
            if (colourProp.Value.TryGetProperty("Levels", out var levelsEl) && levelsEl.ValueKind == JsonValueKind.Object)
                entry.Levels = ParseLevelArrays(levelsEl);
            else
                entry.Levels = ParseLevelArrays(colourProp.Value);

            if (entry.Levels.Count > 0 || entry.LifeScaleOverride.Length > 0)
                outer[colourProp.Name] = entry;
        }

        return outer.Count > 0 ? outer : null;
    }

    private static Dictionary<string, List<string>> ParseLevelArrays(JsonElement levelsObject)
    {
        var levels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (levelsObject.ValueKind != JsonValueKind.Object)
            return levels;

        foreach (var lvlProp in levelsObject.EnumerateObject())
        {
            if (lvlProp.Value.ValueKind != JsonValueKind.Array)
                continue;

            var list = lvlProp.Value.EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => x.Length > 0)
                .ToList();

            if (list.Count > 0)
                levels[lvlProp.Name] = list;
        }

        return levels;
    }

    public sealed class MappedSpecialisationVm : INotifyPropertyChanged
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
        private readonly Dictionary<string, ColourAbilityDefinition> _optionMap;
        private readonly bool _required;
        private bool _suppressNotify;

        public string Key { get; }
        public string Title { get; }
        public string Subtitle { get; }

        public ObservableCollection<string> Options { get; } = new();
        public ObservableCollection<RaceSubtypePreviewLine> AbilitiesPreview { get; } = new();
        public ObservableCollection<RaceSubtypeLevelRow> LevelRows { get; } = new();

        private string? _selectedOption;
        public string? SelectedOption
        {
            get => _selectedOption;
            set
            {
                var normalized = (value ?? string.Empty).Trim();
                if (!Set(ref _selectedOption, normalized)) return;

                UpdatePreview();
                RaiseComputed();

                if (!_suppressNotify)
                    _onChanged();
            }
        }

        private bool _levelsExpanded;
        public bool LevelsExpanded
        {
            get => _levelsExpanded;
            set => Set(ref _levelsExpanded, value);
        }

        public ICommand ToggleLevelsCommand { get; }

        public bool HasSelection => !string.IsNullOrWhiteSpace(_selectedOption);
        public bool IsComplete => !_required || HasSelection;
        public string StatusText => HasSelection ? "Selected" : (_required ? "Required" : "Optional");
        public string CardState => HasSelection ? "Success" : (_required ? "Error" : "Neutral");

        public MappedSpecialisationVm(
            string key,
            string subtitle,
            IEnumerable<int> levels,
            Dictionary<string, ColourAbilityDefinition> optionMap,
            string? initialSelection,
            bool required,
            Action onSelectionChanged)
        {
            Key = key;
            Title = key;
            var levelList = levels?.Distinct().OrderBy(x => x).ToList() ?? new List<int>();
            Subtitle = string.IsNullOrWhiteSpace(subtitle)
                ? (levelList.Count > 0 ? $"Lv {string.Join(", ", levelList)}" : "Class specialisation")
                : subtitle;
            _onChanged = onSelectionChanged;
            _required = required;
            _optionMap = new Dictionary<string, ColourAbilityDefinition>(optionMap ?? new Dictionary<string, ColourAbilityDefinition>(), StringComparer.OrdinalIgnoreCase);

            ToggleLevelsCommand = new Command(() => LevelsExpanded = !LevelsExpanded);

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
            RaiseComputed();
        }

        private void UpdatePreview()
        {
            AbilitiesPreview.Clear();
            LevelRows.Clear();
            LevelsExpanded = false;

            if (string.IsNullOrWhiteSpace(_selectedOption) || !_optionMap.TryGetValue(_selectedOption, out var entry))
                return;

            var abilityByLevel = new Dictionary<int, List<string>>();

            var ordered = (entry.Levels ?? new Dictionary<string, List<string>>())
                .Select(kvp => new
                {
                    Key = kvp.Key ?? string.Empty,
                    Level = int.TryParse(kvp.Key, out var n) ? n : int.MaxValue,
                    Abilities = (kvp.Value ?? new List<string>())
                        .Select(x => (x ?? string.Empty).Trim())
                        .Where(x => x.Length > 0)
                        .ToList()
                })
                .Where(x => x.Abilities.Count > 0)
                .OrderBy(x => x.Level)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var lvl in ordered)
            {
                var label = lvl.Level == int.MaxValue ? lvl.Key : $"Lv {lvl.Level}";
                foreach (var ability in lvl.Abilities)
                {
                    AbilitiesPreview.Add(new RaceSubtypePreviewLine
                    {
                        Level = label,
                        Ability = ability
                    });
                }

                if (lvl.Level != int.MaxValue)
                {
                    if (!abilityByLevel.TryGetValue(lvl.Level, out var list))
                    {
                        list = new List<string>();
                        abilityByLevel[lvl.Level] = list;
                    }

                    list.AddRange(lvl.Abilities);
                }
            }

            foreach (var kvp in abilityByLevel.OrderBy(k => k.Key))
            {
                var abilities = string.Join(", ", kvp.Value.Distinct(StringComparer.OrdinalIgnoreCase));
                LevelRows.Add(new RaceSubtypeLevelRow
                {
                    Level = kvp.Key,
                    Abilities = abilities
                });
            }

            LevelsExpanded = LevelRows.Count > 0;
        }

        private void RaiseComputed()
        {
            Raise(nameof(HasSelection));
            Raise(nameof(IsComplete));
            Raise(nameof(StatusText));
            Raise(nameof(CardState));
        }
    }

    private enum ChoiceSource
    {
        Class,
        Race
    }

    private sealed class RequiredChoice
    {
        public ChoiceSource Source { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string SpecialisationKey { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    private sealed class SpecialisationDefinition
    {
        public List<string>? Abilities { get; set; }

        // TableName (e.g. ElfColourAbilities) -> subtypeName (e.g. Winter) -> level ("1") -> abilities
        public Dictionary<string, ColourAbilityDefinition>? ColourAbilities { get; set; }
    }

    public sealed class ColourAbilityDefinition
    {
        public Dictionary<string, List<string>> Levels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string LifeScaleOverride { get; set; } = string.Empty;
    }

    public sealed class RaceSubtypePreviewLine
    {
        public string Level { get; set; } = string.Empty;
        public string Ability { get; set; } = string.Empty;
    }

    public sealed class RaceSubtypeLevelRow
    {
        public int Level { get; init; }
        public string Body { get; init; } = string.Empty;
        public string Loc { get; init; } = string.Empty;
        public string Abilities { get; init; } = string.Empty;
    }
}
