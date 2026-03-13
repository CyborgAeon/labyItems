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
}
