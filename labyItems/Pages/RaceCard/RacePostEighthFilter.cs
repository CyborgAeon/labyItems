using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.RaceCard;

public static class RacePostEighthFilter
{
    private static readonly HashSet<string> RaceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "race", "races", "baserace", "baseraces", "peopletype", "peopletypes",
        "racetag", "racetags", "specialisation", "specialisations", "specialization", "specializations"
    };

    public static IReadOnlyList<EvolutionService.AbilityResult> Filter(IEnumerable<EvolutionService.AbilityResult> abilities, CharacterDraft raceDraft, IReadOnlyDictionary<string, CharacterClassRecord> classes, IReadOnlyDictionary<string, PeopleRecord> races, IAbilityAvailabilityService availabilityService) => abilities
        .Where(ability => IsSpecificallyAvailable(ability, raceDraft, classes, races, availabilityService))
        .OrderBy(ability => ability.Table)
        .ThenBy(ability => ability.Cost)
        .ThenBy(ability => ability.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(ability => ability.Index, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static bool IsSpecificallyAvailable(EvolutionService.AbilityResult ability, CharacterDraft draft, IReadOnlyDictionary<string, CharacterClassRecord> classes, IReadOnlyDictionary<string, PeopleRecord> races, IAbilityAvailabilityService availabilityService)
    {
        var raceRules = ability.AvailabilityRules
            .Where(rule => rule != null && rule.IsValid && RaceFields.Contains(Normalize(rule.Field)))
            .ToList();

        // A race card is not a catalogue of universally available or class-only abilities.
        if (!raceRules.Any(rule => rule.Operator is Models.Rules.RuleComparisonOp.In or Models.Rules.RuleComparisonOp.Only))
            return false;

        return availabilityService.IsAvailable(raceRules, draft, classes, races);
    }

    private static string Normalize(string value) => new((value ?? string.Empty)
        .Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant)
        .ToArray());
}
