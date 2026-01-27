using System.Collections.ObjectModel;
using System.Linq;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.Characters;

public partial class CharacterWalletPage : ContentPage
{
    public ObservableCollection<Character> Characters { get; } = new();

    public CharacterWalletPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCharacters();
    }

    private void LoadCharacters()
    {
        Characters.Clear();
        var list = LiteDbService.GetCharacters()
            .OrderByDescending(c => c.UpdatedUtc)
            .ToList();

        foreach (var c in list)
            Characters.Add(c);

        EmptyStateLabel.IsVisible = Characters.Count == 0;
    }

    private async void OnViewClicked(object sender, EventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is Character c)
        {
            await Navigation.PushAsync(new CharacterReviewPage(c));
        }
    }

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is Character c)
        {
            await Navigation.PushAsync(new CharacterReviewPage(c));
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is Character c)
        {
            var draft = LiteDbService.ToDraft(c);
            await Navigation.PushAsync(new Wizard(draft, async () => await Navigation.PopToRootAsync()));
        }
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is Character c)
        {
            LiteDbService.DeleteChar(c.Id);
            LoadCharacters();
        }
    }
}
