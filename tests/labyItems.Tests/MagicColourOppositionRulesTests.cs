using labyItems.Helpers;
using labyItems.Models.Enums;
using Xunit;

namespace labyItems.Tests;

public sealed class MagicColourOppositionRulesTests
{
    [Fact]
    public void AreOpposites_ReturnsTrue_ForKnownPair()
    {
        Assert.True(MagicColourOppositionRules.AreOpposites(MagicColours.Red, MagicColours.Green));
    }

    [Fact]
    public void TryGetOpposite_ReturnsMappedColour()
    {
        var success = MagicColourOppositionRules.TryGetOpposite(MagicColours.Ivory, out var opposite);

        Assert.True(success);
        Assert.Equal(MagicColours.Ebony, opposite);
    }
}
