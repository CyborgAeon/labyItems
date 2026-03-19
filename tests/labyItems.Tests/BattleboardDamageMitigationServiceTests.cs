using System;
using System.Collections.Generic;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardDamageMitigationServiceTests : ServiceTestBase
{
    [Fact]
    public void ApplyPostArmourMitigation_HalfEffectMagic_RoundsUpAfterDivision()
    {
        var multipliers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Magic"] = 2
        };

        var (tblp, loc) = BattleboardDamageMitigationService.ApplyPostArmourMitigation(
            tblp: 18,
            loc: 3,
            channel: "Magic",
            getResistanceMultiplier: ResolveMultiplier(multipliers),
            hasInfiniteResistance: _ => false,
            isMiracleSource: false,
            isMantic: false);

        Assert.Equal(9, tblp);
        Assert.Equal(2, loc);
    }

    [Fact]
    public void ApplyPostArmourMitigation_Spiritless_BlocksNonManticMiracles()
    {
        var infinite = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Spirit"
        };

        var (tblp, loc) = BattleboardDamageMitigationService.ApplyPostArmourMitigation(
            tblp: 12,
            loc: 2,
            channel: string.Empty,
            getResistanceMultiplier: _ => 1,
            hasInfiniteResistance: ResolveInfinite(infinite),
            isMiracleSource: true,
            isMantic: false);

        Assert.Equal(0, tblp);
        Assert.Equal(0, loc);
    }

    [Fact]
    public void ApplyPostArmourMitigation_Spiritless_BlocksSpiritDamage()
    {
        var infinite = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Spirit"
        };

        var (tblp, loc) = BattleboardDamageMitigationService.ApplyPostArmourMitigation(
            tblp: 18,
            loc: 0,
            channel: "Spirit",
            getResistanceMultiplier: _ => 1,
            hasInfiniteResistance: ResolveInfinite(infinite),
            isMiracleSource: false,
            isMantic: false);

        Assert.Equal(0, tblp);
        Assert.Equal(0, loc);
    }

    [Fact]
    public void ApplyPostArmourMitigation_Spiritless_AllowsManticMiracleSpiritDamage()
    {
        var infinite = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Spirit"
        };

        var (tblp, loc) = BattleboardDamageMitigationService.ApplyPostArmourMitigation(
            tblp: 10,
            loc: 2,
            channel: "Spirit",
            getResistanceMultiplier: _ => 1,
            hasInfiniteResistance: ResolveInfinite(infinite),
            isMiracleSource: true,
            isMantic: true);

        Assert.Equal(10, tblp);
        Assert.Equal(2, loc);
    }

    [Fact]
    public void ApplyPostArmourMitigation_HalfSpirit_AppliesAfterArmourReduction()
    {
        var multipliers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Spirit"] = 2
        };

        var (tblp, loc) = BattleboardDamageMitigationService.ApplyPostArmourMitigation(
            tblp: 16,
            loc: 0,
            channel: "Spirit",
            getResistanceMultiplier: ResolveMultiplier(multipliers),
            hasInfiniteResistance: _ => false,
            isMiracleSource: true,
            isMantic: false);

        Assert.Equal(8, tblp);
        Assert.Equal(0, loc);
    }

    [Fact]
    public void ApplyPostArmourMitigation_Mindless_BlocksNeuronicDamage()
    {
        var infinite = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Neuro"
        };

        var (tblp, loc) = BattleboardDamageMitigationService.ApplyPostArmourMitigation(
            tblp: 12,
            loc: 3,
            channel: "Neuronic",
            getResistanceMultiplier: _ => 1,
            hasInfiniteResistance: ResolveInfinite(infinite),
            isMiracleSource: false,
            isMantic: false);

        Assert.Equal(0, tblp);
        Assert.Equal(0, loc);
    }

    private static Func<string, int> ResolveMultiplier(IReadOnlyDictionary<string, int> values)
        => type => values.TryGetValue(type, out var value) ? Math.Max(1, value) : 1;

    private static Func<string, bool> ResolveInfinite(IReadOnlySet<string> values)
        => type => values.Contains(type);
}
