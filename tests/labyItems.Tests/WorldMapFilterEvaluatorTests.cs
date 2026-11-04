using labyItems.Helpers;
using labyItems.Models.WorldMap;
using Xunit;

namespace labyItems.Tests;

public sealed class WorldMapFilterEvaluatorTests
{
    private static WorldTownResult Town(string name, params WorldStatistic[] stats) => new(new WorldRegion { Id = name, Name = name, StatisticsId = name }, stats);

    [Fact]
    public void HiringHallRangesAreInclusiveAndDoNotTreatMissingScoresAsZero()
    {
        var towns = new[] { Town("Alguz", new WorldStatistic("Administration", "Volksmight", 15)), Town("Bildteve", new WorldStatistic("Administration", "Volksmight", 45)), Town("Unknown"), Town("Zero", new WorldStatistic("Administration", "Volksmight", 0)) };
        var filter = new WorldMapFilter("", [new("Administration", "Volksmight", 15, 45)]);
        Assert.Equal(new[] { "Alguz", "Bildteve" }, WorldMapFilterEvaluator.Apply(towns, filter).Select(t => t.Name));
        var zero = new WorldMapFilter("", [new("Administration", "Volksmight", 0, 0)]);
        Assert.Equal("Zero", Assert.Single(WorldMapFilterEvaluator.Apply(towns, zero)).Name);
    }

    [Fact]
    public void NameAndMultipleCategoryConstraintsCombineWithoutMixingDeities()
    {
        var towns = new[]
        {
            Town("Alguz", new WorldStatistic("Administration", "Volksmight", 15), new("Dragons", "White Dragon", 55), new("Deities and Orders", "Talthar", 100)),
            Town("Halgar", new WorldStatistic("Administration", "Volksmight", 75), new("Dragons", "White Dragon", 90), new("Deities and Orders", "Other", 100))
        };
        var filter = new WorldMapFilter(" AL ".Trim(), [new("Dragons", "White Dragon", 50, null), new("Deities and Orders", "Talthar", 100, 100)]);
        Assert.Equal("Alguz", Assert.Single(WorldMapFilterEvaluator.Apply(towns, filter)).Name);
        Assert.Empty(WorldMapFilterEvaluator.Apply(towns, filter with { Query = "Deci" }));
    }

    [Fact]
    public void HighestCategoryValueUsesMaximumAndExactMetricUsesItsOwnValue()
    {
        var town = Town("Alguz", new WorldStatistic("Dragons", "Gold Dragon", 53), new("Dragons", "White Dragon", 55));
        Assert.True(WorldMapFilterEvaluator.Matches(town, new("", [new("Dragons", null, 55, 55)])));
        Assert.False(WorldMapFilterEvaluator.Matches(town, new("", [new("Dragons", "Gold Dragon", 55, null)])));
        Assert.False(WorldMapFilterEvaluator.Matches(Town("Missing"), new("", [new("Dragons", null, null, 100)])));
    }

    [Fact]
    public void EmptyFiltersReturnAllTownsInNameOrderAndDoNotInventCouncilAssignments()
    {
        Assert.False(WorldMapFilter.Empty.IsActive);
        var towns = new[] { Town("Halgar"), Town("Alguz") };
        Assert.Equal(new[] { "Alguz", "Halgar" }, WorldMapFilterEvaluator.Apply(towns, WorldMapFilter.Empty).Select(t => t.Name));
        Assert.Empty(WorldMapFilterEvaluator.Apply(towns, new("", [new("Council", "Helgenmyr", 0, 0)])));
    }
}
