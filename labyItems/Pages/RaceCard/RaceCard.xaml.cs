using labyItems.Services;
namespace labyItems.Pages.RaceCard;
public partial class RaceCard : ContentPage
{
    public RaceCard() { InitializeComponent(); }
    public RaceCard(string name, PeopleRecord record) : this() { Details.RaceName = name; Details.RaceRecord = record; Title = "Races"; DetailHeader.HeaderText = Title; }
}
