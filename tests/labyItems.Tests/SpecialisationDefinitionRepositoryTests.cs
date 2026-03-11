using System.Text;
using System.Text.Json;
using labyItems.Services.Specialisations;
using Microsoft.Maui.Storage;
using Xunit;

namespace labyItems.Tests;

public sealed class SpecialisationDefinitionRepositoryTests : ServiceTestBase
{
    private const string LegacyPath = "specialisation/specialisation.json";
    private const string AbilitiesPath = "specialisation/abilities.json";
    private const string ChoiceSetsPath = "specialisation/choice-sets.json";
    private const string ClassDefsPath = "specialisation/class-specialisations.json";
    private const string RaceDefsPath = "specialisation/race-subtypes.json";
    private const string OverridesPath = "specialisation/specialisation-overrides.json";

    [Fact]
    public async Task GetIndexAsync_NormalizesLegacyMappedShapeIntoCanonicalChoiceSet()
    {
        ResetPackageOverrides();
        const string json = """
        {
          "LegacyMap": {
            "Amber": {
              "Description": "Legacy option",
              "ClassRestriction": ["Wizard"],
              "1": [
                {
                  "Name": "Legacy Ability",
                  "Type": "Static"
                }
              ]
            }
          }
        }
        """;

        SetLegacyOnly(json);

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        Assert.True(index.Definitions.TryGetValue("LegacyMap", out var def));
        Assert.NotNull(def);
        var choiceSet = Assert.Single(def.ChoiceSets);
        Assert.Equal(ChoiceMode.MappedSingle, choiceSet.Mode);

        var option = Assert.Single(choiceSet.Options);
        Assert.Equal("Amber", option.Key);
        Assert.Equal("Legacy option", option.Description);
        Assert.Contains("Wizard", option.Restrictions.ClassRestriction, StringComparer.OrdinalIgnoreCase);

        var grant = Assert.Single(option.Grants);
        Assert.Equal(1, grant.Level);
        Assert.Equal("Legacy Ability", grant.Ability.Name);
    }

    [Fact]
    public async Task GetIndexAsync_ResolvesRefAndAppliesOverrides()
    {
        ResetPackageOverrides();
        const string json = """
        {
          "AbilityRefs": {
            "1st Weapon Mastery": {
              "Name": "1st Weapon Mastery",
              "Type": "Static",
              "Description": "Base description"
            }
          },
          "Warrior Specialist": {
            "Abilities": [
              {
                "$ref": "1st Weapon Mastery",
                "Customisation": {
                  "OptionEnum": "WeaponTypeO",
                  "CustomValuesPermitted": true
                }
              }
            ]
          }
        }
        """;

        SetLegacyOnly(json);

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        Assert.True(index.Definitions.TryGetValue("Warrior Specialist", out var def));
        Assert.NotNull(def);
        var choiceSet = Assert.Single(def.ChoiceSets);
        var option = Assert.Single(choiceSet.Options);
        var grant = Assert.Single(option.Grants);

        Assert.Equal("1st Weapon Mastery", grant.Ability.Name);
        Assert.Equal("Static", grant.Ability.Type);
        Assert.NotNull(grant.Ability.Customisation);
        Assert.Equal("WeaponTypeO", grant.Ability.Customisation!.OptionEnum);
        Assert.True(grant.Ability.Customisation.CustomValuesPermitted);
    }

