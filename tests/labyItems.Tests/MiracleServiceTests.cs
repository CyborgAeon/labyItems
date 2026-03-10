using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class MiracleServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_ReturnsSeededMiracleFromDatabase()
    {
        var all = await MiracleService.GetAllAsync();

        var seed = Assert.Single(all, m => m.name == "Seed Miracle");
        Assert.Equal(3, seed.power);
        Assert.Equal("General", seed.sphere);
    }

    [Fact]
    public async Task SearchAsync_FiltersByName()
    {
        var result = await MiracleService.SearchAsync("Seed");

        Assert.Single(result);
        Assert.Equal("Seed Miracle", result.First().name);
    }
}
