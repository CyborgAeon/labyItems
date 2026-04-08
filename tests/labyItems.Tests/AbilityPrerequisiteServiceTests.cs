using System.Collections.Generic;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class AbilityPrerequisiteServiceTests : ServiceTestBase
{
    [Fact]
    public void Evaluate_ReportsMissingAbilityPrerequisites()
    {
        var focus = BuildAbility("Focus", 1, "ability.focus");
        var arcaneAttunement = BuildAbility(
            "Arcane Attunement",
            1,
            "ability.arcane-attunement",
            preReqs: new[] { "Ability:Focus" });

        var result = AbilityPrerequisiteService.Evaluate(
            selectedAbilities: new[] { arcaneAttunement },
            knownAbilityTerms: Array.Empty<string>(),
            abilityCatalog: new[] { focus, arcaneAttunement });

        Assert.True(result.HasIssues);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("Arcane Attunement", issue.AbilityName);
        Assert.Contains("Focus", issue.MissingPrerequisites);
        Assert.Contains("ability.focus", result.MissingPrerequisiteKeys);
    }

    [Fact]
    public void Evaluate_UsesKnownAbilitiesToSatisfyPrerequisites()
    {
        var focus = BuildAbility("Focus", 1, "ability.focus");
        var arcaneAttunement = BuildAbility(
            "Arcane Attunement",
            1,
            "ability.arcane-attunement",
            preReqs: new[] { "Ability:Focus" });

        var result = AbilityPrerequisiteService.Evaluate(
            selectedAbilities: new[] { arcaneAttunement },
            knownAbilityTerms: new[] { "Focus" },
            abilityCatalog: new[] { focus, arcaneAttunement });

        Assert.False(result.HasIssues);
        Assert.Empty(result.MissingPrerequisiteKeys);
    }

    [Fact]
    public void Evaluate_IgnoresNonAbilityPrerequisites()
    {
        var gated = BuildAbility(
            "Wizard Gate",
            1,
            "ability.wizard-gate",
            preReqs: new[] { "Class:Wizard" });

        var result = AbilityPrerequisiteService.Evaluate(
            selectedAbilities: new[] { gated },
            knownAbilityTerms: Array.Empty<string>(),
            abilityCatalog: new[] { gated });

        Assert.False(result.HasIssues);
        Assert.Empty(result.MissingPrerequisiteKeys);
    }

    [Fact]
    public void Evaluate_SatisfiesRankedStrengthAndWeaponMasteryPrerequisitesAcrossEquivalentTerms()
    {
        var veteranFighter = BuildAbility(
            "Veteran Fighter",
            9,
            "ability.veteran-fighter",
            preReqs: new[] { "+1 Weapon Mastery", "+1 Strength" });

        var firstStrength = BuildAbility("1st Grade of Strength", 1, "ability.strength.1");
        var secondMastery = BuildAbility("2nd Weapon Mastery", 2, "ability.wm.2");

        var result = AbilityPrerequisiteService.Evaluate(
            selectedAbilities: new[] { veteranFighter },
            knownAbilityTerms: new[] { "1st Grade of Strength", "2nd Weapon Mastery" },
            abilityCatalog: new[] { veteranFighter, firstStrength, secondMastery });

        Assert.False(result.HasIssues);
        Assert.Empty(result.MissingPrerequisiteKeys);
    }

    [Fact]
    public void Evaluate_ParsesGrantedStrengthFromKnownEffectText()
    {
        var veteranFighter = BuildAbility(
            "Veteran Fighter",
            9,
            "ability.veteran-fighter",
            preReqs: new[] { "+1 Strength" });

        var result = AbilityPrerequisiteService.Evaluate(
            selectedAbilities: new[] { veteranFighter },
            knownAbilityTerms: new[] { "Grants +1 stacking Strength, max +3." },
            abilityCatalog: new[] { veteranFighter });

        Assert.False(result.HasIssues);
        Assert.Empty(result.MissingPrerequisiteKeys);
    }

    private static EvolutionService.AbilityResult BuildAbility(
        string name,
        int table,
        string abilityRef,
        IReadOnlyList<string>? preReqs = null)
    {
        return new EvolutionService.AbilityResult
        {
            Index = name,
            Table = table,
            AbilityRef = abilityRef,
            PreReqs = preReqs ?? Array.Empty<string>()
        };
    }
}
