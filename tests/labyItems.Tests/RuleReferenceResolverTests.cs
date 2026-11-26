using labyItems.Models.Rules;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class RuleReferenceResolverTests : ServiceTestBase
{
    [Fact]
    public async Task DarkBargainsKeepsTouchOfDeathOutOfMechanicalOrder()
    {
        var result = await RuleCatalogueService.ResolveAbilityAsync(
            "ability.guild.church-of-the-tryst.the-dark-bargains");

        Assert.Empty(result.Diagnostics);
        Assert.Contains("mantic comm 2", result.Root?.Text);
        Assert.Equal(["Communication", "Greater Communication", "The dark bargains"],
            result.MechanicalOrder.Select(rule => rule.Name));
        Assert.Contains(result.Links, link =>
            link.To.Name == "Touch of Death" && link.Relationship == RuleRelationship.ComparableTo);
        Assert.Contains(result.RelatedRules, rule => rule.Name == "Total Body Life Points");
    }

    [Fact]
    public async Task MercenaryProblemsUsesOneMajorPrayerRuleAndItsDependencies()
    {
        var result = await RuleCatalogueService.ResolveAbilityAsync(
            "ability.guild.the-order-of-mercenaries.mercenary-problems-1-day");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(["Communication", "Greater Communication", "Major Prayer", "Mercenary Problems 1/day"],
            result.MechanicalOrder.Select(rule => rule.Name));
    }

    [Fact]
    public async Task SwarmTrapWaryLinksToTheExistingScoutSkill()
    {
        var result = await RuleCatalogueService.ResolveAbilityAsync(
            "ability.guild.the-swarm.trap-wary-for-scurry-innates");

        Assert.Empty(result.Diagnostics);
        var reference = Assert.Single(result.Links);
        Assert.Equal(RuleCatalogueKind.ScoutSkill, reference.To.Catalogue);
        Assert.Equal("Trap-Wary", reference.To.Name);
        Assert.Equal(RuleRelationship.References, reference.Relationship);
    }

    [Fact]
    public void MissingAndAmbiguousReferencesAreDiagnosedWithoutDiscardingIndependentRules()
    {
        var catalogue = RuleCatalogueKind.Ability;
        var root = new RuleDefinition(catalogue, "root", "Root", "Source text", "Almanac",
            [new(catalogue, "known", RuleRelationship.Inherits), new(catalogue, "unclear", RuleRelationship.References),
                new(catalogue, "missing", RuleRelationship.Requires)]);
        var known = new RuleDefinition(catalogue, "known", "Known", null, null, []);
        var other = new RuleDefinition(catalogue, "other", "Other", null, null, []);
        var index = new RuleReferenceIndex([root, known, other], [(catalogue, "unclear", "known"), (catalogue, "unclear", "other")]);

        var result = new RuleReferenceResolver(index).Resolve(catalogue, "root");

        Assert.Equal("Source text", result.Root?.Text);
        Assert.Equal(["known", "root"], result.MechanicalOrder.Select(rule => rule.Identifier));
        Assert.Single(result.RelatedRules);
        Assert.Equal([RuleResolutionDiagnosticKind.AmbiguousReference, RuleResolutionDiagnosticKind.MissingReference],
            result.Diagnostics.Select(diagnostic => diagnostic.Kind));
    }

    [Theory]
    [InlineData(RuleRelationship.References, false)]
    [InlineData(RuleRelationship.Inherits, true)]
    public void ReportsOnlyMechanicalCyclesAndDeduplicatesRelatedRules(RuleRelationship backLink, bool invalid)
    {
        var catalogue = RuleCatalogueKind.Ability;
        var root = new RuleDefinition(catalogue, "root", "Root", null, null,
            [new(catalogue, "target", RuleRelationship.Modifies), new(catalogue, "alternate", RuleRelationship.Modifies)]);
        var target = new RuleDefinition(catalogue, "target", "Target", null, null, [new(catalogue, "root", backLink)]);
        var index = new RuleReferenceIndex([root, target], [(catalogue, "alternate", "target")]);

        var result = new RuleReferenceResolver(index).Resolve(catalogue, "root");

        Assert.Single(result.RelatedRules);
        Assert.Equal(2, result.Links.Count);
        Assert.Equal(invalid, result.Diagnostics.Any(diagnostic => diagnostic.Kind == RuleResolutionDiagnosticKind.CircularInheritance));
        Assert.Equal(invalid ? 0 : 2, result.MechanicalOrder.Count);
    }
}
