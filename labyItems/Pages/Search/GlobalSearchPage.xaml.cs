using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.Search;

public partial class GlobalSearchPage : ContentPage
{
    private readonly GlobalSearchVm _vm = new();
    private bool _isAnimatingFilters;

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

    private async void OnFilterChipClicked(object sender, EventArgs e)
    {
        if (_isAnimatingFilters)
            return;

        if (sender is not Button button)
            return;
        if (button.CommandParameter is not GlobalSearchFilterChipVm chip)
            return;

        var transition = _vm.PreviewFilterTransition(chip);
        if (transition == GlobalSearchFilterTransition.None)
        {
            _vm.ApplyFilterChip(chip);
            return;
        }

        _isAnimatingFilters = true;
        try
        {
            await FilterChipView.FadeTo(0, 180, Easing.CubicOut);
            _vm.ApplyFilterChip(chip);
            FilterChipView.Opacity = 0;
            await FilterChipView.FadeTo(1, 220, Easing.CubicIn);
        }
        finally
        {
            _isAnimatingFilters = false;
        }
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
