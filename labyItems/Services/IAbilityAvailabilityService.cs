using labyItems.Models.Characters;
using labyItems.Models.Rules;

namespace labyItems.Services;

public interface IAbilityAvailabilityService
{
    bool IsAvailable(
        IReadOnlyList<RuleClause>? rules,
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races);
}