    [Fact]
    public async Task GetIndexAsync_ParsesInjectionRules()
    {
        ResetPackageOverrides();
        const string json = """
        {
          "Simple Definition": {
            "A": {
              "Description": "Option A"
            }
          },
          "injectionRules": [
            {
              "Id": "rule-a",
              "Conditions": {
                "Race": "Human",
                "Subtype": "Baronial",
                "ClassNotIn": ["Kallah"]
              },
              "Section": {
                "SectionType": "Mapped",
                "DefinitionKey": "Simple Definition",
                "Title": "Injected Section",
                "SectionId": "mapped:Injected Section",
                "Required": false,
                "OptionalClassIn": ["Kallah Beggar"],
                "InsertIndex": 1
              }
            }
          ]
        }
        """;

        SetLegacyOnly(json);

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        var rule = Assert.Single(index.InjectionRules);
        Assert.Equal("rule-a", rule.Id);
        Assert.Equal("Human", rule.Conditions.Race);
        Assert.Equal("Baronial", rule.Conditions.Subtype);
        Assert.Contains("Kallah", rule.Conditions.ClassNotIn, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("Mapped", rule.Section.SectionType);
        Assert.Equal("Simple Definition", rule.Section.DefinitionKey);
        Assert.Equal("Injected Section", rule.Section.Title);
        Assert.Equal("mapped:Injected Section", rule.Section.SectionId);
        Assert.False(rule.Section.Required);
        Assert.Equal(1, rule.Section.InsertIndex);
    }

    [Fact]
    public async Task GetIndexAsync_ComposesFragmentedFilesAndResolvesChoiceSetRefs()
    {
        ResetPackageOverrides();
        SetFragmented(
            abilitiesJson: """
            {
              "abilities": {
                "ability.1st-weapon-mastery": {
                  "Key": "ability.1st-weapon-mastery",
                  "Name": "1st Weapon Mastery",
                  "Type": "Static",
                  "Effect": "Base description"
                },
                "ability.unarmed-combat": {
                  "Key": "ability.unarmed-combat",
                  "Name": "Unarmed Combat",
                  "Type": "WeaponSkill",
                  "Effect": "Punch things"
                }
              },
              "aliases": {
                "1st Weapon Mastery": "ability.1st-weapon-mastery"
              }
            }
            """,
            choiceSetsJson: """
            {
              "choiceSets": {
                "choice.archer.primary": {
                  "Key": "choice.archer.primary",
                  "Title": "Archer Specialist",
                  "Required": true,
                  "Mode": "Single",
                  "StrategyIds": ["section:choice"],
                  "Options": [
                    {
                      "Key": "1st Weapon Mastery",
                      "Label": "1st Weapon Mastery",
                      "Grants": [{ "AbilityRef": "ability.1st-weapon-mastery" }]
                    },
                    {
                      "Key": "Unarmed Combat",
                      "Label": "Unarmed Combat",
                      "Grants": [{ "AbilityRef": "ability.unarmed-combat" }]
                    }
                  ]
                }
              }
            }
            """,
            classDefinitionsJson: """
            {
              "definitions": {
                "Archer Specialist": {
                  "Key": "definition.archer-specialist",
                  "ChoiceSetRefs": ["choice.archer.primary"]
                }
              }
            }
            """,
            raceDefinitionsJson: """
            {
              "definitions": {}
            }
            """,
            overridesJson: """
            {
              "injectionRules": []
            }
            """);

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        Assert.True(index.Definitions.TryGetValue("Archer Specialist", out var definition));
        Assert.NotNull(definition);

        var choiceSet = Assert.Single(definition.ChoiceSets);
        Assert.Equal("choice.archer.primary", choiceSet.Id);
        Assert.Equal(ChoiceMode.Single, choiceSet.Mode);
        Assert.Equal(2, choiceSet.Options.Count);

        var first = choiceSet.Options[0];
        var firstGrant = Assert.Single(first.Grants);
        Assert.Equal("ability.1st-weapon-mastery", firstGrant.Ability.Key);
        Assert.Equal("1st Weapon Mastery", firstGrant.Ability.Name);
    }

    [Fact]
    public async Task GetIndexAsync_FragmentedGrantsApplyAbilityOverrides()
    {
        ResetPackageOverrides();
        SetFragmented(
            abilitiesJson: """
            {
              "abilities": {
                "ability.1st-weapon-mastery": {
                  "Key": "ability.1st-weapon-mastery",
                  "Name": "1st Weapon Mastery",
                  "Type": "Static",
                  "Effect": "Base description"
                }
              },
              "aliases": {
                "1st Weapon Mastery": "ability.1st-weapon-mastery"
              }
            }
            """,
            choiceSetsJson: """
            {
              "choiceSets": {
                "choice.warrior.primary": {
                  "Key": "choice.warrior.primary",
                  "Title": "Warrior Specialist",
                  "Required": true,
                  "Mode": "Single",
                  "Options": [
                    {
                      "Key": "1st Weapon Mastery",
                      "Label": "1st Weapon Mastery",
                      "Grants": [
                        {
                          "AbilityRef": "ability.1st-weapon-mastery",
                          "Overrides": {
                            "Customisation": {
                              "OptionEnum": "WeaponTypeO",
                              "CustomValuesPermitted": true
                            }
                          }
                        }
                      ]
                    }
                  ]
                }
              }
            }
            """,
            classDefinitionsJson: """
            {
              "definitions": {
                "Warrior Specialist": {
                  "ChoiceSetRefs": ["choice.warrior.primary"]
                }
              }
            }
            """,
            raceDefinitionsJson: """
            {
              "definitions": {}
            }
            """,
            overridesJson: """
            {
              "injectionRules": []
            }
            """);

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        var definition = index.Definitions["Warrior Specialist"];
        var option = Assert.Single(Assert.Single(definition.ChoiceSets).Options);
        var grant = Assert.Single(option.Grants);
        Assert.NotNull(grant.Ability.Customisation);
        Assert.Equal("WeaponTypeO", grant.Ability.Customisation!.OptionEnum);
        Assert.True(grant.Ability.Customisation.CustomValuesPermitted);
    }

    [Fact]
    public async Task GetIndexAsync_FragmentedAbilityLookupSupportsStableKeyAndAlias()
    {
        ResetPackageOverrides();
        SetFragmented(
            abilitiesJson: """
            {
              "abilities": {
                "ability.hedge-wizardry": {
                  "Key": "ability.hedge-wizardry",
                  "Name": "Hedge Wizardry",
                  "Type": "Static",
                  "Effect": "Detail text"
                }
              },
              "aliases": {
                "Hedge Wizardry": "ability.hedge-wizardry"
              }
            }
            """,
            choiceSetsJson: """
            {
              "choiceSets": {}
            }
            """,
            classDefinitionsJson: """
            {
              "definitions": {}
            }
            """,
            raceDefinitionsJson: """
            {
              "definitions": {}
            }
            """,
            overridesJson: """
            {
              "injectionRules": []
            }
            """);

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        Assert.True(index.AbilityReferences.TryGetValue("ability.hedge-wizardry", out var byKey));
        Assert.True(index.AbilityReferences.TryGetValue("Hedge Wizardry", out var byAlias));
        Assert.NotNull(byKey);
        Assert.NotNull(byAlias);
        Assert.Equal("ability.hedge-wizardry", byKey.Key);
        Assert.Equal("ability.hedge-wizardry", byAlias.Key);
    }

    [Fact]
    public async Task GetIndexAsync_ParsesOverridesFromFragmentedFile()
    {
        ResetPackageOverrides();
        SetFragmented(
            abilitiesJson: """
            {
              "abilities": {}
            }
            """,
            choiceSetsJson: """
            {
              "choiceSets": {}
            }
            """,
            classDefinitionsJson: """
            {
              "definitions": {}
            }
            """,
            raceDefinitionsJson: """
            {
              "definitions": {}
            }
            """,
            overridesJson: """
            {
              "injectionRules": [
                {
                  "Id": "rule-fragmented",
                  "Conditions": { "Race": "Human" },
                  "Section": {
                    "SectionType": "Mapped",
                    "DefinitionKey": "HumanSubtypeAbilities",
                    "Title": "Human subtype"
                  }
                }
              ]
            }
            """);

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        var rule = Assert.Single(index.InjectionRules);
        Assert.Equal("rule-fragmented", rule.Id);
        Assert.Equal("Human", rule.Conditions.Race);
        Assert.Equal("HumanSubtypeAbilities", rule.Section.DefinitionKey);
    }

    [Fact]
    public async Task GetIndexAsync_FragmentedChoiceMetadataAndAbilityLoreArePreserved()
    {
        ResetPackageOverrides();
        SetFragmented(
            abilitiesJson: """
            {
              "abilities": {
                "ability.crol-memory": {
                  "Key": "ability.crol-memory",
                  "Name": "Crol Memory",
                  "Type": "Static",
                  "Effect": "Base effect",
                  "Lore": "Memory carried through tattoo rites."
                }
              }
            }
            """,
            choiceSetsJson: """
            {
              "choiceSets": {
                "choice.crol.mapped": {
                  "Key": "choice.crol.mapped",
                  "Title": "CrolSubtypeAbilities",
                  "Mode": "MappedSingle",
                  "Required": true,
                  "Options": [
                    {
                      "Key": "Kaerssor",
                      "Label": "Kaerssor",
                      "Metadata": {
                        "Roleplay": "Warrior caste roleplay",
                        "Lore": "Warrior caste lore"
                      },
                      "Grants": [
                        {
                          "AbilityRef": "ability.crol-memory",
                          "Level": 1
                        }
                      ]
                    }
                  ]
                }
              }
            }
            """,
            classDefinitionsJson: """
            {
              "definitions": {}
            }
            """,
            raceDefinitionsJson: """
            {
              "definitions": {
                "CrolSubtypeAbilities": {
                  "ChoiceSetRefs": ["choice.crol.mapped"]
                }
              }
            }
            """,
            overridesJson: """
            {
              "injectionRules": []
            }
            """);

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        var definition = index.Definitions["CrolSubtypeAbilities"];
        var mappedSet = Assert.Single(definition.ChoiceSets);
        var option = Assert.Single(mappedSet.Options);

        Assert.Equal("Warrior caste roleplay", option.Metadata["Roleplay"]);
        Assert.Equal("Warrior caste lore", option.Metadata["Lore"]);

        var grant = Assert.Single(option.Grants);
        Assert.Equal("Memory carried through tattoo rites.", grant.Ability.Lore);
    }

    [Fact]
    public async Task GetIndexAsync_DefaultFragmentsIncludeCrolTalentDefinitionsAndInjectionRules()
    {
        ResetPackageOverrides();

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();

        Assert.Contains("Crol Talents (Yashtar)", index.Definitions.Keys);
        Assert.Contains("Crol Talents (Kaerssor)", index.Definitions.Keys);
        Assert.Contains("Crol Talents (Jaerseen)", index.Definitions.Keys);

        var crolRules = index.InjectionRules
            .Where(rule =>
                rule.Conditions.Race.Equals("Crol", StringComparison.OrdinalIgnoreCase)
                && rule.Id.EndsWith("-talents", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(3, crolRules.Count);
    }

    private static void ResetPackageOverrides()
    {
        FileSystem.ClearPackageOverrides();
    }

    private static void SetLegacyOnly(string legacyJson)
    {
        SetPackageOverride(AbilitiesPath, "{}");
        SetPackageOverride(ChoiceSetsPath, "{}");
        SetPackageOverride(ClassDefsPath, "{}");
        SetPackageOverride(RaceDefsPath, "{}");
        SetPackageOverride(OverridesPath, "{}");
        SetPackageOverride(LegacyPath, legacyJson);
    }

    private static void SetFragmented(
        string abilitiesJson,
        string choiceSetsJson,
        string classDefinitionsJson,
        string raceDefinitionsJson,
        string overridesJson)
    {
        SetPackageOverride(AbilitiesPath, abilitiesJson);
        SetPackageOverride(ChoiceSetsPath, choiceSetsJson);
        SetPackageOverride(ClassDefsPath, classDefinitionsJson);
        SetPackageOverride(RaceDefsPath, raceDefinitionsJson);
        SetPackageOverride(OverridesPath, overridesJson);
        SetPackageOverride(LegacyPath, "{}");
    }

    private static void SetPackageOverride(string relativePath, string json)
    {
        FileSystem.SetPackageOverride(
            relativePath,
            () => new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
