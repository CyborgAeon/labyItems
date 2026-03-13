using System.Collections.ObjectModel;
using System.Text;
using labyItems.Models.Characters;
using labyItems.Services;
using labyItems.Services.Specialisations;
using Microsoft.Maui.Storage;
using Xunit;

namespace labyItems.Tests;

public sealed class CharacterSpecialisationScreenCalculatorTests : ServiceTestBase
{
    [Fact]
    public async Task BuildScreenSections_CrolSubtypeAppliesTalentInjectionRules()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var classes = await ClassService.GetAllAsync();
        var races = await PeopleService.GetAllAsync();
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = "Crol",
            Class = "Wizard",
            RaceSubtypeValue = "Jaerseen"
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            classes,
            races,
            index.Definitions,
            index.InjectionRules);

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var sections = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);

        var crolTalentSection = Assert.Single(sections.Where(section => section.Title == "Crol Talents"));
        Assert.Equal(new[] { 2, 5, 8 }, crolTalentSection.Levels);
        Assert.Contains(crolTalentSection.Options, option => option.Label.Equals("Elementalist", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(crolTalentSection.Options, option => option.Label.Equals("Toughened Feet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Recalculate_ElfSubtypeSelection_IsRetainedAndCompletesSubtypeSection()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var classes = await ClassService.GetAllAsync();
        var races = await PeopleService.GetAllAsync();
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = "Elf",
            Class = "Wizard"
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            classes,
            races,
            index.Definitions,
            index.InjectionRules);

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var subtypeSpec = Assert.Single(specs.Where(section => section.Kind == SpecialisationSectionKind.RaceSubtype));
        Assert.Contains(subtypeSpec.Options, option => option.Key.Equals("Fire", StringComparison.OrdinalIgnoreCase));

        var mappedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [subtypeSpec.SectionId] = "Fire"
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState
            {
                RaceSubtype = "Fire",
                MappedSelections = new ReadOnlyDictionary<string, string>(mappedSelections)
            },
            raceSubtypeKey: "ElfColour",
            raceSubtypeMapKey: "ElfColourAbilities");

        var subtypeSection = Assert.Single(screen.Sections.Where(section => section.Spec.Kind == SpecialisationSectionKind.RaceSubtype));
        Assert.Equal("Fire", screen.RaceSubtypeValue);
        Assert.Equal("Fire", subtypeSection.SelectedOption);
        Assert.True(subtypeSection.IsComplete);
        Assert.True(string.IsNullOrWhiteSpace(subtypeSection.ValidationMessage));
    }

    [Fact]
    public async Task BuildScreenSections_AncientFolkCloudWay_ReplacesWardPactOptions()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = "Ancient Folk",
            Class = "Warrior",
            RaceSubtypeValue = "The cloud way"
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            index.Definitions,
            index.InjectionRules);

        var required = new List<RequiredChoice>
        {
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 2 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 4 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 6 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 8 }
        };

        var sections = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var wardPactSection = Assert.Single(sections.Where(section => section.SectionId.Equals("choice:Ward pact", StringComparison.OrdinalIgnoreCase)));

        var optionLabels = wardPactSection.Options
            .Select(option => option.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        Assert.Equal(3, optionLabels.Count);
        Assert.Contains("Undead", optionLabels, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Wyvern", optionLabels, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Dwarf", optionLabels, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Salamanders", optionLabels, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("selection:multi-delimited", wardPactSection.StrategyIds, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplySavedSelections_WardPactSelectionsLoadFromDelimitedRows()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = "Ancient Folk",
            Class = "Warrior"
        };
        draft.SpecialisationSelections["Ward pact"] = "Undead | Salamanders | Shades | Sprites";

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            index.Definitions,
            index.InjectionRules);

        var required = new List<RequiredChoice>
        {
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 2 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 4 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 6 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 8 }
        };

        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var screen = CharacterSpecialisationScreenCalculator.ApplySavedSelections(context, specs);
        var wardPactSection = Assert.Single(screen.Sections.Where(section => section.Spec.SectionId.Equals("choice:Ward pact", StringComparison.OrdinalIgnoreCase)));

        Assert.Equal("Undead", wardPactSection.SelectedByLevel[2]);
        Assert.Equal("Salamanders", wardPactSection.SelectedByLevel[4]);
        Assert.Equal("Shades", wardPactSection.SelectedByLevel[6]);
        Assert.Equal("Sprites", wardPactSection.SelectedByLevel[8]);
    }

    [Fact]
    public async Task ApplySavedSelections_WardPactDuplicateGrantLevelsKeepSeparateSlots()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = "Ancient Folk",
            Class = "Warrior"
        };
        draft.SpecialisationSelections["Ward pact"] = "Undead | Salamanders | Shades";

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            index.Definitions,
            index.InjectionRules);

        var required = new List<RequiredChoice>
        {
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 2 },
            new() { Source = RequiredChoiceSource.Race, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 2 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Ancient Folk", SpecialisationKey = "Ward pact", Level = 4 }
        };

        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var screen = CharacterSpecialisationScreenCalculator.ApplySavedSelections(context, specs);
        var wardPactSection = Assert.Single(screen.Sections.Where(section => section.Spec.SectionId.Equals("choice:Ward pact", StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(3, wardPactSection.Spec.Levels.Count);
        Assert.Equal(3, wardPactSection.SelectedByLevel.Count);

        var orderedLevelKeys = wardPactSection.Spec.Levels.OrderBy(level => level).ToList();
        Assert.Equal("Undead", wardPactSection.SelectedByLevel[orderedLevelKeys[0]]);
        Assert.Equal("Salamanders", wardPactSection.SelectedByLevel[orderedLevelKeys[1]]);
        Assert.Equal("Shades", wardPactSection.SelectedByLevel[orderedLevelKeys[2]]);
    }

    [Fact]
    public void BuildScreenSections_StandardScoutSkillDuplicateLevelsKeepSeparateSlots()
    {
        var definitions = new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Standard Scout skill"] = new SpecialisationDefinition
            {
                Key = "definition.standard-scout-skill",
                ChoiceSets =
                [
                    new SpecialisationChoiceSet
                    {
                        Id = "choice.standard-scout-skill.primary",
                        Title = "Standard Scout skill",
                        Mode = ChoiceMode.Single,
                        Options =
                        [
                            new ChoiceOption
                            {
                                Key = "Athlete",
                                Label = "Athlete"
                            }
                        ]
                    }
                ]
            }
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            new CharacterDraft { Class = "Pathfinder", Race = "Human" },
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            new ReadOnlyDictionary<string, SpecialisationDefinition>(definitions));

        var required = new List<RequiredChoice>
        {
            new() { Source = RequiredChoiceSource.Class, SourceName = "Pathfinder", SpecialisationKey = "Standard Scout skill", Level = 2 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Pathfinder", SpecialisationKey = "Standard Scout skill", Level = 2 },
            new() { Source = RequiredChoiceSource.Class, SourceName = "Pathfinder", SpecialisationKey = "Standard Scout skill", Level = 3 }
        };

        var sections = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var scoutSection = Assert.Single(sections.Where(section => section.SectionId.Equals("choice:Standard Scout skill", StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(3, scoutSection.Levels.Count);
        Assert.Equal(2, scoutSection.Levels.Count(level => (level > 99 ? level / 100 : level) == 2));
    }

    [Fact]
    public async Task Recalculate_HumanIshmaicSubtypeShowsBaselineAbilityRows()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var classes = await ClassService.GetAllAsync();
        var races = await PeopleService.GetAllAsync();
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warrior",
            RaceSubtypeValue = "Ishmaic"
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            classes,
            races,
            index.Definitions,
            index.InjectionRules);

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var subtypeSpec = Assert.Single(specs.Where(section => section.Kind == SpecialisationSectionKind.RaceSubtype));

        var mappedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [subtypeSpec.SectionId] = "Ishmaic"
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState
            {
                RaceSubtype = "Ishmaic",
                MappedSelections = new ReadOnlyDictionary<string, string>(mappedSelections)
            });

        var subtypeSection = Assert.Single(screen.Sections.Where(section => section.Spec.Kind == SpecialisationSectionKind.RaceSubtype));
        var abilityNames = subtypeSection.AbilityRows
            .Select(row => row.Ability)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        Assert.Contains("Survival", abilityNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("1st Extra Level of Life", abilityNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildScreenSections_HumanSubtypeOmitsLegacyBarbarianOption()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var peoplePath = Path.Combine(ServiceTestEnvironment.PackageRoot, "people", "people.json");
        var original = await File.ReadAllTextAsync(peoplePath);
        var withLegacyBarbarian = original.Replace(
            "Standard,Baronial,Ishmaic,Amlesian",
            "Standard,Baronial,Ishmaic,Barbarian,Amlesian",
            StringComparison.Ordinal);

        FileSystem.SetPackageOverride(
            "people/people.json",
            () => new MemoryStream(Encoding.UTF8.GetBytes(withLegacyBarbarian)));

        ServiceCacheResetter.ResetAll();
        try
        {
            var classes = await ClassService.GetAllAsync();
            var races = await PeopleService.GetAllAsync();
            var index = await SpecialisationDefinitionRepository.GetIndexAsync();

            var draft = new CharacterDraft
            {
                Race = "Human",
                Class = "Warrior"
            };

            var context = CharacterSpecialisationScreenCalculator.LoadContext(
                draft,
                classes,
                races,
                index.Definitions,
                index.InjectionRules);

            var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
            var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
            var subtypeSpec = Assert.Single(specs.Where(section => section.Kind == SpecialisationSectionKind.RaceSubtype));
            var keys = subtypeSpec.Options
                .Select(option => (option.Key ?? string.Empty).Trim())
                .Where(key => key.Length > 0)
                .ToList();

            Assert.DoesNotContain("Barbarian", keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Baronial", keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Ishmaic", keys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Amlesian", keys, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            FileSystem.ClearPackageOverrides();
            ServiceCacheResetter.ResetAll();
        }
    }

    [Fact]
    public async Task ApplySavedSelections_HumanUnknownSubtypeFallsBackToStandard()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var classes = await ClassService.GetAllAsync();
        var races = await PeopleService.GetAllAsync();
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warrior",
            RaceSubtypeValue = "Barbarian"
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            classes,
            races,
            index.Definitions,
            index.InjectionRules);

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var screen = CharacterSpecialisationScreenCalculator.ApplySavedSelections(context, specs);

        var subtypeSection = Assert.Single(screen.Sections.Where(section => section.Spec.Kind == SpecialisationSectionKind.RaceSubtype));
        Assert.Equal("Standard", screen.RaceSubtypeValue);
        Assert.Equal("Standard", subtypeSection.SelectedOption);
    }

    [Fact]
    public async Task Recalculate_HumanEmptySubtypeFallsBackToStandard()
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var classes = await ClassService.GetAllAsync();
        var races = await PeopleService.GetAllAsync();
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warrior",
            RaceSubtypeValue = string.Empty
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            classes,
            races,
            index.Definitions,
            index.InjectionRules);

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState());

        var subtypeSection = Assert.Single(screen.Sections.Where(section => section.Spec.Kind == SpecialisationSectionKind.RaceSubtype));
        Assert.Equal("Standard", screen.RaceSubtypeValue);
        Assert.Equal("Standard", subtypeSection.SelectedOption);
        Assert.True(subtypeSection.IsComplete);
    }

    public static IEnumerable<object[]> RaceSubtypeSelectionCases()
    {
        yield return new object[] { "Human", "Warrior", "Standard" };
        yield return new object[] { "Elf", "Wizard", "Fire" };
        yield return new object[] { "Crol", "Warrior", "Jaerseen" };
        yield return new object[] { "Ancient Folk", "Warrior", "The cloud way" };
    }

    [Theory]
    [MemberData(nameof(RaceSubtypeSelectionCases))]
    public async Task Recalculate_RaceSubtypeSelection_PersistsThroughPeopleAndChoiceSetPipeline(
        string race,
        string @class,
        string selectedSubtype)
    {
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();

        var classes = await ClassService.GetAllAsync();
        var races = await PeopleService.GetAllAsync();
        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        var draft = new CharacterDraft
        {
            Race = race,
            Class = @class
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            classes,
            races,
            index.Definitions,
            index.InjectionRules);

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var subtypeSpec = Assert.Single(specs.Where(section => section.Kind == SpecialisationSectionKind.RaceSubtype));
        Assert.Contains(subtypeSpec.Options, option => string.Equals(option.Key, selectedSubtype, StringComparison.OrdinalIgnoreCase));

        var mappedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [subtypeSpec.SectionId] = selectedSubtype
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState
            {
                RaceSubtype = selectedSubtype,
                MappedSelections = new ReadOnlyDictionary<string, string>(mappedSelections)
            });

        var subtypeSection = Assert.Single(screen.Sections.Where(section => section.Spec.Kind == SpecialisationSectionKind.RaceSubtype));
        Assert.Equal(selectedSubtype, screen.RaceSubtypeValue, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(selectedSubtype, subtypeSection.SelectedOption, StringComparer.OrdinalIgnoreCase);
        Assert.True(subtypeSection.SelectedOption.Length > 0);
    }

    [Fact]
    public void BuildScreenSections_PeopleTypeInjection_UsesBarbarianSelectionAsTribal()
    {
        var definition = new SpecialisationDefinition
        {
            Key = "TestMappedDefinition",
            ChoiceSets =
            [
                new SpecialisationChoiceSet
                {
                    Id = "choice.test.mapped",
                    DefinitionKey = "TestMappedDefinition",
                    Title = "TestMappedDefinition",
                    Required = true,
                    Mode = ChoiceMode.MappedSingle,
                    Options =
                    [
                        new ChoiceOption
                        {
                            Key = "Enabled",
                            Label = "Enabled"
                        }
                    ]
                }
            ]
        };

        var definitions = new ReadOnlyDictionary<string, SpecialisationDefinition>(
            new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["TestMappedDefinition"] = definition
            });

        var injectionRules = new List<SpecialisationInjectionRule>
        {
            new()
            {
                Id = "tribal-only-test",
                Conditions = new InjectionRuleConditions
                {
                    Race = "Human",
                    PeopleTypeIn = ["Tribal"]
                },
                Section = new InjectionRuleSection
                {
                    SectionType = "Mapped",
                    DefinitionKey = "TestMappedDefinition",
                    Title = "Tribal test section",
                    DetailKey = "TestMappedDefinition",
                    SectionId = "mapped:tribal-test",
                    Required = true
                }
            }
        };

        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warrior"
        };
        draft.SpecialisationSelections["Barbarian"] = "Barbarian";

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Human"] = new PeopleRecord
                {
                    PeopleType = ["Standard"]
                }
            },
            definitions,
            injectionRules);

        var sections = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, Array.Empty<RequiredChoice>());
        Assert.Contains(sections, section => section.SectionId.Equals("mapped:tribal-test", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Recalculate_RejectsDuplicateSelectionsAcrossLinkedChoiceGroup()
    {
        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            new CharacterDraft
            {
                Race = "Crol",
                Class = "Wizard",
                RaceSubtypeValue = "Jaerseen"
            },
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase));

        var strategyIds = new[] { "validation:unique-across-group", "selection-group:crol-talents" };
        var specs = new List<SpecialisationSectionSpec>
        {
            new()
            {
                SectionId = "choice:crol-minor",
                DefinitionKey = "Crol Minor Talent (Jaerseen)",
                DetailKey = "Crol Minor Talent (Jaerseen)",
                Title = "Crol Minor Talent",
                Kind = SpecialisationSectionKind.Choice,
                Required = true,
                Levels = [2],
                StrategyIds = strategyIds
            },
            new()
            {
                SectionId = "choice:crol-medium",
                DefinitionKey = "Crol Medium Talent (Jaerseen)",
                DetailKey = "Crol Medium Talent (Jaerseen)",
                Title = "Crol Medium Talent",
                Kind = SpecialisationSectionKind.Choice,
                Required = true,
                Levels = [5],
                StrategyIds = strategyIds
            }
        };

        var choiceSelections = new Dictionary<string, ChoiceSelectionState>(StringComparer.OrdinalIgnoreCase)
        {
            ["choice:crol-minor"] = new ChoiceSelectionState
            {
                SelectedByLevel = new ReadOnlyDictionary<int, string>(new Dictionary<int, string> { [2] = "Large lung capacity" })
            },
            ["choice:crol-medium"] = new ChoiceSelectionState
            {
                SelectedByLevel = new ReadOnlyDictionary<int, string>(new Dictionary<int, string> { [5] = "Large lung capacity" })
            }
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState
            {
                RaceSubtype = "Jaerseen",
                ChoiceSelections = new ReadOnlyDictionary<string, ChoiceSelectionState>(choiceSelections),
                MappedSelections = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
            });

        Assert.False(screen.IsComplete);
        var issues = screen.Sections.Where(section => !string.IsNullOrWhiteSpace(section.ValidationMessage)).ToList();
        Assert.Equal(2, issues.Count);
        Assert.All(issues, section =>
            Assert.Contains("Duplicate selections detected across linked choice groups.", section.ValidationMessage));
    }

    [Fact]
    public void Recalculate_RejectsSelectionBeforeOptionMinLevel()
    {
        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            new CharacterDraft
            {
                Race = "Crol",
                Class = "Wizard",
                RaceSubtypeValue = "Jaerseen"
            },
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase));

        var options = new List<ChoiceOption>
        {
            new()
            {
                Key = "Large lung capacity",
                Label = "Large lung capacity",
                Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MinLevel"] = "2"
                })
            },
            new()
            {
                Key = "Self Heal",
                Label = "Self Heal",
                Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MinLevel"] = "5"
                })
            }
        };

        var specs = new List<SpecialisationSectionSpec>
        {
            new()
            {
                SectionId = "choice:crol-talents",
                DefinitionKey = "Crol Talents (Jaerseen)",
                DetailKey = "Crol Talents (Jaerseen)",
                Title = "Crol Talents",
                Kind = SpecialisationSectionKind.Choice,
                Required = true,
                Levels = [2, 5, 8],
                StrategyIds = ["validation:min-level-by-option-metadata"],
                Options = options
            }
        };

        var choiceSelections = new Dictionary<string, ChoiceSelectionState>(StringComparer.OrdinalIgnoreCase)
        {
            ["choice:crol-talents"] = new ChoiceSelectionState
            {
                SelectedByLevel = new ReadOnlyDictionary<int, string>(new Dictionary<int, string>
                {
                    [2] = "Self Heal",
                    [5] = "Large lung capacity"
                })
            }
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState
            {
                RaceSubtype = "Jaerseen",
                ChoiceSelections = new ReadOnlyDictionary<string, ChoiceSelectionState>(choiceSelections),
                MappedSelections = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
            });

        Assert.False(screen.IsComplete);
        var issueSection = Assert.Single(screen.Sections.Where(section => !string.IsNullOrWhiteSpace(section.ValidationMessage)));
        Assert.Contains("Self Heal is only available from level 5.", issueSection.ValidationMessage);
    }

    [Fact]
    public void Recalculate_RejectsSelectionOutsideOptionExactLevel()
    {
        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            new CharacterDraft
            {
                Race = "Crol",
                Class = "Warrior",
                RaceSubtypeValue = "Yashtar"
            },
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase));

        var options = new List<ChoiceOption>
        {
            new()
            {
                Key = "Large lung capacity",
                Label = "Large Lung Capacity",
                Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Level"] = "2"
                })
            },
            new()
            {
                Key = "Leg Sweep",
                Label = "Leg Sweep",
                Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Level"] = "5"
                })
            }
        };

        var specs = new List<SpecialisationSectionSpec>
        {
            new()
            {
                SectionId = "choice:crol-talents",
                DefinitionKey = "Crol Talents (Yashtar)",
                DetailKey = "Crol Talents (Yashtar)",
                Title = "Crol Talents",
                Kind = SpecialisationSectionKind.Choice,
                Required = true,
                Levels = [2, 5, 8],
                StrategyIds = ["validation:exact-level-by-option-metadata"],
                Options = options
            }
        };

        var choiceSelections = new Dictionary<string, ChoiceSelectionState>(StringComparer.OrdinalIgnoreCase)
        {
            ["choice:crol-talents"] = new ChoiceSelectionState
            {
                SelectedByLevel = new ReadOnlyDictionary<int, string>(new Dictionary<int, string>
                {
                    [5] = "Large lung capacity"
                })
            }
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState
            {
                RaceSubtype = "Yashtar",
                ChoiceSelections = new ReadOnlyDictionary<string, ChoiceSelectionState>(choiceSelections),
                MappedSelections = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
            });

        Assert.False(screen.IsComplete);
        var issueSection = Assert.Single(screen.Sections.Where(section => !string.IsNullOrWhiteSpace(section.ValidationMessage)));
        Assert.Contains("Large Lung Capacity is only available at level 2.", issueSection.ValidationMessage);
    }

    [Fact]
    public void Recalculate_ChoiceSectionEnforcesOptionAlignmentRestrictionsWhenStrategyEnabled()
    {
        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Wizard",
            Alignment = new Alignment(OrderAxis.Lawful, MoralAxis.Good)
        };
        draft.SetAvailableAlignmentsFromRules(new AlignmentRule?[]
        {
            new AlignmentRule
            {
                Mode = "set",
                AllowedPairs = ["Lawful Good"]
            }
        });

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            draft,
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase));

        var specs = new List<SpecialisationSectionSpec>
        {
            new()
            {
                SectionId = "choice:wizard-colour",
                DefinitionKey = "Wizard Colour",
                DetailKey = "Wizard Colour",
                Title = "Wizard Colour",
                Kind = SpecialisationSectionKind.Choice,
                Required = true,
                Levels = [1],
                StrategyIds = ["validation:option-restrictions"],
                Options =
                [
                    new ChoiceOption
                    {
                        Key = "Red",
                        Label = "Red",
                        Restrictions = new Restrictions
                        {
                            AlignmentRestriction = ["Chaotic", "Neutral"]
                        }
                    }
                ]
            }
        };

        var choiceSelections = new Dictionary<string, ChoiceSelectionState>(StringComparer.OrdinalIgnoreCase)
        {
            ["choice:wizard-colour"] = new ChoiceSelectionState
            {
                SelectedByLevel = new ReadOnlyDictionary<int, string>(new Dictionary<int, string>
                {
                    [1] = "Red"
                })
            }
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            specs,
            new SpecialisationSelectionState
            {
                ChoiceSelections = new ReadOnlyDictionary<string, ChoiceSelectionState>(choiceSelections),
                MappedSelections = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
            });

        Assert.False(screen.IsComplete);
        var issueSection = Assert.Single(screen.Sections.Where(section => !string.IsNullOrWhiteSpace(section.ValidationMessage)));
        Assert.Contains("Alignment requirements conflict with the current character.", issueSection.ValidationMessage);
    }

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
    public void ApplySavedSelections_PreservesSingleChoiceCustomisationToken()
    {
        var draft = new CharacterDraft
        {
            Race = "Human",
            Class = "Warlock"
        };
        draft.SpecialisationSelections["1st Weapon Mastery"] = "1st Weapon Mastery::Sword";

        var definitions = new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["1st Weapon Mastery"] = new SpecialisationDefinition
            {
                Key = "1st Weapon Mastery",
                ChoiceSets =
                [
                    new SpecialisationChoiceSet
                    {
                        Id = "1st Weapon Mastery:primary",
                        DefinitionKey = "1st Weapon Mastery",
                        Title = "1st Weapon Mastery",
                        Mode = ChoiceMode.Single,
                        Required = true,
                        Options =
                        [
                            new ChoiceOption
                            {
                                Key = "1st Weapon Mastery",
                                Label = "1st Weapon Mastery",
                                Customisation = new OptionCustomisation
                                {
                                    OptionEnum = "WeaponType",
                                    CustomValuesPermitted = false
                                }
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
                ["Warlock"] = new CharacterClassRecord
                {
                    Levels = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["1"] = [new AbilityDefinition { Name = "1st Weapon Mastery" }]
                    }
                }
            },
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["Human"] = new PeopleRecord()
            },
            definitions);

        var required = CharacterSpecialisationScreenCalculator.ResolveRequiredChoices(context);
        var specs = CharacterSpecialisationScreenCalculator.BuildScreenSections(context, required);
        var screen = CharacterSpecialisationScreenCalculator.ApplySavedSelections(context, specs);

        var section = Assert.Single(screen.Sections.Where(s => s.Spec.Title == "1st Weapon Mastery"));
        Assert.Equal("1st Weapon Mastery", section.SelectedByLevel[1]);
        Assert.Equal("Sword", section.CustomisationByLevel[1]);
        Assert.Equal("1st Weapon Mastery::Sword", screen.PersistedSelections["1st Weapon Mastery"]);
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
