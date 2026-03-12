using System.Collections.ObjectModel;
using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Services;
using labyItems.Services.Specialisations;
using Xunit;

namespace labyItems.Tests;

public sealed class MappedSpecialisationSectionVmTests
{
    [Fact]
    public void SingleOptionEnabled_IgnoresWriteBack_WhenSectionHasMultipleOptions()
    {
        var onChangedCalls = 0;
        var vm = CreateVm(
            required: true,
            options:
            [
                new ChoiceOption { Key = "Amlesian", Label = "Amlesian" },
                new ChoiceOption { Key = "Baronial", Label = "Baronial" },
                new ChoiceOption { Key = "Ishmaic", Label = "Ishmaic" },
                new ChoiceOption { Key = "Standard", Label = "Standard" }
            ],
            initialSelection: "Standard",
            onSelectionChanged: () => onChangedCalls++);

        vm.SelectedOption = "Amlesian";
        vm.SingleOptionEnabled = false;
        vm.SingleOptionEnabled = true;

        Assert.Equal("Amlesian", vm.SelectedOption);
        Assert.Equal(1, onChangedCalls);
    }

    [Fact]
    public void SelectedOption_AcceptsDisplayLabel_AndStoresCanonicalKey()
    {
        var vm = CreateVm(
            required: true,
            options:
            [
                new ChoiceOption { Key = "Standard", Label = "Standard Human" },
                new ChoiceOption { Key = "Baronial", Label = "Baronial Human" }
            ],
            initialSelection: null,
            onSelectionChanged: () => { });

        vm.SelectedOption = "Baronial Human";

        Assert.Equal("Baronial", vm.SelectedOption);
        Assert.Equal("Baronial Human", vm.SelectedOptionDisplay);
    }

    [Fact]
    public void SingleOptionEnabled_TogglesSelection_ForSingleOptionalSection()
    {
        var vm = CreateVm(
            required: false,
            options:
            [
                new ChoiceOption { Key = "Barbarian", Label = "Barbarian" }
            ],
            initialSelection: null,
            onSelectionChanged: () => { });

        vm.SingleOptionEnabled = true;
        Assert.Equal("Barbarian", vm.SelectedOption);

        vm.SingleOptionEnabled = false;
        Assert.True(string.IsNullOrWhiteSpace(vm.SelectedOption));
    }

    [Fact]
    public void Recalculate_MappedSelectionLabel_ResolvesToCanonicalKey()
    {
        var section = new SpecialisationSectionSpec
        {
            SectionId = "mapped:test",
            DefinitionKey = "HumanSubtypeAbilities",
            DetailKey = "HumanSubtypeAbilities",
            Title = "Human subtype",
            Kind = SpecialisationSectionKind.Mapped,
            Required = true,
            Options = new List<ChoiceOption>
            {
                new() { Key = "Standard", Label = "Standard Human" },
                new() { Key = "Baronial", Label = "Baronial Human" }
            }
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            new CharacterDraft { Race = "Human", Class = "Warrior" },
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase));

        var mappedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [section.SectionId] = "Baronial Human"
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            [section],
            new SpecialisationSelectionState
            {
                MappedSelections = new ReadOnlyDictionary<string, string>(mappedSelections)
            });

        var mappedSection = Assert.Single(screen.Sections);
        Assert.Equal("Baronial", mappedSection.SelectedOption);
    }

    [Fact]
    public void Recalculate_RaceSubtypeSelectionLabel_ResolvesToCanonicalKey()
    {
        var section = new SpecialisationSectionSpec
        {
            SectionId = "subtype:human:HumanSubtype",
            DefinitionKey = "HumanSubtypeAbilities",
            DetailKey = "HumanSubtypeAbilities",
            Title = "Human origin",
            Kind = SpecialisationSectionKind.RaceSubtype,
            Required = true,
            Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["raceSubtypeKey"] = "HumanSubtype",
                ["abilityMapKey"] = "HumanSubtypeAbilities"
            }),
            Options = new List<ChoiceOption>
            {
                new() { Key = "Standard", Label = "Standard Human" },
                new() { Key = "Baronial", Label = "Baronial Human" }
            }
        };

        var context = CharacterSpecialisationScreenCalculator.LoadContext(
            new CharacterDraft { Race = "Human", Class = "Warrior" },
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SpecialisationDefinition>(StringComparer.OrdinalIgnoreCase));

        var mappedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [section.SectionId] = "Baronial Human"
        };

        var screen = CharacterSpecialisationScreenCalculator.Recalculate(
            context,
            [section],
            new SpecialisationSelectionState
            {
                RaceSubtype = "Baronial Human",
                MappedSelections = new ReadOnlyDictionary<string, string>(mappedSelections)
            },
            raceSubtypeKey: "HumanSubtype",
            raceSubtypeMapKey: "HumanSubtypeAbilities");

        Assert.Equal("Baronial", screen.RaceSubtypeValue);
        var subtypeSection = Assert.Single(screen.Sections);
        Assert.Equal("Baronial", subtypeSection.SelectedOption);
    }

    private static MappedSpecialisationSectionVm CreateVm(
        bool required,
        IReadOnlyList<ChoiceOption> options,
        string? initialSelection,
        Action onSelectionChanged)
    {
        return new MappedSpecialisationSectionVm(
            sectionId: "subtype:Human:HumanSubtype",
            key: "HumanSubtypeAbilities",
            detailKey: "HumanSubtypeAbilities",
            title: "Human origin",
            subtitle: "Select a lineage for your character.",
            options: options,
            initialSelection: initialSelection,
            required: required,
            onSelectionChanged: onSelectionChanged,
            sectionType: SpecialisationSectionType.RaceSubtype);
    }
}
