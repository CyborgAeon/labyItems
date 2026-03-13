using System.IO;
using ClosedXML.Excel;
using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardServiceTests : ServiceTestBase
{
    [Fact]
    public async Task ExportAsync_WritesBattleboardWorkbook()
    {
        var draft = new CharacterDraft
        {
            Name = "Test Character",
            PlayerName = "Tester",
            Class = "Wizard",
            Race = "Human",
            TBLP = 40,
            Loc = 5,
            MaxAC = 20,
            WornArmour = 2,
            Points = 50,
            Alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral)
        };
        draft.PowerPools["Arcane"] = 10;
        draft.Guilds.Add("Test Guild");
        draft.SpecialisationSelections["Wizard Colour"] = "Red";
        draft.Abilities.Add(new AbilityDraft { Name = "+2 PAC", AbilityType = AbilityType.Pac, Count = 2 });
        draft.Abilities.Add(new AbilityDraft { Name = "Combat Wary", AbilityType = AbilityType.Static, Frequency = "1", LevelGained = 1 });
        draft.Innates.Add(new InnateAbilityDraft { Name = "Innate Sight", Rank = 2 });

        var service = new BattleboardExportService();

        var outputPath = await service.ExportAsync(draft);

        Assert.True(File.Exists(outputPath));
        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("BBoard");
        Assert.Equal("Test Character", sheet.Cell("B2").GetString());
    }

    [Fact]
    public async Task ExportAsync_CombatWaryFrequencyRankUsesLevelsSinceSelected()
    {
        var draft = new CharacterDraft
        {
            Name = "Scout Tester",
            PlayerName = "Tester",
            Class = "Pathfinder",
            Race = "Human",
            TBLP = 40,
            Loc = 5,
            MaxAC = 20,
            Alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral)
        };

        draft.Abilities.Add(new AbilityDraft
        {
            Name = "Combat-Wary",
            AbilityType = AbilityType.Static,
            Frequency = "1",
            LevelGained = 2
        });

        var service = new BattleboardExportService();
        var outputPath = await service.ExportAsync(draft);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("BBoard");

        Assert.Equal("Combat Wary", sheet.Cell("AA4").GetString());
        Assert.Equal(7, sheet.Cell("AD4").GetValue<int>());
    }

    [Fact]
    public async Task ExportAsync_ArmourUsesHighestPerSource()
    {
        var draft = new CharacterDraft
        {
            Name = "Armour Tester",
            PlayerName = "Tester",
            Class = "Pathfinder",
            Race = "Human",
            TBLP = 40,
            Loc = 5,
            MaxAC = 20,
            Alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral)
        };

        draft.Abilities.Add(new AbilityDraft { Name = "Class DAC 1", AbilityType = AbilityType.Dac, Count = 1, Source = "Class" });
        draft.Abilities.Add(new AbilityDraft { Name = "Class DAC 4", AbilityType = AbilityType.Dac, Count = 4, Source = "Class" });
        draft.Abilities.Add(new AbilityDraft { Name = "Guild A DAC 1", AbilityType = AbilityType.Dac, Count = 1, Source = "Guild:Iron & Empire" });
        draft.Abilities.Add(new AbilityDraft { Name = "Guild A DAC 3", AbilityType = AbilityType.Dac, Count = 3, Source = "Guild:Iron & Empire" });
        draft.Abilities.Add(new AbilityDraft { Name = "Guild B DAC 2", AbilityType = AbilityType.Dac, Count = 2, Source = "Guild:Second Guild" });

        var service = new BattleboardExportService();
        var outputPath = await service.ExportAsync(draft);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("BBoard");

        Assert.Equal(9, sheet.Cell("U4").GetValue<int>());
    }
}
