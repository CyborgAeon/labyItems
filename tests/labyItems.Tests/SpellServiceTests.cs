using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class SpellServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_ReturnsSeededSpellFromDatabase()
    {
        var all = await SpellService.GetAllAsync();

        var seed = Assert.Single(all, s => s.name == "Seed Spell");
        Assert.Equal(2, seed.level);
        Assert.Equal("Red", seed.colour);
        Assert.False(seed.isAdvanced ?? false);
    }

    [Fact]
    public async Task SearchAsync_FiltersByName()
    {
        var result = await SpellService.SearchAsync("Seed");

        Assert.Single(result);
        Assert.Equal("Seed Spell", result.First().name);
    }
}
