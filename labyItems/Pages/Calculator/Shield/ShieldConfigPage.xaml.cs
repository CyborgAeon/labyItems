using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;

namespace labyItems.Pages.Calculator;

public partial class ShieldConfigPage : ContentPage
{
    private readonly TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion => _tcs.Task;

    public ShieldConfigPage()
    {
        InitializeComponent();
        BindingContext = new ShieldConfig();
    }

    public void ApplyBaseTotal(int baseTotal)
    {
        if (BindingContext is ShieldConfig cfg)
            cfg.BaseIsp = baseTotal;
    }

    private string BuildSummary(ShieldConfig cfg)
    {
        var kind = cfg.SelectedShield switch
        {
            ShieldKind.Magical0   => "+0 Magical Shield",
            ShieldKind.Spiritual0 => "+0 Spiritual Shield",
            ShieldKind.Mantic0    => "+0 Mantic Shield",
            _                     => "No Shield"
        };

        var tags = new List<string>();
        if (cfg.SelectedShield == ShieldKind.Magical0 && cfg.MagicalColoursCount > 0)
            tags.Add($"+{2 * cfg.MagicalColoursCount} (colours)");
        if (cfg.SelectedShield == ShieldKind.Spiritual0 && cfg.SpiritualNonOpposite)
            tags.Add("+3 (non-opposite)");

        var tagText = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
        return $"{kind}{tagText} → {cfg.Total} ISP";
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
        var cfg = (ShieldConfig)BindingContext;
        var res = new CalcResult
        {
            TotalIsp = cfg.Total,
            Summary = BuildSummary(cfg)
        };
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
    private static int TryParseInt(string? s) => int.TryParse(s, out var v) ? v : 0;
}
