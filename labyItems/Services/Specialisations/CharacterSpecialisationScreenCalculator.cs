using System.Collections.ObjectModel;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.Enums;

namespace labyItems.Services.Specialisations;

public enum SpecialisationSectionKind
{
    Choice,
    Mapped,
    RaceSubtype
}

public enum RequiredChoiceSource
{
    Class,
    Race
}

public sealed class CharacterSpecialisationContext
{
    public CharacterDraft Draft { get; init; } = new();
    public CharacterClassRecord? ClassRecord { get; init; }
    public PeopleRecord? RaceRecord { get; init; }
    public string Race { get; init; } = string.Empty;
    public string Class { get; init; } = string.Empty;
    public string CurrentRaceSubtype { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, SpecialisationDefinition> Definitions { get; init; } =
        new ReadOnlyDictionary<string, SpecialisationDefinition>(new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase));
    public IReadOnlyList<SpecialisationInjectionRule> InjectionRules { get; init; } = Array.Empty<SpecialisationInjectionRule>();
}

public sealed class RequiredChoice
{
    public RequiredChoiceSource Source { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public string SpecialisationKey { get; init; } = string.Empty;
    public int Level { get; init; }
}

public sealed class SpecialisationSectionSpec
{
    public string SectionId { get; init; } = string.Empty;
    public string DefinitionKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string DetailKey { get; init; } = string.Empty;
    public SpecialisationSectionKind Kind { get; init; }
    public bool Required { get; init; }
    public IReadOnlyList<int> Levels { get; init; } = Array.Empty<int>();
    public IReadOnlyList<ChoiceOption> Options { get; init; } = Array.Empty<ChoiceOption>();
    public IReadOnlyList<string> StrategyIds { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
}

public sealed class ChoiceSelectionState
{
    public IReadOnlyDictionary<int, string> SelectedByLevel { get; init; } =
        new ReadOnlyDictionary<int, string>(new Dictionary<int, string>());

