using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Helpers;
using labyItems.Models.Characters;

namespace labyItems.Services;

public sealed class CharacterAdvancementDomainService : ICharacterAdvancementDomainService
{
    public int GetScriptureTablesReached(int points)
        => CharacterProgressionTables.GetHighestTableReached(points);

    public IReadOnlyList<AbilityPointSpendLine> BuildAbilityPointSpendLines(
        IEnumerable<string> selectedAbilityNames,
        IReadOnlyDictionary<string, int> abilityCostByName)
    {
        var lines = new List<AbilityPointSpendLine>();
        var runningTotal = 0;

        foreach (var rawName in selectedAbilityNames ?? Enumerable.Empty<string>())
        {
            var name = (rawName ?? string.Empty).Trim();
            if (name.Length == 0)
                continue;

            var cost = abilityCostByName != null && abilityCostByName.TryGetValue(name, out var resolvedCost)
                ? resolvedCost
                : 0;
            runningTotal += cost;
            lines.Add(new AbilityPointSpendLine(name, cost, runningTotal));
        }

        return lines;
    }

    public int ComputeAbilityPointsSpent(
        IEnumerable<string> selectedAbilityNames,
        IReadOnlyDictionary<string, int> abilityCostByName)
    {
        var total = 0;
        foreach (var rawName in selectedAbilityNames ?? Enumerable.Empty<string>())
        {
            var name = (rawName ?? string.Empty).Trim();
            if (name.Length == 0)
                continue;

            if (abilityCostByName != null && abilityCostByName.TryGetValue(name, out var cost))
                total += cost;
        }

        return total;
    }

    public string NormalizeAlignmentToken(string? value)
    {
        var token = NormalizeToken(value);
        if (token.StartsWith("good", StringComparison.OrdinalIgnoreCase))
            return "good";
        if (token.StartsWith("evil", StringComparison.OrdinalIgnoreCase))
            return "evil";
        return "neutral";
    }

    public HashSet<string> GetAllowedMiracleAlignments(
        Alignment? alignment,
        IEnumerable<MiracleListEntryDraft> entries,
        bool lockTrueNeutral)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!alignment.HasValue)
        {
            allowed.Add("good");
            allowed.Add("neutral");
            allowed.Add("evil");
            return allowed;
        }

        var moral = alignment.Value.Moral;
        var order = alignment.Value.Order;

        if (moral == MoralAxis.Good)
        {
            allowed.Add("good");
            allowed.Add("neutral");
            return allowed;
        }

        if (moral == MoralAxis.Evil)
        {
            allowed.Add("evil");
            allowed.Add("neutral");
            return allowed;
        }

        if (order == OrderAxis.Lawful)
        {
            allowed.Add("good");
            allowed.Add("neutral");
            return allowed;
        }

        if (order == OrderAxis.Chaotic)
        {
            allowed.Add("evil");
            allowed.Add("neutral");
            return allowed;
        }

        if (lockTrueNeutral)
        {
            var hasGood = entries.Any(e => NormalizeAlignmentToken(e.Alignment) == "good");
            var hasEvil = entries.Any(e => NormalizeAlignmentToken(e.Alignment) == "evil");

            if (hasGood && !hasEvil)
            {
                allowed.Add("good");
                allowed.Add("neutral");
                return allowed;
            }

            if (hasEvil && !hasGood)
            {
                allowed.Add("evil");
                allowed.Add("neutral");
                return allowed;
            }
        }

        allowed.Add("good");
        allowed.Add("neutral");
        allowed.Add("evil");
        return allowed;
    }

    public bool AreMiracleEntriesAlignmentCompatible(
        Alignment? alignment,
        IEnumerable<MiracleListEntryDraft> entries)
    {
        var allowed = GetAllowedMiracleAlignments(alignment, entries, lockTrueNeutral: true);
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            if (!allowed.Contains(NormalizeAlignmentToken(entry.Alignment)))
                return false;
        }

        return true;
    }

    public MiraclePointTotals ComputeMiraclePointTotals(IEnumerable<MiracleListEntryDraft> entries)
    {
        var good = 0;
        var neutral = 0;
        var evil = 0;
        var advanced = 0;

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            var power = Math.Max(0, entry.Power);
            var alignment = NormalizeAlignmentToken(entry.Alignment);
            if (alignment == "good")
                good += power;
            else if (alignment == "evil")
                evil += power;
            else
                neutral += power;

            if (entry.IsAdvanced)
                advanced += power;
        }

        return new MiraclePointTotals(
            Good: good,
            Neutral: neutral,
            Evil: evil,
            Total: good + neutral + evil,
            Advanced: advanced);
    }

    public EvocationPointTotals ComputeEvocationPointTotals(IEnumerable<EvocationListEntryDraft> entries)
    {
        var total = entries.Sum(e => Math.Max(0, e.Power));
        var advanced = entries.Where(e => e.IsAdvanced).Sum(e => Math.Max(0, e.Power));
        return new EvocationPointTotals(Total: total, Advanced: advanced);
    }

    public bool AdvancedEvocationsShareAField(
        IEnumerable<EvocationListEntryDraft> entries,
        Func<string, IReadOnlyCollection<string>?> resolveFieldsByName)
    {
        HashSet<string>? common = null;
        foreach (var entry in entries.Where(e => e.IsAdvanced && !string.IsNullOrWhiteSpace(e.Name)))
        {
            var fields = resolveFieldsByName(entry.Name ?? string.Empty);
            if (fields == null || fields.Count == 0)
                return false;

            var normalized = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);
            if (common == null)
                common = normalized;
            else
                common.IntersectWith(normalized);

            if (common.Count == 0)
                return false;
        }

        return true;
    }

    private static string NormalizeToken(string? value)
    {
        var text = value ?? string.Empty;
        var chars = text.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }
}
