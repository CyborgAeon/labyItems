using System;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using SQLite;
using Xunit;

namespace labyItems.Tests;

public sealed class AdvanceAbilitySearchVmTests : ServiceTestBase
{
    [Fact]
    public async Task SelectingAbility_UpdatesSelectedCountAndSaveEligibility()
    {
        var root = new AdvanceCharacterVm(new CharacterDraft
        {
            Class = "Wizard",
            Race = "Human"
        });

        using var vm = new AdvanceAbilitySearchVm(root);
        await vm.LoadAsync();
        await WaitForAsync(() => vm.FilteredAbilities.Count > 0);

        var item = vm.FilteredAbilities.First();
        item.IsSelected = true;

        await WaitForAsync(() => vm.SelectedCount == 1);
        Assert.True(vm.HasSelectedAbilities);
        Assert.Equal("Selected: 1", vm.SelectedSummaryText);

        item.IsSelected = false;

        await WaitForAsync(() => vm.SelectedCount == 0);
        Assert.False(vm.HasSelectedAbilities);
        Assert.Equal("Selected: 0", vm.SelectedSummaryText);
    }

    [Fact]
    public async Task ToggleSelectedOnly_FiltersToSelectedAbilities()
    {
        var root = new AdvanceCharacterVm(new CharacterDraft
        {
            Class = "Wizard",
            Race = "Human"
        });

        using var vm = new AdvanceAbilitySearchVm(root);
        await vm.LoadAsync();
        await WaitForAsync(() => vm.FilteredAbilities.Count >= 2);

        vm.FilteredAbilities[0].IsSelected = true;
        vm.FilteredAbilities[1].IsSelected = true;
        await WaitForAsync(() => vm.SelectedCount == 2);

        vm.ToggleSelectedOnlyCommand.Execute(null);

        await WaitForAsync(() => vm.ShowSelectedOnly);
        await WaitForAsync(() => vm.FilteredAbilities.Count == 2 && vm.FilteredAbilities.All(item => item.IsSelected));
        Assert.Equal("Selected: 2 (showing selected)", vm.SelectedSummaryText);

        vm.FilteredAbilities[1].IsSelected = false;
        await WaitForAsync(() => vm.SelectedCount == 1);
        await WaitForAsync(() => vm.FilteredAbilities.Count == 1 && vm.FilteredAbilities.All(item => item.IsSelected));
    }

