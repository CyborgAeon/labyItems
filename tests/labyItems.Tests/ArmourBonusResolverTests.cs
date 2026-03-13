using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class ArmourBonusResolverTests
{
    [Fact]
    public void ResolveMaxPerSourceTotals_UsesHighestPerSourceAndSumsAcrossSources()
    {
        var abilities = new[]
        {
            new AbilityDraft { Name = "Class DAC 1", AbilityType = AbilityType.Dac, Count = 1, Source = "Class" },
            new AbilityDraft { Name = "Class DAC 4", AbilityType = AbilityType.Dac, Count = 4, Source = "Class" },
            new AbilityDraft { Name = "Guild A DAC 1", AbilityType = AbilityType.Dac, Count = 1, Source = "Guild:Iron & Empire" },
            new AbilityDraft { Name = "Guild A DAC 3", AbilityType = AbilityType.Dac, Count = 3, Source = "Guild:Iron & Empire" },
            new AbilityDraft { Name = "Guild B DAC 2", AbilityType = AbilityType.Dac, Count = 2, Source = "Guild:Second Guild" },
            new AbilityDraft { Name = "Item PAC 2", AbilityType = AbilityType.Pac, Count = 2, Source = "Item:Stone Charm" },
            new AbilityDraft { Name = "Item PAC 1", AbilityType = AbilityType.Pac, Count = 1, Source = "Item:Stone Charm" }
        };

        var totals = ArmourBonusResolver.ResolveMaxPerSourceTotals(abilities);

        Assert.Equal(2, totals.Pac);
        Assert.Equal(9, totals.Dac);
        Assert.Equal(0, totals.Mac);
        Assert.Equal(0, totals.Sac);
    }

    [Fact]
    public void ResolveMaxPerSourceTotals_ParsesTokenisedArmourValues()
    {
        var abilities = new[]
        {
            new AbilityDraft { Name = "+1 DAC", AbilityType = AbilityType.Static, Source = "Guild:A" },
            new AbilityDraft { Name = "+3 DAC", AbilityType = AbilityType.Static, Source = "Guild:A" },
            new AbilityDraft { Effect = "+2 MAC", AbilityType = AbilityType.Static, Source = "Guild:B", Name = "Arcane Ward" }
        };

        var totals = ArmourBonusResolver.ResolveMaxPerSourceTotals(abilities);

        Assert.Equal(3, totals.Dac);
        Assert.Equal(2, totals.Mac);
    }
}
