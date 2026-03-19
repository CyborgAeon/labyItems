using System.Text.Json;
using System.Text.RegularExpressions;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Characters;

namespace labyItems.Services;

public static class BattleboardArmourCalculator
{
    private static readonly Regex ArmourValueRegex = new(
        @"([+-]?\d+)\s*(PAC|DAC|MAC|SAC)",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex AcRegex = new(
        @"\bAC\s*[:=]?\s*(\d+)\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    public static BattleboardArmourTotals Calculate(
        CharacterDraft? draft,
        IEnumerable<Item>? assignedItems = null)
    {
        var character = draft ?? new CharacterDraft();
        var items = (assignedItems ?? ResolveAssignedItems(character)).ToList();
        var extraBonuses = ResolveNonArmourBonuses(items);
        var tier = ResolveArmourTier(character);
        var maxWearablePac = GetMaxTotalPacForTier(tier);
        var baseWornPac = Clamp(Math.Max(0, character.WornArmour), 0, maxWearablePac);

        if (tier == ArmourTier.None || maxWearablePac <= 0)
        {
            return new BattleboardArmourTotals(
                WornPac: Math.Max(0, extraBonuses.Pac),
                ItemDac: Math.Max(0, extraBonuses.Dac),
                ItemMac: Math.Max(0, extraBonuses.Mac),
                ItemSac: Math.Max(0, extraBonuses.Sac),
                ArmourAvailability: tier.ToString());
        }

        var bestItem = ResolveBestWearableItem(items, maxWearablePac);
        if (bestItem == null)
        {
            return new BattleboardArmourTotals(
                WornPac: Math.Max(0, baseWornPac + extraBonuses.Pac),
                ItemDac: Math.Max(0, extraBonuses.Dac),
                ItemMac: Math.Max(0, extraBonuses.Mac),
                ItemSac: Math.Max(0, extraBonuses.Sac),
                ArmourAvailability: tier.ToString());
        }

        var chosenItem = bestItem.Value;
        var shouldUseItem = chosenItem.TotalPac > baseWornPac
                            || (chosenItem.TotalPac == baseWornPac
                                && (chosenItem.Dac > 0 || chosenItem.Mac > 0 || chosenItem.Sac > 0));

        if (!shouldUseItem)
        {
            return new BattleboardArmourTotals(
                WornPac: Math.Max(0, baseWornPac + extraBonuses.Pac),
                ItemDac: Math.Max(0, extraBonuses.Dac),
                ItemMac: Math.Max(0, extraBonuses.Mac),
                ItemSac: Math.Max(0, extraBonuses.Sac),
                ArmourAvailability: tier.ToString());
        }

        return new BattleboardArmourTotals(
            WornPac: Math.Max(0, chosenItem.TotalPac + extraBonuses.Pac),
            ItemDac: Math.Max(0, chosenItem.Dac + extraBonuses.Dac),
            ItemMac: Math.Max(0, chosenItem.Mac + extraBonuses.Mac),
            ItemSac: Math.Max(0, chosenItem.Sac + extraBonuses.Sac),
            ArmourAvailability: tier.ToString());
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

    private static ArmourItemContribution? ResolveBestWearableItem(IEnumerable<Item> items, int maxWearablePac)
    {
        ArmourItemContribution? best = null;

        foreach (var item in items ?? Enumerable.Empty<Item>())
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            var abilities = payload?.Item?.Abilities ?? new List<CalcResult>();

            foreach (var ability in abilities)
            {
                if (!IsArmourAbility(ability))
                    continue;

                var basePac = ExtractBasePac(ability);
                if (basePac <= 0)
                    continue;

                if (!TryParseItemContribution(ability, out var contribution))
                    continue;

                if (contribution.BasePac > maxWearablePac)
                    continue;

                if (best == null || contribution.IsBetterThan(best.Value))
                    best = contribution;
            }
        }

        return best;
    }

    private static (int Pac, int Dac, int Mac, int Sac) ResolveNonArmourBonuses(IEnumerable<Item> items)
    {
        var totals = (Pac: 0, Dac: 0, Mac: 0, Sac: 0);

        foreach (var item in items ?? Enumerable.Empty<Item>())
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            var abilities = payload?.Item?.Abilities ?? new List<CalcResult>();

            foreach (var ability in abilities)
            {
                if (ability == null)
                    continue;

                if (IsArmourAbility(ability) && ExtractBasePac(ability) > 0)
                    continue;

                var bonus = ExtractEnhancementBonuses(ability);
                totals.Pac += Math.Max(0, bonus.Pac);
                totals.Dac += Math.Max(0, bonus.Dac);
                totals.Mac += Math.Max(0, bonus.Mac);
                totals.Sac += Math.Max(0, bonus.Sac);
            }
        }

        return totals;
    }

