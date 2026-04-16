using labyItems.Helpers;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class ScrollDocumentServiceTests : ServiceTestBase
{
    [Fact]
    public async Task CreatePdfAsync_GeneratesPdfWithoutFontResolverException()
    {
        ConfigureFontPackageOverrides();
        var service = new ScrollDocumentService();

        var ex = await Record.ExceptionAsync(async () =>
        {
            var result = await service.CreatePdfAsync(new ScrollPdfRequest(
                Text: "Seed verbal\nScroll do thy work\nSeed Spell",
                Language: ScrollLanguage.ManaGlyphs,
                ManaGlyphVariant: ManaGlyphVariant.Advanced,
                DocumentName: "seed-scroll"));

            Assert.True(File.Exists(result.Path));
            Assert.True(result.PageCount >= 1);
        });

        Assert.Null(ex);
    }

    [Fact]
    public async Task CreatePdfAsync_CanBeCalledTwiceWithoutFontResolverException()
    {
        ConfigureFontPackageOverrides();
        var service = new ScrollDocumentService();

        await service.CreatePdfAsync(new ScrollPdfRequest(
            Text: "First call",
            Language: ScrollLanguage.SpiritRunes,
            ManaGlyphVariant: ManaGlyphVariant.Advanced,
            DocumentName: "first"));

        var ex = await Record.ExceptionAsync(async () =>
        {
            var result = await service.CreatePdfAsync(new ScrollPdfRequest(
                Text: "Second call",
                Language: ScrollLanguage.Ogham,
                ManaGlyphVariant: ManaGlyphVariant.Advanced,
                DocumentName: "second"));

            Assert.True(File.Exists(result.Path));
        });

        Assert.Null(ex);
    }

    private static void ConfigureFontPackageOverrides()
    {
        var fontRoot = Path.Combine(ServiceTestEnvironment.RepoRoot, "labyItems", "Resources", "Fonts");
        MapFont("Mana_Glyphs_smart_v2.ttf");
        MapFont("Mana_Glyphs_handwritten_style.ttf");
        MapFont("Spirit_Runes.ttf");
        MapFont("Ogham.ttf");
        MapFont("OpenSans-Regular.ttf");
        MapFont("OpenSans-Semibold.ttf");
        return;

        void MapFont(string fileName)
        {
            var fullPath = Path.Combine(fontRoot, fileName);
            FileSystem.SetPackageOverride(fileName, () => File.OpenRead(fullPath));
        }
    }
}
