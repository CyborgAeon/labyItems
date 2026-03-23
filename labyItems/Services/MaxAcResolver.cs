using System.Text.Json;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class MaxAcResolver
{
    public static async Task<int> ResolveFromClassAndAdvancementsAsync(
        int classBaseMaxAc,
        IEnumerable<string>? advancementAbilityKeysOrNames)
    {
        var baseMaxAc = Math.Max(0, classBaseMaxAc);
        if (baseMaxAc <= 0)
            return 0;

        var increment = await ResolveIncrementAsync(advancementAbilityKeysOrNames);
        return Math.Max(0, baseMaxAc + increment);
    }

    public static async Task<int> ResolveEffectiveForDraftAsync(
        CharacterDraft? draft)
    {
        if (draft == null)
            return 0;

        var draftMaxAc = Math.Max(0, draft.MaxAC);
        var classBaseMaxAc = await ResolveClassBaseMaxAcAsync(draft.Class);
        if (classBaseMaxAc <= 0)
            return draftMaxAc;

        var resolvedFromData = await ResolveFromClassAndAdvancementsAsync(
            classBaseMaxAc,
            draft.AdvancementAbilities);

        // Preserve already-computed draft values if they are higher than the reconstructed baseline.
        return Math.Max(draftMaxAc, resolvedFromData);
    }

    public static async Task<int> ResolveIncrementAsync(IEnumerable<string>? advancementAbilityKeysOrNames)
    {
        var lookup = await AbilityDetailsLookupService.GetLookupAsync();
        return ResolveIncrementFromLookup(advancementAbilityKeysOrNames, lookup);
    }

    public static int ResolveIncrementFromCachedLookup(IEnumerable<string>? advancementAbilityKeysOrNames)
    {
        if (!AbilityDetailsLookupService.TryGetLookup(out var lookup))
            return 0;

        return ResolveIncrementFromLookup(advancementAbilityKeysOrNames, lookup);
    }

    public static int ResolveIncrementFromLookup(
        IEnumerable<string>? advancementAbilityKeysOrNames,
        IReadOnlyDictionary<string, EvolutionService.AbilityResult>? lookup)
    {
        if (lookup == null || lookup.Count == 0)
            return 0;

        var total = 0;
        foreach (var raw in advancementAbilityKeysOrNames ?? Array.Empty<string>())
        {
            var token = (raw ?? string.Empty).Trim();
            if (token.Length == 0)
                continue;

            var ability = AbilityDetailsLookupService.FindByIndex(lookup, token);
            if (ability == null)
                continue;

            total += Math.Max(0, ability.MaxAcIncrease);
        }

        return total;
    }

    private static async Task<int> ResolveClassBaseMaxAcAsync(
        string? className)
    {
        var normalizedClassName = (className ?? string.Empty).Trim();
        if (normalizedClassName.Length == 0)
            return 0;

        try
        {
            var classes = await ClassService.GetAllAsync();
            if (!TryGetClassRecordByName(classes, normalizedClassName, out var classRecord))
                return 0;

            return ParseInt(classRecord.MaxAC);
        }
        catch
        {
            return 0;
        }
    }

    private static bool TryGetClassRecordByName(
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        string className,
        out CharacterClassRecord classRecord)
    {
        classRecord = null!;
        if (classes == null || string.IsNullOrWhiteSpace(className))
            return false;

        if (classes.TryGetValue(className, out var direct) && direct != null)
        {
            classRecord = direct;
            return true;
        }

        foreach (var entry in classes)
        {
            if (!string.Equals(entry.Key, className, StringComparison.OrdinalIgnoreCase))
                continue;

            if (entry.Value == null)
                return false;

            classRecord = entry.Value;
            return true;
        }

        return false;
    }

    private static int ParseInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var asNumber))
            return Math.Max(0, asNumber);

        if (element.ValueKind == JsonValueKind.String)
        {
            var token = (element.GetString() ?? string.Empty).Trim();
            if (int.TryParse(token, out var asText))
                return Math.Max(0, asText);
        }

        return 0;
    }
}
