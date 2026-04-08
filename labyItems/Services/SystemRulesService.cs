using System.Text.Json;

namespace labyItems.Services;

public sealed class SystemRuleRecord
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string>? Aliases { get; set; }
}

public static class SystemRulesService
{
    private const string RulesPath = "system_rules/system-rules.json";
    private static readonly SemaphoreSlim LookupLock = new(1, 1);
    private static IReadOnlyDictionary<string, SystemRuleRecord>? _lookup;

    public static void InvalidateCache()
    {
        _lookup = null;
    }

    public static async Task<SystemRuleRecord?> FindAsync(string? keyOrName)
    {
        var lookup = await GetLookupAsync();
        foreach (var candidate in BuildCandidateKeys(keyOrName))
        {
            if (lookup.TryGetValue(candidate, out var matched))
                return matched;
        }

        return null;
    }

    public static async Task<IReadOnlyDictionary<string, SystemRuleRecord>> GetLookupAsync()
    {
        if (_lookup != null)
            return _lookup;

        await LookupLock.WaitAsync();
        try
        {
            if (_lookup != null)
                return _lookup;

            var map = new Dictionary<string, SystemRuleRecord>(StringComparer.OrdinalIgnoreCase);
            var json = await ServiceHelper.ReadPackageTextAsync(RulesPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _lookup = map;
                return _lookup;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                _lookup = map;
                return _lookup;
            }

            if (!TryGetProperty(root, out var rulesElement, "rules", "Rules")
                || rulesElement.ValueKind != JsonValueKind.Object)
            {
                rulesElement = root;
            }

            foreach (var entry in rulesElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object)
                    continue;

                var key = ReadStringProperty(entry.Value, "Key");
                if (key.Length == 0)
                    key = entry.Name;

                var name = ReadStringProperty(entry.Value, "Name", "Title");
                if (name.Length == 0)
                    name = key;

                var description = ReadStringProperty(entry.Value, "Description", "Text", "Body");
                if (description.Length == 0)
                    continue;

                var aliases = ReadStringArrayProperty(entry.Value, "Aliases");

                var rule = new SystemRuleRecord
                {
                    Key = key,
                    Name = name,
                    Description = description,
                    Aliases = aliases.Count == 0 ? null : aliases
                };

                AddLookup(map, rule, key);
                AddLookup(map, rule, name);
                foreach (var alias in aliases)
                    AddLookup(map, rule, alias);
            }

            _lookup = map;
            return _lookup;
        }
        catch
        {
            _lookup = new Dictionary<string, SystemRuleRecord>(StringComparer.OrdinalIgnoreCase);
            return _lookup;
        }
        finally
        {
            LookupLock.Release();
        }
    }

    private static void AddLookup(
        IDictionary<string, SystemRuleRecord> map,
        SystemRuleRecord rule,
        string? rawKey)
    {
        var normalized = NormalizeKey(rawKey);
        if (normalized.Length == 0 || map.ContainsKey(normalized))
            return;

        map[normalized] = rule;
    }

    private static IEnumerable<string> BuildCandidateKeys(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            yield break;

        yield return NormalizeKey(text);

        if (text.StartsWith("$", StringComparison.Ordinal))
            yield return NormalizeKey(text[1..]);

        var strippedPrefixes = new[]
        {
            "$system-rule.",
            "$systemrule.",
            "system-rule.",
            "system-rule:",
            "systemrule.",
            "systemrule:",
            "rule.",
            "rule:"
        };

        foreach (var prefix in strippedPrefixes)
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var stripped = text[prefix.Length..].Trim();
            if (stripped.Length > 0)
                yield return NormalizeKey(stripped);
        }
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string ReadStringProperty(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var value, propertyNames)
            || value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return (value.GetString() ?? string.Empty).Trim();
    }

    private static List<string> ReadStringArrayProperty(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, out var value, propertyName)
            || value.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => (item.GetString() ?? string.Empty).Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] propertyNames)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out value))
                return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            foreach (var name in propertyNames)
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = property.Value;
                return true;
            }
        }

        return false;
    }
}
