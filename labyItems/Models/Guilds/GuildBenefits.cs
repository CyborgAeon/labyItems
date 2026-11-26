namespace labyItems.Models.Guilds;

public sealed class GuildBenefits
{
    public List<GuildBenefitEntry> Basic { get; set; } = new();
    public List<GuildBenefitEntry> Intermediate { get; set; } = new();
    public List<GuildBenefitEntry> Advanced { get; set; } = new();
}
