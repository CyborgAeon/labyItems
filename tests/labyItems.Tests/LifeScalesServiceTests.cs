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

    [Fact]
    public async Task GetClassesForRaceAsync_CrolIncludesSubtypeMappedClasses()
    {
        var classes = await LifeScalesService.GetClassesForRaceAsync("Crol");

        Assert.Contains(classes, c => c.Equals("Warrior", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(classes, c => c.Equals("Wizard", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetRacesForClassAsync_WarriorIncludesBaseRaceWhenOnlySubtypeDefinesClass()
    {
        var races = await LifeScalesService.GetRacesForClassAsync("Warrior");

        Assert.Contains(races, r => r.Equals("Crol", StringComparison.OrdinalIgnoreCase));
    }
}
