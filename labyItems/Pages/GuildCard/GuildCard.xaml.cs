using labyItems.Pages.Characters;
using labyItems.Services;

namespace labyItems.Pages.GuildCard;

public partial class GuildCard : ContentPage
{
    public GuildCard()
    {
        InitializeComponent();
    }

    public GuildCard(GuildRecord guild, string? guildName = null) : this()
    {
        _ = GuildDetails.LoadFromRecordAsync(guild, guildName);
        Title = "Almanac";
        DetailHeader.HeaderText = "Almanac";
    }

    public GuildCard(GuildCardVm guildVm) : this()
    {
        GuildDetails.GuildVm = guildVm;
        Title = "Almanac";
        DetailHeader.HeaderText = "Almanac";
    }
}
