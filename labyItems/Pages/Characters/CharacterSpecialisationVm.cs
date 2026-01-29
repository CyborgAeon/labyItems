using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Infrastructure;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public sealed class CharacterSpecialisationVm : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly CharacterBuilderVm _builder;
    private CharacterDraft Draft => _builder.Draft;
    private readonly ReloadGate _reloadGate = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new GuildOverrideRulesConverter() }
    };

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
    private SpecialisationGroupVm? _baronialTraditionGroup;
    private const string BaronialTraditionKey = "BaronialTradition";
    private const string WizardColourKey = "Wizard Colour";
    private const string BaronialAncestryKey = "Baronial Ancestry";
    private readonly List<SpecialisationGroupVm> _wizardColourGroups = new();
    private readonly List<SpecialisationGroupVm> _vivomancerColourGroups = new();
    private readonly List<SpecialisationGroupVm> _spellCustomisationGroups = new();
    private readonly Dictionary<string, List<AbilityDefinition>> _groupOptionDefinitions = new(StringComparer.OrdinalIgnoreCase);
    private bool _isRefreshingPrereqs;
    private bool _isRefreshingSpellOptions;
    private bool _spellCacheLoaded;
    private List<SpellService.SpellRaw> _spellCache = new();

    private static readonly Dictionary<string, MagicColours> _alfarWizardColourMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Löss light (white)"] = MagicColours.White,
        ["Loss light (white)"] = MagicColours.White,
        ["Svart dark (black)"] = MagicColours.Black,
        ["Alfamir seas (green)"] = MagicColours.Green,
        ["Fjell mountain (brown)"] = MagicColours.Brown
    };

    private static readonly Dictionary<MagicColours, MagicColours?> _faerieOpposites = new()
    {
        [MagicColours.Red] = MagicColours.Green,
        [MagicColours.Green] = MagicColours.Red,
        [MagicColours.Brown] = MagicColours.Blue,
        [MagicColours.Blue] = MagicColours.Brown,
        [MagicColours.White] = MagicColours.Black,
        [MagicColours.Black] = MagicColours.White,
        [MagicColours.Gold] = MagicColours.Bronze,
        [MagicColours.Bronze] = MagicColours.Gold,
        [MagicColours.Ivory] = MagicColours.Ebony,
        [MagicColours.Ebony] = MagicColours.Ivory,
        [MagicColours.Jade] = MagicColours.Onyx,
        [MagicColours.Onyx] = MagicColours.Jade,
        [MagicColours.Grey] = null,
        [MagicColours.Silver] = null
    };

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
    private string _raceSubtypeArmourOverride = string.Empty;
    private List<string> _raceSubtypeColourOverride = new();
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

    public void CancelReloads()
        => _reloadGate.Cancel();

    public void Dispose()
        => _reloadGate.Dispose();

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var reload = _reloadGate.Begin(cancellationToken);

        try
        {
            void EnsureActive() => reload.ThrowIfCancelledOrStale();

            EnsureActive();

            Groups.Clear();
            MappedSpecialisations.Clear();
            _wizardColourGroups.Clear();
            _vivomancerColourGroups.Clear();
            _spellCustomisationGroups.Clear();
            _groupOptionDefinitions.Clear();
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
            _raceSubtypeArmourOverride = string.Empty;
            _raceSubtypeColourOverride = new List<string>();
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
            EnsureActive();
            _specialisationIndex.Clear();
            foreach (var kvp in specialisationIndex)
                _specialisationIndex[kvp.Key] = kvp.Value;

            var required = new List<RequiredChoice>();
            if (cls.Length > 0)
            {
                var allClasses = await ClassService.GetAllAsync();
                EnsureActive();
                if (allClasses.TryGetValue(cls, out var classRec) && classRec != null)
                {
                    foreach (var (kvp, abilityToken) in LoopHelper.Flatten(
                                 classRec.Levels ?? new Dictionary<string, List<AbilityDefinition>>(),
                                 entry => entry.Value ?? new List<AbilityDefinition>()))
                    {
                        if (!int.TryParse(kvp.Key, out var level))
                            continue;

                        // gathers by key, saves to 'required'
                        var key = FindSpecialisationKey(abilityToken.Name, specialisationIndex.Keys);
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

            if (race.Length > 0)
            {
                var allPeople = await PeopleService.GetAllAsync();
                EnsureActive();
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

                        FilterRaceSubtypeOptionsByClass();
                        EnsureDefaultHumanStandardSelection();

                        HasRaceSubtypeChoice = options.Count > 0;

                        var initialSelection = BuildSubtypeInitialSelection();
                        if (initialSelection.TryGetValue(0, out var pre) && !string.IsNullOrWhiteSpace(pre))
                            SelectedRaceSubtype = pre;
                        else
                            SyncRaceSubtypeDraftAndPreview();
                    }

                    foreach (var (kvp, abilityToken) in LoopHelper.Flatten(
                                 raceRec.LevelledAbilities ?? new Dictionary<string, List<AbilityDefinition>>(),
                                 entry => entry.Value ?? new List<AbilityDefinition>()))
                    {
                        if (!int.TryParse(kvp.Key, out var level))
                            continue;

                        var key = FindSpecialisationKey(abilityToken.Name, specialisationIndex.Keys);
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

            var byKey = required
                .GroupBy(r => r.SpecialisationKey, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            EnsureActive();
            foreach (var g in byKey)
            {
                if (!specialisationIndex.TryGetValue(g.Key, out var def) || def == null)
                    continue;

                var title = g.Key;
                var levels = BuildLevelsForGroup(title, g);

                if (def.ColourAbilities != null && def.ColourAbilities.Count > 0)
                {
                    var filtered = FilterOptionMapForClass(def.ColourAbilities);
                    if (filtered.Count == 0)
                        continue;

                    var subtitle = BuildSubtitle(g);
                    var mapped = new MappedSpecialisationVm(
                        key: title,
                        subtitle: string.IsNullOrWhiteSpace(subtitle) ? "Select a subtype to unlock its benefits." : subtitle,
                        levels: levels,
                        optionMap: filtered,
                        initialSelection: GetSavedSpecialisationSelection(title),
                        required: true,
                        onSelectionChanged: OnMappedSpecialisationChanged);

                    MappedSpecialisations.Add(mapped);
                    continue;
                }

                var useMagicColourEnum = IsWizardColour(title);
                var useVivomancerColourEnum = IsVivomancerColour(title);
                var useDictionarySearch = UsesDictionarySearch(title);
                var optionNames = def.Abilities?
                    .Select(a => a?.Name ?? string.Empty)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList() ?? new List<string>();
                if (def.Abilities != null)
                    _groupOptionDefinitions[title] = def.Abilities;
                var optionCustomisations = BuildOptionCustomisations(def.Abilities);
                var hasSpellCustomisation = HasSpellCustomisation(def.Abilities);

                var groupVm = CreateGroupForSpecialisation(title, levels, def.Abilities, isOptional: false);
                Groups.Add(groupVm);
                if (useMagicColourEnum)
                    _wizardColourGroups.Add(groupVm);
                if (useVivomancerColourEnum)
                    _vivomancerColourGroups.Add(groupVm);
                if (hasSpellCustomisation)
                    _spellCustomisationGroups.Add(groupVm);

                groupVm.IsExpanded = true;

                if (hasSpellCustomisation)
                    ApplySingleOptionDefault(groupVm);
            }

            EnsureActive();
            UpdateSpellCustomisationVisibility();
            UpdateDynamicSpecialisations();
            ApplyWardPactOverrides();
            SyncMappedSelectionsToDraft();

            HasChoices = HasRaceSubtypeChoice || Groups.Count > 0 || HasMappedSpecialisations;
            HeaderText = HasChoices ? "Make your selections below." : "No specialisation choices required.";

            EnsureActive();
            await RefreshPrereqOptionsAsync();
            EnsureActive();
            await RefreshSpellCustomisationOptionsAsync();

            EnsureActive();
            RecomputeCompletion();
        }
        catch (OperationCanceledException) when (reload.IsCanceledOrStale)
        {
        }
    }

    private IOptionSource ResolveOptionSource(
    string title,
    List<string> optionNames,
    bool useMagicColourEnum,
    bool useVivomancerColourEnum,
    bool useWardPactEnum,
    bool useDictionarySearch)
    {
        if (useWardPactEnum)
            return new WardPactSource();

        if (useMagicColourEnum)
            return new EnumPickerSource<MagicColours>(GetWizardColourOptions());

        if (useVivomancerColourEnum)
            return new EnumPickerSource<VivomancerColours>(GetVivomancerColourOptions(_specialisationIndex[title]));

        if (useDictionarySearch)
        {
            // Map title → lookup service adapter
            ILookupService svc = title.Trim().Equals("Earth Powers", StringComparison.OrdinalIgnoreCase)
                ? new EarthPowerLookupService()
                : new MiracleLookupService(); // example

            return new DictionarySearchSource(svc, optionNames);
        }

        return new PlainPickerSource(optionNames);
    }


    private void OnAnySelectionChanged()
    {
        SyncWizardColourSelectionToDraft();
        SyncRaceSubtypeDraftAndPreview();
        SyncBaronialSelectionToDraft();
        UpdateSpellCustomisationVisibility();
        _ = RefreshSpellCustomisationOptionsAsync();
        RecomputeCompletion();
        _builder.NotifyGatingChanged();
        _ = _builder.RefreshDraftAbilitiesAsync();
    }

    private void OnMappedSpecialisationChanged()
    {
        SyncMappedSelectionsToDraft();
        SyncWizardColourSelectionToDraft();
        UpdateBaronialAncestryNote();
        RecomputeCompletion();
        _builder.NotifyGatingChanged();
        _ = _builder.RefreshDraftAbilitiesAsync();
    }

    private void SyncMappedSelectionsToDraft()
    {
        var currentKeys = MappedSpecialisations
            .Select(m => m.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toRemove = Draft.SpecialisationSelections.Keys
            .Where(k => !currentKeys.Contains(k) && !IsPersistedNonMappedKey(k))
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

    private static bool IsPersistedNonMappedKey(string? key)
        => string.Equals(key?.Trim(), WizardColourKey, StringComparison.OrdinalIgnoreCase)
           || string.Equals(key?.Trim(), BaronialTraditionKey, StringComparison.OrdinalIgnoreCase);

    private void SyncWizardColourSelectionToDraft()
    {
        var group = Groups.FirstOrDefault(g => string.Equals(g.Title?.Trim(), WizardColourKey, StringComparison.OrdinalIgnoreCase));
        if (group == null)
        {
            Draft.SpecialisationSelections.Remove(WizardColourKey);
            return;
        }

        var picked = group.Slots
            .Select(s => (s.SelectedOption ?? string.Empty).Trim())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(picked))
            Draft.SpecialisationSelections.Remove(WizardColourKey);
        else
            Draft.SpecialisationSelections[WizardColourKey] = picked;
    }

    private void SyncBaronialSelectionToDraft()
    {
        if (_baronialTraditionGroup == null)
        {
            Draft.SpecialisationSelections.Remove(BaronialTraditionKey);
            return;
        }

        var choice = _baronialTraditionGroup.Slots.FirstOrDefault()?.SelectedOption ?? string.Empty;
        choice = choice.Trim();

        if (string.IsNullOrWhiteSpace(choice))
            Draft.SpecialisationSelections.Remove(BaronialTraditionKey);
        else
            Draft.SpecialisationSelections[BaronialTraditionKey] = choice;
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
            await _builder.SyncDraftLifeAsync(expandIfChanged: true);
            UpdateRaceSubtypeCardState(effectivePicked);
            UpdateWizardColourGroups();
            UpdateDynamicSpecialisations();
            ApplyWardPactOverrides();
            SyncMappedSelectionsToDraft();
            SyncWizardColourSelectionToDraft();
            UpdateBaronialTraditionGroup();
            UpdateBaronialAncestryNote();
            SyncBaronialSelectionToDraft();

            RecomputeCompletion();
            _builder.NotifyGatingChanged();
            _ = _builder.RefreshDraftAbilitiesAsync();
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

    private GuildOverrideRules? _selectedGuildOverrides;

    public (List<AbilityDraft> Abilities, GuildOverrideRules? GuildOverrides) BuildSelectedAbilityDraftsWithRules()
    {
        var list = new List<AbilityDraft>();
        _selectedGuildOverrides = null;

        list.AddRange(BuildRaceSubtypeAbilities());

        foreach (var (g, slot) in LoopHelper.Flatten(Groups, group => group.Slots))
        {
            if (!slot.HasSelection)
                continue;

            if (string.Equals(g.Title?.Trim(), "Wizard Colour", StringComparison.OrdinalIgnoreCase))
                continue;

            var forcedDef = slot.ForcedAbilityDefinition;
            if (forcedDef != null)
            {
                var def = ApplyCustomisation(forcedDef, slot.CustomisationValue);
                if (def.GuildOverrides != null)
                    _selectedGuildOverrides = GuildOverrideRules.Merge(_selectedGuildOverrides, GuildOverrideRules.FromLegacyStrings(def.GuildOverrides));
                var parsed = AbilityDraftBuilder.ParseAbility(def, slot.Level);
                ApplyAbilitySource(parsed, $"Specialisation:{g.Title}");
                list.AddRange(parsed);
                continue;
            }

            var ability = (slot.SelectedOption ?? string.Empty).Trim();
            if (ability.Length == 0) continue;

            var fromIndex = ResolveSpecialisationAbilityDefinition(g.Title, ability);
            if (fromIndex != null)
            {
                var def = ApplyCustomisation(fromIndex, slot.CustomisationValue);
                if (def.GuildOverrides != null)
                    _selectedGuildOverrides = GuildOverrideRules.Merge(_selectedGuildOverrides, GuildOverrideRules.FromLegacyStrings(def.GuildOverrides));
                var parsed = AbilityDraftBuilder.ParseAbility(def, slot.Level);
                ApplyAbilitySource(parsed, $"Specialisation:{g.Title}");
                list.AddRange(parsed);
            }
            else
            {
                var name = AppendCustomisation(ability, slot.CustomisationValue);
                var parsed = AbilityDraftBuilder.ParseAbility(name, slot.Level);
                ApplyAbilitySource(parsed, $"Specialisation:{g.Title}");
                list.AddRange(parsed);
            }
        }

        foreach (var (mapped, entry) in LoopHelper.Flatten(MappedSpecialisations, m => m.GetSelectedAbilities()))
        {
            if (entry.AbilityDef != null)
            {
                if (entry.AbilityDef.GuildOverrides != null)
                    _selectedGuildOverrides = GuildOverrideRules.Merge(_selectedGuildOverrides, GuildOverrideRules.FromLegacyStrings(entry.AbilityDef.GuildOverrides));
                var parsed = AbilityDraftBuilder.ParseAbility(entry.AbilityDef, entry.Level);
                ApplyAbilitySource(parsed, $"Specialisation:{mapped.Key}");
                list.AddRange(parsed);
            }
            else if (!string.IsNullOrWhiteSpace(entry.Ability))
            {
                var parsed = AbilityDraftBuilder.ParseAbility(entry.Ability!, entry.Level);
                ApplyAbilitySource(parsed, $"Specialisation:{mapped.Key}");
                list.AddRange(parsed);
            }
        }

        return (list, _selectedGuildOverrides);
    }

    private AbilityDefinition? ResolveSpecialisationAbilityDefinition(string key, string selectedName)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(selectedName))
            return null;

        if (!_specialisationIndex.TryGetValue(key, out var def) || def?.Abilities == null)
            return null;

        return def.Abilities.FirstOrDefault(a => string.Equals(a?.Name, selectedName, StringComparison.OrdinalIgnoreCase));
    }

    private List<AbilityDraft> BuildRaceSubtypeAbilities()
    {
        var list = new List<AbilityDraft>();

        if (!HasRaceSubtypeChoice)
            return list;

        var picked = (SelectedRaceSubtype ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(picked))
            return list;

        if (string.IsNullOrWhiteSpace(_raceSubtypeAbilityMapKey))
            return list;

        if (!_specialisationIndex.TryGetValue(_raceSubtypeAbilityMapKey, out var mapDef) || mapDef?.ColourAbilities == null)
            return list;

        if (!mapDef.ColourAbilities.TryGetValue(picked, out var entry) || entry == null)
            return list;

        if (!IsClassAllowed(entry.ClassRestriction))
            return list;

        if (entry.GuildOverrides != null)
            _selectedGuildOverrides = GuildOverrideRules.Merge(_selectedGuildOverrides, entry.GuildOverrides);

        foreach (var (kvp, ability) in LoopHelper.Flatten(
                     entry.Levels ?? new Dictionary<string, List<AbilityDefinition>>(),
                     entryKvp => entryKvp.Value ?? new List<AbilityDefinition>()))
        {
            var key = (kvp.Key ?? string.Empty).Trim();
            int? level = int.TryParse(key, out var parsed) ? parsed : null;
            var parsedAbility = AbilityDraftBuilder.ParseAbility(ability, level);
            ApplyAbilitySource(parsedAbility, "Race");
            list.AddRange(parsedAbility);
        }

        return list;
    }

    private async Task UpdateRaceSubtypePreviewAsync(string picked)
    {
        RaceSubtypeAbilitiesPreview.Clear();
        RaceSubtypeLevelRows.Clear();
        _raceSubtypeLifeScaleOverride = string.Empty;
        _raceSubtypeArmourOverride = string.Empty;
        _raceSubtypeColourOverride.Clear();
        Draft.LifeScaleKeyOverride = string.Empty;
        Draft.ArmourAvailabilityOverride = string.Empty;
        Draft.ColourChoiceOverride.Clear();
        RaceSubtypeLevelsExpanded = false;
        ShowRaceSubtypeLifeScale = false;

        if (!HasRaceSubtypeChoice)
            return;

        if (string.IsNullOrWhiteSpace(_raceSubtypeAbilityMapKey) || string.IsNullOrWhiteSpace(picked))
            return;

        if (!_specialisationIndex.TryGetValue(_raceSubtypeAbilityMapKey, out var mapDef) || mapDef?.ColourAbilities == null)
            return;

        if (!mapDef.ColourAbilities.TryGetValue(picked, out var entry) || entry == null)
        {
            if (IsBaselineHumanStandard(picked))
                return;

            SelectedRaceSubtype = null;
            return;
        }

        if (!IsClassAllowed(entry.ClassRestriction))
        {
            if (IsBaselineHumanStandard(picked))
                return;

            SelectedRaceSubtype = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(entry.ArmourAvailabilityOverride))
        {
            _raceSubtypeArmourOverride = entry.ArmourAvailabilityOverride.Trim();
            Draft.ArmourAvailabilityOverride = _raceSubtypeArmourOverride;

            RaceSubtypeAbilitiesPreview.Add(new RaceSubtypePreviewLine
            {
                Level = "Armour",
                Ability = $"Armour availability override: {_raceSubtypeArmourOverride}"
            });
        }

        _raceSubtypeColourOverride = (entry.ColourChoiceOverride ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();
        Draft.ColourChoiceOverride.Clear();
        foreach (var colour in _raceSubtypeColourOverride)
            Draft.ColourChoiceOverride.Add(colour);

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

        if (entry.GuildOverrides != null)
        {
            var guildSummary = SummarizeGuildOverrides(entry.GuildOverrides);
            if (!string.IsNullOrWhiteSpace(guildSummary))
            {
                RaceSubtypeAbilitiesPreview.Add(new RaceSubtypePreviewLine
                {
                    Level = "Guild",
                    Ability = guildSummary
                });
            }
        }

        var levels = entry.Levels ?? new Dictionary<string, List<AbilityDefinition>>();
        var abilityByLevel = new Dictionary<int, List<string>>();
        var hasAbilities = false;

        var ordered = levels
            .Select(kvp => new
            {
                Key = kvp.Key ?? string.Empty,
                Level = int.TryParse(kvp.Key, out var n) ? n : int.MaxValue,
                Abilities = (kvp.Value ?? new List<AbilityDefinition>())
                    .Select(ToDisplayName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList()
            })
            .Where(x => x.Abilities.Count > 0)
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (lvl, ability) in LoopHelper.Flatten(ordered, entry => entry.Abilities))
        {
            var label = lvl.Level == int.MaxValue ? lvl.Key : $"Lvl {lvl.Level}: ";
            RaceSubtypeAbilitiesPreview.Add(new RaceSubtypePreviewLine
            {
                Level = label,
                Ability = ability
            });
        }

        foreach (var lvl in ordered)
        {
            if (lvl.Level == int.MaxValue)
                continue;

            if (!abilityByLevel.TryGetValue(lvl.Level, out var list))
            {
                list = new List<string>();
                abilityByLevel[lvl.Level] = list;
            }

            list.AddRange(lvl.Abilities);
            hasAbilities = true;
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

            var includeRow = hasLife || !string.IsNullOrWhiteSpace(abilities);
            if (!includeRow)
                continue;

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

    private static bool UsesDictionarySearch(string groupTitle)
    {
        var title = groupTitle.Trim();
        return string.Equals(title, "Standard Scout skill", StringComparison.OrdinalIgnoreCase)
               || string.Equals(title, "Specialist Scout skill", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyCustomisation(List<AbilityDefinition>? abilities)
    {
        foreach (var ability in abilities ?? new List<AbilityDefinition>())
        {
            var option = (ability?.Customisation?.OptionEnum ?? string.Empty).Trim();
            if (TryParseSpellCustomisation(option, out _))
                return true;
        }

        return false;
    }

    private static bool HasSpellCustomisation(List<AbilityDefinition>? abilities)
    {
        foreach (var ability in abilities ?? new List<AbilityDefinition>())
        {
            var option = (ability?.Customisation?.OptionEnum ?? string.Empty).Trim();
            if (TryParseSpellCustomisation(option, out _))
                return true;
        }

        return false;
    }

    private Dictionary<string, string>? ResolveCustomisationOptions(AbilityCustomisation? customisation)
    {
        if (customisation == null)
            return null;

        if (!TryParseSpellCustomisation(customisation.OptionEnum, out var maxLevel))
            return null;

        return BuildSpellCustomisationOptions(maxLevel);
    }

    private static bool TryParseSpellCustomisation(string? optionEnum, out int maxLevel)
    {
        maxLevel = 0;
        if (string.IsNullOrWhiteSpace(optionEnum))
            return false;

        var trimmed = optionEnum.Trim();
        var parts = trimmed.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        if (!parts[0].Equals("SpellUpTo", StringComparison.OrdinalIgnoreCase)
            && !parts[0].Equals("Spell", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(parts[1], out maxLevel) && maxLevel > 0;
    }

    private void ApplySingleOptionDefault(SpecialisationGroupVm group)
    {
        if (group.OptionNames.Count != 1)
            return;

        var option = group.OptionNames[0];
        foreach (var slot in group.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.SelectedOption))
                slot.SelectedOption = option;
        }
    }

    private static Dictionary<string, AbilityCustomisation> BuildOptionCustomisations(List<AbilityDefinition>? abilities)
    {
        var map = new Dictionary<string, AbilityCustomisation>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in abilities ?? new List<AbilityDefinition>())
        {
            if (ability == null)
                continue;

            var name = (ability.Name ?? string.Empty).Trim();
            if (name.Length == 0 || ability.Customisation == null)
                continue;

            if (!map.ContainsKey(name))
                map[name] = ability.Customisation;
        }

        return map;
    }

    private static bool IsWizardColour(string groupTitle)
    {
        var title = groupTitle.Trim();
        return string.Equals(title, "Wizard Colour", StringComparison.OrdinalIgnoreCase)
               || string.Equals(title, "Faerie Colour", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVivomancerColour(string groupTitle)
        => string.Equals(groupTitle.Trim(), "Vivomancer Colour", StringComparison.OrdinalIgnoreCase);

    private async Task RefreshSpellCustomisationOptionsAsync()
    {
        if (_spellCustomisationGroups.Count == 0 || _isRefreshingSpellOptions)
            return;

        _isRefreshingSpellOptions = true;
        try
        {
            await EnsureSpellCacheAsync();
            var groups = _spellCustomisationGroups.ToList();
            foreach (var group in groups)
                group.RefreshCustomisationOptions();
        }
        finally
        {
            _isRefreshingSpellOptions = false;
        }
    }

    private async Task EnsureSpellCacheAsync()
    {
        if (_spellCacheLoaded)
            return;

        try
        {
            _spellCache = await SpellService.GetAllAsync();
        }
        catch
        {
            _spellCache = new List<SpellService.SpellRaw>();
        }
        finally
        {
            _spellCacheLoaded = true;
        }
    }

    private Dictionary<string, string> BuildSpellCustomisationOptions(int maxLevel)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_spellCache.Count == 0)
            return options;

        var colourFilter = GetSelectedFaerieSpellColours(out var hasFaerieGroup);
        if (hasFaerieGroup && colourFilter.Count == 0)
            return options;

        var filterByColour = colourFilter.Count > 0;

        var spells = _spellCache
            .Where(s => s != null)
            .Where(s => s.level >= 0 && s.level <= maxLevel && (s.isAdvanced ?? false) == false)
            .Where(s => !filterByColour || SpellMatchesColours(s, colourFilter))
            .OrderBy(s => s.level)
            .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase);

        foreach (var spell in spells)
        {
            var label = FormatSpellLabel(spell);
            if (!options.ContainsKey(label))
                options[label] = label;
        }

        return options;
    }

    private static string FormatSpellLabel(SpellService.SpellRaw spell)
    {
        if (spell == null)
            return string.Empty;

        var name = (spell.name ?? string.Empty).Trim();
        if (name.Length == 0)
            return string.Empty;

        return $"{name} (lvl {spell.level})";
    }

    private HashSet<string> GetSelectedFaerieSpellColours(out bool hasFaerieGroup)
    {
        hasFaerieGroup = false;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var group = Groups.FirstOrDefault(g => string.Equals(g.Title, "Faerie Colour", StringComparison.OrdinalIgnoreCase));
        if (group == null)
            return set;

        hasFaerieGroup = true;
        foreach (var slot in group.Slots)
        {
            var normalized = NormalizeSpellColour(slot.SelectedOption);
            if (!string.IsNullOrWhiteSpace(normalized))
                set.Add(normalized);
        }

        return set;
    }

    private static bool SpellMatchesColours(SpellService.SpellRaw spell, HashSet<string> colours)
    {
        if (spell == null || colours == null || colours.Count == 0)
            return true;

        foreach (var colour in SplitSpellColours(spell.colour))
        {
            if (colours.Contains(colour))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> SplitSpellColours(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        var normalized = raw.Replace("&", ",").Replace("/", ",").Replace("|", ",");
        var parts = normalized.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var colour = NormalizeSpellColour(part);
            if (!string.IsNullOrWhiteSpace(colour))
                yield return colour;
        }
    }

    private static string NormalizeSpellColour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private List<MagicColours> GetWizardColourOptions()
    {
        var overrideColours = Draft.ColourChoiceOverride
            .Select(ToMagicColour)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .Distinct()
            .ToList();

        if (overrideColours.Count > 0)
            return overrideColours;

        if (string.Equals(Draft.Race?.Trim(), "Alfar", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(SelectedRaceSubtype)
            && _alfarWizardColourMap.TryGetValue(SelectedRaceSubtype ?? string.Empty, out var alfarColour))
        {
            return new List<MagicColours> { alfarColour };
        }

        return Enum.GetValues<MagicColours>().ToList();
    }

    private List<VivomancerColours> GetVivomancerColourOptions(SpecialisationDefinition def)
    {
        var list = new List<VivomancerColours>();
        foreach (var opt in def?.Abilities ?? new List<AbilityDefinition>())
        {
            var normalized = (opt?.Name ?? string.Empty).Trim();
            if (normalized.Length == 0) continue;

            if (Enum.TryParse<VivomancerColours>(normalized.Replace(" ", ""), ignoreCase: true, out var parsed))
                list.Add(parsed);
        }

        if (list.Count == 0)
            list.AddRange(Enum.GetValues<VivomancerColours>());

        return list
            .Distinct()
            .OrderBy(x => EnumDisplayFormatter.Format(x), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Func<IReadOnlyList<string>, string?>? GetSelectionValidator(string title)
    {
        if (string.Equals(title.Trim(), "Faerie Colour", StringComparison.OrdinalIgnoreCase))
        {
            return picked =>
            {
                if (picked.Count < 2) return null;
                var a = ToMagicColour(picked.ElementAtOrDefault(0));
                var b = ToMagicColour(picked.ElementAtOrDefault(1));
                if (a == null || b == null) return null;

                if (_faerieOpposites.TryGetValue(a.Value, out var opp) && opp is MagicColours opposite && opposite == b)
                    return "Faerie colours cannot be opposite pairs.";

                if (_faerieOpposites.TryGetValue(b.Value, out var oppB) && oppB is MagicColours oppositeB && oppositeB == a)
                    return "Faerie colours cannot be opposite pairs.";

                return null;
            };
        }

        return null;
    }

    private static MagicColours? ToMagicColour(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return null;

        if (Enum.TryParse<MagicColours>(text.Replace(" ", ""), ignoreCase: true, out var parsed))
            return parsed;

        foreach (var colour in Enum.GetValues<MagicColours>())
        {
            var label = EnumDisplayFormatter.Format(colour);
            if (label.Equals(text, StringComparison.OrdinalIgnoreCase))
                return colour;
        }

        return null;
    }

    private static void ApplyAbilitySource(IEnumerable<AbilityDraft> abilities, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            ability.Source = source;
        }
    }

    private void UpdateWizardColourGroups()
    {
        var options = GetWizardColourOptions();
        foreach (var g in _wizardColourGroups)
            g.UpdateMagicColourOptions(options);
    }

    private void UpdateSpellCustomisationVisibility()
    {
        if (_spellCustomisationGroups.Count == 0)
            return;

        var colours = GetSelectedFaerieSpellColours(out var hasFaerieGroup);
        var show = !hasFaerieGroup || colours.Count > 0;

        foreach (var group in _spellCustomisationGroups)
            group.IsVisible = show;
    }

    private void UpdateDynamicSpecialisations()
    {
        RemoveDynamicSpecialisations("Ishmaic Clan", "Ratfolk Clan", BaronialAncestryKey, "Amlesian Caste");

        if (IsIshmaicHumanSelected()
            && _specialisationIndex.TryGetValue("IshmaicClanAbilities", out var ishmaicDef)
            && ishmaicDef?.ColourAbilities != null)
        {
            var required = !IsIshmaicClanOptionalForClass();
            AddDynamicMappedSpecialisation(
                key: "Ishmaic Clan",
                subtitle: "Human • clan choice",
                optionMap: FilterOptionMapForClass(ishmaicDef.ColourAbilities),
                required: required);
        }

        if (IsAmlesianHumanSelected()
            && _specialisationIndex.TryGetValue("AmlesianCasteAbilities", out var amlesianDef)
            && amlesianDef?.ColourAbilities != null)
        {
            AddDynamicMappedSpecialisation(
                key: "Amlesian Caste",
                subtitle: "Amlesian caste (required)",
                optionMap: FilterOptionMapForClass(amlesianDef.ColourAbilities),
                required: true);
        }

        if (string.Equals(Draft.Race?.Trim(), "Ratfolk", StringComparison.OrdinalIgnoreCase)
            && _specialisationIndex.TryGetValue("RatfolkClanAbilities", out var ratClanDef)
            && ratClanDef?.ColourAbilities != null)
        {
            AddDynamicMappedSpecialisation(
                key: "Ratfolk Clan",
                subtitle: "Ratfolk clan (optional)",
                optionMap: FilterOptionMapForClass(ratClanDef.ColourAbilities),
                required: false);
        }

        if (IsBaronialHumanSelected()
            && _specialisationIndex.TryGetValue("BaronialAncestry", out var ancestryDef)
            && ancestryDef?.ColourAbilities != null)
        {
            var filtered = FilterOptionMapForClass(ancestryDef.ColourAbilities);
            if (filtered.Count > 0)
            {
                AddDynamicMappedSpecialisation(
                    key: BaronialAncestryKey,
                    subtitle: "Baronial ancestry (required)",
                    optionMap: filtered,
                    required: true);
            }
        }
    }

    private void ApplyWardPactOverrides()
    {
        var group = Groups.FirstOrDefault(g => string.Equals(g.Title, "Ward pact", StringComparison.OrdinalIgnoreCase));
        if (group == null)
            return;

        var overrides = BuildWardPactOverrides();
        group.ApplyForcedSelections(overrides, def => def.Name ?? string.Empty);
    }

    private Dictionary<int, AbilityDefinition> BuildWardPactOverrides()
    {
        var overrides = new Dictionary<int, AbilityDefinition>();

        if (!string.Equals(Draft.Race?.Trim(), "Ancient Folk", StringComparison.OrdinalIgnoreCase))
            return overrides;

        var picked = (SelectedRaceSubtype ?? string.Empty).Trim();
        if (picked.Length == 0 || string.IsNullOrWhiteSpace(_raceSubtypeAbilityMapKey))
            return overrides;

        if (!_specialisationIndex.TryGetValue(_raceSubtypeAbilityMapKey, out var mapDef) || mapDef?.ColourAbilities == null)
            return overrides;

        if (!mapDef.ColourAbilities.TryGetValue(picked, out var entry) || entry?.Levels == null)
            return overrides;

        foreach (var (kvp, ability) in LoopHelper.Flatten(
                     entry.Levels ?? new Dictionary<string, List<AbilityDefinition>>(),
                     entryKvp => entryKvp.Value ?? new List<AbilityDefinition>()))
        {
            if (!int.TryParse(kvp.Key, out var level))
                continue;

            var overrideDef = BuildWardPactOverride(ability);
            if (overrideDef != null)
                overrides[level] = overrideDef;
        }

        return overrides;
    }

    private static bool IsWardPactOverride(AbilityDefinition? def)
    {
        var name = (def?.Name ?? string.Empty).Trim();
        return name.StartsWith("Ward Pact", StringComparison.OrdinalIgnoreCase);
    }

    private static AbilityDefinition? BuildWardPactOverride(AbilityDefinition? def)
    {
        if (!IsWardPactOverride(def))
            return null;

        var name = (def?.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        return new AbilityDefinition
        {
            Name = name,
            Type = "Static"
        };
    }

    public async Task RefreshPrereqOptionsAsync()
    {
        if (_isRefreshingPrereqs || _groupOptionDefinitions.Count == 0)
            return;

        _isRefreshingPrereqs = true;
        try
        {
            var abilityNames = Draft.Abilities
                .Select(a => (a?.Name ?? string.Empty).Trim())
                .Where(n => n.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var className = (Draft.Class ?? string.Empty).Trim();
            var peopleTypes = await GetCurrentPeopleTypesAsync();

            foreach (var group in Groups)
            {
                if (!_groupOptionDefinitions.TryGetValue(group.Title, out var defs))
                    continue;

                var allowed = defs
                    .Where(d => d != null && MeetsPrereqs(d, abilityNames, peopleTypes, className))
                    .Select(d => d.Name ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                group.UpdateOptionNames(allowed);
                ClearDisallowedSelections(group, allowed);
            }
        }
        finally
        {
            _isRefreshingPrereqs = false;
        }
    }

    private static void ClearDisallowedSelections(SpecialisationGroupVm group, IReadOnlyList<string> allowed)
    {
        foreach (var slot in group.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.SelectedOption))
                continue;

            var match = allowed.Any(a => string.Equals(a, slot.SelectedOption, StringComparison.OrdinalIgnoreCase));
            if (!match)
                slot.SelectedOption = null;
        }
    }

    private async Task<HashSet<string>> GetCurrentPeopleTypesAsync()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var race = (Draft.Race ?? string.Empty).Trim();
        if (race.Length == 0)
            return set;

        var all = await PeopleService.GetAllAsync();
        PeopleRecord? record = null;
        if (all.TryGetValue(race, out var direct))
        {
            record = direct;
        }
        else
        {
            foreach (var kvp in all)
            {
                if (string.Equals(kvp.Key, race, StringComparison.OrdinalIgnoreCase))
                {
                    record = kvp.Value;
                    break;
                }
            }
        }

        if (record?.PeopleType == null)
            return set;

        foreach (var type in record.PeopleType)
        {
            var normalized = NormalizePrereqToken(type);
            if (normalized.Length > 0)
                set.Add(normalized);
        }

        return set;
    }

    private static bool MeetsPrereqs(
        AbilityDefinition def,
        HashSet<string> abilityNames,
        HashSet<string> peopleTypes,
        string className)
    {
        if (def.PreReqs == null || def.PreReqs.Count == 0)
            return true;

        foreach (var raw in def.PreReqs)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var (kind, value) = ParsePrereq(raw);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            switch (kind)
            {
                case "Ability":
                    if (!abilityNames.Contains(value))
                        return false;
                    break;
                case "PeopleType":
                    if (!peopleTypes.Contains(NormalizePrereqToken(value)))
                        return false;
                    break;
                case "Class":
                    if (string.IsNullOrWhiteSpace(className)
                        || !string.Equals(className, value, StringComparison.OrdinalIgnoreCase))
                        return false;
                    break;
            }
        }

        return true;
    }

    private static (string Kind, string Value) ParsePrereq(string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            return ("Ability", string.Empty);

        var parts = text.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            var kind = parts[0];
            if (kind.Equals("Ability", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("PeopleType", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Class", StringComparison.OrdinalIgnoreCase))
                return (kind, parts[1]);
        }

        return ("Ability", text);
    }

    private static string NormalizePrereqToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private bool IsIshmaicHumanSelected()
        => string.Equals(Draft.Race?.Trim(), "Human", StringComparison.OrdinalIgnoreCase)
           && string.Equals(SelectedRaceSubtype?.Trim(), "Ishmaic", StringComparison.OrdinalIgnoreCase);

    private bool IsIshmaicClanOptionalForClass()
    {
        var cls = (Draft.Class ?? string.Empty).Trim();
        return string.Equals(cls, "Kallah Beggar", StringComparison.OrdinalIgnoreCase)
               || string.Equals(cls, "Kallah", StringComparison.OrdinalIgnoreCase)
               || string.Equals(cls, "Hanot Beggar", StringComparison.OrdinalIgnoreCase)
               || string.Equals(cls, "Hannot Beggar", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsBaronialHumanSelected()
        => string.Equals(Draft.Race?.Trim(), "Human", StringComparison.OrdinalIgnoreCase)
           && string.Equals(SelectedRaceSubtype?.Trim(), "Baronial", StringComparison.OrdinalIgnoreCase);

    private bool IsAmlesianHumanSelected()
        => string.Equals(Draft.Race?.Trim(), "Human", StringComparison.OrdinalIgnoreCase)
           && string.Equals(SelectedRaceSubtype?.Trim(), "Amlesian", StringComparison.OrdinalIgnoreCase);

    private void RemoveDynamicSpecialisations(params string[] keys)
    {
        var set = new HashSet<string>(keys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        for (var i = MappedSpecialisations.Count - 1; i >= 0; i--)
        {
            if (set.Contains(MappedSpecialisations[i].Key))
                MappedSpecialisations.RemoveAt(i);
        }
    }

    private void UpdateBaronialTraditionGroup()
    {
        var isHuman = string.Equals(Draft.Race?.Trim(), "Human", StringComparison.OrdinalIgnoreCase);
        var isBaronial = string.Equals(SelectedRaceSubtype?.Trim(), "Baronial", StringComparison.OrdinalIgnoreCase);
        _specialisationIndex.TryGetValue(_raceSubtypeAbilityMapKey, out var mapDef);
        mapDef ??= null;

        var hasEntry = isHuman
                       && isBaronial
                       && mapDef?.ColourAbilities != null
                       && mapDef.ColourAbilities.TryGetValue("Baronial", out var entry)
                       && entry != null
                       && entry.HedgeOrCircle.Count > 0;

        if (!hasEntry)
        {
            if (_baronialTraditionGroup != null)
                Groups.Remove(_baronialTraditionGroup);

            _baronialTraditionGroup = null;
            Draft.SpecialisationSelections.Remove(BaronialTraditionKey);
            return;
        }

        var options = mapDef!.ColourAbilities!["Baronial"].HedgeOrCircle
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var initSelections = new Dictionary<int, string>();
        if (Draft.SpecialisationSelections.TryGetValue(BaronialTraditionKey, out var savedInitial) && !string.IsNullOrWhiteSpace(savedInitial))
            initSelections[1] = savedInitial;

        var classHasPowerBase = ClassHasPowerBase();
        var optional = !classHasPowerBase;

        if (_baronialTraditionGroup == null)
        {
            _baronialTraditionGroup = CreateSingleLevelGroup(
                title: "Baronial tradition",
                optionNames: options,
                savedSelection: Draft.SpecialisationSelections.TryGetValue(BaronialTraditionKey, out var savedSelection) ? savedSelection : null,
                isOptional: optional);

            Groups.Insert(0, _baronialTraditionGroup);
        }
        else
        {
            _baronialTraditionGroup.UpdateOptionNames(options);

            if (Draft.SpecialisationSelections.TryGetValue(BaronialTraditionKey, out var saved)
                && !string.IsNullOrWhiteSpace(saved))
            {
                _baronialTraditionGroup.Slots[0].SelectedOption = saved.Trim();
            }

            _baronialTraditionGroup.IsOptional = optional;
        }
    }

    private SpecialisationGroupVm CreateSingleLevelGroup(
    string title,
    IReadOnlyList<string> optionNames,
    string? savedSelection,
    bool isOptional = false)
    {
        // centralise the “standard” wiring once
        var initSelections = new Dictionary<int, string>();
        if (!string.IsNullOrWhiteSpace(savedSelection))
            initSelections[1] = savedSelection.Trim();

        var optionSource = new PlainPickerSource(optionNames);
        var config = new SpecialisationGroupConfig(
            title,
            new[] { 1 },
            optionSource,
            OnAnySelectionChanged,
            IsOptional: isOptional);

        var group = new SpecialisationGroupVm(config, initSelections);

        group.IsExpanded = true;
        return group;
    }

    private SpecialisationGroupVm CreateGroupForSpecialisation(
        string title,
        IReadOnlyList<int> levels,
        List<AbilityDefinition>? abilities,
        bool isOptional,
        Dictionary<int, string>? initialByLevel = null)
    {
        var optionNames = (abilities ?? new List<AbilityDefinition>())
            .Select(a => a?.Name ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (abilities != null)
            _groupOptionDefinitions[title] = abilities;

        var optionCustomisations = BuildOptionCustomisations(abilities);
        var hasSpellCustomisation = HasSpellCustomisation(abilities);

        var hasSingleOptionWithCustomisation =
            optionNames.Count == 1 &&
            optionCustomisations.ContainsKey(optionNames[0].Trim());

        var useMagicColourEnum = IsWizardColour(title);
        var useVivomancerColourEnum = IsVivomancerColour(title);
        var useDictionarySearch = UsesDictionarySearch(title);
        var useWardPactEnum = UsesInlineDictionarySearch(title);

        var optionSource = ResolveOptionSource(
            title,
            optionNames,
            useMagicColourEnum,
            useVivomancerColourEnum,
            useWardPactEnum,
            useDictionarySearch);

        var config = new SpecialisationGroupConfig(
            title,
            levels,
            optionSource,
            OnAnySelectionChanged,
            SelectionValidator: GetSelectionValidator(title),
            IsOptional: isOptional,
            OptionCustomisations: optionCustomisations,
            CustomisationOptionsProvider: ResolveCustomisationOptions,
            HideAbilityPickerWhenSingleOption: hasSpellCustomisation || hasSingleOptionWithCustomisation);

        var seeded = initialByLevel;
        if (seeded == null && string.Equals(title.Trim(), WizardColourKey, StringComparison.OrdinalIgnoreCase))
        {
            var saved = GetSavedSpecialisationSelection(WizardColourKey);
            if (!string.IsNullOrWhiteSpace(saved))
            {
                var level = levels.FirstOrDefault();
                seeded = new Dictionary<int, string>();
                if (level > 0)
                    seeded[level] = saved.Trim();
            }
        }

        var group = new SpecialisationGroupVm(config, seeded ?? new Dictionary<int, string>());

        group.IsExpanded = true;

        // Track groups that need later updates
        if (useMagicColourEnum) _wizardColourGroups.Add(group);
        if (useVivomancerColourEnum) _vivomancerColourGroups.Add(group);
        if (hasSpellCustomisation) _spellCustomisationGroups.Add(group);

        // Auto-default when needed (spell or strict single-option customisation)
        if (hasSpellCustomisation || hasSingleOptionWithCustomisation)
            ApplySingleOptionDefault(group);

        return group;
    }

    private void AddDynamicMappedSpecialisation(
        string key,
        string subtitle,
        Dictionary<string, ColourAbilityDefinition> optionMap,
        bool required)
    {
        if (optionMap == null || optionMap.Count == 0)
            return;

        var mapped = new MappedSpecialisationVm(
            key: key,
            subtitle: subtitle,
            levels: new[] { 1 },
            optionMap: optionMap,
            initialSelection: GetSavedSpecialisationSelection(key),
            required: required,
            onSelectionChanged: OnMappedSpecialisationChanged);

        mapped.LevelsExpanded = true;
        MappedSpecialisations.Add(mapped);
    }

    private static string ToDisplayName(AbilityDefinition def)
    {
        if (def == null) return string.Empty;

        var name = def.Name ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(def.Effect))
            return $"{name} ({def.Effect})";

        return name;
    }

    private static AbilityDefinition ApplyCustomisation(AbilityDefinition def, string? customValue)
    {
        if (def == null)
            return new AbilityDefinition();

        var cleanedCustom = NormalizeSpellCustomisationValue(def, customValue);
        var updatedName = AppendCustomisation(def.Name ?? string.Empty, cleanedCustom);
        if (string.Equals(updatedName, def.Name ?? string.Empty, StringComparison.Ordinal))
            return def;

        return new AbilityDefinition
        {
            Name = updatedName,
            Type = def.Type ?? string.Empty,
            BattleboardNameOverride = def.BattleboardNameOverride,
            UpdateKey = def.UpdateKey,
            Effect = def.Effect,
            Source = def.Source,
            Count = def.Count,
            Frequency = def.Frequency,
            OverwriteKey = def.OverwriteKey,
            PreReqs = def.PreReqs?.ToList(),
            GuildOverrides = def.GuildOverrides?.ToList(),
            Customisation = def.Customisation
        };
    }

    private static string? NormalizeSpellCustomisationValue(AbilityDefinition? def, string? customValue)
    {
        if (def?.Customisation == null)
            return customValue;

        if (!TryParseSpellCustomisation(def.Customisation.OptionEnum, out _))
            return customValue;

        var text = (customValue ?? string.Empty).Trim();
        if (text.Length == 0)
            return text;

        text = Regex.Replace(text, @"\s*\(lvl\s*\d+\)\s*$", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s*lvl\s*\d+\s*$", string.Empty, RegexOptions.IgnoreCase);
        return text.Trim();
    }

    private static string AppendCustomisation(string name, string? customValue)
    {
        var baseName = (name ?? string.Empty).Trim();
        var custom = (customValue ?? string.Empty).Trim();
        if (custom.Length == 0)
            return baseName;

        if (baseName.Length == 0)
            return custom;

        if (baseName.Contains(custom, StringComparison.OrdinalIgnoreCase))
            return baseName;

        return $"{baseName} ({custom})";
    }

    private bool IsClassAllowed(IEnumerable<string>? restrictions)
    {
        var className = (Draft.Class ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(className)) return true;

        var list = restrictions?
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .ToList() ?? new List<string>();

        if (list.Count == 0)
            return true;

        var clsNorm = className.ToLowerInvariant();
        foreach (var r in list)
        {
            var norm = r.ToLowerInvariant();
            if (clsNorm.Equals(norm))
                return true;

            var singular = norm.EndsWith("s") ? norm.TrimEnd('s') : norm;
            if (clsNorm.Equals(singular))
                return true;

            if (clsNorm.Contains(norm) || norm.Contains(clsNorm))
                return true;
        }

        return false;
    }

    private Dictionary<string, ColourAbilityDefinition> FilterOptionMapForClass(Dictionary<string, ColourAbilityDefinition>? optionMap)
    {
        var result = new Dictionary<string, ColourAbilityDefinition>(StringComparer.OrdinalIgnoreCase);
        if (optionMap == null)
            return result;

        foreach (var kvp in optionMap)
        {
            if (kvp.Value != null && IsClassAllowed(kvp.Value.ClassRestriction))
                result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    private void FilterRaceSubtypeOptionsByClass()
    {
        if (RaceSubtypeOptions.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(_raceSubtypeAbilityMapKey))
            return;

        if (!_specialisationIndex.TryGetValue(_raceSubtypeAbilityMapKey, out var mapDef) || mapDef?.ColourAbilities == null)
            return;

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in RaceSubtypeOptions.ToList())
        {
            if (mapDef.ColourAbilities.TryGetValue(option, out var entry))
            {
                if (IsClassAllowed(entry.ClassRestriction))
                    allowed.Add(option);
            }
            else
            {
                allowed.Add(option);
            }
        }

        // Always allow the baseline human subtype "Standard" so it cannot be filtered out
        // by mis-parsed restrictions or missing map entries.
        if (string.Equals(_currentRaceForSubtype, "Human", StringComparison.OrdinalIgnoreCase))
            allowed.Add("Standard");

        if (allowed.Count == 0)
        {
            RaceSubtypeOptions.Clear();
            SelectedRaceSubtype = null;
            HasRaceSubtypeChoice = false;
            return;
        }

        for (var i = RaceSubtypeOptions.Count - 1; i >= 0; i--)
        {
            if (!allowed.Contains(RaceSubtypeOptions[i]))
                RaceSubtypeOptions.RemoveAt(i);
        }

        if (!string.IsNullOrWhiteSpace(SelectedRaceSubtype) && !allowed.Contains(SelectedRaceSubtype!))
            SelectedRaceSubtype = null;

        HasRaceSubtypeChoice = RaceSubtypeOptions.Count > 0;
        EnsureDefaultHumanStandardSelection();
    }

    private void EnsureDefaultHumanStandardSelection()
    {
        if (!string.Equals(_currentRaceForSubtype, "Human", StringComparison.OrdinalIgnoreCase))
            return;

        // Make sure Standard remains in the options list
        if (!RaceSubtypeOptions.Contains("Standard", StringComparer.OrdinalIgnoreCase))
            RaceSubtypeOptions.Add("Standard");

        if (!string.IsNullOrWhiteSpace(SelectedRaceSubtype))
            return;

        var standard = RaceSubtypeOptions.FirstOrDefault(o => string.Equals(o, "Standard", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(standard))
            SelectedRaceSubtype = standard;
    }

    private bool IsBaselineHumanStandard(string picked)
    {
        return string.Equals(_currentRaceForSubtype, "Human", StringComparison.OrdinalIgnoreCase)
               && string.Equals(picked, "Standard", StringComparison.OrdinalIgnoreCase);
    }

    private bool ClassHasPowerBase()
    {
        var cls = (Draft.Class ?? string.Empty).Trim();
        if (cls.Length == 0) return false;

        var match = _builder.AllClasses.FirstOrDefault(c => string.Equals(c.Name, cls, StringComparison.OrdinalIgnoreCase))
                    ?? _builder.AllClasses.FirstOrDefault(c => string.Equals(c.Key, cls, StringComparison.OrdinalIgnoreCase));

        var powerBase = (match?.PowerBase ?? string.Empty).Trim();
        return powerBase.Length > 0;
    }

    private void UpdateBaronialAncestryNote()
    {
        var notes = Draft.Notes ?? string.Empty;
        var prefix = "Baronial Ancestry:";
        var lines = notes.Split('\n').Select(l => l.TrimEnd()).ToList();
        lines.RemoveAll(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (IsBaronialHumanSelected()
            && Draft.SpecialisationSelections.TryGetValue(BaronialAncestryKey, out var ancestry)
            && !string.IsNullOrWhiteSpace(ancestry))
        {
            lines.Add($"{prefix} {ancestry.Trim()}");
        }

        Draft.Notes = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

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

    private static List<int> BuildLevelsForGroup(string key, IEnumerable<RequiredChoice> grouped)
    {
        var levels = grouped.Select(x => x.Level).ToList();
        if (AllowsDuplicateSlots(key))
            return levels.OrderBy(x => x).ToList();

        return levels.Distinct().OrderBy(x => x).ToList();
    }

    private static bool AllowsDuplicateSlots(string key)
        => string.Equals(key.Trim(), "Faerie Colour", StringComparison.OrdinalIgnoreCase);

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
        => ReflectionHelper.FindEnumTypeByName(enumName);


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

        var optionSource = new PlainPickerSource(options);
        var config = new SpecialisationGroupConfig(
            title,
            new[] { 0 },
            optionSource,
            OnAnySelectionChanged);

        var group = new SpecialisationGroupVm(config, BuildSubtypeInitialSelection());

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

    private static string SummarizeGuildOverrides(GuildOverrideRules rules)
    {
        var parts = new List<string>();
        if (rules.IsCityBound) parts.Add("City bound");

        AddChannelSummary(parts, "Political", rules.Political);
        AddChannelSummary(parts, "Professional", rules.Professional);
        AddChannelSummary(parts, "Social", rules.Social);

        return string.Join("; ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static void AddChannelSummary(List<string> parts, string label, GuildOverrideChannel? channel)
    {
        if (channel == null) return;

        if (channel.CanJoin == false)
        {
            parts.Add($"No {label.ToLowerInvariant()} guilds");
            return;
        }

        if (channel.ReplacedBy is { Count: > 0 })
            parts.Add($"{label} replaced by {string.Join(", ", channel.ReplacedBy)}");

        if (!string.IsNullOrWhiteSpace(channel.GuildPeople))
            parts.Add($"{label} limited to {channel.GuildPeople}");
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
                def.Abilities = ParseAbilityArray(abilitiesEl);
            }
            else if (prop.Value.TryGetProperty("Options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array)
            {
                def.Abilities = ParseAbilityArray(optionsEl);
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
            if (colourProp.Value.ValueKind != JsonValueKind.Object && colourProp.Value.ValueKind != JsonValueKind.Array)
                continue;

            var entry = new ColourAbilityDefinition();

            if (colourProp.Value.ValueKind == JsonValueKind.Array)
            {
                entry.Levels["1"] = ParseAbilityArray(colourProp.Value);
                if (entry.Levels.Count > 0)
                    outer[colourProp.Name] = entry;
                continue;
            }

            if (colourProp.Value.TryGetProperty("Description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                entry.Description = descEl.GetString() ?? string.Empty;
            if (colourProp.Value.TryGetProperty("LifeScaleOverride", out var lsEl) && lsEl.ValueKind == JsonValueKind.String)
                entry.LifeScaleOverride = lsEl.GetString() ?? string.Empty;
            if (colourProp.Value.TryGetProperty("ArmourAvailabilityOverride", out var armourEl) && armourEl.ValueKind == JsonValueKind.String)
                entry.ArmourAvailabilityOverride = armourEl.GetString() ?? string.Empty;
            if (colourProp.Value.TryGetProperty("ColourChoiceOverride", out var colourOverrideEl) && colourOverrideEl.ValueKind == JsonValueKind.Array)
            {
                entry.ColourChoiceOverride = colourOverrideEl
                    .EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .ToList();
            }

            if (colourProp.Value.TryGetProperty("GuildOverrides", out var guildOverrideEl))
            {
                entry.GuildOverrides = GuildOverrideRulesConverter.FromElement(guildOverrideEl, _jsonOptions);
            }

            if (colourProp.Value.TryGetProperty("HedgeOrCircle", out var hedgeEl) && hedgeEl.ValueKind == JsonValueKind.Array)
            {
                entry.HedgeOrCircle = hedgeEl
                    .EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .ToList();
            }

            if (colourProp.Value.TryGetProperty("ClassRestriction", out var classRestrictEl) && classRestrictEl.ValueKind == JsonValueKind.Array)
            {
                entry.ClassRestriction = classRestrictEl
                    .EnumerateArray()
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => x.Length > 0)
                    .ToList();
            }

            // Preferred shape: colour -> { Levels: { "1": [..], ... } }
            if (colourProp.Value.TryGetProperty("Levels", out var levelsEl) && levelsEl.ValueKind == JsonValueKind.Object)
                entry.Levels = ParseLevelArrays(levelsEl);
            else
                entry.Levels = ParseLevelArrays(colourProp.Value);

            if (entry.Levels.Count > 0
                || entry.Description.Length > 0
                || entry.LifeScaleOverride.Length > 0
                || entry.ArmourAvailabilityOverride.Length > 0
                || entry.ColourChoiceOverride.Count > 0
                || entry.GuildOverrides != null
                || entry.HedgeOrCircle.Count > 0
                || entry.ClassRestriction.Count > 0)
                outer[colourProp.Name] = entry;
        }

        return outer.Count > 0 ? outer : null;
    }

    private static List<AbilityDefinition> ParseAbilityArray(JsonElement array)
    {
        var list = new List<AbilityDefinition>();
        if (array.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in array.EnumerateArray())
        {
            var def = ParseAbilityDefinition(item);
            if (!string.IsNullOrWhiteSpace(def.Name))
                list.Add(def);
        }

        return list;
    }

    private static AbilityDefinition ParseAbilityDefinition(JsonElement el)
    {
        try
        {
            return JsonSerializer.Deserialize<AbilityDefinition>(el.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AbilityDefinition();
        }
        catch
        {
            var fallback = el.ValueKind switch
            {
                JsonValueKind.String => new AbilityDefinition { Name = el.GetString() ?? string.Empty },
                _ => new AbilityDefinition { Name = el.GetRawText() }
            };
            return fallback;
        }
    }

    private static Dictionary<string, List<AbilityDefinition>> ParseLevelArrays(JsonElement levelsObject)
    {
        var levels = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase);

        if (levelsObject.ValueKind != JsonValueKind.Object)
            return levels;

        foreach (var lvlProp in levelsObject.EnumerateObject())
        {
            if (lvlProp.Value.ValueKind != JsonValueKind.Array)
                continue;

            var list = ParseAbilityArray(lvlProp.Value);

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

        public IEnumerable<(int? Level, string Ability, AbilityDefinition? AbilityDef)> GetSelectedAbilities()
        {
            if (string.IsNullOrWhiteSpace(_selectedOption))
                yield break;

            if (!_optionMap.TryGetValue(_selectedOption, out var entry) || entry == null)
                yield break;

            foreach (var (kvp, ability) in LoopHelper.Flatten(
                         entry.Levels ?? new Dictionary<string, List<AbilityDefinition>>(),
                         entryKvp => entryKvp.Value ?? new List<AbilityDefinition>()))
            {
                var key = (kvp.Key ?? string.Empty).Trim();
                int? level = int.TryParse(key, out var parsed) ? parsed : null;
                yield return (level, ability.Name, ability);
            }
        }

        private void UpdatePreview()
        {
            AbilitiesPreview.Clear();
            LevelRows.Clear();
            LevelsExpanded = false;

            if (string.IsNullOrWhiteSpace(_selectedOption) || !_optionMap.TryGetValue(_selectedOption, out var entry))
                return;

            var abilityByLevel = new Dictionary<int, List<string>>();

            var ordered = (entry.Levels ?? new Dictionary<string, List<AbilityDefinition>>())
                .Select(kvp => new
                {
                    Key = kvp.Key ?? string.Empty,
                    Level = int.TryParse(kvp.Key, out var n) ? n : int.MaxValue,
                    Abilities = (kvp.Value ?? new List<AbilityDefinition>())
                        .Select(ToDisplayName)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList()
                })
                .Where(x => x.Abilities.Count > 0)
                .OrderBy(x => x.Level)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var (lvl, ability) in LoopHelper.Flatten(ordered, entry => entry.Abilities))
            {
                var label = lvl.Level == int.MaxValue ? lvl.Key : $"Lv {lvl.Level}";
                AbilitiesPreview.Add(new RaceSubtypePreviewLine
                {
                    Level = label,
                    Ability = ability
                });
            }

            foreach (var lvl in ordered)
            {
                if (lvl.Level == int.MaxValue)
                    continue;

                if (!abilityByLevel.TryGetValue(lvl.Level, out var list))
                {
                    list = new List<string>();
                    abilityByLevel[lvl.Level] = list;
                }

                list.AddRange(lvl.Abilities);
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

            if (LevelRows.Count == 0 && !string.IsNullOrWhiteSpace(entry.Description))
            {
                LevelRows.Add(new RaceSubtypeLevelRow
                {
                    Level = 1,
                    Abilities = entry.Description.Trim()
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
        public List<AbilityDefinition>? Abilities { get; set; }

        // TableName (e.g. ElfColourAbilities) -> subtypeName (e.g. Winter) -> level ("1") -> abilities
        public Dictionary<string, ColourAbilityDefinition>? ColourAbilities { get; set; }
    }

    public sealed class ColourAbilityDefinition
    {
        public Dictionary<string, List<AbilityDefinition>> Levels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Description { get; set; } = string.Empty;
        public string LifeScaleOverride { get; set; } = string.Empty;
        public string ArmourAvailabilityOverride { get; set; } = string.Empty;
        public List<string> ColourChoiceOverride { get; set; } = new();
        public GuildOverrideRules? GuildOverrides { get; set; }
        public List<string> HedgeOrCircle { get; set; } = new();
        public List<string> ClassRestriction { get; set; } = new();
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
