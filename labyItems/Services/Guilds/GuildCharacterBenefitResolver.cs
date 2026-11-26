using labyItems.Models.Characters;
using labyItems.Models.Guilds;
using labyItems.Models.Rules;

namespace labyItems.Services.Guilds;

public static class GuildCharacterBenefitResolver
{
    public static GuildGrantResolutionResult ResolveGrants(
        GuildRecord record, CharacterDraft draft, string guildName,
        Func<string, IEnumerable<string>?>? resolveContextField = null)
    {
        GuildGrantResolutionContext context;
        lock (draft.GuildSelectionGate)
        {
            var membership = draft.GuildGrantSelections?.GetValueOrDefault(guildName);
            var adventure = draft.AdventureGuildGrantSelections.GetValueOrDefault(guildName);
            var completed = draft.CompletedGuildAdventureMigrations?.GetValueOrDefault(guildName);
            context = new GuildGrantResolutionContext
            {
                GuildName = guildName,
                LegacySelections = new Dictionary<string, int>(
                    draft.GuildBenefitSelections ?? new(), StringComparer.OrdinalIgnoreCase),
                Selections = AsSelections(membership),
                AdventureSelections = AsSelections(adventure),
                CompletedAdventureMigrations = new HashSet<string>(
                    completed ?? [], StringComparer.OrdinalIgnoreCase)
            };
        }

        var result = GuildGrantResolver.Resolve(ActiveNodes(record, draft), context with
        {
            ResolveRuleField = field => resolveContextField?.Invoke(field) ?? ResolveDraftField(draft, guildName, field),
            ResolveChoiceSet = id => record.ChoiceSetTemplates.TryGetValue(id, out var template)
                ? template.Options.Select(option => new GuildGrantOption
                {
                    Id = option.Key,
                    Name = option.Label,
                    Grants = option.Grants.Select(grant => new GuildGrantNode
                    {
                        Id = grant.Ability.Key ?? grant.Ability.AbilityRef ?? option.Key,
                        Ability = grant.Ability
                    }).ToArray()
                }).ToArray()
                : null
        });

        var diagnostics = new List<GuildGrantDiagnostic>();
        if (record.AbilityReferences.Count > 0)
        {
            diagnostics.AddRange(result.Grants
                .Where(grant => grant.Ability.AbilityRef is { Length: > 0 } reference
                                && !record.AbilityReferences.ContainsKey(reference))
                .Select(grant => new GuildGrantDiagnostic("MissingAbilityReference", grant.Id,
                    $"Ability reference '{grant.Ability.AbilityRef}' could not be resolved.")));
        }

        var legacyKeys = context.LegacySelections.Keys.Where(key =>
            key.StartsWith(guildName + "::", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (legacyKeys.Length > 0)
        {
            var knownKeys = record.RawBenefits.Basic.Concat(record.RawBenefits.Intermediate).Concat(record.RawBenefits.Advanced)
                .Select(entry => entry.Node?.LegacySelectionKey).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            diagnostics.AddRange(legacyKeys.Where(key => !knownKeys.Contains(key)).Select(key =>
                new GuildGrantDiagnostic("UnmappedLegacySelection", key, "The saved position has no verified mapping.")));
        }

        return diagnostics.Count == 0 ? result : result with
        {
            Diagnostics = result.Diagnostics.Concat(diagnostics).ToArray()
        };
    }

    public static void ApplyVerifiedMigrations(
        CharacterDraft draft, string guildName, GuildGrantResolutionResult resolution)
    {
        if (resolution.MigratedSelections.Count == 0 && resolution.MigratedAdventureSelections.Count == 0)
            return;

        lock (draft.GuildSelectionGate)
        {
            AddSelections(draft.GuildGrantSelections, resolution.MigratedSelections);
            if (resolution.MigratedAdventureSelections.Count == 0)
                return;

            AddSelections(draft.AdventureGuildGrantSelections, resolution.MigratedAdventureSelections);
            if (!draft.CompletedGuildAdventureMigrations.TryGetValue(guildName, out var completed))
            {
                completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                draft.CompletedGuildAdventureMigrations[guildName] = completed;
            }
            completed.UnionWith(resolution.MigratedAdventureSelections.Keys);
        }

        void AddSelections(
            Dictionary<string, Dictionary<string, List<string>>> target,
            IReadOnlyDictionary<string, IReadOnlyList<string>> migrations)
        {
            if (migrations.Count == 0)
                return;
            if (!target.TryGetValue(guildName, out var selections))
            {
                selections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                target[guildName] = selections;
            }
            foreach (var selection in migrations)
                selections.TryAdd(selection.Key, selection.Value.ToList());
        }
    }

    private static IEnumerable<GuildGrantNode> ActiveNodes(GuildRecord record, CharacterDraft draft)
    {
        if (GuildBenefitTierService.IsTierAvailable(draft, record.Type, GuildBenefitTier.Basic))
            foreach (var node in record.RawBenefits.Basic.Select(entry => entry.Node).OfType<GuildGrantNode>())
                yield return node;
        if (GuildBenefitTierService.IsTierAvailable(draft, record.Type, GuildBenefitTier.Intermediate))
            foreach (var node in record.RawBenefits.Intermediate.Select(entry => entry.Node).OfType<GuildGrantNode>())
                yield return node;
        if (GuildBenefitTierService.IsTierAvailable(draft, record.Type, GuildBenefitTier.Advanced))
            foreach (var node in record.RawBenefits.Advanced.Select(entry => entry.Node).OfType<GuildGrantNode>())
                yield return node;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> AsSelections(Dictionary<string, List<string>>? source)
        => source?.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)entry.Value.ToArray(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string>? ResolveDraftField(CharacterDraft draft, string guildName, string field)
        => GuildsService.NormalizeRuleField(field) switch
        {
            "race" => [draft.Race ?? string.Empty],
            "racesubtype" or "subtype" => [string.IsNullOrWhiteSpace(draft.RaceSubtypeValue) ? draft.RaceSubtype ?? string.Empty : draft.RaceSubtypeValue],
            "class" => [draft.Class ?? string.Empty],
            "guild" or "guilds" => draft.Guilds,
            "outlawhood" or "status" or "statuses" => [draft.Outlawhood.ToString()],
            "adventureguildmemberbracket" when draft.AdventureGuildMemberBrackets != null =>
                draft.AdventureGuildMemberBrackets.GetValueOrDefault(guildName) ?? Array.Empty<string>(),
            "alignmentorder" when draft.Alignment is { } alignment => [alignment.Order.ToString()],
            "alignmentmoral" when draft.Alignment is { } alignment => [alignment.Moral.ToString()],
            "ability" or "abilities" => draft.Abilities.Select(ability => ability.AbilityKey ?? ability.Name),
            _ => null
        };

    public static string GetGrantSource(string guildName, ResolvedGuildGrant grant)
    {
        var source = grant.Patches.Select(patch => patch.Overrides?.Source).LastOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? grant.Ability.Overrides?.Source ?? grant.Ability.Source;
        return string.IsNullOrWhiteSpace(source) || source.Equals("Guild", StringComparison.OrdinalIgnoreCase)
            ? $"Guild:{guildName}"
            : source;
    }

    public static AbilityDefinition MaterializeGrant(GuildRecord record, ResolvedGuildGrant grant)
    {
        var ability = GuildsService.NormalizeAbility(grant.Ability, record.AbilityReferences);
        foreach (var patch in grant.Patches)
        {
            if (!string.IsNullOrWhiteSpace(patch.DescriptionRef)
                && !string.Equals(ability.AbilityRef, patch.DescriptionRef, StringComparison.OrdinalIgnoreCase))
            {
                ability.RuleReferences = (ability.RuleReferences ?? [])
                    .Append(new RuleReference(RuleCatalogueKind.Ability, patch.DescriptionRef, RuleRelationship.References)).ToArray();
            }
            if (patch.Overrides != null)
                GuildsService.ApplyGrantOverrides(ability, patch.Overrides);
            if (patch.Amount is { Count: > 0 })
                ability.Amount = patch.Amount.ToList();
            if (patch.Count.HasValue)
                ability.Count = patch.Count;
            if (patch.CountDelta.HasValue)
                ability.Count = Math.Max(0, (ability.Count ?? 0) + patch.CountDelta.Value);
        }
        ability.GrantId = grant.InstanceId;
        return ability;
    }

    public static AbilityDefinition MaterializeGrantOption(GuildRecord record, GuildGrantNode grant)
        => GuildsService.NormalizeAbility(grant.Ability, record.AbilityReferences);
}
