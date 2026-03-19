using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Characters;

namespace labyItems.Services.Specialisations;

public static class SpecialisationDefinitionRepository
{
    private const string LegacySpecialisationPath = "specialisation/specialisation.json";
    private const string AbilitiesPath = "specialisation/abilities.json";
    private const string ChoiceSetsPath = "specialisation/choice-sets.json";
    private const string ClassSpecialisationsPath = "specialisation/class-specialisations.json";
    private const string RaceSubtypesPath = "specialisation/race-subtypes.json";
    private const string OverridesPath = "specialisation/specialisation-overrides.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new GuildOverrideRulesConverter() }
    };

    private static readonly HashSet<string> ReservedTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "definitions",
        "Definitions",
        "AbilityRefs",
        "AbilityReferences",
        "$refs",
        "$abilityRefs",
        "$definitions",
        "$schema",
        "schemaVersion",
        "$schemaVersion",
        "injectionRules",
        "InjectionRules"
    };

    private static readonly HashSet<string> KnownChoiceMetadataFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Description",
        "Levels",
        "LifeScaleOverride",
        "ArmourAvailabilityOverride",
        "ColourChoiceOverride",
        "GuildOverrides",
        "HedgeOrCircle",
        "ClassRestriction",
        "AlignmentRestriction",
        "AlignmentRestrictions",
        "RaceRestriction",
        "RaceRestrictions",
        "Races",
        "PeopleType",
        "Abilities",
        "Options",
        "Talents",
        "Notes",
        "Metadata",
        "StrategyIds",
        "PrimaryStrategyIds",
        "MappedStrategyIds"
    };

    private static SpecialisationIndex? _cache;

    public static void InvalidateCache()
        => _cache = null;

    public static async Task<SpecialisationIndex> GetIndexAsync()
    {
        if (_cache != null)
            return _cache;

        var fragmented = await TryBuildFragmentedIndexAsync();
        if (fragmented != null)
        {
            _cache = fragmented;
            return _cache;
        }

        _cache = await BuildLegacyIndexAsync();
        return _cache;
    }

    private static async Task<SpecialisationIndex> BuildLegacyIndexAsync()
    {
        var json = await ServiceHelper.ReadPackageTextAsync(LegacySpecialisationPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return EmptyIndex();
        }

        var definitionsRoot = ResolveDefinitionsRoot(root);
        var references = BuildAbilityReferences(root, definitionsRoot);
        var injectionRules = ParseInjectionRules(root);
        var definitions = new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in definitionsRoot.EnumerateObject())
        {
            if (ReservedTopLevelKeys.Contains(entry.Name))
                continue;

            var normalized = NormalizeDefinition(entry.Name, entry.Value, references);
            definitions[entry.Name] = normalized;

            foreach (var grant in normalized.PassiveGrants)
                TryAddReference(references, grant.Ability);

            foreach (var set in normalized.ChoiceSets)
            {
                foreach (var option in set.Options)
                {
                    foreach (var grant in option.Grants)
                        TryAddReference(references, grant.Ability);
                }
            }
        }

        var choiceSetTemplates = new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions.Values)
        {
            foreach (var set in definition.ChoiceSets)
            {
                var id = (set.Id ?? string.Empty).Trim();
                if (id.Length == 0 || choiceSetTemplates.ContainsKey(id))
                    continue;

                choiceSetTemplates[id] = CloneChoiceSet(set, set.DefinitionKey);
            }
        }

        return new SpecialisationIndex
        {
            Definitions = new ReadOnlyDictionary<string, SpecialisationDefinition>(definitions),
            AbilityReferences = new ReadOnlyDictionary<string, AbilityDefinition>(references),
            ChoiceSetTemplates = new ReadOnlyDictionary<string, SpecialisationChoiceSet>(choiceSetTemplates),
            InjectionRules = injectionRules
        };
    }

    private static async Task<SpecialisationIndex?> TryBuildFragmentedIndexAsync()
    {
        var abilitiesJson = await TryReadPackageTextAsync(AbilitiesPath);
        var choiceSetsJson = await TryReadPackageTextAsync(ChoiceSetsPath);
        var classJson = await TryReadPackageTextAsync(ClassSpecialisationsPath);
        var raceJson = await TryReadPackageTextAsync(RaceSubtypesPath);
        var overridesJson = await TryReadPackageTextAsync(OverridesPath);

        var hasAnyFragment =
            abilitiesJson != null
            || choiceSetsJson != null
            || classJson != null
            || raceJson != null
            || overridesJson != null;

        if (!hasAnyFragment)
            return null;

        var references = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
        var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (abilitiesJson != null)
        {
            using var abilityDoc = JsonDocument.Parse(abilitiesJson);
            if (abilityDoc.RootElement.ValueKind == JsonValueKind.Object)
                ParseFragmentAbilityReferences(abilityDoc.RootElement, references, aliasMap);
        }

        var choiceSetTemplates = new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase);
        if (choiceSetsJson != null)
        {
            using var choiceDoc = JsonDocument.Parse(choiceSetsJson);
            if (choiceDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in ParseFragmentChoiceSetTemplates(choiceDoc.RootElement, references))
                    choiceSetTemplates[entry.Key] = entry.Value;
            }
        }

        var definitions = new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase);
        if (classJson != null)
        {
            using var classDoc = JsonDocument.Parse(classJson);
            if (classDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                ParseFragmentDefinitions(
                    classDoc.RootElement,
                    choiceSetTemplates,
                    references,
                    definitions);
            }
        }

        if (raceJson != null)
        {
            using var raceDoc = JsonDocument.Parse(raceJson);
            if (raceDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                ParseFragmentDefinitions(
                    raceDoc.RootElement,
                    choiceSetTemplates,
                    references,
                    definitions);
            }
        }

        foreach (var alias in aliasMap)
        {
            if (alias.Key.Trim().Length == 0 || alias.Value.Trim().Length == 0)
                continue;

            if (references.ContainsKey(alias.Key))
                continue;

            if (!references.TryGetValue(alias.Value, out var resolved))
                continue;

            references[alias.Key] = CloneAbility(resolved);
        }

        foreach (var definition in definitions.Values)
        {
            foreach (var grant in definition.PassiveGrants)
                TryAddReference(references, grant.Ability);

            foreach (var choiceSet in definition.ChoiceSets)
            {
                foreach (var option in choiceSet.Options)
                {
                    foreach (var grant in option.Grants)
                        TryAddReference(references, grant.Ability);
                }
            }
        }

        IReadOnlyList<SpecialisationInjectionRule> injectionRules = Array.Empty<SpecialisationInjectionRule>();
        if (overridesJson != null)
        {
            using var overridesDoc = JsonDocument.Parse(overridesJson);
            if (overridesDoc.RootElement.ValueKind == JsonValueKind.Object)
                injectionRules = ParseInjectionRules(overridesDoc.RootElement);
        }

        if (definitions.Count == 0
            && references.Count == 0
            && injectionRules.Count == 0)
        {
            return null;
        }

        return new SpecialisationIndex
        {
            Definitions = new ReadOnlyDictionary<string, SpecialisationDefinition>(definitions),
            AbilityReferences = new ReadOnlyDictionary<string, AbilityDefinition>(references),
            ChoiceSetTemplates = new ReadOnlyDictionary<string, SpecialisationChoiceSet>(
                choiceSetTemplates.ToDictionary(
                    kvp => kvp.Key,
                    kvp => CloneChoiceSet(kvp.Value, kvp.Value.DefinitionKey),
                    StringComparer.OrdinalIgnoreCase)),
            InjectionRules = injectionRules
        };
    }

    private static async Task<string?> TryReadPackageTextAsync(string relativePath)
    {
        try
        {
            return await ServiceHelper.ReadPackageTextAsync(relativePath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static void ParseFragmentAbilityReferences(
        JsonElement root,
        IDictionary<string, AbilityDefinition> references,
        IDictionary<string, string> aliases)
    {
        if (!TryGetAnyProperty(root, out var abilitiesElement, "abilities", "Abilities")
            || abilitiesElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var entry in abilitiesElement.EnumerateObject())
        {
            var parsed = ResolveAbilityDefinition(entry.Value, references);
            if (string.IsNullOrWhiteSpace(parsed.Name))
                parsed.Name = entry.Name;

            parsed.Key = ReadStringProperty(entry.Value, "Key", "key");
            if (string.IsNullOrWhiteSpace(parsed.Key))
                parsed.Key = entry.Name;

            references[entry.Name] = parsed;
            TryAddReference(references, parsed);
        }

        if (!TryGetAnyProperty(root, out var aliasesElement, "aliases", "Aliases")
            || aliasesElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var aliasEntry in aliasesElement.EnumerateObject())
        {
            if (aliasEntry.Value.ValueKind != JsonValueKind.String)
                continue;

            var aliasName = aliasEntry.Name.Trim();
            var targetRef = (aliasEntry.Value.GetString() ?? string.Empty).Trim();
            if (aliasName.Length == 0 || targetRef.Length == 0)
                continue;

            aliases[aliasName] = targetRef;
        }
    }

    private static IReadOnlyDictionary<string, SpecialisationChoiceSet> ParseFragmentChoiceSetTemplates(
        JsonElement root,
        IDictionary<string, AbilityDefinition> references)
    {
        if (!TryGetAnyProperty(root, out var setsElement, "choiceSets", "ChoiceSets")
            || setsElement.ValueKind != JsonValueKind.Object)
        {
            return new ReadOnlyDictionary<string, SpecialisationChoiceSet>(
                new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase));
        }

        var result = new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in setsElement.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
                continue;

            var id = ReadStringProperty(entry.Value, "Key", "Id", "key", "id");
            if (id.Length == 0)
                id = entry.Name;

            if (id.Length == 0)
                continue;

            var title = ReadStringProperty(entry.Value, "Title", "title");
            if (title.Length == 0)
                title = entry.Name;

            var mode = ParseChoiceMode(ReadStringProperty(entry.Value, "Mode", "mode"));
            var required = ReadNullableBooleanProperty(entry.Value, "Required", "required") ?? true;
            var levels = ReadIntArrayProperty(entry.Value, "Levels");
            var strategyIds = ParseStrategyIds(entry.Value);
            var metadata = ParseMetadata(ReadObjectProperty(entry.Value, "Metadata", "metadata"));

            var options = new List<ChoiceOption>();
            if (TryGetAnyProperty(entry.Value, out var optionsElement, "Options", "options")
                && optionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var optionElement in optionsElement.EnumerateArray())
                {
                    var option = ParseFragmentChoiceOption(optionElement, references);
                    if (option != null)
                        options.Add(option);
                }
            }

            result[id] = new SpecialisationChoiceSet
            {
                Id = id,
                DefinitionKey = ReadStringProperty(entry.Value, "DefinitionKey", "definitionKey"),
                Title = title,
                Required = required,
                Levels = levels,
                Mode = mode,
                Options = options,
                StrategyIds = strategyIds,
                Metadata = metadata
            };
        }

        return new ReadOnlyDictionary<string, SpecialisationChoiceSet>(result);
    }

    private static void ParseFragmentDefinitions(
        JsonElement root,
        IReadOnlyDictionary<string, SpecialisationChoiceSet> choiceSetTemplates,
        IDictionary<string, AbilityDefinition> references,
        IDictionary<string, SpecialisationDefinition> destination)
    {
        if (!TryGetAnyProperty(root, out var definitionsElement, "definitions", "Definitions")
            || definitionsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var entry in definitionsElement.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
                continue;

            destination[entry.Name] = ParseFragmentDefinition(entry.Name, entry.Value, choiceSetTemplates, references);
        }
    }

    private static SpecialisationDefinition ParseFragmentDefinition(
        string definitionName,
        JsonElement element,
        IReadOnlyDictionary<string, SpecialisationChoiceSet> choiceSetTemplates,
        IDictionary<string, AbilityDefinition> references)
    {
        var notes = ParseNotes(element);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["definitionKey"] = definitionName
        };

        var stableKey = ReadStringProperty(element, "Key", "key");
        if (stableKey.Length > 0)
            metadata["stableKey"] = stableKey;

        foreach (var kvp in ParseMetadata(ReadObjectProperty(element, "Metadata", "metadata")))
            metadata[kvp.Key] = kvp.Value;

        var choiceSets = new List<SpecialisationChoiceSet>();
        if (TryGetAnyProperty(element, out var refsElement, "ChoiceSetRefs", "choiceSetRefs")
            && refsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var refElement in refsElement.EnumerateArray())
            {
                if (refElement.ValueKind != JsonValueKind.String)
                    continue;

                var setRef = (refElement.GetString() ?? string.Empty).Trim();
                if (setRef.Length == 0)
                    continue;

                if (!choiceSetTemplates.TryGetValue(setRef, out var template))
                    continue;

                choiceSets.Add(CloneChoiceSet(template, definitionName));
            }
        }

        var passiveGrants = new List<AbilityGrant>();
        if (TryGetAnyProperty(element, out var grantsElement, "PassiveGrants", "passiveGrants")
            && grantsElement.ValueKind == JsonValueKind.Array)
        {
            passiveGrants.AddRange(ParseFragmentGrantArray(grantsElement, references));
        }
        else if (choiceSets.Count == 0
                 && TryGetAnyProperty(element, out var abilitiesElement, "Abilities", "abilities")
                 && abilitiesElement.ValueKind == JsonValueKind.Array)
        {
            passiveGrants.AddRange(ParseAbilityGrants(abilitiesElement, null, references));
        }

        return new SpecialisationDefinition
        {
            Key = stableKey.Length == 0 ? definitionName : stableKey,
            ChoiceSets = choiceSets,
            PassiveGrants = passiveGrants,
            Notes = notes,
            Metadata = new ReadOnlyDictionary<string, string>(metadata)
        };
    }

    private static SpecialisationChoiceSet CloneChoiceSet(SpecialisationChoiceSet source, string definitionName)
    {
        return new SpecialisationChoiceSet
        {
            Id = source.Id,
            DefinitionKey = definitionName,
            Title = source.Title,
            Required = source.Required,
            Levels = source.Levels.ToList(),
            Mode = source.Mode,
            Options = source.Options.Select(CloneChoiceOption).ToList(),
            StrategyIds = source.StrategyIds.ToList(),
            Metadata = new ReadOnlyDictionary<string, string>(
                source.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase))
        };
    }

    private static ChoiceOption CloneChoiceOption(ChoiceOption source)
    {
        return new ChoiceOption
        {
            Key = source.Key,
            Label = source.Label,
            Description = source.Description,
            Grants = source.Grants.Select(CloneAbilityGrant).ToList(),
            Customisation = source.Customisation == null
                ? null
                : new OptionCustomisation
                {
                    OptionEnum = source.Customisation.OptionEnum,
                    CustomValuesPermitted = source.Customisation.CustomValuesPermitted
                },
            Effects = new OptionEffects
            {
                LifeScaleOverride = source.Effects.LifeScaleOverride,
                ArmourAvailabilityOverride = source.Effects.ArmourAvailabilityOverride,
                ColourChoiceOverride = source.Effects.ColourChoiceOverride.ToList(),
                GuildOverrides = source.Effects.GuildOverrides,
                HedgeOrCircle = source.Effects.HedgeOrCircle.ToList(),
                Notes = source.Effects.Notes.ToList()
            },
            Restrictions = new Restrictions
            {
                ClassRestriction = source.Restrictions.ClassRestriction.ToList(),
                AlignmentRestriction = source.Restrictions.AlignmentRestriction.ToList(),
                RaceRestriction = source.Restrictions.RaceRestriction.ToList(),
                PeopleType = source.Restrictions.PeopleType.ToList()
            },
            StrategyIds = source.StrategyIds.ToList(),
            Metadata = new ReadOnlyDictionary<string, string>(
                source.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase))
        };
    }

    private static AbilityGrant CloneAbilityGrant(AbilityGrant source)
    {
        return new AbilityGrant
        {
            Level = source.Level,
            Ability = CloneAbility(source.Ability),
            StrategyIds = source.StrategyIds.ToList(),
            Metadata = new ReadOnlyDictionary<string, string>(
                source.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase))
        };
    }

    private static ChoiceOption? ParseFragmentChoiceOption(
        JsonElement optionElement,
        IDictionary<string, AbilityDefinition> references)
    {
        if (optionElement.ValueKind != JsonValueKind.Object)
            return null;

        var key = ReadStringProperty(optionElement, "Key", "key");
        var label = ReadStringProperty(optionElement, "Label", "label", "Name", "name");
        if (label.Length == 0)
            label = key;

        if (key.Length == 0)
            key = label;

        if (key.Length == 0)
            return null;

        var description = ReadStringProperty(optionElement, "Description", "description");
        var grants = new List<AbilityGrant>();
        if (TryGetAnyProperty(optionElement, out var grantsElement, "Grants", "grants")
            && grantsElement.ValueKind == JsonValueKind.Array)
        {
            grants.AddRange(ParseFragmentGrantArray(grantsElement, references));
        }
        else if (TryGetAnyProperty(optionElement, out var abilitiesElement, "Abilities", "abilities")
                 && abilitiesElement.ValueKind == JsonValueKind.Array)
        {
            grants.AddRange(ParseAbilityGrants(abilitiesElement, null, references));
        }

        OptionCustomisation? customisation = null;
        if (TryGetAnyProperty(optionElement, out var customisationElement, "Customisation", "Customization")
            && customisationElement.ValueKind == JsonValueKind.Object)
        {
            customisation = JsonSerializer.Deserialize<OptionCustomisation>(customisationElement.GetRawText(), JsonOptions);
        }

        var effects = ParseFragmentOptionEffects(optionElement);
        var restrictions = ParseFragmentRestrictions(optionElement);
        var metadata = ParseMetadata(ReadObjectProperty(optionElement, "Metadata", "metadata"));
        var strategyIds = ParseStrategyIds(optionElement);

        return new ChoiceOption
        {
            Key = key,
            Label = label.Length == 0 ? key : label,
            Description = description,
            Grants = grants,
            Customisation = customisation,
            Effects = effects,
            Restrictions = restrictions,
            StrategyIds = strategyIds,
            Metadata = metadata
        };
    }

    private static OptionEffects ParseFragmentOptionEffects(JsonElement optionElement)
    {
        var source = optionElement;
        if (TryGetAnyProperty(optionElement, out var effectsElement, "Effects", "effects")
            && effectsElement.ValueKind == JsonValueKind.Object)
        {
            source = effectsElement;
        }

        var lifeScaleOverride = ReadStringProperty(source, "LifeScaleOverride");
        var armourOverride = ReadStringProperty(source, "ArmourAvailabilityOverride");
        var colourChoiceOverride = ReadStringArrayProperty(source, "ColourChoiceOverride");
        var hedgeOrCircle = ReadStringArrayProperty(source, "HedgeOrCircle");
        var notes = ReadStringArrayProperty(source, "Notes");

        GuildOverrideRules? guildOverrides = null;
        if (TryGetAnyProperty(source, out var guildElement, "GuildOverrides"))
            guildOverrides = GuildOverrideRulesConverter.FromElement(guildElement, JsonOptions);

        return new OptionEffects
        {
            LifeScaleOverride = lifeScaleOverride,
            ArmourAvailabilityOverride = armourOverride,
            ColourChoiceOverride = colourChoiceOverride,
            GuildOverrides = guildOverrides,
            HedgeOrCircle = hedgeOrCircle,
            Notes = notes
        };
    }

    private static Restrictions ParseFragmentRestrictions(JsonElement optionElement)
    {
        var source = optionElement;
        if (TryGetAnyProperty(optionElement, out var restrictionsElement, "Restrictions", "restrictions")
            && restrictionsElement.ValueKind == JsonValueKind.Object)
        {
            source = restrictionsElement;
        }

        var classRestriction = ReadStringArrayProperty(source, "ClassRestriction");
        var alignmentRestriction = ReadStringArrayProperty(source, "AlignmentRestriction");
        if (alignmentRestriction.Count == 0)
            alignmentRestriction = ReadStringArrayProperty(source, "AlignmentRestrictions");

        var raceRestriction = ReadStringArrayProperty(source, "RaceRestriction");
        if (raceRestriction.Count == 0)
            raceRestriction = ReadStringArrayProperty(source, "RaceRestrictions");
        if (raceRestriction.Count == 0)
            raceRestriction = ReadStringArrayProperty(source, "Races");

        var peopleType = ReadStringArrayProperty(source, "PeopleType");
        return new Restrictions
        {
            ClassRestriction = classRestriction,
            AlignmentRestriction = alignmentRestriction,
            RaceRestriction = raceRestriction,
            PeopleType = peopleType
        };
    }

    private static IReadOnlyList<AbilityGrant> ParseFragmentGrantArray(
        JsonElement grantsElement,
        IDictionary<string, AbilityDefinition> references)
    {
        var grants = new List<AbilityGrant>();
        if (grantsElement.ValueKind != JsonValueKind.Array)
            return grants;

        foreach (var grantElement in grantsElement.EnumerateArray())
        {
            var parsed = ParseFragmentGrant(grantElement, references);
            if (parsed != null)
                grants.Add(parsed);
        }

        return grants;
    }

    private static AbilityGrant? ParseFragmentGrant(
        JsonElement grantElement,
        IDictionary<string, AbilityDefinition> references)
    {
        if (grantElement.ValueKind == JsonValueKind.String)
        {
            var resolvedAbility = ResolveAbilityByReference(grantElement.GetString() ?? string.Empty, references);
            if (string.IsNullOrWhiteSpace(resolvedAbility.Name))
                return null;

            return new AbilityGrant
            {
                Ability = resolvedAbility
            };
        }

        if (grantElement.ValueKind != JsonValueKind.Object)
            return null;

        var level = ReadNullableIntProperty(grantElement, "Level", "level");
        var abilityRef = ReadStringProperty(grantElement, "AbilityRef", "abilityRef", "$ref");

        AbilityDefinition ability;
        if (abilityRef.Length > 0)
        {
            ability = ResolveAbilityByReference(abilityRef, references);

            if (TryGetAnyProperty(grantElement, out var overrideElement, "Overrides", "overrides")
                && overrideElement.ValueKind == JsonValueKind.Object)
            {
                var overrides = ParseAbilityWithoutReference(overrideElement);
                ability = MergeAbilityOverride(ability, overrides);
            }
        }
        else if (TryGetAnyProperty(grantElement, out var abilityElement, "Ability", "ability"))
        {
            ability = ResolveAbilityDefinition(abilityElement, references);
        }
        else
        {
            ability = ResolveAbilityDefinition(grantElement, references);
        }

        if (string.IsNullOrWhiteSpace(ability.Name))
            return null;

        return new AbilityGrant
        {
            Level = level,
            Ability = ability,
            StrategyIds = ParseStrategyIds(grantElement),
            Metadata = ParseMetadata(ReadObjectProperty(grantElement, "Metadata", "metadata"))
        };
    }

    private static AbilityDefinition ResolveAbilityByReference(
        string abilityRef,
        IDictionary<string, AbilityDefinition> references)
    {
        var refKey = (abilityRef ?? string.Empty).Trim();
        if (refKey.Length == 0)
            return new AbilityDefinition();

        if (references.TryGetValue(refKey, out var existing))
        {
            var clone = CloneAbility(existing);
            if (string.IsNullOrWhiteSpace(clone.Key))
                clone.Key = refKey;
            if (string.IsNullOrWhiteSpace(clone.Name))
                clone.Name = refKey;
            return clone;
        }

        return new AbilityDefinition
        {
            Key = refKey,
            Name = refKey
        };
    }

    private static ChoiceMode ParseChoiceMode(string modeText)
    {
        var normalized = (modeText ?? string.Empty).Trim();
        if (normalized.Length == 0)
            return ChoiceMode.Single;

        if (Enum.TryParse<ChoiceMode>(normalized, ignoreCase: true, out var parsed))
            return parsed;

        return normalized switch
        {
            "Mapped" => ChoiceMode.MappedSingle,
            "MappedSingle" => ChoiceMode.MappedSingle,
            _ => ChoiceMode.Single
        };
    }

    private static SpecialisationIndex EmptyIndex()
    {
        return new SpecialisationIndex
        {
            Definitions = new ReadOnlyDictionary<string, SpecialisationDefinition>(
                new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase)),
            AbilityReferences = new ReadOnlyDictionary<string, AbilityDefinition>(
                new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase)),
            ChoiceSetTemplates = new ReadOnlyDictionary<string, SpecialisationChoiceSet>(
                new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase)),
            InjectionRules = Array.Empty<SpecialisationInjectionRule>()
        };
    }

    private static JsonElement ResolveDefinitionsRoot(JsonElement root)
    {
        if (root.TryGetProperty("definitions", out var definitionsElement)
            && definitionsElement.ValueKind == JsonValueKind.Object)
        {
            return definitionsElement;
        }

        if (root.TryGetProperty("Definitions", out definitionsElement)
            && definitionsElement.ValueKind == JsonValueKind.Object)
        {
            return definitionsElement;
        }

        return root;
    }

    private static Dictionary<string, AbilityDefinition> BuildAbilityReferences(JsonElement root, JsonElement definitionsRoot)
    {
        var references = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var propertyName in new[] { "AbilityRefs", "AbilityReferences", "$refs", "$abilityRefs", "$definitions" })
        {
            if (!root.TryGetProperty(propertyName, out var refsElement)
                || refsElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var entry in refsElement.EnumerateObject())
            {
                var parsed = ResolveAbilityDefinition(entry.Value, references);
                if (string.IsNullOrWhiteSpace(parsed.Name))
                    parsed.Name = entry.Name;
                if (string.IsNullOrWhiteSpace(parsed.Key))
                    parsed.Key = entry.Name;

                references[entry.Name] = parsed;
                TryAddReference(references, parsed);
            }
        }

        foreach (var entry in definitionsRoot.EnumerateObject())
        {
            CollectDirectAbilityReferences(entry.Value, references);
        }

        return references;
    }

    private static void CollectDirectAbilityReferences(JsonElement element, Dictionary<string, AbilityDefinition> references)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectDirectAbilityReferences(item, references);
                return;

            case JsonValueKind.Object:
                if (element.TryGetProperty("$ref", out _))
                    return;

                if (element.TryGetProperty("Name", out var nameElement)
                    && nameElement.ValueKind == JsonValueKind.String)
                {
                    var parsed = ParseAbilityWithoutReference(element);
                    TryAddReference(references, parsed);
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                        CollectDirectAbilityReferences(property.Value, references);
                }

                return;
        }
    }

    private static SpecialisationDefinition NormalizeDefinition(
        string key,
        JsonElement element,
        IDictionary<string, AbilityDefinition> references)
    {
        var choiceSets = new List<SpecialisationChoiceSet>();
        var passiveGrants = new List<AbilityGrant>();
        var notes = ParseNotes(element);
        var definitionStrategyIds = ParseStrategyIds(element);

        if (element.ValueKind == JsonValueKind.Object)
        {
            var primaryChoiceSet = BuildPrimaryChoiceSet(key, element, references, definitionStrategyIds);
            if (primaryChoiceSet != null)
                choiceSets.Add(primaryChoiceSet);

            var mappedChoiceSet = BuildMappedChoiceSet(key, element, references, definitionStrategyIds);
            if (mappedChoiceSet != null)
                choiceSets.Add(mappedChoiceSet);

            if (choiceSets.Count == 0)
                passiveGrants.AddRange(ParsePassiveGrants(element, references));
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["definitionKey"] = key
        };

        return new SpecialisationDefinition
        {
            Key = key,
            ChoiceSets = choiceSets,
            PassiveGrants = passiveGrants,
            Notes = notes,
            Metadata = new ReadOnlyDictionary<string, string>(metadata)
        };
    }

    private static SpecialisationChoiceSet? BuildPrimaryChoiceSet(
        string definitionKey,
        JsonElement element,
        IDictionary<string, AbilityDefinition> references,
        IReadOnlyList<string> definitionStrategyIds)
    {
        var sourceArray = default(JsonElement);
        if (element.TryGetProperty("Abilities", out var abilitiesElement) && abilitiesElement.ValueKind == JsonValueKind.Array)
            sourceArray = abilitiesElement;
        else if (element.TryGetProperty("Options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array)
            sourceArray = optionsElement;

        if (sourceArray.ValueKind != JsonValueKind.Array)
            return null;

        var options = new List<ChoiceOption>();
        foreach (var abilityElement in sourceArray.EnumerateArray())
        {
            var parsed = ResolveAbilityDefinition(abilityElement, references);
            var name = (parsed.Name ?? string.Empty).Trim();
            if (name.Length == 0)
                continue;

            var customisation = parsed.Customisation == null
                ? null
                : new OptionCustomisation
                {
                    OptionEnum = parsed.Customisation.OptionEnum ?? string.Empty,
                    CustomValuesPermitted = parsed.Customisation.CustomValuesPermitted
                };

            options.Add(new ChoiceOption
            {
                Key = name,
                Label = name,
                Description = parsed.Effect ?? string.Empty,
                Grants =
                [
                    new AbilityGrant
                    {
                        Ability = parsed
                    }
                ],
                Customisation = customisation
            });
        }

        if (options.Count == 0)
            return null;

        var explicitSetStrategyIds = ParseStrategyIds(element, "PrimaryStrategyIds");
        var strategyIds = MergeStrategyIds(
            explicitSetStrategyIds,
            definitionStrategyIds,
            ResolveFallbackStrategies(definitionKey, mapped: false));

        return new SpecialisationChoiceSet
        {
            Id = $"{definitionKey}:primary",
            DefinitionKey = definitionKey,
            Title = definitionKey,
            Required = true,
            Mode = ChoiceMode.Single,
            Options = options,
            StrategyIds = strategyIds
        };
    }

    private static SpecialisationChoiceSet? BuildMappedChoiceSet(
        string definitionKey,
        JsonElement element,
        IDictionary<string, AbilityDefinition> references,
        IReadOnlyList<string> definitionStrategyIds)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        var options = new List<ChoiceOption>();

        foreach (var property in element.EnumerateObject())
        {
            if (KnownChoiceMetadataFields.Contains(property.Name))
                continue;

            var parsedOption = ParseMappedOption(property.Name, property.Value, references);
            if (parsedOption == null)
                continue;

            options.Add(parsedOption);
        }

        if (options.Count == 0)
            return null;

        var explicitSetStrategyIds = ParseStrategyIds(element, "MappedStrategyIds");
        var strategyIds = MergeStrategyIds(
            explicitSetStrategyIds,
            definitionStrategyIds,
            ResolveFallbackStrategies(definitionKey, mapped: true));

        return new SpecialisationChoiceSet
        {
            Id = $"{definitionKey}:mapped",
            DefinitionKey = definitionKey,
            Title = definitionKey,
            Required = true,
            Mode = ChoiceMode.MappedSingle,
            Options = options,
            StrategyIds = strategyIds
        };
    }

    private static ChoiceOption? ParseMappedOption(
        string optionKey,
        JsonElement element,
        IDictionary<string, AbilityDefinition> references)
    {
        var grants = new List<AbilityGrant>();
        var description = string.Empty;
        var lifeScaleOverride = string.Empty;
        var armourOverride = string.Empty;
        var colourChoiceOverride = new List<string>();
        GuildOverrideRules? guildOverrides = null;
        var hedgeOrCircle = new List<string>();
        var classRestriction = new List<string>();
        var alignmentRestriction = new List<string>();
        var raceRestriction = new List<string>();
        var peopleType = new List<string>();
        IReadOnlyList<string> optionStrategyIds = Array.Empty<string>();

        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                grants.AddRange(ParseAbilityGrants(element, 1, references));
                break;

            case JsonValueKind.Object:
            {
                optionStrategyIds = ParseStrategyIds(element);

                if (element.TryGetProperty("Description", out var descElement)
                    && descElement.ValueKind == JsonValueKind.String)
                {
                    description = descElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("LifeScaleOverride", out var lifeScaleElement)
                    && lifeScaleElement.ValueKind == JsonValueKind.String)
                {
                    lifeScaleOverride = lifeScaleElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("ArmourAvailabilityOverride", out var armourElement)
                    && armourElement.ValueKind == JsonValueKind.String)
                {
                    armourOverride = armourElement.GetString() ?? string.Empty;
                }

                if (element.TryGetProperty("ColourChoiceOverride", out var colourElement)
                    && colourElement.ValueKind == JsonValueKind.Array)
                {
                    colourChoiceOverride = ParseStringArray(colourElement);
                }

                if (element.TryGetProperty("GuildOverrides", out var guildElement))
                    guildOverrides = GuildOverrideRulesConverter.FromElement(guildElement, JsonOptions);

                if (element.TryGetProperty("HedgeOrCircle", out var hedgeElement)
                    && hedgeElement.ValueKind == JsonValueKind.Array)
                {
                    hedgeOrCircle = ParseStringArray(hedgeElement);
                }

                if (element.TryGetProperty("ClassRestriction", out var classRestrictionElement)
                    && classRestrictionElement.ValueKind == JsonValueKind.Array)
                {
                    classRestriction = ParseStringArray(classRestrictionElement);
                }

                if (element.TryGetProperty("AlignmentRestriction", out var alignmentRestrictionElement)
                    && alignmentRestrictionElement.ValueKind == JsonValueKind.Array)
                {
                    alignmentRestriction = ParseStringArray(alignmentRestrictionElement);
                }
                else if (element.TryGetProperty("AlignmentRestrictions", out var alignmentRestrictionsElement)
                         && alignmentRestrictionsElement.ValueKind == JsonValueKind.Array)
                {
                    alignmentRestriction = ParseStringArray(alignmentRestrictionsElement);
                }

                if (element.TryGetProperty("PeopleType", out var peopleTypeElement)
                    && peopleTypeElement.ValueKind == JsonValueKind.Array)
                {
                    peopleType = ParseStringArray(peopleTypeElement);
                }

                if (element.TryGetProperty("RaceRestriction", out var raceRestrictionElement)
                    && raceRestrictionElement.ValueKind == JsonValueKind.Array)
                {
                    raceRestriction = ParseStringArray(raceRestrictionElement);
                }
                else if (element.TryGetProperty("RaceRestrictions", out var raceRestrictionsElement)
                         && raceRestrictionsElement.ValueKind == JsonValueKind.Array)
                {
                    raceRestriction = ParseStringArray(raceRestrictionsElement);
                }
                else if (element.TryGetProperty("Races", out var racesElement)
                         && racesElement.ValueKind == JsonValueKind.Array)
                {
                    raceRestriction = ParseStringArray(racesElement);
                }

                if (element.TryGetProperty("Levels", out var levelsElement)
                    && levelsElement.ValueKind == JsonValueKind.Object)
                {
                    grants.AddRange(ParseLevelGrants(levelsElement, references));
                }
                else
                {
                    grants.AddRange(ParseLevelGrants(element, references));
                }

                grants.AddRange(ParseLegacyGrants(element, references));
                break;
            }
        }

        if (grants.Count == 0 && string.IsNullOrWhiteSpace(description)
            && lifeScaleOverride.Length == 0
            && armourOverride.Length == 0
            && colourChoiceOverride.Count == 0
            && hedgeOrCircle.Count == 0
            && classRestriction.Count == 0
            && alignmentRestriction.Count == 0
            && raceRestriction.Count == 0
            && peopleType.Count == 0
            && guildOverrides == null)
        {
            return null;
        }

        return new ChoiceOption
        {
            Key = optionKey,
            Label = optionKey,
            Description = description,
            Grants = grants,
            Effects = new OptionEffects
            {
                LifeScaleOverride = lifeScaleOverride,
                ArmourAvailabilityOverride = armourOverride,
                ColourChoiceOverride = colourChoiceOverride,
                GuildOverrides = guildOverrides,
                HedgeOrCircle = hedgeOrCircle
            },
            Restrictions = new Restrictions
            {
                ClassRestriction = classRestriction,
                AlignmentRestriction = alignmentRestriction,
                RaceRestriction = raceRestriction,
                PeopleType = peopleType
            },
            StrategyIds = optionStrategyIds
        };
    }

    private static IReadOnlyList<AbilityGrant> ParsePassiveGrants(JsonElement element, IDictionary<string, AbilityDefinition> references)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return Array.Empty<AbilityGrant>();

        if (!element.TryGetProperty("Abilities", out var abilitiesElement)
            || abilitiesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AbilityGrant>();
        }

        return ParseAbilityGrants(abilitiesElement, null, references);
    }

    private static List<AbilityGrant> ParseLevelGrants(JsonElement levelsElement, IDictionary<string, AbilityDefinition> references)
    {
        var grants = new List<AbilityGrant>();
        if (levelsElement.ValueKind != JsonValueKind.Object)
            return grants;

        foreach (var levelProperty in levelsElement.EnumerateObject())
        {
            if (!int.TryParse(levelProperty.Name, out var parsedLevel)
                || levelProperty.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            grants.AddRange(ParseAbilityGrants(levelProperty.Value, parsedLevel, references));
        }

        return grants;
    }

    private static List<AbilityGrant> ParseLegacyGrants(JsonElement element, IDictionary<string, AbilityDefinition> references)
    {
        var grants = new List<AbilityGrant>();
        if (element.ValueKind != JsonValueKind.Object)
            return grants;

        if (element.TryGetProperty("Abilities", out var abilitiesElement)
            && abilitiesElement.ValueKind == JsonValueKind.Array)
        {
            grants.AddRange(ParseAbilityGrants(abilitiesElement, 1, references));
        }

        if (element.TryGetProperty("Talents", out var talentsElement)
            && talentsElement.ValueKind == JsonValueKind.Object)
        {
            if (talentsElement.TryGetProperty("Minor", out var minorElement)
                && minorElement.ValueKind == JsonValueKind.Array)
            {
                grants.AddRange(ParseAbilityGrants(minorElement, 2, references));
            }

            if (talentsElement.TryGetProperty("Medium", out var mediumElement)
                && mediumElement.ValueKind == JsonValueKind.Array)
            {
                grants.AddRange(ParseAbilityGrants(mediumElement, 5, references));
            }

            if (talentsElement.TryGetProperty("Major", out var majorElement)
                && majorElement.ValueKind == JsonValueKind.Array)
            {
                grants.AddRange(ParseAbilityGrants(majorElement, 8, references));
            }
        }

        return grants;
    }

    private static List<AbilityGrant> ParseAbilityGrants(
        JsonElement abilityArray,
        int? level,
        IDictionary<string, AbilityDefinition> references)
    {
        var grants = new List<AbilityGrant>();
        if (abilityArray.ValueKind != JsonValueKind.Array)
            return grants;

        foreach (var abilityElement in abilityArray.EnumerateArray())
        {
            var parsed = ResolveAbilityDefinition(abilityElement, references);
            if (string.IsNullOrWhiteSpace(parsed.Name))
                continue;

            grants.Add(new AbilityGrant
            {
                Level = level,
                Ability = parsed
            });
        }

        return grants;
    }

    private static AbilityDefinition ResolveAbilityDefinition(
        JsonElement element,
        IDictionary<string, AbilityDefinition> references)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("$ref", out var refElement)
            && refElement.ValueKind == JsonValueKind.String)
        {
            var refKey = (refElement.GetString() ?? string.Empty).Trim();
            var resolved = references.TryGetValue(refKey, out var baseValue)
                ? CloneAbility(baseValue)
                : new AbilityDefinition { Key = refKey, Name = refKey };

            var overrideValue = ParseAbilityWithoutReference(element);
            return MergeAbilityOverride(resolved, overrideValue);
        }

        return ParseAbilityWithoutReference(element);
    }

    private static AbilityDefinition ParseAbilityWithoutReference(JsonElement element)
    {
        try
        {
            return JsonSerializer.Deserialize<AbilityDefinition>(element.GetRawText(), JsonOptions)
                   ?? new AbilityDefinition();
        }
        catch
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => new AbilityDefinition { Name = element.GetString() ?? string.Empty },
                _ => new AbilityDefinition { Name = element.GetRawText() }
            };
        }
    }

    private static AbilityDefinition MergeAbilityOverride(AbilityDefinition baseline, AbilityDefinition overrides)
    {
        if (overrides == null)
            return baseline;

        return new AbilityDefinition
        {
            Key = string.IsNullOrWhiteSpace(overrides.Key) ? baseline.Key : overrides.Key,
            AbilityRef = string.IsNullOrWhiteSpace(overrides.AbilityRef) ? baseline.AbilityRef : overrides.AbilityRef,
            GrantId = string.IsNullOrWhiteSpace(overrides.GrantId) ? baseline.GrantId : overrides.GrantId,
            GrantType = string.IsNullOrWhiteSpace(overrides.GrantType) ? baseline.GrantType : overrides.GrantType,
            Duration = string.IsNullOrWhiteSpace(overrides.Duration) ? baseline.Duration : overrides.Duration,
            Overrides = overrides.Overrides ?? baseline.Overrides,
            UpgradeGrantRef = string.IsNullOrWhiteSpace(overrides.UpgradeGrantRef)
                ? baseline.UpgradeGrantRef
                : overrides.UpgradeGrantRef,
            ReplaceWith = overrides.ReplaceWith ?? baseline.ReplaceWith,
            Modify = overrides.Modify ?? baseline.Modify,
            Name = string.IsNullOrWhiteSpace(overrides.Name) ? baseline.Name : overrides.Name,
            Type = string.IsNullOrWhiteSpace(overrides.Type) ? baseline.Type : overrides.Type,
            Effect = string.IsNullOrWhiteSpace(overrides.Effect) ? baseline.Effect : overrides.Effect,
            Lore = string.IsNullOrWhiteSpace(overrides.Lore) ? baseline.Lore : overrides.Lore,
            BattleboardNameOverride = string.IsNullOrWhiteSpace(overrides.BattleboardNameOverride)
                ? baseline.BattleboardNameOverride
                : overrides.BattleboardNameOverride,
            UpdateKey = string.IsNullOrWhiteSpace(overrides.UpdateKey) ? baseline.UpdateKey : overrides.UpdateKey,
            Source = string.IsNullOrWhiteSpace(overrides.Source) ? baseline.Source : overrides.Source,
            Count = overrides.Count ?? baseline.Count,
            Amount = overrides.Amount is { Count: > 0 } ? overrides.Amount.ToList() : baseline.Amount?.ToList(),
            Frequency = string.IsNullOrWhiteSpace(overrides.Frequency) ? baseline.Frequency : overrides.Frequency,
            OverwriteKey = string.IsNullOrWhiteSpace(overrides.OverwriteKey) ? baseline.OverwriteKey : overrides.OverwriteKey,
            PreReqs = overrides.PreReqs is { Count: > 0 } ? overrides.PreReqs.ToList() : baseline.PreReqs?.ToList(),
            GuildOverrides = overrides.GuildOverrides is { Count: > 0 }
                ? overrides.GuildOverrides.ToList()
                : baseline.GuildOverrides?.ToList(),
            ChoiceSetRef = string.IsNullOrWhiteSpace(overrides.ChoiceSetRef) ? baseline.ChoiceSetRef : overrides.ChoiceSetRef,
            ChoiceSetRefs = overrides.ChoiceSetRefs is { Count: > 0 }
                ? overrides.ChoiceSetRefs.ToList()
                : baseline.ChoiceSetRefs?.ToList(),
            Customisation = overrides.Customisation ?? baseline.Customisation,
            SystemEffects = overrides.SystemEffects is { Count: > 0 }
                ? overrides.SystemEffects.Select(CloneSystemEffect).ToList()
                : baseline.SystemEffects?.Select(CloneSystemEffect).ToList()
        };
    }

    private static AbilityDefinition CloneAbility(AbilityDefinition source)
    {
        return new AbilityDefinition
        {
            Key = source.Key,
            AbilityRef = source.AbilityRef,
            GrantId = source.GrantId,
            GrantType = source.GrantType,
            Duration = source.Duration,
            Overrides = source.Overrides == null
                ? null
                : new GuildGrantOverrides
                {
                    DisplayName = source.Overrides.DisplayName,
                    Verbal = source.Overrides.Verbal,
                    Effect = source.Overrides.Effect,
                    Source = source.Overrides.Source,
                    GrantType = source.Overrides.GrantType,
                    Count = source.Overrides.Count,
                    Frequency = source.Overrides.Frequency,
                    Duration = source.Overrides.Duration
                },
            UpgradeGrantRef = source.UpgradeGrantRef,
            ReplaceWith = source.ReplaceWith == null ? null : CloneAbility(source.ReplaceWith),
            Modify = source.Modify == null ? null : new GuildGrantModify { CountDelta = source.Modify.CountDelta },
            Name = source.Name,
            Type = source.Type,
            Effect = source.Effect,
            Lore = source.Lore,
            BattleboardNameOverride = source.BattleboardNameOverride,
            UpdateKey = source.UpdateKey,
            Source = source.Source,
            Count = source.Count,
            Amount = source.Amount?.ToList(),
            Frequency = source.Frequency,
            OverwriteKey = source.OverwriteKey,
            PreReqs = source.PreReqs?.ToList(),
            GuildOverrides = source.GuildOverrides?.ToList(),
            ChoiceSetRef = source.ChoiceSetRef,
            ChoiceSetRefs = source.ChoiceSetRefs?.ToList(),
            Customisation = source.Customisation == null
                ? null
                : new AbilityCustomisation
                {
                    OptionEnum = source.Customisation.OptionEnum,
                    CustomValuesPermitted = source.Customisation.CustomValuesPermitted
                },
            SystemEffects = source.SystemEffects?.Select(CloneSystemEffect).ToList()
        };
    }

    private static AbilitySystemEffect CloneSystemEffect(AbilitySystemEffect source)
    {
        return new AbilitySystemEffect
        {
            EffectType = source.EffectType,
            DisplayName = source.DisplayName,
            ResistanceType = source.ResistanceType,
            Level = source.Level,
            ImmunityName = source.ImmunityName
        };
    }

    private static void TryAddReference(IDictionary<string, AbilityDefinition> references, AbilityDefinition? ability)
    {
        var key = (ability?.Key ?? string.Empty).Trim();
        if (key.Length > 0 && !references.ContainsKey(key))
            references[key] = CloneAbility(ability!);

        var name = (ability?.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            return;

        if (!references.ContainsKey(name))
            references[name] = CloneAbility(ability!);
    }

    private static IReadOnlyList<SpecialisationInjectionRule> ParseInjectionRules(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return Array.Empty<SpecialisationInjectionRule>();

        if (!TryGetAnyProperty(root, out var rulesElement, "injectionRules", "InjectionRules")
            || rulesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SpecialisationInjectionRule>();
        }

        var rules = new List<SpecialisationInjectionRule>();
        foreach (var ruleElement in rulesElement.EnumerateArray())
        {
            if (ruleElement.ValueKind != JsonValueKind.Object)
                continue;

            var id = ReadStringProperty(ruleElement, "Id");
            var conditions = ParseInjectionRuleConditions(ruleElement);
            var section = ParseInjectionRuleSection(ruleElement);
            if (string.IsNullOrWhiteSpace(section.DefinitionKey))
                continue;

            rules.Add(new SpecialisationInjectionRule
            {
                Id = id.Length == 0 ? section.DefinitionKey : id,
                Conditions = conditions,
                Section = section,
                StrategyIds = ParseStrategyIds(ruleElement)
            });
        }

        return rules;
    }

    private static InjectionRuleConditions ParseInjectionRuleConditions(JsonElement ruleElement)
    {
        if (!TryGetAnyProperty(ruleElement, out var conditionsElement, "Conditions")
            || conditionsElement.ValueKind != JsonValueKind.Object)
        {
            return new InjectionRuleConditions();
        }

        return new InjectionRuleConditions
        {
            Race = ReadStringProperty(conditionsElement, "Race"),
            Subtype = ReadStringProperty(conditionsElement, "Subtype"),
            Class = ReadStringProperty(conditionsElement, "Class"),
            ClassIn = ReadStringArrayProperty(conditionsElement, "ClassIn"),
            ClassNotIn = ReadStringArrayProperty(conditionsElement, "ClassNotIn"),
            PeopleTypeIn = ReadStringArrayProperty(conditionsElement, "PeopleTypeIn")
        };
    }

    private static InjectionRuleSection ParseInjectionRuleSection(JsonElement ruleElement)
    {
        JsonElement sectionElement;
        if (TryGetAnyProperty(ruleElement, out var nestedSection, "Section")
            && nestedSection.ValueKind == JsonValueKind.Object)
        {
            sectionElement = nestedSection;
        }
        else
        {
            sectionElement = ruleElement;
        }

        var metadata = ParseMetadata(ReadObjectProperty(sectionElement, "Metadata"));
        var sectionType = ReadStringProperty(sectionElement, "SectionType");
        if (sectionType.Length == 0)
            sectionType = "Mapped";

        return new InjectionRuleSection
        {
            SectionType = sectionType,
            DefinitionKey = ReadStringProperty(sectionElement, "DefinitionKey"),
            Title = ReadStringProperty(sectionElement, "Title"),
            Subtitle = ReadStringProperty(sectionElement, "Subtitle"),
            DetailKey = ReadStringProperty(sectionElement, "DetailKey"),
            SectionId = ReadStringProperty(sectionElement, "SectionId"),
            Required = ReadNullableBooleanProperty(sectionElement, "Required"),
            RequiredWhenClassHasPowerBase = ReadBooleanProperty(sectionElement, "RequiredWhenClassHasPowerBase"),
            OptionalClassIn = ReadStringArrayProperty(sectionElement, "OptionalClassIn"),
            Levels = ReadIntArrayProperty(sectionElement, "Levels"),
            InsertIndex = ReadNullableIntProperty(sectionElement, "InsertIndex"),
            OptionFilter = ParseInjectionOptionFilter(ReadObjectProperty(sectionElement, "OptionFilter")),
            Metadata = metadata
        };
    }

    private static InjectionOptionFilter? ParseInjectionOptionFilter(JsonElement? optionFilterElement)
    {
        if (optionFilterElement is not JsonElement filter || filter.ValueKind != JsonValueKind.Object)
            return null;

        var sourceDefinitionKey = ReadStringProperty(filter, "SourceDefinitionKey");
        var sourceOption = ReadStringProperty(filter, "SourceOption");
        var effectListField = ReadStringProperty(filter, "EffectListField");

        if (sourceDefinitionKey.Length == 0 || sourceOption.Length == 0 || effectListField.Length == 0)
            return null;

        return new InjectionOptionFilter
        {
            SourceDefinitionKey = sourceDefinitionKey,
            SourceOption = sourceOption,
            EffectListField = effectListField
        };
    }

    private static IReadOnlyDictionary<string, string> ParseMetadata(JsonElement? metadataElement)
    {
        if (metadataElement is not JsonElement metadata || metadata.ValueKind != JsonValueKind.Object)
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in metadata.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = (property.Value.GetString() ?? string.Empty).Trim();
                if (value.Length > 0)
                    dict[property.Name] = value;
            }
            else if (property.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                dict[property.Name] = property.Value.GetRawText();
            }
        }

        return new ReadOnlyDictionary<string, string>(dict);
    }

    private static JsonElement? ReadObjectProperty(JsonElement element, params string[] propertyNames)
    {
        return TryGetAnyProperty(element, out var propertyValue, propertyNames)
               && propertyValue.ValueKind == JsonValueKind.Object
            ? propertyValue
            : null;
    }

    private static string ReadStringProperty(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetAnyProperty(element, out var propertyValue, propertyNames)
            || propertyValue.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return (propertyValue.GetString() ?? string.Empty).Trim();
    }

    private static IReadOnlyList<string> ReadStringArrayProperty(JsonElement element, string propertyName)
    {
        if (!TryGetAnyProperty(element, out var propertyValue, propertyName)
            || propertyValue.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return ParseStringArray(propertyValue);
    }

    private static IReadOnlyList<int> ReadIntArrayProperty(JsonElement element, string propertyName)
    {
        if (!TryGetAnyProperty(element, out var propertyValue, propertyName)
            || propertyValue.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        var list = new List<int>();
        foreach (var entry in propertyValue.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var parsedNumber))
                list.Add(parsedNumber);
            else if (entry.ValueKind == JsonValueKind.String && int.TryParse(entry.GetString(), out var parsedString))
                list.Add(parsedString);
        }

        return list;
    }

    private static bool ReadBooleanProperty(JsonElement element, string propertyName)
    {
        if (!TryGetAnyProperty(element, out var propertyValue, propertyName))
            return false;

        return propertyValue.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(propertyValue.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private static bool? ReadNullableBooleanProperty(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetAnyProperty(element, out var propertyValue, propertyNames))
            return null;

        return propertyValue.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(propertyValue.GetString(), out var parsed) ? parsed : null,
            _ => null
        };
    }

    private static int? ReadNullableIntProperty(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetAnyProperty(element, out var propertyValue, propertyNames))
            return null;

        if (propertyValue.ValueKind == JsonValueKind.Number && propertyValue.TryGetInt32(out var parsedNumber))
            return parsedNumber;

        if (propertyValue.ValueKind == JsonValueKind.String && int.TryParse(propertyValue.GetString(), out var parsedString))
            return parsedString;

        return null;
    }

    private static bool TryGetAnyProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out value))
                return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var name in names)
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static IReadOnlyList<string> ResolveFallbackStrategies(string definitionKey, bool mapped)
    {
        var ids = new List<string>();
        if (mapped)
            ids.Add("section:mapped");
        else
            ids.Add("section:choice");

        if (definitionKey.Equals("Wizard Colour", StringComparison.OrdinalIgnoreCase))
        {
            ids.Add("options:magic-colour");
            ids.Add("selection:multi-delimited");
        }

        if (definitionKey.Equals("Vivomancer Colour", StringComparison.OrdinalIgnoreCase))
        {
            ids.Add("options:vivomancer-colour");
            ids.Add("selection:multi-delimited");
        }

        if (definitionKey.Equals("Faerie Colour", StringComparison.OrdinalIgnoreCase))
        {
            ids.Add("options:magic-colour");
            ids.Add("selection:multi-delimited");
            ids.Add("validation:faerie-opposites");
            ids.Add("spells:faerie-colour-filter");
        }

        if (definitionKey.Equals("Standard Scout skill", StringComparison.OrdinalIgnoreCase)
            || definitionKey.Equals("Specialist Scout skill", StringComparison.OrdinalIgnoreCase))
        {
            ids.Add("lookup:scout-skill");
        }

        if (definitionKey.Equals("Earth Powers", StringComparison.OrdinalIgnoreCase))
            ids.Add("lookup:earth-power");

        if (definitionKey.Equals("Spells", StringComparison.OrdinalIgnoreCase))
            ids.Add("lookup:spell");

        if (definitionKey.Equals("Ward pact", StringComparison.OrdinalIgnoreCase))
        {
            ids.Add("lookup:ward-pact");
            ids.Add("selection:multi-delimited");
        }

        if (definitionKey.Equals("HumanSubtypeAbilities", StringComparison.OrdinalIgnoreCase))
            ids.Add("subtype:human-map");

        if (definitionKey.Equals("IshmaicClanAbilities", StringComparison.OrdinalIgnoreCase))
            ids.Add("dynamic:ishmaic-clan");

        if (definitionKey.Equals("RatfolkClanAbilities", StringComparison.OrdinalIgnoreCase))
            ids.Add("dynamic:ratfolk-clan");

        if (definitionKey.Equals("AmlesianCasteAbilities", StringComparison.OrdinalIgnoreCase))
            ids.Add("dynamic:amlesian-caste");

        if (definitionKey.Equals("BaronialAncestry", StringComparison.OrdinalIgnoreCase))
            ids.Add("dynamic:baronial-ancestry");

        return ids;
    }

    private static IReadOnlyList<string> ParseStrategyIds(JsonElement element, string propertyName = "StrategyIds")
    {
        if (element.ValueKind != JsonValueKind.Object)
            return Array.Empty<string>();

        if (!TryGetAnyProperty(element, out var strategyElement, propertyName)
            || strategyElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return ParseStringArray(strategyElement);
    }

    private static IReadOnlyList<string> MergeStrategyIds(
        IReadOnlyList<string> preferred,
        IReadOnlyList<string> inherited,
        IReadOnlyList<string> fallback)
    {
        if (preferred.Count > 0)
            return preferred.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (inherited.Count > 0)
            return inherited.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return fallback.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> ParseNotes(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return Array.Empty<string>();

        if (element.TryGetProperty("Notes", out var notesElement))
        {
            if (notesElement.ValueKind == JsonValueKind.Array)
                return ParseStringArray(notesElement);

            if (notesElement.ValueKind == JsonValueKind.String)
            {
                var single = (notesElement.GetString() ?? string.Empty).Trim();
                if (single.Length > 0)
                    return new[] { single };
            }
        }

        return Array.Empty<string>();
    }

    private static List<string> ParseStringArray(JsonElement element)
    {
        var result = new List<string>();
        if (element.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;

            var text = (item.GetString() ?? string.Empty).Trim();
            if (text.Length > 0)
                result.Add(text);
        }

        return result;
    }
}
