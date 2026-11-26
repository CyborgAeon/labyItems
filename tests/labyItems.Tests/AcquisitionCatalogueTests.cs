using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class AcquisitionCatalogueTests : ServiceTestBase
{
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 4)]
    [InlineData(6, 6)]
    public async Task HealingSapUnlocksAtFourAndScalesFromTotalMultiraceLevel(int level, int expected)
    {
        var draft = new CharacterDraft { MultiRaceKey = "Child of the Pines", MultiRaceLevel = level };
        var abilities = await MultiRaceInnateResolver.ResolveAsync(draft);

        Assert.Equal(expected, abilities.Where(ability => ability.Name == "Healing Sap").Sum(ability => ability.Rank));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(6, 8)]
    public async Task AlignedElfUsesTheSelectedRacialInnateProgression(int level, int expected)
    {
        var draft = new CharacterDraft { MultiRaceKey = "Aligned Elf", MultiRaceLevel = level };
        draft.MultiRaceChoiceSelections["choice.elfcolourabilities.mapped"] = "Earth";
        var abilities = await MultiRaceInnateResolver.ResolveAsync(draft);

        Assert.Equal(expected, Assert.Single(abilities, ability => ability.Name == "Body of Stone").Rank);
    }

    [Fact]
    public async Task SchemaTwoMulticlassHydratesOnlyOmittedCanonicalText()
    {
        var catalogue = await MultiClassService.GetCatalogAsync();
        Assert.Equal(2, catalogue.SchemaVersion);
        var waking = Assert.Single(catalogue.MultiClasses["Awoken Mind"].Levels["1"]);
        Assert.False(string.IsNullOrWhiteSpace(waking.Name));
        Assert.False(string.IsNullOrWhiteSpace(waking.Effect));

        var draft = new CharacterDraft();
        draft.MultiClassLevels["Art of Battle"] = 5;
        draft.MultiClassLevels["Psycher"] = 6;
        draft.Innates.Add(new InnateAbilityDraft { Name = "Neuronic Penetration", Rank = 24 });

        var intervals = await MultiClassUsageService.ResolveAsync(draft);
        Assert.Equal(10, Assert.Single(intervals, usage => usage.Source == "Art of Battle").Policy.Minutes);
        Assert.Equal(2, Assert.Single(intervals, usage => usage.Source == "Art of Battle").Policy.Uses);
        Assert.Equal(24, Assert.Single(intervals, usage => usage.Source == "Psycher").Policy.RequiredInnates);
        Assert.Equal(24, Assert.Single(draft.Innates).Rank);
    }
}
