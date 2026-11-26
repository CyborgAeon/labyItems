namespace labyItems.Models.Characters;

public sealed record ResolvedAbilityUsage(
    string Source,
    string AbilityRef,
    string Name,
    string PublishedEffect,
    AbilityUsagePolicy Policy)
{
    public string DisplayName => $"{Name} ({Policy.Uses}/{Policy.Minutes} min)";
}