    private static bool TryParseItemContribution(CalcResult ability, out ArmourItemContribution contribution)
    {
        contribution = default;
        if (ability == null)
            return false;

        var basePac = ExtractBasePac(ability);
        var bonus = ExtractEnhancementBonuses(ability);

        contribution = new ArmourItemContribution(
            BasePac: basePac,
            TotalPac: Math.Max(0, basePac + bonus.Pac),
            Dac: Math.Max(0, bonus.Dac),
            Mac: Math.Max(0, bonus.Mac),
            Sac: Math.Max(0, bonus.Sac));

        return contribution.TotalPac > 0 || contribution.Dac > 0 || contribution.Mac > 0 || contribution.Sac > 0;
    }

    private static int ExtractBasePac(CalcResult ability)
    {
        if (TryGetDetailInt(ability, "AC", out var ac))
            return Math.Max(0, ac);

        if (TryGetDetailString(ability, "layeredSummary", out var layered)
            && TryReadAcFromText(layered, out var layeredAc))
        {
            return Math.Max(0, layeredAc);
        }

        if (TryReadAcFromText(ability.Summary, out var summaryAc))
            return Math.Max(0, summaryAc);

        if (TryReadAcFromText(ability.AbilityName, out var namedAc))
            return Math.Max(0, namedAc);

        var byMaterial = ResolveBasePacFromMaterial(ability);
        return Math.Max(0, byMaterial);
    }

    private static (int Pac, int Dac, int Mac, int Sac) ExtractEnhancementBonuses(CalcResult ability)
    {
        var totals = (Pac: 0, Dac: 0, Mac: 0, Sac: 0);
        var foundStructured = false;

        if (ability?.Details == null)
            return totals;

        if (TryGetDetailValue(ability, "enhancementBonuses", out var rawEnhancement))
        {
            foundStructured = TryAccumulateEnhancementBonuses(rawEnhancement, ref totals);
        }

        if (!foundStructured)
        {
            if (TryGetDetailInt(ability, "pac", out var pac))
                totals.Pac += Math.Max(0, pac);
            if (TryGetDetailInt(ability, "dac", out var dac))
                totals.Dac += Math.Max(0, dac);
            if (TryGetDetailInt(ability, "mac", out var mac))
                totals.Mac += Math.Max(0, mac);
            if (TryGetDetailInt(ability, "sac", out var sac))
                totals.Sac += Math.Max(0, sac);
        }

        if (totals.Pac == 0 && totals.Dac == 0 && totals.Mac == 0 && totals.Sac == 0)
        {
            totals = ParseArmourTokens(ability.Summary);
            if (totals.Pac == 0 && totals.Dac == 0 && totals.Mac == 0 && totals.Sac == 0)
                totals = ParseArmourTokens(ability.AbilityName);
        }

        return totals;
    }

    private static bool TryAccumulateEnhancementBonuses(object? rawEnhancement, ref (int Pac, int Dac, int Mac, int Sac) totals)
    {
        var foundAny = false;
        if (rawEnhancement is JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in json.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;

                var type = entry.TryGetProperty("type", out var typeElement)
                    ? (typeElement.GetString() ?? string.Empty)
                    : string.Empty;
                var value = entry.TryGetProperty("value", out var valueElement)
                    ? (TryGetJsonInt(valueElement, out var parsed) ? parsed : 0)
                    : 0;

                foundAny |= AddStat(type, value, ref totals);
            }

            return foundAny;
        }

