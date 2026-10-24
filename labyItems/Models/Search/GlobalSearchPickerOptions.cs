namespace labyItems.Models.Search;

public sealed class GlobalSearchPickerOptions
{
    public required string Title { get; init; }
    public required GlobalSearchKind Kind { get; init; }
    public required SelectionOptions SelectionOptions { get; init; }
    public FooterOptions? FooterOptions { get; init; }
    public IReadOnlySet<string>? AllowedNames { get; init; }
    public Func<string, bool>? IsFilterAvailable { get; init; }
    public bool AllowAllKinds { get; init; }
}
