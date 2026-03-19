using System.Collections.Generic;
using System.Linq;
using labyItems.Models;
using labyItems.Services;

namespace labyItems.Helpers;

public static class ItemDisplayHelper
{
    public static ItemJsonPayload? TryGetPayload(Item? item)
        => item == null ? null : ItemEmailService.TryDeserializeItemPayload(item.PayloadJson);

    public static List<CalcResult> GetAbilities(Item? item)
    {
        var payload = TryGetPayload(item);
        return payload?.Item?.Abilities?
            .Where(ability => ability != null)
            .ToList() ?? new List<CalcResult>();
    }

    public static bool IsMonsterPointItem(Item? item)
    {
        var source = (TryGetPayload(item)?.Item?.SourceFlow ?? string.Empty).Trim();
        return source.Equals("monster-point", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetStoredPhysicalRep(Item? item)
        => (TryGetPayload(item)?.Item?.PhysicalRepresentation ?? string.Empty).Trim();

    public static string GetAutoPhysicalRep(IEnumerable<CalcResult>? abilities)
    {
        var list = (abilities ?? Enumerable.Empty<CalcResult>()).ToList();
        if (list.Any(IsShieldAbility))
            return "shield";
        if (list.Any(IsArmourAbility))
            return "armour";
        if (list.Any(IsWeaponAbility))
            return "weapon";
        return string.Empty;
    }

    public static bool RequiresPhysicalRepPrompt(IEnumerable<CalcResult>? abilities)
        => GetAutoPhysicalRep(abilities).Length == 0;

    public static string BuildDisplayName(Item? item)
    {
        var payload = TryGetPayload(item);
        var stored = (payload?.Item?.DisplayName ?? string.Empty).Trim();
        if (stored.Length > 0)
            return stored;

        return BuildDisplayName(
            payload?.Item?.Abilities ?? Enumerable.Empty<CalcResult>(),
            payload?.Item?.PhysicalRepresentation);
    }

    public static string BuildDisplayName(IEnumerable<CalcResult>? abilities, string? physicalRep)
    {
        var list = (abilities ?? Enumerable.Empty<CalcResult>()).ToList();
        var life = GetLifeDescriptor(list);
        var rep = (physicalRep ?? string.Empty).Trim();
        if (rep.Length == 0)
            rep = GetAutoPhysicalRep(list);

        var parts = new List<string>();
        if (life.Length > 0)
            parts.Add($"{life} life");
        if (rep.Length > 0)
            parts.Add(rep.ToLowerInvariant());

        if (parts.Count == 0)
        {
            var fallback = GetFirstMeaningfulAbilityName(list);
            if (fallback.Length > 0)
                parts.Add(fallback);
        }

        if (parts.Count == 0)
            parts.Add("item");

        return string.Join(" ", parts).Trim();
    }

    public static string GetLifeDescriptor(IEnumerable<CalcResult>? abilities)
    {
        foreach (var ability in abilities ?? Enumerable.Empty<CalcResult>())
        {
            if (ability == null)
                continue;

            var type = (ability.AbilityType ?? string.Empty).Trim();
            if (!type.Equals("Life", StringComparison.OrdinalIgnoreCase))
                continue;

            var key = (ability.AbilityName ?? string.Empty).Trim();
            if (key.Length == 0 && ability.Details.TryGetValue("life", out var lifeValue))
                key = (lifeValue?.ToString() ?? string.Empty).Trim();

            if (key.Length == 0 || key.StartsWith("No additional life", StringComparison.OrdinalIgnoreCase))
                continue;

            return key;
        }

        return string.Empty;
    }

    private static bool IsWeaponAbility(CalcResult ability)
        => IsType(ability, "Weapon");

    private static bool IsShieldAbility(CalcResult ability)
        => IsType(ability, "Shield");

    private static bool IsArmourAbility(CalcResult ability)
        => IsType(ability, "Armour") || IsType(ability, "Armor");

    private static bool IsType(CalcResult ability, string type)
        => (ability.AbilityType ?? string.Empty).Trim().Equals(type, StringComparison.OrdinalIgnoreCase);

    private static string GetFirstMeaningfulAbilityName(IEnumerable<CalcResult> abilities)
    {
        foreach (var ability in abilities)
        {
            var type = (ability.AbilityType ?? string.Empty).Trim();
            if (type.Length == 0
                || type.Equals("Base", StringComparison.OrdinalIgnoreCase)
                || type.Equals("More", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = (ability.AbilityName ?? string.Empty).Trim();
            if (name.Length > 0)
                return name.ToLowerInvariant();
        }

        return string.Empty;
    }
}
