using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
using labyItems.Pages.Calculator;
using labyItems.Controls;

namespace labyItems.Pages.Calculator;

public partial class EvocationConfigPage : ContentPage
{
    private readonly TaskCompletionSource<CalcResult?> _tcs = new();
    public Task<CalcResult?> Completion => _tcs.Task;
        public Command ReturnFromConfigCommand { get; }

    public EvocationConfigPage()
    {
        InitializeComponent();
        BindingContext = new EvocationConfig();
        ReturnFromConfigCommand = new Command(async () =>
            {
                if (BindingContext is not EvocationConfig cfg) return;

                var result = new CalcResult
                {
                    TotalIsp = cfg.Total,
                    Summary  = BuildSummary(cfg)
                };

                _tcs.TrySetResult(result);
                await StickyFooterControl.DefaultNavigateAsync(this);
            });
    }
private async void OnFooterReturnClicked(object sender, EventArgs e)
        {
            if (BindingContext is not EvocationConfig cfg) return;

            var result = new CalcResult
            {
                TotalIsp = cfg.Total,
                Summary  = BuildSummary(cfg)
            };

            if (Navigation?.NavigationStack?.Count > 1)
            {
                _tcs.TrySetResult(result);
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            _tcs.TrySetResult(result);
            await Navigation.PopAsync();
        }
    private string BuildSummary(EvocationConfig cfg)
    {
        // One-line summary for contribution list:
        var basic = cfg.BasicPerDay > 0 ? $"Cast x {cfg.BasicPerDay}/day" : null;
        var adv = cfg.AdvancedPerDay > 0 ? $"Cast x {cfg.AdvancedPerDay}/day" : null;
        var tags = new List<string?>(new[]{
            basic, adv,
            cfg.AddBasic ? $"add {cfg.EvocationName} to base list (B)" : null,
            cfg.AddAdvanced ? $"add {cfg.EvocationName} to base list (A)" : null,
            cfg.AddPrep ? $"add {cfg.EvocationName} to base list with 30s prep" : null
        }).Where(s => !string.IsNullOrWhiteSpace(s));

        var tagText = string.Join(",\n", tags);
        var name = string.IsNullOrWhiteSpace(cfg.EvocationName) ? "Evocation" : cfg.EvocationName;
        return $"{name}: {tagText} → {cfg.Total} ISP";
    }

    private async void OnSearchEvocation(object sender, EventArgs e)
    {
        var picked = await new Evocation().PickAsync(Navigation);
        if (picked == null) return;

        var cfg = (EvocationConfig)BindingContext;
        cfg.ApplyEvocation(picked);

    }

    private async void OnReturn(object sender, EventArgs e)
    {
        var cfg = (EvocationConfig)BindingContext;
        var res = new CalcResult
        {
            TotalIsp = cfg.Total,
            Summary = BuildSummary(cfg)
        };
        // Complete the task then pop
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
}
