using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Helpers;

public static class WizardSpellRules
{
    public static List<string> ParseWizardSelections(string? raw)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return list;

        var parts = raw.Split(new[] { '|', ',', ';', '/', '&' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var normalized = NormalizeWizardSelection(part);
            if (normalized.Length > 0 && seen.Add(normalized))
                list.Add(normalized);
        }

        if (list.Count > 0)
            return list;

        var fallback = NormalizeWizardSelection(raw);
        if (fallback.Length > 0)
            list.Add(fallback);

        return list;
    }

    public static string NormalizeWizardSelection(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        var compact = value.Replace(" ", string.Empty);
        if (Enum.TryParse<MagicColours>(compact, true, out var colour))
            return colour.ToString();

        if (Enum.TryParse<VivomancerColours>(compact, true, out var vivoColour))
            return vivoColour.ToString();

        if (Enum.TryParse<ExtendedMagicColours>(compact, true, out var extended) && extended == ExtendedMagicColours.Sorcorial)
            return "Sorcorial";

        foreach (var magic in Enum.GetValues<MagicColours>())
        {
            var token = magic.ToString();
            if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
                return token;
        }

        if (value.Contains("Sorc", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Soc", StringComparison.OrdinalIgnoreCase))
            return "Sorcorial";

        return value;
    }

    public static bool SpellMatchesAnyWizardSelection(string? rawColour, IReadOnlyList<string> selections)
    {
        if (selections == null || selections.Count == 0)
            return false;

        foreach (var selection in selections)
        {
            if (SpellMatchesWizardSelection(rawColour, selection))
                return true;
        }

        return false;
    }

    public static bool SpellMatchesWizardSelection(string? rawColour, string selection)
    {
        if (string.IsNullOrWhiteSpace(rawColour) || string.IsNullOrWhiteSpace(selection))
            return false;

        var trimmedSelection = NormalizeWizardSelection(selection);
        if (trimmedSelection.Length == 0)
            trimmedSelection = selection.Trim();

        var selectionIsElemental = IsElementalSelection(trimmedSelection);
        var selectionIsSorcorial = IsSorcorialSelection(trimmedSelection);
        var selectionColour = TryParseMagicColour(trimmedSelection, out var parsed) ? parsed : (MagicColours?)null;

        var parts = rawColour.Split(new[] { '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (SpellColourPartMatches(part, selectionColour, selectionIsElemental, selectionIsSorcorial))
                return true;
        }

        return SpellColourPartMatches(rawColour, selectionColour, selectionIsElemental, selectionIsSorcorial);
    }

    public static bool TryParseMagicColour(string? value, out MagicColours colour)
    {
        colour = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace(" ", string.Empty);
        return Enum.TryParse(normalized, ignoreCase: true, out colour);
    }

    public static bool TryExtractSingleMagicColour(string? rawColour, out MagicColours colour)
    {
        colour = default;
        var tokens = TokenizeColour(rawColour);
        if (tokens.Count != 1)
            return false;

        return TryParseMagicColour(tokens[0], out colour);
    }

    public static bool IsAllOrAnyColour(string? rawColour)
    {
        if (string.IsNullOrWhiteSpace(rawColour))
            return false;

        var tokens = TokenizeColour(rawColour);
        return tokens.Any(t => t == "all" || t == "any");
    }

    public static bool IsGreyOrAllBonusSpell(SpellService.SpellRaw? spell)
    {
        if (spell == null || spell.level > 8)
            return false;

        if (SpellMatchesWizardSelection(spell.colour, MagicColours.Grey.ToString()))
            return true;

        return IsAllOrAnyColour(spell.colour);
    }

    public static List<SpellListEntryDraft> BuildBaseSpellEntries(
        IReadOnlyList<SpellService.SpellRaw> allSpells,
        IReadOnlyList<string> selectedColours,
        bool includeGreyBonus)
    {
        var uniqueByName = new Dictionary<string, SpellService.SpellRaw>(StringComparer.OrdinalIgnoreCase);
        foreach (var spell in allSpells ?? Array.Empty<SpellService.SpellRaw>())
        {
            if (spell == null || string.IsNullOrWhiteSpace(spell.name))
                continue;

            if ((spell.isAdvanced ?? false) == true)
                continue;

            var includeBySelection = selectedColours.Count == 0
                                     || SpellMatchesAnyWizardSelection(spell.colour, selectedColours);
            if (!includeBySelection && (!includeGreyBonus || !IsGreyOrAllBonusSpell(spell)))
                continue;

            var key = spell.name.Trim();
            if (!uniqueByName.ContainsKey(key))
                uniqueByName[key] = spell;
        }

        return uniqueByName.Values
            .OrderBy(s => s.level)
            .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new SpellListEntryDraft
            {
                Name = s.name ?? string.Empty,
                Level = s.level,
                Colour = ResolveListEntryColour(s.colour, selectedColours),
                IsAdvanced = s.isAdvanced ?? false
            })
            .ToList();
    }

    private static string ResolveListEntryColour(string? rawColour, IReadOnlyList<string> selectedColours)
    {
        if (TryExtractSingleMagicColour(rawColour, out var single))
            return single.ToString();

        var normalizedSelections = (selectedColours ?? Array.Empty<string>())
            .Select(NormalizeWizardSelection)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (normalizedSelections.Count == 0)
            return (rawColour ?? string.Empty).Trim();

        foreach (var selection in normalizedSelections)
        {
            if (TryParseMagicColour(selection, out var parsed)
                && SpellMatchesWizardSelection(rawColour, parsed.ToString()))
            {
                return parsed.ToString();
            }
        }

        foreach (var selection in normalizedSelections)
        {
            if (SpellMatchesWizardSelection(rawColour, selection))
                return selection;
        }

        return (rawColour ?? string.Empty).Trim();
    }

    private static bool SpellColourPartMatches(string rawPart, MagicColours? selectionColour, bool selectionIsElemental, bool selectionIsSorcorial)
    {
        var tokens = TokenizeColour(rawPart);
        if (tokens.Count == 0)
            return false;

        if (tokens.Any(t => t == "all" || t == "any"))
            return selectionColour.HasValue || selectionIsElemental || selectionIsSorcorial;

        var hasEle = tokens.Any(t => t == "ele" || t == "elemental");
        var hasSoc = tokens.Any(t => t.StartsWith("sorc", StringComparison.OrdinalIgnoreCase) || t == "soc");
        var hasBar = tokens.Any(t => t == "bar" || t == "not" || t == "except");
        var hasGrey = tokens.Any(t => t == "grey" || t == "gray" || t == "gr");

        if (hasEle && hasBar && hasGrey)
            return selectionIsElemental && selectionColour.HasValue && selectionColour.Value != MagicColours.Grey;

        if (hasEle && hasSoc)
            return selectionIsElemental || selectionIsSorcorial;

        if (hasEle)
            return selectionIsElemental;

        if (hasSoc)
            return selectionIsSorcorial;

        foreach (var token in tokens)
        {
            if (TryParseMagicColour(token, out var colour))
            {
                if (selectionColour.HasValue && colour == selectionColour.Value)
                    return true;
            }
        }

        return false;
    }

    private static List<string> TokenizeColour(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var chars = raw.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray();

        return new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool IsElementalSelection(string selection)
        => Enum.TryParse<ElementalColours>(selection.Trim(), ignoreCase: true, out _);

    private static bool IsSorcorialSelection(string selection)
    {
        var trimmed = selection.Trim();
        if (trimmed.Length == 0)
            return false;

        if (trimmed.StartsWith("Sorc", StringComparison.OrdinalIgnoreCase))
            return true;

        if (Enum.TryParse<MagicColours>(trimmed, ignoreCase: true, out var colour))
            return !Enum.TryParse<ElementalColours>(colour.ToString(), ignoreCase: true, out _);

        return Enum.TryParse<ExtendedMagicColours>(trimmed, ignoreCase: true, out var ext)
               && ext == ExtendedMagicColours.Sorcorial;
    }
}
