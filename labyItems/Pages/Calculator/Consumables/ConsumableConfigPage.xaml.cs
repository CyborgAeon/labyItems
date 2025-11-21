using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
namespace labyItems.Pages.Calculator;

public partial class ConsumableConfigPage : ConfigPageBase<ConsumableConfig>
{
    public ConsumableConfigPage()
    {
        InitializeComponent();
    }

 private async void OnFooterReturnClicked(object sender, EventArgs e)
        {
            if (BindingContext is not ConsumableConfig cfg) return;

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

    public ConsumableConfigPage(ConsumableType presetType, int initialFocussingCrystals = 0)
    {
        InitializeComponent();
        Config.Type = presetType;
        if (presetType == ConsumableType.None && initialFocussingCrystals > 0)
            Config.FocussingCrystals = initialFocussingCrystals; // for “Create Neuro crystal”

        // Preselect the picker (still overridable by user)
        TypePicker.SelectedIndex = MapTypeToIndex(presetType);
    }

    private static int MapTypeToIndex(ConsumableType t) => t switch
    {
        ConsumableType.BatchOfPotions => 1,
        ConsumableType.MagicalScroll  => 2,
        ConsumableType.SpiritualScroll=> 3,
        ConsumableType.DruidicTalisman=> 4,
        ConsumableType.NeuronicShard  => 5,
        _ => 0
    };

    private static ConsumableType MapIndexToType(int i) => i switch
    {
        1 => ConsumableType.BatchOfPotions,
        2 => ConsumableType.MagicalScroll,
        3 => ConsumableType.SpiritualScroll,
        4 => ConsumableType.DruidicTalisman,
        5 => ConsumableType.NeuronicShard,
        _ => ConsumableType.None
    };

    private void OnTypeChanged(object sender, EventArgs e)
    {
        var cfg = (ConsumableConfig)BindingContext;
        cfg.Type = MapIndexToType(TypePicker.SelectedIndex);
    }

    private async void OnSearch(object sender, EventArgs e)
    {
        var cfg = (ConsumableConfig)BindingContext;
        var entry = await ConsumableService.PickAsync(Navigation, cfg.Type);
        if (entry is null) return;
        cfg.ApplyEntry(entry);
    }

    protected override string BuildSummary(ConsumableConfig cfg)
        => $"{cfg.SelectedText} → {cfg.Total} ISP";

    private async void OnReturn(object sender, EventArgs e)
    {
        var cfg = (ConsumableConfig)BindingContext;
        var res = new CalcResult { TotalIsp = cfg.Total, Summary = BuildSummary(cfg) };
        _tcs.TrySetResult(res);
        await Navigation.PopAsync();
    }
}
