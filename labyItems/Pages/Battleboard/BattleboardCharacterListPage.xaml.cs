using System.Collections.ObjectModel;
using System.Linq;
using labyItems.Models;
using labyItems.Services;

namespace labyItems.Pages.Battleboard;

public partial class BattleboardCharacterListPage : ContentPage
{
    public ObservableCollection<Character> Characters { get; } = new();
    private bool _openingBattleboard;

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
        if (_openingBattleboard || sender is not BindableObject bindable || bindable.BindingContext is not Character character)
            return;

        _openingBattleboard = true;
        try
        {
            await SavedBattleboardPreparation.OpenAsync(this, character);
        }
        finally
        {
            _openingBattleboard = false;
        }
    }
}
