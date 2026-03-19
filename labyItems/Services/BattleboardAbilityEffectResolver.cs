using labyItems.Models.Characters;
using labyItems.Services.AbilityEffects;

namespace labyItems.Services;

public static class BattleboardAbilityEffectResolver
{
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
            infiniteResistanceTypes);
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
        var immunities = new HashSet<string>(fallback.Immunities, StringComparer.OrdinalIgnoreCase);

        if (abilityList.Count == 0)
            return new BattleboardAdvancementEffects(resistance, immunities.ToList(), multipliers, infiniteResistanceTypes);

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
            infiniteResistanceTypes);
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
}
