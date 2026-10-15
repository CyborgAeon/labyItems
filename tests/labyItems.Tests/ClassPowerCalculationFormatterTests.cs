using labyItems.Pages.ClassCard;
using Xunit;

namespace labyItems.Tests;

public sealed class ClassPowerCalculationFormatterTests
{
    [Theory]
    [InlineData("CL^2+CL", "72")]
    [InlineData("CL * 2", "16")]
    [InlineData("(CL+2)/2", "5")]
    public void Format_EvaluatesCasterLevelAtEight(string calculation, string expected)
        => Assert.Equal(expected, ClassPowerCalculationFormatter.Format(calculation));

    [Fact]
    public void Format_KeepsTribeMembersSymbolic_AndSubstitutesCasterLevel()
        => Assert.Equal("Tribe Members+8", ClassPowerCalculationFormatter.Format("TM+CL"));
}