    [Fact]
    public async Task LoadAsync_DoesNotPreselectSourceBookFilters()
    {
        AddEvolutionAbility(
            index: "Route Source Classes",
            description: "Visible class source ability",
            cost: 10,
            table: 1,
            available: "Any",
            dataJson: "{\"sourceBook\":\"Classes\",\"abilityRef\":\"ability.test.route-source-classes\"}");

        AddEvolutionAbility(
            index: "Route Source Manufacturers",
            description: "Visible manufacturer source ability",
            cost: 10,
            table: 1,
            available: "Any",
            dataJson: "{\"sourceBook\":\"Manufacturers Guide\",\"abilityRef\":\"ability.make.route-source-manufacturers\"}");

        var root = new AdvanceCharacterVm(new CharacterDraft
        {
            Class = "Wizard",
            Race = "Human"
        });

        using var vm = new AdvanceAbilitySearchVm(root);
        await vm.LoadAsync();

        Assert.All(vm.SourceBookFilters, filter => Assert.False(filter.IsSelected));

        vm.SearchText = "Route Source";

        await WaitForAsync(() => vm.FilteredAbilities.Count == 2);
        Assert.Contains(vm.FilteredAbilities, item => item.Name.Equals("Route Source Classes", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(vm.FilteredAbilities, item => item.Name.Equals("Route Source Manufacturers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetSelectionPrerequisiteIssues_ReportsMissingPrereqs_ForSaveValidation()
    {
        var root = new AdvanceCharacterVm(new CharacterDraft
        {
            Class = "Wizard",
            Race = "Human"
        });

        using var vm = new AdvanceAbilitySearchVm(root);
        await vm.LoadAsync();
        await WaitForAsync(() => vm.FilteredAbilities.Count > 0);

        var prereqAbility = vm.FilteredAbilities
            .First(item => item.Name.Equals("AA1", StringComparison.OrdinalIgnoreCase));
        prereqAbility.IsSelected = true;
        await WaitForAsync(() => vm.SelectedCount == 1);

        var result = vm.GetSelectionPrerequisiteIssues();

        Assert.True(result.HasIssues);
        Assert.Contains(result.Issues, issue => issue.AbilityName.Equals("AA1", StringComparison.OrdinalIgnoreCase));
        var message = vm.BuildMissingPrerequisiteMessage(result);
        Assert.Contains("AA1", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Focus", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildSpecialisationRequestsAsync_UsesChoiceSetRefsFromEvolutionData()
    {
        AddEvolutionAbility(
            index: "Chosen Field",
            description: "Select a field",
            cost: 40,
            table: 1,
            available: "Any",
            dataJson: "{\"abilityRef\":\"ability.druid.chosen-field\",\"sourceBook\":\"Druids Way\",\"choiceSetRefs\":[\"choice.druid.chosen-field.primary\"]}");

        var root = new AdvanceCharacterVm(new CharacterDraft
        {
            Class = "Druid",
            Race = "Human"
        });

        using var vm = new AdvanceAbilitySearchVm(root);
        await vm.LoadAsync();
        await WaitForAsync(() => vm.FilteredAbilities.Count > 0);

        var chosenField = vm.FilteredAbilities.First(item =>
            item.Name.Equals("Chosen Field", StringComparison.OrdinalIgnoreCase));
        chosenField.IsSelected = true;
        await WaitForAsync(() => vm.SelectedCount == 1);

        var requests = await vm.BuildSpecialisationRequestsAsync();

        var request = Assert.Single(requests);
        Assert.Equal("Chosen Field", request.AbilityName);
        Assert.Contains("choice.druid.chosen-field.primary", request.ChoiceSetRefs, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("advancement::ability.druid.chosen-field::1::choice.druid.chosen-field.primary",
            request.BuildStorageKey("choice.druid.chosen-field.primary"));
    }

    [Fact]
    public async Task BuildSpecialisationRequestsAsync_ResolvesClassSpecialisationMappings_WhenAbilityDataHasNoChoiceSetRefs()
    {
        AddEvolutionAbility(
            index: "Weapon Mastery",
            description: "Choose a weapon",
            cost: 20,
            table: 1,
            available: "Any",
            dataJson: "{\"sourceBook\":\"Classes\"}");

        var root = new AdvanceCharacterVm(new CharacterDraft
        {
            Class = "Warrior",
            Race = "Human"
        });

        using var vm = new AdvanceAbilitySearchVm(root);
        await vm.LoadAsync();
        await WaitForAsync(() => vm.FilteredAbilities.Count > 0);

        var weaponMastery = vm.FilteredAbilities.First(item =>
            item.Name.Equals("Weapon Mastery", StringComparison.OrdinalIgnoreCase));
        weaponMastery.IsSelected = true;
        await WaitForAsync(() => vm.SelectedCount == 1);

        var requests = await vm.BuildSpecialisationRequestsAsync();

        var request = Assert.Single(requests);
        Assert.Contains("choice.1st-weapon-mastery.primary", request.ChoiceSetRefs, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 2500)
    {
        var started = DateTime.UtcNow;
        while (!predicate())
        {
            if ((DateTime.UtcNow - started).TotalMilliseconds > timeoutMs)
                throw new TimeoutException("Timed out waiting for condition.");

            await Task.Delay(25);
        }
    }

    private static void AddEvolutionAbility(
        string index,
        string description,
        int cost,
        int table,
        string available,
        string dataJson)
    {
        var evolutionId = SQLiteTestStore.AddEvolution(
            index,
            description,
            cost,
            table,
            available,
            canBuyMultiple: 0,
            preReqsJson: "[]",
            dataJson: dataJson);

        var searchable = ServiceHelper.NormalizeForNgrams($"{index} {description}".ToLowerInvariant());
        var ngrams = ServiceHelper.GenerateNGrams(searchable, 3)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var token in ngrams)
            SQLiteTestStore.AddEvolutionNgram(evolutionId, token);

        EvolutionService.InvalidateCache();
    }
}
