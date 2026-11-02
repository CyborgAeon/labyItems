using labyItems.Controls;
using labyItems.Models.Items;
using System.Text.Json;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class ItemEmailServiceTests
{
    [Fact]
    public void ItemPayload_RoundTripPreservesMonsterPointEditorSettings()
    {
        var payload = new ItemJsonPayload
        {
            Item = new ItemJsonDetail
            {
                MpEditorState = new MpEditorState
                {
                    CalculatorType = "MpThemedayPage",
                    LifeIndex = 3,
                    SelectedWeaponKind = "Magic",
                    SelectedWeaponType = Enum.GetValues<labyItems.Models.Enums.WeaponType>().Last(),
                    SelectedWeaponColour = Enum.GetValues<labyItems.Models.Enums.MagicColours>().Last(),
                    RepelGoodEvilTarget = "Good",
                    RepelGoodEvilCount = 4,
                    MagicPowerStorePoints = 3,
                    SpellSelections = new() { new() { Option = new SpellOption("Fireblade", 6, false), Uses = 2 } }
                }
            }
        };

        var json = ItemEmailService.SerializeItemPayload(payload);
        var restored = ItemEmailService.TryDeserializeItemPayload(json)?.Item.MpEditorState;

        Assert.NotNull(restored);
        Assert.Equal("MpThemedayPage", restored.CalculatorType);
        Assert.Equal(3, restored.LifeIndex);
        Assert.Equal(payload.Item.MpEditorState.SelectedWeaponType, restored.SelectedWeaponType);
        Assert.Equal(payload.Item.MpEditorState.SelectedWeaponColour, restored.SelectedWeaponColour);
        Assert.Equal(4, restored.RepelGoodEvilCount);
        Assert.Equal(3, restored.MagicPowerStorePoints);
        Assert.Equal(new SpellOption("Fireblade", 6, false), Assert.Single(restored.SpellSelections).Option);
        Assert.Equal(2, restored.SpellSelections[0].Uses);
    }

    [Fact]
    public void BuildItemPayload_DerivesCanonicalTypeArrayFromAbilities()
    {
        var item = new Item
        {
            WitnessName = "Witness",
            Description = "Item: Spellbound Key\nPhys rep: Brass key",
            Isp = 42,
            CreatedDate = DateTime.UtcNow
        };

        var payload = ItemEmailService.BuildItemPayload(item, new[]
        {
            new CalcResult { AbilityType = "Spell", AbilityName = "Fireblade", TotalIsp = 12 },
            new CalcResult { AbilityType = "Miracle", AbilityName = "Heal", TotalIsp = 8 },
            new CalcResult { AbilityType = "Evocation", AbilityName = "Barkskin", TotalIsp = 6 }
        });

        Assert.Equal(new[] { "magical", "spiritual", "earthpower" }, payload.Item.Types);
    }

    [Fact]
    public void BuildItemPayload_BuildsHumanReadableDescriptionFromIspAbilities()
    {
        var item = new Item
        {
            Description = "Item: Spellbound Key\nPhys rep: Brass key",
            Isp = 42,
            CreatedDate = DateTime.UtcNow
        };

        var payload = ItemEmailService.BuildItemPayload(item, new[]
        {
            new CalcResult { AbilityType = "Spell", AbilityName = "Fireblade", Summary = "Spell: Fireblade (lvl 4) x3 = 24", TotalIsp = 24 },
            new CalcResult { AbilityType = "Miracle", AbilityName = "Heal", Summary = "Miracle: Heal (4) x5 = 40", TotalIsp = 40 }
        });
        var draft = ItemEmailService.BuildItemEmailDraft(item, payload, to: "desk@example.test");

        Assert.Equal("Spellbound Key - Grants Fireblade 3/day and Heal 5/day", payload.Description);
        Assert.Equal("Spellbound Key", payload.Item.DisplayName);
        Assert.Equal("Brass key", payload.Item.PhysicalRepresentation);
        Assert.Contains("Spellbound Key - Grants Fireblade 3/day and Heal 5/day", draft.Body);
    }

    [Fact]
    public void BuildItemPayload_UsesStructuredIspDetailsForHumanReadableAbilities()
    {
        var item = new Item
        {
            Description = "Item: Stormwand",
            Isp = 42,
            CreatedDate = DateTime.UtcNow
        };

        var payload = ItemEmailService.BuildItemPayload(item, new[]
        {
            new CalcResult
            {
                AbilityType = "Spell",
                AbilityName = "Spell list (2)",
                Summary = "Spell list (42)\n| raw calculator breakdown",
                TotalIsp = 42,
                Details = new()
                {
                    ["spells"] = new[]
                    {
                        new { spellName = "Fireblade", basicPerDay = 3, advancedPerDay = 0 },
                        new { spellName = "Magebolt", basicPerDay = 1, advancedPerDay = 1 }
                    }
                }
            }
        });

        Assert.Equal("Stormwand - Grants Fireblade 3/day and Magebolt 2/day", payload.Description);
    }

    [Fact]
    public void BuildGrantSummary_DoesNotPluralizeArmourAndShieldAcronyms()
    {
        var summary = NotationHelper.BuildGrantSummary(new[]
        {
            new CalcResult
            {
                AbilityType = "Shield",
                AbilityName = "No Shield",
                TotalIsp = 48,
                Details = new()
                {
                    ["MAC"] = 5,
                    ["enhancementBonuses"] = new[]
                    {
                        new { type = "PAC", value = 2, isp = 12 }
                    }
                }
            }
        });

        Assert.Contains("5 MAC", summary);
        Assert.Contains("2 PAC", summary);
        Assert.DoesNotContain("MACs", summary);
        Assert.DoesNotContain("PACs", summary);
    }

    [Fact]
    public void BuildMpSubmissionEmailDraft_IncludesItemNameAndDerivedTypesInCompressedJson()
    {
        var recipient = new RecipientInfo
        {
            ItemName = "manifold key",
            PlayerName = "Player",
            CharacterName = "Character",
            CharacterClass = "Wizard"
        };
        var payload = new MpSubmissionPayload
        {
            TotalIsp = 20,
            TotalMp = 120,
            IspBreakdown = new List<ContributionRow>
            {
                new() { Id = "isp-spell-0", Text = "Spell: Fireblade (Lvl 6) x1 = 12", RunningTotal = 12 },
                new() { Id = "isp-miracle-0", Text = "Miracle: Heal (3) x1 = 8", RunningTotal = 20 }
            }
        };

        var draft = ItemEmailService.BuildMpSubmissionEmailDraft(recipient, payload, to: "desk@example.test");
        using var document = JsonDocument.Parse(JsonTokenCompressor.DecompressFromBase64(draft.Token));
        var root = document.RootElement;

        Assert.Equal("manifold key", root.GetProperty("itemName").GetString());
        Assert.Equal(
            new[] { "magical", "spiritual" },
            root.GetProperty("itemType").EnumerateArray().Select(type => type.GetString()).ToArray());
        Assert.Equal(
            new[] { "magical", "spiritual" },
            root.GetProperty("item").GetProperty("types").EnumerateArray().Select(type => type.GetString()).ToArray());
        Assert.Equal(
            "manifold key - Grants Fireblade 1/day and Heal 1/day",
            root.GetProperty("description").GetString());
        Assert.Equal(
            new[] { "Fireblade 1/day", "Heal 1/day" },
            root.GetProperty("abilities").EnumerateArray().Select(ability => ability.GetString()).ToArray());
        Assert.Contains("Item name: manifold key", draft.Body);
        Assert.Contains("Item type: Magic, Spiritual", draft.Body);
        Assert.Contains("Description: manifold key - Grants Fireblade 1/day and Heal 1/day", draft.Body);
    }

    [Fact]
    public void BuildMpSubmissionEmailDraft_FallsBackToPhysicalWhenNoCastingRowsExist()
    {
        var recipient = new RecipientInfo
        {
            ItemName = "Plainbelt",
            PlayerName = "Player"
        };
        var payload = new MpSubmissionPayload
        {
            TotalIsp = 4,
            TotalMp = 40,
            IspBreakdown = new List<ContributionRow>
            {
                new() { Id = "isp-pac", Text = "+1 PAC = 4", RunningTotal = 4 }
            }
        };

        var draft = ItemEmailService.BuildMpSubmissionEmailDraft(recipient, payload, to: "desk@example.test");
        using var document = JsonDocument.Parse(JsonTokenCompressor.DecompressFromBase64(draft.Token));
        var itemTypes = document.RootElement.GetProperty("itemType")
            .EnumerateArray()
            .Select(type => type.GetString())
            .ToArray();

        Assert.Equal(new[] { "physical" }, itemTypes);
    }

    [Fact]
    public void BuildDeskSubmissionEmailDraft_UsesSameDeskShapeForIspPayloads()
    {
        var recipient = new RecipientInfo
        {
            ItemName = "Runed Belt",
            PlayerName = "Player",
            CharacterName = "Character",
            CharacterClass = "Warrior"
        };
        var payload = new MpSubmissionPayload
        {
            SourceFlow = "isp",
            PhysicalRepresentation = "Belt",
            TotalIsp = 10,
            Abilities = new List<CalcResult>
            {
                new()
                {
                    AbilityType = "General",
                    AbilityName = "Accuracy",
                    TotalIsp = 10,
                    Details = new()
                    {
                        ["selectedGeneralAbilities"] = new[]
                        {
                            new { name = "Accuracy", table = 7, cost = 50, isp = 10 }
                        }
                    }
                }
            },
            IspBreakdown = new List<ContributionRow>
            {
                new() { Id = "general", Text = "General (10)", RunningTotal = 10 }
            }
        };

        var draft = ItemEmailService.BuildDeskSubmissionEmailDraft(recipient, payload, to: "desk@example.test");
        using var document = JsonDocument.Parse(JsonTokenCompressor.DecompressFromBase64(draft.Token));
        var root = document.RootElement;

        Assert.Equal("ISP item for Player", draft.Subject);
        Assert.Equal("isp", root.GetProperty("item").GetProperty("sourceFlow").GetString());
        Assert.Equal("Belt", root.GetProperty("item").GetProperty("physicalRepresentation").GetString());
        Assert.Equal("Runed Belt - Grants Accuracy", root.GetProperty("description").GetString());
        Assert.Equal(new[] { "Accuracy" }, root.GetProperty("abilities").EnumerateArray().Select(ability => ability.GetString()).ToArray());
        Assert.Contains("Physical representation: Belt", draft.Body);
        Assert.DoesNotContain("MP cost:", draft.Body);
    }
}
