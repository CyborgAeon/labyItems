using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class EvolutionServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAbilitiesAsync_ParsesPrereqsAndMaxAvailable()
    {
        var all = await EvolutionService.GetAllAbilitiesAsync();

        var entry = Assert.Single(all, e => e.Index == "AA1");
        Assert.Contains("Ability:Focus", entry.PreReqs);
        Assert.Equal(2, entry.MaxAvailable);
    }

    [Fact]
    public async Task GetAllAsync_FlagsImmunityIndices()
    {
        var all = await EvolutionService.GetAllAsync();

        var immunity = Assert.Single(all, e => e.Index == "Immunity! Fire");
        Assert.True(immunity.IsImmunity);
    }

    [Fact]
    public async Task SearchByIndexAsync_ReturnsNgramMatches()
    {
        var result = await EvolutionService.SearchByIndexAsync("Arcane");

        var hit = Assert.Single(result);
        Assert.Equal("AA1", hit.Index);
    }
}
