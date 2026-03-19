using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardLifeCalculatorTests : ServiceTestBase
{
    [Fact]
    public void Calculate_UsesHighestPerSource_StacksTables_AndCapsTblpOnly()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "calc-life-1",
            Name = "Life Tester",
            PlayerName = "Player",
            Points = 500,
            TBLP = 45,
            Loc = 14
        };

        draft.Abilities.Add(new AbilityDraft
        {
            Name = "+6/2 stamina",
            AbilityType = AbilityType.Life,
            Source = "Guild",
            Amount = new List<int> { 6, 2 }
        });
        draft.Abilities.Add(new AbilityDraft
        {
            Name = "+9/3 stamina",
            AbilityType = AbilityType.Life,
            Source = "Guild",
            Amount = new List<int> { 9, 3 }
        });
        draft.Abilities.Add(new AbilityDraft
        {
            Name = "+3/1 stamina",
            AbilityType = AbilityType.Life,
            Source = "Tables",
            Amount = new List<int> { 3, 1 }
        });
        draft.Abilities.Add(new AbilityDraft
        {
            Name = "+4/1 stamina",
            AbilityType = AbilityType.Life,
            Source = "Tables",
            Amount = new List<int> { 4, 1 }
        });

        var assignedItem = BuildAssignedItem(
            draft,
            new CalcResult
            {
                AbilityType = "Life",
                AbilityName = "9/3",
                Details = new Dictionary<string, object?> { ["life"] = "9/3" }
            },
            new CalcResult
            {
                AbilityType = "Life",
                AbilityName = "24/8",
                Details = new Dictionary<string, object?> { ["life"] = "24/8" }
            });

        var totals = BattleboardLifeCalculator.Calculate(draft, new[] { assignedItem });

        Assert.Equal(44, totals.TotalTblp);
        Assert.Equal(22, totals.TotalLoc);
        Assert.Equal(69, totals.UncappedTblp);
        Assert.Equal(44, totals.TblpCap);
        Assert.Equal(29, totals.BaseTblp);
        Assert.Equal(9, totals.BaseLoc);
    }

    [Fact]
    public void Calculate_ExtraLifeLevelFromTablesAdvancesCapStage()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "calc-life-2",
            Name = "Cap Stage Tester",
            PlayerName = "Player",
            Points = 500,
            TBLP = 29,
            Loc = 9
        };

        var assignedItem = BuildAssignedItem(
            draft,
            new CalcResult
            {
                AbilityType = "Life",
                AbilityName = "24/8",
                Details = new Dictionary<string, object?> { ["life"] = "24/8" }
            },
            new CalcResult
            {
                AbilityType = "General",
                AbilityName = "1st Extra Level of Life",
                Details = new Dictionary<string, object?>
                {
                    ["selectedGeneralAbilities"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["name"] = "1st Extra Level of Life",
                            ["table"] = 9,
                            ["cost"] = 50
                        }
                    }
                }
            });

        var totals = BattleboardLifeCalculator.Calculate(draft, new[] { assignedItem });

        Assert.Equal(49, totals.TotalTblp);
        Assert.Equal(17, totals.TotalLoc);
        Assert.Equal(53, totals.UncappedTblp);
        Assert.Equal(49, totals.TblpCap);
        Assert.Equal(1, totals.ExtraLifeLevels);
    }

    private static Item BuildAssignedItem(CharacterDraft draft, params CalcResult[] abilities)
    {
        var item = new Item
        {
            Id = Guid.NewGuid().ToString("N"),
            AssignedCharacterId = draft.CharacterRecordId,
            AssignedCharacterName = draft.Name,
            AssignedCharacterPlayerName = draft.PlayerName,
            CreatedDate = DateTime.UtcNow
        };

        var payload = ItemEmailService.BuildItemPayload(item, abilities);
        item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);
        return item;
    }
}
