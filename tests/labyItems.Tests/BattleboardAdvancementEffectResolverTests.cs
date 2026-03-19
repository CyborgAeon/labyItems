using System.Collections.Generic;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardAdvancementEffectResolverTests
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
}
