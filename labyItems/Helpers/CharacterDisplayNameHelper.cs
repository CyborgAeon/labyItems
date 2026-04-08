using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Characters;
using labyItems.Models.Enums;

namespace labyItems.Helpers;

public static class CharacterDisplayNameHelper
{
    public static string BuildClassDisplayName(CharacterDraft draft)
    {
        var cls = (draft?.Class ?? string.Empty).Trim();
        if (cls.Length == 0)
            return cls;

        if (!IsWizardClassName(cls))
            return cls;

        var colour = draft.TryGetWizardColour();
        if (!colour.HasValue)
            return cls;

        var colourName = colour.Value.ToString();
        if (cls.StartsWith(colourName, StringComparison.OrdinalIgnoreCase))
            return cls;

        return $"{colourName} {cls}";
    }

    private static bool IsWizardClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;

        return className.Equals("Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("High-Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("High Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("Warlock", StringComparison.OrdinalIgnoreCase)
               || className.Equals("Rogue", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildRaceDisplayName(CharacterDraft draft, IEnumerable<AbilityDraft> abilities)
    {
        var race = (draft?.Race ?? string.Empty).Trim();
        var suffixes = new List<string>();

        var subtype = (draft?.RaceSubtypeValue ?? draft?.RaceSubtype ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(subtype) && !string.Equals(subtype, "Standard", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = TrimSubtypeLabel(subtype);
            if (!string.IsNullOrWhiteSpace(trimmed))
                suffixes.Add(trimmed);
        }

        if (string.Equals(race, "Faerie", StringComparison.OrdinalIgnoreCase))
        {
            var faerieColours = GetFaerieColourSelections(abilities);
            foreach (var colour in faerieColours)
            {
                if (!suffixes.Any(s => string.Equals(s, colour, StringComparison.OrdinalIgnoreCase)))
                    suffixes.Add(colour);
            }
        }

        if (suffixes.Count == 0)
            return race;

        if (string.IsNullOrWhiteSpace(race))
            return string.Join(", ", suffixes);

        return $"{race} ({string.Join(", ", suffixes)})";
    }

    private static List<string> GetFaerieColourSelections(IEnumerable<AbilityDraft> abilities)
    {
        return (abilities ?? Array.Empty<AbilityDraft>())
            .Where(IsFaerieColourSelection)
            .Select(a => (a?.Name ?? string.Empty).Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsFaerieColourSelection(AbilityDraft ability)
    {
        var source = (ability?.Source ?? string.Empty).Trim();
        return string.Equals(source, "Specialisation:Faerie Colour", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimSubtypeLabel(string subtype)
    {
        var value = (subtype ?? string.Empty).Trim();
        if (value.Length == 0)
            return value;

        var parenIndex = value.IndexOf('(');
        if (parenIndex >= 0)
            value = value[..parenIndex].Trim();

        if (value.Length == 0)
            return value;

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return value;

        var end = parts.Length;
        while (end > 1 && IsAllLower(parts[end - 1]))
            end--;

        return string.Join(' ', parts.Take(end));
    }

    private static bool IsAllLower(string token)
    {
        var hasLetter = false;
        foreach (var ch in token)
        {
            if (!char.IsLetter(ch))
                continue;

            hasLetter = true;
            if (!char.IsLower(ch))
                return false;
        }

        return hasLetter;
    }
}
