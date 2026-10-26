using labyItems.Models.Characters;
using labyItems.Services.Specialisations;
using Xunit;

namespace labyItems.Tests;

public sealed class SpecialisationSelectionPersistenceTests
{
    [Fact]
    public void Reconcile_RemovesRetiredManagedSelection_AndPreservesUnrelatedSelection()
    {
        var draft = new CharacterDraft();
        draft.SpecialisationSelections["Amlesian Caste"] = "Samurai";
        draft.SpecialisationSelections["Unrelated workflow"] = "Keep me";

        SpecialisationSelectionPersistence.Reconcile(
            draft,
            ["Amlesian Caste"],
            new Dictionary<string, string>());

        Assert.DoesNotContain("Amlesian Caste", draft.SpecialisationSelections.Keys);
        Assert.Equal("Keep me", draft.SpecialisationSelections["Unrelated workflow"]);
    }

    [Fact]
    public void Reconcile_ReplacesCurrentManagedValues()
    {
        var draft = new CharacterDraft();
        draft.SpecialisationSelections["Scout Skills"] = "Old";

        SpecialisationSelectionPersistence.Reconcile(
            draft,
            ["Scout Skills"],
            new Dictionary<string, string>
            {
                ["Scout Skills"] = "New"
            });

        Assert.Equal("New", draft.SpecialisationSelections["Scout Skills"]);
    }
}
