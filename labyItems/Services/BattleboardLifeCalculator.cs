using System.Text.Json;
using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class BattleboardLifeCalculator
{
    private static readonly Regex LifePairRegex = new(
        @"([+-]?\d+)\s*/\s*([+-]?\d+)",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex ExtraLevelNumericRegex = new(
        @"\b\d+(?:st|nd|rd|th)\s+extra\s+level\s+of\s+life\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex ExtraLevelWordRegex = new(
        @"\b(first|second|third|fourth|fifth)\s+extra\s+level\s+of\s+life\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly IReadOnlyList<LifeCapEntry> LifeCapTable = new[]
    {
        new LifeCapEntry(82, 123, 136, 164, 191),
        new LifeCapEntry(72, 108, 120, 144, 168),
        new LifeCapEntry(68, 102, 113, 136, 158),
        new LifeCapEntry(63, 94, 105, 126, 147),
        new LifeCapEntry(60, 90, 100, 120, 140),
        new LifeCapEntry(51, 76, 85, 102, 119),
        new LifeCapEntry(47, 70, 78, 94, 109),
        new LifeCapEntry(39, 58, 65, 78, 91),
        new LifeCapEntry(32, 48, 53, 64, 74),
        new LifeCapEntry(29, 44, 49, 58, 68)
    };

    public static BattleboardLifeTotals Calculate(
        CharacterDraft? draft,
        IEnumerable<Item>? assignedItems = null)
    {
        var character = draft ?? new CharacterDraft();
        var draftContributions = CollectDraftContributions(character.Abilities);

        var baseTblp = Math.Max(0, character.TBLP - draftContributions.TotalTblp);
        var baseLoc = Math.Max(0, character.Loc - draftContributions.TotalLoc);

        if (baseTblp == 0 && character.TBLP > 0)
            baseTblp = Math.Max(0, character.TBLP);
        if (baseLoc == 0 && character.Loc > 0)
            baseLoc = Math.Max(0, character.Loc);

        var resolvedItems = (assignedItems ?? ResolveAssignedItems(character)).ToList();

        var itemContributions = CollectItemContributions(resolvedItems);
        var bestBySource = MergeBestBySource(draftContributions.BestBySource, itemContributions.BestBySource);
        var tables = AddAmounts(draftContributions.Tables, itemContributions.Tables);

        var bonusTblp = bestBySource.Values.Sum(v => v.Tblp) + tables.Tblp;
        var bonusLoc = bestBySource.Values.Sum(v => v.Loc) + tables.Loc;

        var uncappedTblp = Math.Max(0, baseTblp + bonusTblp);
        var totalLoc = Math.Max(0, baseLoc + bonusLoc);

        var extraLifeLevels = draftContributions.ExtraLifeLevels + itemContributions.ExtraLifeLevels;
        var capStage = Math.Clamp(ResolveBaseCapStage(character.Points) + extraLifeLevels, 0, 3);
        var tblpCap = ResolveTblpCap(baseTblp, capStage);
        var totalTblp = tblpCap > 0 ? Math.Min(uncappedTblp, tblpCap) : uncappedTblp;

        return new BattleboardLifeTotals(
            TotalTblp: totalTblp,
            TotalLoc: totalLoc,
            UncappedTblp: uncappedTblp,
            TblpCap: tblpCap,
            BaseTblp: baseTblp,
            BaseLoc: baseLoc,
            ExtraLifeLevels: extraLifeLevels);
    }

    private static ContributionTotals CollectDraftContributions(IEnumerable<AbilityDraft>? abilities)
    {
        var totals = new ContributionTotals();
        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            var source = ability.Source ?? string.Empty;
            if (IsTablesSource(source))
                totals.ExtraLifeLevels += ExtractExtraLifeLevelCount(ability.Name, ability.Effect);

            if (ability.AbilityType != AbilityType.Life)
                continue;

            if (!TryReadLifePair(ability, out var amount))
                continue;

            totals.Add(amount, source);
        }

        return totals;
    }

    private static ContributionTotals CollectItemContributions(IEnumerable<Item> items)
    {
        var totals = new ContributionTotals();

        foreach (var item in items ?? Enumerable.Empty<Item>())
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            var abilities = payload?.Item?.Abilities ?? new List<CalcResult>();
            foreach (var ability in abilities)
            {
                if (ability == null)
                    continue;

                var type = (ability.AbilityType ?? string.Empty).Trim();
                if (type.Equals("Life", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryReadLifePair(ability, out var itemLife))
                        totals.Add(itemLife, "item");
                    continue;
                }

                if (!type.Equals("General", StringComparison.OrdinalIgnoreCase))
                    continue;

                var generalAbilityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(ability.AbilityName))
                    generalAbilityNames.Add(ability.AbilityName.Trim());

                foreach (var selectedAbilityName in ExtractSelectedGeneralAbilityNames(ability))
                    generalAbilityNames.Add(selectedAbilityName);

                foreach (var generalAbilityName in generalAbilityNames)
                {
                    totals.ExtraLifeLevels += ExtractExtraLifeLevelCount(generalAbilityName);
                    if (TryReadLifePairFromText(generalAbilityName, out var tableLife))
                        totals.Add(tableLife, "tables");
                }
            }
        }

        return totals;
    }

    private static IEnumerable<Item> ResolveAssignedItems(CharacterDraft character)
    {
        try
        {
            return LiteDbService.GetItemsAssignedToCharacter(
                character.CharacterRecordId,
                character.Name,
                character.PlayerName);
        }
        catch
        {
            return Enumerable.Empty<Item>();
        }
    }

    private static Dictionary<string, LifeAmount> MergeBestBySource(
        IReadOnlyDictionary<string, LifeAmount> left,
        IReadOnlyDictionary<string, LifeAmount> right)
    {
        var merged = new Dictionary<string, LifeAmount>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in left)
            merged[pair.Key] = pair.Value;

        foreach (var pair in right)
        {
            if (!merged.TryGetValue(pair.Key, out var existing) || pair.Value.IsHigherThan(existing))
                merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static IEnumerable<string> ExtractSelectedGeneralAbilityNames(CalcResult result)
    {
        if (result?.Details == null || !result.Details.TryGetValue("selectedGeneralAbilities", out var raw) || raw == null)
            yield break;

        if (raw is JsonElement element)
        {
            foreach (var name in ExtractSelectedGeneralAbilityNames(element))
                yield return name;
            yield break;
        }

        if (raw is IEnumerable<object> objects)
        {
            foreach (var entry in objects)
            {
                if (entry is string text && !string.IsNullOrWhiteSpace(text))
                {
                    yield return text.Trim();
                    continue;
                }

                var nameProp = entry?.GetType().GetProperty("name");
                if (nameProp?.GetValue(entry) is string reflected
                    && !string.IsNullOrWhiteSpace(reflected))
                {
                    yield return reflected.Trim();
                }
            }
        }
    }

    private static IEnumerable<string> ExtractSelectedGeneralAbilityNames(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var text = (entry.GetString() ?? string.Empty).Trim();
                if (text.Length > 0)
                    yield return text;
                continue;
            }

            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("name", out var nameElement)
                || nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = (nameElement.GetString() ?? string.Empty).Trim();
            if (name.Length > 0)
                yield return name;
        }
    }

    private static int ResolveBaseCapStage(int points)
    {
        if (points >= 5250)
            return 3;
        if (points >= 3000)
            return 2;
        if (points >= 1000)
            return 1;
        return 0;
    }

    private static int ResolveTblpCap(int baseTblp, int stage)
    {
        if (baseTblp <= 0)
            return 0;

        foreach (var row in LifeCapTable)
        {
            if (baseTblp >= row.BaseTblp)
                return row.GetStageValue(stage);
        }

        return LifeCapTable[^1].GetStageValue(stage);
    }

    private static int ExtractExtraLifeLevelCount(params string?[] values)
    {
        var count = 0;
        foreach (var value in values ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            count += ExtraLevelNumericRegex.Matches(value).Count;
            count += ExtraLevelWordRegex.Matches(value).Count;
        }

        return count;
    }

    private static bool TryReadLifePair(AbilityDraft ability, out LifeAmount amount)
    {
        amount = default;
        if (ability == null)
            return false;

        if (ability.Amount is { Count: > 0 })
        {
            var tblp = ability.Amount.Count > 0 ? ability.Amount[0] : 0;
            var loc = ability.Amount.Count > 1 ? ability.Amount[1] : 0;
            amount = new LifeAmount(tblp, loc);
            return !amount.IsZero;
        }

        if (TryReadLifePairFromText(ability.Effect, out amount))
            return !amount.IsZero;

        if (TryReadLifePairFromText(ability.Name, out amount))
            return !amount.IsZero;

        return false;
    }

    private static bool TryReadLifePair(CalcResult ability, out LifeAmount amount)
    {
        amount = default;
        if (ability == null)
            return false;

        if (TryReadLifePairFromText(ability.AbilityName, out amount))
            return !amount.IsZero;

        if (ability.Details != null
            && ability.Details.TryGetValue("life", out var rawLife))
        {
            if (rawLife is JsonElement jsonLife && jsonLife.ValueKind == JsonValueKind.String)
            {
                if (TryReadLifePairFromText(jsonLife.GetString(), out amount))
                    return !amount.IsZero;
            }
            else if (TryReadLifePairFromText(rawLife?.ToString(), out amount))
            {
                return !amount.IsZero;
            }
        }

        return false;
    }

    private static bool TryReadLifePairFromText(string? text, out LifeAmount amount)
    {
        amount = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var match = LifePairRegex.Match(text);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups[1].Value, out var tblp))
            return false;
        if (!int.TryParse(match.Groups[2].Value, out var loc))
            return false;

        amount = new LifeAmount(tblp, loc);
        return !amount.IsZero;
    }

    private static bool IsTablesSource(string? source)
        => NormalizeSource(source).Equals("tables", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSource(string? source)
    {
        var token = (source ?? string.Empty).Trim();
        if (token.Length == 0)
            return "unspecified";

        var compact = token.Replace("_", " ").Replace("-", " ").Trim();
        if (compact.Contains("table", StringComparison.OrdinalIgnoreCase))
            return "tables";
        if (compact.Contains("perm", StringComparison.OrdinalIgnoreCase))
            return "perm buff";
        if (compact.Contains("temp", StringComparison.OrdinalIgnoreCase))
            return "temporary";
        if (compact.Contains("item", StringComparison.OrdinalIgnoreCase))
            return "item";
        if (compact.Contains("guild", StringComparison.OrdinalIgnoreCase))
            return "guild";
        if (compact.Contains("status", StringComparison.OrdinalIgnoreCase))
            return "status";
        if (compact.Contains("radiat", StringComparison.OrdinalIgnoreCase))
            return "radiated";
        if (compact.Contains("base", StringComparison.OrdinalIgnoreCase))
            return "base";

        return compact.ToLowerInvariant();
    }

    private static LifeAmount AddAmounts(LifeAmount left, LifeAmount right)
        => new(left.Tblp + right.Tblp, left.Loc + right.Loc);

    private readonly record struct LifeAmount(int Tblp, int Loc)
    {
        public bool IsZero => Tblp == 0 && Loc == 0;

        public bool IsHigherThan(LifeAmount other)
            => Tblp > other.Tblp || (Tblp == other.Tblp && Loc > other.Loc);
    }

    private sealed class ContributionTotals
    {
        public Dictionary<string, LifeAmount> BestBySource { get; } = new(StringComparer.OrdinalIgnoreCase);
        public LifeAmount Tables { get; private set; }
        public int ExtraLifeLevels { get; set; }

        public int TotalTblp => BestBySource.Values.Sum(value => value.Tblp) + Tables.Tblp;
        public int TotalLoc => BestBySource.Values.Sum(value => value.Loc) + Tables.Loc;

        public void Add(LifeAmount amount, string source)
        {
            if (amount.IsZero)
                return;

            var normalizedSource = NormalizeSource(source);
            if (normalizedSource.Equals("tables", StringComparison.OrdinalIgnoreCase))
            {
                Tables = AddAmounts(Tables, amount);
                return;
            }

            if (!BestBySource.TryGetValue(normalizedSource, out var existing)
                || amount.IsHigherThan(existing))
            {
                BestBySource[normalizedSource] = amount;
            }
        }
    }

    private readonly record struct LifeCapEntry(
        int BaseTblp,
        int PreTb8,
        int PreTb10,
        int PreTb11,
        int PreTb12)
    {
        public int GetStageValue(int stage)
            => stage switch
            {
                <= 0 => PreTb8,
                1 => PreTb10,
                2 => PreTb11,
                _ => PreTb12
            };
    }
}

public readonly record struct BattleboardLifeTotals(
    int TotalTblp,
    int TotalLoc,
    int UncappedTblp,
    int TblpCap,
    int BaseTblp,
    int BaseLoc,
    int ExtraLifeLevels);
