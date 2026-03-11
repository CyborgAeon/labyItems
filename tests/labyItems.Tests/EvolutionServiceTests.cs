using System.Linq;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class EvolutionServiceTests : ServiceTestBase
{
    private const string LegacyResistanceName = "9th Level Resistance to Spirits and Magic";
    private const string CanonicalResistanceName = "9th Level Resistance to Magic and Spirits";

    [Fact]
    public async Task GetAllAbilitiesAsync_ParsesPrereqsAndMaxAvailable()
    {
        var all = await EvolutionService.GetAllAbilitiesAsync();

        var entry = Assert.Single(all, e => e.Index == "AA1");
        Assert.Contains("Ability:Focus", entry.PreReqs);
        Assert.Equal(2, entry.MaxAvailable);
    }

    [Fact]
    public async Task GetAllAsync_FlagsImmunityIndices()
    {
        var all = await EvolutionService.GetAllAsync();

        var immunity = Assert.Single(all, e => e.Index == "Immunity! Fire");
        Assert.True(immunity.IsImmunity);
    }

    [Fact]
    public async Task SearchByIndexAsync_ReturnsNgramMatches()
    {
        var result = await EvolutionService.SearchByIndexAsync("Arcane");

        var hit = Assert.Single(result);
        Assert.Equal("AA1", hit.Index);
    }

    [Fact]
    public void NormalizeAbilityDisplayText_MapsLegacyResistanceNameToCanonical()
    {
        var normalized = EvolutionService.NormalizeAbilityDisplayText(LegacyResistanceName);
        Assert.Equal(CanonicalResistanceName, normalized);
    }

    [Fact]
    public void GetEquivalentAbilityNames_IncludesBothLegacyAndCanonicalForRenamedAbility()
    {
        var names = EvolutionService.GetEquivalentAbilityNames(CanonicalResistanceName);

        Assert.Contains(CanonicalResistanceName, names);
        Assert.Contains(LegacyResistanceName, names);
    }

    [Fact]
    public void AbilityLookup_FindsCanonicalAbilityWhenQueriedByLegacyName()
    {
        var canonicalAbility = new EvolutionService.AbilityResult
        {
            Index = CanonicalResistanceName,
            Description = CanonicalResistanceName,
            Cost = 0,
            Table = 3,
            Available = "Any",
            CanBuyMultiple = false
        };

        var lookup = new Dictionary<string, EvolutionService.AbilityResult>(StringComparer.OrdinalIgnoreCase)
        {
            [AbilityDetailsLookupService.NormalizeKey(CanonicalResistanceName)] = canonicalAbility
        };

        var found = AbilityDetailsLookupService.FindByIndex(lookup, LegacyResistanceName);
        Assert.NotNull(found);
        Assert.Equal(CanonicalResistanceName, found!.Index);
    }
}
