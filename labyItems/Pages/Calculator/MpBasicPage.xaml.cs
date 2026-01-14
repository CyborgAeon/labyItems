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

    protected override DictionarySearchBar<SpellOption> SpellSearchControl => SpellSearch;
    protected override DictionarySearchBar<MiracleOption> MiracleSearchControl => MiracleSearch;
    protected override DictionarySearchBar<EvocationOption> EvocationSearchControl => EvocationSearch;
    protected override DictionarySlider LifeSliderControl => LifeSlider;
    protected override DictionarySearchBar<WeaponType> WeaponSearchControl => WeaponSearch;
}
