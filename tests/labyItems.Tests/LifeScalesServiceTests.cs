using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class LifeScalesServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsLifeScales()
    {
        var all = await LifeScalesService.GetAllAsync();

        Assert.NotEmpty(all);
    }

    [Fact]
    public async Task GetLifeScaleAsync_ReturnsPointsForKnownRaceAndClass()
    {
        var all = await LifeScalesService.GetAllAsync();
        var race = all.Keys.First();
        var @class = all[race].Keys.First();

        var points = await LifeScalesService.GetLifeScaleAsync(race, @class);

        Assert.NotEmpty(points);
    }
}
