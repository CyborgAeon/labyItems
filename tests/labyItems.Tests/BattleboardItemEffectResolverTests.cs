using System.Text;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services;
using Microsoft.Maui.Storage;
using Xunit;

namespace labyItems.Tests;

public sealed class BattleboardItemEffectResolverTests : ServiceTestBase
{
    [Fact]
    public void ResolveFallback_ParsesSelectedGeneralResistanceAndImmunity()
    {
        var draft = new CharacterDraft
        {
            CharacterRecordId = "item-effects-fallback",
            Name = "Fallback Tester",
            PlayerName = "Tester",
            Class = "Wizard",
            Race = "Human"
        };

        var assignedItem = BuildAssignedItem(
            draft,
            new CalcResult
            {
                AbilityType = "General",
                AbilityName = "General abilities (2)",
                Summary = "General abilities",
                Details = new Dictionary<string, object?>
                {
                    ["selectedGeneralAbilities"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["name"] = "11th Level Resistance to Magic and Spirits"
                        },
                        new Dictionary<string, object?>
                        {
                            ["name"] = "Immunity to Repels"
                        }
                    }
                }
            });

        var resolved = BattleboardItemEffectResolver.ResolveFallback(draft, new[] { assignedItem });

        Assert.Equal(11, resolved.ResistanceOverrides["Magic"]);
        Assert.Equal(11, resolved.ResistanceOverrides["Spirit"]);
        Assert.Contains("Immunity to Repels", resolved.Immunities);
    }

    [Fact]
    public async Task ResolveAsync_RespectsClassPreReq_WhenDefinitionExists()
    {
        FileSystem.SetPackageOverride(
            "specialisation/abilities.json",
            () => new MemoryStream(Encoding.UTF8.GetBytes("""
                {
                  "abilities": {
                    "ability.test.blocked-resistance": {
                      "Name": "Blocked Resistance",
                      "Key": "ability.test.blocked-resistance",
                      "PreReqs": [ "Class:Warrior" ],
                      "SystemEffects": [
                        {
                          "EffectType": "lor:magic",
                          "DisplayName": "11th level resistance to Magic",
                          "ResistanceType": "Magic",
                          "Level": 11
                        }
                      ],
                      "Effect": "11th Level Resistance to Magic"
                    }
                  }
                }
                """)));
        AbilityDefinitionLookupService.InvalidateCache();

        var draft = new CharacterDraft
        {
            CharacterRecordId = "item-effects-prereq-miss",
            Name = "PreReq Miss",
            PlayerName = "Tester",
            Class = "Wizard",
            Race = "Human"
        };

        var assignedItem = BuildAssignedItem(
            draft,
            new CalcResult
            {
                AbilityType = "General",
                AbilityName = "General",
                Summary = "General",
                Details = new Dictionary<string, object?>
                {
                    ["selectedGeneralAbilities"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["abilityRef"] = "ability.test.blocked-resistance",
                            ["name"] = "Blocked Resistance"
                        }
                    }
                }
            });

        var resolved = await BattleboardItemEffectResolver.ResolveAsync(draft, new[] { assignedItem });

        Assert.False(resolved.ResistanceOverrides.ContainsKey("Magic"));
    }

    [Fact]
    public async Task ResolveAsync_AppliesItemEffects_WhenPreReqSatisfied()
    {
        FileSystem.SetPackageOverride(
            "specialisation/abilities.json",
            () => new MemoryStream(Encoding.UTF8.GetBytes("""
                {
                  "abilities": {
                    "ability.test.allowed-resistance": {
                      "Name": "Allowed Resistance",
                      "Key": "ability.test.allowed-resistance",
                      "PreReqs": [ "Class:Warrior" ],
                      "SystemEffects": [
                        {
                          "EffectType": "lor:magic",
                          "DisplayName": "11th level resistance to Magic",
                          "ResistanceType": "Magic",
                          "Level": 11
                        }
                      ],
                      "Effect": "11th Level Resistance to Magic"
                    }
                  }
                }
                """)));
        AbilityDefinitionLookupService.InvalidateCache();

        var draft = new CharacterDraft
        {
            CharacterRecordId = "item-effects-prereq-hit",
            Name = "PreReq Hit",
            PlayerName = "Tester",
            Class = "Warrior",
            Race = "Human"
        };

        var assignedItem = BuildAssignedItem(
            draft,
            new CalcResult
            {
                AbilityType = "General",
                AbilityName = "General",
                Summary = "General",
                Details = new Dictionary<string, object?>
                {
                    ["selectedGeneralAbilities"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["abilityRef"] = "ability.test.allowed-resistance",
                            ["name"] = "Allowed Resistance"
                        }
                    }
                }
            });

        var resolved = await BattleboardItemEffectResolver.ResolveAsync(draft, new[] { assignedItem });

        Assert.Equal(11, resolved.ResistanceOverrides["Magic"]);
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
