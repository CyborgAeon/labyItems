using System.Collections.ObjectModel;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using SpecialisationCardPage = labyItems.Pages.SpecialisationCard.SpecialisationCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.AbilityCard;

public partial class AbilityCardOptionListPage : ContentPage
{
    private static readonly Color RowEvenColor = Colors.White;           // White
    private static readonly Color RowOddColor = Color.FromArgb("#F6F6F6"); // Light gray

    public ObservableCollection<ChoiceSetAbilityRowVm> Options { get; } = new();
    public string TitleText { get; private set; }

    public AbilityCardOptionListPage(string title, IReadOnlyList<ChoiceSetAbilityRowVm> options)
    {
        InitializeComponent();
        Title = title;
        TitleText = title;
        
        var index = 0;
        foreach (var option in options)
        {
            option.RowBackgroundColor = index % 2 == 0 ? RowEvenColor : RowOddColor;
            Options.Add(option);
            index++;
        }

        BindingContext = this;
    }

    private async void OnInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ChoiceSetAbilityRowVm option)
            return;

        var detail = await DetailCardLookupService.ResolveDetailAsync(option.LookupKey, option.DisplayName);
        if (detail?.Ability != null)
        {
            await Navigation.PushAsync(new AbilityCardPage(detail.Ability));
            return;
        }

        if (detail?.Specialisation != null)
        {
            await Navigation.PushAsync(new SpecialisationCardPage(detail.Key, detail.Specialisation));
            return;
        }

        await DisplayAlert("No Ability Card", $"Could not find a detail card for \"{option.DisplayName}\".", "OK");
    }
}
