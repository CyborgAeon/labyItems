using System;
using System.Linq;
using labyItems.Services;
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
}
