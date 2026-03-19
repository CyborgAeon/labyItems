using System;
using System.Collections.Generic;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardAdvancementEffectResolverTests : ServiceTestBase
{
    [Fact]
    public void ResolveFallback_ParsesImmunityAndLevelResistanceEffects()
    {
        var resolved = BattleboardAdvancementEffectResolver.ResolveFallback(new[]
        {
            "Immunity to Repels",
            "9th Level Resistance to Neuronics"
        });

        Assert.True(resolved.ResistanceOverrides.TryGetValue("Neuro", out var neuro));
        Assert.Equal(9, neuro);
        Assert.Contains("Immunity to Repels", resolved.Immunities);
    }

    [Fact]
    public void ApplyResistanceOverrides_TakesHighestValuePerType()
    {
        var merged = BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(
            new Dictionary<string, int>
            {
                ["Magic"] = 8,
                ["Neuro"] = 8
            },
            new Dictionary<string, int>
            {
                ["Magic"] = 10,
                ["Neuronic"] = 9
            });

        Assert.Equal(10, merged["Magic"]);
        Assert.Equal(9, merged["Neuro"]);
        Assert.DoesNotContain("Neuronic", merged.Keys);
    }

    [Fact]
    public void ResolveFallback_HandlesAllButNeuronics()
    {
        var resolved = BattleboardAdvancementEffectResolver.ResolveFallback(new[]
        {
            "9th Level Resistance to All but Neuronics"
        });

        Assert.Equal(9, resolved.ResistanceOverrides["Physical"]);
        Assert.Equal(9, resolved.ResistanceOverrides["Magic"]);
        Assert.Equal(9, resolved.ResistanceOverrides["Spirit"]);
        Assert.False(resolved.ResistanceOverrides.ContainsKey("Neuro"));
    }

    [Fact]
    public void ResolveFallback_ParsesHalfEffectMagic_AsMultiplier()
    {
        var resolved = BattleboardAdvancementEffectResolver.ResolveFallback(new[]
        {
            "1/2 effect magic"
        });

        Assert.Equal(2, resolved.ResistanceMultipliers["Magic"]);
    }

    [Fact]
    public void ResolveFallback_ParsesHalfSpirit_AsSpiritMultiplier()
    {
        var resolved = BattleboardAdvancementEffectResolver.ResolveFallback(new[]
        {
            "Alfar have only half a spirit, in the same manner as Half-Elves."
        });

        Assert.Equal(2, resolved.ResistanceMultipliers["Spirit"]);
    }

    [Fact]
    public void ResolveFallback_ParsesSpiritlessAndMindless_AsInfiniteResistance()
    {
        var resolved = BattleboardAdvancementEffectResolver.ResolveFallback(new[]
        {
            "Spiritless",
            "Mindless"
        });

        Assert.Contains("Spirit", resolved.InfiniteResistanceTypes);
        Assert.Contains("Neuro", resolved.InfiniteResistanceTypes);
    }
}
