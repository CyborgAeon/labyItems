namespace labyItems.Models.Guilds;

public static class GuildBenefitKeys
{
    public static string BuildSelectionKey(string guildName, string tier, int optionIndex)
    {
        var guild = (guildName ?? string.Empty).Trim();
        var level = (tier ?? string.Empty).Trim();
        return $"{guild}::{level}::{optionIndex}";
    }
}
