using System.Text.Json;
using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class BattleboardInnateCalculator
{
    private static readonly Regex LegacyMpInnateRegex = new(
        @"^(?<kind>Spell|Miracle|Evocation)\s*:\s*(?<name>.+?)\s*x(?<count>\d+)\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex UsesPerDayRegex = new(
        @"(?<count>\d+)\s*/\s*day\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex UsesXRegex = new(
        @"\bx(?<count>\d+)\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    public static IReadOnlyList<Item> ResolveAssignedItems(CharacterDraft? draft)
    {
        var character = draft ?? new CharacterDraft();
        try
        {
            return LiteDbService.GetItemsAssignedToCharacter(
                    character.CharacterRecordId,
                    character.Name,
                    character.PlayerName)
                .ToList();
        }
        catch
        {
            return Array.Empty<Item>();
        }
    }

    public static List<InnateAbilityDraft> Calculate(
        CharacterDraft? draft,
        IEnumerable<Item>? assignedItems = null)
    {
        var character = draft ?? new CharacterDraft();
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var innate in character.Innates ?? new List<InnateAbilityDraft>())
            AddInnate(totals, innate?.Name, innate?.Rank ?? 0);

        var items = (assignedItems ?? ResolveAssignedItems(character)).ToList();
        foreach (var item in items)
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            var abilities = payload?.Item?.Abilities ?? new List<CalcResult>();

            foreach (var ability in abilities)
            {
                foreach (var itemInnate in ExtractItemInnates(ability))
                    AddInnate(totals, itemInnate.Name, itemInnate.Rank);
            }
        }

        return totals
            .Where(kvp => kvp.Value > 0)
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new InnateAbilityDraft
            {
                Name = kvp.Key,
                Rank = kvp.Value
            })
            .ToList();
    }

    private static void AddInnate(Dictionary<string, int> totals, string? rawName, int rank)
    {
        var name = NormalizeInnateName(rawName);
        if (name.Length == 0)
            return;

        var safeRank = Math.Max(0, rank);
        if (safeRank == 0)
            return;

        totals[name] = totals.TryGetValue(name, out var existing) ? existing + safeRank : safeRank;
    }

    private static IEnumerable<InnateAbilityDraft> ExtractItemInnates(CalcResult? ability)
    {
        if (ability == null)
            yield break;

        var type = (ability.AbilityType ?? string.Empty).Trim();
        var isTypedCasting =
            type.Equals("Spell", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Miracle", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Evocation", StringComparison.OrdinalIgnoreCase);

        if (isTypedCasting)
        {
            var foundStructured = false;
            foreach (var innate in ExtractTypedCastingInnates(ability, type))
            {
                foundStructured = true;
                yield return innate;
            }

            if (foundStructured)
                yield break;

            if (TryExtractLegacyMpInnate(ability.Summary, out var parsedName, out var parsedCount)
                || TryExtractLegacyMpInnate(ability.AbilityName, out parsedName, out parsedCount))
            {
                yield return new InnateAbilityDraft { Name = parsedName, Rank = parsedCount };
                yield break;
            }

            var fallbackName = NormalizeInnateName(ability.AbilityName);
            var fallbackCount = ResolveInnateUses(ability);
            if (fallbackName.Length > 0 && fallbackCount > 0)
                yield return new InnateAbilityDraft { Name = fallbackName, Rank = fallbackCount };

            yield break;
        }

        if (TryExtractLegacyMpInnate(ability.Summary, out var legacyName, out var legacyCount)
            || TryExtractLegacyMpInnate(ability.AbilityName, out legacyName, out legacyCount))
        {
            yield return new InnateAbilityDraft { Name = legacyName, Rank = legacyCount };
        }
    }

    private static IEnumerable<InnateAbilityDraft> ExtractTypedCastingInnates(CalcResult ability, string type)
    {
        if (type.Equals("Spell", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var innate in ExtractFromTypedCollection(ability, "spells", "spellName"))
                yield return innate;
            yield break;
        }

        if (type.Equals("Miracle", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var innate in ExtractFromTypedCollection(ability, "miracles", "miracleName"))
                yield return innate;
            yield break;
        }

        foreach (var innate in ExtractFromTypedCollection(ability, "evocations", "evocationName"))
            yield return innate;
    }

    private static IEnumerable<InnateAbilityDraft> ExtractFromTypedCollection(
        CalcResult ability,
        string detailKey,
        string nameKey)
    {
        object? raw = null;
        if (!TryGetDetailValue(ability, detailKey, out raw) || raw == null)
        {
            var alternateKey = detailKey.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? detailKey[..^1]
                : $"{detailKey}s";
            if (!TryGetDetailValue(ability, alternateKey, out raw) || raw == null)
                yield break;
        }

        if (raw is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Object)
            {
                if (TryExtractInnate(json, nameKey, out var innate))
                    yield return innate;
                yield break;
            }

            if (json.ValueKind == JsonValueKind.String)
            {
                if (TryExtractInnateFromText(json.GetString(), out var innate))
                    yield return innate;
                yield break;
            }

            if (json.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var entry in json.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    if (TryExtractInnateFromText(entry.GetString(), out var fromText))
                        yield return fromText;
                    continue;
                }

                if (TryExtractInnate(entry, nameKey, out var innate))
                    yield return innate;
            }

            yield break;
        }

        if (raw is string text)
        {
            if (TryExtractInnateFromText(text, out var fromText))
                yield return fromText;
            yield break;
        }

        if (raw is not IEnumerable<object> entries)
        {
            if (TryExtractInnate(raw, nameKey, out var singleEntry))
                yield return singleEntry;
            yield break;
        }

        foreach (var entry in entries)
        {
            if (entry is string stringEntry)
            {
                if (TryExtractInnateFromText(stringEntry, out var fromText))
                    yield return fromText;
                continue;
            }

            if (TryExtractInnate(entry, nameKey, out var innate))
                yield return innate;
        }
    }

    private static bool TryExtractInnate(JsonElement entry, string nameKey, out InnateAbilityDraft innate)
    {
        innate = new InnateAbilityDraft();
        if (entry.ValueKind != JsonValueKind.Object)
            return false;

        var name = NormalizeInnateName(ReadJsonString(entry, nameKey) ?? ReadJsonString(entry, "name"));
        if (name.Length == 0)
            return false;

        var uses = ResolveInnateUses(entry);
        if (uses <= 0)
            return false;

        innate = new InnateAbilityDraft { Name = name, Rank = uses };
        return true;
    }

    private static bool TryExtractInnate(object? entry, string nameKey, out InnateAbilityDraft innate)
    {
        innate = new InnateAbilityDraft();
        var name = NormalizeInnateName(ReadObjectString(entry, nameKey) ?? ReadObjectString(entry, "name"));
        if (name.Length == 0)
            return false;

        var uses = ResolveInnateUses(entry);
        if (uses <= 0)
            return false;

        innate = new InnateAbilityDraft { Name = name, Rank = uses };
        return true;
    }

    private static bool TryExtractInnateFromText(string? text, out InnateAbilityDraft innate)
    {
        innate = new InnateAbilityDraft();
        if (!TryExtractLegacyMpInnate(text, out var name, out var count))
            return false;

        if (name.Length == 0 || count <= 0)
            return false;

        innate = new InnateAbilityDraft { Name = name, Rank = count };
        return true;
    }

    private static int ResolveInnateUses(CalcResult ability)
    {
        var detailCount = 0;
        detailCount += ReadDetailInt(ability, "basicPerDay");
        detailCount += ReadDetailInt(ability, "advancedPerDay");

        if (detailCount <= 0)
            detailCount = Math.Max(0, ReadDetailInt(ability, "count"));
        if (detailCount <= 0)
            detailCount = Math.Max(0, ReadDetailInt(ability, "uses"));
        if (detailCount <= 0)
            detailCount = Math.Max(0, ReadDetailInt(ability, "perDay"));
        if (detailCount > 0)
            return detailCount;

        if (TryExtractUsesFromText(ability.Summary, out var summaryCount))
            return summaryCount;
        if (TryExtractUsesFromText(ability.AbilityName, out var nameCount))
            return nameCount;

        return 0;
    }

    private static int ResolveInnateUses(JsonElement entry)
    {
        var uses = ReadJsonInt(entry, "basicPerDay") + ReadJsonInt(entry, "advancedPerDay");
        if (uses > 0)
            return uses;

        uses = Math.Max(0, ReadJsonInt(entry, "count"));
        if (uses > 0)
            return uses;

        uses = Math.Max(0, ReadJsonInt(entry, "uses"));
        if (uses > 0)
            return uses;

        uses = Math.Max(0, ReadJsonInt(entry, "perDay"));
        if (uses > 0)
            return uses;

        var name = ReadJsonString(entry, "spellName")
                   ?? ReadJsonString(entry, "miracleName")
                   ?? ReadJsonString(entry, "evocationName")
                   ?? ReadJsonString(entry, "name");

        return TryExtractUsesFromText(name, out var parsed) ? parsed : 0;
    }

    private static int ResolveInnateUses(object? entry)
    {
        var uses = ReadObjectInt(entry, "basicPerDay") + ReadObjectInt(entry, "advancedPerDay");
        if (uses > 0)
            return uses;

        uses = Math.Max(0, ReadObjectInt(entry, "count"));
        if (uses > 0)
            return uses;

        uses = Math.Max(0, ReadObjectInt(entry, "uses"));
        if (uses > 0)
            return uses;

        uses = Math.Max(0, ReadObjectInt(entry, "perDay"));
        if (uses > 0)
            return uses;

        var name = ReadObjectString(entry, "spellName")
                   ?? ReadObjectString(entry, "miracleName")
                   ?? ReadObjectString(entry, "evocationName")
                   ?? ReadObjectString(entry, "name");

        return TryExtractUsesFromText(name, out var parsed) ? parsed : 0;
    }

    private static string NormalizeInnateName(string? rawName)
    {
        var value = (rawName ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        if (TryExtractLegacyMpInnate(value, out var parsedName, out _))
            value = parsedName;

        var parenIndex = value.IndexOf(" (", StringComparison.Ordinal);
        if (parenIndex > 0)
            value = value[..parenIndex].Trim();

        if (value.Equals("Spell", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Miracle", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Evocation", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (value.StartsWith("Spell list", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Miracle list", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Evocation list", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return value;
    }

    private static bool TryExtractLegacyMpInnate(string? text, out string name, out int count)
    {
        name = string.Empty;
        count = 0;

        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
            return false;

        var match = LegacyMpInnateRegex.Match(value);
        if (!match.Success)
            return false;

        var rawName = (match.Groups["name"].Value ?? string.Empty).Trim();
        if (!int.TryParse(match.Groups["count"].Value, out count))
            count = 0;

        name = NormalizeInnateName(rawName);
        count = Math.Max(0, count);

        return name.Length > 0 && count > 0;
    }

    private static bool TryExtractUsesFromText(string? text, out int count)
    {
        count = 0;
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
            return false;

        var perDay = UsesPerDayRegex.Match(value);
        if (perDay.Success && int.TryParse(perDay.Groups["count"].Value, out var parsedPerDay))
        {
            count = Math.Max(0, parsedPerDay);
            return count > 0;
        }

        var xCount = UsesXRegex.Match(value);
        if (xCount.Success && int.TryParse(xCount.Groups["count"].Value, out var parsedX))
        {
            count = Math.Max(0, parsedX);
            return count > 0;
        }

        return false;
    }

    private static bool TryGetDetailValue(CalcResult ability, string key, out object? value)
    {
        value = null;
        if (ability?.Details == null || ability.Details.Count == 0)
            return false;

        foreach (var pair in ability.Details)
        {
            if (!pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            value = pair.Value;
            return true;
        }

        return false;
    }

    private static int ReadDetailInt(CalcResult ability, string key)
    {
        if (!TryGetDetailValue(ability, key, out var raw) || raw == null)
            return 0;

        if (raw is int number)
            return number;
        if (raw is long longNumber && longNumber >= int.MinValue && longNumber <= int.MaxValue)
            return (int)longNumber;
        if (raw is JsonElement json)
            return ReadJsonInt(json, null);

        return int.TryParse(raw.ToString(), out var parsed) ? parsed : 0;
    }

    private static int ReadJsonInt(JsonElement element, string? propertyName)
    {
        if (propertyName != null)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(propertyName, out var property))
            {
                return 0;
            }

            return ReadJsonInt(property, null);
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
            return number;

        if (element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static string? ReadJsonString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = (property.GetString() ?? string.Empty).Trim();
        return value.Length > 0 ? value : null;
    }

    private static int ReadObjectInt(object? entry, string propertyName)
    {
        var raw = ReadObjectValue(entry, propertyName);
        if (raw == null)
            return 0;

        if (raw is int number)
            return number;
        if (raw is long longNumber && longNumber >= int.MinValue && longNumber <= int.MaxValue)
            return (int)longNumber;
        if (raw is JsonElement json)
            return ReadJsonInt(json, null);

        return int.TryParse(raw.ToString(), out var parsed) ? parsed : 0;
    }

    private static string? ReadObjectString(object? entry, string propertyName)
    {
        var raw = ReadObjectValue(entry, propertyName);
        if (raw == null)
            return null;

        if (raw is JsonElement json && json.ValueKind == JsonValueKind.String)
        {
            var parsed = (json.GetString() ?? string.Empty).Trim();
            return parsed.Length > 0 ? parsed : null;
        }

        var value = (raw.ToString() ?? string.Empty).Trim();
        return value.Length > 0 ? value : null;
    }

    private static object? ReadObjectValue(object? entry, string propertyName)
    {
        if (entry == null)
            return null;

        if (entry is Dictionary<string, object?> dict)
        {
            return dict.TryGetValue(propertyName, out var raw) ? raw : null;
        }

        var property = entry.GetType().GetProperty(propertyName);
        return property?.GetValue(entry);
    }
}
