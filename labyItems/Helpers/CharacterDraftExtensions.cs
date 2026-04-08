using labyItems.Models.Characters;

namespace labyItems.Helpers;

public static class CharacterDraftExtensions
{
    public static string ResolveRaceKeyForLifeScale(this CharacterDraft draft)
    {
        if (draft == null)
            return string.Empty;

        var race = (draft.Race ?? string.Empty).Trim();
        if (!race.Equals("Elf", StringComparison.OrdinalIgnoreCase))
            return race;

        var subtype = (draft.RaceSubtype ?? string.Empty).Trim();
        if (subtype.Equals("Winter", StringComparison.OrdinalIgnoreCase))
            return "Winter Elf";

        if (subtype.Equals("Summer", StringComparison.OrdinalIgnoreCase))
            return "Drowe";

        return "Elf";
    }
}
