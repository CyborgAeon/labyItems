using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public partial class WeaponConfigPage : ConfigPageBase<WeaponConfig>
{
    public WeaponConfigPage()
    {
        InitializeComponent();
    }

    protected override CalcResult BuildResult(WeaponConfig cfg) => ConfigSummaryService.BuildWeaponResult(cfg);
}
