using labyItems.Models.Rules;

namespace labyItems.Pages.Characters.ViewModels;

public readonly record struct AbilityOption(
    string Name,
    int Cost,
    int Table,
    string Availability,
    IReadOnlyList<RuleClause> AvailabilityRules,
    string Description)
{
    public bool Equals(AbilityOption other)
        => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
           && Cost == other.Cost
           && Table == other.Table;

    public override int GetHashCode()
        => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? string.Empty), Cost, Table);
}
