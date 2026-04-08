using labyItems.Models.Characters;
using labyItems.Helpers;
using labyItems.Services.AbilityEffects;

namespace labyItems.Services;

public static class BattleboardAbilityEffectResolver
{
    private static readonly string[] ResistanceTypes = { "Physical", "Magic", "Neuro", "Spirit" };

    public static BattleboardAdvancementEffects ResolveFallback(IEnumerable<AbilityDraft>? abilities)
    {
        var resistance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var multipliers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var immunities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            foreach (var candidate in EnumerateFallbackTextCandidates(ability))
                TextFallbackEffectApplier.Apply(candidate, resistance, immunities, multipliers, infiniteResistanceTypes);

            if (ability.AbilityType == AbilityType.Immunity)
            {
                var name = (ability.BattleboardNameOverride ?? ability.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                    continue;

                var normalized = name.StartsWith("Immunity to ", StringComparison.OrdinalIgnoreCase)
                    ? name
                    : $"Immunity to {name}";
                immunities.Add(normalized);
            }
        }

        return new BattleboardAdvancementEffects(
            resistance,
            immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            multipliers,
            infiniteResistanceTypes,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
    }

    public static BattleboardAdvancementEffects ResolveFallback(CharacterDraft? draft)
    {
        var character = draft ?? new CharacterDraft();
        var resolved = ResolveFallback(character.Abilities);
        var multipliers = new Dictionary<string, int>(resolved.ResistanceMultipliers, StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(resolved.InfiniteResistanceTypes, StringComparer.OrdinalIgnoreCase);
        var perSixths = new Dictionary<string, int>(resolved.ResistancePerSixths, StringComparer.OrdinalIgnoreCase);

        ApplyFallbackRaceHeuristics(character, multipliers, infiniteResistanceTypes);
        ApplyFallbackMultiRaceHeuristics(character, multipliers, perSixths, infiniteResistanceTypes);

        return new BattleboardAdvancementEffects(
            new Dictionary<string, int>(resolved.ResistanceOverrides, StringComparer.OrdinalIgnoreCase),
            resolved.Immunities.ToList(),
            multipliers,
            infiniteResistanceTypes,
            perSixths);
    }

    public static async Task<BattleboardAdvancementEffects> ResolveAsync(IEnumerable<AbilityDraft>? abilities)
    {
        var abilityList = (abilities ?? Array.Empty<AbilityDraft>())
            .Where(ability => ability != null)
            .ToList();

        var fallback = ResolveFallback(abilityList);
        var resistance = new Dictionary<string, int>(fallback.ResistanceOverrides, StringComparer.OrdinalIgnoreCase);
        var multipliers = new Dictionary<string, int>(fallback.ResistanceMultipliers, StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(fallback.InfiniteResistanceTypes, StringComparer.OrdinalIgnoreCase);
        var perSixths = new Dictionary<string, int>(fallback.ResistancePerSixths, StringComparer.OrdinalIgnoreCase);
        var immunities = new HashSet<string>(fallback.Immunities, StringComparer.OrdinalIgnoreCase);

        if (abilityList.Count == 0)
            return new BattleboardAdvancementEffects(resistance, immunities.ToList(), multipliers, infiniteResistanceTypes, perSixths);

        try
        {
            var definitionLookup = await AbilityDefinitionLookupService.GetLookupAsync();
            foreach (var ability in abilityList)
            {
                var definition = ResolveDefinition(definitionLookup, ability);
                var effects = definition?.SystemEffects;
                if (effects is { Count: > 0 })
                {
                    var instructions = AbilityEffectEvaluator.FromSystemEffects(effects);
                    AbilityEffectEvaluator.ApplyInstructions(
                        instructions,
                        resistance,
                        immunities,
                        multipliers,
                        infiniteResistanceTypes);
                }

                foreach (var candidate in EnumerateFallbackTextCandidates(ability))
                    TextFallbackEffectApplier.Apply(candidate, resistance, immunities, multipliers, infiniteResistanceTypes);

                TextFallbackEffectApplier.Apply(definition?.Effect, resistance, immunities, multipliers, infiniteResistanceTypes);
            }
        }
        catch
        {
            // Keep fallback behaviour when lookup is unavailable.
        }

        return new BattleboardAdvancementEffects(
            resistance,
            immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            multipliers,
            infiniteResistanceTypes,
            perSixths);
    }

    public static async Task<BattleboardAdvancementEffects> ResolveAsync(CharacterDraft? draft)
    {
        var character = draft ?? new CharacterDraft();

        var baseEffectsTask = ResolveAsync(character.Abilities);
        var supplementalEffectsTask = ResolveSupplementalEffectsAsync(character);
        await Task.WhenAll(baseEffectsTask, supplementalEffectsTask);

        var merged = Merge(baseEffectsTask.Result, supplementalEffectsTask.Result);
        var multipliers = new Dictionary<string, int>(merged.ResistanceMultipliers, StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(merged.InfiniteResistanceTypes, StringComparer.OrdinalIgnoreCase);
        var perSixths = new Dictionary<string, int>(merged.ResistancePerSixths, StringComparer.OrdinalIgnoreCase);

        ApplyFallbackRaceHeuristics(character, multipliers, infiniteResistanceTypes);
        ApplyFallbackMultiRaceHeuristics(character, multipliers, perSixths, infiniteResistanceTypes);

        return new BattleboardAdvancementEffects(
            new Dictionary<string, int>(merged.ResistanceOverrides, StringComparer.OrdinalIgnoreCase),
            merged.Immunities.ToList(),
            multipliers,
            infiniteResistanceTypes,
            perSixths);
    }

    private static AbilityDefinition? ResolveDefinition(
        IReadOnlyDictionary<string, AbilityDefinition> lookup,
        AbilityDraft ability)
    {
        return AbilityDefinitionLookupService.Find(lookup, ability.AbilityKey)
               ?? AbilityDefinitionLookupService.Find(lookup, ability.Name)
               ?? AbilityDefinitionLookupService.Find(lookup, ability.UpdateKey)
               ?? AbilityDefinitionLookupService.Find(lookup, ability.BattleboardNameOverride)
               ?? AbilityDefinitionLookupService.Find(lookup, ability.OverwriteKey);
    }

    private static IEnumerable<string?> EnumerateFallbackTextCandidates(AbilityDraft ability)
    {
        yield return ability.BattleboardNameOverride;
        yield return ability.Name;
        yield return ability.Effect;
        yield return ability.ShortStringValue;
    }

    private static BattleboardAdvancementEffects Merge(
        BattleboardAdvancementEffects baseline,
        BattleboardAdvancementEffects incoming)
    {
        var resistance = new Dictionary<string, int>(
            BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(
                baseline.ResistanceOverrides,
                incoming.ResistanceOverrides),
            StringComparer.OrdinalIgnoreCase);
        var multipliers = new Dictionary<string, int>(
            BattleboardAdvancementEffectResolver.ApplyResistanceMultipliers(
                baseline.ResistanceMultipliers,
                incoming.ResistanceMultipliers),
            StringComparer.OrdinalIgnoreCase);
        var infinite = new HashSet<string>(
            BattleboardAdvancementEffectResolver.MergeInfiniteResistanceTypes(
                baseline.InfiniteResistanceTypes,
                incoming.InfiniteResistanceTypes),
            StringComparer.OrdinalIgnoreCase);
        var perSixths = new Dictionary<string, int>(
            BattleboardAdvancementEffectResolver.ApplyResistancePerSixths(
                baseline.ResistancePerSixths,
                incoming.ResistancePerSixths),
            StringComparer.OrdinalIgnoreCase);
        var immunities = baseline.Immunities
            .Concat(incoming.Immunities)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BattleboardAdvancementEffects(
            resistance,
            immunities,
            multipliers,
            infinite,
            perSixths);
    }

    private static void ApplyFallbackRaceHeuristics(
        CharacterDraft draft,
        IDictionary<string, int> multipliers,
        ISet<string> infiniteResistanceTypes)
    {
        var race = NormalizeToken(draft.Race);
        if (race.Length == 0)
            return;

        if (race is "halfelf" or "halfathfanal" or "calabrim")
            SetResistanceMultiplier(multipliers, "Spirit", 2);

        if (race is "samila" or "elf" or "faerie" or "athfanal" or "farfolk")
            infiniteResistanceTypes.Add("Spirit");

        if (race is "samila")
            infiniteResistanceTypes.Add("Neuro");
    }

    private static void ApplyFallbackMultiRaceHeuristics(
        CharacterDraft draft,
        IDictionary<string, int> multipliers,
        IDictionary<string, int> perSixths,
        ISet<string> infiniteResistanceTypes)
    {
        var key = NormalizeToken(draft.MultiRaceKey);
        var level = Math.Max(0, draft.MultiRaceLevel);
        if (key.Length == 0 || level <= 0)
            return;

        if (key.Equals("highelf", StringComparison.OrdinalIgnoreCase))
        {
            SetResistanceMultiplier(multipliers, "Spirit", 2);
            return;
        }

        if (key.Equals("mindoverreality", StringComparison.OrdinalIgnoreCase))
            ApplyScaledPerSixths(perSixths, infiniteResistanceTypes, exceptedTypes: new[] { "Physical", "Neuro" }, level);
    }

    private static async Task<BattleboardAdvancementEffects> ResolveSupplementalEffectsAsync(CharacterDraft draft)
    {
        var supplementalAbilities = new List<AbilityDraft>();
        var perSixths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var races = await PeopleService.GetAllAsync();
            if (TryResolveRaceRecord(races, draft.Race, out var raceRecord))
            {
                var raceAbilities = AbilityDraftBuilder.BuildFromLevels(
                    raceRecord.LevelledAbilities,
                    achievedLevel: 8,
                    achievedTable: CharacterProgressionTables.GetHighestTableReached(draft.Points));
                foreach (var ability in raceAbilities)
                {
                    if (ability == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(ability.Source))
                        ability.Source = "Race";

                    supplementalAbilities.Add(ability);
                }

                foreach (var tag in raceRecord.Tags ?? new List<string>())
                {
                    var tagText = (tag ?? string.Empty).Trim();
                    if (tagText.Length == 0)
                        continue;

                    supplementalAbilities.Add(new AbilityDraft
                    {
                        Name = tagText,
                        AbilityType = AbilityType.Resistance,
                        Source = "Race"
                    });
                }
            }
        }
        catch
        {
            // Keep existing behaviour when race metadata cannot be loaded.
        }

        try
        {
            var catalog = await MultiRaceService.GetCatalogAsync();
            if (TryResolveMultiRaceDefinition(catalog.MultiRaces, draft.MultiRaceKey, out var definition))
            {
                var ownedTokens = BuildOwnedTokenSet(draft);
                var maxLevel = ResolveMaxLevel(definition);
                var selectedLevel = Math.Clamp(draft.MultiRaceLevel, 0, maxLevel);
                var applyPerSixthsToResistance = ShouldApplyPerSixthScalingToResistance(draft.MultiRaceKey, definition);
                if (selectedLevel > 0)
                {
                    for (var level = 1; level <= selectedLevel; level++)
                    {
                        if (!definition.Levels.TryGetValue(level.ToString(), out var levelAbilities)
                            || levelAbilities == null)
                        {
                            continue;
                        }

                        foreach (var levelAbility in levelAbilities)
                        {
                            if (levelAbility == null)
                                continue;
                            if (!ArePreReqsSatisfied(levelAbility.PreReqs, ownedTokens))
                                continue;

                            var abilityDefinition = new AbilityDefinition
                            {
                                Name = levelAbility.Name,
                                Key = levelAbility.AbilityRef,
                                AbilityRef = levelAbility.AbilityRef,
                                Type = levelAbility.Type,
                                Effect = levelAbility.Effect,
                                Count = levelAbility.Count,
                                Progression = levelAbility.Progression,
                                PreReqs = levelAbility.PreReqs
                            };

                            var parsed = AbilityDraftBuilder.ParseAbility(
                                abilityDefinition,
                                levelGained: level,
                                achievedLevel: selectedLevel);
                            foreach (var abilityDraft in parsed)
                            {
                                if (abilityDraft == null)
                                    continue;

                                abilityDraft.Source = "MultiRace";
                                supplementalAbilities.Add(abilityDraft);

                                AddOwnedToken(ownedTokens, abilityDraft.AbilityKey);
                                AddOwnedToken(ownedTokens, abilityDraft.Name);
                                AddOwnedToken(ownedTokens, abilityDraft.UpdateKey);
                                AddOwnedToken(ownedTokens, abilityDraft.OverwriteKey);
                            }

                            if (applyPerSixthsToResistance
                                && TryParsePerSixthScalingTargets(levelAbility.Effect, out var affectedTypes))
                            {
                                ApplyScaledPerSixths(perSixths, infiniteResistanceTypes, affectedTypes, selectedLevel);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Keep existing behaviour when multi-race metadata cannot be loaded.
        }

        if (supplementalAbilities.Count == 0
            && perSixths.Count == 0
            && infiniteResistanceTypes.Count == 0)
        {
            return new BattleboardAdvancementEffects(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>(),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        }

        var resolved = await ResolveAsync(supplementalAbilities);
        var mergedInfinite = new HashSet<string>(
            BattleboardAdvancementEffectResolver.MergeInfiniteResistanceTypes(
                resolved.InfiniteResistanceTypes,
                infiniteResistanceTypes),
            StringComparer.OrdinalIgnoreCase);
        var mergedPerSixths = new Dictionary<string, int>(
            BattleboardAdvancementEffectResolver.ApplyResistancePerSixths(
                resolved.ResistancePerSixths,
                perSixths),
            StringComparer.OrdinalIgnoreCase);

        return new BattleboardAdvancementEffects(
            new Dictionary<string, int>(resolved.ResistanceOverrides, StringComparer.OrdinalIgnoreCase),
            resolved.Immunities.ToList(),
            new Dictionary<string, int>(resolved.ResistanceMultipliers, StringComparer.OrdinalIgnoreCase),
            mergedInfinite,
            mergedPerSixths);
    }

    private static void SetResistanceMultiplier(
        IDictionary<string, int> multipliers,
        string resistanceType,
        int multiplier)
    {
        var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(resistanceType);
        if (key.Length == 0 || multiplier <= 1)
            return;

        if (!multipliers.TryGetValue(key, out var existing) || multiplier > existing)
            multipliers[key] = multiplier;
    }

    private static void ApplyScaledPerSixths(
        IDictionary<string, int> perSixths,
        ISet<string> infiniteResistanceTypes,
        IEnumerable<string> exceptedTypes,
        int level)
    {
        var excepted = new HashSet<string>(
            (exceptedTypes ?? Array.Empty<string>())
            .Select(BattleboardAdvancementEffectResolver.NormalizeResistanceType)
            .Where(x => x.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var clampedLevel = Math.Clamp(level, 0, 6);
        if (clampedLevel <= 0)
            return;

        foreach (var raw in ResistanceTypes)
        {
            var key = BattleboardAdvancementEffectResolver.NormalizeResistanceType(raw);
            if (key.Length == 0 || excepted.Contains(key))
                continue;

            if (clampedLevel >= 6)
            {
                infiniteResistanceTypes.Add(key);
                perSixths.Remove(key);
                continue;
            }

            if (!perSixths.TryGetValue(key, out var existing) || clampedLevel > existing)
                perSixths[key] = clampedLevel;
        }
    }

    private static bool TryParsePerSixthScalingTargets(string? source, out IReadOnlyList<string> exceptedTypes)
    {
        exceptedTypes = Array.Empty<string>();
        var text = (source ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var normalized = text.ToLowerInvariant();
        if (!normalized.Contains("off all damage", StringComparison.Ordinal)
            || !normalized.Contains("per level", StringComparison.Ordinal))
        {
            return false;
        }

        var hasOneSixth = normalized.Contains("one-sixth", StringComparison.Ordinal)
                          || normalized.Contains("1/6", StringComparison.Ordinal)
                          || normalized.Contains("one sixth", StringComparison.Ordinal);
        if (!hasOneSixth)
            return false;

        var marker = normalized.Contains("except", StringComparison.Ordinal)
            ? "except"
            : normalized.Contains("all but", StringComparison.Ordinal)
                ? "all but"
                : string.Empty;
        if (marker.Length == 0)
            return false;

        var markerIndex = normalized.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;

        var tail = normalized[(markerIndex + marker.Length)..];
        var perLevelIndex = tail.IndexOf("per level", StringComparison.Ordinal);
        if (perLevelIndex >= 0)
            tail = tail[..perLevelIndex];

        var parsedExcepted = ParseResistanceTypes(tail);
        if (parsedExcepted.Count == 0)
            return false;

        exceptedTypes = parsedExcepted;
        return true;
    }

    private static IReadOnlyList<string> ParseResistanceTypes(string? source)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = (source ?? string.Empty).ToLowerInvariant();
        if (text.Contains("phys", StringComparison.Ordinal))
            set.Add("Physical");
        if (text.Contains("magic", StringComparison.Ordinal))
            set.Add("Magic");
        if (text.Contains("neuronic", StringComparison.Ordinal) || text.Contains("neuro", StringComparison.Ordinal))
            set.Add("Neuro");
        if (text.Contains("spirit", StringComparison.Ordinal))
            set.Add("Spirit");
        return set.ToList();
    }

    private static bool TryResolveRaceRecord(
        IReadOnlyDictionary<string, PeopleRecord> races,
        string? raceName,
        out PeopleRecord record)
    {
        record = null!;

        var raw = (raceName ?? string.Empty).Trim();
        if (raw.Length == 0)
            return false;

        if (races.TryGetValue(raw, out var direct) && direct != null)
        {
            record = direct;
            return true;
        }

        var wanted = NormalizeToken(raw);
        foreach (var pair in races)
        {
            if (NormalizeToken(pair.Key).Equals(wanted, StringComparison.OrdinalIgnoreCase))
            {
                record = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveMultiRaceDefinition(
        IReadOnlyDictionary<string, MultiRaceDefinition> definitions,
        string? selectedKey,
        out MultiRaceDefinition definition)
    {
        definition = null!;
        var raw = (selectedKey ?? string.Empty).Trim();
        if (raw.Length == 0 || definitions.Count == 0)
            return false;

        if (definitions.TryGetValue(raw, out var direct) && direct != null)
        {
            definition = direct;
            return true;
        }

        var wanted = NormalizeToken(raw);
        foreach (var pair in definitions)
        {
            if (NormalizeToken(pair.Key).Equals(wanted, StringComparison.OrdinalIgnoreCase))
            {
                definition = pair.Value;
                return true;
            }

            var display = (pair.Value.DisplayName ?? string.Empty).Trim();
            if (display.Length == 0)
                continue;

            if (NormalizeToken(display).Equals(wanted, StringComparison.OrdinalIgnoreCase))
            {
                definition = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static int ResolveMaxLevel(MultiRaceDefinition definition)
    {
        if (definition.MaxLevel > 0)
            return definition.MaxLevel;

        var parsedMax = definition.Levels.Keys
            .Select(level => int.TryParse(level, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(1, parsedMax);
    }

    private static bool ShouldApplyPerSixthScalingToResistance(string? selectedKey, MultiRaceDefinition definition)
    {
        var selected = NormalizeToken(selectedKey);
        if (selected.Equals("mindoverreality", StringComparison.OrdinalIgnoreCase))
            return true;

        var display = NormalizeToken(definition.DisplayName);
        return display.Equals("mindoverreality", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string((value ?? string.Empty)
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static HashSet<string> BuildOwnedTokenSet(CharacterDraft draft)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ability in draft.Abilities ?? new List<AbilityDraft>())
        {
            AddOwnedToken(tokens, ability?.AbilityKey);
            AddOwnedToken(tokens, ability?.Name);
            AddOwnedToken(tokens, ability?.UpdateKey);
            AddOwnedToken(tokens, ability?.OverwriteKey);
        }

        foreach (var advancement in draft.AdvancementAbilities ?? new List<string>())
            AddOwnedToken(tokens, advancement);

        foreach (var selection in draft.MultiRaceChoiceSelections ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            AddOwnedToken(tokens, selection.Key);
            AddOwnedToken(tokens, selection.Value);
        }

        return tokens;
    }

    private static void AddOwnedToken(ISet<string> tokens, string? raw)
    {
        var normalized = AbilityDefinitionLookupService.NormalizeKey(raw);
        if (normalized.Length > 0)
            tokens.Add(normalized);
    }

    private static bool ArePreReqsSatisfied(IEnumerable<string>? preReqs, ISet<string> ownedTokens)
    {
        foreach (var raw in preReqs ?? Array.Empty<string>())
        {
            var token = (raw ?? string.Empty).Trim();
            if (token.Length == 0)
                continue;

            var normalized = AbilityDefinitionLookupService.NormalizeKey(token);
            if (normalized.Length == 0)
                continue;

            if (!ownedTokens.Contains(normalized))
                return false;
        }

        return true;
    }
}
