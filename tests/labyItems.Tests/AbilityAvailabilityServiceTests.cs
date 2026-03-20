using System;
using System.Collections.Generic;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class AbilityAvailabilityServiceTests
{
    private static readonly IReadOnlyDictionary<string, CharacterClassRecord> Classes =
        new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["Warrior"] = new() { Brackets = new List<string> { "🛡️ Warrior" } },
            ["Wizard"] = new() { Brackets = new List<string> { "🧙 Wizard" } },
            ["Scout"] = new() { Brackets = new List<string> { "⚔ Scout" } }
        };

    private static readonly IReadOnlyDictionary<string, PeopleRecord> Races =
        new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dwarf"] = new() { Tags = new List<string>() },
            ["Human"] = new() { Tags = new List<string>() }
        };

    [Fact]
    public void IsAvailable_EmptyRules_ReturnsTrue()
    {
        var service = new AbilityAvailabilityService();
        var draft = new CharacterDraft
        {
            Class = "Warrior",
            Race = "Dwarf"
        };

        var available = service.IsAvailable(Array.Empty<RuleClause>(), draft, Classes, Races);

        Assert.True(available);
    }

    [Fact]
    public void IsAvailable_BracketNotInWarrior_BlocksWarrior_AllowsWizard()
    {
        var service = new AbilityAvailabilityService();
        var rules = new[]
        {
            new RuleClause
            {
                Field = "Bracket",
                Operator = RuleComparisonOp.NotIn,
                Value = new List<string> { "🛡️ Warrior" }
            }
        };

        var warriorDraft = new CharacterDraft
        {
            Class = "Warrior",
            Race = "Dwarf"
        };
        var wizardDraft = new CharacterDraft
        {
            Class = "Wizard",
            Race = "Human"
        };

        Assert.False(service.IsAvailable(rules, warriorDraft, Classes, Races));
        Assert.True(service.IsAvailable(rules, wizardDraft, Classes, Races));
    }

    [Fact]
    public void IsAvailable_BracketOnly_RequiresPureBracketAcrossAllClasses()
    {
        var service = new AbilityAvailabilityService();
        var rules = new[]
        {
            new RuleClause
            {
                Field = "Bracket",
                Operator = RuleComparisonOp.Only,
                Value = new List<string> { "⚔ Scout" }
            }
        };

        var pureScoutDraft = new CharacterDraft
        {
            Class = "Scout",
            Race = "Human"
        };

        var mixedScoutWizardDraft = new CharacterDraft
        {
            Class = "Scout",
            Race = "Human",
            MultiClassLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Wizard"] = 1
            }
        };

        Assert.True(service.IsAvailable(rules, pureScoutDraft, Classes, Races));
        Assert.False(service.IsAvailable(rules, mixedScoutWizardDraft, Classes, Races));
    }

    [Fact]
    public void IsAvailable_SpecialisationKey_RestrictsRuleToMatchingSelectionGroup()
    {
        var service = new AbilityAvailabilityService();
        var rules = new[]
        {
            new RuleClause
            {
                Field = "Bracket",
                Operator = RuleComparisonOp.Only,
                Value = new List<string> { "🧙 Wizard" }
            },
            new RuleClause
            {
                Field = "Specialisation",
                Operator = RuleComparisonOp.In,
                Value = new List<string> { "Grey" },
                SpecialisationKey = "Wizard Colour"
            }
        };

        var validDraft = new CharacterDraft
        {
            Class = "Wizard",
            Race = "Human"
        };
        validDraft.SpecialisationSelections["Wizard::Wizard Colour"] = "Grey";
        validDraft.SpecialisationSelections["Wizard::School"] = "Earth";

        var wrongGroupDraft = new CharacterDraft
        {
            Class = "Wizard",
            Race = "Human"
        };
        wrongGroupDraft.SpecialisationSelections["Wizard::Wizard Colour"] = "Blue";
        wrongGroupDraft.SpecialisationSelections["Wizard::School"] = "Grey";

        Assert.True(service.IsAvailable(rules, validDraft, Classes, Races));
        Assert.False(service.IsAvailable(rules, wrongGroupDraft, Classes, Races));
    }
}
