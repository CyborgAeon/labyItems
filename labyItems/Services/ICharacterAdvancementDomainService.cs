using System.Collections.Generic;
using labyItems.Models.Characters;

namespace labyItems.Services;

public interface ICharacterAdvancementDomainService
{
    int GetScriptureTablesReached(int points);
    IReadOnlyList<AbilityPointSpendLine> BuildAbilityPointSpendLines(
        IEnumerable<string> selectedAbilityNames,
        IReadOnlyDictionary<string, int> abilityCostByName);
    int ComputeAbilityPointsSpent(
        IEnumerable<string> selectedAbilityNames,
        IReadOnlyDictionary<string, int> abilityCostByName);
    string NormalizeAlignmentToken(string? value);
    HashSet<string> GetAllowedMiracleAlignments(
        Alignment? alignment,
        IEnumerable<MiracleListEntryDraft> entries,
        bool lockTrueNeutral);
    bool AreMiracleEntriesAlignmentCompatible(
        Alignment? alignment,
        IEnumerable<MiracleListEntryDraft> entries);
    MiraclePointTotals ComputeMiraclePointTotals(IEnumerable<MiracleListEntryDraft> entries);
    EvocationPointTotals ComputeEvocationPointTotals(IEnumerable<EvocationListEntryDraft> entries);
    bool AdvancedEvocationsShareAField(
        IEnumerable<EvocationListEntryDraft> entries,
        Func<string, IReadOnlyCollection<string>?> resolveFieldsByName);
}

public readonly record struct AbilityPointSpendLine(string Name, int Cost, int RunningTotal);
public readonly record struct MiraclePointTotals(int Good, int Neutral, int Evil, int Total, int Advanced);
public readonly record struct EvocationPointTotals(int Total, int Advanced);
