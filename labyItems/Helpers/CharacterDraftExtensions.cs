using System;
using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Characters;
using labyItems.Models.Enums;

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

    public static MagicColours? TryGetWizardColour(this CharacterDraft? draft)
    {
        if (draft?.SpecialisationSelections != null)
        {
            var kvp = draft.SpecialisationSelections.FirstOrDefault(x =>
                x.Key.Contains("Wizard Colour", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(x.Value));

            if (!string.IsNullOrWhiteSpace(kvp.Value)
                && Enum.TryParse<MagicColours>(kvp.Value.Trim().Replace(" ", string.Empty), true, out var colour))
            {
                return colour;
            }
        }

        if (draft?.Abilities != null)
        {
            var ability = draft.Abilities.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a?.Source)
                && a.Source.Contains("Specialisation:Wizard Colour", StringComparison.OrdinalIgnoreCase));

            var name = ability?.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name)
                && Enum.TryParse<MagicColours>(name.Trim().Replace(" ", string.Empty), true, out var fromAbility))
            {
                return fromAbility;
            }
        }

        return null;
    }
}
