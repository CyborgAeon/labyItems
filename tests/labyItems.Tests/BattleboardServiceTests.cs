using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using labyItems.Models;
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

    [Fact]
    public async Task ExportAsync_IncludesAssignedItemLife_WithTblpCapAndUncappedLoc()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "battleboard-item-life",
            Name = "Wizard Life Item",
            PlayerName = "Tester",
            Class = "Wizard",
            Race = "Human",
            TBLP = 29,
            Loc = 9,
            MaxAC = 20,
            Points = 500,
            Alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral)
        };

        var item = new Item
        {
            Id = Guid.NewGuid().ToString("N"),
            AssignedCharacterId = draft.CharacterRecordId,
            AssignedCharacterName = draft.Name,
            AssignedCharacterPlayerName = draft.PlayerName,
            CreatedDate = DateTime.UtcNow
        };

        var payload = ItemEmailService.BuildItemPayload(item, new[]
        {
            new CalcResult
            {
                AbilityType = "Life",
                AbilityName = "24/8",
                Details = new Dictionary<string, object?> { ["life"] = "24/8" }
            }
        });

        item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);

        var service = new BattleboardExportService(_ => new[] { item });
        var outputPath = await service.ExportAsync(draft);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("BBoard");

        Assert.Equal(44, sheet.Cell("C3").GetValue<int>());
        Assert.Equal(17, sheet.Cell("W3").GetValue<int>());
    }

    [Fact]
    public async Task ExportAsync_IncludesAssignedWearableArmourPac_FromItem()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "battleboard-item-armour",
            Name = "Wizard Armour Item",
            PlayerName = "Tester",
            Class = "Wizard",
            Race = "Human",
            TBLP = 29,
            Loc = 9,
            WornArmour = 6,
            ArmourAvailability = "Heavy",
            MaxAC = 20,
            Alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral)
        };

        var item = new Item
        {
            Id = Guid.NewGuid().ToString("N"),
            AssignedCharacterId = draft.CharacterRecordId,
            AssignedCharacterName = draft.Name,
            AssignedCharacterPlayerName = draft.PlayerName,
            CreatedDate = DateTime.UtcNow
        };

        var payload = ItemEmailService.BuildItemPayload(item, new[]
        {
            new CalcResult
            {
                AbilityType = "Armour",
                AbilityName = "Plate mail",
                Details = new Dictionary<string, object?>
                {
                    ["AC"] = 8,
                    ["enhancementBonuses"] = new[]
                    {
                        new Dictionary<string, object> { ["type"] = "PAC", ["value"] = 6 }
                    }
                }
            }
        });

        item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);

        var service = new BattleboardExportService(_ => new[] { item });
        var outputPath = await service.ExportAsync(draft);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("BBoard");

        Assert.Equal(14, sheet.Cell("U3").GetValue<int>());
    }

    [Fact]
    public async Task ExportAsync_IncludesAssignedItemInnates()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "battleboard-item-innates",
            Name = "Wizard Innate Item",
            PlayerName = "Tester",
            Class = "Wizard",
            Race = "Human",
            TBLP = 29,
            Loc = 9,
            MaxAC = 20,
            Alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral)
        };

        var item = new Item
        {
            Id = Guid.NewGuid().ToString("N"),
            AssignedCharacterId = draft.CharacterRecordId,
            AssignedCharacterName = draft.Name,
            AssignedCharacterPlayerName = draft.PlayerName,
            CreatedDate = DateTime.UtcNow
        };

        var payload = ItemEmailService.BuildItemPayload(item, new[]
        {
            new CalcResult
            {
                AbilityType = "MonsterPoint",
                AbilityName = "Spell: Fireblade (lvl 6 handbook) x6",
                Summary = "Spell: Fireblade (lvl 6 handbook) x6 = 72",
                Details = new Dictionary<string, object?>
                {
                    ["source"] = "monster-point"
                }
            }
        });

        item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);

        var resolvedInnates = BattleboardInnateCalculator.Calculate(draft, new[] { item });
        var parsedInnate = Assert.Single(resolvedInnates);
        Assert.Equal("Fireblade", parsedInnate.Name);
        Assert.Equal(6, parsedInnate.Rank);

        var service = new BattleboardExportService(_ => new[] { item });
        var outputPath = await service.ExportAsync(draft);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("BBoard");

        var innateNames = Enumerable.Range(17, 38)
            .Select(row => sheet.Cell($"B{row}").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        Assert.Contains("Fireblade", innateNames);
    }

    [Fact]
    public async Task ExportAsync_AppliesAdvancementResistanceLevelsAndImmunities()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "battleboard-advancement-effects",
            Name = "Advancement Effects Tester",
            PlayerName = "Tester",
            Class = "Wizard",
            Race = "Human",
            TBLP = 29,
            Loc = 9,
            MaxAC = 20,
            Alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Neutral)
        };

        draft.AdvancementAbilities.Add("9th Level Resistance to Neuronics");
        draft.AdvancementAbilities.Add("Immunity to Repels");

        var service = new BattleboardExportService();
        var outputPath = await service.ExportAsync(draft);

        using var workbook = new XLWorkbook(outputPath);
        var sheet = workbook.Worksheet("BBoard");

        Assert.Equal(9, sheet.Cell("AD22").GetValue<int>());

        var immunities = Enumerable.Range(26, 8)
            .Select(row => sheet.Cell($"AC{row}").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        Assert.Contains("Repels", immunities);
    }
}
