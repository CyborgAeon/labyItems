using System.Text.RegularExpressions;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class ItemAbilityLinkService
{
    private static readonly Regex EquationRowRegex = new(
        @"^(?<label>.+?)\s*=\s*(?<cost>-?\d+)(?:\s*\(\s*-?\d+\s*\))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex StrengthTokenRegex = new(
        @"\+\s*(?<value>\d+)\s*(?:str|strength)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PerDaySuffixRegex = new(
        @"\s*\(\s*\d+\s*/\s*day\s*\)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TrailingParentheticalRegex = new(
        @"\s*\([^)]*\)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingPercentRegex = new(
        @"^\s*\d+\s*%\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LeadingIconRegex = new(
        @"^[^\p{L}\p{N}\+\-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] NonAbilityTokens =
    {
        "base isp",
        "total isp",
        "isp total",
        "created",
        "status bag",
        "apprentice status",
        "weapon (",
        "shield (",
        "power store",
        "cp benefit",
        "basic miracles",
        "advanced miracles",
        "basic spells",
        "advanced spells",
        "evocations",
        "manual isp entry",
        "final isp"
    };

    public sealed record AbilityLinkHint(
        string SourceText,
        IReadOnlyList<string> CandidateNames,
        string DisplayContext);

    public static AbilityLinkHint? TryCreateHint(string? rowText)
    {
        var label = ExtractLabel(rowText);
        if (label.Length == 0)
            return null;

        var candidates = new List<string>();
        var displayContext = string.Empty;

        if (ContainsToken(label, "cold rage"))
        {
            candidates.Add("Cold Rage");
            displayContext = label;
        }

        var strengthMatch = StrengthTokenRegex.Match(label);
        if (strengthMatch.Success)
        {
            candidates.Add("1st Grade of Strength");
            displayContext = label;
        }

        if (ContainsToken(label, "primal strike"))
        {
            candidates.Add("Ki Strike");
            if (displayContext.Length == 0)
                displayContext = "Primal Strike";
        }
        else if (ContainsToken(label, "ki strike"))
        {
            candidates.Add("Ki Strike");
            if (displayContext.Length == 0)
                displayContext = "Ki Strike";
        }

        foreach (var candidate in BuildGenericCandidates(label))
            candidates.Add(candidate);

        var distinct = candidates
            .Select(c => (c ?? string.Empty).Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
            return null;

        if (displayContext.Length == 0)
            displayContext = label;

        return new AbilityLinkHint(label, distinct, displayContext);
    }

    public static async Task<EvolutionService.AbilityResult?> ResolveAbilityAsync(AbilityLinkHint hint)
    {
        if (hint == null || hint.CandidateNames.Count == 0)
            return null;

        foreach (var candidate in hint.CandidateNames)
        {
            var resolved = await AbilityDetailsLookupService.FindByIndexAsync(candidate);
            if (resolved != null)
                return ApplyDisplayContext(resolved, hint.DisplayContext);
        }

        foreach (var candidate in hint.CandidateNames)
        {
            var specialisationAbility = await DetailCardLookupService.FindSpecialisationAbilityAsync(candidate);
            if (specialisationAbility.Ability == null)
                continue;

            var converted = ToAbilityResult(specialisationAbility.Ability, specialisationAbility.Key, candidate);
            return ApplyDisplayContext(converted, hint.DisplayContext);
        }

        return null;
    }

    private static EvolutionService.AbilityResult ApplyDisplayContext(
        EvolutionService.AbilityResult ability,
        string displayContext)
    {
        var context = (displayContext ?? string.Empty).Trim();
        if (context.Length == 0)
            return ability;

        var index = (ability.Index ?? string.Empty).Trim();
        if (index.Length == 0)
            return ability with { Index = context };

        if (index.Equals(context, StringComparison.OrdinalIgnoreCase))
            return ability;

        return ability with { Index = $"{index} ({context})" };
    }

    private static EvolutionService.AbilityResult ToAbilityResult(
        AbilityDefinition source,
        string resolvedKey,
        string requestedKey)
    {
        var name = (source.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (resolvedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (requestedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = "Ability";

        return new EvolutionService.AbilityResult
        {
            Index = name,
            Description = source.Effect ?? string.Empty,
            Cost = 0,
            Table = 0,
            Available = source.Source ?? "ALL",
            CanBuyMultiple = false,
            PreReqs = source.PreReqs is { Count: > 0 } preReqs
                ? preReqs
                : Array.Empty<string>(),
            MaxAvailable = source.Count
        };
    }

    private static IEnumerable<string> BuildGenericCandidates(string label)
    {
        var normalized = (label ?? string.Empty).Trim();
        if (normalized.Length == 0)
            yield break;

        if (LeadingPercentRegex.IsMatch(normalized)
            && !ContainsToken(normalized, "cold rage"))
        {
            yield break;
        }

        var lower = normalized.ToLowerInvariant();
        if (NonAbilityTokens.Any(token => lower.Contains(token, StringComparison.OrdinalIgnoreCase)))
            yield break;

        if (normalized.Contains(" = ", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(':'))
        {
            yield break;
        }

        var candidate = normalized;
        candidate = LeadingIconRegex.Replace(candidate, string.Empty).Trim();
        candidate = LeadingPercentRegex.Replace(candidate, string.Empty).Trim();
        candidate = PerDaySuffixRegex.Replace(candidate, string.Empty).Trim();

        if (candidate.Contains(" vs ", StringComparison.OrdinalIgnoreCase))
            yield break;

        if (candidate.Length >= 3)
            yield return candidate;

        var withoutTrailingParen = TrailingParentheticalRegex.Replace(candidate, string.Empty).Trim();
        if (withoutTrailingParen.Length >= 3
            && !withoutTrailingParen.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        {
            yield return withoutTrailingParen;
        }
    }

    private static string ExtractLabel(string? rowText)
    {
        var line = NormalizeLine(rowText);
        if (line.Length == 0)
            return string.Empty;

        var equation = EquationRowRegex.Match(line);
        if (equation.Success)
        {
            var label = (equation.Groups["label"].Value ?? string.Empty).Trim();
            return NormalizeLine(label);
        }

        return line;
    }

    private static string NormalizeLine(string? text)
    {
        var line = (text ?? string.Empty).Trim();
        while (line.Length > 0 && (line[0] == '|' || line[0] == '-' || line[0] == '•'))
            line = line[1..].TrimStart();
        return line;
    }

    private static bool ContainsToken(string source, string token)
        => source.Contains(token, StringComparison.OrdinalIgnoreCase);
}
