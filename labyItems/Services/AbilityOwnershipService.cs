using System.Text.RegularExpressions;
using labyItems.Models.Abilities;

namespace labyItems.Services;

public readonly record struct AbilityOwnershipProfile(
    IReadOnlySet<string> Aliases,
    int StrengthGrade,
    int WeaponMasteryGrade);

public static class AbilityOwnershipService
{
    private static readonly Regex StrengthGradeStartRegex = new(
        @"^\s*(?<grade>\d+)(?:st|nd|rd|th)?\s*grade\s+of\s+strength\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StrengthPlusStartRegex = new(
        @"^\s*\+?(?<grade>\d+)(?:st|nd|rd|th)?\s*strength\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StrengthGrantRegex = new(
        @"^\s*(?:grants?|gains?|provides?)\s*\+?(?<grade>\d+)\s*(?:stacking\s*)?(?:grade\s+of\s+)?strength\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WeaponMasteryStartRegex = new(
        @"^\s*\+?(?<grade>\d+)(?:st|nd|rd|th)?\s*(?:weapon\s*mastery|wm)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WeaponMasteryGrantRegex = new(
        @"^\s*(?:grants?|gains?)\s*\+?(?<grade>\d+)\s*(?:weapon\s*mastery|wm)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StrengthRequirementRegex = new(
        @"(?:\+|\b)(?<grade>\d+)(?:st|nd|rd|th)?\s*(?:grade\s+of\s+)?strength\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex WeaponMasteryRequirementRegex = new(
        @"(?:\+|\b)(?<grade>\d+)(?:st|nd|rd|th)?\s*(?:weapon\s*mastery|wm)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static AbilityOwnershipProfile Build(
        IEnumerable<string>? knownAbilityTerms,
        IEnumerable<EvolutionService.AbilityResult>? selectedAbilities,
        IReadOnlyDictionary<string, EvolutionService.AbilityResult> aliasLookup)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedAbilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var strengthGrade = 0;
        var weaponMasteryGrade = 0;

        void RegisterTerm(string? rawTerm, bool resolveAliases)
        {
            var term = (rawTerm ?? string.Empty).Trim();
            if (term.Length == 0)
                return;

            UpdateOwnedStrengthGrade(term, ref strengthGrade);
            UpdateOwnedWeaponMasteryGrade(term, ref weaponMasteryGrade);

            foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(term))
            {
                var normalized = AbilityDetailsLookupService.NormalizeKey(equivalent);
                if (normalized.Length == 0)
                    continue;

                aliases.Add(normalized);
                if (!resolveAliases)
                    continue;

                if (aliasLookup.TryGetValue(normalized, out var resolved))
                    RegisterAbility(resolved, resolveAliases: false);
            }
        }

        void RegisterAbility(EvolutionService.AbilityResult ability, bool resolveAliases)
        {
            var identity = AbilityKey.Build(ability);
            if (identity.Length == 0)
                identity = $"{ability.Table}|{ability.Index}".Trim();
            if (identity.Length == 0 || !visitedAbilityKeys.Add(identity))
                return;

            foreach (var term in GetEquivalentTerms(ability))
                RegisterTerm(term, resolveAliases);

            RegisterTerm(ability.Description, resolveAliases: false);
        }

        foreach (var known in knownAbilityTerms ?? Array.Empty<string>())
            RegisterTerm(known, resolveAliases: true);

        foreach (var selected in selectedAbilities ?? Array.Empty<EvolutionService.AbilityResult>())
            RegisterAbility(selected, resolveAliases: false);

        return new AbilityOwnershipProfile(aliases, strengthGrade, weaponMasteryGrade);
    }

    public static bool TryParseStrengthRequirement(string? raw, out int requiredGrade)
        => TryParseRequiredGrade(raw, StrengthRequirementRegex, out requiredGrade);

    public static bool TryParseWeaponMasteryRequirement(string? raw, out int requiredGrade)
        => TryParseRequiredGrade(raw, WeaponMasteryRequirementRegex, out requiredGrade);

    private static IEnumerable<string> GetEquivalentTerms(EvolutionService.AbilityResult ability)
    {
        foreach (var equivalent in EvolutionService.GetEquivalentAbilityNames(ability.Index))
            yield return equivalent;

        var abilityRef = (ability.AbilityRef ?? string.Empty).Trim();
        if (abilityRef.Length > 0)
            yield return abilityRef;

        var abilityKey = AbilityKey.Build(ability);
        if (abilityKey.Length > 0)
            yield return abilityKey;

        var legacyKey = AbilityKey.BuildEvolutionFallback(ability);
        if (legacyKey.Length > 0)
            yield return legacyKey;
    }

    private static void UpdateOwnedStrengthGrade(string value, ref int current)
    {
        if (TryParseOwnedGrade(value, StrengthGradeStartRegex, out var parsed))
        {
            current = Math.Max(current, parsed);
            return;
        }

        if (TryParseOwnedGrade(value, StrengthPlusStartRegex, out parsed))
        {
            current = Math.Max(current, parsed);
            return;
        }

        if (TryParseOwnedGrade(value, StrengthGrantRegex, out parsed))
            current = Math.Max(current, parsed);
    }

    private static void UpdateOwnedWeaponMasteryGrade(string value, ref int current)
    {
        if (TryParseOwnedGrade(value, WeaponMasteryStartRegex, out var parsed))
        {
            current = Math.Max(current, parsed);
            return;
        }

        if (TryParseOwnedGrade(value, WeaponMasteryGrantRegex, out parsed))
            current = Math.Max(current, parsed);
    }

    private static bool TryParseOwnedGrade(string raw, Regex regex, out int grade)
        => TryParseGrade(raw, regex, out grade);

    private static bool TryParseRequiredGrade(string? raw, Regex regex, out int grade)
        => TryParseGrade(raw, regex, out grade);

    private static bool TryParseGrade(string? raw, Regex regex, out int grade)
    {
        grade = 0;
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var match = regex.Match(text);
        if (!match.Success)
            return false;

        var token = match.Groups["grade"].Value;
        if (!int.TryParse(token, out var parsed) || parsed <= 0)
            return false;

        grade = parsed;
        return true;
    }
}
