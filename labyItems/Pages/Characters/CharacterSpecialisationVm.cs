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
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public sealed class CharacterSpecialisationVm : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly CharacterBuilderVm _builder;
    private CharacterDraft Draft => _builder.Draft;
    private readonly ReloadGate _reloadGate = new();
    private readonly SemaphoreSlim _selectionRecalcLock = new(1, 1);
    private CancellationTokenSource? _selectionRecalcCts;
    private int _selectionRecalcVersion;

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
    private bool _isLoading;
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

    private const string HedgeInfoImmunityByClassPowerbaseKey = "ability.hedge-informational-immunity-by-class-powerbase";
    private const string HedgeResistanceByClassPowerbaseKey = "ability.hedge-plus1-resistance-by-class-powerbase";

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
    public bool HasNoChoices => !IsLoading && !HasChoices;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!Set(ref _isLoading, value))
                return;

            Raise(nameof(HasNoChoices));
        }
    }

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
    {
        _reloadGate.Cancel();
        CancelPendingSelectionRecalculation();
    }

    public void BeginLoadingState()
    {
        MainThread.BeginInvokeOnMainThread(() => IsLoading = true);
    }

    public async Task ReleaseSearchCacheAsync()
    {
        CancelPendingSelectionRecalculation();
        _spellCacheLoaded = false;
        _spellCache.Clear();
        await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
    }

    public void Dispose()
    {
        _reloadGate.Dispose();
        CancelPendingSelectionRecalculation();
        _selectionRecalcLock.Dispose();
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        CancelPendingSelectionRecalculation();
        var reload = _reloadGate.Begin(cancellationToken);

        try
        {
            void EnsureActive() => reload.ThrowIfCancelledOrStale();

            EnsureActive();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoading = true;
                Sections.Clear();
                _specBySectionId.Clear();
                _spellCustomisationSectionIds.Clear();
                _screenState = null;
                IsComplete = false;
            });

            var specialisationIndexTask = SpecialisationDefinitionRepository.GetIndexAsync();
            var classesTask = ClassService.GetAllAsync();
            var racesTask = PeopleService.GetAllAsync();
            await Task.WhenAll(specialisationIndexTask, classesTask, racesTask).ConfigureAwait(false);
            _specialisationIndex = specialisationIndexTask.Result;
            _allClasses = classesTask.Result;
            _allRaces = racesTask.Result;
            EnsureActive();

            var loaded = await Task.Run(() =>
            {
                var context = CharacterSpecialisationScreenCalculator.LoadContext(
                    Draft,
                    _allClasses,
                    _allRaces,
                    _specialisationIndex.Definitions,
                    _specialisationIndex.InjectionRules);

                var requiredChoices = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
                var sectionSpecs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, requiredChoices);
                var initialScreen = CharacterSpecialisationScreenCalculator.ApplySavedSelections(context, sectionSpecs);

                return (context, requiredChoices, sectionSpecs, initialScreen);
            }).ConfigureAwait(false);

            _context = loaded.context;
            _requiredChoices = loaded.requiredChoices;
            _sectionSpecs = loaded.sectionSpecs;
            await MainThread.InvokeOnMainThreadAsync(() => ApplyScreenState(loaded.initialScreen, preserveExpanded: false));

            EnsureActive();
            await RefreshSpellCustomisationOptionsAsync();
            EnsureActive();
            await RefreshPrereqOptionsAsync();
            EnsureActive();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RecomputeCompletion();
                _builder.NotifyGatingChanged();
            });
        }
        catch (OperationCanceledException) when (reload.IsCanceledOrStale)
        {
        }
        finally
        {
            if (reload.IsCurrent)
                await MainThread.InvokeOnMainThreadAsync(() => IsLoading = false);
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
        var optionPreviewRows = HasStrategy(spec, "preview:option-grants")
            ? BuildOptionPreviewRows(spec)
            : null;
        var config = new SpecialisationGroupConfig(
            spec.Title,
            spec.Levels,
            optionSource,
            OnSectionSelectionChanged,
            SelectionValidator: GetSelectionValidator(spec),
            IsOptional: !spec.Required,
            OptionCustomisations: optionCustomisations,
            CustomisationOptionsProvider: ResolveCustomisationOptions,
            HideAbilityPickerWhenSingleOption: hasSpellCustomisation || hasSingleOptionWithCustomisation,
            OptionPreviewRows: optionPreviewRows);

        var initialByLevel = state.SelectedByLevel.ToDictionary(k => k.Key, v => v.Value);
        var group = new SpecialisationGroupVm(config, initialByLevel);
        group.ConfigureSectionMetadata(spec.SectionId, spec.DetailKey, spec.StrategyIds, SpecialisationSectionType.Choice);
        ApplyLevelScopedOptionsIfNeeded(spec, group);
        group.IsExpanded = true;

        foreach (var slot in group.Slots)
        {
            if (state.CustomisationByLevel.TryGetValue(slot.Level, out var customisation)
                && !string.IsNullOrWhiteSpace(customisation))
            {
                slot.CustomisationValue = customisation;
            }
        }

        if (hasSpellCustomisation || hasSingleOptionWithCustomisation)
            ApplySingleOptionDefault(group);

        if (!string.IsNullOrWhiteSpace(state.ValidationMessage))
            group.SetIssueMessage(state.ValidationMessage);

        return group;
    }

    private Dictionary<string, IReadOnlyList<SpecialisationAbilityRow>> BuildOptionPreviewRows(SpecialisationSectionSpec spec)
    {
        var rowsByOption = new Dictionary<string, IReadOnlyList<SpecialisationAbilityRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in spec.Options ?? Array.Empty<ChoiceOption>())
        {
            var optionKey = (option?.Label ?? option?.Key ?? string.Empty).Trim();
            if (optionKey.Length == 0)
                continue;

            var rows = (option?.Grants ?? Array.Empty<AbilityGrant>())
                .Select(grant => new
                {
                    Grant = grant,
                    Ability = ResolveGrantAbilityForCurrentDraft(grant?.Ability)
                })
                .Where(item => item.Grant != null && !string.IsNullOrWhiteSpace(item.Ability.Name))
                .OrderBy(item => item.Grant!.Table.HasValue ? 1 : 0)
                .ThenBy(item => item.Grant!.Table ?? item.Grant!.Level ?? int.MaxValue)
                .ThenBy(item => item.Ability.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new SpecialisationAbilityRow
                {
                    Level = item.Grant!.Level,
                    Table = item.Grant!.Table,
                    Ability = item.Ability.Name,
                    AbilityKey = (item.Ability.Key ?? string.Empty).Trim(),
                    SpecialisationKey = spec.DetailKey,
                    SelectedOption = optionKey,
                    SelectedAbility = item.Ability.Name
                })
                .ToList();

            rowsByOption[optionKey] = rows;
        }

        return rowsByOption;
    }

    private MappedSpecialisationSectionVm BuildMappedSection(SpecialisationSectionSpec spec, SpecialisationSectionState state)
    {
        var sectionType = spec.Kind == SpecialisationSectionKind.RaceSubtype
            ? SpecialisationSectionType.RaceSubtype
            : SpecialisationSectionType.Mapped;

        var initialSelection = (state.SelectedOption ?? string.Empty).Trim();
        if (sectionType == SpecialisationSectionType.RaceSubtype
            && initialSelection.Length == 0
            && string.Equals((Draft.Race ?? string.Empty).Trim(), "Human", StringComparison.OrdinalIgnoreCase))
        {
            var standard = spec.Options.FirstOrDefault(option =>
                string.Equals((option.Key ?? string.Empty).Trim(), "Standard", StringComparison.OrdinalIgnoreCase)
                || string.Equals((option.Label ?? string.Empty).Trim(), "Standard", StringComparison.OrdinalIgnoreCase));
            if (standard != null)
                initialSelection = ResolveChoiceSelectionToken(standard);
        }

        var mapped = new MappedSpecialisationSectionVm(
            sectionId: spec.SectionId,
            key: spec.DefinitionKey,
            detailKey: spec.DetailKey,
            title: spec.Title,
            subtitle: spec.Subtitle,
            options: spec.Options,
            initialSelection: initialSelection,
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
            return new WardPactSource(optionNames);

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

    private static string ResolveChoiceSelectionToken(ChoiceOption? option)
    {
        if (option == null)
            return string.Empty;

        var key = (option.Key ?? string.Empty).Trim();
        if (key.Length > 0)
            return key;

        return (option.Label ?? string.Empty).Trim();
    }

    private static int DecodeSelectionLevel(int encodedLevel)
        => encodedLevel > 99 ? encodedLevel / 100 : encodedLevel;

    private static void ApplyLevelScopedOptionsIfNeeded(SpecialisationSectionSpec spec, SpecialisationGroupVm group)
    {
        if (!HasStrategy(spec, "options:exact-level-by-option-metadata"))
            return;

        foreach (var slot in group.Slots)
        {
            var level = DecodeSelectionLevel(slot.Level);
            slot.SetOptionsSource(() => GetOptionNamesForLevel(spec.Options, level, requireExactLevel: true));
            slot.RaiseFilteredOptionsChanged();
        }
    }

    private static IReadOnlyList<string> GetOptionNamesForLevel(
        IReadOnlyList<ChoiceOption> options,
        int level,
        bool requireExactLevel)
        => options
            .Where(option => IsOptionAvailableForLevel(option, level, requireExactLevel))
            .Select(option => option.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsOptionAvailableForLevel(ChoiceOption option, int level, bool requireExactLevel)
    {
        if (!TryGetOptionMetadataLevel(option, out var optionLevel))
            return true;

        return requireExactLevel
            ? level == optionLevel
            : level >= optionLevel;
    }

    private static bool TryGetOptionMetadataLevel(ChoiceOption option, out int level)
    {
        level = 0;
        if (option?.Metadata == null || option.Metadata.Count == 0)
            return false;

        if (option.Metadata.TryGetValue("Level", out var levelText)
            && int.TryParse(levelText, out level))
        {
            return true;
        }

        if (option.Metadata.TryGetValue("MinLevel", out levelText)
            && int.TryParse(levelText, out level))
        {
            return true;
        }

        return false;
    }

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

            return MagicColourOppositionRules.AreOpposites(first.Value, second.Value)
                ? "Faerie colours cannot be opposite pairs."
                : null;
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
        var raceAllowed = IsRaceAllowed(option.Restrictions.RaceRestriction);
        var subtypeAllowed = IsSubtypeAllowed(option.Restrictions.RaceSubtypeRestriction);

        if (classAllowed && alignmentAllowed && raceAllowed && subtypeAllowed)
            return string.Empty;

        var failures = new List<string>();
        if (!classAllowed)
            failures.Add("Class");
        if (!alignmentAllowed)
            failures.Add("Alignment");
        if (!raceAllowed)
            failures.Add("Race");
        if (!subtypeAllowed)
            failures.Add("Race subtype");

        return failures.Count switch
        {
            <= 0 => string.Empty,
            1 => $"{failures[0]} requirements conflict with the current character.",
            2 => $"{failures[0]} and {failures[1]} requirements conflict with the current character.",
            _ => $"{string.Join(", ", failures.Take(failures.Count - 1))}, and {failures[^1]} requirements conflict with the current character."
        };
    }

    private void OnSectionSelectionChanged()
    {
        if (_isApplyingState)
            return;

        // Surface completion/gating changes immediately; expensive recomputation can trail behind.
        RecomputeCompletion();
        _builder.NotifyGatingChanged();
        QueueSelectionRecalculation();
    }

    private void QueueSelectionRecalculation()
    {
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _selectionRecalcCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        var version = Interlocked.Increment(ref _selectionRecalcVersion);
        _ = RecalculateFromCurrentSelectionsAsync(version, cts.Token);
    }

    private void CancelPendingSelectionRecalculation()
    {
        var cts = Interlocked.Exchange(ref _selectionRecalcCts, null);
        cts?.Cancel();
        cts?.Dispose();
        Interlocked.Increment(ref _selectionRecalcVersion);
    }

    private bool IsLatestSelectionRecalc(int version)
        => version == Volatile.Read(ref _selectionRecalcVersion);

    private async Task RecalculateFromCurrentSelectionsAsync(int version, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            await _selectionRecalcLock.WaitAsync(cancellationToken);
            try
            {
                if (!IsLatestSelectionRecalc(version))
                    return;

                if (_specialisationIndex.Definitions.Count == 0)
                    return;

                var selectionState = BuildSelectionStateFromSections();
                var result = await Task.Run(() =>
                {
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

                    var requiredChoices = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
                    var sectionSpecs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, requiredChoices);

                    var raceSubtypeSpec = sectionSpecs.FirstOrDefault(spec => spec.Kind == SpecialisationSectionKind.RaceSubtype);
                    var raceSubtypeKey = raceSubtypeSpec?.Metadata.TryGetValue("raceSubtypeKey", out var subtypeKey) == true ? subtypeKey : string.Empty;
                    var raceSubtypeMapKey = raceSubtypeSpec?.Metadata.TryGetValue("abilityMapKey", out var mapKey) == true ? mapKey : string.Empty;

                    var recalculated = CharacterSpecialisationScreenCalculator.Recalculate(
                        context,
                        sectionSpecs,
                        selectionState,
                        raceSubtypeKey,
                        raceSubtypeMapKey);

                    return (context, requiredChoices, sectionSpecs, recalculated);
                });

                if (cancellationToken.IsCancellationRequested || !IsLatestSelectionRecalc(version))
                    return;

                _context = result.context;
                _requiredChoices = result.requiredChoices;
                _sectionSpecs = result.sectionSpecs;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested || !IsLatestSelectionRecalc(version))
                        return;

                    ApplyScreenState(result.recalculated, preserveExpanded: true);
                    UpdateSpellCustomisationVisibility();
                });
            }
            finally
            {
                _selectionRecalcLock.Release();
            }

            if (cancellationToken.IsCancellationRequested || !IsLatestSelectionRecalc(version))
                return;

            await RefreshSpellCustomisationOptionsAsync();
            if (cancellationToken.IsCancellationRequested || !IsLatestSelectionRecalc(version))
                return;

            await RefreshPrereqOptionsAsync();
            if (cancellationToken.IsCancellationRequested || !IsLatestSelectionRecalc(version))
                return;

            _builder.NotifyGatingChanged();
            _ = _builder.RefreshDraftAbilitiesAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Specialisation recalculation failed: {ex}");
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
                    var customisationByLevel = new Dictionary<int, string>();
                    foreach (var slot in group.Slots.Where(slot => slot != null))
                    {
                        selectedByLevel[slot.Level] = (slot.SelectedOption ?? string.Empty).Trim();
                        var custom = (slot.CustomisationValue ?? string.Empty).Trim();
                        if (custom.Length > 0)
                            customisationByLevel[slot.Level] = custom;
                    }

                    choiceSelections[group.SectionId] = new ChoiceSelectionState
                    {
                        SelectedByLevel = new ReadOnlyDictionary<int, string>(selectedByLevel),
                        CustomisationByLevel = new ReadOnlyDictionary<int, string>(customisationByLevel)
                    };

                    break;
                }

                case MappedSpecialisationSectionVm mapped:
                {
                    var selected = NormalizeMappedSelectionToken(
                        mapped.SectionId,
                        (mapped.SelectedOption ?? string.Empty).Trim());
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

    private string NormalizeMappedSelectionToken(string sectionId, string selectedToken)
    {
        var selected = (selectedToken ?? string.Empty).Trim();
        if (selected.Length == 0)
            return string.Empty;

        if (!_specBySectionId.TryGetValue(sectionId, out var spec))
            return selected;

        var option = spec.Options.FirstOrDefault(o =>
            string.Equals(o.Key, selected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Label, selected, StringComparison.OrdinalIgnoreCase));

        return (option?.Key ?? selected).Trim();
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

    public Task<(List<AbilityDraft> Abilities, GuildOverrideRules? GuildOverrides)> BuildSelectedAbilityDraftsWithRulesAsync()
    {
        if (MainThread.IsMainThread)
            return Task.FromResult(BuildSelectedAbilityDraftsWithRules());

        return MainThread.InvokeOnMainThreadAsync(BuildSelectedAbilityDraftsWithRules);
    }

    public IReadOnlyList<AlignmentRule> BuildSelectedAlignmentRules()
    {
        var rules = new List<AlignmentRule>();

        foreach (var section in Sections)
        {
            if (!_specBySectionId.TryGetValue(section.SectionId, out var spec))
                continue;

            switch (section)
            {
                case SpecialisationGroupVm group:
                {
                    foreach (var slot in group.Slots)
                    {
                        if (!slot.HasSelection)
                            continue;

                        var option = FindOptionBySelectionToken(spec, slot.SelectedOption);
                        var rule = BuildAlignmentRuleFromRestrictions(option?.Restrictions?.AlignmentRestriction);
                        if (rule != null)
                            rules.Add(rule);
                    }

                    break;
                }

                case MappedSpecialisationSectionVm mapped:
                {
                    var option = FindOptionBySelectionToken(spec, mapped.SelectedOption);
                    var rule = BuildAlignmentRuleFromRestrictions(option?.Restrictions?.AlignmentRestriction);
                    if (rule != null)
                        rules.Add(rule);
                    break;
                }
            }
        }

        return rules;
    }

    public Task<IReadOnlyList<AlignmentRule>> BuildSelectedAlignmentRulesAsync()
    {
        if (MainThread.IsMainThread)
            return Task.FromResult((IReadOnlyList<AlignmentRule>)BuildSelectedAlignmentRules());

        return MainThread.InvokeOnMainThreadAsync(() => BuildSelectedAlignmentRules());
    }

    private static ChoiceOption? FindOptionBySelectionToken(SpecialisationSectionSpec spec, string? token)
    {
        var selected = (token ?? string.Empty).Trim();
        if (selected.Length == 0)
            return null;

        return spec.Options.FirstOrDefault(option =>
            string.Equals(option.Key, selected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Label, selected, StringComparison.OrdinalIgnoreCase));
    }

    private static AlignmentRule? BuildAlignmentRuleFromRestrictions(IReadOnlyList<string>? restrictions)
    {
        var normalized = restrictions?
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (normalized.Count == 0)
            return null;

        var morals = new HashSet<MoralAxis>();
        var orders = new HashSet<OrderAxis>();
        var pairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in normalized)
        {
            if (TryParseAlignmentPair(raw, out var pair))
            {
                pairs.Add(pair.ToString());
                continue;
            }

            var token = NormalizeAlignmentKeyword(raw);
            switch (token)
            {
                case "good":
                case "goodly":
                    morals.Add(MoralAxis.Good);
                    break;
                case "evil":
                    morals.Add(MoralAxis.Evil);
                    break;
                case "neutral":
                    morals.Add(MoralAxis.Neutral);
                    break;
                case "lawful":
                    orders.Add(OrderAxis.Lawful);
                    break;
                case "chaotic":
                    orders.Add(OrderAxis.Chaotic);
                    break;
            }
        }

        if (morals.Count == 0 && orders.Count == 0 && pairs.Count == 0)
            return null;

        return new AlignmentRule
        {
            Mode = "restrict",
            Allowed = morals.Count > 0 || orders.Count > 0
                ? new AllowedAxes
                {
                    Moral = morals.Count > 0 ? morals.ToList() : null,
                    Order = orders.Count > 0 ? orders.ToList() : null
                }
                : null,
            AllowedPairs = pairs.Count > 0 ? pairs.ToList() : null
        };
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

        var achievedTable = CharacterProgressionTables.GetHighestTableReached(Draft.Points);

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
                    if (grant?.Ability == null || string.IsNullOrWhiteSpace(grant.Ability.Name))
                        continue;

                    if (grant.Table.HasValue && !CharacterProgressionTables.HasReachedTable(Draft.Points, grant.Table.Value))
                        continue;

                    var resolvedAbility = ResolveGrantAbilityForCurrentDraft(grant.Ability);
                    var definition = ApplyCustomisation(resolvedAbility, slot.CustomisationValue);
                    if (definition.GuildOverrides != null)
                    {
                        selectedGuildOverrides = GuildOverrideRules.Merge(
                            selectedGuildOverrides,
                            GuildOverrideRules.FromLegacyStrings(definition.GuildOverrides));
                    }

                    var parsed = AbilityDraftBuilder.ParseAbility(
                        definition,
                        levelGained: grant.Level ?? DecodeSelectionLevel(slot.Level),
                        achievedLevel: 8,
                        tableGained: grant.Table,
                        achievedTable: achievedTable);
                    ApplyAbilitySource(parsed, $"Specialisation:{group.Title}");
                    output.AddRange(parsed);
                }

                continue;
            }

            var name = AppendCustomisation(selected, slot.CustomisationValue);
            var fallback = AbilityDraftBuilder.ParseAbility(name, DecodeSelectionLevel(slot.Level));
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

        var achievedTable = CharacterProgressionTables.GetHighestTableReached(Draft.Points);

        foreach (var grant in option?.Grants ?? Array.Empty<AbilityGrant>())
        {
            if (grant?.Ability == null || string.IsNullOrWhiteSpace(grant.Ability.Name))
                continue;

            if (grant.Table.HasValue && !CharacterProgressionTables.HasReachedTable(Draft.Points, grant.Table.Value))
                continue;

            if (grant.Ability.GuildOverrides != null)
            {
                selectedGuildOverrides = GuildOverrideRules.Merge(
                    selectedGuildOverrides,
                    GuildOverrideRules.FromLegacyStrings(grant.Ability.GuildOverrides));
            }

            var parsed = AbilityDraftBuilder.ParseAbility(
                grant.Ability,
                levelGained: grant.Level,
                achievedLevel: 8,
                tableGained: grant.Table,
                achievedTable: achievedTable);
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
            var abilityNames = (Draft.Abilities ?? new List<AbilityDraft>())
                .Select(ability => (ability?.Name ?? string.Empty).Trim())
                .Where(name => name.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var className = (Draft.Class ?? string.Empty).Trim();
            var peopleTypes = await GetCurrentPeopleTypesAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
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
            });
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

        if (HasBarbarianPeopleType(Draft))
            set.Add(NormalizePrereqToken("Tribal"));

        var subtype = (Draft.RaceSubtypeValue ?? Draft.RaceSubtype ?? string.Empty).Trim();
        if (subtype.Equals("Verdant Heart", StringComparison.OrdinalIgnoreCase))
            set.Add(NormalizePrereqToken("Tribal"));

        return set;
    }

    private static bool HasBarbarianPeopleType(CharacterDraft? draft)
    {
        if (IsBarbarianSelection(draft?.RaceSubtypeValue) || IsBarbarianSelection(draft?.RaceSubtype))
            return true;

        if (draft?.SpecialisationSelections == null || draft.SpecialisationSelections.Count == 0)
            return false;

        if (draft.SpecialisationSelections.TryGetValue("Barbarian", out var directSelection)
            && IsBarbarianSelection(directSelection))
        {
            return true;
        }

        return draft.SpecialisationSelections.Values.Any(IsBarbarianSelection);
    }

    private static bool IsBarbarianSelection(string? value)
    {
        var token = (value ?? string.Empty).Trim();
        return token.Length > 0
            && (token.Equals("Barbarian", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("Barbarian", StringComparison.OrdinalIgnoreCase));
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
            await EnsureSpellCacheAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var group in Sections.OfType<SpecialisationGroupVm>())
                {
                    if (!_spellCustomisationSectionIds.Contains(group.SectionId))
                        continue;

                    group.RefreshCustomisationOptions();
                }

                UpdateSpellCustomisationVisibility();
            });
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
            // SpellService performs synchronous DB loading/deserialization; keep it off the UI thread.
            _spellCache = await Task.Run(async () => await SpellService.GetAllAsync());
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

    private bool IsRaceAllowed(IReadOnlyList<string>? restrictions)
    {
        var raceName = (Draft.Race ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return true;

        var list = restrictions?
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .ToList() ?? new List<string>();

        if (list.Count == 0)
            return true;

        var raceToken = NormalizeRaceToken(raceName);
        if (raceToken.Length == 0)
            return true;

        var singularRace = TrimPluralToken(raceToken);
        foreach (var restriction in list)
        {
            var normalized = NormalizeRaceToken(restriction);
            if (normalized.Length == 0)
                continue;

            var singularRestriction = TrimPluralToken(normalized);
            if (raceToken.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || singularRace.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || raceToken.Equals(singularRestriction, StringComparison.OrdinalIgnoreCase)
                || singularRace.Equals(singularRestriction, StringComparison.OrdinalIgnoreCase)
                || raceToken.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(raceToken, StringComparison.OrdinalIgnoreCase)
                || raceToken.Contains(singularRestriction, StringComparison.OrdinalIgnoreCase)
                || singularRestriction.Contains(raceToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSubtypeAllowed(IReadOnlyList<string>? restrictions)
    {
        var subtype = (Draft.RaceSubtypeValue ?? Draft.RaceSubtype ?? string.Empty).Trim();

        var list = restrictions?
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .ToList() ?? new List<string>();

        if (list.Count == 0)
            return true;

        if (subtype.Length == 0)
            return false;

        var subtypeToken = NormalizeRaceToken(subtype);
        if (subtypeToken.Length == 0)
            return false;

        var singularSubtype = TrimPluralToken(subtypeToken);
        foreach (var restriction in list)
        {
            var normalized = NormalizeRaceToken(restriction);
            if (normalized.Length == 0)
                continue;

            var singularRestriction = TrimPluralToken(normalized);
            if (subtypeToken.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || singularSubtype.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || subtypeToken.Equals(singularRestriction, StringComparison.OrdinalIgnoreCase)
                || singularSubtype.Equals(singularRestriction, StringComparison.OrdinalIgnoreCase)
                || subtypeToken.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(subtypeToken, StringComparison.OrdinalIgnoreCase)
                || subtypeToken.Contains(singularRestriction, StringComparison.OrdinalIgnoreCase)
                || singularRestriction.Contains(subtypeToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private static string NormalizeRaceToken(string? raw)
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

    private AbilityDefinition ResolveGrantAbilityForCurrentDraft(AbilityDefinition? ability)
    {
        if (ability == null)
            return new AbilityDefinition();

        var key = (ability.Key ?? string.Empty).Trim();
        if (key.Length == 0)
            return ability;

        if (key.Equals(HedgeInfoImmunityByClassPowerbaseKey, StringComparison.OrdinalIgnoreCase))
            return ResolveMappedAbilityDefinition(
                ResolveHedgeInformationalImmunityKey(),
                ability);

        if (key.Equals(HedgeResistanceByClassPowerbaseKey, StringComparison.OrdinalIgnoreCase))
            return ResolveMappedAbilityDefinition(
                ResolveHedgeResistanceKey(),
                ability);

        return ability;
    }

    private AbilityDefinition ResolveMappedAbilityDefinition(string resolvedKey, AbilityDefinition fallback)
    {
        var key = (resolvedKey ?? string.Empty).Trim();
        if (key.Length > 0
            && _specialisationIndex.AbilityReferences.TryGetValue(key, out var resolved)
            && !string.IsNullOrWhiteSpace(resolved.Name))
        {
            return CloneAbilityDefinition(resolved);
        }

        return CloneAbilityDefinition(fallback);
    }

    private string ResolveHedgeInformationalImmunityKey()
        => ResolveCurrentHedgePowerbaseCategory() switch
        {
            HedgePowerbaseCategory.Magical => "ability.hedge-immunity-to-magical-informational-effects",
            HedgePowerbaseCategory.Spiritual => "ability.hedge-immunity-to-spiritual-informational-effects",
            HedgePowerbaseCategory.Neuronic => "ability.hedge-immunity-to-neuronic-informational-effects",
            _ => "ability.hedge-immunity-to-physical-informational-effects"
        };

    private string ResolveHedgeResistanceKey()
        => ResolveCurrentHedgePowerbaseCategory() switch
        {
            HedgePowerbaseCategory.Magical => "ability.hedge-1-level-resistance-magical",
            HedgePowerbaseCategory.Spiritual => "ability.hedge-1-level-resistance-spiritual",
            HedgePowerbaseCategory.Neuronic => "ability.hedge-1-level-resistance-neuronic",
            _ => "ability.hedge-1-level-resistance-physical"
        };

    private HedgePowerbaseCategory ResolveCurrentHedgePowerbaseCategory()
    {
        var className = (Draft.Class ?? string.Empty).Trim();
        if (className.Length == 0)
            return HedgePowerbaseCategory.Physical;

        if (!_allClasses.TryGetValue(className, out var classRecord))
        {
            classRecord = _allClasses
                .FirstOrDefault(pair => string.Equals(pair.Key, className, StringComparison.OrdinalIgnoreCase))
                .Value;
        }

        var powerBase = ResolvePrimaryClassPowerbase(classRecord);
        if (powerBase.Contains("magic", StringComparison.OrdinalIgnoreCase))
            return HedgePowerbaseCategory.Magical;
        if (powerBase.Contains("spirit", StringComparison.OrdinalIgnoreCase))
            return HedgePowerbaseCategory.Spiritual;
        if (powerBase.Contains("neuro", StringComparison.OrdinalIgnoreCase))
            return HedgePowerbaseCategory.Neuronic;

        return HedgePowerbaseCategory.Physical;
    }

    private static string ResolvePrimaryClassPowerbase(CharacterClassRecord? classRecord)
    {
        if (classRecord == null)
            return string.Empty;

        var direct = classRecord.Powerbase?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(direct))
            return direct.Trim();

        var calculated = classRecord.PowerCalculations?
            .Select(calc => calc?.PowerBase)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return (calculated ?? string.Empty).Trim();
    }

    private static AbilityDefinition CloneAbilityDefinition(AbilityDefinition source)
    {
        return new AbilityDefinition
        {
            Name = source.Name,
            Type = source.Type,
            BattleboardNameOverride = source.BattleboardNameOverride,
            UpdateKey = source.UpdateKey,
            Effect = source.Effect,
            Lore = source.Lore,
            Source = source.Source,
            Count = source.Count,
            Amount = source.Amount?.ToList(),
            Frequency = source.Frequency,
            OverwriteKey = source.OverwriteKey,
            PreReqs = source.PreReqs?.ToList(),
            GuildOverrides = source.GuildOverrides?.ToList(),
            Customisation = source.Customisation,
            Key = source.Key
        };
    }

    private enum HedgePowerbaseCategory
    {
        Physical,
        Magical,
        Spiritual,
        Neuronic
    }

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
