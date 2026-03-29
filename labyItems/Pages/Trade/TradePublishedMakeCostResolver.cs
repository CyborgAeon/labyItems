using System.Globalization;
using System.Text.Json;
using labyItems.Models;

namespace labyItems.Pages.Trade;

internal sealed record TradeCustomCostResolution(
    int Cost,
    string Formula,
    bool UsesPublishedMake,
    string SourceLabel,
    bool HasIspEstimatePortion = false);

internal static class TradePublishedMakeCostResolver
{
    private static readonly HashSet<string> AllowedWeaponKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "base",
        "type",
        "spiritual",
        "magicalColours"
    };

    private static readonly HashSet<string> AllowedArmourKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "armourType",
        "AC",
        "layeredSummary",
        "magicalColours",
        "spiritualNonOpposite",
        "enhancementBonuses"
    };

    private static readonly HashSet<string> AllowedShieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "shieldColours",
        "spiritualNonOpposite",
        "shieldAlignments",
        "MAC",
        "SAC"
    };

    public static TradeCustomCostResolution Resolve(IReadOnlyList<CalcResult> abilities, int totalIsp)
    {
        var fallback = BuildWholeFallback(totalIsp);

        var list = (abilities ?? Array.Empty<CalcResult>())
            .Where(item => item != null)
            .ToList();
        if (list.Count == 0)
            return fallback;

        var nonBase = list
            .Where(item => !IsType(item, "Base"))
            .ToList();
        if (nonBase.Count == 0)
            return fallback;

        var more = nonBase.FirstOrDefault(item => IsType(item, "More"));
        var status = ResolveStatusContribution(more);

        var totalCost = 0;
        var hasPublished = false;
        var hasFallback = false;
        var statusAppliedToPublished = false;
        var formulaParts = new List<string>();

        foreach (var ability in nonBase.Where(item => !IsType(item, "More")))
        {
            var statusRolls = !statusAppliedToPublished ? status.StatusRolls : 0;

            if (IsType(ability, "Weapon") && TryResolveWeapon(ability, statusRolls, out var weaponCost))
            {
                totalCost += weaponCost.Cost;
                hasPublished = true;
                if (statusRolls > 0)
                    statusAppliedToPublished = true;
                formulaParts.Add($"{weaponCost.SourceLabel}: {weaponCost.Formula}");
                continue;
            }

            if (IsType(ability, "Armour") && TryResolveArmour(ability, statusRolls, out var armourCost))
            {
                totalCost += armourCost.Cost;
                hasPublished = true;
                if (statusRolls > 0)
                    statusAppliedToPublished = true;
                formulaParts.Add($"{armourCost.SourceLabel}: {armourCost.Formula}");
                continue;
            }

            if (IsType(ability, "Shield") && TryResolveShield(ability, statusRolls, out var shieldCost))
            {
                totalCost += shieldCost.Cost;
                hasPublished = true;
                if (statusRolls > 0)
                    statusAppliedToPublished = true;
                formulaParts.Add($"{shieldCost.SourceLabel}: {shieldCost.Formula}");
                continue;
            }

            var fallbackIsp = Math.Max(0, ability.TotalIsp);
            var fallbackCost = fallbackIsp * 500;
            totalCost += fallbackCost;
            hasFallback = true;
            formulaParts.Add($"{ResolveFallbackLabel(ability)} ISP {fallbackIsp} × 500");
        }

        if (more != null && (status.HasUnsupportedOptions || (status.StatusRolls > 0 && !statusAppliedToPublished)))
        {
            var moreIsp = Math.Max(0, more.TotalIsp);
            totalCost += moreIsp * 500;
            hasFallback = true;
            formulaParts.Add($"More ISP {moreIsp} × 500");
        }

        if (!hasPublished && !hasFallback)
            return fallback;

        var finalFormula = formulaParts.Count == 0
            ? fallback.Formula
            : string.Join(" + ", formulaParts);

        return new TradeCustomCostResolution(
            Cost: Math.Max(0, totalCost),
            Formula: finalFormula,
            UsesPublishedMake: hasPublished,
            SourceLabel: hasPublished && hasFallback
                ? "Published + ISP estimate"
                : (hasPublished ? "Published make" : "ISP fallback"),
            HasIspEstimatePortion: hasFallback);
    }

    private static TradeCustomCostResolution BuildWholeFallback(int totalIsp)
    {
        var fallbackIsp = Math.Max(0, totalIsp);
        return new TradeCustomCostResolution(
            Cost: fallbackIsp * 500,
            Formula: $"ISP {fallbackIsp} × 500",
            UsesPublishedMake: false,
            SourceLabel: "ISP fallback",
            HasIspEstimatePortion: true);
    }

    private static bool TryResolveWeapon(CalcResult weapon, int statusRolls, out TradeCustomCostResolution resolution)
    {
        resolution = default!;

        var details = weapon.Details ?? new Dictionary<string, object?>();
        if (!TryGetString(details, "base", out var baseOption) || baseOption.Length == 0)
            return false;

        if (HasUnsupportedKeys(details, AllowedWeaponKeys))
            return false;

        var normalized = NormalizeToken(baseOption);

        var isSpiritualBase = TryGetBool(details, "spiritual", out var spiritualMarker) && spiritualMarker;
        var isMagicalBase = normalized.StartsWith("magic", StringComparison.OrdinalIgnoreCase);

        var (setup, rollCost, baseRolls, label) = normalized switch
        {
            "physicalplus1" => (1000, 1000, 5, "+1 Physical Weapon"),

            "magic0" => (2000, 2000, 4, "+0 Magic Weapon"),
            "magicplus1" => (2000, 2000, 9, "+1 Magic Weapon"),
            "magicplus2" => (2000, 2000, 15, "+2 Magic Weapon"),

            "spirit0" => (3000, 3000, 4, "+0 Spiritual Weapon"),
            "spiritplus1" => (3000, 3000, 9, "+1 Spiritual Weapon"),
            "spiritplus2" => (3000, 3000, 15, "+2 Spiritual Weapon"),

            _ => (0, 0, 0, string.Empty)
        };

        if (setup <= 0 || rollCost <= 0 || baseRolls <= 0)
            return false;

        var colourOrAlignmentRolls = 0;
        if (isMagicalBase && TryGetInt(details, "magicalColours", out var magicalColours) && magicalColours > 0)
            colourOrAlignmentRolls = 1;

        if (!isSpiritualBase && normalized.StartsWith("spirit", StringComparison.OrdinalIgnoreCase))
            return false;

        var rolls = baseRolls + colourOrAlignmentRolls + Math.Max(0, statusRolls);
        var cost = setup + (rollCost * rolls);

        var rollParts = new List<string> { $"base {baseRolls}" };
        if (colourOrAlignmentRolls > 0)
            rollParts.Add("colour/alignment 1");
        if (statusRolls > 0)
            rollParts.Add($"status {statusRolls}");

        var formula = $"setup {setup.ToString("N0", CultureInfo.InvariantCulture)} + ({rollCost.ToString("N0", CultureInfo.InvariantCulture)} × (rolls {string.Join(" + ", rollParts)}))";

        resolution = new TradeCustomCostResolution(cost, formula, true, label);
        return true;
    }

    private static bool TryResolveArmour(CalcResult armour, int statusRolls, out TradeCustomCostResolution resolution)
    {
        resolution = default!;

        var details = armour.Details ?? new Dictionary<string, object?>();
        if (!TryGetString(details, "armourType", out var armourTypeRaw) || armourTypeRaw.Length == 0)
            return false;

        if (!TryGetInt(details, "AC", out var ac) || ac <= 0)
            return false;

        if (HasUnsupportedKeys(details, AllowedArmourKeys))
            return false;

        var armourType = NormalizeToken(armourTypeRaw);
        var isMagical = armourType.Contains("magic", StringComparison.OrdinalIgnoreCase);
        var isSpiritual = armourType.Contains("spirit", StringComparison.OrdinalIgnoreCase);

        if (!isMagical && !isSpiritual)
            return false;

        var setup = isMagical ? 2000 : 3000;
        var rollCost = isMagical ? 2000 : 3000;
        var baseRolls = ac <= 2 ? 3 : (ac <= 4 ? 4 : 5);

        if (!TryResolveArmourEnhancementRolls(details, isMagical, out var enhancementRolls))
            return false;

        var alignmentOrColourRolls = 0;
        if (isMagical && TryGetInt(details, "magicalColours", out var colours) && colours > 0)
            alignmentOrColourRolls = 1;
        if (isSpiritual && TryGetBool(details, "spiritualNonOpposite", out var aligned) && aligned)
            alignmentOrColourRolls = 1;

        var rolls = baseRolls + enhancementRolls + alignmentOrColourRolls + Math.Max(0, statusRolls);
        var cost = setup + (rollCost * rolls);

        var rollParts = new List<string> { $"base {baseRolls}" };
        if (enhancementRolls > 0)
            rollParts.Add($"enhancement {enhancementRolls}");
        if (alignmentOrColourRolls > 0)
            rollParts.Add($"colour/alignment {alignmentOrColourRolls}");
        if (statusRolls > 0)
            rollParts.Add($"status {statusRolls}");

        var formula = $"setup {setup.ToString("N0", CultureInfo.InvariantCulture)} + ({rollCost.ToString("N0", CultureInfo.InvariantCulture)} × (rolls {string.Join(" + ", rollParts)}))";
        var label = isMagical ? "Magical Armour" : "Spiritual Armour";

        resolution = new TradeCustomCostResolution(cost, formula, true, label);
        return true;
    }

    private static bool TryResolveShield(CalcResult shield, int statusRolls, out TradeCustomCostResolution resolution)
    {
        resolution = default!;

        var details = shield.Details ?? new Dictionary<string, object?>();
        if (HasUnsupportedKeys(details, AllowedShieldKeys))
            return false;

        var name = (shield.AbilityName ?? string.Empty).Trim();
        var normalizedName = NormalizeToken(name);

        var isMagical = normalizedName.Contains("magicalshield", StringComparison.OrdinalIgnoreCase);
        var isSpiritual = normalizedName.Contains("spiritualshield", StringComparison.OrdinalIgnoreCase);

        if (!isMagical && !isSpiritual)
            return false;

        var setup = isMagical ? 2000 : 3000;
        var rollCost = isMagical ? 2000 : 3000;
        var baseRolls = 4; // Published rules treat manufactured supernatural shields as medium-armour equivalent.

        if (!TryResolveShieldEnhancementRolls(details, isMagical, out var enhancementRolls))
            return false;

        var colourOrAlignmentRolls = 0;
        if (isMagical)
        {
            if (TryGetInt(details, "shieldColours", out var colours) && colours > 0)
                colourOrAlignmentRolls = 1;
            if (TryGetBool(details, "spiritualNonOpposite", out var spiritualAligned) && spiritualAligned)
                return false;
            if (TryGetInt(details, "shieldAlignments", out var alignments) && alignments > 0)
                return false;
        }
        else
        {
            if (TryGetBool(details, "spiritualNonOpposite", out var aligned) && aligned)
                colourOrAlignmentRolls = 1;
            if (TryGetInt(details, "shieldAlignments", out var alignments) && alignments > 0)
                colourOrAlignmentRolls = 1;
            if (TryGetInt(details, "shieldColours", out var colours) && colours > 0)
                return false;
        }

        var rolls = baseRolls + enhancementRolls + colourOrAlignmentRolls + Math.Max(0, statusRolls);
        var cost = setup + (rollCost * rolls);

        var rollExpression = new List<string> { $"base {baseRolls}" };
        if (enhancementRolls > 0)
            rollExpression.Add($"enhancement {enhancementRolls}");
        if (colourOrAlignmentRolls > 0)
            rollExpression.Add($"colour/alignment {colourOrAlignmentRolls}");
        if (statusRolls > 0)
            rollExpression.Add($"status {statusRolls}");

        var formula = $"setup {setup.ToString("N0", CultureInfo.InvariantCulture)} + ({rollCost.ToString("N0", CultureInfo.InvariantCulture)} × (rolls {string.Join(" + ", rollExpression)}))";
        var label = isMagical ? "Magical Shield" : "Spiritual Shield";

        resolution = new TradeCustomCostResolution(cost, formula, true, label);
        return true;
    }

    private static bool TryResolveArmourEnhancementRolls(
        Dictionary<string, object?> details,
        bool isMagical,
        out int enhancementRolls)
    {
        enhancementRolls = 0;
        if (!details.TryGetValue("enhancementBonuses", out var raw) || raw == null)
            return true;

        foreach (var entry in EnumerateEnhancementBonuses(raw))
        {
            var type = NormalizeToken(entry.Type);
            var value = Math.Max(0, entry.Value);
            if (value <= 0)
                continue;

            if (isMagical && type != "mac")
                return false;
            if (!isMagical && type != "sac")
                return false;

            if (value == 1)
            {
                enhancementRolls += 5;
                continue;
            }

            if (value == 2)
            {
                enhancementRolls += 10;
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool TryResolveShieldEnhancementRolls(
        Dictionary<string, object?> details,
        bool isMagical,
        out int enhancementRolls)
    {
        enhancementRolls = 0;

        var mac = TryGetInt(details, "MAC", out var macValue) ? Math.Max(0, macValue) : 0;
        var sac = TryGetInt(details, "SAC", out var sacValue) ? Math.Max(0, sacValue) : 0;

        if (isMagical && sac > 0)
            return false;
        if (!isMagical && mac > 0)
            return false;

        var relevant = isMagical ? mac : sac;
        if (relevant <= 0)
            return true;

        if (relevant == 1)
        {
            enhancementRolls = 5;
            return true;
        }

        if (relevant == 2)
        {
            enhancementRolls = 10;
            return true;
        }

        return false;
    }

    private static IEnumerable<(string Type, int Value)> EnumerateEnhancementBonuses(object raw)
    {
        if (raw is IEnumerable<Dictionary<string, object>> typedDictionary)
        {
            foreach (var entry in typedDictionary)
            {
                var type = GetStringValue(entry, "type");
                var value = GetIntValue(entry, "value");
                if (type.Length > 0)
                    yield return (type, value);
            }

            yield break;
        }

        if (raw is IEnumerable<object> objectList)
        {
            foreach (var item in objectList)
            {
                switch (item)
                {
                    case Dictionary<string, object?> nullableEntry:
                    {
                        var type = GetStringValue(nullableEntry, "type");
                        var value = GetIntValue(nullableEntry, "value");
                        if (type.Length > 0)
                            yield return (type, value);
                        break;
                    }
                    case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object:
                    {
                        var type = GetStringValue(jsonElement, "type");
                        var value = GetIntValue(jsonElement, "value");
                        if (type.Length > 0)
                            yield return (type, value);
                        break;
                    }
                }
            }

            yield break;
        }

        if (raw is JsonElement json && json.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in json.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var type = GetStringValue(item, "type");
                var value = GetIntValue(item, "value");
                if (type.Length > 0)
                    yield return (type, value);
            }
        }
    }

    private static (int StatusRolls, bool HasUnsupportedOptions) ResolveStatusContribution(CalcResult? more)
    {
        if (more == null)
            return (0, false);

        var details = more.Details ?? new Dictionary<string, object?>();
        if (HasUnsupportedMoreModifiers(details))
            return (0, true);

        if (!TryGetString(details, "status", out var status) || status.Length == 0)
            return (0, false);

        var normalized = NormalizeToken(status);
        if (normalized.Contains("masterstatus", StringComparison.OrdinalIgnoreCase))
            return (10, false);
        if (normalized.Contains("journeymenstatus", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("journeymanstatus", StringComparison.OrdinalIgnoreCase))
        {
            return (6, false);
        }
        if (normalized.Contains("apprenticestatus", StringComparison.OrdinalIgnoreCase))
            return (3, false);

        return (0, false);
    }

    private static bool HasUnsupportedMoreModifiers(Dictionary<string, object?> details)
    {
        if (TryGetBool(details, "animate", out var animate) && animate)
            return true;
        if (TryGetBool(details, "beneficiallyInseparable", out var inseparable) && inseparable)
            return true;
        if (TryGetBool(details, "activatesOnCondition", out var condition) && condition)
            return true;
        if (TryGetBool(details, "activateOoc", out var ooc) && ooc)
            return true;
        if (TryGetInt(details, "additionalBlowUpMonths", out var months) && months > 0)
            return true;
        if (TryGetBool(details, "chosenPersonFiveMinutes", out var chosenPerson) && chosenPerson)
            return true;
        if (TryGetBool(details, "alignmentOrBracketUse", out var bracket) && bracket)
            return true;
        if (TryGetInt(details, "callsToHandPerDay", out var calls) && calls > 0)
            return true;
        if (TryGetBool(details, "grantsUtiliseSpecificType", out var utilise) && utilise)
            return true;
        if (TryGetBool(details, "noBlowUpFirstDeath", out var noFirstDeath) && noFirstDeath)
            return true;
        if (TryGetBool(details, "noBlowUpDeath", out var noDeath) && noDeath)
            return true;
        if (TryGetBool(details, "useOnceEver", out var onceEver) && onceEver)
            return true;
        if (TryGetBool(details, "closedGuildAllMembers", out var closedGuild) && closedGuild)
            return true;
        if (TryGetBool(details, "guildAnyMemberUse", out var anyMemberGuild) && anyMemberGuild)
            return true;
        if (TryGetBool(details, "guildAllMembersBenefit", out var allMemberGuild) && allMemberGuild)
            return true;

        if (TryGetDouble(details, "combinedMultiplier", out var multiplier) && Math.Abs(multiplier - 1d) > 0.0001d)
            return true;

        return false;
    }

    private static bool HasUnsupportedKeys(Dictionary<string, object?> details, HashSet<string> allowed)
    {
        foreach (var pair in details)
        {
            if (allowed.Contains(pair.Key))
                continue;

            if (IsMeaningfulValue(pair.Value))
                return true;
        }

        return false;
    }

    private static bool IsMeaningfulValue(object? value)
    {
        if (value == null)
            return false;

        return value switch
        {
            string text => !string.IsNullOrWhiteSpace(text),
            bool flag => flag,
            int number => number != 0,
            long number => number != 0,
            double number => Math.Abs(number) > 0.0001d,
            float number => Math.Abs(number) > 0.0001f,
            decimal number => number != 0,
            IEnumerable<object> list => list.Any(IsMeaningfulValue),
            JsonElement json => IsMeaningfulJson(json),
            _ => true
        };
    }

    private static bool IsMeaningfulJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => false,
            JsonValueKind.False => false,
            JsonValueKind.True => true,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
            JsonValueKind.Number => element.TryGetInt64(out var asInt)
                                    ? asInt != 0
                                    : element.TryGetDouble(out var asDouble) && Math.Abs(asDouble) > 0.0001d,
            JsonValueKind.Array => element.EnumerateArray().Any(IsMeaningfulJson),
            JsonValueKind.Object => element.EnumerateObject().Any(prop => IsMeaningfulJson(prop.Value)),
            _ => true
        };
    }

    private static bool TryGetString(Dictionary<string, object?> source, string key, out string value)
    {
        value = string.Empty;
        if (!source.TryGetValue(key, out var raw) || raw == null)
            return false;

        if (raw is string text)
        {
            value = text.Trim();
            return value.Length > 0;
        }

        if (raw is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.String)
            {
                value = (json.GetString() ?? string.Empty).Trim();
                return value.Length > 0;
            }

            if (json.ValueKind == JsonValueKind.Number)
            {
                value = json.ToString().Trim();
                return value.Length > 0;
            }
        }

        value = raw.ToString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetInt(Dictionary<string, object?> source, string key, out int value)
    {
        value = 0;
        if (!source.TryGetValue(key, out var raw) || raw == null)
            return false;

        return TryConvertToInt(raw, out value);
    }

    private static bool TryGetBool(Dictionary<string, object?> source, string key, out bool value)
    {
        value = false;
        if (!source.TryGetValue(key, out var raw) || raw == null)
            return false;

        return TryConvertToBool(raw, out value);
    }

    private static bool TryGetDouble(Dictionary<string, object?> source, string key, out double value)
    {
        value = 0;
        if (!source.TryGetValue(key, out var raw) || raw == null)
            return false;

        return TryConvertToDouble(raw, out value);
    }

    private static bool TryConvertToInt(object raw, out int value)
    {
        value = raw switch
        {
            int i => i,
            long l => (int)l,
            float f => (int)Math.Round(f, MidpointRounding.AwayFromZero),
            double d => (int)Math.Round(d, MidpointRounding.AwayFromZero),
            decimal m => (int)Math.Round(m, MidpointRounding.AwayFromZero),
            _ => 0
        };

        if (raw is string text && int.TryParse(text.Trim(), out var parsed))
        {
            value = parsed;
            return true;
        }

        if (raw is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number)
            {
                if (json.TryGetInt32(out var parsedInt))
                {
                    value = parsedInt;
                    return true;
                }

                if (json.TryGetDouble(out var asDouble))
                {
                    value = (int)Math.Round(asDouble, MidpointRounding.AwayFromZero);
                    return true;
                }
            }

            if (json.ValueKind == JsonValueKind.String
                && int.TryParse((json.GetString() ?? string.Empty).Trim(), out var fromString))
            {
                value = fromString;
                return true;
            }
        }

        return raw is int or long or float or double or decimal;
    }

    private static bool TryConvertToBool(object raw, out bool value)
    {
        value = raw switch
        {
            bool flag => flag,
            int i => i != 0,
            long l => l != 0,
            _ => false
        };

        if (raw is string text && bool.TryParse(text.Trim(), out var parsed))
        {
            value = parsed;
            return true;
        }

        if (raw is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.True || json.ValueKind == JsonValueKind.False)
            {
                value = json.GetBoolean();
                return true;
            }

            if (json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var asInt))
            {
                value = asInt != 0;
                return true;
            }

            if (json.ValueKind == JsonValueKind.String
                && bool.TryParse((json.GetString() ?? string.Empty).Trim(), out var asBool))
            {
                value = asBool;
                return true;
            }
        }

        return raw is bool or int or long;
    }

    private static bool TryConvertToDouble(object raw, out double value)
    {
        value = raw switch
        {
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            decimal m => (double)m,
            _ => 0d
        };

        if (raw is string text && double.TryParse(text.Trim(), out var parsed))
        {
            value = parsed;
            return true;
        }

        if (raw is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var asDouble))
            {
                value = asDouble;
                return true;
            }

            if (json.ValueKind == JsonValueKind.String
                && double.TryParse((json.GetString() ?? string.Empty).Trim(), out var fromString))
            {
                value = fromString;
                return true;
            }
        }

        return raw is int or long or float or double or decimal;
    }

    private static string GetStringValue(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var raw) || raw == null)
            return string.Empty;

        if (raw is string text)
            return text.Trim();

        if (raw is JsonElement json)
            return json.ValueKind == JsonValueKind.String
                ? (json.GetString() ?? string.Empty).Trim()
                : json.ToString().Trim();

        return raw.ToString()?.Trim() ?? string.Empty;
    }

    private static string GetStringValue(JsonElement source, string key)
    {
        if (!source.TryGetProperty(key, out var prop))
            return string.Empty;

        return prop.ValueKind == JsonValueKind.String
            ? (prop.GetString() ?? string.Empty).Trim()
            : prop.ToString().Trim();
    }

    private static int GetIntValue(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var raw) || raw == null)
            return 0;

        return TryConvertToInt(raw, out var parsed) ? parsed : 0;
    }

    private static int GetIntValue(JsonElement source, string key)
    {
        if (!source.TryGetProperty(key, out var prop))
            return 0;

        if (prop.ValueKind == JsonValueKind.Number)
        {
            if (prop.TryGetInt32(out var parsed))
                return parsed;
            if (prop.TryGetDouble(out var asDouble))
                return (int)Math.Round(asDouble, MidpointRounding.AwayFromZero);
        }

        if (prop.ValueKind == JsonValueKind.String
            && int.TryParse((prop.GetString() ?? string.Empty).Trim(), out var fromString))
        {
            return fromString;
        }

        return 0;
    }

    private static bool IsType(CalcResult? result, string type)
        => result != null
           && (result.AbilityType ?? string.Empty).Trim().Equals(type, StringComparison.OrdinalIgnoreCase);

    private static string ResolveFallbackLabel(CalcResult? ability)
    {
        if (ability == null)
            return "Ability";

        var type = (ability.AbilityType ?? string.Empty).Trim();
        var name = (ability.AbilityName ?? string.Empty).Trim();

        if (type.Length == 0 && name.Length == 0)
            return "Ability";
        if (name.Length == 0)
            return type;
        if (type.Length == 0)
            return name;

        return $"{type} ({name})";
    }

    private static string NormalizeToken(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        var chars = text
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars).ToLowerInvariant();
    }
}
