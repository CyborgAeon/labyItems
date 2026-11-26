namespace labyItems.Models.Characters;

public sealed record AbilityUsagePolicy(int Uses, int Minutes, int RequiredInnates = 0);
