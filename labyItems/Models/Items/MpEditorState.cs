using labyItems.Models.Enums;

namespace labyItems.Models.Items;

public sealed class MpEditorState
{
    public string CalculatorType { get; set; } = "MpBasicPage";
    public int LifeIndex { get; set; }
    public int SpellCount { get; set; }
    public int MiracleCount { get; set; }
    public int EvocationCount { get; set; }
    public int ActiveNeuroCount { get; set; }
    public int PassiveNeuroCount { get; set; }
    public int RepelGoodEvilCount { get; set; }
    public string? RepelGoodEvilTarget { get; set; }
    public int RepelLifeCount { get; set; }
    public bool PacChecked { get; set; }
    public bool DacChecked { get; set; }
    public bool MacChecked { get; set; }
    public bool SacChecked { get; set; }
    public string? ApprenticeType { get; set; }
    public WeaponType? SelectedWeapon { get; set; }
    public bool Mac2Checked { get; set; }
    public bool Sac2Checked { get; set; }
    public string? SelectedWeaponPowerType { get; set; }
    public WeaponType? SelectedWeaponType { get; set; }
    public MagicColours? SelectedWeaponColour { get; set; }
    public Alignments? SelectedWeaponAlignment { get; set; }
    public bool WeaponApprenticeStatus { get; set; }
    public string? SelectedBagType { get; set; }
    public string? SelectedWeaponKind { get; set; }
    public string? SelectedShieldType { get; set; }
    public MagicColours? SelectedShieldColour { get; set; }
    public Alignments? SelectedShieldAlignment { get; set; }
    public int SpiritPowerStorePoints { get; set; }
    public SpiritualSpheres? SelectedSpiritSphere { get; set; }
    public int MagicPowerStorePoints { get; set; }
    public MagicColours? SelectedMagicStoreColour { get; set; }
    public int EarthpowerStorePoints { get; set; }
    public EvocationFields? SelectedEarthpowerField { get; set; }
    public bool ShieldApprenticeStatus { get; set; }
    public string? SelectedCpOption { get; set; }
    public LocationOption? SelectedCpLocation { get; set; }
    public string? CpLocationText { get; set; }
    public List<MpEditorSelection<SpellOption>> SpellSelections { get; set; } = new();
    public List<MpEditorSelection<MiracleOption>> MiracleSelections { get; set; } = new();
    public List<MpEditorSelection<EvocationOption>> EvocationSelections { get; set; } = new();
    public List<MpEditorSelection<NeuroOption>> NeuroSelections { get; set; } = new();
}
