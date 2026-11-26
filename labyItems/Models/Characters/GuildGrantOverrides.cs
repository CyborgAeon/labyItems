namespace labyItems.Models.Characters;

public sealed class GuildGrantOverrides
{
    public string? DisplayName { get; set; }
    public string? Name
    {
        set => DisplayName ??= value;
    }
    public string? Type { get; set; }
    public string? Verbal { get; set; }
    public string? Effect { get; set; }
    public string? Source { get; set; }
    public string? GrantType { get; set; }
    public int? Count { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
}
