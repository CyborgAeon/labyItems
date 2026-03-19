using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardAbilityEffectResolverTests : ServiceTestBase
{
    [Fact]
    public void ResolveFallback_ParsesTextualResistanceAndImmunity()
    {
        var resolved = BattleboardAbilityEffectResolver.ResolveFallback(new[]
        {
            new AbilityDraft
            {
                Name = "9th Level Resistance to Neuronics",
                AbilityType = AbilityType.Static
            },
            new AbilityDraft
            {
                Name = "Immunity to Repels",
                AbilityType = AbilityType.Static
            }
        });

        Assert.Equal(9, resolved.ResistanceOverrides["Neuro"]);
        Assert.Contains("Immunity to Repels", resolved.Immunities);
    }

    [Fact]
    public async Task ResolveAsync_ResolvesSystemEffectsFromAbilityKey()
    {
        var resolved = await BattleboardAbilityEffectResolver.ResolveAsync(new[]
        {
            new AbilityDraft
            {
                AbilityKey = "ability.merged.9th-level-resistance-to-all-but-neuronics",
                Name = "Custom Resistance Grant",
                AbilityType = AbilityType.Static
            },
            new AbilityDraft
            {
                AbilityKey = "ability.immunity-to-repels",
                Name = "Custom Immunity Grant",
                AbilityType = AbilityType.Static
            }
        });

        Assert.Equal(9, resolved.ResistanceOverrides["Physical"]);
        Assert.Equal(9, resolved.ResistanceOverrides["Magic"]);
        Assert.Equal(9, resolved.ResistanceOverrides["Spirit"]);
        Assert.False(resolved.ResistanceOverrides.ContainsKey("Neuro"));
        Assert.Contains("Immunity to Repels", resolved.Immunities);
    }

    [Fact]
    public async Task ResolveAsync_ResolvesMultiplierAndInfiniteResistanceEffects()
    {
        var resolved = await BattleboardAbilityEffectResolver.ResolveAsync(new[]
        {
            new AbilityDraft
            {
                AbilityKey = "ability.half-effect-magic",
                Name = "1/2 effect magic",
                AbilityType = AbilityType.Resistance
            },
            new AbilityDraft
            {
                AbilityKey = "ability.half-spirit",
                Name = "Half-Spirit",
                AbilityType = AbilityType.Resistance
            },
            new AbilityDraft
            {
                AbilityKey = "ability.spiritless",
                Name = "Spiritless",
                AbilityType = AbilityType.Resistance
            },
            new AbilityDraft
            {
                AbilityKey = "ability.mindless",
                Name = "Mindless",
                AbilityType = AbilityType.Resistance
            }
        });

        Assert.Equal(2, resolved.ResistanceMultipliers["Magic"]);
        Assert.Equal(2, resolved.ResistanceMultipliers["Spirit"]);
        Assert.Contains("Spirit", resolved.InfiniteResistanceTypes);
        Assert.Contains("Neuro", resolved.InfiniteResistanceTypes);
    }
}
