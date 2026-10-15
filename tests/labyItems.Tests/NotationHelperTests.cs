using labyItems.Helpers;
using labyItems.Models.Characters;
using Xunit;

namespace labyItems.Tests;

public sealed class NotationHelperTests
{
    [Fact]
    public void FormatAlignment_UsesGuildAxisNotation()
    {
        var rule = new AlignmentRule
        {
            Allowed = new AllowedAxes
            {
                Order = [OrderAxis.Lawful, OrderAxis.Neutral, OrderAxis.Chaotic],
                Moral = [MoralAxis.Good]
            }
        };

        Assert.Equal("Any Good", NotationHelper.FormatAlignment(rule));
    }

    [Fact]
    public void FormatAlignment_CombinesRestrictedAxes()
    {
        Assert.Equal(
            "Lawful/Neutral · Good/Evil",
            NotationHelper.FormatAlignment(["Lawful", "Neutral"], ["Good", "Evil"]));
    }

    [Fact]
    public void FormatAlignment_PrefersExplicitPairs()
    {
        var rule = new AlignmentRule { AllowedPairs = [" Lawful Good ", "True Neutral"] };

        Assert.Equal("Lawful Good, True Neutral", NotationHelper.FormatAlignment(rule));
    }

    [Theory]
    [InlineData(MoralAxis.Evil, "Any Evil")]
    [InlineData(MoralAxis.Neutral, "Any Neutral")]
    public void FormatAlignment_CompressesExplicitPairs_WhenAxesDescribeAnyOrder(
        MoralAxis moral,
        string expected)
    {
        var rule = new AlignmentRule
        {
            Allowed = new AllowedAxes
            {
                Order = [OrderAxis.Lawful, OrderAxis.Neutral, OrderAxis.Chaotic],
                Moral = [moral]
            },
            AllowedPairs = ["Lawful value", "Neutral value", "Chaotic value"]
        };

        Assert.Equal(expected, NotationHelper.FormatAlignment(rule));
    }
}
