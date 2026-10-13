using labyItems.Services;
namespace labyItems.Pages.ClassCard;
public partial class ClassCard : ContentPage
{
    public ClassCard() { InitializeComponent(); }
    public ClassCard(string name, CharacterClassRecord record) : this() { Details.ClassName = name; Details.ClassRecord = record; Title = name; DetailHeader.HeaderText = "Class"; }
}