    public IReadOnlyDictionary<int, string> CustomisationByLevel { get; init; } =
        new ReadOnlyDictionary<int, string>(new Dictionary<int, string>());
}

public sealed class SpecialisationSelectionState
{
    public string RaceSubtype { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, ChoiceSelectionState> ChoiceSelections { get; init; } =
        new ReadOnlyDictionary<string, ChoiceSelectionState>(new Dictionary<string, ChoiceSelectionState>(StringComparer.OrdinalIgnoreCase));
    public IReadOnlyDictionary<string, string> MappedSelections { get; init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}

public sealed class SpecialisationSectionState
{
    public SpecialisationSectionSpec Spec { get; init; } = new();
    public IReadOnlyDictionary<int, string> SelectedByLevel { get; init; } =
        new ReadOnlyDictionary<int, string>(new Dictionary<int, string>());
    public IReadOnlyDictionary<int, string> CustomisationByLevel { get; init; } =
        new ReadOnlyDictionary<int, string>(new Dictionary<int, string>());
    public string SelectedOption { get; init; } = string.Empty;
    public IReadOnlyList<SpecialisationAbilityRowState> AbilityRows { get; init; } = Array.Empty<SpecialisationAbilityRowState>();
    public string ValidationMessage { get; init; } = string.Empty;
    public bool IsComplete { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public string CardState { get; init; } = "Neutral";
}

public sealed class SpecialisationScreenState
{
    public IReadOnlyList<SpecialisationSectionState> Sections { get; init; } = Array.Empty<SpecialisationSectionState>();
    public SpecialisationSelectionState SelectionState { get; init; } = new();
    public IReadOnlyDictionary<string, string> PersistedSelections { get; init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    public string RaceSubtypeKey { get; init; } = string.Empty;
    public string RaceSubtypeValue { get; init; } = string.Empty;
    public string RaceSubtypeAbilityMapKey { get; init; } = string.Empty;
    public string LifeScaleOverride { get; init; } = string.Empty;
    public string ArmourAvailabilityOverride { get; init; } = string.Empty;
    public IReadOnlyList<string> ColourChoiceOverride { get; init; } = Array.Empty<string>();
    public GuildOverrideRules? GuildOverrides { get; init; }
    public bool IsComplete { get; init; }
}

public sealed class SpecialisationValidationState
{
    public bool IsComplete { get; init; }
    public IReadOnlyDictionary<string, string> SectionIssues { get; init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}

public sealed class SpecialisationAbilityRowState
{
    public int? Level { get; init; }
    public string Ability { get; init; } = string.Empty;
    public string AbilityKey { get; init; } = string.Empty;
    public string SpecialisationKey { get; init; } = string.Empty;
    public string SelectedOption { get; init; } = string.Empty;
    public string SelectedAbility { get; init; } = string.Empty;
}

public static class CharacterSpecialisationScreenCalculator
{
    public static CharacterSpecialisationContext LoadContext(
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> allClasses,
        IReadOnlyDictionary<string, PeopleRecord> allRaces,
        IReadOnlyDictionary<string, SpecialisationDefinition> definitions,
        IReadOnlyList<SpecialisationInjectionRule>? injectionRules = null)
    {
        var race = (draft.Race ?? string.Empty).Trim();
        var cls = (draft.Class ?? string.Empty).Trim();
        var subtype = (draft.RaceSubtypeValue ?? draft.RaceSubtype ?? string.Empty).Trim();

        allClasses.TryGetValue(cls, out var classRecord);
        allRaces.TryGetValue(race, out var raceRecord);

        return new CharacterSpecialisationContext
        {
            Draft = draft,
            Race = race,
            Class = cls,
            CurrentRaceSubtype = subtype,
            ClassRecord = classRecord,
            RaceRecord = raceRecord,
            Definitions = definitions,
            InjectionRules = injectionRules ?? Array.Empty<SpecialisationInjectionRule>()
        };
    }

    public static IReadOnlyList<RequiredChoice> ResolveRequiredChoices(CharacterSpecialisationContext context)
    {
        var required = new List<RequiredChoice>();
        var knownKeys = context.Definitions.Keys;

        if (context.ClassRecord?.Levels != null)
        {
            foreach (var levelEntry in context.ClassRecord.Levels)
            {
                if (!int.TryParse(levelEntry.Key, out var level))
                    continue;

                foreach (var ability in levelEntry.Value ?? new List<AbilityDefinition>())
                {
                    var key = FindSpecialisationKey(ability?.Name, knownKeys)
                              ?? FindSpecialisationKey(ability?.OverwriteKey, knownKeys)
                              ?? FindSpecialisationKey(ability?.UpdateKey, knownKeys);

                    if (key == null)
                        continue;

                    required.Add(new RequiredChoice
                    {
                        Source = RequiredChoiceSource.Class,
                        SourceName = context.Class,
                        SpecialisationKey = key,
                        Level = level
                    });
                }
            }
        }

        if (context.RaceRecord?.LevelledAbilities != null)
        {
            foreach (var levelEntry in context.RaceRecord.LevelledAbilities)
            {
                if (!int.TryParse(levelEntry.Key, out var level))
                    continue;

                foreach (var ability in levelEntry.Value ?? new List<AbilityDefinition>())
                {
                    var key = FindSpecialisationKey(ability?.Name, knownKeys)
                              ?? FindSpecialisationKey(ability?.OverwriteKey, knownKeys)
                              ?? FindSpecialisationKey(ability?.UpdateKey, knownKeys);

                    if (key == null)
                        continue;

                    required.Add(new RequiredChoice
                    {
                        Source = RequiredChoiceSource.Race,
                        SourceName = context.Race,
                        SpecialisationKey = key,
                        Level = level
                    });
                }
            }
        }

        return required;
    }

    public static IReadOnlyList<SpecialisationSectionSpec> BuildScreenSections(
        CharacterSpecialisationContext context,
        IReadOnlyList<RequiredChoice> requiredChoices)
    {
        var sections = new List<SpecialisationSectionSpec>();

        var subtype = context.RaceRecord?.Subtype;
        if (subtype != null)
        {
            var options = ResolveSubtypeOptions(subtype.OptionsSource);
            var abilityMapKey = (subtype.AbilityMapKey ?? string.Empty).Trim();
            var isHumanRace = string.Equals(context.Race, "Human", StringComparison.OrdinalIgnoreCase);

            var mappedOptions = context.Definitions.TryGetValue(abilityMapKey, out var subtypeDefinition)
                ? subtypeDefinition.ChoiceSets.FirstOrDefault(x => x.Mode == ChoiceMode.MappedSingle)?.Options
                : null;

            var sectionOptions = new List<ChoiceOption>();

            void AddSubtypeOption(string rawKey)
            {
                var key = (rawKey ?? string.Empty).Trim();
                if (key.Length == 0)
                    return;

                if (isHumanRace && key.Equals("Barbarian", StringComparison.OrdinalIgnoreCase))
                    return;

                if (sectionOptions.Any(existing => string.Equals(existing.Key, key, StringComparison.OrdinalIgnoreCase)))
                    return;

                var mapped = mappedOptions?.FirstOrDefault(option =>
                    string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase));

                if (mapped != null)
                {
                    sectionOptions.Add(string.IsNullOrWhiteSpace(mapped.Label)
                        ? new ChoiceOption
                        {
                            Key = mapped.Key,
                            Label = key,
                            Description = mapped.Description,
                            Grants = mapped.Grants,
                            Customisation = mapped.Customisation,
                            Effects = mapped.Effects,
                            Restrictions = mapped.Restrictions,
                            StrategyIds = mapped.StrategyIds,
                            Metadata = mapped.Metadata
                        }
                        : mapped);
                    return;
                }

                sectionOptions.Add(new ChoiceOption { Key = key, Label = key });
            }

            if (mappedOptions != null && mappedOptions.Count > 0)
            {
                var mappedKeys = mappedOptions
                    .Select(option => (option.Key ?? string.Empty).Trim())
                    .Where(key => key.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (options.Count == 0)
                {
                    options = mappedKeys;
                }
                else
                {
                    foreach (var key in mappedKeys)
                    {
                        if (!options.Contains(key, StringComparer.OrdinalIgnoreCase))
                            options.Add(key);
                    }
                }
            }

            foreach (var option in options)
                AddSubtypeOption(option);

            var title = string.IsNullOrWhiteSpace(subtype.DisplayName)
                ? $"{context.Race} subtype"
                : subtype.DisplayName.Trim();

            sections.Add(new SpecialisationSectionSpec
            {
                SectionId = $"subtype:{context.Race}:{subtype.Key}",
                DefinitionKey = abilityMapKey,
                DetailKey = abilityMapKey,
                Title = title,
                Subtitle = subtype.Description ?? string.Empty,
                Kind = SpecialisationSectionKind.RaceSubtype,
                Required = (subtype.SelectionMode ?? string.Empty).Contains("Required", StringComparison.OrdinalIgnoreCase),
                Options = sectionOptions,
                StrategyIds = ["section:race-subtype"],
                Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
                {
                    ["raceSubtypeKey"] = subtype.Key ?? string.Empty,
                    ["abilityMapKey"] = abilityMapKey
                })
            });
        }

        var grouped = requiredChoices
            .GroupBy(r => r.SpecialisationKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            if (!context.Definitions.TryGetValue(group.Key, out var definition))
                continue;

            var mappedSet = definition.ChoiceSets.FirstOrDefault(set => set.Mode == ChoiceMode.MappedSingle);
            if (mappedSet != null)
            {
                sections.Add(new SpecialisationSectionSpec
                {
                    SectionId = $"mapped:{group.Key}",
                    DefinitionKey = definition.Key,
                    DetailKey = definition.Key,
                    Title = group.Key,
                    Subtitle = BuildSubtitle(group),
                    Kind = SpecialisationSectionKind.Mapped,
                    Required = true,
                    Options = mappedSet.Options,
                    StrategyIds = mappedSet.StrategyIds
                });

                continue;
            }

            var choiceSet = definition.ChoiceSets.FirstOrDefault(set => set.Mode == ChoiceMode.Single || set.Mode == ChoiceMode.Lookup);
            if (choiceSet == null)
                continue;

            var eligibleOptions = choiceSet.Options
                .Where(option => string.IsNullOrWhiteSpace(ResolveRestrictionIssue(option, context)))
                .ToList();
            if (eligibleOptions.Count == 0)
                eligibleOptions = choiceSet.Options.ToList();

            var levels = BuildLevelsForGroup(group.Key, group);
            sections.Add(new SpecialisationSectionSpec
            {
                SectionId = $"choice:{group.Key}",
                DefinitionKey = definition.Key,
                DetailKey = definition.Key,
                Title = group.Key,
                Subtitle = BuildSubtitle(group),
                Kind = SpecialisationSectionKind.Choice,
                Required = true,
                Levels = levels,
                Options = eligibleOptions,
                StrategyIds = choiceSet.StrategyIds
            });
        }

        ApplyInjectionRules(context, sections);

        return sections;
    }

    public static SpecialisationScreenState ApplySavedSelections(
        CharacterSpecialisationContext context,
        IReadOnlyList<SpecialisationSectionSpec> sectionSpecs)
    {
        var choiceSelections = new Dictionary<string, ChoiceSelectionState>(StringComparer.OrdinalIgnoreCase);
        var mappedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var resolvedSubtype = context.CurrentRaceSubtype;
        var raceSubtypeKey = string.Empty;
        var raceSubtypeMapKey = string.Empty;

        foreach (var section in sectionSpecs)
        {
            if (section.Kind == SpecialisationSectionKind.RaceSubtype)
            {
                raceSubtypeKey = section.Metadata.TryGetValue("raceSubtypeKey", out var subtypeKey)
                    ? subtypeKey
                    : string.Empty;
                raceSubtypeMapKey = section.Metadata.TryGetValue("abilityMapKey", out var mapKey)
                    ? mapKey
                    : string.Empty;

                resolvedSubtype = NormalizeMappedSelection(section.Options, resolvedSubtype);

                if (string.IsNullOrWhiteSpace(resolvedSubtype)
                    && string.Equals(context.Race, "Human", StringComparison.OrdinalIgnoreCase)
                    && section.Options.Any(o => string.Equals(o.Key, "Standard", StringComparison.OrdinalIgnoreCase)))
                {
                    resolvedSubtype = ResolveChoiceSelectionToken(
                        section.Options.First(o => string.Equals(o.Key, "Standard", StringComparison.OrdinalIgnoreCase)));
                }

                if (!string.IsNullOrWhiteSpace(resolvedSubtype))
                    mappedSelections[section.SectionId] = resolvedSubtype;

                continue;
            }

            var saved = context.Draft.SpecialisationSelections.TryGetValue(section.Title, out var savedValue)
                ? (savedValue ?? string.Empty).Trim()
                : string.Empty;

            if (section.Kind == SpecialisationSectionKind.Mapped)
            {
                if (saved.Length > 0)
                    mappedSelections[section.SectionId] = saved;
                continue;
            }

            var selectedByLevel = new Dictionary<int, string>();
            var customisationByLevel = new Dictionary<int, string>();
            var orderedLevels = section.Levels.OrderBy(x => x).ToList();

            if (section.StrategyIds.Any(s => s.Equals("selection:multi-delimited", StringComparison.OrdinalIgnoreCase)))
            {
                var tokens = ParseStoredSelections(saved);
                var max = Math.Min(tokens.Count, orderedLevels.Count);
                for (var i = 0; i < max; i++)
                {
                    if (orderedLevels[i] <= 0)
                        continue;

                    var (selection, customisation) = ParseSelectionToken(tokens[i]);
                    if (selection.Length == 0)
                        continue;

                    selectedByLevel[orderedLevels[i]] = selection;
                    if (customisation.Length > 0)
                        customisationByLevel[orderedLevels[i]] = customisation;
                }
            }
            else if (saved.Length > 0 && orderedLevels.Count == 1)
            {
                var (selection, customisation) = ParseSelectionToken(saved);
                if (selection.Length > 0)
                    selectedByLevel[orderedLevels[0]] = selection;
                if (customisation.Length > 0)
                    customisationByLevel[orderedLevels[0]] = customisation;
            }

            choiceSelections[section.SectionId] = new ChoiceSelectionState
            {
                SelectedByLevel = new ReadOnlyDictionary<int, string>(selectedByLevel),
                CustomisationByLevel = new ReadOnlyDictionary<int, string>(customisationByLevel)
            };
        }

        var selectionState = new SpecialisationSelectionState
        {
            RaceSubtype = resolvedSubtype,
            ChoiceSelections = new ReadOnlyDictionary<string, ChoiceSelectionState>(choiceSelections),
            MappedSelections = new ReadOnlyDictionary<string, string>(mappedSelections)
        };

        return Recalculate(context, sectionSpecs, selectionState, raceSubtypeKey, raceSubtypeMapKey);
    }

    public static SpecialisationValidationState Validate(SpecialisationScreenState screenState, CharacterSpecialisationContext context)
    {
        var issues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in screenState.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.ValidationMessage))
                issues[section.Spec.SectionId] = section.ValidationMessage;

            if (section.Spec.Required && !section.IsComplete && !issues.ContainsKey(section.Spec.SectionId))
                issues[section.Spec.SectionId] = "Required choice missing.";
        }

        return new SpecialisationValidationState
        {
            IsComplete = issues.Count == 0 && screenState.IsComplete,
            SectionIssues = new ReadOnlyDictionary<string, string>(issues)
        };
    }

    public static SpecialisationScreenState Recalculate(
        CharacterSpecialisationContext context,
        IReadOnlyList<SpecialisationSectionSpec> sectionSpecs,
        SpecialisationSelectionState selectionState,
        string raceSubtypeKey = "",
        string raceSubtypeMapKey = "")
    {
        var sections = new List<SpecialisationSectionState>();
        var persistedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var selectedSubtype = selectionState.RaceSubtype;
        var lifeScaleOverride = string.Empty;
        var armourOverride = string.Empty;
        var colourChoiceOverride = new List<string>();
        GuildOverrideRules? guildOverrides = null;

        foreach (var spec in sectionSpecs)
        {
            switch (spec.Kind)
            {
                case SpecialisationSectionKind.Choice:
                {
                    selectionState.ChoiceSelections.TryGetValue(spec.SectionId, out var choiceSelection);
                    var selectedByLevel = choiceSelection?.SelectedByLevel?.ToDictionary(k => k.Key, v => v.Value)
                                          ?? new Dictionary<int, string>();
                    var customisationByLevel = choiceSelection?.CustomisationByLevel?.ToDictionary(k => k.Key, v => v.Value)
                                              ?? new Dictionary<int, string>();

                    var validation = ValidateChoiceSection(spec, selectedByLevel);
                    if (string.IsNullOrWhiteSpace(validation)
                        && spec.StrategyIds.Any(id => id.Equals("validation:option-restrictions", StringComparison.OrdinalIgnoreCase)))
                    {
                        validation = ValidateSelectedChoiceRestrictions(spec, selectedByLevel, context);
                    }
                    var selectedCount = selectedByLevel.Values.Count(value => !string.IsNullOrWhiteSpace(value));
                    var requiredCount = spec.Required ? spec.Levels.Count : 0;

                    var complete = spec.Required
                        ? selectedCount == spec.Levels.Count && string.IsNullOrWhiteSpace(validation)
                        : string.IsNullOrWhiteSpace(validation);

                    if (spec.StrategyIds.Any(id => id.Equals("selection:multi-delimited", StringComparison.OrdinalIgnoreCase))
                        && selectedCount > 0)
                    {
                        var stored = spec.Levels
                            .OrderBy(x => x)
                            .Select(level =>
                            {
                                var picked = selectedByLevel.TryGetValue(level, out var selectedToken)
                                    ? (selectedToken ?? string.Empty).Trim()
                                    : string.Empty;
                                var custom = customisationByLevel.TryGetValue(level, out var customToken)
                                    ? (customToken ?? string.Empty).Trim()
                                    : string.Empty;
                                return ComposeSelectionToken(picked, custom);
                            })
                            .Where(value => value.Length > 0)
                            .ToList();

                        if (stored.Count > 0)
                            persistedSelections[spec.Title] = string.Join(" | ", stored);
                    }
                    else if (spec.Levels.Count == 1
                             && selectedByLevel.TryGetValue(spec.Levels[0], out var single)
                             && !string.IsNullOrWhiteSpace(single))
                    {
                        var level = spec.Levels[0];
                        var custom = customisationByLevel.TryGetValue(level, out var customToken)
                            ? (customToken ?? string.Empty).Trim()
                            : string.Empty;
                        persistedSelections[spec.Title] = ComposeSelectionToken(single, custom);
                    }

                    sections.Add(new SpecialisationSectionState
                    {
                        Spec = spec,
                        SelectedByLevel = new ReadOnlyDictionary<int, string>(selectedByLevel),
                        CustomisationByLevel = new ReadOnlyDictionary<int, string>(customisationByLevel),
                        IsComplete = complete,
                        ValidationMessage = validation,
                        StatusText = spec.Required
                            ? $"{selectedCount}/{spec.Levels.Count}"
                            : (selectedCount == 0 ? "Optional" : $"{selectedCount}/{spec.Levels.Count}"),
                        CardState = ResolveCardState(complete, validation, spec.Required, selectedCount > 0)
                    });

                    break;
                }

                case SpecialisationSectionKind.Mapped:
                {
                    var selectedToken = selectionState.MappedSelections.TryGetValue(spec.SectionId, out var mapped)
                        ? (mapped ?? string.Empty).Trim()
                        : string.Empty;

                    var option = ResolveSelectedOption(spec.Options, selectedToken);
                    var selected = (option?.Key ?? string.Empty).Trim();
                    var issue = ResolveRestrictionIssue(option, context);
                    var rows = BuildAbilityRows(spec.DetailKey, selected, option);
                    var complete = (!spec.Required || selected.Length > 0) && issue.Length == 0;

                    if (selected.Length > 0)
                        persistedSelections[spec.Title] = selected;

                    if (option?.Effects.GuildOverrides != null)
                        guildOverrides = GuildOverrideRules.Merge(guildOverrides, option.Effects.GuildOverrides);

                    sections.Add(new SpecialisationSectionState
                    {
                        Spec = spec,
                        SelectedOption = selected,
                        AbilityRows = rows,
                        IsComplete = complete,
                        ValidationMessage = issue,
                        StatusText = issue.Length > 0 ? "Issue" : (selected.Length > 0 ? "Selected" : (spec.Required ? "Required" : "Optional")),
                        CardState = issue.Length > 0 ? "Issue" : (selected.Length > 0 ? "Success" : (spec.Required ? "Error" : "Neutral"))
                    });

                    break;
                }

                case SpecialisationSectionKind.RaceSubtype:
                {
                    if (selectedSubtype.Length == 0)
                    {
                        selectedSubtype = selectionState.MappedSelections.TryGetValue(spec.SectionId, out var mapped)
                            ? (mapped ?? string.Empty).Trim()
                            : string.Empty;
                    }

                    if (selectedSubtype.Length == 0
                        && string.Equals(context.Race, "Human", StringComparison.OrdinalIgnoreCase))
                    {
                        var standard = spec.Options.FirstOrDefault(option =>
                            string.Equals((option.Key ?? string.Empty).Trim(), "Standard", StringComparison.OrdinalIgnoreCase)
                            || string.Equals((option.Label ?? string.Empty).Trim(), "Standard", StringComparison.OrdinalIgnoreCase));
                        if (standard != null)
                            selectedSubtype = ResolveChoiceSelectionToken(standard);
                    }

                    var option = ResolveSelectedOption(spec.Options, selectedSubtype);
                    selectedSubtype = (option?.Key ?? string.Empty).Trim();

                    var mappedOption = ResolveMappedOptionFromDefinition(spec.DefinitionKey, selectedSubtype, context.Definitions);
                    var issue = ResolveRestrictionIssue(mappedOption, context);
                    var rows = BuildAbilityRows(spec.DetailKey, selectedSubtype, mappedOption);
                    var complete = (!spec.Required || selectedSubtype.Length > 0) && issue.Length == 0;

                    if (mappedOption?.Effects.GuildOverrides != null)
                        guildOverrides = GuildOverrideRules.Merge(guildOverrides, mappedOption.Effects.GuildOverrides);

                    if (mappedOption != null)
                    {
                        lifeScaleOverride = mappedOption.Effects.LifeScaleOverride;
                        armourOverride = mappedOption.Effects.ArmourAvailabilityOverride;
                        colourChoiceOverride = mappedOption.Effects.ColourChoiceOverride?.ToList() ?? new List<string>();
                    }

                    sections.Add(new SpecialisationSectionState
                    {
                        Spec = spec,
                        SelectedOption = selectedSubtype,
                        AbilityRows = rows,
                        IsComplete = complete,
                        ValidationMessage = issue,
                        StatusText = issue.Length > 0 ? "Issue" : (selectedSubtype.Length > 0 ? "Selected" : (spec.Required ? "Required" : "Optional")),
                        CardState = issue.Length > 0 ? "Issue" : (selectedSubtype.Length > 0 ? "Success" : (spec.Required ? "Error" : "Neutral"))
                    });

                    break;
                }
            }
        }

        sections = ApplyCrossSectionChoiceValidation(sections);
        var isComplete = sections.All(section => section.IsComplete);

        return new SpecialisationScreenState
        {
            Sections = sections,
            SelectionState = selectionState,
            PersistedSelections = new ReadOnlyDictionary<string, string>(persistedSelections),
            RaceSubtypeKey = raceSubtypeKey,
            RaceSubtypeValue = selectedSubtype,
            RaceSubtypeAbilityMapKey = raceSubtypeMapKey,
            LifeScaleOverride = lifeScaleOverride,
            ArmourAvailabilityOverride = armourOverride,
            ColourChoiceOverride = colourChoiceOverride,
            GuildOverrides = guildOverrides,
            IsComplete = isComplete
        };
    }

    private static ChoiceOption? ResolveMappedOptionFromDefinition(
        string definitionKey,
        string selectedOption,
        IReadOnlyDictionary<string, SpecialisationDefinition> definitions)
    {
        if (string.IsNullOrWhiteSpace(definitionKey)
            || string.IsNullOrWhiteSpace(selectedOption)
            || !definitions.TryGetValue(definitionKey, out var definition))
        {
            return null;
        }

        var mappedSet = definition.ChoiceSets.FirstOrDefault(x => x.Mode == ChoiceMode.MappedSingle);
        return mappedSet?.Options.FirstOrDefault(o => string.Equals(o.Key, selectedOption, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveCardState(bool complete, string validation, bool required, bool hasSelection)
    {
        if (!string.IsNullOrWhiteSpace(validation))
            return validation.StartsWith("Issue", StringComparison.OrdinalIgnoreCase) ? "Issue" : "Error";

        if (complete)
            return "Success";

        if (required && !hasSelection)
            return "Error";

        return "Neutral";
    }

    private static string ValidateChoiceSection(SpecialisationSectionSpec spec, IDictionary<int, string> selectedByLevel)
    {
        if (selectedByLevel.Count == 0)
            return string.Empty;

        var selections = selectedByLevel.Values
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        if (selections.Count != selections.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            && !AllowsDuplicateSlots(spec))
        {
            return "Duplicate selections detected. Choose different abilities for each level.";
        }

        if (spec.StrategyIds.Any(s => s.Equals("validation:faerie-opposites", StringComparison.OrdinalIgnoreCase))
            && selections.Count >= 2)
        {
            var first = ToMagicColour(selections[0]);
            var second = ToMagicColour(selections[1]);
            if (first.HasValue && second.HasValue && AreFaerieOpposites(first.Value, second.Value))
                return "Faerie colours cannot be opposite pairs.";
        }

        if (spec.StrategyIds.Any(s => s.Equals("validation:min-level-by-option-metadata", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var pick in selectedByLevel.OrderBy(entry => entry.Key))
            {
                var selected = (pick.Value ?? string.Empty).Trim();
                if (selected.Length == 0)
                    continue;

                var option = ResolveSelectedOption(spec.Options, selected);
                if (option == null)
                    continue;

                if (!TryGetOptionMetadataLevel(option, out var minLevel))
                {
                    continue;
                }

                if (DecodeBaseLevel(pick.Key) < minLevel)
                    return $"{option.Label} is only available from level {minLevel}.";
            }
        }

        if (spec.StrategyIds.Any(s => s.Equals("validation:exact-level-by-option-metadata", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var pick in selectedByLevel.OrderBy(entry => entry.Key))
            {
                var selected = (pick.Value ?? string.Empty).Trim();
                if (selected.Length == 0)
                    continue;

                var option = ResolveSelectedOption(spec.Options, selected);
                if (option == null)
                    continue;

                if (!TryGetOptionMetadataLevel(option, out var exactLevel))
                    continue;

                if (DecodeBaseLevel(pick.Key) != exactLevel)
                    return $"{option.Label} is only available at level {exactLevel}.";
            }
        }

        return string.Empty;
    }

    private static string ValidateSelectedChoiceRestrictions(
        SpecialisationSectionSpec spec,
        IDictionary<int, string> selectedByLevel,
        CharacterSpecialisationContext context)
    {
        foreach (var selected in selectedByLevel
                     .OrderBy(entry => entry.Key)
                     .Select(entry => (entry.Value ?? string.Empty).Trim())
                     .Where(value => value.Length > 0))
        {
            var option = ResolveSelectedOption(spec.Options, selected);
            var issue = ResolveRestrictionIssue(option, context);
            if (!string.IsNullOrWhiteSpace(issue))
                return issue;
        }

        return string.Empty;
    }

    private static ChoiceOption? ResolveSelectedOption(IReadOnlyList<ChoiceOption> options, string selected)
    {
        if (selected.Length == 0 || options.Count == 0)
            return null;

        return options.FirstOrDefault(option =>
                   option.Key.Equals(selected, StringComparison.OrdinalIgnoreCase))
               ?? options.FirstOrDefault(option =>
                   option.Label.Equals(selected, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveChoiceSelectionToken(ChoiceOption? option)
    {
        if (option == null)
            return string.Empty;

        var key = (option.Key ?? string.Empty).Trim();
        if (key.Length > 0)
            return key;

        return (option.Label ?? string.Empty).Trim();
    }

    private static string NormalizeMappedSelection(IReadOnlyList<ChoiceOption> options, string? selectedToken)
    {
        var selected = (selectedToken ?? string.Empty).Trim();
        if (selected.Length == 0)
            return string.Empty;

        var option = ResolveSelectedOption(options, selected);
        return (option?.Key ?? string.Empty).Trim();
    }

    private static bool TryGetOptionMetadataLevel(ChoiceOption option, out int level)
    {
        level = 0;
        if (option.Metadata == null || option.Metadata.Count == 0)
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

    private static List<SpecialisationSectionState> ApplyCrossSectionChoiceValidation(List<SpecialisationSectionState> sections)
    {
        if (sections.Count == 0)
            return sections;

        var issuesBySection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const string duplicateMessage = "Duplicate selections detected across linked choice groups.";

        var groupedSections = sections
            .Where(section => section.Spec.Kind == SpecialisationSectionKind.Choice)
            .Select(section => new
            {
                Section = section,
                Group = ResolveUniqueSelectionGroup(section.Spec)
            })
            .Where(entry => entry.Group.Length > 0)
            .GroupBy(entry => entry.Group, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groupedSections)
        {
            var seenBySelection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in group)
            {
                var sectionId = entry.Section.Spec.SectionId;
                var picks = entry.Section.SelectedByLevel.Values
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var pick in picks)
                {
                    if (!seenBySelection.TryAdd(pick, sectionId))
                    {
                        issuesBySection[sectionId] = duplicateMessage;
                        issuesBySection[seenBySelection[pick]] = duplicateMessage;
                    }
                }
            }
        }

        if (issuesBySection.Count == 0)
            return sections;

        return sections
            .Select(section =>
            {
                if (!issuesBySection.TryGetValue(section.Spec.SectionId, out var duplicateIssue)
                    || !string.IsNullOrWhiteSpace(section.ValidationMessage))
                {
                    return section;
                }

                return new SpecialisationSectionState
                {
                    Spec = section.Spec,
                    SelectedByLevel = section.SelectedByLevel,
                    CustomisationByLevel = section.CustomisationByLevel,
                    SelectedOption = section.SelectedOption,
                    AbilityRows = section.AbilityRows,
                    ValidationMessage = duplicateIssue,
                    IsComplete = false,
                    StatusText = "Issue",
                    CardState = "Issue"
                };
            })
            .ToList();
    }

    private static string ResolveUniqueSelectionGroup(SpecialisationSectionSpec spec)
    {
        if (!spec.StrategyIds.Any(id => id.Equals("validation:unique-across-group", StringComparison.OrdinalIgnoreCase)))
            return string.Empty;

        var groupStrategy = spec.StrategyIds.FirstOrDefault(id =>
            id.StartsWith("selection-group:", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(groupStrategy))
            return string.Empty;

        return groupStrategy["selection-group:".Length..].Trim();
    }

    private static bool AllowsDuplicateSlots(SpecialisationSectionSpec spec)
        => spec.StrategyIds.Any(s => s.Equals("allow:duplicate-slots", StringComparison.OrdinalIgnoreCase))
           || string.Equals(spec.Title, "Faerie Colour", StringComparison.OrdinalIgnoreCase);

    private static bool AreFaerieOpposites(MagicColours left, MagicColours right)
        => MagicColourOppositionRules.AreOpposites(left, right);

    private static MagicColours? ToMagicColour(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return null;

        if (Enum.TryParse<MagicColours>(text.Replace(" ", string.Empty), ignoreCase: true, out var parsed))
            return parsed;

        foreach (var colour in Enum.GetValues<MagicColours>())
        {
            var label = EnumDisplayFormatter.Format(colour);
            if (label.Equals(text, StringComparison.OrdinalIgnoreCase))
                return colour;
        }

        return null;
    }

    private static IReadOnlyList<SpecialisationAbilityRowState> BuildAbilityRows(string detailKey, string selectedOption, ChoiceOption? option)
    {
        if (option == null)
            return Array.Empty<SpecialisationAbilityRowState>();

        return option.Grants
            .Where(grant => grant != null && !string.IsNullOrWhiteSpace(grant.Ability?.Name))
            .OrderBy(grant => grant.Level ?? int.MaxValue)
            .ThenBy(grant => grant.Ability.Name, StringComparer.OrdinalIgnoreCase)
            .Select(grant => new SpecialisationAbilityRowState
            {
                Level = grant.Level,
                Ability = grant.Ability.Name,
                AbilityKey = grant.Ability.Key ?? string.Empty,
                SpecialisationKey = detailKey,
                SelectedOption = selectedOption,
                SelectedAbility = grant.Ability.Name
            })
            .ToList();
    }

    private static string ResolveRestrictionIssue(ChoiceOption? option, CharacterSpecialisationContext context)
    {
        if (option == null)
            return string.Empty;

        var classAllowed = IsClassAllowed(option.Restrictions.ClassRestriction, context);
        var alignmentAllowed = IsAlignmentAllowed(option.Restrictions.AlignmentRestriction, context.Draft);
        var raceAllowed = IsRaceAllowed(option.Restrictions.RaceRestriction, context);
        var raceSubtypeAllowed = IsSubtypeAllowed(option.Restrictions.RaceSubtypeRestriction, context);

        if (classAllowed && alignmentAllowed && raceAllowed && raceSubtypeAllowed)
            return string.Empty;

        var failures = new List<string>();
        if (!classAllowed)
            failures.Add("Class");
        if (!alignmentAllowed)
            failures.Add("Alignment");
        if (!raceAllowed)
            failures.Add("Race");
        if (!raceSubtypeAllowed)
            failures.Add("Race subtype");

        return failures.Count switch
        {
            <= 0 => string.Empty,
            1 => $"{failures[0]} requirements conflict with the current character.",
            2 => $"{failures[0]} and {failures[1]} requirements conflict with the current character.",
            _ => $"{string.Join(", ", failures.Take(failures.Count - 1))}, and {failures[^1]} requirements conflict with the current character."
        };
    }

    private static bool IsClassAllowed(IReadOnlyList<string>? restrictions, CharacterSpecialisationContext context)
    {
        var className = context.Class;
        if (string.IsNullOrWhiteSpace(className))
            return true;

        var tokens = restrictions?
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .ToList() ?? new List<string>();

        if (tokens.Count == 0)
            return true;

        var classTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddClassToken(classTokens, className);

        foreach (var bracket in context.ClassRecord?.Brackets ?? new List<string>())
            AddClassToken(classTokens, bracket);

        foreach (var token in tokens)
        {
            var normalized = NormalizeClassToken(token);
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

    private static bool IsRaceAllowed(IReadOnlyList<string>? restrictions, CharacterSpecialisationContext context)
    {
        var raceName = (context.Race ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return true;

        var tokens = restrictions?
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .ToList() ?? new List<string>();

        if (tokens.Count == 0)
            return true;

        var raceToken = NormalizeRaceToken(raceName);
        if (raceToken.Length == 0)
            return true;

        var singularRace = TrimPluralToken(raceToken);
        foreach (var token in tokens)
        {
            var normalized = NormalizeRaceToken(token);
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

    private static bool IsSubtypeAllowed(IReadOnlyList<string>? restrictions, CharacterSpecialisationContext context)
    {
        var subtype = (context.CurrentRaceSubtype ?? string.Empty).Trim();

        var tokens = restrictions?
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .ToList() ?? new List<string>();

        if (tokens.Count == 0)
            return true;

        if (subtype.Length == 0)
            return false;

        var subtypeToken = NormalizeRaceToken(subtype);
        if (subtypeToken.Length == 0)
            return false;

        var singularSubtype = TrimPluralToken(subtypeToken);
        foreach (var token in tokens)
        {
            var normalized = NormalizeRaceToken(token);
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

    private static void AddClassToken(HashSet<string> sink, string? value)
    {
        var token = NormalizeClassToken(value);
        if (token.Length > 0)
            sink.Add(token);
    }

    private static string NormalizeClassToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string NormalizeRaceToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
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

    private static bool IsAlignmentAllowed(IReadOnlyList<string>? restrictions, CharacterDraft draft)
    {
        var normalized = restrictions?
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (normalized.Count == 0)
            return true;

        var allowedAlignments = draft.AvailableAlignments?.ToList() ?? new List<Alignment>();
        if (allowedAlignments.Count == 0 && draft.Alignment is Alignment selectedAlignment)
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

    private static bool TryParseAlignmentPair(string value, out Alignment alignment)
    {
        alignment = default;
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        if (text.Equals("True Neutral", StringComparison.OrdinalIgnoreCase))
        {
            alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral);
            return true;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        if (!Enum.TryParse<OrderAxis>(parts[0], ignoreCase: true, out var order)
            || !Enum.TryParse<MoralAxis>(parts[1], ignoreCase: true, out var moral))
        {
            return false;
        }

        alignment = new Alignment(order, moral);
        return true;
    }

    private static string NormalizeAlignmentKeyword(string value)
        => new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();

    private static void ApplyInjectionRules(CharacterSpecialisationContext context, List<SpecialisationSectionSpec> sections)
    {
        if (context.InjectionRules.Count == 0)
            return;

        foreach (var rule in context.InjectionRules)
        {
            if (rule == null || !DoesRuleMatchContext(rule, context))
                continue;

            var spec = BuildSectionFromRule(context, rule);
            if (spec == null)
                continue;

            var existingByIdIndex = !string.IsNullOrWhiteSpace(spec.SectionId)
                ? sections.FindIndex(existing => existing.SectionId.Equals(spec.SectionId, StringComparison.OrdinalIgnoreCase))
                : -1;

            if (existingByIdIndex >= 0)
            {
                sections[existingByIdIndex] = spec;
                continue;
            }

            if (sections.Any(existing => existing.Title.Equals(spec.Title, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (rule.Section.InsertIndex is int insertIndex
                && insertIndex >= 0
                && insertIndex <= sections.Count)
            {
                sections.Insert(insertIndex, spec);
            }
            else
            {
                sections.Add(spec);
            }
        }
    }

    private static bool DoesRuleMatchContext(SpecialisationInjectionRule rule, CharacterSpecialisationContext context)
    {
        var conditions = rule.Conditions;
        if (!MatchesExact(conditions.Race, context.Race))
            return false;

        if (!MatchesExact(conditions.Subtype, context.CurrentRaceSubtype))
            return false;

        if (!MatchesExact(conditions.Class, context.Class))
            return false;

        var className = (context.Class ?? string.Empty).Trim();
        if (conditions.ClassIn.Count > 0
            && !conditions.ClassIn.Any(candidate => candidate.Equals(className, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (conditions.ClassNotIn.Count > 0
            && conditions.ClassNotIn.Any(candidate => candidate.Equals(className, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (conditions.PeopleTypeIn.Count > 0)
        {
            var peopleTypes = ResolveContextPeopleTypes(context);
            var peopleTypeAllowed = peopleTypes.Any(type =>
                conditions.PeopleTypeIn.Any(candidate => candidate.Equals(type, StringComparison.OrdinalIgnoreCase)));

            if (!peopleTypeAllowed)
                return false;
        }

        return true;
    }

    private static IReadOnlyList<string> ResolveContextPeopleTypes(CharacterSpecialisationContext context)
    {
        var peopleTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in context.RaceRecord?.PeopleType ?? new List<string>())
        {
            var token = (type ?? string.Empty).Trim();
            if (token.Length > 0)
                peopleTypes.Add(token);
        }

        if (HasBarbarianPeopleType(context.Draft))
            peopleTypes.Add("Tribal");

        return peopleTypes.ToList();
    }

    private static bool HasBarbarianPeopleType(CharacterDraft? draft)
    {
        if (draft?.SpecialisationSelections == null || draft.SpecialisationSelections.Count == 0)
            return false;

        if (draft.SpecialisationSelections.TryGetValue("Barbarian", out var directSelection)
            && string.Equals((directSelection ?? string.Empty).Trim(), "Barbarian", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return draft.SpecialisationSelections.Values.Any(selection =>
            string.Equals((selection ?? string.Empty).Trim(), "Barbarian", StringComparison.OrdinalIgnoreCase));
    }

    private static SpecialisationSectionSpec? BuildSectionFromRule(
        CharacterSpecialisationContext context,
        SpecialisationInjectionRule rule)
    {
        var section = rule.Section;
        if (!context.Definitions.TryGetValue(section.DefinitionKey, out var definition))
            return null;

        var kind = ParseSectionKind(section.SectionType);
        var choiceSet = kind switch
        {
            SpecialisationSectionKind.Choice => definition.ChoiceSets.FirstOrDefault(set =>
                set.Mode == ChoiceMode.Single || set.Mode == ChoiceMode.Lookup),
            SpecialisationSectionKind.Mapped => definition.ChoiceSets.FirstOrDefault(set => set.Mode == ChoiceMode.MappedSingle),
            _ => null
        };

        if (choiceSet == null || choiceSet.Options.Count == 0)
            return null;

        var options = ApplyOptionFilter(context, choiceSet.Options, section.OptionFilter);
        if (options.Count == 0)
            return null;

        var title = (section.Title ?? string.Empty).Trim();
        if (title.Length == 0)
            title = definition.Key;

        var sectionId = (section.SectionId ?? string.Empty).Trim();
        if (sectionId.Length == 0)
        {
            var prefix = kind switch
            {
                SpecialisationSectionKind.Choice => "choice",
                SpecialisationSectionKind.Mapped => "mapped",
                SpecialisationSectionKind.RaceSubtype => "subtype",
                _ => "section"
            };
            sectionId = $"{prefix}:{title}";
        }

        var required = ResolveRuleRequired(section, context);
        var levels = kind == SpecialisationSectionKind.Choice
            ? ResolveRuleLevels(section)
            : Array.Empty<int>();

        var strategyIds = choiceSet.StrategyIds
            .Concat(rule.StrategyIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var metadata = new Dictionary<string, string>(rule.Section.Metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(rule.Id))
            metadata["injectionRuleId"] = rule.Id;

        return new SpecialisationSectionSpec
        {
            SectionId = sectionId,
            DefinitionKey = section.DefinitionKey,
            DetailKey = string.IsNullOrWhiteSpace(section.DetailKey) ? section.DefinitionKey : section.DetailKey,
            Title = title,
            Subtitle = ResolveRuleSubtitle(section, kind),
            Kind = kind,
            Required = required,
            Levels = levels,
            Options = options,
            StrategyIds = strategyIds,
            Metadata = new ReadOnlyDictionary<string, string>(metadata)
        };
    }

    private static SpecialisationSectionKind ParseSectionKind(string? sectionType)
    {
        var normalized = (sectionType ?? string.Empty).Trim();
        if (normalized.Equals("Choice", StringComparison.OrdinalIgnoreCase))
            return SpecialisationSectionKind.Choice;

        if (normalized.Equals("RaceSubtype", StringComparison.OrdinalIgnoreCase))
            return SpecialisationSectionKind.RaceSubtype;

        return SpecialisationSectionKind.Mapped;
    }

    private static IReadOnlyList<int> ResolveRuleLevels(InjectionRuleSection section)
    {
        if (section.Levels.Count > 0)
            return section.Levels.Distinct().OrderBy(x => x).ToList();

        return [1];
    }

    private static string ResolveRuleSubtitle(InjectionRuleSection section, SpecialisationSectionKind kind)
    {
        if (!string.IsNullOrWhiteSpace(section.Subtitle))
            return section.Subtitle.Trim();

        if (section.Metadata.TryGetValue("subtitle", out var subtitle)
            && !string.IsNullOrWhiteSpace(subtitle))
        {
            return subtitle.Trim();
        }

        if (kind == SpecialisationSectionKind.Mapped)
            return "Select an option to unlock its benefits.";

        if (kind == SpecialisationSectionKind.Choice)
            return "Select one option.";

        return string.Empty;
    }

    private static bool ResolveRuleRequired(InjectionRuleSection section, CharacterSpecialisationContext context)
    {
        var required = section.Required ?? true;
        if (section.RequiredWhenClassHasPowerBase)
            required = ClassHasPowerBase(context.ClassRecord);

        var className = (context.Class ?? string.Empty).Trim();
        if (section.OptionalClassIn.Any(candidate => candidate.Equals(className, StringComparison.OrdinalIgnoreCase)))
            required = false;

        return required;
    }

    private static IReadOnlyList<ChoiceOption> ApplyOptionFilter(
        CharacterSpecialisationContext context,
        IReadOnlyList<ChoiceOption> options,
        InjectionOptionFilter? filter)
    {
        if (filter == null)
            return options;

        if (!context.Definitions.TryGetValue(filter.SourceDefinitionKey, out var sourceDefinition))
            return Array.Empty<ChoiceOption>();

        var sourceSet = sourceDefinition.ChoiceSets.FirstOrDefault(set => set.Mode == ChoiceMode.MappedSingle);
        var sourceOption = sourceSet?.Options.FirstOrDefault(option =>
            option.Key.Equals(filter.SourceOption, StringComparison.OrdinalIgnoreCase));
        if (sourceOption == null)
            return Array.Empty<ChoiceOption>();

        var allowedOptions = ResolveEffectValues(sourceOption.Effects, filter.EffectListField);
        if (allowedOptions.Count == 0)
            return Array.Empty<ChoiceOption>();

        return options
            .Where(option =>
                allowedOptions.Any(allowed =>
                    allowed.Equals(option.Key, StringComparison.OrdinalIgnoreCase)
                    || allowed.Equals(option.Label, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static IReadOnlyList<string> ResolveEffectValues(OptionEffects effects, string effectListField)
    {
        var normalized = (effectListField ?? string.Empty).Trim();
        if (normalized.Equals("HedgeOrCircle", StringComparison.OrdinalIgnoreCase))
            return effects.HedgeOrCircle ?? Array.Empty<string>();

        if (normalized.Equals("ColourChoiceOverride", StringComparison.OrdinalIgnoreCase))
            return effects.ColourChoiceOverride ?? Array.Empty<string>();

        if (normalized.Equals("Notes", StringComparison.OrdinalIgnoreCase))
            return effects.Notes ?? Array.Empty<string>();

        return Array.Empty<string>();
    }

    private static bool MatchesExact(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return true;

        return string.Equals(
            expected.Trim(),
            (actual ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool ClassHasPowerBase(CharacterClassRecord? classRecord)
    {
        var powerBase = classRecord?.Powerbase?.Any(value => !string.IsNullOrWhiteSpace(value)) == true;
        return powerBase;
    }

    private static IReadOnlyList<int> BuildLevelsForGroup(string key, IEnumerable<RequiredChoice> grouped)
    {
        var orderedLevels = grouped
            .Select(x => x.Level)
            .OrderBy(x => x)
            .ToList();

        if (SupportsGrantMultiplicity(key))
        {
            var perLevelCount = new Dictionary<int, int>();
            var expanded = new List<int>(orderedLevels.Count);
            foreach (var level in orderedLevels)
            {
                var duplicateIndex = perLevelCount.TryGetValue(level, out var count) ? count : 0;
                perLevelCount[level] = duplicateIndex + 1;
                expanded.Add(duplicateIndex == 0 ? level : EncodeDuplicateLevel(level, duplicateIndex));
            }

            return expanded;
        }

        return orderedLevels.Distinct().ToList();
    }

    private static bool SupportsGrantMultiplicity(string key)
        => key.Equals("Ward pact", StringComparison.OrdinalIgnoreCase)
           || key.Equals("Wizard Colour", StringComparison.OrdinalIgnoreCase)
           || key.Equals("Faerie Colour", StringComparison.OrdinalIgnoreCase)
           || key.Equals("Standard Scout skill", StringComparison.OrdinalIgnoreCase)
           || key.Equals("Specialist Scout skill", StringComparison.OrdinalIgnoreCase);

    private static int EncodeDuplicateLevel(int baseLevel, int duplicateIndex)
        => baseLevel * 100 + duplicateIndex;

    private static int DecodeBaseLevel(int encodedLevel)
        => encodedLevel > 99 ? encodedLevel / 100 : encodedLevel;

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

    private static string? FindSpecialisationKey(string? rawToken, IEnumerable<string> knownKeys)
    {
        var token = (rawToken ?? string.Empty).Trim();
        if (token.Length == 0)
            return null;

        foreach (var key in knownKeys)
        {
            if (string.Equals(key, token, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return null;
    }

    private static List<string> ParseStoredSelections(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw
            .Split(new[] { '|', ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string Selection, string Customisation) ParseSelectionToken(string? token)
    {
        var normalized = (token ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return (string.Empty, string.Empty);

        var parts = normalized.Split(new[] { "::" }, 2, StringSplitOptions.None);
        var selection = (parts[0] ?? string.Empty).Trim();
        var customisation = parts.Length > 1
            ? (parts[1] ?? string.Empty).Trim()
            : string.Empty;

        return (selection, customisation);
    }

    private static string ComposeSelectionToken(string? selection, string? customisation)
    {
        var baseSelection = (selection ?? string.Empty).Trim();
        if (baseSelection.Length == 0)
            return string.Empty;

        var custom = (customisation ?? string.Empty).Trim();
        return custom.Length == 0
            ? baseSelection
            : $"{baseSelection}::{custom}";
    }

    private static List<string> ResolveSubtypeOptions(string? optionsSource)
    {
        var source = (optionsSource ?? string.Empty).Trim();
        if (source.Length == 0)
            return new List<string>();

        if (source.StartsWith("Enum:", StringComparison.OrdinalIgnoreCase))
        {
            var enumName = source["Enum:".Length..].Trim();
            if (enumName.Length == 0)
                return new List<string>();

            if (enumName.Equals("ElfColours", StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    "Fire", "Air", "Earth", "Aquatic", "Light", "Dark", "Twilight", "Bronze", "Ebony", "Gold",
                    "Ivory", "Silver", "Jade", "Onyx", "Winter", "Spring", "Summer", "Autumn"
                ];
            }

            if (enumName.Equals("AthfanalColours", StringComparison.OrdinalIgnoreCase))
            {
                return
                [
                    "Fire", "Aquatic", "Earth", "Air", "Twilight", "Light", "Dark"
                ];
            }

            var enumType = ReflectionHelper.FindEnumTypeByName(enumName);
            if (enumType == null)
                return new List<string>();

            return Enum.GetNames(enumType).ToList();
        }

        if (source.Contains(',', StringComparison.Ordinal))
        {
            return source
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<string>();
    }
}
