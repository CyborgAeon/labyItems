namespace labyItems.Models.Guilds;

public sealed record GuildGrantResolutionResult(
    IReadOnlyList<ResolvedGuildGrant> Grants,
    IReadOnlyList<GuildGrantChoiceState> Choices,
    IReadOnlyList<GuildGrantDiagnostic> Diagnostics,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MigratedSelections,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MigratedAdventureSelections,
    IReadOnlyList<string> MiracleAdditions);
