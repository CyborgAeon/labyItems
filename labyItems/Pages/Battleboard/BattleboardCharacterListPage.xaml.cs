using System.Collections.ObjectModel;
using System.Linq;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Services;

namespace labyItems.Pages.Battleboard;

public partial class BattleboardCharacterListPage : ContentPage
{
    public ObservableCollection<Character> Characters { get; } = new();

    public BattleboardCharacterListPage()
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

    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is BindableObject bo && bo.BindingContext is Character c)
        {
            var draft = LiteDbService.ToDraft(c) ?? new CharacterDraft();
            await Navigation.PushAsync(new BattleboardPage(draft));
        }
    }
}
