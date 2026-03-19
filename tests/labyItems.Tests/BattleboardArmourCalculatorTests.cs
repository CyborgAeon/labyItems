using System.Collections.Generic;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardArmourCalculatorTests : ServiceTestBase
{
    [Fact]
    public void Calculate_UsesAssignedWearableArmourItem_WhenTierAllows()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "armour-calc-1",
            Name = "Wizzy",
            PlayerName = "Tester",
            ArmourAvailability = "Heavy",
            WornArmour = 6
        };

        var assignedItem = BuildAssignedItem(
            draft,
            baseAc: 8,
            pacEnhancement: 6);

        var totals = BattleboardArmourCalculator.Calculate(draft, new[] { assignedItem });

        Assert.Equal(14, totals.WornPac);
        Assert.Equal(0, totals.ItemDac);
        Assert.Equal(0, totals.ItemMac);
        Assert.Equal(0, totals.ItemSac);
    }

    [Fact]
    public void Calculate_DoesNotUseHeavyArmourItem_WhenTierIsOnlyMedium()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "armour-calc-2",
            Name = "Wizzy",
            PlayerName = "Tester",
            ArmourAvailability = "Medium",
            WornArmour = 6
        };

        var assignedItem = BuildAssignedItem(
            draft,
            baseAc: 8,
            pacEnhancement: 6);

        var totals = BattleboardArmourCalculator.Calculate(draft, new[] { assignedItem });

        Assert.Equal(6, totals.WornPac);
        Assert.Equal(0, totals.ItemDac);
        Assert.Equal(0, totals.ItemMac);
        Assert.Equal(0, totals.ItemSac);
    }

    [Fact]
    public void Calculate_IncludesNonArmourItemBonuses()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "armour-calc-3",
            Name = "Wizzy",
            PlayerName = "Tester",
            ArmourAvailability = "Heavy",
            WornArmour = 6
        };

        var assignedItem = BuildAssignedItemFromAbilities(
            draft,
            new CalcResult
            {
                AbilityType = "MonsterPoint",
                AbilityName = "+2 MAC",
                Summary = "+2 MAC = 16",
                Details = new Dictionary<string, object?>
                {
                    ["mac"] = 2
                }
            },
            new CalcResult
            {
                AbilityType = "MonsterPoint",
                AbilityName = "+1 SAC",
                Summary = "+1 SAC = 6",
                Details = new Dictionary<string, object?>
                {
                    ["sac"] = 1
                }
            });

        var totals = BattleboardArmourCalculator.Calculate(draft, new[] { assignedItem });

        Assert.Equal(6, totals.WornPac);
        Assert.Equal(0, totals.ItemDac);
        Assert.Equal(2, totals.ItemMac);
        Assert.Equal(1, totals.ItemSac);
    }

    [Fact]
    public void Calculate_IncludesEnhancementBonuses_FromNonWearableArmourAbility()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "armour-calc-4",
            Name = "Wizzy",
            PlayerName = "Tester",
            ArmourAvailability = "Heavy",
            WornArmour = 6
        };

        var assignedItem = BuildAssignedItemFromAbilities(
            draft,
            new CalcResult
            {
                AbilityType = "Armour",
                AbilityName = "Magical MC Armour",
                Details = new Dictionary<string, object?>
                {
                    ["enhancementBonuses"] = new[]
                    {
                        new Dictionary<string, object> { ["type"] = "DAC", ["value"] = 3 },
                        new Dictionary<string, object> { ["type"] = "MAC", ["value"] = 2 },
                        new Dictionary<string, object> { ["type"] = "SAC", ["value"] = 1 }
                    }
                }
            });

        var totals = BattleboardArmourCalculator.Calculate(draft, new[] { assignedItem });

        Assert.Equal(6, totals.WornPac);
        Assert.Equal(3, totals.ItemDac);
        Assert.Equal(2, totals.ItemMac);
        Assert.Equal(1, totals.ItemSac);
    }

    private static Item BuildAssignedItem(
        CharacterDraft draft,
        int baseAc,
        int pacEnhancement = 0,
        int dacEnhancement = 0,
        int macEnhancement = 0,
        int sacEnhancement = 0)
    {
        var item = new Item
        {
            Id = Guid.NewGuid().ToString("N"),
            AssignedCharacterId = draft.CharacterRecordId,
            AssignedCharacterName = draft.Name,
            AssignedCharacterPlayerName = draft.PlayerName,
            CreatedDate = DateTime.UtcNow
        };

        var enhancements = new List<Dictionary<string, object>>();
        AddEnhancement(enhancements, "PAC", pacEnhancement);
        AddEnhancement(enhancements, "DAC", dacEnhancement);
        AddEnhancement(enhancements, "MAC", macEnhancement);
        AddEnhancement(enhancements, "SAC", sacEnhancement);

        var payload = ItemEmailService.BuildItemPayload(item, new[]
        {
            new CalcResult
            {
                AbilityType = "Armour",
                AbilityName = "Plate mail",
                Details = new Dictionary<string, object?>
                {
                    ["AC"] = baseAc,
                    ["enhancementBonuses"] = enhancements
                }
            }
        });

        item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);
        return item;
    }

    private static Item BuildAssignedItemFromAbilities(
        CharacterDraft draft,
        params CalcResult[] abilities)
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

    private static void AddEnhancement(List<Dictionary<string, object>> target, string type, int value)
    {
        if (value <= 0)
            return;

        target.Add(new Dictionary<string, object>
        {
            ["type"] = type,
            ["value"] = value
        });
    }
}
