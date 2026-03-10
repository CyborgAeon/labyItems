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
}
