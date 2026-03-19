using System.Text.Json;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services.AbilityEffects;

namespace labyItems.Services;

public static class BattleboardItemEffectResolver
{
    public static BattleboardAdvancementEffects ResolveFallback(
        CharacterDraft? draft,
        IEnumerable<Item>? assignedItems = null)
    {
        var character = draft ?? new CharacterDraft();
        var resistance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var multipliers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var immunities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ability in EnumerateItemAbilities(character, assignedItems))
        {
            foreach (var candidate in EnumerateFallbackTextCandidates(ability))
                TextFallbackEffectApplier.Apply(candidate, resistance, immunities, multipliers, infiniteResistanceTypes);
        }

        return new BattleboardAdvancementEffects(
            resistance,
            immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            multipliers,
            infiniteResistanceTypes);
    }

    public static async Task<BattleboardAdvancementEffects> ResolveAsync(
        CharacterDraft? draft,
        IEnumerable<Item>? assignedItems = null)
    {
        var character = draft ?? new CharacterDraft();
        var abilityList = EnumerateItemAbilities(character, assignedItems).ToList();
        var resistance = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var multipliers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var immunities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (abilityList.Count == 0)
            return new BattleboardAdvancementEffects(resistance, immunities.ToList(), multipliers, infiniteResistanceTypes);

        try
        {
            var definitionLookup = await AbilityDefinitionLookupService.GetLookupAsync();
            var preReqContext = await BuildPreReqContextAsync(character);

            foreach (var ability in abilityList)
            {
                var matchedDefinitions = new List<AbilityDefinition>();
                var appliedDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var candidate in EnumerateEffectLookupCandidates(ability))
                {
                    var definition = AbilityDefinitionLookupService.Find(definitionLookup, candidate);
                    if (definition == null)
                        continue;

                    var definitionKey = (definition.Key ?? definition.AbilityRef ?? definition.Name ?? string.Empty).Trim();
                    if (definitionKey.Length > 0 && !appliedDefinitions.Add(definitionKey))
                        continue;

                    matchedDefinitions.Add(definition);
                }

                if (matchedDefinitions.Count == 0)
                {
                    foreach (var candidate in EnumerateFallbackTextCandidates(ability))
                        TextFallbackEffectApplier.Apply(candidate, resistance, immunities, multipliers, infiniteResistanceTypes);
                    continue;
                }

                var hasAnySatisfiedDefinition = false;
                foreach (var definition in matchedDefinitions)
                {
                    if (!ArePreReqsSatisfied(definition.PreReqs, preReqContext))
                        continue;

                    hasAnySatisfiedDefinition = true;

                    if (definition.SystemEffects is { Count: > 0 })
                    {
                        var instructions = AbilityEffectEvaluator.FromSystemEffects(definition.SystemEffects);
                        AbilityEffectEvaluator.ApplyInstructions(
                            instructions,
                            resistance,
                            immunities,
                            multipliers,
                            infiniteResistanceTypes);
                    }

                    TextFallbackEffectApplier.Apply(
                        definition.Effect,
                        resistance,
                        immunities,
                        multipliers,
                        infiniteResistanceTypes);
                }

                if (!hasAnySatisfiedDefinition)
                    continue;

                foreach (var candidate in EnumerateFallbackTextCandidates(ability))
                    TextFallbackEffectApplier.Apply(candidate, resistance, immunities, multipliers, infiniteResistanceTypes);
            }
        }
        catch
        {
            // Keep fallback behaviour when lookup data is unavailable.
            foreach (var ability in abilityList)
            {
                foreach (var candidate in EnumerateFallbackTextCandidates(ability))
                    TextFallbackEffectApplier.Apply(candidate, resistance, immunities, multipliers, infiniteResistanceTypes);
            }
        }

        return new BattleboardAdvancementEffects(
            resistance,
            immunities.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            multipliers,
            infiniteResistanceTypes);
    }

    private static IEnumerable<CalcResult> EnumerateItemAbilities(
        CharacterDraft draft,
        IEnumerable<Item>? assignedItems)
    {
        var items = (assignedItems ?? BattleboardInnateCalculator.ResolveAssignedItems(draft)).ToList();
        foreach (var item in items)
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            foreach (var ability in payload?.Item?.Abilities ?? new List<CalcResult>())
            {
                if (ability != null)
                    yield return ability;
            }
        }
    }

    private static IEnumerable<string?> EnumerateFallbackTextCandidates(CalcResult ability)
    {
        yield return ability.AbilityName;
        yield return ability.Summary;

        foreach (var selected in ExtractSelectedGeneralAbilities(ability))
            yield return selected.Name;
    }

    private static IEnumerable<string?> EnumerateEffectLookupCandidates(CalcResult ability)
    {
        yield return ability.AbilityName;

        foreach (var selected in ExtractSelectedGeneralAbilities(ability))
        {
            yield return selected.AbilityKey;
            yield return selected.AbilityRef;
            yield return selected.Name;
        }
    }

    private static IEnumerable<SelectedGeneralAbility> ExtractSelectedGeneralAbilities(CalcResult ability)
    {
        if (ability?.Details == null
            || !ability.Details.TryGetValue("selectedGeneralAbilities", out var raw)
            || raw == null)
        {
            yield break;
        }

        if (raw is JsonElement element)
        {
            foreach (var selected in ExtractSelectedGeneralAbilities(element))
                yield return selected;
            yield break;
        }

        if (raw is IEnumerable<object> objects)
        {
            foreach (var entry in objects)
            {
                var name = ReadObjectString(entry, "name");
                var abilityRef = ReadObjectString(entry, "abilityRef");
                var abilityKey = ReadObjectString(entry, "key");
                if (abilityKey.Length == 0)
                    abilityKey = ReadObjectString(entry, "abilityKey");
                if (name.Length == 0 && abilityRef.Length == 0 && abilityKey.Length == 0)
                    continue;

                yield return new SelectedGeneralAbility(name, abilityRef, abilityKey);
            }
        }
    }

    private static IEnumerable<SelectedGeneralAbility> ExtractSelectedGeneralAbilities(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var parsedName = (entry.GetString() ?? string.Empty).Trim();
                if (parsedName.Length > 0)
                    yield return new SelectedGeneralAbility(parsedName, string.Empty, string.Empty);
                continue;
            }

            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var selectedName = ReadJsonString(entry, "name");
            var abilityRef = ReadJsonString(entry, "abilityRef");
            var abilityKey = ReadJsonString(entry, "key");
            if (abilityKey.Length == 0)
                abilityKey = ReadJsonString(entry, "abilityKey");
            if (selectedName.Length == 0 && abilityRef.Length == 0 && abilityKey.Length == 0)
                continue;

            yield return new SelectedGeneralAbility(selectedName, abilityRef, abilityKey);
        }
    }

    private static string ReadJsonString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return (property.GetString() ?? string.Empty).Trim();
    }

    private static string ReadObjectString(object? entry, string propertyName)
    {
        if (entry == null)
            return string.Empty;

        if (entry is Dictionary<string, object?> dict)
        {
            if (!dict.TryGetValue(propertyName, out var value) || value == null)
                return string.Empty;

            return (value.ToString() ?? string.Empty).Trim();
        }

        var property = entry.GetType().GetProperty(propertyName);
        if (property == null)
            return string.Empty;

        var raw = property.GetValue(entry);
        return (raw?.ToString() ?? string.Empty).Trim();
    }

    private static async Task<PreReqContext> BuildPreReqContextAsync(CharacterDraft draft)
    {
        var knownAbilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddToken(string? token)
        {
            var normalized = AbilityDefinitionLookupService.NormalizeKey(token);
            if (normalized.Length > 0)
                knownAbilities.Add(normalized);
        }

        foreach (var ability in draft.Abilities ?? new List<AbilityDraft>())
        {
            if (ability == null)
                continue;

            AddToken(ability.AbilityKey);
            AddToken(ability.Name);
            AddToken(ability.BattleboardNameOverride);
            AddToken(ability.UpdateKey);
            AddToken(ability.OverwriteKey);
            AddToken(ability.ShortStringValue);
        }

        foreach (var ability in draft.AdvancementAbilities ?? new List<string>())
            AddToken(ability);

        var classTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddClassToken(classTokens, draft.Class);
        foreach (var pair in draft.MultiClassLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value > 0)
                AddClassToken(classTokens, pair.Key);
        }

        var peopleTypeTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var raceName = (draft.Race ?? string.Empty).Trim();
            if (raceName.Length > 0)
            {
                var races = await PeopleService.GetAllAsync();
                if (races.TryGetValue(raceName, out var record))
                {
                    foreach (var type in record.PeopleType ?? new List<string>())
                    {
                        var normalized = AbilityDefinitionLookupService.NormalizeKey(type);
                        if (normalized.Length > 0)
                            peopleTypeTokens.Add(normalized);
                    }
                }
            }
        }
        catch
        {
            // Keep people type context empty when data is unavailable.
        }

        return new PreReqContext(knownAbilities, classTokens, peopleTypeTokens);
    }

    private static void AddClassToken(ISet<string> target, string? className)
    {
        var normalized = AbilityDefinitionLookupService.NormalizeKey(className);
        if (normalized.Length > 0)
            target.Add(normalized);
    }

    private static bool ArePreReqsSatisfied(IEnumerable<string>? preReqs, PreReqContext context)
    {
        foreach (var raw in preReqs ?? Array.Empty<string>())
        {
            var preReq = (raw ?? string.Empty).Trim();
            if (preReq.Length == 0)
                continue;

            if (!TryParsePreReq(preReq, out var kind, out var value))
            {
                var normalized = AbilityDefinitionLookupService.NormalizeKey(preReq);
                if (normalized.Length > 0 && !context.Abilities.Contains(normalized))
                    return false;

                continue;
            }

            var normalizedValue = AbilityDefinitionLookupService.NormalizeKey(value);
            if (normalizedValue.Length == 0)
                continue;

            var normalizedKind = kind.ToLowerInvariant();
            if (normalizedKind == "ability")
            {
                if (!context.Abilities.Contains(normalizedValue))
                    return false;
                continue;
            }

            if (normalizedKind == "class")
            {
                if (!context.Classes.Contains(normalizedValue))
                    return false;
                continue;
            }

            if (normalizedKind == "peopletype")
            {
                if (!context.PeopleTypes.Contains(normalizedValue))
                    return false;
                continue;
            }
        }

        return true;
    }

    private static bool TryParsePreReq(string raw, out string kind, out string value)
    {
        kind = string.Empty;
        value = string.Empty;

        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var splitIndex = text.IndexOf(':');
        if (splitIndex <= 0 || splitIndex >= text.Length - 1)
            return false;

        kind = text[..splitIndex].Trim();
        value = text[(splitIndex + 1)..].Trim();
        return kind.Length > 0 && value.Length > 0;
    }

    private sealed record SelectedGeneralAbility(string Name, string AbilityRef, string AbilityKey);

    private sealed record PreReqContext(
        IReadOnlySet<string> Abilities,
        IReadOnlySet<string> Classes,
        IReadOnlySet<string> PeopleTypes);
}
