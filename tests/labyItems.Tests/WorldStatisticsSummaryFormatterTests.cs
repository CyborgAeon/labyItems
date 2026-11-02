using labyItems.Helpers;
using labyItems.Models.WorldMap;
using Xunit;

namespace labyItems.Tests;

public sealed class WorldStatisticsSummaryFormatterTests
{
    [Fact]
    public void HighestValueIsFilteredByCategoryAndTiesAreStable()
    {
        var groups = new[] { new WorldStatisticsGroup("Alguz", [new("Administration", "Prosperity", 80), new("Administration", "Fealty", 80), new("Dragons", "Gold Dragon", 95)]) };
        Assert.Equal("Fealty · 80", WorldStatisticsSummaryFormatter.HighestText(groups, "Administration"));
        Assert.Equal("Gold Dragon · 95", WorldStatisticsSummaryFormatter.HighestText(groups, "Dragons"));
        Assert.Equal("No published data", WorldStatisticsSummaryFormatter.HighestText(groups, "Deities and Orders"));
    }

    [Fact]
    public void CompositeTerritoriesKeepTheSourcePlaceAndDoNotSumValues()
    {
        var groups = new[] { new WorldStatisticsGroup("Gothiel", [new("Dragons", "Gold Dragon", 42)]), new WorldStatisticsGroup("Eartholme", [new("Dragons", "Gold Dragon", 63)]) };
        Assert.Equal("Gold Dragon · 63 · Eartholme", WorldStatisticsSummaryFormatter.HighestText(groups, "Dragons"));
        Assert.Equal("No published data", WorldStatisticsSummaryFormatter.HighestText(null, "Administration"));
    }
}
