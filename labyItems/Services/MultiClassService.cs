using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Services;

public static class MultiClassService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static MultiClassCatalog? _cache;

    public static async Task<MultiClassCatalog> GetCatalogAsync()
    {
        if (_cache != null)
            return _cache;

        var catalog = await LoadAdvancedCatalogAsync();
        await MergeStandardClassDefinitionsAsync(catalog);

        _cache = catalog;

        return _cache;
    }

    public static void InvalidateCache()
        => _cache = null;

    private static async Task<MultiClassCatalog> LoadAdvancedCatalogAsync()
    {
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("people/multi-classes.json");
            return JsonSerializer.Deserialize<MultiClassCatalog>(json, JsonOptions)
                   ?? new MultiClassCatalog();
        }
        catch
        {
            return new MultiClassCatalog();
        }
    }

    private static async Task MergeStandardClassDefinitionsAsync(MultiClassCatalog catalog)
    {
        StandardMultiClassCostCatalog? costCatalog;
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("people/standard-multi-class-costs.json");
            costCatalog = JsonSerializer.Deserialize<StandardMultiClassCostCatalog>(json, JsonOptions);
        }
        catch
        {
            return;
        }

        if (costCatalog?.CostsBySourceClass == null || costCatalog.CostsBySourceClass.Count == 0)
            return;

        var classes = await ClassService.GetAllAsync();
        if (classes.Count == 0)
            return;

        var choiceSetRefsByAbilityToken = await LoadClassSpecialisationChoiceSetRefsAsync();
        var byTarget = BuildAvailabilityByTarget(costCatalog.CostsBySourceClass, classes);
        foreach (var target in byTarget)
        {
            if (catalog.MultiClasses.ContainsKey(target.Key))
                continue;

            var className = target.Key;
            classes.TryGetValue(className, out var classRecord);

            catalog.MultiClasses[className] = BuildStandardDefinition(
                className,
                classRecord,
                target.Value,
                choiceSetRefsByAbilityToken,
                ResolveStandardTargetIconGlyph(className, costCatalog));
        }
    }

    private static Dictionary<string, List<MultiClassAvailabilityOption>> BuildAvailabilityByTarget(
        Dictionary<string, Dictionary<string, Dictionary<string, int>>> costsBySourceClass,
        IReadOnlyDictionary<string, CharacterClassRecord> classes)
    {
        var byTarget = new Dictionary<string, List<MultiClassAvailabilityOption>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourcePair in costsBySourceClass)
        {
            var sourceClass = ResolveClassName(sourcePair.Key, classes);
            if (sourceClass.Length == 0)
                continue;

            foreach (var targetPair in sourcePair.Value ?? new Dictionary<string, Dictionary<string, int>>())
            {
                var targetClass = ResolveClassName(targetPair.Key, classes);
                if (targetClass.Length == 0)
                    continue;

                var costs = NormalizeCostsByLevel(targetPair.Value);
                if (costs.Count == 0)
                    continue;

                if (!byTarget.TryGetValue(targetClass, out var options))
                {
                    options = new List<MultiClassAvailabilityOption>();
                    byTarget[targetClass] = options;
                }

                options.Add(new MultiClassAvailabilityOption
                {
                    Source = sourceClass,
                    Display = $"1st class {sourceClass}",
                    Rules = new List<RuleClause>
                    {
                        new()
                        {
                            Field = "Class",
                            Operator = RuleComparisonOp.In,
                            Value = new List<string> { sourceClass }
                        }
                    },
                    CostsByLevel = costs
                });
            }
        }

        foreach (var pair in byTarget)
        {
            pair.Value.Sort((left, right) =>
                string.Compare(left.Source, right.Source, StringComparison.OrdinalIgnoreCase));
        }

        return byTarget;
    }

    private static MultiClassDefinition BuildStandardDefinition(
        string className,
        CharacterClassRecord? classRecord,
        IReadOnlyList<MultiClassAvailabilityOption> options,
        IReadOnlyDictionary<string, IReadOnlyList<string>> choiceSetRefsByAbilityToken,
        string iconGlyph)
    {
        var maxCostLevel = options
            .SelectMany(option => option.CostsByLevel.Keys)
            .Select(ParseLevel)
            .DefaultIfEmpty(0)
            .Max();

        var maxAbilityLevel = classRecord?.Levels?.Keys
            .Select(ParseLevel)
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        var maxLevel = Math.Max(1, Math.Max(maxCostLevel, maxAbilityLevel));
        var levels = new Dictionary<string, List<MultiClassLevelAbility>>(StringComparer.OrdinalIgnoreCase);
        var systemEffects = new Dictionary<string, List<MultiClassSystemEffect>>(StringComparer.OrdinalIgnoreCase);

        for (var level = 1; level <= maxLevel; level++)
        {
            var levelKey = level.ToString();
            if (classRecord?.Levels != null
                && classRecord.Levels.TryGetValue(levelKey, out var abilityDefs)
                && abilityDefs != null
                && abilityDefs.Count > 0)
            {
                levels[levelKey] = abilityDefs
                    .Where(ability => ability != null)
                    .Select(ability => MapClassAbilityToMultiClass(ability, choiceSetRefsByAbilityToken))
                    .ToList();
            }

            systemEffects[levelKey] = new List<MultiClassSystemEffect>
            {
                new()
                {
                    EffectType = "BaseLifeFromLifeScale",
                    Display = $"Base life as {className} level {level}",
                    LifeScaleReference = new MultiClassLifeScaleReference
                    {
                        File = "lifescales.json",
                        Class = className,
                        Level = level
                    },
                    Value = EmptyJsonObject(),
                    SourceCategory = "base",
                    Conditions = new List<RuleClause>(),
                    LinkedAbilityRefs = new List<string>()
                }
            };
        }

        var maxAc = ReadMaxAc(classRecord);
        if (maxAc > 0)
        {
            if (!systemEffects.TryGetValue("1", out var levelOneEffects))
            {
                levelOneEffects = new List<MultiClassSystemEffect>();
                systemEffects["1"] = levelOneEffects;
            }

            levelOneEffects.Add(new MultiClassSystemEffect
            {
                EffectType = "MaxAcOverride",
                Display = $"Base max AC is {maxAc}",
                Value = NumberJson(maxAc),
                SourceCategory = "base",
                Conditions = new List<RuleClause>(),
                LinkedAbilityRefs = new List<string>()
            });
        }

        return new MultiClassDefinition
        {
            DisplayName = className,
            IconGlyph = iconGlyph,
            MaxLevel = maxLevel,
            AvailabilityOptions = options.ToList(),
            Levels = levels,
            SystemEffectsByLevel = systemEffects
        };
    }

    private static string ResolveStandardTargetIconGlyph(string className, StandardMultiClassCostCatalog? costCatalog)
    {
        var iconMap = costCatalog?.IconGlyphByTargetClass;
        if (iconMap == null || iconMap.Count == 0)
            return string.Empty;

        if (iconMap.TryGetValue(className, out var direct))
            return (direct ?? string.Empty).Trim();

        var wanted = NormalizeToken(className);
        foreach (var pair in iconMap)
        {
            if (NormalizeToken(pair.Key).Equals(wanted, StringComparison.OrdinalIgnoreCase))
                return (pair.Value ?? string.Empty).Trim();
        }

        return string.Empty;
    }

    private static MultiClassLevelAbility MapClassAbilityToMultiClass(
        AbilityDefinition ability,
        IReadOnlyDictionary<string, IReadOnlyList<string>> choiceSetRefsByAbilityToken)
    {
        var name = (ability?.Name ?? string.Empty).Trim();
        var key = (ability?.Key ?? string.Empty).Trim();
        var overwriteKey = (ability?.OverwriteKey ?? string.Empty).Trim();
        var updateKey = (ability?.UpdateKey ?? string.Empty).Trim();
        var abilityRef = key.Length > 0
            ? key
            : overwriteKey.Length > 0
                ? overwriteKey
                : updateKey.Length > 0
                    ? updateKey
                    : name;

        var choiceSetRefs = ResolveChoiceSetRefsForAbility(ability, choiceSetRefsByAbilityToken);

        return new MultiClassLevelAbility
        {
            Name = name,
            AbilityRef = abilityRef,
            Type = (ability?.Type ?? string.Empty).Trim(),
            Effect = (ability?.Effect ?? string.Empty).Trim(),
            Count = ability?.Count,
            Progression = ability?.Progression,
            PreReqs = ability?.PreReqs?.ToList(),
            ChoiceSetRefs = choiceSetRefs.Count > 0 ? choiceSetRefs.ToList() : null
        };
    }

    private static IReadOnlyList<string> ResolveChoiceSetRefsForAbility(
        AbilityDefinition? ability,
        IReadOnlyDictionary<string, IReadOnlyList<string>> choiceSetRefsByAbilityToken)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in EnumerateAbilityLookupTokens(ability))
        {
            if (!choiceSetRefsByAbilityToken.TryGetValue(token, out var mapped))
                continue;

            foreach (var item in mapped)
            {
                var trimmed = (item ?? string.Empty).Trim();
                if (trimmed.Length > 0)
                    refs.Add(trimmed);
            }
        }

        return refs.ToList();
    }

    private static IEnumerable<string> EnumerateAbilityLookupTokens(AbilityDefinition? ability)
    {
        var candidates = new[]
        {
            ability?.Name,
            ability?.OverwriteKey,
            ability?.UpdateKey,
            ability?.Key
        };

        foreach (var candidate in candidates)
        {
            var token = NormalizeChoiceSetLookupToken(candidate);
            if (token.Length > 0)
                yield return token;
        }
    }

    private static string ResolveClassName(string rawName, IReadOnlyDictionary<string, CharacterClassRecord> classes)
    {
        var trimmed = (rawName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        if (classes.ContainsKey(trimmed))
            return trimmed;

        var normalizedWanted = NormalizeToken(trimmed);
        foreach (var key in classes.Keys)
        {
            if (NormalizeToken(key).Equals(normalizedWanted, StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return trimmed;
    }

    private static string NormalizeToken(string value)
        => new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static string NormalizeChoiceSetLookupToken(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.EndsWith(" skill", StringComparison.OrdinalIgnoreCase))
            text = text[..^6].Trim();

        return NormalizeToken(text);
    }

    private static Dictionary<string, int> NormalizeCostsByLevel(Dictionary<string, int>? rawCosts)
    {
        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in rawCosts ?? new Dictionary<string, int>())
        {
            var parsedLevel = ParseLevel(pair.Key);
            if (parsedLevel <= 0)
                continue;

            normalized[parsedLevel.ToString()] = Math.Max(0, pair.Value);
        }

        return normalized;
    }

    private static int ParseLevel(string? rawLevel)
    {
        if (!int.TryParse((rawLevel ?? string.Empty).Trim(), out var parsed))
            return 0;

        return parsed > 0 ? parsed : 0;
    }

    private static int ReadMaxAc(CharacterClassRecord? classRecord)
    {
        if (classRecord == null)
            return 0;

        var maxAc = classRecord.MaxAC;
        if (maxAc.ValueKind == JsonValueKind.Number && maxAc.TryGetInt32(out var number))
            return number;

        if (maxAc.ValueKind == JsonValueKind.String
            && int.TryParse(maxAc.GetString(), out number))
        {
            return number;
        }

        return 0;
    }

    private static JsonElement NumberJson(int value)
    {
        using var doc = JsonDocument.Parse(value.ToString());
        return doc.RootElement.Clone();
    }

    private static JsonElement EmptyJsonObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private static async Task<Dictionary<string, IReadOnlyList<string>>> LoadClassSpecialisationChoiceSetRefsAsync()
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("specialisation/class-specialisations.json");
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("definitions", out var definitions)
                || definitions.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var definition in definitions.EnumerateObject())
            {
                if (definition.Value.ValueKind != JsonValueKind.Object)
                    continue;

                if (!TryReadChoiceSetRefs(definition.Value, out var choiceSetRefs))
                    continue;

                var lookupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    definition.Name
                };

                if (definition.Value.TryGetProperty("DisplayName", out var displayNameElement)
                    && displayNameElement.ValueKind == JsonValueKind.String)
                {
                    var displayName = (displayNameElement.GetString() ?? string.Empty).Trim();
                    if (displayName.Length > 0)
                        lookupNames.Add(displayName);
                }

                foreach (var name in lookupNames)
                {
                    var token = NormalizeChoiceSetLookupToken(name);
                    if (token.Length == 0)
                        continue;

                    map[token] = choiceSetRefs;
                }
            }
        }
        catch
        {
            return map;
        }

        return map;
    }

    private static bool TryReadChoiceSetRefs(JsonElement definitionElement, out IReadOnlyList<string> refs)
    {
        refs = Array.Empty<string>();
        if (!definitionElement.TryGetProperty("ChoiceSetRefs", out var refsElement)
            || refsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = refsElement.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => (item.GetString() ?? string.Empty).Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parsed.Count == 0)
            return false;

        refs = parsed;
        return true;
    }
}

