using labyItems.Services;
using labyItems.Models.Characters;

namespace labyItems.Models.Abilities;

public static class AbilityKey
{
    // Canonical format:
    // - Prefer stable ability identifiers from JSON data (e.g. `AbilityRef` / `AbilityDefinition.Key`).
    // - Fallback for evolution-based abilities: `{table}|{idx_lower}`.
    public static string Build(EvolutionService.AbilityResult? ability)
    {
        if (ability == null)
            return string.Empty;

        var abilityRef = (ability.AbilityRef ?? string.Empty).Trim();
        if (abilityRef.Length > 0)
            return abilityRef;

        return BuildEvolutionFallback(ability);
    }

    // Canonical key resolution for ability-definition payloads (race/class/guild/multi-race data).
    // Preference order:
    // 1) definition `Key`
    // 2) definition `AbilityRef`
    // 3) normalized ability name (last-resort fallback)
    public static string Build(AbilityDefinition? ability)
    {
        if (ability == null)
            return string.Empty;

        var key = (ability.Key ?? string.Empty).Trim();
        if (key.Length > 0)
            return key;

        var abilityRef = (ability.AbilityRef ?? string.Empty).Trim();
        if (abilityRef.Length > 0)
            return abilityRef;

        var name = (ability.Name ?? string.Empty).Trim().ToLowerInvariant();
        return name;
    }

    // Backwards-compatible fallback for earlier key formats.
    // Format: `{table}|{idx_lower}`
    public static string BuildEvolutionFallback(EvolutionService.AbilityResult? ability)
    {
        if (ability == null)
            return string.Empty;

        var table = Math.Max(0, ability.Table);
        var idxLower = (ability.Index ?? string.Empty).Trim().ToLowerInvariant();
        if (idxLower.Length == 0)
            return string.Empty;

        return $"{table}|{idxLower}";
    }

    public static string Normalize(string? key)
        => (key ?? string.Empty).Trim();

    public static bool IsNullOrWhiteSpace(string? key)
        => string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(Normalize(key));
}
