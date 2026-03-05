using System.Collections.Generic;

namespace labyItems.Services;

public interface IAdvancementTabVisibilityService
{
    AdvancementTabState Resolve(string? className, CharacterClassRecord? classRecord);
    AdvancementTabState ApplyNameFallback(string? className, AdvancementTabState current);
}

public readonly record struct AdvancementTabState(
    bool ShowSpellsTab,
    bool ShowMiraclesTab,
    bool ShowPriestMiracleLists,
    bool ShowEvilStairway,
    bool ShowEvocationsTab);
