using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Calculator;

public partial class WeaponConfigPage : ConfigPageBase<WeaponConfig>
{
    public WeaponConfigPage()
    {
        ReturnFromConfigCommand = new Command(async () => await OnReturnWithContributionAsync());
        InitializeComponent();
        OnPropertyChanged(nameof(ReturnFromConfigCommand));
    }

    public event Action<CalcContribution>? ContributionAdded;
    protected override CalcResult BuildResult(WeaponConfig cfg) => ConfigSummaryService.BuildWeaponResult(cfg);
    private async Task OnReturnWithContributionAsync()
    {
        var result = BuildResult(Config);
        ContributionAdded?.Invoke(
            new CalcContribution(
                Id: "weapon",
                Source: "Weapon",
                Result: result,
                OnRemove: ResetConfig
            )
        );

        if (CalculatorContext?.ReturnToFormCommand?.CanExecute(null) == true)
        {
            CalculatorContext.ReturnToFormCommand.Execute(null);
            return;
        }

        // Fallback: only used if somehow not inside the calculator
        await StickyFooterControl.DefaultNavigateAsync(this);
    }
}
