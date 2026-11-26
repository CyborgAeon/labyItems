using Xunit;
using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Tests;

public sealed class AbilityUsageResolverTests
{
    [Fact]
    public void PsycherAddsAnIndependentIntervalOnlyWhenTheInnateRequirementIsMet()
    {
        var interval = new ResolvedAbilityUsage("Psycher", "ability.neuronic-penetration",
            "Neuronic Penetration", "Published text", new AbilityUsagePolicy(2, 1, 24));
        var pool = new InnateAbilityDraft { Name = interval.Name, Rank = 23 };
        Assert.Empty(AbilityUsageResolver.ResolveIntervals([interval], [pool]));

        pool.Rank = 24;
        var result = Assert.Single(AbilityUsageResolver.ResolveIntervals([interval], [pool]));
        Assert.Equal("Neuronic Penetration (2/1 min)", result.DisplayName);
        Assert.Equal(24, pool.Rank);
    }

    [Fact]
    public void ArtOfBattleReplacesItsEarlierIntervalWithoutReplacingAnotherSource()
    {
        var basic = new ResolvedAbilityUsage("Art of Battle", "ability.daring-deed",
            "Daring Deed", "Basic wording", new AbilityUsagePolicy(1, 10));
        var advanced = basic with { PublishedEffect = "Advanced wording", Policy = new AbilityUsagePolicy(2, 10) };
        var unrelated = basic with { Source = "Independent source" };

        var result = AbilityUsageResolver.ResolveIntervals([basic, unrelated, advanced], []);
        Assert.Equal(2, result.Count);
        Assert.Contains(advanced, result);
        Assert.Contains(unrelated, result);
    }
}
