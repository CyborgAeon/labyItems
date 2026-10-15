using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BookDirectoryTests
{
    [Theory]
    [InlineData("Oraculum Insight", "o-insight/Oraculum-Insight.pdf")]
    [InlineData("Engarde", "Engarde/Engarde.pdf")]
    [InlineData("At the sharp end", "at_the_sharp_end/At-The-Sharp-End.pdf")]
    [InlineData("Manufacturers Guide", "Manufacturers_guide/The-Manufacturers-Guide.pdf")]
    [InlineData("Almanac", "people/Almanac.pdf")]
    [InlineData("  Words From Above  ", "words_from_above/Words-From-above.pdf")]
    [InlineData("wizard's grimoire", "grimoire/Wizard-Grimoire.pdf")]
    [InlineData("Druids Way", "druids_way/Druid's_way.pdf")]
    [InlineData("Collated.pdf", "people/Collated.pdf")]
    public void TryResolve_KnownBook_ReturnsPackagedAsset(string title, string expectedPath)
    {
        Assert.True(BookDirectory.TryResolve(title, out var path));
        Assert.Equal(expectedPath, path);
    }

    [Fact]
    public void TryResolve_NonBookTitle_ReturnsFalse()
    {
        Assert.False(BookDirectory.TryResolve("Ambidexterity", out _));
    }

    [Fact]
    public void AssetPaths_ContainsEveryPackagedSourceBookPdf()
    {
        var expected = new[]
        {
            "Engarde/Engarde.pdf",
            "Manufacturers_guide/ISP-Tables.pdf",
            "Manufacturers_guide/The-Manufacturers-Guide.pdf",
            "at_the_sharp_end/At-The-Sharp-End.pdf",
            "druids_way/Druid's_way.pdf",
            "evolution_classes/Classes.pdf",
            "grimoire/Wizard-Grimoire.pdf",
            "o-insight/Oraculum-Insight.pdf",
            "people/Almanac.pdf",
            "people/Collated.pdf",
            "people/Races.pdf",
            "words_from_above/Divine_Guidance.pdf",
            "words_from_above/Words-From-above.pdf"
        };

        Assert.Equal(expected.Order(), BookDirectory.AssetPaths.Order());
    }
}
