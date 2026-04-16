using System;
using System.Linq;
using labyItems.Services;
using labyItems.Services.Specialisations;
using Xunit;

namespace labyItems.Tests;

public sealed class SpecialisationServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsSpecialisationsAndParsesColourAbilityBlocks()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();
        var all = await SpecialisationService.GetAllAsync();

        Assert.NotEmpty(all);
        Assert.Contains(all.Values, record => record.ColourAbilities is { Count: > 0 });
    }

    [Fact]
    public async Task GetAllAsync_CrolSubtypeDataIncludesLoreAndUpdatedCasteRules()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();
        var all = await SpecialisationService.GetAllAsync();
        var crolSubtype = Assert.Contains("CrolSubtypeAbilities", all);
        Assert.NotNull(crolSubtype.ColourAbilities);
        var mapped = crolSubtype.ColourAbilities!;

        var yashtar = Assert.Contains("Yashtar", mapped!);
        Assert.Equal("Ogre", yashtar.LifeScaleOverride);
        Assert.Contains("Warrior", yashtar.ClassRestriction ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(yashtar.Roleplay);
        Assert.NotEmpty(yashtar.Lore);
        Assert.Contains("1 PAC", yashtar.Levels?["3"].Select(a => a.Name) ?? Enumerable.Empty<string>());
        Assert.Contains("2 PAC", yashtar.Levels?["5"].Select(a => a.Name) ?? Enumerable.Empty<string>());
        Assert.Contains("3 PAC", yashtar.Levels?["7"].Select(a => a.Name) ?? Enumerable.Empty<string>());
        Assert.Contains("4 PAC", yashtar.Levels?["8"].Select(a => a.Name) ?? Enumerable.Empty<string>());

        var jaerseen = Assert.Contains("Jaerseen", mapped);
        Assert.Equal("Crol (Jaerseen)", jaerseen.LifeScaleOverride);
        Assert.DoesNotContain("Grey", jaerseen.ColourChoiceOverride ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAllAsync_ElfColourAbilities_UsesOverviewDescription()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();
        var all = await SpecialisationService.GetAllAsync();

        var elfColour = Assert.Contains("ElfColourAbilities", all);
        Assert.Contains("immune to spiritual effects", elfColour.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Elven Abilities", elfColour.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAllAsync_UnholyChampionDevotion_IncludesRepelGoodAndPermanentCurse()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();
        var all = await SpecialisationService.GetAllAsync();
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var devotion = Assert.Contains("Unholy Champion Devotion", all);
        Assert.True((devotion.Abilities?.Count ?? 0) + (devotion.Options?.Count ?? 0) > 0);

        var definition = Assert.Contains("Unholy Champion Devotion", index.Definitions);
        var choiceSet = Assert.Single(definition.ChoiceSets);

        var repelGood = Assert.Single(choiceSet.Options.Where(option => option.Label == "Repel Good"))
            .Grants
            .Single()
            .Ability;
        Assert.NotNull(repelGood);
        Assert.NotNull(repelGood.Progression);
        Assert.Equal(1, repelGood.Progression!.Amount);
        Assert.Equal(2, repelGood.Progression.PerLevels);

        var permanentCurse = Assert.Single(choiceSet.Options.Where(option => option.Label == "Permanent Beneficial Curse"))
            .Grants
            .Single()
            .Ability;
        Assert.NotNull(permanentCurse);
        Assert.Equal("Curse", permanentCurse.Source);
        Assert.Equal("DAC", permanentCurse.Type);
        Assert.Equal(2, permanentCurse.Count);
    }
}
