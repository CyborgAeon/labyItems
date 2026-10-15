using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.ClassCard;

public static class ClassPostEighthFilter
{
    private static readonly HashSet<string> ClassFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "classes", "sourceclass", "sourceclasses", "firstclass", "firstclasses",
        "originalclass", "originalclasses", "anyclass", "anyclasses", "bracket", "brackets",
        "firstclassbracket", "firstclassbrackets", "anyclassbracket", "anyclassbrackets"
    };

    public static IReadOnlyList<EvolutionService.AbilityResult> Filter(
        IEnumerable<EvolutionService.AbilityResult> abilities,
        CharacterDraft classDraft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races,
        IAbilityAvailabilityService availabilityService) => abilities
        .Where(ability => IsSpecificallyAvailable(ability, classDraft, classes, races, availabilityService))
        .OrderBy(ability => ability.Table)
        .ThenBy(ability => ability.Cost)
        .ThenBy(ability => ability.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(ability => ability.Index, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static bool IsSpecificallyAvailable(
        EvolutionService.AbilityResult ability,
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races,
        IAbilityAvailabilityService availabilityService)
    {
        var classRules = ability.AvailabilityRules
            .Where(rule => rule != null && rule.IsValid && ClassFields.Contains(Normalize(rule.Field)))
            .ToList();
        if (!classRules.Any(rule => rule.Operator is Models.Rules.RuleComparisonOp.In or Models.Rules.RuleComparisonOp.Only))
            return false;
        return availabilityService.IsAvailable(classRules, draft, classes, races);
    }

    private static string Normalize(string value) => new((value ?? string.Empty)
        .Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant)
        .ToArray());
}
