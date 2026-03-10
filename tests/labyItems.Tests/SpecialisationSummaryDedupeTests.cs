using labyItems.Pages.Characters.ViewModels;
using Xunit;

namespace labyItems.Tests;

public sealed class SpecialisationSummaryDedupeTests
{
    [Fact]
    public void BuildKey_DedupesSameSpecialisationAndSelection()
    {
        var first = SpecialisationSummaryDedupe.BuildKey(
            text: "Amlesian Caste (Lv 1): Landed",
            specialisationKey: "Amlesian Caste",
            selectedOption: "Landed");
        var second = SpecialisationSummaryDedupe.BuildKey(
            text: "Amlesian Caste (Lv 4): Landed",
            specialisationKey: "Amlesian Caste",
            selectedOption: "Landed");

        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildKey_DistinguishesDifferentSelections()
    {
        var first = SpecialisationSummaryDedupe.BuildKey(
            text: "Amlesian Caste: Landed",
            specialisationKey: "Amlesian Caste",
            selectedOption: "Landed");
        var second = SpecialisationSummaryDedupe.BuildKey(
            text: "Amlesian Caste: Merchant",
            specialisationKey: "Amlesian Caste",
            selectedOption: "Merchant");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void BuildKey_IsNonEmptyWhenSpecialisationKeyProvided()
    {
        var key = SpecialisationSummaryDedupe.BuildKey(
            text: "Amlesian Caste: Landed",
            specialisationKey: "Amlesian Caste",
            selectedOption: "Landed");

        Assert.False(string.IsNullOrWhiteSpace(key));
    }
}
