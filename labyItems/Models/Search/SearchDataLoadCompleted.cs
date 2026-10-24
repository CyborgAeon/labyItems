namespace labyItems.Models.Search;

public sealed record SearchDataLoadCompleted(
    IReadOnlyList<GlobalSearchResultVm> Results,
    DateTime CompletedAt);
