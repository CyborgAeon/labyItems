namespace labyItems.Models.Guilds;

public sealed record GuildGrantChoiceState(
    string SelectionKey,
    GuildGrantNode Choice,
    string? Timing,
    int Allowance,
    IReadOnlyList<GuildGrantChoiceOptionState> Options,
    IReadOnlyList<string> SelectedOptions,
    bool GrantsAll);