public sealed class StandardMultiClassCostCatalog
{
    [JsonPropertyName("costsBySourceClass")]
    public Dictionary<string, Dictionary<string, Dictionary<string, int>>> CostsBySourceClass { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("iconGlyphByTargetClass")]
    public Dictionary<string, string> IconGlyphByTargetClass { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MultiClassCatalog
{
    [JsonPropertyName("metadata")]
    public JsonElement Metadata { get; set; }

    [JsonPropertyName("multiClasses")]
    public Dictionary<string, MultiClassDefinition> MultiClasses { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MultiClassDefinition :
    IMultiPathDefinition<MultiClassAvailabilityOption, MultiClassLevelAbility, MultiClassSystemEffect>
{
    public string DisplayName { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public int MaxLevel { get; set; }
    public Dictionary<string, List<MultiClassLevelAbility>> Levels { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public List<MultiClassAvailabilityOption> AvailabilityOptions { get; set; } = new();
    public Dictionary<string, List<MultiClassSystemEffect>> SystemEffectsByLevel { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("requiresBracketPure")]
    public string? RequiresBracketPure { get; set; }
}

public sealed class MultiClassLevelAbility : IMultiPathLevelAbility
{
    public string Name { get; set; } = string.Empty;
    public string AbilityRef { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public int? Count { get; set; }
    public AbilityCountProgression? Progression { get; set; }
    public List<string>? AsPerAbilityRefs { get; set; }
    public List<string>? PreReqs { get; set; }
    public List<string>? ChoiceSetRefs { get; set; }
}

public sealed class MultiClassAvailabilityOption : IMultiPathAvailabilityOption
{
    public string Source { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public List<RuleClause> Rules { get; set; } = new();
    public Dictionary<string, int> CostsByLevel { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MultiClassSystemEffect : IMultiPathSystemEffect<MultiClassLifeScaleReference>
{
    public string EffectType { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
    public MultiClassLifeScaleReference? LifeScaleReference { get; set; }
    public List<RuleClause> Conditions { get; set; } = new();
    public string SourceCategory { get; set; } = string.Empty;
    public List<string> LinkedAbilityRefs { get; set; } = new();
}

public sealed class MultiClassLifeScaleReference : IMultiPathLifeScaleReference
{
    public string File { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Level { get; set; }
}
