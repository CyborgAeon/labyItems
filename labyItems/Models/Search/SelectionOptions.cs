namespace labyItems.Models.Search;

public sealed class SelectionOptions
{
    public SelectionType SelectionType { get; init; }
    public IReadOnlySet<string>? SelectedKeys { get; init; }
    public int? Max { get; init; }
}
