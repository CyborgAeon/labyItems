using System.Linq;
using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class GuildsServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsGuildDefinitions()
    {
        var all = await GuildsService.GetAllAsync();

        Assert.NotEmpty(all);
    }

    [Fact]
    public async Task GetGuildNamesAsync_ReturnsSortedNames()
    {
        var names = await GuildsService.GetGuildNamesAsync();

        var sorted = names.OrderBy(x => x, System.StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public void GetAlignmentRule_BuildsFallbackFromAvailability()
    {
        var record = new GuildRecord
        {
            Availability = new GuildAvailability
            {
                Whitelist = new GuildAvailabilityRules
                {
                    Alignments = new GuildAvailabilityAlignments
                    {
                        Order = new() { "Lawful" },
                        Moral = new() { "Good" }
                    }
                }
            }
        };

        var rule = GuildsService.GetAlignmentRule(record);

        Assert.NotNull(rule);
        Assert.Equal("restrict", rule!.Mode);
        Assert.Contains(OrderAxis.Lawful, rule.Allowed!.Order!);
        Assert.Contains(MoralAxis.Good, rule.Allowed!.Moral!);
    }
}
