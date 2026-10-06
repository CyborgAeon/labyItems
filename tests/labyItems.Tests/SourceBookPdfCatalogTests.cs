using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class SourceBookPdfCatalogTests
{
    [Theory]
    [InlineData("Oraculum Insight", "o-insight/O-Insight-16.3.pdf")]
    [InlineData("Engarde", "Engarde/Engarde.pdf")]
    [InlineData("At the sharp end", "at_the_sharp_end/At-The-Sharp-End-16.3.pdf")]
    [InlineData("Manufacturers Guide", "Manufacturers_guide/The-Manufacturers-Guide.pdf")]
    [InlineData("  Words From Above  ", "words_from_above/Words-From-above-16.3.pdf")]
    public void TryResolve_KnownBook_ReturnsPackagedAsset(string title, string expectedPath)
    {
        Assert.True(SourceBookPdfCatalog.TryResolve(title, out var path));
        Assert.Equal(expectedPath, path);
    }

    [Fact]
    public void TryResolve_NonBookTitle_ReturnsFalse()
    {
        Assert.False(SourceBookPdfCatalog.TryResolve("Ambidexterity", out _));
    }
}
