using labyItems.Services;
using SQLite;
using Xunit;

namespace labyItems.Tests;

public sealed class ManuAbilityServiceTests : ServiceTestBase
{
    [Fact]
    public async Task SearchMergedCatalogAsync_ReturnsMergedAbilitiesAcrossSourceBooks()
    {
        AddEvolutionAbility(
            index: "Merged Catalog Class Ability",
            description: "Class source row",
            cost: 20,
            table: 1,
            available: "Any",
            dataJson: "{\"sourceBook\":\"Classes\",\"abilityRef\":\"ability.test.merged-catalog-class\"}");

        AddEvolutionAbility(
            index: "Merged Catalog Make Ability",
            description: "Manufacturer source row",
            cost: 20,
            table: 1,
            available: "Any",
            dataJson: "{\"sourceBook\":\"Manufacturers Guide\",\"abilityRef\":\"ability.make.merged-catalog-make\"}");

        var results = await ManuAbilityService.SearchMergedCatalogAsync(string.Empty);

        Assert.Contains(results, entry =>
            entry.name.Equals("Merged Catalog Class Ability", StringComparison.OrdinalIgnoreCase)
            && entry.sourceBook.Equals("Classes", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, entry =>
            entry.name.Equals("Merged Catalog Make Ability", StringComparison.OrdinalIgnoreCase)
            && entry.sourceBook.Equals("Manufacturers Guide", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchAsync_RemainsManufacturingOnly()
    {
        AddEvolutionAbility(
            index: "Manufacturing Filter Class Ability",
            description: "Class source row",
            cost: 20,
            table: 1,
            available: "Any",
            dataJson: "{\"sourceBook\":\"Classes\",\"abilityRef\":\"ability.test.manufacturing-filter-class\"}");

        AddEvolutionAbility(
            index: "Manufacturing Filter Make Ability",
            description: "Manufacturer source row",
            cost: 20,
            table: 1,
            available: "Any",
            dataJson: "{\"sourceBook\":\"Manufacturers Guide\",\"abilityRef\":\"ability.make.manufacturing-filter-make\"}");

        var results = await ManuAbilityService.SearchAsync(string.Empty);

        Assert.DoesNotContain(results, entry =>
            entry.name.Equals("Manufacturing Filter Class Ability", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, entry =>
            entry.name.Equals("Manufacturing Filter Make Ability", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddEvolutionAbility(
        string index,
        string description,
        int cost,
        int table,
        string available,
        string dataJson)
    {
        var evolutionId = SQLiteTestStore.AddEvolution(
            index,
            description,
            cost,
            table,
            available,
            canBuyMultiple: 0,
            preReqsJson: "[]",
            dataJson: dataJson);

        var searchable = ServiceHelper.NormalizeForNgrams($"{index} {description}".ToLowerInvariant());
        var ngrams = ServiceHelper.GenerateNGrams(searchable, 3)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var token in ngrams)
            SQLiteTestStore.AddEvolutionNgram(evolutionId, token);

        EvolutionService.InvalidateCache();
    }
}
