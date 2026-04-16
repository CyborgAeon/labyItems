using labyItems.Helpers;
using Xunit;

namespace labyItems.Tests;

public sealed class ScrollTextFormatterTests
{
    [Fact]
    public void BuildOutputText_AppendsScrollSuffix_ForSpell()
    {
        var result = ScrollTextFormatter.BuildOutputText(
            "By fire made bright",
            ScrollSourceKind.Spell,
            asScroll: true,
            sourceName: "Flame Dart");

        Assert.Equal("By fire made bright\nScroll do thy work\nFlame Dart", result);
    }

    [Fact]
    public void BuildOutputText_DoesNotAppendScrollSuffix_ForEvocation()
    {
        var result = ScrollTextFormatter.BuildOutputText(
            "Roots awaken",
            ScrollSourceKind.Evocation,
            asScroll: true,
            sourceName: "Thorn Grasp");

        Assert.Equal("Roots awaken", result);
    }

    [Fact]
    public void CanUseAsScroll_OnlyForSpellsAndMiracles()
    {
        Assert.True(ScrollTextFormatter.CanUseAsScroll(ScrollSourceKind.Spell));
        Assert.True(ScrollTextFormatter.CanUseAsScroll(ScrollSourceKind.Miracle));
        Assert.False(ScrollTextFormatter.CanUseAsScroll(ScrollSourceKind.Evocation));
        Assert.False(ScrollTextFormatter.CanUseAsScroll(ScrollSourceKind.Custom));
    }

    [Fact]
    public void NormalizeForGlyphDisplay_StripsPunctuation_And_Lowercases_Text()
    {
        var result = ScrollTextFormatter.NormalizeForGlyphDisplay("With the power of the Onyx Dragon, I bid thee!");

        Assert.Equal("with the power of the onyx dragon i bid thee", result);
    }

    [Fact]
    public void NormalizeForGlyphDisplay_MapsSpiritDigraphs_ToDedicatedGlyphSlots()
    {
        var result = ScrollTextFormatter.NormalizeForGlyphDisplay("thing change song", ScrollLanguage.SpiritRunes);

        Assert.Equal("[i} -a}e so}", result);
    }
}
