using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class ClassServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsClassDefinitions()
    {
        var all = await ClassService.GetAllAsync();

        Assert.NotEmpty(all);
        Assert.Contains(all, kvp => kvp.Value != null && kvp.Value.Levels.Count > 0);
    }

    [Fact]
    public async Task PaladinProgressionAbilities_AreParsed()
    {
        var all = await ClassService.GetAllAsync();
        var paladin = all["Paladin"];

        var layOnHands = paladin.Levels["3"].First(a => a.Name == "Lay on hands");
        var blessOther = paladin.Levels["4"].First(a => a.Name == "Bless Other");
        var repelUndead = paladin.Levels["6"].First(a => a.Name == "Repel Undead");

        Assert.NotNull(layOnHands.Progression);
        Assert.Equal(3, layOnHands.Progression!.Amount);
        Assert.Equal(1, layOnHands.Progression.PerLevels);

        Assert.NotNull(blessOther.Progression);
        Assert.Equal(1, blessOther.Progression!.Amount);
        Assert.Equal(2, blessOther.Progression.PerLevels);

        Assert.NotNull(repelUndead.Progression);
        Assert.Equal(1, repelUndead.Progression!.Amount);
        Assert.Equal(4, repelUndead.Progression.PerLevels);
    }

    [Fact]
    public async Task UnholyChampion_RepelGood_IsAtLevelTwoWithProgression()
    {
        var all = await ClassService.GetAllAsync();
        var unholyChampion = all["Unholy Champion"];

        var level2 = unholyChampion.Levels["2"];
        var level3 = unholyChampion.Levels["3"];
        var repelGood = level2.First(a => a.Name == "Repel Good");

        Assert.Equal("Innate", repelGood.Type);
        Assert.NotNull(repelGood.Progression);
        Assert.Equal(1, repelGood.Progression!.Amount);
        Assert.Equal(2, repelGood.Progression.PerLevels);
        Assert.DoesNotContain(level3, a => a.Name == "Repel Good");
    }
}
