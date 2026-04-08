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

    [Fact]
    public async Task GetLifeScaleAsync_HumanForgottenUsesOneLevelLowerProgression()
    {
        var human = await LifeScalesService.GetLifeScaleAsync("Human", "Warrior");
        var forgotten = await LifeScalesService.GetLifeScaleAsync("Human (Forgotten)", "Warrior");

        Assert.Equal(human.Count, forgotten.Count);
        Assert.True(human.Count >= 8);

        for (var i = 1; i < forgotten.Count; i++)
            Assert.Equal(human[i - 1], forgotten[i]);
    }

    [Fact]
    public async Task GetClassesForRaceAsync_WyrmKinIncludesConfiguredClassSet()
    {
        var classes = await LifeScalesService.GetClassesForRaceAsync("Wyrm-Kin");

        Assert.Contains(classes, c => c.Equals("Wizard", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(classes, c => c.Equals("Warlock", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(classes, c => c.Equals("Warrior", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(classes, c => c.Equals("Cavalier", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(classes, c => c.Equals("Power Warrior", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(classes, c => c.Equals("Spiritual Warrior", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(classes, c => c.Equals("Paladin", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(classes, c => c.Equals("Druid", StringComparison.OrdinalIgnoreCase));
    }
}
