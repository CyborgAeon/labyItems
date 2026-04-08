using System.Collections.Generic;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardResistanceLevelServiceTests : ServiceTestBase
{
    [Fact]
    public void BuildBaselineRawLevels_ZeroOrMissingLevels_DefaultToEight()
    {
        var raw = BattleboardResistanceLevelService.BuildBaselineRawLevels(new Dictionary<string, int>
        {
            ["Magic"] = 0,
            ["Spirit"] = 3
        });

        Assert.Equal(8, raw["Physical"]);
        Assert.Equal(8, raw["Magic"]);
        Assert.Equal(8, raw["Neuro"]);
        Assert.Equal(3, raw["Spirit"]);
    }

    [Fact]
    public void BuildDisplayedLevels_AppliesBaselineMultiplierAndInfinity()
    {
        var displayed = BattleboardResistanceLevelService.BuildDisplayedLevels(
            new Dictionary<string, int>
            {
                ["Physical"] = 0,
                ["Magic"] = 9,
                ["Neuro"] = 8,
                ["Spirit"] = 9
            },
            new Dictionary<string, int>
            {
                ["Magic"] = 2
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Spirit"
            });

        Assert.Equal(8, displayed["Physical"]);
        Assert.Equal(18, displayed["Magic"]);
        Assert.Equal(8, displayed["Neuro"]);
        Assert.Equal(int.MaxValue, displayed["Spirit"]);
    }

    [Fact]
    public void BuildDisplayedLevels_AppliesPerSixthScaling()
    {
        var displayed = BattleboardResistanceLevelService.BuildDisplayedLevels(
            new Dictionary<string, int>
            {
                ["Physical"] = 8,
                ["Magic"] = 8,
                ["Neuro"] = 8,
                ["Spirit"] = 8
            },
            multipliers: null,
            infiniteResistanceTypes: null,
            perSixths: new Dictionary<string, int>
            {
                ["Magic"] = 3
            });

        Assert.Equal(12, displayed["Magic"]);
        Assert.Equal(8, displayed["Physical"]);
        Assert.Equal(8, displayed["Neuro"]);
        Assert.Equal(8, displayed["Spirit"]);
    }
}
