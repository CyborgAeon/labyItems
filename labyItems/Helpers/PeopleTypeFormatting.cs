using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Characters;

namespace labyItems.Helpers;

public static class PeopleTypeFormatting
{
    public static List<string> NormalizePeopleTypes(IEnumerable<string>? raw)
    {
        return (raw ?? Array.Empty<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FormatPeopleTypes(IEnumerable<string>? peopleTypes)
    {
        var list = NormalizePeopleTypes(peopleTypes);
        return list.Count == 0 ? string.Empty : string.Join(", ", list);
    }

    public static string SelectPrimaryPeopleType(IReadOnlyList<string> peopleTypes)
    {
        if (peopleTypes == null || peopleTypes.Count == 0)
            return string.Empty;

        var nonDemon = peopleTypes.FirstOrDefault(t => !string.Equals(t, "Demon", StringComparison.OrdinalIgnoreCase));
        return nonDemon ?? peopleTypes[0];
    }

    public static string IconForPeopleType(string peopleType)
    {
        var normalized = (peopleType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "tribal" => "🪓",
            _ when normalized.Contains("magic") => "✨",
            "ishmaic" => "🏜️",
            "baronial" => "🏰",
            _ => "👤"
        };
    }
}
