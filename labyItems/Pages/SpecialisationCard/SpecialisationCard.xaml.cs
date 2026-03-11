using labyItems.Services;

namespace labyItems.Pages.SpecialisationCard;

public partial class SpecialisationCard : ContentPage
{
    public SpecialisationCard()
    {
        InitializeComponent();
    }

    public SpecialisationCard(string specialisationName, SpecialisationRecord specialisation, string? selectedOption = null)
        : this()
    {
        SpecialisationDetails.SpecialisationName = specialisationName;
        SpecialisationDetails.Specialisation = specialisation;
        SpecialisationDetails.SelectedOption = selectedOption ?? string.Empty;

        var baseTitle = (specialisationName ?? string.Empty).Trim();
        var selected = (selectedOption ?? string.Empty).Trim();

        if (baseTitle.Length == 0)
            baseTitle = "Specialisation";

        Title = selected.Length == 0
            ? baseTitle
            : $"{baseTitle} ({selected})";
    }
}
