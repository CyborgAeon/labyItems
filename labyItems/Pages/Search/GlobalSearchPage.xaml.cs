using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Search;

public partial class GlobalSearchPage : ContentPage
{
    private readonly GlobalSearchVm _vm = new();
    private bool _isNavigatingBack;

    public GlobalSearchPage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.EnsureLoadedAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (_isNavigatingBack)
            return;

        _isNavigatingBack = true;
        try
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isNavigatingBack = false;
        }
    }

    private void OnFilterChipTapped(object? sender, TappedEventArgs e)
    {
        var chip = e.Parameter as GlobalSearchFilterChipVm
            ?? (sender as BindableObject)?.BindingContext as GlobalSearchFilterChipVm;
        if (chip == null)
            return;

        _vm.ApplyFilterChip(chip);
    }

    private async void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not GlobalSearchResultVm result || !result.CanOpenDetails)
            return;

        if (result.Ability != null)
        {
            await Navigation.PushAsync(new AbilityCardPage(result.Ability));
            return;
        }

        if (result.Spell != null)
        {
            await Navigation.PushAsync(new SpellCardPage(result.Spell));
            return;
        }

        if (result.Miracle != null)
        {
            await Navigation.PushAsync(new MiracleCardPage(result.Miracle));
            return;
        }

        if (result.Evocation != null)
            await Navigation.PushAsync(new EvocationCardPage(result.Evocation));
    }
}
