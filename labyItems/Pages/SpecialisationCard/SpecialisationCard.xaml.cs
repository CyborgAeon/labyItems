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

        var cardTitle = (selectedOption ?? string.Empty).Trim();
        if (cardTitle.Length == 0)
            cardTitle = specialisationName?.Trim() ?? string.Empty;

        Title = cardTitle.Length == 0 ? "Specialisation" : cardTitle;
    }
}
