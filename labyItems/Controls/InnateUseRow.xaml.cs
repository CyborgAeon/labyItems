using labyItems.Pages.Battleboard.ViewModels;

namespace labyItems.Controls;

public partial class InnateUseRow : ContentView
{
    private InnateRowVm? _vm;

    public InnateUseRow()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.PulseRequested -= OnPulseRequested;

        _vm = BindingContext as InnateRowVm;
        if (_vm != null)
            _vm.PulseRequested += OnPulseRequested;
    }

    private async void OnPulseRequested(InnateRowVm vm)
    {
        if (UsesLabel == null)
            return;

        await UsesLabel.ScaleTo(1.12, 80, Easing.CubicOut);
        await UsesLabel.ScaleTo(1.0, 80, Easing.CubicIn);
    }
}
