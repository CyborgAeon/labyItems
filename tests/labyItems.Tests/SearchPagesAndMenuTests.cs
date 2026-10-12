using labyItems.Controls;
using Xunit;

namespace labyItems.Tests;

public class SearchPagesAndMenuTests
{
    [Fact]
    public void ToolsetRouteKeys_ContainsAllSearchRoutes()
    {
        Assert.Equal("search", ToolsetRouteKeys.Search);
        Assert.Equal("search-global", ToolsetRouteKeys.SearchGlobal);
        Assert.Equal("search-spells", ToolsetRouteKeys.SearchSpells);
        Assert.Equal("search-miracles", ToolsetRouteKeys.SearchMiracles);
        Assert.Equal("search-evocations", ToolsetRouteKeys.SearchEvocations);
        Assert.Equal("search-neuronics", ToolsetRouteKeys.SearchNeuronics);
        Assert.Equal("search-classes", ToolsetRouteKeys.SearchClasses);
        Assert.Equal("search-races", ToolsetRouteKeys.SearchRaces);
        Assert.Equal("search-abilities", ToolsetRouteKeys.SearchAbilities);
        Assert.Equal("search-guilds", ToolsetRouteKeys.SearchGuilds);
        Assert.Equal("search-multi-classes", ToolsetRouteKeys.SearchMultiClasses);
        Assert.Equal("search-multi-races", ToolsetRouteKeys.SearchMultiRaces);
    }

    [Fact]
    public void ToolsetRouteKeys_AllKeysAreDistinct()
    {
        var keys = new[]
        {
            ToolsetRouteKeys.Home,
            ToolsetRouteKeys.Characters,
            ToolsetRouteKeys.ItemsIsp,
            ToolsetRouteKeys.ItemsMp,
            ToolsetRouteKeys.ItemsCrafting,
            ToolsetRouteKeys.ScrollBuilder,
            ToolsetRouteKeys.ItemsWallet,
            ToolsetRouteKeys.Battleboard,
            ToolsetRouteKeys.NonStandard,
            ToolsetRouteKeys.Calendar,
            ToolsetRouteKeys.Search,
            ToolsetRouteKeys.SearchGlobal,
            ToolsetRouteKeys.SearchSpells,
            ToolsetRouteKeys.SearchMiracles,
            ToolsetRouteKeys.SearchEvocations,
            ToolsetRouteKeys.SearchNeuronics,
            ToolsetRouteKeys.SearchClasses,
            ToolsetRouteKeys.SearchRaces,
            ToolsetRouteKeys.SearchAbilities,
            ToolsetRouteKeys.SearchGuilds,
            ToolsetRouteKeys.SearchMultiClasses,
            ToolsetRouteKeys.SearchMultiRaces,
            ToolsetRouteKeys.Guilds,
            ToolsetRouteKeys.Trade,
            ToolsetRouteKeys.Settings
        };

        var distinct = keys.Distinct().Count();
        Assert.Equal(keys.Length, distinct);
    }
}
