using labyItems.Controls;
using labyItems.Models.Enums;

namespace labyItems.Pages.Calculator;

public partial class MpBasicPage : MpCalculatorPageBase
{
    public MpBasicPage()
    {
        InitializeComponent();
        InitializeCalculatorPage();
    }

    public bool ApprenticeShieldChecked
    {
        get => _apprenticeShieldChecked;
        set
        {
            if (SetProperty(ref _apprenticeShieldChecked, value))
            {
                if (value)
                {
                    ApprenticeType = "🛡️ Shield";
                    if (ApprenticeWeaponChecked)
                        ApprenticeWeaponChecked = false;
                }
                else if (!ApprenticeWeaponChecked)
                {
                    ApprenticeType = null;
                }
            }
        }
    }
    private bool _apprenticeShieldChecked;

    public bool ApprenticeWeaponChecked
    {
        get => _apprenticeWeaponChecked;
        set
        {
            if (SetProperty(ref _apprenticeWeaponChecked, value))
            {
                if (value)
                {
                    ApprenticeType = "🗡️ Weapon";
                    if (ApprenticeShieldChecked)
                        ApprenticeShieldChecked = false;
                }
                else if (!ApprenticeShieldChecked)
                {
                    ApprenticeType = null;
                }
            }
        }
    }
    private bool _apprenticeWeaponChecked;

    protected override DictionarySearchBar<SpellOption> SpellSearchControl => SpellSearch;
    protected override DictionarySearchBar<MiracleOption> MiracleSearchControl => MiracleSearch;
    protected override DictionarySearchBar<EvocationOption> EvocationSearchControl => EvocationSearch;
    protected override DictionarySearchBar<NeuroOption> NeuroSearchControl => NeuroSearch;
    protected override DictionarySlider LifeSliderControl => LifeSlider;
    protected override DictionarySearchBar<WeaponType> WeaponSearchControl => WeaponSearch;
}
