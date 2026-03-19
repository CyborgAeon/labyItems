using System;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
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
}
