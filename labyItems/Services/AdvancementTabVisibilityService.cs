using System;
using System.Collections.Generic;
using System.Linq;

namespace labyItems.Services;

public sealed class AdvancementTabVisibilityService : IAdvancementTabVisibilityService
{
    public AdvancementTabState Resolve(string? className, CharacterClassRecord? classRecord)
    {
        var trimmed = (className ?? string.Empty).Trim();
        var hasClassName = trimmed.Length > 0;

        var isWizard = HasBracket(classRecord?.Brackets, "Wizard")
                       || (hasClassName && (trimmed.Contains("Wizard", StringComparison.OrdinalIgnoreCase)
                                            || trimmed.Contains("Warlock", StringComparison.OrdinalIgnoreCase)
                                            || trimmed.Contains("Vivomancer", StringComparison.OrdinalIgnoreCase)));
        var isPriest = HasBracket(classRecord?.Brackets, "Priest")
                       || (hasClassName && trimmed.Contains("Priest", StringComparison.OrdinalIgnoreCase));
        var isDruid = HasBracket(classRecord?.Brackets, "Druid")
                      || (hasClassName && trimmed.Contains("Druid", StringComparison.OrdinalIgnoreCase));
        var hasEvilStairway = HasClassAbility(classRecord, "Evil stairway", "evil stairway list");

        return new AdvancementTabState(
            ShowSpellsTab: isWizard,
            ShowMiraclesTab: isPriest || hasEvilStairway,
            ShowPriestMiracleLists: isPriest,
            ShowEvilStairway: hasEvilStairway,
            ShowEvocationsTab: isDruid);
    }

    public AdvancementTabState ApplyNameFallback(string? className, AdvancementTabState current)
    {
        var name = (className ?? string.Empty).Trim();
        if (name.Length == 0)
            return current;

        var showSpells = current.ShowSpellsTab
                         || name.Contains("Wizard", StringComparison.OrdinalIgnoreCase)
                         || name.Contains("Warlock", StringComparison.OrdinalIgnoreCase)
                         || name.Contains("Vivomancer", StringComparison.OrdinalIgnoreCase);

        var showMiracles = current.ShowMiraclesTab
                           || name.Contains("Priest", StringComparison.OrdinalIgnoreCase)
                           || name.Contains("Vivomancer", StringComparison.OrdinalIgnoreCase);

        var showPriestMiracles = current.ShowPriestMiracleLists
                                 || name.Contains("Priest", StringComparison.OrdinalIgnoreCase)
                                 || name.Contains("Vivomancer", StringComparison.OrdinalIgnoreCase);

        var showEvocations = current.ShowEvocationsTab
                            || name.Contains("Druid", StringComparison.OrdinalIgnoreCase);

        return new AdvancementTabState(
            ShowSpellsTab: showSpells,
            ShowMiraclesTab: showMiracles,
            ShowPriestMiracleLists: showPriestMiracles,
            ShowEvilStairway: current.ShowEvilStairway,
            ShowEvocationsTab: showEvocations);
    }

    private static bool HasBracket(IEnumerable<string>? brackets, string token)
        => brackets != null && brackets.Any(b => b.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool HasClassAbility(CharacterClassRecord? classRecord, params string[] names)
    {
        if (classRecord?.Levels == null || names.Length == 0)
            return false;

        foreach (var level in classRecord.Levels.Values)
        {
            if (level == null)
                continue;

            foreach (var ability in level)
            {
                var name = ability?.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                foreach (var token in names)
                {
                    if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }
}
