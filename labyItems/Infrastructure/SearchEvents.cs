using labyItems.Pages.Search;

namespace labyItems.Infrastructure;

public sealed record SearchDataLoadStarted;

public sealed record SearchDataLoadCompleted(
    IReadOnlyList<GlobalSearchResultVm> Results,
    DateTime CompletedAt);

public sealed record SearchResultsAvailable(
    IReadOnlyList<GlobalSearchResultVm> Results);

public sealed record SearchFilterApplied(
    string SearchText,
    DateTime AppliedAt);
