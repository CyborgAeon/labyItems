using labyItems.Pages.Battleboard.ViewModels;

namespace labyItems.Controls;

public partial class CastingPoolPill : ContentView
{
    private CastingPoolVm? _vm;

    public CastingPoolPill()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.PulseRequested -= OnPulseRequested;

        _vm = BindingContext as CastingPoolVm;
        if (_vm != null)
            _vm.PulseRequested += OnPulseRequested;
    }

    private async void OnPulseRequested(CastingPoolVm vm)
    {
        if (CurrentLabel == null)
            return;

        await CurrentLabel.ScaleTo(1.1, 80, Easing.CubicOut);
        await CurrentLabel.ScaleTo(1.0, 80, Easing.CubicIn);
    }
}
