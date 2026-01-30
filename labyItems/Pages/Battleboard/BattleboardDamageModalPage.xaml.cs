using labyItems.Pages.Battleboard.ViewModels;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Battleboard;

public partial class BattleboardDamageModalPage : TabbedPage
{
    private readonly BattleboardDamageModalViewModel _vm;

    public BattleboardDamageModalPage(BattleboardViewModel board, object target)
    {
        InitializeComponent();
        _vm = new BattleboardDamageModalViewModel(board, target);
        BindingContext = _vm;

        ToolbarItems.Add(new ToolbarItem("Done", null, async () => await Navigation.PopModalAsync()));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (Application.Current?.MainPage is Page page)
            page.BackgroundColor = Colors.White;
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
