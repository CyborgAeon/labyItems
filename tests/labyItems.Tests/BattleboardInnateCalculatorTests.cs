using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardInnateCalculatorTests : ServiceTestBase
{
    [Fact]
    public void Calculate_ParsesLegacyMonsterPointInnateLines()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "innate-calc-1",
            Name = "Legacy Tester",
            PlayerName = "Tester"
        };

        var assignedItem = BuildAssignedItem(
            draft,
            new CalcResult
            {
                AbilityType = "MonsterPoint",
                AbilityName = "Spell: Fireblade (lvl 6 handbook) x6",
                Summary = "Spell: Fireblade (lvl 6 handbook) x6 = 72",
                Details = new Dictionary<string, object?>
                {
                    ["source"] = "monster-point"
                }
            });

        var innates = BattleboardInnateCalculator.Calculate(draft, new[] { assignedItem });
        var fireblade = Assert.Single(innates);

        Assert.Equal("Fireblade", fireblade.Name);
        Assert.Equal(6, fireblade.Rank);
    }

    [Fact]
    public void Calculate_MergesDraftAndStructuredItemInnates()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "innate-calc-2",
            Name = "Structured Tester",
            PlayerName = "Tester"
        };

        draft.Innates.Add(new InnateAbilityDraft
        {
            Name = "Fireblade",
            Rank = 2
        });

        var assignedItem = BuildAssignedItem(
            draft,
            new CalcResult
            {
                AbilityType = "Spell",
                AbilityName = "Fireblade",
                Summary = "Spell: Fireblade x3 = 36",
                Details = new Dictionary<string, object?>
                {
                    ["source"] = "monster-point",
                    ["spells"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["spellName"] = "Fireblade",
                            ["basicPerDay"] = 3,
                            ["advancedPerDay"] = 0
                        }
                    }
                }
            });

        var innates = BattleboardInnateCalculator.Calculate(draft, new[] { assignedItem });
        var fireblade = Assert.Single(innates);

        Assert.Equal("Fireblade", fireblade.Name);
        Assert.Equal(5, fireblade.Rank);
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
