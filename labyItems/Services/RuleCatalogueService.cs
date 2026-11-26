using System.Text.Json;
using labyItems.Models.Rules;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class RuleCatalogueService
{
    private static readonly SemaphoreSlim LoadLock = new(1, 1);
    private static RuleReferenceResolver? _resolver;

    internal static void InvalidateCache() => Volatile.Write(ref _resolver, null);

    public static Task<RuleResolution> ResolveAbilityAsync(string identifier, IReadOnlyList<RuleReference>? effectiveReferences = null)
        => ResolveAsync(RuleCatalogueKind.Ability, identifier, effectiveReferences);

    public static async Task<RuleResolution> ResolveAsync(
        RuleCatalogueKind catalogue, string identifier, IReadOnlyList<RuleReference>? effectiveReferences = null)
    {
        var resolver = await GetResolverAsync().ConfigureAwait(false);
        return resolver.Resolve(catalogue, identifier, effectiveReferences: effectiveReferences);
    }

    private static async Task<RuleReferenceResolver> GetResolverAsync()
    {
        var session = LabyrintheSession.Shared;
        var version = session.Version;
        if (Volatile.Read(ref _resolver) is { } resolver)
            return resolver;

        await LoadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            session.RequireVersion(version);
            if (_resolver is { } cached)
                return cached;

            var catalogue = await SpecialisationDefinitionRepository.GetIndexAsync().ConfigureAwait(false);
            var definitions = new List<RuleDefinition>();
            var aliases = new List<(RuleCatalogueKind, string, string)>();
            var canonicalAbilities = catalogue.AbilityReferences.Values
                .Where(ability => !string.IsNullOrWhiteSpace(ability.Key))
                .DistinctBy(ability => ability.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var ability in canonicalAbilities)
            {
                definitions.Add(new(RuleCatalogueKind.Ability, ability.Key!, ability.Name, ability.Effect,
                    ability.Source, ability.RuleReferences ?? [], ability.Family));
                if (!string.IsNullOrWhiteSpace(ability.Name))
                    aliases.Add((RuleCatalogueKind.Ability, ability.Name, ability.Key!));
            }

            foreach (var (alias, ability) in catalogue.AbilityReferences)
            {
                if (!string.IsNullOrWhiteSpace(ability.Key))
                    aliases.Add((RuleCatalogueKind.Ability, alias, ability.Key));
            }

            var scoutSkills = catalogue.ChoiceSetTemplates.Values
                .Where(choice => choice.StrategyIds.Contains("lookup:scout-skill", StringComparer.OrdinalIgnoreCase))
                .SelectMany(choice => choice.Options)
                .SelectMany(option => option.Grants.Select(grant => (Option: option, grant.Ability)))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Ability.AbilityRef ?? entry.Ability.Key))
                .DistinctBy(entry => entry.Ability.AbilityRef ?? entry.Ability.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var (option, ability) in scoutSkills)
            {
                var identifier = (ability.AbilityRef ?? ability.Key)!;
                definitions.Add(new(RuleCatalogueKind.ScoutSkill, identifier, option.Label,
                    ability.Effect ?? option.Description, ability.Source, ability.RuleReferences ?? [], "scout-skill"));
                aliases.Add((RuleCatalogueKind.ScoutSkill, option.Key, identifier));
                aliases.Add((RuleCatalogueKind.ScoutSkill, option.Label, identifier));
            }

            var json = await ServiceHelper.ReadPackageTextAsync("system_rules/system-rules.json").ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.GetProperty("$schemaVersion").GetInt32() != 1)
                throw new InvalidDataException("Unsupported system rule catalogue version.");
            var overrides = ReadReferenceOverlays(document.RootElement);

            foreach (var definition in await ReferenceCatalogueDataSynchronizer.ReadPackagedRulesAsync().ConfigureAwait(false))
            {
                var key = (definition.Catalogue, definition.Identifier);
                if (overrides.Remove(key, out var overlay))
                {
                    definitions.Add(definition with { References = overlay.References });
                    foreach (var alias in overlay.Aliases)
                        aliases.Add((definition.Catalogue, alias, definition.Identifier));
                }
                else
                {
                    definitions.Add(definition);
                }

                aliases.Add((definition.Catalogue, definition.Name, definition.Identifier));
            }

            if (overrides.Count > 0)
                throw new InvalidDataException($"Missing packaged rule for overlay {overrides.Keys.First()}.");

            foreach (var property in document.RootElement.GetProperty("rules").EnumerateObject())
            {
                var entry = property.Value;
                var kind = entry.TryGetProperty("Catalogue", out var category)
                    ? category.Deserialize<RuleCatalogueKind>()
                    : RuleCatalogueKind.SystemRule;
                definitions.Add(new(kind, property.Name, entry.GetProperty("Name").GetString()!,
                    ReadText(entry, "Description"), ReadText(entry, "Source"), ReadReferences(entry)));
                foreach (var alias in ReadAliases(entry))
                    aliases.Add((kind, alias, property.Name));
            }

            resolver = new RuleReferenceResolver(new(definitions, aliases));
            session.Publish(version, () => Volatile.Write(ref _resolver, resolver));
            return resolver;
        }
        finally
        {
            LoadLock.Release();
        }
    }

    private static Dictionary<(RuleCatalogueKind, string), (IReadOnlyList<RuleReference> References, string[] Aliases)>
        ReadReferenceOverlays(JsonElement root)
    {
        var result = new Dictionary<(RuleCatalogueKind, string), (IReadOnlyList<RuleReference>, string[])>();
        if (!root.TryGetProperty("referenceOverlays", out var overlays))
            return result;

        foreach (var group in overlays.EnumerateObject())
        {
            var kind = Enum.Parse<RuleCatalogueKind>(group.Name);
            foreach (var property in group.Value.EnumerateObject())
                result.Add((kind, property.Name), (ReadReferences(property.Value), ReadAliases(property.Value)));
        }

        return result;
    }

    private static IReadOnlyList<RuleReference> ReadReferences(JsonElement entry)
        => entry.TryGetProperty("RuleReferences", out var references)
            ? references.Deserialize<RuleReference[]>() ?? []
            : [];

    private static string[] ReadAliases(JsonElement entry)
        => entry.TryGetProperty("Aliases", out var aliases) ? aliases.Deserialize<string[]>() ?? [] : [];

    private static string? ReadText(JsonElement entry, string name)
        => entry.TryGetProperty(name, out var text) ? text.GetString() : null;
}
