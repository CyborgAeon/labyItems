using System;

namespace labyItems.Services;

public static class BattleboardDamageMitigationService
{
    public static (int Tblp, int Loc) ApplyPostArmourMitigation(
        int tblp,
        int loc,
        string? channel,
        Func<string, int>? getResistanceMultiplier,
        Func<string, int>? getDamagePerSixthLevel,
        Func<string, bool>? hasInfiniteResistance,
        bool isMiracleSource,
        bool isMantic)
    {
        tblp = Math.Max(0, tblp);
        loc = Math.Max(0, loc);

        var normalizedChannel = NormalizeResistanceChannel(channel);
        var spiritless = hasInfiniteResistance?.Invoke("Spirit") == true;
        var mindless = hasInfiniteResistance?.Invoke("Neuro") == true;

        // Spiritless: immune to all non-mantic miracles.
        if (spiritless && isMiracleSource && !isMantic)
            return (0, 0);

        // Spiritless: immune to spirit channel damage unless it is the mantic miracle exception.
        if (spiritless
            && normalizedChannel.Equals("Spirit", StringComparison.OrdinalIgnoreCase)
            && !(isMiracleSource && isMantic))
        {
            return (0, 0);
        }

        // Mindless: immune to neuronic damage.
        if (mindless && normalizedChannel.Equals("Neuro", StringComparison.OrdinalIgnoreCase))
            return (0, 0);

        if (normalizedChannel.Equals("Magic", StringComparison.OrdinalIgnoreCase))
        {
            var multiplier = getResistanceMultiplier?.Invoke("Magic") ?? 1;
            if (multiplier > 1)
            {
                tblp = DivideAndRoundUp(tblp, multiplier);
                loc = DivideAndRoundUp(loc, multiplier);
            }
        }
        else if (normalizedChannel.Equals("Spirit", StringComparison.OrdinalIgnoreCase))
        {
            var multiplier = getResistanceMultiplier?.Invoke("Spirit") ?? 1;
            if (multiplier > 1)
            {
                tblp = DivideAndRoundUp(tblp, multiplier);
                loc = DivideAndRoundUp(loc, multiplier);
            }
        }

        var reductionChannel = NormalizePerSixthDamageChannel(normalizedChannel);
        var perSixthLevel = Math.Clamp(getDamagePerSixthLevel?.Invoke(reductionChannel) ?? 0, 0, 6);
        if (perSixthLevel >= 6)
            return (0, 0);

        if (perSixthLevel > 0)
        {
            tblp = ApplyPerSixthReduction(tblp, perSixthLevel);
            loc = ApplyPerSixthReduction(loc, perSixthLevel);
        }

        return (tblp, loc);
    }

    public static string NormalizeResistanceChannel(string? channel)
    {
        var raw = (channel ?? string.Empty).Trim();
        if (raw.Length == 0)
            return string.Empty;

        if (raw.Contains("magic", StringComparison.OrdinalIgnoreCase))
            return "Magic";
        if (raw.Contains("spirit", StringComparison.OrdinalIgnoreCase))
            return "Spirit";
        if (raw.Contains("neur", StringComparison.OrdinalIgnoreCase))
            return "Neuro";

        return raw;
    }

    private static int DivideAndRoundUp(int value, int divisor)
    {
        if (value <= 0)
            return 0;

        var safeDivisor = Math.Max(1, divisor);
        return (int)Math.Ceiling(value / (double)safeDivisor);
    }

    private static int ApplyPerSixthReduction(int value, int level)
    {
        if (value <= 0)
            return 0;

        var clampedLevel = Math.Clamp(level, 0, 6);
        if (clampedLevel <= 0)
            return value;
        if (clampedLevel >= 6)
            return 0;

        var numerator = Math.Max(0, 6 - clampedLevel);
        return (int)Math.Ceiling(value * (numerator / 6.0));
    }

    private static string NormalizePerSixthDamageChannel(string normalizedChannel)
    {
        if (normalizedChannel.Equals("Magic", StringComparison.OrdinalIgnoreCase))
            return "Magic";
        if (normalizedChannel.Equals("Spirit", StringComparison.OrdinalIgnoreCase))
            return "Spirit";
        if (normalizedChannel.Equals("Neuro", StringComparison.OrdinalIgnoreCase))
            return "Neuro";
        if (normalizedChannel.Equals("Physical", StringComparison.OrdinalIgnoreCase))
            return "Physical";

        // Unspecified channels in manual/preset flows are physical blows by default.
        return "Physical";
    }
}
