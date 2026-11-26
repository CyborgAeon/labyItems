using System.Collections.Generic;
using System.Linq;
using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class AbilityDraftBuilderTests
{
    [Fact]
    public void BuildFromLevels_ResolvesProgressionAgainstAchievedLevel()
    {
        var levels = new Dictionary<string, List<AbilityDefinition>>
        {
            ["2"] =
            [
                new AbilityDefinition
                {
                    Name = "Auric Colour",
                    Type = "Innate",
                    Progression = new AbilityCountProgression
                    {
                        Amount = 1,
                        PerLevels = 1
                    }
                }
            ],
            ["6"] =
            [
                new AbilityDefinition
                {
                    Name = "Late Ability",
                    Type = "Innate",
                    Progression = new AbilityCountProgression
                    {
                        Amount = 1,
                        PerLevels = 2
                    }
                }
            ]
        };

        var level2 = AbilityDraftBuilder.BuildFromLevels(levels, achievedLevel: 2);
        var level8 = AbilityDraftBuilder.BuildFromLevels(levels, achievedLevel: 8);

        var auricAt2 = level2.Single(a => a.Name == "Auric Colour");
        var auricAt8 = level8.Single(a => a.Name == "Auric Colour");

        Assert.Equal(2, auricAt2.Count);
        Assert.Equal(8, auricAt8.Count);
        Assert.DoesNotContain(level2, a => a.Name == "Late Ability");
        Assert.Contains(level8, a => a.Name == "Late Ability");
    }

    [Fact]
    public void ResolveInnateRank_UsesFrequencyFromLevelGained()
    {
        var athlete = new AbilityDraft
        {
            Name = "Athlete",
            AbilityType = AbilityType.Innate,
            Frequency = "3",
            LevelGained = 2
        };

        var rank = AbilityDraftBuilder.ResolveInnateRank(athlete, achievedLevel: 8);

        Assert.Equal(3, rank);
    }

    [Fact]
    public void TryResolveFrequencySkillRank_ComputesLevelsSinceSelection()
    {
        var combatWary = new AbilityDraft
        {
            Name = "Combat-Wary",
            AbilityType = AbilityType.Static,
            Frequency = "1",
            LevelGained = 2
        };

        var success = AbilityDraftBuilder.TryResolveFrequencySkillRank(combatWary, out var rank, achievedLevel: 8);

        Assert.True(success);
        Assert.Equal(7, rank);
    }
    [Theory]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(8, 2)]
    [InlineData(11, 3)]
    public void Stealth_UsesInclusiveLevelsAndRoundsUp(int level, int innates)
    {
        var draft = Assert.Single(AbilityDraftBuilder.ParseAbility(
            new AbilityDefinition { Name = "Stealth", Type = "Innate", Frequency = "4" }, 2, level));

        Assert.Equal(innates, AbilityDraftBuilder.ResolveInnateRank(draft));
    }

    [Fact]
    public void FrequencyCountsOnlyContributionsExplicitlyAppliedToTheSameGrant()
    {
        var stealth = Assert.Single(AbilityDraftBuilder.ParseAbility(
            new AbilityDefinition { Name = "Stealth", Type = "Innate", Frequency = "4" }, 2));
        Assert.Equal(3, AbilityDraftBuilder.ResolveInnateRank(stealth, permittedAdditionalLevels: [2]));
        Assert.Equal(2, AbilityDraftBuilder.ResolveInnateRank(stealth));

        var baseLevels = AbilityQuantityResolver.GetEffectiveLevels(8, 2);
        var withPurchasedLevels = AbilityQuantityResolver.AddPermittedEffectiveLevels(baseLevels, [2]);
        Assert.Equal(9, withPurchasedLevels);
        Assert.Equal(3, AbilityQuantityResolver.GetFrequencyIncrements(withPurchasedLevels, 4));
        Assert.Equal(2, AbilityQuantityResolver.GetFrequencyIncrements(baseLevels, 4));
        Assert.Equal(1, AbilityQuantityResolver.GetFrequencyIncrements(2, 4));

        var independentPools = new[] { 1, 4 };
        Assert.Equal(2, independentPools.Sum(levels => AbilityQuantityResolver.GetFrequencyIncrements(levels, 4)));
        Assert.Equal(1, AbilityQuantityResolver.GetFrequencyIncrements(
            AbilityQuantityResolver.AddPermittedEffectiveLevels(1, [2]), 4));
    }

    [Fact]
    public void Progression_UsesTotalSourceLevelAndCompletesWholeBandsBeforeAddingAmount()
    {
        Assert.Equal(2, new AbilityCountProgression { PerLevels = 2 }.ResolveCount(5));
        Assert.Equal(4, new AbilityCountProgression { PerLevels = 2, Amount = 2 }.ResolveCount(5));
        Assert.Equal(int.MaxValue, new AbilityCountProgression { Amount = int.MaxValue }.ResolveCount(8));
    }

    [Fact]
    public void FixedInnatesRetainTheirCountAndAreUnavailableBeforeAcquisition()
    {
        var grant = new AbilityDefinition { Name = "Fixed pool", Type = "Innate", Count = 12 };
        Assert.Empty(AbilityDraftBuilder.ParseAbility(grant, 4, 3));
        var draft = Assert.Single(AbilityDraftBuilder.ParseAbility(grant, 4, 6));

        Assert.Equal(12, AbilityDraftBuilder.ResolveInnateRank(draft));
        Assert.Equal(0, AbilityDraftBuilder.ResolveInnateRank(draft, 3));
    }
}
