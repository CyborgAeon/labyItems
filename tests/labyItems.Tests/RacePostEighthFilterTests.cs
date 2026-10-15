using labyItems.Models.Characters;
using labyItems.Pages.RaceCard;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class RacePostEighthFilterTests
{
    [Fact]
    public void Filter_ReturnsOnlyMatchingRaceAbilities()
    {
        var halflingRule = new Models.Rules.RuleClause
        {
            Field = "Race",
            Operator = Models.Rules.RuleComparisonOp.In,
            Value = ["Halfling"]
        };
        var abilities = new[]
        {
            Ability("max-damage-quin", "Max Damage Quin", halflingRule),
            Ability("human-only", "Human Only", new Models.Rules.RuleClause
            {
                Field = "Race",
                Operator = Models.Rules.RuleComparisonOp.In,
                Value = ["Human"]
            }),
            Ability("generic", "Generic")
        };
        var races = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["Halfling"] = new PeopleRecord()
        };

        var result = RacePostEighthFilter.Filter(
            abilities,
            new CharacterDraft { Race = "Halfling" },
            new Dictionary<string, CharacterClassRecord>(),
            races,
            new AbilityAvailabilityService());

        Assert.Single(result);
        var row = Assert.Single(result, ability => ability.Index == "max-damage-quin");
        Assert.Equal("max-damage-quin", row.Index);
        Assert.Equal("Max Damage Quin", row.DisplayName);
        Assert.Equal(75, row.Cost);
        Assert.Equal(8, row.Table);
        Assert.DoesNotContain(result, ability => ability.Index == "generic");
        Assert.DoesNotContain(result, ability => ability.Index == "human-only");
    }

    [Fact]
    public void Filter_UsesSelectedRaceSubtype()
    {
        var abilities = new[]
        {
            Ability("double-drowe-abilities", "Double Drowe abilities",
                new Models.Rules.RuleClause { Field = "Race", Operator = Models.Rules.RuleComparisonOp.In, Value = ["Elf"] },
                new Models.Rules.RuleClause { Field = "Specialisation", SpecialisationKey = "Race Subtype", Operator = Models.Rules.RuleComparisonOp.In, Value = ["Drowe"] }),
            Ability("elf-base", "Elf base", new Models.Rules.RuleClause { Field = "Race", Operator = Models.Rules.RuleComparisonOp.In, Value = ["Elf"] })
        };
        var races = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase) { ["Elf"] = new PeopleRecord() };

        var drowe = RacePostEighthFilter.Filter(abilities, new CharacterDraft { Race = "Elf", RaceSubtypeValue = "Drowe" }, new Dictionary<string, CharacterClassRecord>(), races, new AbilityAvailabilityService());
        var fire = RacePostEighthFilter.Filter(abilities, new CharacterDraft { Race = "Elf", RaceSubtypeValue = "Fire" }, new Dictionary<string, CharacterClassRecord>(), races, new AbilityAvailabilityService());

        Assert.Contains(drowe, ability => ability.Index == "double-drowe-abilities");
        Assert.DoesNotContain(fire, ability => ability.Index == "double-drowe-abilities");
        Assert.Contains(drowe, ability => ability.Index == "elf-base");
    }

    [Fact]
    public void Filter_TribalHumanIncludesRaceSpecificAbilityDespiteAdditionalClassRule()
    {
        var ability = Ability("warhaft-of-the-host", "Warhaft of the Host",
            new Models.Rules.RuleClause { Field = "PeopleType", Operator = Models.Rules.RuleComparisonOp.In, Value = ["Tribal"] },
            new Models.Rules.RuleClause { Field = "Bracket", Operator = Models.Rules.RuleComparisonOp.In, Value = ["🛡️ Warrior"] });
        var races = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["Human"] = new PeopleRecord { PeopleType = ["Standard"] }
        };

        var result = RacePostEighthFilter.Filter([ability], new CharacterDraft { Race = "Human", RaceSubtypeValue = "Barbarian" }, new Dictionary<string, CharacterClassRecord>(), races, new AbilityAvailabilityService());

        Assert.Contains(result, item => item.Index == "warhaft-of-the-host");
    }

    [Fact]
    public void Filter_SortsByTableThenCostThenName()
    {
        var raceRule = new Models.Rules.RuleClause { Field = "Race", Operator = Models.Rules.RuleComparisonOp.In, Value = ["Elf"] };
        var abilities = new[]
        {
            Ability("later", "A later table", raceRule) with { Table = 2, Cost = 1 },
            Ability("zulu", "Zulu", raceRule) with { Table = 1, Cost = 2 },
            Ability("alpha", "Alpha", raceRule) with { Table = 1, Cost = 2 },
            Ability("cheap", "Cheap", raceRule) with { Table = 1, Cost = 1 }
        };

        var result = RacePostEighthFilter.Filter(abilities, new CharacterDraft { Race = "Elf" },
            new Dictionary<string, CharacterClassRecord>(),
            new Dictionary<string, PeopleRecord> { ["Elf"] = new() },
            new AbilityAvailabilityService());

        Assert.Equal(new[] { "cheap", "alpha", "zulu", "later" }, result.Select(item => item.Index));
    }

    private static EvolutionService.AbilityResult Ability(
        string index,
        string displayName,
        params Models.Rules.RuleClause[] rules) => new()
    {
        Index = index,
        DisplayName = displayName,
        Cost = index == "max-damage-quin" ? 75 : 1,
        Table = index == "max-damage-quin" ? 8 : 1,
        AvailabilityRules = rules
    };
}
