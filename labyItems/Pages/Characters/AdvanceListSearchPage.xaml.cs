using labyItems.Pages.Characters.ViewModels;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;

namespace labyItems.Pages.Characters;

public partial class AdvanceListSearchPage : ContentPage
{
    private readonly AdvanceListSearchVm _vm;
    private bool _disposed;

    public AdvanceListSearchPage(AdvanceListSearchVm vm)
    {
        _vm = vm;
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_disposed)
            return;

        if (Navigation.NavigationStack.Contains(this) || Navigation.ModalStack.Contains(this))
            return;

        DisposeVm();
    }

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not ListSearchItemVm item)
            return;

        switch (item.Raw)
        {
            case Services.SpellService.SpellRaw spell:
                await Navigation.PushAsync(new SpellCardPage(spell));
                break;
            case Services.MiracleService.MiracRaw miracle:
                await Navigation.PushAsync(new MiracleCardPage(miracle));
                break;
            case Services.DruidEvocationService.EvocRaw evocation:
                await Navigation.PushAsync(new EvocationCardPage(evocation));
                break;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
        => await CloseAsync();

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (!await _vm.CommitAsync())
            return;

        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        DisposeVm();

        if (Navigation.NavigationStack.LastOrDefault() == this)
        {
            await Navigation.PopAsync();
            return;
        }

        if (Navigation.ModalStack.LastOrDefault() == this)
        {
            await Navigation.PopModalAsync();
            return;
        }

        if (Shell.Current != null)
            await Shell.Current.GoToAsync("..");
    }

    private void DisposeVm()
    {
        if (_disposed)
            return;

        _disposed = true;
        _vm.Dispose();
    }
}