        if (rawEnhancement is IEnumerable<object> values)
        {
            foreach (var value in values)
            {
                if (value is Dictionary<string, object?> dict)
                {
                    var type = dict.TryGetValue("type", out var t) ? t?.ToString() ?? string.Empty : string.Empty;
                    var amount = TryConvertToInt(dict.TryGetValue("value", out var v) ? v : null);
                    foundAny |= AddStat(type, amount, ref totals);
                    continue;
                }

                var typeProperty = value?.GetType().GetProperty("type");
                var valueProperty = value?.GetType().GetProperty("value");
                var reflectedType = typeProperty?.GetValue(value)?.ToString() ?? string.Empty;
                var reflectedValue = TryConvertToInt(valueProperty?.GetValue(value));
                foundAny |= AddStat(reflectedType, reflectedValue, ref totals);
            }
        }

        return foundAny;
    }

    private static bool AddStat(string type, int amount, ref (int Pac, int Dac, int Mac, int Sac) totals)
    {
        var safeAmount = Math.Max(0, amount);
        if (safeAmount == 0)
            return false;

        switch ((type ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "PAC":
                totals.Pac += safeAmount;
                return true;
            case "DAC":
                totals.Dac += safeAmount;
                return true;
            case "MAC":
                totals.Mac += safeAmount;
                return true;
            case "SAC":
                totals.Sac += safeAmount;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetDetailValue(CalcResult ability, string key, out object? value)
    {
        value = null;
        if (ability?.Details == null || ability.Details.Count == 0)
            return false;

        foreach (var pair in ability.Details)
        {
            if (!pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;
            value = pair.Value;
            return true;
        }

        return false;
    }

    private static bool TryGetDetailInt(CalcResult ability, string key, out int value)
    {
        value = 0;
        if (!TryGetDetailValue(ability, key, out var raw))
            return false;

        value = TryConvertToInt(raw);
        return true;
    }

    private static bool TryGetDetailString(CalcResult ability, string key, out string value)
    {
        value = string.Empty;
        if (!TryGetDetailValue(ability, key, out var raw) || raw == null)
            return false;

        if (raw is JsonElement element && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return value.Length > 0;
        }

        value = raw.ToString() ?? string.Empty;
        return value.Length > 0;
    }

    private static int TryConvertToInt(object? raw)
    {
        if (raw == null)
            return 0;

        if (raw is int i)
            return i;
        if (raw is long l && l >= int.MinValue && l <= int.MaxValue)
            return (int)l;
        if (raw is JsonElement element && TryGetJsonInt(element, out var jsonInt))
            return jsonInt;

        return int.TryParse(raw.ToString(), out var parsed) ? parsed : 0;
    }

    private static bool TryGetJsonInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            value = number;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), out var fromString))
        {
            value = fromString;
            return true;
        }

        return false;
    }

    private static (int Pac, int Dac, int Mac, int Sac) ParseArmourTokens(string? text)
    {
        var totals = (Pac: 0, Dac: 0, Mac: 0, Sac: 0);
        if (string.IsNullOrWhiteSpace(text))
            return totals;

        foreach (Match match in ArmourValueRegex.Matches(text))
        {
            if (!int.TryParse(match.Groups[1].Value, out var amount))
                continue;

            AddStat(match.Groups[2].Value, amount, ref totals);
        }

        return totals;
    }

    private static bool TryReadAcFromText(string? text, out int ac)
    {
        ac = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var match = AcRegex.Match(text);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedAc))
        {
            ac = parsedAc;
            return true;
        }

        return false;
    }

    private static int ResolveBasePacFromMaterial(CalcResult ability)
    {
        var candidates = new[]
        {
            ability?.AbilityName ?? string.Empty,
            ability?.Summary ?? string.Empty
        };

        foreach (var candidate in candidates)
        {
            var lower = candidate.ToLowerInvariant();
            if (lower.Contains("plate", StringComparison.Ordinal))
                return 8;
            if (lower.Contains("tight chain", StringComparison.Ordinal))
                return 7;
            if (lower.Contains("loose chain", StringComparison.Ordinal))
                return 6;
            if (lower.Contains("studded leather", StringComparison.Ordinal))
                return 5;
            if (lower.Contains("stiff leather", StringComparison.Ordinal))
                return 4;
            if (lower.Contains("leather", StringComparison.Ordinal))
                return 3;
        }

        return 0;
    }

    private static bool IsArmourAbility(CalcResult? ability)
    {
        var type = (ability?.AbilityType ?? string.Empty).Trim();
        return type.Equals("Armour", StringComparison.OrdinalIgnoreCase)
               || type.Equals("Armor", StringComparison.OrdinalIgnoreCase);
    }

    private static ArmourTier ResolveArmourTier(CharacterDraft character)
    {
        var parsed = ParseArmourTier(character?.ArmourAvailability);
        if (parsed.HasValue)
            return parsed.Value;

        var overrideTier = ParseArmourTier(character?.ArmourAvailabilityOverride);
        if (overrideTier.HasValue)
            return overrideTier.Value;

        if (HasNoArmourAbility(character?.Abilities))
            return ArmourTier.None;

        var inferred = InferArmourTierFromAbilities(character?.Abilities);
        if (inferred != ArmourTier.Unknown)
            return inferred;

        return InferArmourTierFromWornPac(character?.WornArmour ?? 0);
    }

    private static ArmourTier InferArmourTierFromWornPac(int wornPac)
    {
        if (wornPac >= 8)
            return ArmourTier.Heavy;
        if (wornPac >= 5)
            return ArmourTier.Medium;
        if (wornPac >= 3)
            return ArmourTier.Light;
        return ArmourTier.Heavy;
    }

    private static ArmourTier InferArmourTierFromAbilities(IEnumerable<AbilityDraft>? abilities)
    {
        var tier = ArmourTier.Unknown;
        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            var lower = (ability?.Name ?? string.Empty).ToLowerInvariant();
            if (lower.Length == 0)
                continue;

            if (lower.Contains("heavy armour") || lower.Contains("heavy armor"))
                return ArmourTier.Heavy;
            if (tier < ArmourTier.Medium && (lower.Contains("medium armour") || lower.Contains("medium armor")))
                tier = ArmourTier.Medium;
            if (tier < ArmourTier.Light && (lower.Contains("light armour") || lower.Contains("light armor")))
                tier = ArmourTier.Light;
        }

        return tier;
    }

    private static bool HasNoArmourAbility(IEnumerable<AbilityDraft>? abilities)
    {
        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            var lower = (ability?.Name ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("cannot wear armour")
                || lower.Contains("cannot wear armor")
                || lower.Contains("may not wear armour")
                || lower.Contains("may not wear armor")
                || lower.Contains("no armour")
                || lower.Contains("no armor"))
            {
                return true;
            }
        }

        return false;
    }

    private static ArmourTier? ParseArmourTier(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return null;

        if (Enum.TryParse<ArmourTier>(text, ignoreCase: true, out var parsed)
            && parsed != ArmourTier.Unknown)
        {
            return parsed;
        }

        return text.ToLowerInvariant() switch
        {
            "light armour" or "light armor" => ArmourTier.Light,
            "medium armour" or "medium armor" => ArmourTier.Medium,
            "heavy armour" or "heavy armor" => ArmourTier.Heavy,
            "none" => ArmourTier.None,
            _ => null
        };
    }

    private static int GetMaxTotalPacForTier(ArmourTier tier)
        => tier switch
        {
            ArmourTier.Light => 4,
            ArmourTier.Medium => 7,
            ArmourTier.Heavy => 11,
            _ => 0
        };

    private static int Clamp(int value, int min, int max)
    {
        if (max < min)
            return min;
        return Math.Max(min, Math.Min(value, max));
    }

    private readonly record struct ArmourItemContribution(
        int BasePac,
        int TotalPac,
        int Dac,
        int Mac,
        int Sac)
    {
        public bool IsBetterThan(ArmourItemContribution other)
        {
            if (TotalPac != other.TotalPac)
                return TotalPac > other.TotalPac;

            var thisSecondary = Dac + Mac + Sac;
            var otherSecondary = other.Dac + other.Mac + other.Sac;
            return thisSecondary > otherSecondary;
        }
    }

    private enum ArmourTier
    {
        Unknown = -1,
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3
    }
}

public readonly record struct BattleboardArmourTotals(
    int WornPac,
    int ItemDac,
    int ItemMac,
    int ItemSac,
    string ArmourAvailability);
