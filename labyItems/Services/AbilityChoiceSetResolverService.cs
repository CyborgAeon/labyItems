using System.Text.Json;
using labyItems.Models.Abilities;
using labyItems.Models.Characters;

namespace labyItems.Services;

public interface IAbilityChoiceSetResolverService
{
    Task<IReadOnlyList<string>> ResolveChoiceSetRefsAsync(
        EvolutionService.AbilityResult? ability,
        CancellationToken cancellationToken = default);
}

public sealed class AbilityChoiceSetResolverService : IAbilityChoiceSetResolverService
{
    private static readonly SemaphoreSlim ClassChoiceSetLookupLock = new(1, 1);
    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? _classChoiceSetRefsByAbilityToken;

    public async Task<IReadOnlyList<string>> ResolveChoiceSetRefsAsync(
        EvolutionService.AbilityResult? ability,
        CancellationToken cancellationToken = default)
    {
        if (ability == null)
            return Array.Empty<string>();

        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var choiceSetRef in ability.ChoiceSetRefs)
            AddChoiceSetRef(refs, choiceSetRef);

        var definitionLookup = await AbilityDefinitionLookupService.GetLookupAsync();
        var definition = ResolveDefinition(definitionLookup, ability);
        if (definition != null)
        {
            AddChoiceSetRef(refs, definition.ChoiceSetRef);
            foreach (var choiceSetRef in definition.ChoiceSetRefs ?? new List<string>())
                AddChoiceSetRef(refs, choiceSetRef);
        }

        var classChoiceSetLookup = await GetClassChoiceSetRefLookupAsync(cancellationToken);
        foreach (var token in EnumerateLookupTokens(ability))
        {
            if (!classChoiceSetLookup.TryGetValue(token, out var mappedRefs))
                continue;

            foreach (var mapped in mappedRefs)
                AddChoiceSetRef(refs, mapped);
        }

        return refs.Count == 0
            ? Array.Empty<string>()
            : refs.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static AbilityDefinition? ResolveDefinition(
        IReadOnlyDictionary<string, AbilityDefinition> lookup,
        EvolutionService.AbilityResult ability)
    {
        var candidates = new[]
        {
            ability.AbilityRef,
            AbilityKey.Build(ability),
            ability.Index,
            AbilityKey.BuildEvolutionFallback(ability)
        };

        foreach (var candidate in candidates)
        {
            var resolved = AbilityDefinitionLookupService.Find(lookup, candidate);
            if (resolved != null)
                return resolved;
        }

        return null;
    }

    private static void AddChoiceSetRef(ISet<string> target, string? choiceSetRef)
    {
        var trimmed = (choiceSetRef ?? string.Empty).Trim();
        if (trimmed.Length > 0)
            target.Add(trimmed);
    }

    private static IEnumerable<string> EnumerateLookupTokens(EvolutionService.AbilityResult ability)
    {
        var tokens = new[]
        {
            ability.AbilityRef,
            ability.Index,
            AbilityKey.Build(ability),
            AbilityKey.BuildEvolutionFallback(ability)
        };

        foreach (var token in tokens)
        {
            var normalized = NormalizeChoiceSetLookupToken(token);
            if (normalized.Length > 0)
                yield return normalized;
        }
    }

    private static string NormalizeChoiceSetLookupToken(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.EndsWith(" skill", StringComparison.OrdinalIgnoreCase))
            text = text[..^6].Trim();

        return new string(text
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetClassChoiceSetRefLookupAsync(
        CancellationToken cancellationToken)
    {
        if (_classChoiceSetRefsByAbilityToken != null)
            return _classChoiceSetRefsByAbilityToken;

        await ClassChoiceSetLookupLock.WaitAsync(cancellationToken);
        try
        {
            if (_classChoiceSetRefsByAbilityToken != null)
                return _classChoiceSetRefsByAbilityToken;

            _classChoiceSetRefsByAbilityToken = await BuildClassChoiceSetRefLookupAsync();
            return _classChoiceSetRefsByAbilityToken;
        }
        finally
        {
            ClassChoiceSetLookupLock.Release();
        }
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> BuildClassChoiceSetRefLookupAsync()
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("specialisation/class-specialisations.json");
            if (string.IsNullOrWhiteSpace(json))
                return map;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("definitions", out var definitions)
                || definitions.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var definition in definitions.EnumerateObject())
            {
                if (definition.Value.ValueKind != JsonValueKind.Object
                    || !TryReadChoiceSetRefs(definition.Value, out var choiceSetRefs))
                {
                    continue;
                }

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
