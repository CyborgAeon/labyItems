using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Infrastructure;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;
using labyItems.Services.Specialisations;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public sealed class CharacterSpecialisationVm : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly CharacterBuilderVm _builder;
    private CharacterDraft Draft => _builder.Draft;
    private readonly ReloadGate _reloadGate = new();
    private readonly SemaphoreSlim _selectionRecalcLock = new(1, 1);

    private SpecialisationIndex _specialisationIndex = new();
    private IReadOnlyDictionary<string, CharacterClassRecord> _allClasses = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, PeopleRecord> _allRaces = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);
    private CharacterSpecialisationContext? _context;
    private IReadOnlyList<RequiredChoice> _requiredChoices = Array.Empty<RequiredChoice>();
    private IReadOnlyList<SpecialisationSectionSpec> _sectionSpecs = Array.Empty<SpecialisationSectionSpec>();
    private readonly Dictionary<string, SpecialisationSectionSpec> _specBySectionId = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _spellCustomisationSectionIds = new(StringComparer.OrdinalIgnoreCase);
    private SpecialisationScreenState? _screenState;

    private bool _isApplyingState;
    private bool _isRefreshingPrereqs;
    private bool _isRefreshingSpellOptions;
    private bool _spellCacheLoaded;
    private List<SpellService.SpellRaw> _spellCache = new();

    private static readonly Dictionary<string, MagicColours> AlfarWizardColourMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Löss light (white)"] = MagicColours.White,
        ["Loss light (white)"] = MagicColours.White,
        ["Svart dark (black)"] = MagicColours.Black,
        ["Alfamir seas (green)"] = MagicColours.Green,
        ["Fjell mountain (brown)"] = MagicColours.Brown
    };

    public ObservableCollection<ISpecialisationSectionVm> Sections { get; } = new();
    public IReadOnlyList<SpecialisationGroupVm> Groups => Sections.OfType<SpecialisationGroupVm>().ToList();
    public IReadOnlyList<MappedSpecialisationSectionVm> MappedSpecialisations =>
        Sections.OfType<MappedSpecialisationSectionVm>()
            .Where(section => section.SectionType != SpecialisationSectionType.RaceSubtype)
            .ToList();
    public bool HasMappedSpecialisations => MappedSpecialisations.Count > 0;

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

    public bool HasChoices => Sections.Any(section => section.IsVisible);
    public bool HasNoChoices => !HasChoices;

    private bool _isComplete;
    public bool IsComplete
    {
        get => _isComplete;
        private set => Set(ref _isComplete, value);
    }

    public string SelectedRaceSubtype => _screenState?.RaceSubtypeValue ?? string.Empty;
    public string RaceSubtypeDetailKey => _screenState?.RaceSubtypeAbilityMapKey ?? string.Empty;
    public bool HasRaceSubtypeDetail => !string.IsNullOrWhiteSpace(RaceSubtypeDetailKey);

    public CharacterSpecialisationVm(CharacterBuilderVm builder)
    {
        _builder = builder;
        Sections.CollectionChanged += (_, _) =>
        {
            Raise(nameof(HasChoices));
            Raise(nameof(HasNoChoices));
            Raise(nameof(Groups));
            Raise(nameof(MappedSpecialisations));
            Raise(nameof(HasMappedSpecialisations));
        };
    }

    public void CancelReloads()
        => _reloadGate.Cancel();

    public void Dispose()
    {
        _reloadGate.Dispose();
        _selectionRecalcLock.Dispose();
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var reload = _reloadGate.Begin(cancellationToken);

        try
        {
            void EnsureActive() => reload.ThrowIfCancelledOrStale();

            EnsureActive();
            Sections.Clear();
            _specBySectionId.Clear();
            _spellCustomisationSectionIds.Clear();
            _screenState = null;
            IsComplete = false;

            _specialisationIndex = await SpecialisationDefinitionRepository.GetIndexAsync();
            EnsureActive();
            _allClasses = await ClassService.GetAllAsync();
            EnsureActive();
            _allRaces = await PeopleService.GetAllAsync();
            EnsureActive();

            _context = CharacterSpecialisationScreenCalculator.LoadContext(
                Draft,
                _allClasses,
                _allRaces,
                _specialisationIndex.Definitions,
                _specialisationIndex.InjectionRules);

            _requiredChoices = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(_context);
            _sectionSpecs = CharacterSpecialisationScreenCalculator.BuildScreenSections(_context, _requiredChoices);
            var initialScreen = CharacterSpecialisationScreenCalculator.ApplySavedSelections(_context, _sectionSpecs);

            ApplyScreenState(initialScreen, preserveExpanded: false);

            EnsureActive();
            await RefreshSpellCustomisationOptionsAsync();
            EnsureActive();
            await RefreshPrereqOptionsAsync();
            EnsureActive();

            RecomputeCompletion();
            _builder.NotifyGatingChanged();
        }
        catch (OperationCanceledException) when (reload.IsCanceledOrStale)
        {
        }
    }

    private void ApplyScreenState(SpecialisationScreenState screenState, bool preserveExpanded)
    {
        var expandedBySection = preserveExpanded
            ? Sections.ToDictionary(section => section.SectionId, section => section.IsExpanded, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        _isApplyingState = true;
        try
        {
            _screenState = screenState;
            _specBySectionId.Clear();
            _spellCustomisationSectionIds.Clear();
            Sections.Clear();

            foreach (var sectionState in screenState.Sections)
            {
                var spec = sectionState.Spec;
                _specBySectionId[spec.SectionId] = spec;

                switch (spec.Kind)
                {
                    case SpecialisationSectionKind.Choice:
                    {
                        var choiceVm = BuildChoiceSection(spec, sectionState);
                        if (expandedBySection.TryGetValue(spec.SectionId, out var expanded))
                            choiceVm.IsExpanded = expanded;
                        Sections.Add(choiceVm);

                        if (HasSpellCustomisation(choiceVm))
                            _spellCustomisationSectionIds.Add(choiceVm.SectionId);
                        break;
                    }

                    case SpecialisationSectionKind.Mapped:
                    case SpecialisationSectionKind.RaceSubtype:
                    {
                        var mappedVm = BuildMappedSection(spec, sectionState);
                        if (expandedBySection.TryGetValue(spec.SectionId, out var expanded))
                            mappedVm.IsExpanded = expanded;
                        Sections.Add(mappedVm);
                        break;
                    }
                }
            }

            ApplyDraftState(screenState);
            RecomputeCompletion();

            Raise(nameof(SelectedRaceSubtype));
            Raise(nameof(RaceSubtypeDetailKey));
            Raise(nameof(HasRaceSubtypeDetail));
            Raise(nameof(HasChoices));
            Raise(nameof(HasNoChoices));
        }
        finally
        {
            _isApplyingState = false;
        }
    }

    private SpecialisationGroupVm BuildChoiceSection(SpecialisationSectionSpec spec, SpecialisationSectionState state)
    {
        var optionNames = spec.Options
            .Select(option => option.Label)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var optionCustomisations = BuildOptionCustomisations(spec.Options);
        var hasSpellCustomisation = optionCustomisations.Values.Any(custom => TryParseSpellCustomisation(custom.OptionEnum, out _));
        var hasSingleOptionWithCustomisation = optionNames.Count == 1 && optionCustomisations.ContainsKey(optionNames[0]);

        var optionSource = ResolveOptionSource(spec, optionNames);
        var config = new SpecialisationGroupConfig(
            spec.Title,
            spec.Levels,
            optionSource,
            OnSectionSelectionChanged,
            SelectionValidator: GetSelectionValidator(spec),
            IsOptional: !spec.Required,
            OptionCustomisations: optionCustomisations,
            CustomisationOptionsProvider: ResolveCustomisationOptions,
            HideAbilityPickerWhenSingleOption: hasSpellCustomisation || hasSingleOptionWithCustomisation);

        var initialByLevel = state.SelectedByLevel.ToDictionary(k => k.Key, v => v.Value);
        var group = new SpecialisationGroupVm(config, initialByLevel);
        group.ConfigureSectionMetadata(spec.SectionId, spec.DetailKey, spec.StrategyIds, SpecialisationSectionType.Choice);
        group.IsExpanded = true;

        if (hasSpellCustomisation || hasSingleOptionWithCustomisation)
            ApplySingleOptionDefault(group);

        if (!string.IsNullOrWhiteSpace(state.ValidationMessage))
            group.SetIssueMessage(state.ValidationMessage);

        return group;
    }

    private MappedSpecialisationSectionVm BuildMappedSection(SpecialisationSectionSpec spec, SpecialisationSectionState state)
    {
        var sectionType = spec.Kind == SpecialisationSectionKind.RaceSubtype
            ? SpecialisationSectionType.RaceSubtype
            : SpecialisationSectionType.Mapped;

        var mapped = new MappedSpecialisationSectionVm(
            sectionId: spec.SectionId,
            key: spec.DefinitionKey,
            detailKey: spec.DetailKey,
            title: spec.Title,
            subtitle: spec.Subtitle,
            options: spec.Options,
            initialSelection: state.SelectedOption,
            required: spec.Required,
            onSelectionChanged: OnSectionSelectionChanged,
            sectionType: sectionType,
            selectionIssueResolver: ResolveMappedSelectionIssue);

        mapped.IsExpanded = true;
        return mapped;
    }

    private IOptionSource ResolveOptionSource(SpecialisationSectionSpec spec, List<string> optionNames)
    {
        if (HasStrategy(spec, "lookup:ward-pact"))
            return new WardPactSource();

        if (HasStrategy(spec, "options:magic-colour"))
            return new EnumPickerSource<MagicColours>(GetWizardColourOptions());

        if (HasStrategy(spec, "options:vivomancer-colour"))
            return new EnumPickerSource<VivomancerColours>(GetVivomancerColourOptions(spec));

        if (HasStrategy(spec, "lookup:earth-power"))
            return new DictionarySearchSource(new EarthPowerLookupService(), optionNames);

        if (HasStrategy(spec, "lookup:spell"))
            return new DictionarySearchSource(new SpellLookupService(), optionNames);

        if (HasStrategy(spec, "lookup:scout-skill"))
            return new DictionarySearchSource(new MiracleLookupService(), optionNames);

        return new PlainPickerSource(optionNames);
    }

    private static Dictionary<string, AbilityCustomisation> BuildOptionCustomisations(IEnumerable<ChoiceOption> options)
    {
        var map = new Dictionary<string, AbilityCustomisation>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in options ?? Array.Empty<ChoiceOption>())
        {
            if (option?.Customisation == null)
                continue;

            var key = (option.Label ?? option.Key ?? string.Empty).Trim();
            if (key.Length == 0)
                continue;

            map[key] = new AbilityCustomisation
            {
                OptionEnum = option.Customisation.OptionEnum,
                CustomValuesPermitted = option.Customisation.CustomValuesPermitted
            };
        }

        return map;
    }

    private static bool HasStrategy(SpecialisationSectionSpec spec, string strategyId)
        => spec.StrategyIds.Any(id => id.Equals(strategyId, StringComparison.OrdinalIgnoreCase));

    private static void ApplySingleOptionDefault(SpecialisationGroupVm group)
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

    private static Func<IReadOnlyList<string>, string?>? GetSelectionValidator(SpecialisationSectionSpec spec)
    {
        if (!HasStrategy(spec, "validation:faerie-opposites"))
            return null;

        return picked =>
        {
            if (picked.Count < 2)
                return null;

            var first = ToMagicColour(picked[0]);
            var second = ToMagicColour(picked[1]);
            if (first == null || second == null)
                return null;

            var conflict = (first, second) switch
            {
                (MagicColours.Red, MagicColours.Green) => true,
                (MagicColours.Green, MagicColours.Red) => true,
                (MagicColours.Brown, MagicColours.Blue) => true,
                (MagicColours.Blue, MagicColours.Brown) => true,
                (MagicColours.White, MagicColours.Black) => true,
                (MagicColours.Black, MagicColours.White) => true,
                (MagicColours.Gold, MagicColours.Bronze) => true,
                (MagicColours.Bronze, MagicColours.Gold) => true,
                (MagicColours.Ivory, MagicColours.Ebony) => true,
                (MagicColours.Ebony, MagicColours.Ivory) => true,
                (MagicColours.Jade, MagicColours.Onyx) => true,
                (MagicColours.Onyx, MagicColours.Jade) => true,
                _ => false
            };

            return conflict ? "Faerie colours cannot be opposite pairs." : null;
        };
    }

    private static MagicColours? ToMagicColour(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return null;

        if (Enum.TryParse<MagicColours>(text.Replace(" ", string.Empty), ignoreCase: true, out var parsed))
            return parsed;

        return null;
    }

    private List<MagicColours> GetWizardColourOptions()
    {
        var overrideColours = Draft.ColourChoiceOverride
            .Select(ToMagicColour)
            .Where(colour => colour.HasValue)
            .Select(colour => colour!.Value)
            .Distinct()
            .ToList();

        if (overrideColours.Count > 0)
            return overrideColours;

        if (string.Equals(Draft.Race?.Trim(), "Alfar", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(SelectedRaceSubtype)
            && AlfarWizardColourMap.TryGetValue(SelectedRaceSubtype, out var alfarColour))
        {
            return new List<MagicColours> { alfarColour };
        }

        return Enum.GetValues<MagicColours>().ToList();
    }

    private static List<VivomancerColours> GetVivomancerColourOptions(SpecialisationSectionSpec spec)
    {
        var list = new List<VivomancerColours>();
        foreach (var option in spec.Options ?? Array.Empty<ChoiceOption>())
        {
            var normalized = (option.Label ?? option.Key ?? string.Empty).Trim();
            if (normalized.Length == 0)
                continue;

            if (Enum.TryParse<VivomancerColours>(normalized.Replace(" ", string.Empty), ignoreCase: true, out var parsed))
                list.Add(parsed);
        }

        if (list.Count == 0)
            list.AddRange(Enum.GetValues<VivomancerColours>());

        return list.Distinct().ToList();
    }

    private string ResolveMappedSelectionIssue(ChoiceOption? option)
    {
        if (option == null)
            return string.Empty;

        var classAllowed = IsClassAllowed(option.Restrictions.ClassRestriction);
        var alignmentAllowed = IsAlignmentAllowed(option.Restrictions.AlignmentRestriction);

        if (classAllowed && alignmentAllowed)
            return string.Empty;

        if (!classAllowed && !alignmentAllowed)
            return "Class and alignment requirements conflict with the current character.";
        if (!alignmentAllowed)
            return "Alignment requirements conflict with the current character.";
        return "Class requirements conflict with the current character.";
    }

    private async void OnSectionSelectionChanged()
    {
        if (_isApplyingState)
            return;

        try
        {
            await RecalculateFromCurrentSelectionsAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Specialisation recalculation failed: {ex}");
        }
    }

    private async Task RecalculateFromCurrentSelectionsAsync()
    {
        await _selectionRecalcLock.WaitAsync();
        try
        {
            if (_specialisationIndex.Definitions.Count == 0)
                return;

            var selectionState = BuildSelectionStateFromSections();
            var context = CharacterSpecialisationScreenCalculator.LoadContext(
                Draft,
                _allClasses,
                _allRaces,
                _specialisationIndex.Definitions,
                _specialisationIndex.InjectionRules);

            context = new CharacterSpecialisationContext
            {
                Draft = context.Draft,
                Race = context.Race,
                Class = context.Class,
                ClassRecord = context.ClassRecord,
                RaceRecord = context.RaceRecord,
                Definitions = context.Definitions,
                InjectionRules = context.InjectionRules,
                CurrentRaceSubtype = selectionState.RaceSubtype
            };

            _context = context;
            _requiredChoices = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
            _sectionSpecs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, _requiredChoices);

            var raceSubtypeSpec = _sectionSpecs.FirstOrDefault(spec => spec.Kind == SpecialisationSectionKind.RaceSubtype);
            var raceSubtypeKey = raceSubtypeSpec?.Metadata.TryGetValue("raceSubtypeKey", out var subtypeKey) == true ? subtypeKey : string.Empty;
            var raceSubtypeMapKey = raceSubtypeSpec?.Metadata.TryGetValue("abilityMapKey", out var mapKey) == true ? mapKey : string.Empty;

            var recalculated = CharacterSpecialisationScreenCalculator.Recalculate(
                context,
                _sectionSpecs,
                selectionState,
                raceSubtypeKey,
                raceSubtypeMapKey);

            ApplyScreenState(recalculated, preserveExpanded: true);
            UpdateSpellCustomisationVisibility();
            await RefreshSpellCustomisationOptionsAsync();
            await RefreshPrereqOptionsAsync();

            _builder.NotifyGatingChanged();
            _ = _builder.RefreshDraftAbilitiesAsync();
        }
        finally
        {
            _selectionRecalcLock.Release();
        }
    }

    private SpecialisationSelectionState BuildSelectionStateFromSections()
    {
        var sectionSnapshot = Sections.ToList();
        var choiceSelections = new Dictionary<string, ChoiceSelectionState>(StringComparer.OrdinalIgnoreCase);
        var mappedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var raceSubtype = string.Empty;

        foreach (var section in sectionSnapshot)
        {
            switch (section)
            {
                case SpecialisationGroupVm group:
                {
                    var selectedByLevel = new Dictionary<int, string>();
                    foreach (var slot in group.Slots.Where(slot => slot != null))
                        selectedByLevel[slot.Level] = (slot.SelectedOption ?? string.Empty).Trim();

                    choiceSelections[group.SectionId] = new ChoiceSelectionState
                    {
                        SelectedByLevel = new ReadOnlyDictionary<int, string>(selectedByLevel)
                    };

                    break;
                }

                case MappedSpecialisationSectionVm mapped:
                {
                    var selected = (mapped.SelectedOption ?? string.Empty).Trim();
                    if (selected.Length > 0)
                        mappedSelections[mapped.SectionId] = selected;

                    if (mapped.SectionType == SpecialisationSectionType.RaceSubtype)
                        raceSubtype = selected;

                    break;
                }
            }
        }

        return new SpecialisationSelectionState
        {
            RaceSubtype = raceSubtype,
            ChoiceSelections = new ReadOnlyDictionary<string, ChoiceSelectionState>(choiceSelections),
            MappedSelections = new ReadOnlyDictionary<string, string>(mappedSelections)
        };
    }

    private void ApplyDraftState(SpecialisationScreenState screenState)
    {
        var managedKeys = _sectionSpecs
            .Where(spec => spec.Kind != SpecialisationSectionKind.RaceSubtype)
            .Select(spec => spec.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toRemove = Draft.SpecialisationSelections.Keys
            .Where(key => managedKeys.Contains(key))
            .ToList();

        foreach (var key in toRemove)
            Draft.SpecialisationSelections.Remove(key);

        foreach (var entry in screenState.PersistedSelections)
            Draft.SpecialisationSelections[entry.Key] = entry.Value;

        Draft.RaceSubtypeKey = screenState.RaceSubtypeKey;
        Draft.RaceSubtypeValue = screenState.RaceSubtypeValue;
        Draft.RaceSubtype = screenState.RaceSubtypeValue;

        Draft.LifeScaleKeyOverride = screenState.LifeScaleOverride;
        Draft.ArmourAvailabilityOverride = screenState.ArmourAvailabilityOverride;

        Draft.ColourChoiceOverride.Clear();
        foreach (var colour in screenState.ColourChoiceOverride ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(colour))
                Draft.ColourChoiceOverride.Add(colour.Trim());
        }
    }

    private void RecomputeCompletion()
    {
        IsComplete = Sections.All(section => section.IsComplete);
        Raise(nameof(HasChoices));
        Raise(nameof(HasNoChoices));
    }

    public (List<AbilityDraft> Abilities, GuildOverrideRules? GuildOverrides) BuildSelectedAbilityDraftsWithRules()
    {
        var abilities = new List<AbilityDraft>();
        GuildOverrideRules? selectedGuildOverrides = null;

        foreach (var section in Sections)
        {
            switch (section)
            {
                case SpecialisationGroupVm group:
                    BuildChoiceSectionAbilities(group, abilities, ref selectedGuildOverrides);
                    break;

                case MappedSpecialisationSectionVm mapped:
                    BuildMappedSectionAbilities(mapped, abilities, ref selectedGuildOverrides);
                    break;
            }
        }

        return (abilities, selectedGuildOverrides);
    }

    private void BuildChoiceSectionAbilities(
        SpecialisationGroupVm group,
        List<AbilityDraft> output,
        ref GuildOverrideRules? selectedGuildOverrides)
    {
        if (!_specBySectionId.TryGetValue(group.SectionId, out var spec))
            return;

        // Colour selection sections drive downstream filtering/state and do not grant direct abilities.
        if (HasStrategy(spec, "selection:multi-delimited"))
            return;

        foreach (var slot in group.Slots)
        {
            if (!slot.HasSelection)
                continue;

            var selected = (slot.SelectedOption ?? string.Empty).Trim();
            if (selected.Length == 0)
                continue;

            var option = spec.Options.FirstOrDefault(o =>
                string.Equals(o.Key, selected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.Label, selected, StringComparison.OrdinalIgnoreCase));

            if (option != null && option.Grants.Count > 0)
            {
                foreach (var grant in option.Grants)
                {
                    var definition = ApplyCustomisation(grant.Ability, slot.CustomisationValue);
                    if (definition.GuildOverrides != null)
                    {
                        selectedGuildOverrides = GuildOverrideRules.Merge(
                            selectedGuildOverrides,
                            GuildOverrideRules.FromLegacyStrings(definition.GuildOverrides));
                    }

                    var parsed = AbilityDraftBuilder.ParseAbility(definition, grant.Level ?? slot.Level);
                    ApplyAbilitySource(parsed, $"Specialisation:{group.Title}");
                    output.AddRange(parsed);
                }

                continue;
            }

            var name = AppendCustomisation(selected, slot.CustomisationValue);
            var fallback = AbilityDraftBuilder.ParseAbility(name, slot.Level);
            ApplyAbilitySource(fallback, $"Specialisation:{group.Title}");
            output.AddRange(fallback);
        }
    }

    private void BuildMappedSectionAbilities(
        MappedSpecialisationSectionVm mapped,
        List<AbilityDraft> output,
        ref GuildOverrideRules? selectedGuildOverrides)
    {
        if (!_specBySectionId.TryGetValue(mapped.SectionId, out var spec))
            return;

        if (mapped.HasIssue)
            return;

        var selected = (mapped.SelectedOption ?? string.Empty).Trim();
        if (selected.Length == 0)
            return;

        var option = spec.Options.FirstOrDefault(o => string.Equals(o.Key, selected, StringComparison.OrdinalIgnoreCase));
        if (option?.Effects.GuildOverrides != null)
        {
            selectedGuildOverrides = GuildOverrideRules.Merge(selectedGuildOverrides, option.Effects.GuildOverrides);
        }

        var source = mapped.SectionType == SpecialisationSectionType.RaceSubtype
            ? "Race"
            : $"Specialisation:{spec.Title}";

        foreach (var grant in option?.Grants ?? Array.Empty<AbilityGrant>())
        {
            if (grant?.Ability == null || string.IsNullOrWhiteSpace(grant.Ability.Name))
                continue;

            if (grant.Ability.GuildOverrides != null)
            {
                selectedGuildOverrides = GuildOverrideRules.Merge(
                    selectedGuildOverrides,
                    GuildOverrideRules.FromLegacyStrings(grant.Ability.GuildOverrides));
            }

            var parsed = AbilityDraftBuilder.ParseAbility(grant.Ability, grant.Level);
            ApplyAbilitySource(parsed, source);
            output.AddRange(parsed);
        }
    }

    public async Task RefreshPrereqOptionsAsync()
    {
        if (_isRefreshingPrereqs)
            return;

        _isRefreshingPrereqs = true;
        try
        {
            var abilityNames = Draft.Abilities
                .Select(ability => (ability?.Name ?? string.Empty).Trim())
                .Where(name => name.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var className = (Draft.Class ?? string.Empty).Trim();
            var peopleTypes = await GetCurrentPeopleTypesAsync();

            foreach (var group in Sections.OfType<SpecialisationGroupVm>())
            {
                if (!_specBySectionId.TryGetValue(group.SectionId, out var spec))
                {
                    group.SetIssueMessage(string.Empty);
                    continue;
                }

                group.UpdateOptionNames(spec.Options.Select(option => option.Label));

                var issueLines = new List<string>();
                foreach (var slot in group.Slots)
                {
                    var selectedName = (slot.SelectedOption ?? string.Empty).Trim();
                    if (selectedName.Length == 0)
                        continue;

                    var option = spec.Options.FirstOrDefault(o =>
                        string.Equals(o.Key, selectedName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(o.Label, selectedName, StringComparison.OrdinalIgnoreCase));

                    var ability = option?.Grants.FirstOrDefault()?.Ability;
                    if (ability == null)
                        continue;

                    var unmet = GetUnmetPrerequisites(ability, abilityNames, peopleTypes, className);
                    if (unmet.Count == 0)
                        continue;

                    issueLines.Add($"'{selectedName}' requires {string.Join(", ", unmet)}.");
                }

                group.SetIssueMessage(issueLines.Count == 0
                    ? string.Empty
                    : $"Issue: {string.Join(" ", issueLines)}");
            }
        }
        finally
        {
            _isRefreshingPrereqs = false;
        }
    }

    private async Task<HashSet<string>> GetCurrentPeopleTypesAsync()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var race = (Draft.Race ?? string.Empty).Trim();
        if (race.Length == 0)
            return set;

        if (!_allRaces.TryGetValue(race, out var raceRecord) || raceRecord?.PeopleType == null)
            return set;

        foreach (var type in raceRecord.PeopleType)
        {
            var normalized = NormalizePrereqToken(type);
            if (normalized.Length > 0)
                set.Add(normalized);
        }

        return set;
    }

    private static List<string> GetUnmetPrerequisites(
        AbilityDefinition definition,
        HashSet<string> abilityNames,
        HashSet<string> peopleTypes,
        string className)
    {
        var missing = new List<string>();
        if (definition.PreReqs == null || definition.PreReqs.Count == 0)
            return missing;

        foreach (var raw in definition.PreReqs)
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
                        missing.Add($"ability '{value}'");
                    break;
                case "PeopleType":
                    if (!peopleTypes.Contains(NormalizePrereqToken(value)))
                        missing.Add($"people type '{value}'");
                    break;
                case "Class":
                    if (string.IsNullOrWhiteSpace(className)
                        || !string.Equals(className, value, StringComparison.OrdinalIgnoreCase))
                    {
                        missing.Add($"class '{value}'");
                    }
                    break;
            }
        }

        return missing
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
            {
                return (kind, parts[1]);
            }
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
        {
            return false;
        }

        return int.TryParse(parts[1], out maxLevel) && maxLevel > 0;
    }

    private async Task RefreshSpellCustomisationOptionsAsync()
    {
        if (_spellCustomisationSectionIds.Count == 0 || _isRefreshingSpellOptions)
            return;

        _isRefreshingSpellOptions = true;
        try
        {
            await EnsureSpellCacheAsync();

            foreach (var group in Sections.OfType<SpecialisationGroupVm>())
            {
                if (!_spellCustomisationSectionIds.Contains(group.SectionId))
                    continue;

                group.RefreshCustomisationOptions();
            }

            UpdateSpellCustomisationVisibility();
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

    private void UpdateSpellCustomisationVisibility()
    {
        if (_spellCustomisationSectionIds.Count == 0)
            return;

        var colours = GetSelectedFaerieSpellColours(out var hasFaerieGroup);
        var show = !hasFaerieGroup || colours.Count > 0;

        foreach (var group in Sections.OfType<SpecialisationGroupVm>())
        {
            if (_spellCustomisationSectionIds.Contains(group.SectionId))
                group.IsVisible = show;
        }
    }

    private bool HasSpellCustomisation(SpecialisationGroupVm group)
    {
        foreach (var slot in group.Slots)
        {
            if (slot?.ForcedAbilityDefinition?.Customisation != null
                && TryParseSpellCustomisation(slot.ForcedAbilityDefinition.Customisation.OptionEnum, out _))
            {
                return true;
            }
        }

        if (!_specBySectionId.TryGetValue(group.SectionId, out var spec))
            return false;

        foreach (var option in spec.Options)
        {
            if (option?.Customisation != null && TryParseSpellCustomisation(option.Customisation.OptionEnum, out _))
                return true;
        }

        return false;
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
            .Where(spell => spell != null)
            .Where(spell => spell.level >= 0 && spell.level <= maxLevel && (spell.isAdvanced ?? false) == false)
            .Where(spell => !filterByColour || SpellMatchesColours(spell, colourFilter))
            .OrderBy(spell => spell.level)
            .ThenBy(spell => spell.name, StringComparer.OrdinalIgnoreCase);

        foreach (var spell in spells)
        {
            var label = FormatSpellLabel(spell);
            if (!options.ContainsKey(label))
                options[label] = label;
        }

        return options;
    }

    private HashSet<string> GetSelectedFaerieSpellColours(out bool hasFaerieGroup)
    {
        hasFaerieGroup = false;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var group = Sections
            .OfType<SpecialisationGroupVm>()
            .FirstOrDefault(section =>
                section.StrategyIds.Any(id => id.Equals("validation:faerie-opposites", StringComparison.OrdinalIgnoreCase)));

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
        if (spell == null || colours.Count == 0)
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

    private static string FormatSpellLabel(SpellService.SpellRaw spell)
    {
        var name = (spell?.name ?? string.Empty).Trim();
        if (name.Length == 0)
            return string.Empty;

        return $"{name} (lvl {spell.level})";
    }

    private bool IsClassAllowed(IReadOnlyList<string>? restrictions)
    {
        var className = (Draft.Class ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(className))
            return true;

        var list = restrictions?
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .ToList() ?? new List<string>();

        if (list.Count == 0)
            return true;

        var classTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddClassToken(classTokens, className);

        if (_allClasses.TryGetValue(className, out var classRecord))
        {
            foreach (var bracket in classRecord.Brackets ?? new List<string>())
                AddClassToken(classTokens, bracket);
        }

        foreach (var restriction in list)
        {
            var normalized = NormalizeClassToken(restriction);
            if (normalized.Length == 0)
                continue;

            var singularRestriction = TrimPluralToken(normalized);
            foreach (var classToken in classTokens)
            {
                var singularClass = TrimPluralToken(classToken);
                if (classToken.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                    || singularClass.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                    || classToken.Equals(singularRestriction, StringComparison.OrdinalIgnoreCase)
                    || singularClass.Equals(singularRestriction, StringComparison.OrdinalIgnoreCase)
                    || classToken.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains(classToken, StringComparison.OrdinalIgnoreCase)
                    || classToken.Contains(singularRestriction, StringComparison.OrdinalIgnoreCase)
                    || singularRestriction.Contains(classToken, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsAlignmentAllowed(IReadOnlyList<string>? restrictions)
    {
        var normalized = restrictions?
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (normalized.Count == 0)
            return true;

        var allowedAlignments = Draft.AvailableAlignments?.ToList() ?? new List<Alignment>();
        if (allowedAlignments.Count == 0 && Draft.Alignment is Alignment selectedAlignment)
            allowedAlignments.Add(selectedAlignment);

        if (allowedAlignments.Count == 0)
            return true;

        var allowedMorals = new HashSet<MoralAxis>();
        var allowedOrders = new HashSet<OrderAxis>();
        var allowedPairs = new HashSet<Alignment>();

        foreach (var raw in normalized)
        {
            if (TryParseAlignmentPair(raw, out var pair))
            {
                allowedPairs.Add(pair);
                continue;
            }

            var token = NormalizeAlignmentKeyword(raw);
            switch (token)
            {
                case "good":
                case "goodly":
                    allowedMorals.Add(MoralAxis.Good);
                    continue;
                case "evil":
                    allowedMorals.Add(MoralAxis.Evil);
                    continue;
                case "neutral":
                    allowedMorals.Add(MoralAxis.Neutral);
                    continue;
                case "lawful":
                    allowedOrders.Add(OrderAxis.Lawful);
                    continue;
                case "chaotic":
                    allowedOrders.Add(OrderAxis.Chaotic);
                    continue;
            }
        }

        if (allowedMorals.Count == 0 && allowedOrders.Count == 0 && allowedPairs.Count == 0)
            return true;

        return allowedAlignments.Any(alignment =>
            (allowedPairs.Count == 0 || allowedPairs.Contains(alignment))
            && (allowedMorals.Count == 0 || allowedMorals.Contains(alignment.Moral))
            && (allowedOrders.Count == 0 || allowedOrders.Contains(alignment.Order)));
    }

    private static void AddClassToken(HashSet<string> sink, string? raw)
    {
        var token = NormalizeClassToken(raw);
        if (token.Length > 0)
            sink.Add(token);
    }

    private static string NormalizeClassToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return new string(raw
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string TrimPluralToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        if (token.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
            return $"{token[..^3]}y";

        if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.OrdinalIgnoreCase) && token.Length > 1)
            return token[..^1];

        return token;
    }

    private static bool TryParseAlignmentPair(string value, out Alignment pair)
    {
        pair = default;
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        if (text.Equals("True Neutral", StringComparison.OrdinalIgnoreCase))
        {
            pair = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral);
            return true;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (!Enum.TryParse<OrderAxis>(parts[0], ignoreCase: true, out var order))
            return false;
        if (!Enum.TryParse<MoralAxis>(parts[1], ignoreCase: true, out var moral))
            return false;

        pair = new Alignment(order, moral);
        return true;
    }

    private static string NormalizeAlignmentKeyword(string value)
        => new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();

    private static AbilityDefinition ApplyCustomisation(AbilityDefinition definition, string? customValue)
    {
        if (definition == null)
            return new AbilityDefinition();

        var cleanedCustom = NormalizeSpellCustomisationValue(definition, customValue);
        var updatedName = AppendCustomisation(definition.Name ?? string.Empty, cleanedCustom);
        if (string.Equals(updatedName, definition.Name ?? string.Empty, StringComparison.Ordinal))
            return definition;

        return new AbilityDefinition
        {
            Name = updatedName,
            Type = definition.Type ?? string.Empty,
            BattleboardNameOverride = definition.BattleboardNameOverride,
            UpdateKey = definition.UpdateKey,
            Effect = definition.Effect,
            Source = definition.Source,
            Count = definition.Count,
            Frequency = definition.Frequency,
            OverwriteKey = definition.OverwriteKey,
            PreReqs = definition.PreReqs?.ToList(),
            GuildOverrides = definition.GuildOverrides?.ToList(),
            Customisation = definition.Customisation
        };
    }

    private static string? NormalizeSpellCustomisationValue(AbilityDefinition? definition, string? customValue)
    {
        if (definition?.Customisation == null)
            return customValue;

        if (!TryParseSpellCustomisation(definition.Customisation.OptionEnum, out _))
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
}
