using labyItems.Controls;
using labyItems.Models.Enums;

namespace labyItems.Helpers;

public static class CombatQuickFactsFormatter
{
    public static CombatQuickFactsVm Build(
        bool hasObject,
        IReadOnlyList<int[]> amounts,
        IReadOnlyList<string> types,
        IReadOnlyList<DamageTypeEnum>? damageTypes = null,
        IReadOnlyList<int[]>? armourApplies = null,
        string? armourType = null,
        bool isHealing = false)
    {
        if (!hasObject)
            return CombatQuickFactsVm.Empty;
        if (isHealing && amounts.Count == 0)
            return CombatQuickFactsVm.Empty;

        var valueLabel = isHealing ? "HEALING" : "DAMAGE";
        var normalizedTypes = types.Select(NormalizeType).ToList();
        var hasBlastType = normalizedTypes.Any(type => type.Equals("blast", StringComparison.OrdinalIgnoreCase));
        var amountTotal = amounts.Sum(amount => Math.Abs(At(amount, 0)) + Math.Abs(At(amount, 1)));
        string valueText;
        string typeText;
        var isWide = false;

        if ((amounts.Count == 0 || amountTotal == 0) && !hasBlastType)
        {
            valueText = "Sever";
            typeText = normalizedTypes.FirstOrDefault() ?? string.Empty;
        }
        else if (amounts.Count == 0)
        {
            return CombatQuickFactsVm.Empty;
        }
        else if (AllAmountsEqual(amounts) && AllEffectiveTypesEqual(normalizedTypes))
        {
            valueText = FormatAmount(amounts[0]) + (amounts.Count > 1 ? $" x {amounts.Count}" : string.Empty);
            typeText = normalizedTypes.FirstOrDefault() ?? string.Empty;
        }
        else
        {
            valueText = BuildMixedNotation(amounts, normalizedTypes);
            typeText = string.Empty;
            isWide = true;
        }

        var damageTypeText = string.Join(", ", (damageTypes ?? Array.Empty<DamageTypeEnum>())
            .Select(value => value.ToDisplayText())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        if (damageTypeText.Length > 0)
            valueText = $"{valueText} {damageTypeText}";

        var armourText = BuildArmourText(armourApplies, armourType);
        return new CombatQuickFactsVm(valueLabel, valueText, typeText, armourText, isWide);
    }

    private static string BuildMixedNotation(IReadOnlyList<int[]> amounts, IReadOnlyList<string> types)
    {
        var parts = new List<string>();
        var consumed = new HashSet<int>();
        for (var i = 0; i < amounts.Count; i++)
        {
            if (consumed.Contains(i)) continue;
            var amount = amounts[i];
            var type = TypeAt(types, i);
            if (At(amount, 0) == 0 && At(amount, 1) > 0 && IsNamedLocation(type))
            {
                var locations = new List<string>();
                for (var j = i; j < amounts.Count; j++)
                {
                    if (At(amounts[j], 0) == 0 && At(amounts[j], 1) == At(amount, 1) && IsNamedLocation(TypeAt(types, j)))
                    {
                        locations.Add(ToTitleCase(TypeAt(types, j)));
                        consumed.Add(j);
                    }
                }
                locations = locations
                    .OrderBy(LocationOrder)
                    .ThenBy(location => location, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                parts.Add($"{At(amount, 1)} to {string.Join(", ", locations)}");
                continue;
            }

            consumed.Add(i);
            var suffix = type.Length == 0 ? string.Empty : $" {type}";
            parts.Add($"{FormatAmount(amount)}{suffix}");
        }
        return string.Join("; ", parts);
    }

    private static string BuildArmourText(IReadOnlyList<int[]>? armour, string? armourType)
    {
        if (armour == null || armour.Count == 0) return string.Empty;
        if (armour.All(pair => At(pair, 0) == 0 && At(pair, 1) == 0)) return "does not apply";
        var rate = AllAmountsEqual(armour)
            ? FormatAmount(armour[0]) + (armour.Count > 1 ? $"x{armour.Count}" : string.Empty)
            : string.Join("; ", armour.Select(FormatAmount));
        var type = FormatArmourType(armourType);
        return type.Length == 0 ? rate : $"{type} {rate}";
    }

    private static string FormatArmourType(string? raw) => (raw ?? string.Empty).Trim() switch
    {
        var value when value.Equals("InnatePac", StringComparison.OrdinalIgnoreCase) => "Innate PAC",
        var value when value.Equals("SAC", StringComparison.OrdinalIgnoreCase) => "SAC",
        var value when value.Equals("MAC", StringComparison.OrdinalIgnoreCase) => "MAC",
        var value => value
    };

    private static bool AllAmountsEqual(IReadOnlyList<int[]> values) => values.Count > 0 && values.All(value => At(value, 0) == At(values[0], 0) && At(value, 1) == At(values[0], 1));
    private static bool AllEffectiveTypesEqual(IReadOnlyList<string> types) => types.Count <= 1 || types.All(type => type.Equals(types[0], StringComparison.OrdinalIgnoreCase));
    private static string FormatAmount(int[] value) => $"{At(value, 0)}/{At(value, 1)}";
    private static int At(int[]? value, int index) => value != null && value.Length > index ? value[index] : 0;
    private static string TypeAt(IReadOnlyList<string> types, int index) => types.Count == 0 ? string.Empty : index < types.Count ? types[index] : types[^1];
    private static bool IsNamedLocation(string type) => type.Length > 0 && type is not "blast" and not "target loc" and not "worst loc";
    private static string NormalizeType(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch { "missile" => "target loc", "worst" => "worst loc", "blast" => "blast", var value => value };
    private static string ToTitleCase(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
    private static int LocationOrder(string location) => location.ToLowerInvariant() switch
    {
        "chest" => 0,
        "head" => 1,
        "abdomen" => 2,
        _ => 10
    };
}
