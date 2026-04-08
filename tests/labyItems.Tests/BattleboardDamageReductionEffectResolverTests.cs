using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardDamageReductionEffectResolverTests : ServiceTestBase
{
    [Fact]
    public void ResolveFallback_SpiritualVessel_LevelThree_AppliesAllButSpirit()
    {
        var draft = new CharacterDraft
        {
            MultiRaceKey = "Spiritual Vessel",
            MultiRaceLevel = 3
        };

        var resolved = BattleboardDamageReductionEffectResolver.ResolveFallback(draft);

        Assert.Equal(3, resolved.DamagePerSixths["Physical"]);
        Assert.Equal(3, resolved.DamagePerSixths["Magic"]);
        Assert.Equal(3, resolved.DamagePerSixths["Neuro"]);
        Assert.DoesNotContain("Spirit", resolved.DamagePerSixths.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveFallback_VesselOfTheDragons_LevelSix_AppliesAllButMagic()
    {
        var draft = new CharacterDraft
        {
            MultiRaceKey = "Vessel of the Dragons",
            MultiRaceLevel = 6
        };

        var resolved = BattleboardDamageReductionEffectResolver.ResolveFallback(draft);

        Assert.Equal(6, resolved.DamagePerSixths["Physical"]);
        Assert.Equal(6, resolved.DamagePerSixths["Spirit"]);
        Assert.Equal(6, resolved.DamagePerSixths["Neuro"]);
        Assert.DoesNotContain("Magic", resolved.DamagePerSixths.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveFallback_MindOverReality_DoesNotApplyDamagePerSixths()
    {
        var draft = new CharacterDraft
        {
            MultiRaceKey = "Mind Over Reality",
            MultiRaceLevel = 6
        };

        var resolved = BattleboardDamageReductionEffectResolver.ResolveFallback(draft);
        Assert.Empty(resolved.DamagePerSixths);
    }
}
