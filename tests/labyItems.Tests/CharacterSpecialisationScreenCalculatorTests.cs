using System.Collections.ObjectModel;
using labyItems.Models.Characters;
using labyItems.Services;
using labyItems.Services.Specialisations;
using Xunit;

namespace labyItems.Tests;

public sealed class CharacterSpecialisationScreenCalculatorTests : ServiceTestBase
{
    [Fact]
    public void ResolveRequiredChoices_MapsClassAndRaceAbilitiesToSpecialisationKeys()
    {
        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warlock",
            RaceSubtypeValue = "Baronial"
        };

        var classRecord = new CharacterClassRecord
        {
            Powerbase = ["Magic"],
            Levels = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                ["1"] = [new AbilityDefinition { Name = "Wizard Colour" }],
                ["6"] = [new AbilityDefinition { Name = "Faerie Colour" }]
            }
        };

        var raceRecord = new PeopleRecord
        {
            Subtype = new PeopleSubtypeRecord
            {
                Key = "HumanSubtype",
                DisplayName = "Human subtype",
                SelectionMode = "SingleRequired",
                OptionsSource = "Standard,Baronial",
                AbilityMapKey = "HumanSubtypeAbilities"
            }
        };

        var definitions = BuildDefinitions();

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Warlock"] = classRecord
            },
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Human"] = raceRecord
            },
            definitions,
            BuildInjectionRules());

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        Assert.Equal(2, required.Count);

        var sections = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        Assert.Contains(sections, s => s.Kind == SpecialisationSectionKind.RaceSubtype);
        Assert.Contains(sections, s => s.Title == "Baronial Tradition");
        Assert.Contains(sections, s => s.Title == "Baronial Ancestry");
    }

    [Fact]
    public void BuildScreenSections_AppliesOptionFilterAndRequiredRuleFromInjectionMetadata()
    {
        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warlock",
            RaceSubtypeValue = "Baronial"
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Warlock"] = new CharacterClassRecord
                {
                    Powerbase = ["Magic"]
                }
            },
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Human"] = new PeopleRecord()
            },
            BuildDefinitions(),
            BuildInjectionRules());

        var sections = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, Array.Empty<RequiredChoice>());
        var tradition = Assert.Single(sections.Where(section => section.Title == "Baronial Tradition"));

        Assert.Equal(SpecialisationSectionKind.Choice, tradition.Kind);
        Assert.True(tradition.Required);
        Assert.Equal(2, tradition.Options.Count);
        Assert.All(tradition.Options, option =>
            Assert.Contains(option.Key, new[] { "Hedge Wizardry", "Circle Membership" }, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildScreenSections_PreservesSubtypeOptionsEvenWhenMappedDefinitionOmitsEntries()
    {
        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warlock"
        };

        var definitions = new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["HumanSubtypeAbilities"] = new SpecialisationDefinition
            {
                Key = "HumanSubtypeAbilities",
                ChoiceSets =
                [
                    new SpecialisationChoiceSet
                    {
                        Id = "HumanSubtypeAbilities:mapped",
                        DefinitionKey = "HumanSubtypeAbilities",
                        Title = "HumanSubtypeAbilities",
                        Mode = ChoiceMode.MappedSingle,
                        Required = true,
                        Options =
                        [
                            new ChoiceOption
                            {
                                Key = "Baronial",
                                Label = "Baronial"
                            }
                        ]
                    }
                ]
            }
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Warlock"] = new CharacterClassRecord()
            },
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Human"] = new PeopleRecord
                {
                    Subtype = new PeopleSubtypeRecord
                    {
                        Key = "HumanSubtype",
                        DisplayName = "Human subtype",
                        SelectionMode = "SingleRequired",
                        OptionsSource = "Standard,Baronial",
                        AbilityMapKey = "HumanSubtypeAbilities"
                    }
                }
            },
            definitions);

        var sections = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, Array.Empty<RequiredChoice>());
        var subtypeSection = Assert.Single(sections.Where(section => section.Kind == SpecialisationSectionKind.RaceSubtype));
        var options = subtypeSection.Options.Select(option => option.Key).ToList();

        Assert.Contains("Standard", options, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Baronial", options, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplySavedSelections_AssignsDelimitedColourSelectionsByLevel()
    {
        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warlock"
        };
        draft.SpecialisationSelections["Wizard Colour"] = "Red | Blue";

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Warlock"] = new CharacterClassRecord
                {
                    Levels = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["1"] = [new AbilityDefinition { Name = "Wizard Colour" }],
                        ["2"] = [new AbilityDefinition { Name = "Wizard Colour" }]
                    }
                }
            },
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Human"] = new PeopleRecord()
            },
            BuildDefinitions());

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var screen = CharacterSpecialisationScreenCalculator.ApplySavedSelections(context, specs);

        var section = Assert.Single(screen.Sections.Where(s => s.Spec.Title == "Wizard Colour"));
        Assert.Equal("Red", section.SelectedByLevel[1]);
        Assert.Equal("Blue", section.SelectedByLevel[2]);
    }

    [Fact]
    public void Recalculate_AppliesSubtypeOverridesAndValidation()
    {
        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warlock",
            Alignment = new Alignment(OrderAxis.Neutral, MoralAxis.Good)
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Warlock"] = new CharacterClassRecord
                {
                    Levels = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["1"] = [new AbilityDefinition { Name = "Faerie Colour" }]
                    }
                }
            },
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Human"] = new PeopleRecord
                {
                    Subtype = new PeopleSubtypeRecord
                    {
                        Key = "HumanSubtype",
                        DisplayName = "Human subtype",
                        SelectionMode = "SingleRequired",
                        OptionsSource = "Standard,Baronial",
                        AbilityMapKey = "HumanSubtypeAbilities"
                    }
                }
            },
            BuildDefinitions());

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var subtypeSpec = specs.First(s => s.Kind == SpecialisationSectionKind.RaceSubtype);

        var choiceSelections = new Dictionary<string, ChoiceSelectionState>(StringComparer.OrdinalIgnoreCase)
        {
            ["choice:Faerie Colour"] = new ChoiceSelectionState
            {
                SelectedByLevel = new ReadOnlyDictionary<int, string>(new Dictionary<int, string>
                {
                    [1] = "Red",
                    [2] = "Green"
                })
            }
        };

        var mappedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [subtypeSpec.SectionId] = "Baronial"
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState
            {
                RaceSubtype = "Baronial",
                ChoiceSelections = new ReadOnlyDictionary<string, ChoiceSelectionState>(choiceSelections),
                MappedSelections = new ReadOnlyDictionary<string, string>(mappedSelections)
            },
            raceSubtypeKey: "HumanSubtype",
            raceSubtypeMapKey: "HumanSubtypeAbilities");

        Assert.Equal("Baronial", screen.RaceSubtypeValue);
        Assert.Equal("BaronialLife", screen.LifeScaleOverride);
        Assert.Equal("None", screen.ArmourAvailabilityOverride);
        Assert.Contains("Red", screen.ColourChoiceOverride);

        var faerie = Assert.Single(screen.Sections.Where(s => s.Spec.Title == "Faerie Colour"));
        Assert.Contains("Faerie colours cannot be opposite pairs.", faerie.ValidationMessage);
        Assert.False(screen.IsComplete);
    }

    private static Dictionary<string, SpecialisationDefinition> BuildDefinitions()
    {
        return new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Wizard Colour"] = new SpecialisationDefinition
            {
                Key = "Wizard Colour",
                ChoiceSets =
                [
                    new SpecialisationChoiceSet
                    {
                        Id = "Wizard Colour:primary",
                        DefinitionKey = "Wizard Colour",
                        Title = "Wizard Colour",
                        Mode = ChoiceMode.Single,
                        Required = true,
                        StrategyIds = ["selection:multi-delimited", "options:magic-colour"],
                        Options =
                        [
                            BuildFlatOption("Red"),
                            BuildFlatOption("Blue"),
                            BuildFlatOption("Green")
                        ]
                    }
                ]
            },
            ["Faerie Colour"] = new SpecialisationDefinition
            {
                Key = "Faerie Colour",
                ChoiceSets =
                [
                    new SpecialisationChoiceSet
                    {
                        Id = "Faerie Colour:primary",
                        DefinitionKey = "Faerie Colour",
                        Title = "Faerie Colour",
                        Mode = ChoiceMode.Single,
                        Required = true,
                        StrategyIds = ["validation:faerie-opposites", "allow:duplicate-slots"],
                        Options =
                        [
                            BuildFlatOption("Red"),
                            BuildFlatOption("Green")
                        ]
                    }
                ]
            },
            ["HumanSubtypeAbilities"] = new SpecialisationDefinition
            {
                Key = "HumanSubtypeAbilities",
                ChoiceSets =
                [
                    new SpecialisationChoiceSet
                    {
                        Id = "HumanSubtypeAbilities:mapped",
                        DefinitionKey = "HumanSubtypeAbilities",
                        Title = "HumanSubtypeAbilities",
                        Mode = ChoiceMode.MappedSingle,
                        Required = true,
                        Options =
                        [
                            new ChoiceOption
                            {
                                Key = "Standard",
                                Label = "Standard"
                            },
                            new ChoiceOption
                            {
                                Key = "Baronial",
                                Label = "Baronial",
                                Effects = new OptionEffects
                                {
                                    LifeScaleOverride = "BaronialLife",
                                    ArmourAvailabilityOverride = "None",
                                    ColourChoiceOverride = ["Red"],
                                    HedgeOrCircle = ["Hedge Wizardry", "Circle Membership"]
                                },
                                Grants =
                                [
                                    new AbilityGrant
                                    {
                                        Level = 1,
                                        Ability = new AbilityDefinition
                                        {
                                            Name = "Baronial Trait",
                                            Type = "Static"
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            },
            ["Baronial Tradition"] = new SpecialisationDefinition
            {
                Key = "Baronial Tradition",
                ChoiceSets =
                [
                    new SpecialisationChoiceSet
                    {
                        Id = "Baronial Tradition:primary",
                        DefinitionKey = "Baronial Tradition",
                        Title = "Baronial Tradition",
                        Mode = ChoiceMode.Single,
                        Required = true,
                        Options =
                        [
                            BuildFlatOption("Hedge Wizardry"),
                            BuildFlatOption("Circle Membership")
                        ]
                    }
                ]
            },
            ["BaronialAncestry"] = new SpecialisationDefinition
            {
                Key = "BaronialAncestry",
                ChoiceSets =
                [
                    new SpecialisationChoiceSet
                    {
                        Id = "BaronialAncestry:mapped",
                        DefinitionKey = "BaronialAncestry",
                        Title = "BaronialAncestry",
                        Mode = ChoiceMode.MappedSingle,
                        Required = true,
                        Options =
                        [
                            BuildFlatOption("Rhagrimm")
                        ]
                    }
                ]
            }
        };
    }

    private static IReadOnlyList<SpecialisationInjectionRule> BuildInjectionRules()
    {
        return
        [
            new SpecialisationInjectionRule
            {
                Id = "human-baronial-ancestry",
                Conditions = new InjectionRuleConditions
                {
                    Race = "Human",
                    Subtype = "Baronial"
                },
                Section = new InjectionRuleSection
                {
                    SectionType = "Mapped",
                    DefinitionKey = "BaronialAncestry",
                    Title = "Baronial Ancestry",
                    SectionId = "mapped:Baronial Ancestry",
                    Required = true
                }
            },
            new SpecialisationInjectionRule
            {
                Id = "human-baronial-tradition",
                Conditions = new InjectionRuleConditions
                {
                    Race = "Human",
                    Subtype = "Baronial"
                },
                Section = new InjectionRuleSection
                {
                    SectionType = "Choice",
                    DefinitionKey = "Baronial Tradition",
                    Title = "Baronial Tradition",
                    Subtitle = "Select your Baronial magical tradition.",
                    SectionId = "choice:Baronial Tradition",
                    RequiredWhenClassHasPowerBase = true,
                    Levels = [1],
                    InsertIndex = 0,
                    OptionFilter = new InjectionOptionFilter
                    {
                        SourceDefinitionKey = "HumanSubtypeAbilities",
                        SourceOption = "Baronial",
                        EffectListField = "HedgeOrCircle"
                    }
                }
            }
        ];
    }

    private static ChoiceOption BuildFlatOption(string name)
    {
        return new ChoiceOption
        {
            Key = name,
            Label = name,
            Grants =
            [
                new AbilityGrant
                {
                    Ability = new AbilityDefinition
                    {
                        Name = name,
                        Type = "Static"
                    }
                }
            ]
        };
    }
}
