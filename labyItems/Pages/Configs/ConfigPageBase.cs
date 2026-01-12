using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Calculator;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Configs;

public abstract class ConfigPageBase<TConfig> : ContentPage
    where TConfig : ConfigBase, new()
{
    private TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion
    {
        get
        {
            if (_tcs.Task.IsCompleted)
                _tcs = new TaskCompletionSource<CalcResult?>();
            return _tcs.Task;
        }
    }

    public TConfig Config { get; private set; }
    public Command ReturnFromConfigCommand { get; set; }
    private Pages.Calculator.IspCalculator? _calculatorContext;
    public Pages.Calculator.IspCalculator? CalculatorContext
    {
        get => _calculatorContext;
        set
        {
            _calculatorContext = value;
            OnPropertyChanged();
        }
    }
    private bool CompletionSet => _tcs.Task.IsCompleted;

    protected ConfigPageBase()
    {
        Config = new TConfig();
        BindingContext = Config;

        ReturnFromConfigCommand = new Command(async () =>
        {
            Complete(BuildResult(Config));
            await StickyFooterControl.DefaultNavigateAsync(this);
        });
    }

    public void ApplyBaseTotal(int baseTotal)
    {
        Config.BaseIsp = baseTotal;
    }

    public void ResetConfig()
    {
        var baseIsp = Config.BaseIsp;
        Config = new TConfig { BaseIsp = baseIsp };
        BindingContext = Config;
    }

    protected abstract CalcResult BuildResult(TConfig cfg);

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // OnDisappearing fires when a child page is pushed (search, etc) as well as when this
        // page is popped. Delay and check the navigation stack so we only complete on a true pop.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(25);

            var stillInStack = Navigation?.NavigationStack?.Contains(this) == true;
            if (!stillInStack && !CompletionSet)
                Complete(null);
        });
    }

    protected override bool OnBackButtonPressed()
    {
        Complete(null);
        return base.OnBackButtonPressed();
    }

    protected void Complete(CalcResult? result)
    {
        if (CompletionSet)
            return;

        _tcs.TrySetResult(result);
    }
}
