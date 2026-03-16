using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Services;
using Xunit;

namespace labyItems.Tests;

public sealed class WizardSpellRulesTests
{
    [Fact]
    public void BuildBaseSpellEntries_Includes_All_NonAdvanced_And_Assigns_Selected_Colour()
    {
        var spells = new List<SpellService.SpellRaw>
        {
            new() { name = "Elemental Warding", level = 1, colour = "All", isAdvanced = false },
            new() { name = "Elemental Warding (Advanced)", level = 6, colour = "All", isAdvanced = true }
        };

        var result = WizardSpellRules.BuildBaseSpellEntries(spells, new[] { "White" }, includeGreyBonus: false);

        var entry = Assert.Single(result, s => s.Name == "Elemental Warding");
        Assert.Equal("White", entry.Colour);
        Assert.DoesNotContain(result, s => s.Name == "Elemental Warding (Advanced)");
    }

    [Fact]
    public void SpellMatchesWizardSelection_Ele_Matches_Elemental_Colours()
    {
        Assert.True(WizardSpellRules.SpellMatchesWizardSelection("Ele", "Red"));
        Assert.False(WizardSpellRules.SpellMatchesWizardSelection("Ele", "Sorcorial"));
    }

    [Fact]
    public void SpellMatchesWizardSelection_EleAndSoc_Matches_Elemental_And_Sorcorial()
    {
        Assert.True(WizardSpellRules.SpellMatchesWizardSelection("Ele & Soc.", "Green"));
        Assert.True(WizardSpellRules.SpellMatchesWizardSelection("Ele & Soc.", "Sorcorial"));
    }

    [Fact]
    public void SpellMatchesWizardSelection_EleBarGrey_Excludes_Grey()
    {
        Assert.True(WizardSpellRules.SpellMatchesWizardSelection("Ele bar Grey", "Red"));
        Assert.False(WizardSpellRules.SpellMatchesWizardSelection("Ele bar Grey", "Grey"));
    }

    [Fact]
    public void BuildBaseSpellEntries_Assigns_Selected_Elemental_Colour_For_EleAndSoc()
    {
        var spells = new List<SpellService.SpellRaw>
        {
            new() { name = "Palewise's Reforge The Fallen Child", level = 4, colour = "Ele & Soc.", isAdvanced = false }
        };

        var result = WizardSpellRules.BuildBaseSpellEntries(spells, new[] { MagicColours.Green.ToString() }, includeGreyBonus: false);

        var entry = Assert.Single(result);
        Assert.Equal("Palewise's Reforge The Fallen Child", entry.Name);
        Assert.Equal(MagicColours.Green.ToString(), entry.Colour);
    }

    [Fact]
    public void BuildBaseSpellEntries_Includes_Sorcery_When_Sorcorial_Selected()
    {
        var spells = new List<SpellService.SpellRaw>
        {
            new() { name = "Sorcery Bolt", level = 2, colour = "Sorcery", isAdvanced = false },
            new() { name = "Elemental Bolt", level = 2, colour = "Red", isAdvanced = false }
        };

        var result = WizardSpellRules.BuildBaseSpellEntries(spells, new[] { "Sorcorial" }, includeGreyBonus: false);

        Assert.Single(result);
        Assert.Equal("Sorcery Bolt", result[0].Name);
    }

    [Fact]
    public void BuildBaseSpellEntries_GreyBonus_Includes_UpTo_Level8_Only()
    {
        var spells = new List<SpellService.SpellRaw>
        {
            new() { name = "Grey Eight", level = 8, colour = "Grey", isAdvanced = false },
            new() { name = "Grey Nine", level = 9, colour = "Grey", isAdvanced = false }
        };

        var result = WizardSpellRules.BuildBaseSpellEntries(spells, new[] { "Green" }, includeGreyBonus: true);

        Assert.Contains(result, entry => entry.Name == "Grey Eight");
        Assert.DoesNotContain(result, entry => entry.Name == "Grey Nine");
    }
}
