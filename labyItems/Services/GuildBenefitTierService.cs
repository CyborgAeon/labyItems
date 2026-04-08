using labyItems.Helpers;
using labyItems.Models.Characters;

namespace labyItems.Services;

public enum GuildBenefitTier
{
    Basic = 0,
    Intermediate = 1,
    Advanced = 2
}

public readonly record struct GuildBenefitTierTables(int Basic, int Intermediate, int Advanced)
{
    public int GetRequiredTable(GuildBenefitTier tier)
        => tier switch
        {
            GuildBenefitTier.Basic => Basic,
            GuildBenefitTier.Intermediate => Intermediate,
            GuildBenefitTier.Advanced => Advanced,
            _ => Basic
        };
}

public static class GuildBenefitTierService
{
    public static GuildBenefitTierTables ResolveTierTables(CharacterDraft draft, string? guildType)
    {
        var race = (draft?.Race ?? string.Empty).Trim();
        var subtype = (draft?.RaceSubtypeValue ?? draft?.RaceSubtype ?? string.Empty).Trim();
        var type = (guildType ?? string.Empty).Trim();

        if (race.Equals("Human", StringComparison.OrdinalIgnoreCase)
            && subtype.Equals("Mourat", StringComparison.OrdinalIgnoreCase))
        {
            return new GuildBenefitTierTables(8, 10, 11);
        }

        if (race.Equals("Wyrm-Kin", StringComparison.OrdinalIgnoreCase))
            return new GuildBenefitTierTables(1, 4, 9);

        if (race.Equals("Human", StringComparison.OrdinalIgnoreCase)
            && subtype.Equals("Forgotten", StringComparison.OrdinalIgnoreCase)
            && type.Equals("political", StringComparison.OrdinalIgnoreCase))
        {
            return new GuildBenefitTierTables(2, 4, 9);
        }

        return new GuildBenefitTierTables(1, 3, 8);
    }

    public static bool IsTierAvailable(CharacterDraft draft, string? guildType, GuildBenefitTier tier)
    {
        var tables = ResolveTierTables(draft, guildType);
        var requiredTable = tables.GetRequiredTable(tier);
        var points = Math.Max(0, draft?.Points ?? 0);
        return CharacterProgressionTables.HasReachedTable(points, requiredTable);
    }
}
