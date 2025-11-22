using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;
using labyItems.Pages.Calculator;
using System.Windows.Input;

namespace labyItems.Pages.Calculator;

public partial class ShieldConfigPage : ContentPage
{
    private readonly TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion => _tcs.Task;
    public IspCalculator? CalculatorContext { get; set; }
    public ICommand ReturnCommand { get; }

    public ShieldConfigPage()
    {
        InitializeComponent();
        BindingContext = new ShieldConfig();
        ReturnCommand = new Command(async () => await OnReturnInternal());
    }

    public void ResetConfig()
    {
        BindingContext = new ShieldConfig { BaseIsp = (BindingContext as ShieldConfig)?.BaseIsp ?? 0 };
    }

    public void ApplyBaseTotal(int baseTotal)
    {
        if (BindingContext is ShieldConfig cfg)
            cfg.BaseIsp = baseTotal;
    }

    private CalcResult BuildResult(ShieldConfig cfg)
    {
        var kind = cfg.SelectedShield switch
        {
            ShieldKind.Magical0   => "+0 Magical Shield",
            ShieldKind.Spiritual0 => "+0 Spiritual Shield",
            ShieldKind.Mantic0    => "+0 Mantic Shield",
            _                     => "No Shield"
        };

        var details = new Dictionary<string, object?>();
        if (cfg.SelectedShield == ShieldKind.Magical0 && cfg.MagicalColoursCount > 0)
            details["magicalColours"] = cfg.MagicalColoursCount;
        if (cfg.SelectedShield == ShieldKind.Spiritual0 && cfg.SpiritualNonOpposite)
            details["spiritualNonOpposite"] = true;

        return new CalcResult
        {
            AbilityType = "Shield",
            AbilityName = kind,
            TotalIsp = cfg.Total,
            Details = details
        };
    }

    // ---- Event handlers to wire from XAML ----

    private void OnCalculate(object sender, EventArgs e)
    {
        var cfg = (ShieldConfig)BindingContext;
        cfg.Recalculate();
    }

    private void OnShieldChanged(object sender, EventArgs e)
    {
        var cfg = (ShieldConfig)BindingContext;
        cfg.Recalculate();
    }

    private void OnMagicalColoursChanged(object sender, TextChangedEventArgs e)
    {
        var cfg = (ShieldConfig)BindingContext;
        cfg.MagicalColoursCount = Math.Max(0, TryParseInt(e.NewTextValue));
        cfg.Recalculate();
    }

    private void OnSpiritualNonOppositeToggled(object sender, ToggledEventArgs e)
    {
        var cfg = (ShieldConfig)BindingContext;
        cfg.SpiritualNonOpposite = e.Value;
        cfg.Recalculate();
    }

    private async void OnReturn(object sender, EventArgs e)
    {
        await OnReturnInternal();
    }

    private async Task OnReturnInternal()
    {
        var cfg = (ShieldConfig)BindingContext;
        var res = BuildResult(cfg);
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
    private static int TryParseInt(string? s) => int.TryParse(s, out var v) ? v : 0;
}
