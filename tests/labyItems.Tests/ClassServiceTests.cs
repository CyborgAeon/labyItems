using System.Linq;
using labyItems.Models.ViewModels;
using labyItems.Pages.ClassCard;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class ClassServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAllAsync_LoadsClassDefinitions()
    {
        var all = await ClassService.GetAllAsync();

        Assert.NotEmpty(all);
        Assert.Contains(all, kvp => kvp.Value != null && kvp.Value.Levels.Count > 0);
    }

    [Fact]
    public async Task EveryClass_HasResolvableSourceBook()
    {
        var all = await ClassService.GetAllAsync();

        Assert.All(all, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Value.SourceBook), $"{entry.Key} has no SourceBook.");
            Assert.True(
                BookDirectory.TryResolve(entry.Value.SourceBook, out _),
                $"{entry.Key} has unmapped SourceBook '{entry.Value.SourceBook}'.");
        });
    }

    [Fact]
    public async Task PaladinProgressionAbilities_AreParsed()
    {
        var all = await ClassService.GetAllAsync();
        var paladin = all["Paladin"];

        var layOnHands = paladin.Levels["3"].First(a => a.Name == "Lay on hands");
        var blessOther = paladin.Levels["4"].First(a => a.Name == "Bless Other");
        var repelUndead = paladin.Levels["6"].First(a => a.Name == "Repel Undead");

        Assert.NotNull(layOnHands.Progression);
        Assert.Equal(3, layOnHands.Progression!.Amount);
        Assert.Equal(1, layOnHands.Progression.PerLevels);

        Assert.NotNull(blessOther.Progression);
        Assert.Equal(1, blessOther.Progression!.Amount);
        Assert.Equal(2, blessOther.Progression.PerLevels);

        Assert.NotNull(repelUndead.Progression);
        Assert.Equal(1, repelUndead.Progression!.Amount);
        Assert.Equal(4, repelUndead.Progression.PerLevels);
    }

    [Fact]
    public async Task UnholyChampion_LevelTwo_UsesDevotionSpecialisation()
    {
        var all = await ClassService.GetAllAsync();
        var unholyChampion = all["Unholy Champion"];

        var level2 = unholyChampion.Levels["2"];
        var level3 = unholyChampion.Levels["3"];
        var devotion = level2.First(a => a.Name == "Unholy Champion Devotion");

        Assert.Equal("Overwrite", devotion.Type);
        Assert.Equal("Unholy Champion Devotion", devotion.OverwriteKey);
        Assert.DoesNotContain(level3, a => a.Name == "Repel Good");
    }

    [Fact]
    public async Task MonkProgression_SeparatesWeaponSkillsAndArmourGrants()
    {
        var monk = (await ClassService.GetAllAsync())["Monk"];

        var progression = ClassProgressionProjector.BuildProgressionRows(monk.Levels);
        var weaponSkills = ClassProgressionProjector.BuildWeaponSkillRows(monk.Levels);

        Assert.Equal(new[] { "O", "E", "H", "B", "MP", "UAC" }, weaponSkills.Select(row => row.Name));
        Assert.Contains(progression, row => row.Level == 2
            && row.Name == "Detect Possible Neuronic Activity"
            && row.ArmourText == "1 DAC");
        Assert.Contains(progression, row => row.Level == 5
            && row.Name == string.Empty
            && row.ArmourText == "3 DAC");
        Assert.Contains(progression, row => row.Level == 6
            && row.Name == "1st Weapon Mastery (H)"
            && row.ArmourText == "4 DAC");
        Assert.Contains(progression, row => row.Level == 6
            && row.Name == "Immunity to Befriend, Immunity to Beguile, Immunity to Spiritual Mastery"
            && row.ArmourText == string.Empty);
        Assert.Contains(progression, row => row.Level == 8
            && row.Name == "Immunity to Poison, Immunity to Bless/Curse, Monk Specialist"
            && row.ArmourText == string.Empty);
    }

    [Fact]
    public async Task AlfarProgression_IncludesArmourAmountAndType()
    {
        var alfar = (await PeopleService.GetAllAsync())["Alfar"];
        var row = LevelAbilityRowBuilder.Build(6, alfar.LevelledAbilities["6"]);

        Assert.Equal("1 DAC", row.ArmourText);
        Assert.Equal("No abilities gained", row.AbilitiesDisplayText);
    }

}
