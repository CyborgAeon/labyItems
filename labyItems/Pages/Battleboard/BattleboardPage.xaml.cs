using labyItems.Models.Characters;
using labyItems.Pages.Battleboard.ViewModels;

namespace labyItems.Pages.Battleboard;

public partial class BattleboardPage : TabbedPage
{
    private readonly BattleboardViewModel _vm;
    private bool _isModalOpen;

    public BattleboardPage(CharacterDraft draft)
    {
        InitializeComponent();
        _vm = new BattleboardViewModel(draft);
        BindingContext = _vm;

        Title = string.IsNullOrWhiteSpace(_vm.CharacterName) ? "Battleboard" : _vm.CharacterName;

        if (!_vm.HasCastingTab && Children.Contains(CastingTab))
            Children.Remove(CastingTab);
    }

    private async void OnLifeTapped(object sender, TappedEventArgs e)
    {
        if (_isModalOpen)
            return;

        object? target = null;
        if (sender is BindableObject bo)
            target = bo.BindingContext;

        if (target == null && e?.Parameter != null)
            target = e.Parameter;

        if (target == null)
            return;

        _isModalOpen = true;
        await Navigation.PushModalAsync(new BattleboardDamageModalPage(_vm, target));
        _isModalOpen = false;
    }
}
