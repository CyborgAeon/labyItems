using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using labyItems.Infrastructure;
using labyItems.Models;
using labyItems.Models.Characters;
using Microsoft.Maui.Graphics;
using labyItems.Services;
using labyItems.Services.Specialisations;

namespace labyItems.Pages.Makes;

public sealed class MakeSheetViewModel : ObservableObject
{
    private const string TabBuild = "Build";
    private const string TabConsequences = "Consequences";
    private const string TabCritical = "Critical";

    private const string DisciplineMagical = "Magical";
    private const string DisciplineSpiritual = "Spiritual";
    private const string DisciplineEarthpower = "Earthpower";
    private const string DisciplineNeuronic = "Neuronic";
    private const string DisciplineSmithed = "Smithed";

    private const string ItemWeapons = "Weapons";
    private const string ItemArmour = "Armour";
    private const string ItemArtefactMagical = "Artefacts (Spells)";
    private const string ItemArtefactSpiritual = "Artefacts (Miracles)";
    private const string ItemTeaching = "Teaching Scrolls";
    private const string ItemScriptures = "Scriptures of the Faith";
    private const string ItemCharms = "Charms (Evocations)";
    private const string ItemPrisms = "Prisms";
    private const string ItemTorques = "Torques";
    private const string ItemShieldsAndOther = "Shields and Other Goods";

    private const string SmithedQualityApprentice = "Apprentice";
    private const string SmithedQualityJourneymen = "Journeymen";
    private const string SmithedQualityMaster = "Master";

    private const string BonusModeAutomated = "Automated";
    private const string BonusModeManual = "Manual";
    private const string EffectCategoryStandard = "Standard";
    private const string EffectCategoryAdvanced = "Advanced";
    private const string EffectCategoryNonStandard = "Non-standard";

    private const string ChoiceCraftWizard = "choice.make.craft-speciality.wizard.type";
    private const string ChoiceCraftSpiritual = "choice.make.craft-speciality.spiritual.type";
    private const string ChoiceCraftWarrior = "choice.make.craft-speciality.warrior.type";
    private const string ChoiceCraftNeuronic = "choice.make.craft-speciality.neuronic.type";
    private const string ChoiceSmithType = "choice.make.smith.type";

    private const string McMagicalArtisanRefPrefix = "ability.mc.magical-artisan";
    private const string McMagicalCalligrapherRefPrefix = "ability.mc.magical-calligrapher";
    private const string McSpiritualArtisanRefPrefix = "ability.mc.spiritual-artisan";
    private const string McSpiritualCalligrapherRefPrefix = "ability.mc.spiritual-calligrapher";
    private const string McNaturalArtisanRefPrefix = "ability.mc.natural-artisan";
    private const string McNeuronicArtisanRefPrefix = "ability.mc.artisan-of-the-mind";
    private const string McSmithRefPrefix = "ability.mc.smith";

    private readonly Character _character;
    private readonly CharacterDraft _draft;
    private readonly bool _isDraftingMode;
    private readonly List<OwnedAbilityEntry> _ownedAbilities = new();
    private readonly HashSet<string> _ownedAbilityDedupes = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoaded;

    private string _selectedTab = TabBuild;
    private string _selectedDiscipline = DisciplineMagical;
    private string _selectedItemType = ItemArtefactMagical;
    private string _selectedBonusMode = BonusModeAutomated;
    private string _selectedWeaponTier = "+0";
    private string _selectedArmourWeight = "Light";
    private string _selectedArmourEnhancement = "+0";
    private string _selectedSmithedQuality = SmithedQualityApprentice;
    private string _selectedSmithedWeaponEnhancement = "+0";
    private string _selectedSmithedArmourEnhancement = "+0";
    private string _selectedTorqueNacOption = "None";
    private int _smithedArmourAc = 1;
    private int _smithedTemplateCount = 1;
    private bool _isColouredOrAligned;
    private bool _smithedArmourWellFitted;
    private bool _smithedWeaponDifferentMaterial;
    private bool _smithedUseTemplates;
    private bool _torqueFocusMind;
    private bool _torquePsionicRetard;
    private bool _useTrinketMode;
    private bool _useReciprocatesBinding;
    private bool _showEffectSearch;
    private string _newEffectName = string.Empty;
    private string _newEffectPower = "1";
    private string _newEffectUses = "1";
    private string _newEffectCategory = EffectCategoryStandard;
    private string _effectSearchPlaceholder = "Search spell";
    private string _newManualBonusReason = string.Empty;
    private string _newManualBonusPercent = "0";
    private int _baseChancePercent = 90;
    private int _classBonusPercent;
    private int _bonusPercent;
    private int _difficultyPercent;
    private int _finalChancePercent;
    private int _totalRolls;
    private int _setupCostGrulls;
    private int _rollCostGrulls;
    private int _totalCostGrulls;
    private string _overallSuccessChanceText = "0.00%";
    private bool _canUseTrinketMode;
    private bool _canUseReciprocatesBinding;
    private string _trinketModeLabel = "Trinket mode available.";
    private MakeEffectLookupOption? _selectedEffectLookup;
    private Dictionary<string, MakeEffectLookupOption> _effectLookupOptions = new(StringComparer.OrdinalIgnoreCase);
    private int _effectLookupRequestId;
    private Color _itemSetupBorderColor = Color.FromArgb("#D1D5DB");
    private Color _itemSetupBackgroundColor = Color.FromArgb("#FFFFFF");
    private string _itemSetupStatusText = string.Empty;
    private bool _itemSetupHasWarning;

    public MakeSheetViewModel()
        : this(new Character { Name = "Crafting Draft" }, isDraftingMode: true)
    {
    }

    public MakeSheetViewModel(Character character)
        : this(character, isDraftingMode: false)
    {
    }

    private MakeSheetViewModel(Character character, bool isDraftingMode)
    {
        _character = character ?? throw new ArgumentNullException(nameof(character));
        _isDraftingMode = isDraftingMode;
        _draft = isDraftingMode
            ? new CharacterDraft()
            : LiteDbService.ToDraft(character) ?? new CharacterDraft();
        _selectedBonusMode = isDraftingMode ? BonusModeManual : BonusModeAutomated;

        PlayerName = _character.PlayerName ?? string.Empty;
        CharacterName = _character.Name ?? string.Empty;
        CharacterClass = _character.Class ?? string.Empty;

        Disciplines = new ObservableCollection<string>
        {
            DisciplineMagical,
            DisciplineSpiritual,
            DisciplineEarthpower,
            DisciplineNeuronic,
            DisciplineSmithed
        };

        BonusModes = new ObservableCollection<string>(
            isDraftingMode
                ? new[] { BonusModeManual }
                : new[] { BonusModeAutomated, BonusModeManual });

        WeaponTierOptions = new ObservableCollection<string>
        {
            "+0",
            "+1",
            "+2"
        };

        ArmourWeightOptions = new ObservableCollection<string>
        {
            "Light",
            "Medium",
            "Heavy"
        };

        ArmourEnhancementOptions = new ObservableCollection<string>
        {
            "+0",
            "+1",
            "+2"
        };

        SmithedQualityOptions = new ObservableCollection<string>
        {
            SmithedQualityApprentice,
            SmithedQualityJourneymen,
            SmithedQualityMaster
        };

        SmithedWeaponEnhancementOptions = new ObservableCollection<string>
        {
            "+0",
            "+1",
            "+2"
        };

        SmithedArmourEnhancementOptions = new ObservableCollection<string>
        {
            "+0",
            "+1",
            "+2"
        };

        TorqueNacOptions = new ObservableCollection<string>
        {
            "None",
            "+1 NAC",
            "+2 NAC"
        };

        ItemTypes = new ObservableCollection<string>();
        EffectCategoryOptions = new ObservableCollection<string>();
        EffectCategoryDisabledOptions = new ObservableCollection<string>();
        EffectRows = new ObservableCollection<MakeEffectRowVm>();
        ManualBonusRows = new ObservableCollection<MakeBonusRowVm>();
        AutomatedBonusLines = new ObservableCollection<string>();
        RequirementLines = new ObservableCollection<string>();
        OwnedAbilityLines = new ObservableCollection<string>();
        ResultRows = new ObservableCollection<MakeResultRowVm>();

        FailureRows = new ObservableCollection<MakeTableRowVm>(BuildFailureRows());
        CriticalFailureRows = new ObservableCollection<MakeTableRowVm>(BuildCriticalFailureRows());

        RebuildItemTypes();
        RebuildEffectCategoryOptions();
        Recalculate();
    }

    public string PlayerName { get; }
    public string CharacterName { get; }
    public string CharacterClass { get; }
    public CharacterDraft Draft => _draft;
    public bool IsDraftingMode => _isDraftingMode;
    public bool HasCharacterContext => !IsDraftingMode;
    public bool ShowOwnedAbilitySummary => HasCharacterContext;
    public string PageTitle => IsDraftingMode ? "Crafting" : "Make Sheet";
    public string BonusSectionTitle => IsDraftingMode ? "Abilities" : "Bonus Mode";
    public bool ShowBonusModePicker => !IsDraftingMode;

    public ObservableCollection<string> Disciplines { get; }
    public ObservableCollection<string> ItemTypes { get; }
    public ObservableCollection<string> BonusModes { get; }
    public ObservableCollection<string> WeaponTierOptions { get; }
    public ObservableCollection<string> ArmourWeightOptions { get; }
    public ObservableCollection<string> ArmourEnhancementOptions { get; }
    public ObservableCollection<string> SmithedQualityOptions { get; }
    public ObservableCollection<string> SmithedWeaponEnhancementOptions { get; }
    public ObservableCollection<string> SmithedArmourEnhancementOptions { get; }
    public ObservableCollection<string> TorqueNacOptions { get; }
    public ObservableCollection<string> EffectCategoryOptions { get; }
    public ObservableCollection<string> EffectCategoryDisabledOptions { get; }

    public ObservableCollection<MakeEffectRowVm> EffectRows { get; }
    public ObservableCollection<MakeBonusRowVm> ManualBonusRows { get; }
    public ObservableCollection<string> AutomatedBonusLines { get; }
    public ObservableCollection<string> RequirementLines { get; }
    public ObservableCollection<string> OwnedAbilityLines { get; }
    public ObservableCollection<MakeResultRowVm> ResultRows { get; }

    public ObservableCollection<MakeTableRowVm> FailureRows { get; }
    public ObservableCollection<MakeTableRowVm> CriticalFailureRows { get; }

    public bool ShowBuildTab => string.Equals(SelectedTab, TabBuild, StringComparison.OrdinalIgnoreCase);
    public bool ShowConsequencesTab => string.Equals(SelectedTab, TabConsequences, StringComparison.OrdinalIgnoreCase);
    public bool ShowCriticalTab => string.Equals(SelectedTab, TabCritical, StringComparison.OrdinalIgnoreCase);

    public string SelectedTab
    {
        get => _selectedTab;
        private set
        {
            if (!SetProperty(ref _selectedTab, value))
                return;

            Raise(nameof(ShowBuildTab));
            Raise(nameof(ShowConsequencesTab));
            Raise(nameof(ShowCriticalTab));
        }
    }

    public string SelectedDiscipline
    {
        get => _selectedDiscipline;
        set
        {
            var normalized = NormalizeSelection(value, _selectedDiscipline);
            if (!SetProperty(ref _selectedDiscipline, normalized))
                return;

            RebuildItemTypes();
            RebuildEffectCategoryOptions();
            RaisePathVisibilityProperties();
            _ = RefreshEffectLookupAsync();
            Recalculate();
        }
    }

    public string SelectedItemType
    {
        get => _selectedItemType;
        set
        {
            var normalized = NormalizeSelection(value, _selectedItemType);
            if (!SetProperty(ref _selectedItemType, normalized))
                return;

            RebuildEffectCategoryOptions();
            RaisePathVisibilityProperties();
            _ = RefreshEffectLookupAsync();
            Recalculate();
        }
    }

    public string SelectedBonusMode
    {
        get => _selectedBonusMode;
        set
        {
            var normalized = NormalizeSelection(value, _selectedBonusMode);
            if (IsDraftingMode)
                normalized = BonusModeManual;

            if (!SetProperty(ref _selectedBonusMode, normalized))
                return;

            Raise(nameof(ShowAutomatedBonusMode));
            Raise(nameof(ShowManualBonusMode));
            Recalculate();
        }
    }

    public string SelectedWeaponTier
    {
        get => _selectedWeaponTier;
        set
        {
            var normalized = NormalizeSelection(value, _selectedWeaponTier);
            if (!SetProperty(ref _selectedWeaponTier, normalized))
                return;

            Recalculate();
        }
    }

    public string SelectedArmourWeight
    {
        get => _selectedArmourWeight;
        set
        {
            var normalized = NormalizeSelection(value, _selectedArmourWeight);
            if (!SetProperty(ref _selectedArmourWeight, normalized))
                return;

            Recalculate();
        }
    }

    public string SelectedArmourEnhancement
    {
        get => _selectedArmourEnhancement;
        set
        {
            var normalized = NormalizeSelection(value, _selectedArmourEnhancement);
            if (!SetProperty(ref _selectedArmourEnhancement, normalized))
                return;

            Recalculate();
        }
    }

    public string SelectedSmithedQuality
    {
        get => _selectedSmithedQuality;
        set
        {
            var normalized = NormalizeSelection(value, _selectedSmithedQuality);
            if (!SetProperty(ref _selectedSmithedQuality, normalized))
                return;

            Recalculate();
        }
    }

    public string SelectedSmithedWeaponEnhancement
    {
        get => _selectedSmithedWeaponEnhancement;
        set
        {
            var normalized = NormalizeSelection(value, _selectedSmithedWeaponEnhancement);
            if (!SetProperty(ref _selectedSmithedWeaponEnhancement, normalized))
                return;

            Recalculate();
        }
    }

    public string SelectedSmithedArmourEnhancement
    {
        get => _selectedSmithedArmourEnhancement;
        set
        {
            var normalized = NormalizeSelection(value, _selectedSmithedArmourEnhancement);
            if (!SetProperty(ref _selectedSmithedArmourEnhancement, normalized))
                return;

            Recalculate();
        }
    }

    public int SmithedArmourAc
    {
        get => _smithedArmourAc;
        set
        {
            var normalized = Math.Clamp(value, 1, 100);
            if (!SetProperty(ref _smithedArmourAc, normalized))
                return;

            Recalculate();
        }
    }

    public bool SmithedArmourWellFitted
    {
        get => _smithedArmourWellFitted;
        set
        {
            if (!SetProperty(ref _smithedArmourWellFitted, value))
                return;

            Recalculate();
        }
    }

    public bool SmithedWeaponDifferentMaterial
    {
        get => _smithedWeaponDifferentMaterial;
        set
        {
            if (!SetProperty(ref _smithedWeaponDifferentMaterial, value))
                return;

            Recalculate();
        }
    }

    public bool SmithedUseTemplates
    {
        get => _smithedUseTemplates;
        set
        {
            if (!SetProperty(ref _smithedUseTemplates, value))
                return;

            RaisePathVisibilityProperties();
            Recalculate();
        }
    }

    public int SmithedTemplateCount
    {
        get => _smithedTemplateCount;
        set
        {
            var normalized = Math.Clamp(value, 1, 20);
            if (!SetProperty(ref _smithedTemplateCount, normalized))
                return;

            Recalculate();
        }
    }

    public bool TorqueFocusMind
    {
        get => _torqueFocusMind;
        set
        {
            if (!SetProperty(ref _torqueFocusMind, value))
                return;

            Recalculate();
        }
    }

    public string SelectedTorqueNacOption
    {
        get => _selectedTorqueNacOption;
        set
        {
            var normalized = NormalizeSelection(value, _selectedTorqueNacOption);
            if (!SetProperty(ref _selectedTorqueNacOption, normalized))
                return;

            Recalculate();
        }
    }

    public bool TorquePsionicRetard
    {
        get => _torquePsionicRetard;
        set
        {
            if (!SetProperty(ref _torquePsionicRetard, value))
                return;

            Recalculate();
        }
    }

    public bool IsColouredOrAligned
    {
        get => _isColouredOrAligned;
        set
        {
            if (!SetProperty(ref _isColouredOrAligned, value))
                return;

            Recalculate();
        }
    }

    public bool UseTrinketMode
    {
        get => _useTrinketMode;
        set
        {
            if (!SetProperty(ref _useTrinketMode, value))
                return;

            Recalculate();
        }
    }

    public bool UseReciprocatesBinding
    {
        get => _useReciprocatesBinding;
        set
        {
            if (!SetProperty(ref _useReciprocatesBinding, value))
                return;

            Recalculate();
        }
    }

    public string NewEffectName
    {
        get => _newEffectName;
        set => SetProperty(ref _newEffectName, value ?? string.Empty);
    }

    public string NewEffectPower
    {
        get => _newEffectPower;
        set => SetProperty(ref _newEffectPower, value ?? string.Empty);
    }

    public string NewEffectUses
    {
        get => _newEffectUses;
        set => SetProperty(ref _newEffectUses, value ?? string.Empty);
    }

    public string NewEffectCategory
    {
        get => _newEffectCategory;
        set
        {
            var normalized = NormalizeSelection(value, _newEffectCategory);
            if (!SetProperty(ref _newEffectCategory, normalized))
                return;

            _ = RefreshEffectLookupAsync();
        }
    }

    public string NewManualBonusReason
    {
        get => _newManualBonusReason;
        set => SetProperty(ref _newManualBonusReason, value ?? string.Empty);
    }

    public string NewManualBonusPercent
    {
        get => _newManualBonusPercent;
        set => SetProperty(ref _newManualBonusPercent, value ?? string.Empty);
    }

    public int BaseChancePercent
    {
        get => _baseChancePercent;
        set
        {
            var normalized = Math.Clamp(value, 0, 99);
            if (!SetProperty(ref _baseChancePercent, normalized))
                return;

            Recalculate();
        }
    }

    public int ClassBonusPercent
    {
        get => _classBonusPercent;
        private set => SetProperty(ref _classBonusPercent, value);
    }

    public int BonusPercent
    {
        get => _bonusPercent;
        private set => SetProperty(ref _bonusPercent, value);
    }

    public int DifficultyPercent
    {
        get => _difficultyPercent;
        private set => SetProperty(ref _difficultyPercent, value);
    }

    public int FinalChancePercent
    {
        get => _finalChancePercent;
        private set => SetProperty(ref _finalChancePercent, value);
    }

    public int TotalRolls
    {
        get => _totalRolls;
        private set => SetProperty(ref _totalRolls, value);
    }

    public int SetupCostGrulls
    {
        get => _setupCostGrulls;
        private set => SetProperty(ref _setupCostGrulls, value);
    }

    public int RollCostGrulls
    {
        get => _rollCostGrulls;
        private set => SetProperty(ref _rollCostGrulls, value);
    }

    public int TotalCostGrulls
    {
        get => _totalCostGrulls;
        private set => SetProperty(ref _totalCostGrulls, value);
    }

    public string OverallSuccessChanceText
    {
        get => _overallSuccessChanceText;
        private set => SetProperty(ref _overallSuccessChanceText, value);
    }

    public bool ShowAdvancedWeaponOptions
        => IsMagicOrSpiritualPath
           && string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase);

    public bool ShowAdvancedArmourOptions
        => IsMagicOrSpiritualPath
           && string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase);

    public bool ShowAlignmentOption => ShowAdvancedWeaponOptions || ShowAdvancedArmourOptions;

    public bool ShowNeuronicTorqueOptions
        => IsNeuronicPath && string.Equals(SelectedItemType, ItemTorques, StringComparison.OrdinalIgnoreCase);

    public bool ShowSmithedQualityOptions => IsSmithedPath && !SmithedUseTemplates;

    public bool ShowSmithedTemplateOptions => IsSmithedPath;

    public bool ShowSmithedTemplateCount => ShowSmithedTemplateOptions && SmithedUseTemplates;

    public bool ShowSmithedWeaponOptions
        => IsSmithedPath
           && !SmithedUseTemplates
           && string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase);

    public bool ShowSmithedArmourOptions
        => IsSmithedPath
           && !SmithedUseTemplates
           && string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase);

    public bool ShowEffectEditor => ItemUsesEffectBuilder();

    public bool ShowEffectSearch => ShowEffectEditor && _showEffectSearch;

    public bool ShowEffectNameEntry => ShowEffectEditor && !ShowEffectSearch;

    public bool ShowEffectUsesPerDay => ShowEffectEditor && !IsSingleEffectType();

    public int EffectPowerColumnSpan => ShowEffectUsesPerDay ? 1 : 2;

    public string EffectSearchPlaceholder
    {
        get => _effectSearchPlaceholder;
        private set => SetProperty(ref _effectSearchPlaceholder, value);
    }

    public Dictionary<string, MakeEffectLookupOption> EffectLookupOptions
    {
        get => _effectLookupOptions;
        private set => SetProperty(ref _effectLookupOptions, value);
    }

    public MakeEffectLookupOption? SelectedEffectLookup
    {
        get => _selectedEffectLookup;
        set
        {
            if (!SetProperty(ref _selectedEffectLookup, value))
                return;

            if (value != null)
            {
                NewEffectName = value.Name;
                NewEffectPower = Math.Max(1, value.BaseCost).ToString();
                if (IsSingleEffectType())
                    NewEffectUses = "1";
            }
        }
    }

    public bool ShowAutomatedBonusMode
        => string.Equals(SelectedBonusMode, BonusModeAutomated, StringComparison.OrdinalIgnoreCase);

    public bool ShowManualBonusMode
        => string.Equals(SelectedBonusMode, BonusModeManual, StringComparison.OrdinalIgnoreCase);

    public bool CanUseTrinketMode
    {
        get => _canUseTrinketMode;
        private set => SetProperty(ref _canUseTrinketMode, value);
    }

    public bool CanUseReciprocatesBinding
    {
        get => _canUseReciprocatesBinding;
        private set => SetProperty(ref _canUseReciprocatesBinding, value);
    }

    public string TrinketModeLabel
    {
        get => _trinketModeLabel;
        private set => SetProperty(ref _trinketModeLabel, value);
    }

    public Color ItemSetupBorderColor
    {
        get => _itemSetupBorderColor;
        private set => SetProperty(ref _itemSetupBorderColor, value);
    }

    public Color ItemSetupBackgroundColor
    {
        get => _itemSetupBackgroundColor;
        private set => SetProperty(ref _itemSetupBackgroundColor, value);
    }

    public string ItemSetupStatusText
    {
        get => _itemSetupStatusText;
        private set => SetProperty(ref _itemSetupStatusText, value);
    }

    public bool ItemSetupHasWarning
    {
        get => _itemSetupHasWarning;
        private set => SetProperty(ref _itemSetupHasWarning, value);
    }

    private bool IsMagicOrSpiritualPath
        => string.Equals(SelectedDiscipline, DisciplineMagical, StringComparison.OrdinalIgnoreCase)
           || string.Equals(SelectedDiscipline, DisciplineSpiritual, StringComparison.OrdinalIgnoreCase);

    private bool IsNeuronicPath
        => string.Equals(SelectedDiscipline, DisciplineNeuronic, StringComparison.OrdinalIgnoreCase);

    private bool IsSmithedPath
        => string.Equals(SelectedDiscipline, DisciplineSmithed, StringComparison.OrdinalIgnoreCase);

    public async Task EnsureLoadedAsync()
    {
        if (_isLoaded)
            return;

        _isLoaded = true;
        if (!IsDraftingMode)
            await LoadOwnedAbilitiesAsync();

        await RefreshEffectLookupAsync();
        Recalculate();
    }

    public void ActivateBuildTab() => SelectedTab = TabBuild;

    public void ActivateConsequencesTab() => SelectedTab = TabConsequences;

    public void ActivateCriticalTab() => SelectedTab = TabCritical;

    public string? AddEffect()
    {
        var lookupKind = GetEffectLookupKind();
        var requiresSearch = lookupKind != EffectLookupKind.None;
        var selectedLookup = SelectedEffectLookup;

        string name;
        if (requiresSearch)
        {
            if (selectedLookup == null)
            {
                return lookupKind switch
                {
                    EffectLookupKind.Spells => "Select a spell from search before adding.",
                    EffectLookupKind.Miracles => "Select a miracle from search before adding.",
                    EffectLookupKind.Evocations => "Select an evocation from search before adding.",
                    _ => "Select an effect from search before adding."
                };
            }

            name = selectedLookup.Name;
        }
        else
        {
            name = (NewEffectName ?? string.Empty).Trim();
            if (name.Length == 0)
                return "Effect name is required.";
        }

        var defaultPower = Math.Max(1, selectedLookup?.BaseCost ?? 1);
        if (!int.TryParse(NewEffectPower, out var power) || power <= 0)
        {
            if (requiresSearch)
                power = defaultPower;
            else
                return "Cost must be a positive integer.";
        }

        var usesEnabled = !IsSingleEffectType();
        var uses = 1;
        if (usesEnabled && (!int.TryParse(NewEffectUses, out uses) || uses <= 0))
            return "Uses/day must be a positive integer.";

        var category = (NewEffectCategory ?? string.Empty).Trim();
        if (category.Length == 0)
            category = EffectCategoryOptions.FirstOrDefault() ?? EffectCategoryStandard;

        if (IsSingleEffectType())
            EffectRows.Clear();

        EffectRows.Add(new MakeEffectRowVm(
            name: name,
            power: power,
            usesPerDay: uses,
            category: category,
            sourceType: selectedLookup?.SourceType ?? string.Empty,
            usesPerDayEnabled: usesEnabled));
        NewEffectName = string.Empty;
        NewEffectPower = "1";
        NewEffectUses = "1";
        if (requiresSearch)
            SelectedEffectLookup = null;
        Recalculate();
        return null;
    }

    public void RemoveEffect(MakeEffectRowVm? row)
    {
        if (row == null)
            return;

        EffectRows.Remove(row);
        Recalculate();
    }

    public void EditEffect(MakeEffectRowVm? row)
    {
        if (row == null)
            return;

        if (EffectRows.Contains(row))
            EffectRows.Remove(row);

        NewEffectName = row.Name;
        NewEffectPower = Math.Max(1, row.Power).ToString();
        NewEffectUses = Math.Max(1, row.UsesPerDay).ToString();

        var incomingCategory = (row.Category ?? string.Empty).Trim();
        if (incomingCategory.Length > 0
            && EffectCategoryOptions.Any(option => option.Equals(incomingCategory, StringComparison.OrdinalIgnoreCase)))
        {
            NewEffectCategory = incomingCategory;
        }

        var matchedLookup = EffectLookupOptions.Values.FirstOrDefault(option =>
            option.Name.Equals(row.Name, StringComparison.OrdinalIgnoreCase)
            && (row.SourceType.Length == 0 || option.SourceType.Equals(row.SourceType, StringComparison.OrdinalIgnoreCase)));
        SelectedEffectLookup = matchedLookup;
        NewEffectPower = Math.Max(1, row.Power).ToString();
        NewEffectUses = Math.Max(1, row.UsesPerDay).ToString();

        Recalculate();
    }

    public async Task<SpellService.SpellRaw?> FindSpellByNameAsync(string? spellName)
    {
        var name = (spellName ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        var all = await SpellService.GetAllAsync();
        return all.FirstOrDefault(spell =>
            string.Equals((spell?.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<MiracleService.MiracRaw?> FindMiracleByNameAsync(string? miracleName)
    {
        var name = (miracleName ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        var all = await MiracleService.GetAllAsync();
        return all.FirstOrDefault(miracle =>
            string.Equals((miracle?.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DruidEvocationService.EvocRaw?> FindEvocationByNameAsync(string? evocationName)
    {
        var name = (evocationName ?? string.Empty).Trim();
        if (name.Length == 0)
            return null;

        var all = await DruidEvocationService.GetAllAsync();
        return all.FirstOrDefault(evoc =>
            string.Equals((evoc?.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    public string? AddManualBonus()
    {
        var reason = (NewManualBonusReason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return "Bonus reason is required.";

        if (!int.TryParse(NewManualBonusPercent, out var percent))
            return "Bonus % must be an integer (e.g. 2 or -5).";

        ManualBonusRows.Add(new MakeBonusRowVm(reason, percent));
        NewManualBonusReason = string.Empty;
        NewManualBonusPercent = "0";
        Recalculate();
        return null;
    }

    public void RemoveManualBonus(MakeBonusRowVm? row)
    {
        if (row == null)
            return;

        ManualBonusRows.Remove(row);
        Recalculate();
    }

    public void AddManualBonusFromAbility(
        string? abilityName,
        string? description,
        EvolutionService.AbilityResult? ability = null,
        IReadOnlyList<string>? choiceSetRefs = null)
    {
        var name = (abilityName ?? "Ability").Trim();
        var delta = ExtractFirstPercent(description ?? string.Empty);
        if (name.Length == 0)
            name = "Ability";

        var resolvedAbility = ability ?? new EvolutionService.AbilityResult
        {
            Index = name,
            Description = description ?? string.Empty
        };
        var resolvedChoiceSetRefs = NormalizeChoiceSetRefs(choiceSetRefs ?? resolvedAbility.ChoiceSetRefs);

        ManualBonusRows.Add(new MakeBonusRowVm(
            name,
            delta,
            resolvedAbility,
            resolvedChoiceSetRefs));
        Recalculate();
    }

    public async Task<IReadOnlyList<MakeBonusChoiceSetEditorVm>> GetManualBonusChoiceSetEditorsAsync(MakeBonusRowVm? row)
    {
        if (row == null || !row.CanEditSpecialisation)
            return Array.Empty<MakeBonusChoiceSetEditorVm>();

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        if (index.ChoiceSetTemplates.Count == 0)
            return Array.Empty<MakeBonusChoiceSetEditorVm>();

        var editors = new List<MakeBonusChoiceSetEditorVm>();
        foreach (var choiceSetRef in row.ChoiceSetRefs)
        {
            var trimmedRef = (choiceSetRef ?? string.Empty).Trim();
            if (trimmedRef.Length == 0)
                continue;

            if (!index.ChoiceSetTemplates.TryGetValue(trimmedRef, out var template))
            {
                continue;
            }

            var options = (template.Options ?? Array.Empty<ChoiceOption>())
                .Select(option => new MakeBonusChoiceSetOptionVm(
                    ResolveOptionKey(option),
                    ResolveOptionLabel(option)))
                .Where(option => option.IsValid)
                .GroupBy(option => option.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (options.Count == 0)
                continue;

            var title = (template.Title ?? string.Empty).Trim();
            if (title.Length == 0)
                title = trimmedRef;

            var selectedKey = row.GetSelectedSpecialisationKey(trimmedRef);
            editors.Add(new MakeBonusChoiceSetEditorVm(
                trimmedRef,
                title,
                options,
                selectedKey));
        }

        return editors;
    }

    public void SetManualBonusSpecialisation(
        MakeBonusRowVm? row,
        string? choiceSetRef,
        string? choiceSetTitle,
        string? optionKey,
        string? optionLabel)
    {
        if (row == null)
            return;

        row.SetSpecialisation(
            (choiceSetRef ?? string.Empty).Trim(),
            (choiceSetTitle ?? string.Empty).Trim(),
            (optionKey ?? string.Empty).Trim(),
            (optionLabel ?? string.Empty).Trim());
    }

    public static int ExtractFirstPercent(string text)
    {
        var match = Regex.Match(text ?? string.Empty, @"([+-]?\d+)\s*%");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
            return parsed;

        return 0;
    }

    private static IReadOnlyList<string> NormalizeChoiceSetRefs(IEnumerable<string>? refs)
    {
        if (refs == null)
            return Array.Empty<string>();

        return refs
            .Select(entry => (entry ?? string.Empty).Trim())
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveOptionKey(ChoiceOption option)
        => (option?.Key ?? option?.Label ?? string.Empty).Trim();

    private static string ResolveOptionLabel(ChoiceOption option)
        => (option?.Label ?? option?.Key ?? string.Empty).Trim();

    public string BuildExportText()
    {
        var builder = new StringBuilder();

        builder.AppendLine("Make Sheet Export");
        builder.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        builder.AppendLine();
        if (HasCharacterContext)
        {
            builder.AppendLine($"Character: {CharacterName}");
            builder.AppendLine($"Player: {PlayerName}");
            builder.AppendLine($"Class: {CharacterClass}");
            builder.AppendLine();
        }

        builder.AppendLine("Build");
        builder.AppendLine($"Discipline: {SelectedDiscipline}");
        builder.AppendLine($"Item Type: {SelectedItemType}");
        builder.AppendLine($"Base Chance: {BaseChancePercent}%");

        if (ShowAdvancedWeaponOptions)
            builder.AppendLine($"Weapon Tier: {SelectedWeaponTier}");
        if (ShowAdvancedArmourOptions)
        {
            builder.AppendLine($"Armour Weight: {SelectedArmourWeight}");
            builder.AppendLine($"Armour Enhancement: {SelectedArmourEnhancement}");
        }
        if (ShowAlignmentOption)
            builder.AppendLine($"Coloured/Aligned: {(IsColouredOrAligned ? "Yes" : "No")}");

        if (ShowNeuronicTorqueOptions)
        {
            builder.AppendLine($"Focus the Mind: {(TorqueFocusMind ? "Yes" : "No")}");
            builder.AppendLine($"Shield the Mind (NAC): {SelectedTorqueNacOption}");
            builder.AppendLine($"Psionic Retard: {(TorquePsionicRetard ? "Yes" : "No")}");
        }

        if (IsSmithedPath)
        {
            builder.AppendLine($"Using Templates: {(SmithedUseTemplates ? "Yes" : "No")}");
            if (SmithedUseTemplates)
                builder.AppendLine($"Template Count: {SmithedTemplateCount}");
            else
                builder.AppendLine($"Quality: {SelectedSmithedQuality}");
            if (ShowSmithedWeaponOptions)
            {
                builder.AppendLine($"Weapon Enhancement: {SelectedSmithedWeaponEnhancement}");
                builder.AppendLine($"Different Material: {(SmithedWeaponDifferentMaterial ? "Yes" : "No")}");
            }
            if (ShowSmithedArmourOptions)
            {
                builder.AppendLine($"Armour AC: {SmithedArmourAc}");
                builder.AppendLine($"Armour Enhancement: {SelectedSmithedArmourEnhancement}");
                builder.AppendLine($"Well Fitted: {(SmithedArmourWellFitted ? "Yes" : "No")}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Effects");
        if (EffectRows.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var row in EffectRows)
                builder.AppendLine($"- {row.DisplayText}");
        }

        builder.AppendLine();
        if (ShowBonusModePicker)
            builder.AppendLine($"Bonus Mode: {SelectedBonusMode}");
        builder.AppendLine(BonusSectionTitle);
        if (ShowAutomatedBonusMode)
        {
            foreach (var line in AutomatedBonusLines)
                builder.AppendLine($"- {line}");
        }
        else if (ManualBonusRows.Count > 0)
        {
            foreach (var row in ManualBonusRows)
                builder.AppendLine($"- {row.DisplayText}");
        }
        else
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
        builder.AppendLine("Results");
        builder.AppendLine($"Class Bonus: {FormatSignedPercent(ClassBonusPercent)}");
        builder.AppendLine($"Mode Bonus: {FormatSignedPercent(BonusPercent)}");
        builder.AppendLine($"Difficulty: -{DifficultyPercent}%");
        builder.AppendLine($"Final Roll Chance: {FinalChancePercent}%");
        builder.AppendLine($"All-roll Completion: {OverallSuccessChanceText}");
        builder.AppendLine($"Target Rolls: {TotalRolls}");
        builder.AppendLine($"Setup Cost: {SetupCostGrulls} grulls");
        builder.AppendLine($"Cost Per Roll: {RollCostGrulls} grulls");
        builder.AppendLine($"Total Cost: {TotalCostGrulls} grulls");

        builder.AppendLine();
        builder.AppendLine("Requirements / Notes");
        foreach (var line in RequirementLines)
            builder.AppendLine($"- {line}");

        if (ShowOwnedAbilitySummary)
        {
            builder.AppendLine();
            builder.AppendLine("Detected Character / Item Abilities");
            foreach (var line in OwnedAbilityLines)
                builder.AppendLine($"- {line}");
        }

        builder.AppendLine();
        builder.AppendLine("Failure Table");
        foreach (var row in FailureRows)
            builder.AppendLine($"- {row.RollRange}: {row.Outcome}");

        builder.AppendLine();
        builder.AppendLine("Catastrophic Failure Table");
        foreach (var row in CriticalFailureRows)
            builder.AppendLine($"- {row.RollRange}: {row.Outcome}");

        return builder.ToString().TrimEnd();
    }

    private void RebuildItemTypes()
    {
        ItemTypes.Clear();
        switch (SelectedDiscipline)
        {
            case DisciplineSpiritual:
                ItemTypes.Add(ItemArtefactSpiritual);
                ItemTypes.Add(ItemWeapons);
                ItemTypes.Add(ItemArmour);
                ItemTypes.Add(ItemScriptures);
                break;
            case DisciplineEarthpower:
                ItemTypes.Add(ItemCharms);
                break;
            case DisciplineNeuronic:
                ItemTypes.Add(ItemPrisms);
                ItemTypes.Add(ItemTorques);
                break;
            case DisciplineSmithed:
                ItemTypes.Add(ItemWeapons);
                ItemTypes.Add(ItemArmour);
                ItemTypes.Add(ItemShieldsAndOther);
                break;
            default:
                ItemTypes.Add(ItemArtefactMagical);
                ItemTypes.Add(ItemWeapons);
                ItemTypes.Add(ItemArmour);
                ItemTypes.Add(ItemTeaching);
                break;
        }

        if (!ItemTypes.Contains(SelectedItemType, StringComparer.OrdinalIgnoreCase))
            SelectedItemType = ItemTypes.FirstOrDefault() ?? string.Empty;

        RaisePathVisibilityProperties();
    }

    private void RebuildEffectCategoryOptions()
    {
        EffectCategoryOptions.Clear();
        EffectCategoryDisabledOptions.Clear();

        if (string.Equals(SelectedItemType, ItemTeaching, StringComparison.OrdinalIgnoreCase))
        {
            EffectCategoryOptions.Add(EffectCategoryStandard);
            EffectCategoryOptions.Add(EffectCategoryAdvanced);
            EffectCategoryOptions.Add(EffectCategoryNonStandard);
            EffectCategoryDisabledOptions.Add(EffectCategoryNonStandard);
        }
        else if (string.Equals(SelectedItemType, ItemScriptures, StringComparison.OrdinalIgnoreCase))
        {
            EffectCategoryOptions.Add(EffectCategoryStandard);
            EffectCategoryOptions.Add(EffectCategoryAdvanced);
            EffectCategoryOptions.Add(EffectCategoryNonStandard);
            EffectCategoryDisabledOptions.Add(EffectCategoryNonStandard);
        }
        else
        {
            EffectCategoryOptions.Add(EffectCategoryStandard);
        }

        var hasCurrent = EffectCategoryOptions.Contains(NewEffectCategory, StringComparer.OrdinalIgnoreCase);
        var isCurrentDisabled = EffectCategoryDisabledOptions.Any(option =>
            option.Equals(NewEffectCategory, StringComparison.OrdinalIgnoreCase));
        if (!hasCurrent || isCurrentDisabled)
        {
            NewEffectCategory = EffectCategoryOptions.FirstOrDefault(option =>
                !EffectCategoryDisabledOptions.Any(disabled =>
                    disabled.Equals(option, StringComparison.OrdinalIgnoreCase)))
                ?? EffectCategoryOptions.FirstOrDefault()
                ?? EffectCategoryStandard;
        }
    }

    private bool IsSingleEffectType()
        => string.Equals(SelectedItemType, ItemTeaching, StringComparison.OrdinalIgnoreCase)
           || string.Equals(SelectedItemType, ItemScriptures, StringComparison.OrdinalIgnoreCase);

    private bool ItemUsesEffectBuilder()
    {
        if (string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase)
            || string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase)
            || string.Equals(SelectedItemType, ItemShieldsAndOther, StringComparison.OrdinalIgnoreCase)
            || string.Equals(SelectedItemType, ItemTorques, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private enum EffectLookupKind
    {
        None,
        Spells,
        Miracles,
        Evocations
    }

    private EffectLookupKind GetEffectLookupKind()
    {
        if (!ItemUsesEffectBuilder())
            return EffectLookupKind.None;

        if (string.Equals(SelectedDiscipline, DisciplineMagical, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(SelectedItemType, ItemTeaching, StringComparison.OrdinalIgnoreCase)
                || string.Equals(SelectedItemType, ItemArtefactMagical, StringComparison.OrdinalIgnoreCase)))
        {
            return EffectLookupKind.Spells;
        }

        if (string.Equals(SelectedDiscipline, DisciplineSpiritual, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(SelectedItemType, ItemScriptures, StringComparison.OrdinalIgnoreCase)
                || string.Equals(SelectedItemType, ItemArtefactSpiritual, StringComparison.OrdinalIgnoreCase)))
        {
            return EffectLookupKind.Miracles;
        }

        if (string.Equals(SelectedDiscipline, DisciplineEarthpower, StringComparison.OrdinalIgnoreCase)
            && string.Equals(SelectedItemType, ItemCharms, StringComparison.OrdinalIgnoreCase))
        {
            return EffectLookupKind.Evocations;
        }

        return EffectLookupKind.None;
    }

    private async Task RefreshEffectLookupAsync()
    {
        var requestId = ++_effectLookupRequestId;
        var kind = GetEffectLookupKind();

        if (kind == EffectLookupKind.None)
        {
            if (requestId != _effectLookupRequestId)
                return;

            _showEffectSearch = false;
            EffectSearchPlaceholder = "Search effect";
            SelectedEffectLookup = null;
            EffectLookupOptions = new Dictionary<string, MakeEffectLookupOption>(StringComparer.OrdinalIgnoreCase);
            RaisePathVisibilityProperties();
            return;
        }

        var dbInitializer = ServiceHelper.ResolveService<IDatabaseInitializer>();
        if (dbInitializer != null)
            await dbInitializer.InitializeAsync();

        try
        {
            Dictionary<string, MakeEffectLookupOption> lookup;
            string placeholder;

            if (kind == EffectLookupKind.Spells)
            {
                var spells = await SpellService.GetAllAsync();
                var filtered = FilterSpellsForCategory(spells, NewEffectCategory);
                lookup = BuildLookupDictionary(filtered
                    .Where(spell => !string.IsNullOrWhiteSpace(spell?.name))
                    .Select(spell => new MakeEffectLookupOption(
                        spell.name.Trim(),
                        Math.Max(1, spell.level),
                        "Spell"))
                    .OrderBy(option => option.BaseCost)
                    .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase),
                    option => $"{option.Name} (L{option.BaseCost})");
                placeholder = "Search spells";
            }
            else if (kind == EffectLookupKind.Miracles)
            {
                var miracles = await MiracleService.GetAllAsync();
                lookup = BuildLookupDictionary(miracles
                    .Where(miracle => !string.IsNullOrWhiteSpace(miracle?.name))
                    .Select(miracle => new MakeEffectLookupOption(
                        miracle.name.Trim(),
                        Math.Max(1, miracle.power),
                        "Miracle"))
                    .OrderBy(option => option.BaseCost)
                    .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase),
                    option => $"{option.Name} (P{option.BaseCost})");
                placeholder = "Search miracles";
            }
            else
            {
                var evocations = await DruidEvocationService.GetAllAsync();
                lookup = BuildLookupDictionary(evocations
                    .Where(evoc => !string.IsNullOrWhiteSpace(evoc?.name))
                    .Select(evoc => new MakeEffectLookupOption(
                        evoc.name.Trim(),
                        Math.Max(1, evoc.power),
                        "Evocation"))
                    .OrderBy(option => option.BaseCost)
                    .ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase),
                    option => $"{option.Name} (P{option.BaseCost})");
                placeholder = "Search evocations";
            }

            if (requestId != _effectLookupRequestId)
                return;

            _showEffectSearch = true;
            EffectSearchPlaceholder = placeholder;
            EffectLookupOptions = lookup;

            if (SelectedEffectLookup != null)
            {
                var hasSelection = lookup.Values.Any(option =>
                    string.Equals(option.Name, SelectedEffectLookup.Name, StringComparison.OrdinalIgnoreCase)
                    && option.BaseCost == SelectedEffectLookup.BaseCost
                    && string.Equals(option.SourceType, SelectedEffectLookup.SourceType, StringComparison.OrdinalIgnoreCase));
                if (!hasSelection)
                    SelectedEffectLookup = null;
            }
        }
        catch
        {
            if (requestId != _effectLookupRequestId)
                return;

            _showEffectSearch = true;
            EffectLookupOptions = new Dictionary<string, MakeEffectLookupOption>(StringComparer.OrdinalIgnoreCase);
            EffectSearchPlaceholder = kind switch
            {
                EffectLookupKind.Spells => "Spells unavailable",
                EffectLookupKind.Miracles => "Miracles unavailable",
                EffectLookupKind.Evocations => "Evocations unavailable",
                _ => "Search unavailable"
            };
            SelectedEffectLookup = null;
        }

        RaisePathVisibilityProperties();
    }

    private static IEnumerable<SpellService.SpellRaw> FilterSpellsForCategory(
        IEnumerable<SpellService.SpellRaw> spells,
        string? category)
    {
        var selected = (category ?? string.Empty).Trim();
        if (selected.Equals(EffectCategoryAdvanced, StringComparison.OrdinalIgnoreCase))
        {
            return spells.Where(IsSpellAdvanced);
        }

        if (selected.Equals(EffectCategoryNonStandard, StringComparison.OrdinalIgnoreCase))
        {
            return spells.Where(IsSpellNonStandard);
        }

        return spells.Where(spell => !IsSpellAdvanced(spell));
    }

    private static bool IsSpellAdvanced(SpellService.SpellRaw? spell)
        => spell?.isAdvanced == true || spell?.IsAdvancedCompat == true;

    private static bool IsSpellNonStandard(SpellService.SpellRaw? spell)
        => spell?.nonStandard == true || spell?.NonStandardCompat == true;

    private static Dictionary<string, MakeEffectLookupOption> BuildLookupDictionary(
        IEnumerable<MakeEffectLookupOption> options,
        Func<MakeEffectLookupOption, string> keyFactory)
    {
        var lookup = new Dictionary<string, MakeEffectLookupOption>(StringComparer.OrdinalIgnoreCase);
        var seenByBaseKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in options ?? Enumerable.Empty<MakeEffectLookupOption>())
        {
            if (option == null)
                continue;

            var baseKey = (keyFactory(option) ?? string.Empty).Trim();
            if (baseKey.Length == 0)
                baseKey = option.Name;
            if (baseKey.Length == 0)
                continue;

            var key = baseKey;
            if (seenByBaseKey.TryGetValue(baseKey, out var count))
            {
                count++;
                seenByBaseKey[baseKey] = count;
                key = $"{baseKey} #{count}";
            }
            else
            {
                seenByBaseKey[baseKey] = 1;
            }

            while (lookup.ContainsKey(key))
                key = $"{key}#";

            lookup[key] = option;
        }

        return lookup;
    }

    private void RaisePathVisibilityProperties()
    {
        Raise(nameof(ShowAdvancedWeaponOptions));
        Raise(nameof(ShowAdvancedArmourOptions));
        Raise(nameof(ShowAlignmentOption));
        Raise(nameof(ShowNeuronicTorqueOptions));
        Raise(nameof(ShowSmithedQualityOptions));
        Raise(nameof(ShowSmithedTemplateOptions));
        Raise(nameof(ShowSmithedTemplateCount));
        Raise(nameof(ShowSmithedWeaponOptions));
        Raise(nameof(ShowSmithedArmourOptions));
        Raise(nameof(ShowEffectEditor));
        Raise(nameof(ShowEffectSearch));
        Raise(nameof(ShowEffectNameEntry));
        Raise(nameof(ShowEffectUsesPerDay));
        Raise(nameof(EffectPowerColumnSpan));
    }

    private void Recalculate()
    {
        var calculation = BuildBaseCalculation();

        var automaticLines = new List<string>();
        var requirementNotes = new List<string>();
        var bonusFromMode = 0;
        var canUseTrinkets = false;
        var canUseReciprocates = false;
        var trinketRollCost = calculation.RollCostGrulls;
        var trinketLabel = "Trinket mode available.";

        if (ShowAutomatedBonusMode)
        {
            ApplyAutomatedBonuses(
                calculation,
                automaticLines,
                requirementNotes,
                ref bonusFromMode,
                ref canUseTrinkets,
                ref canUseReciprocates,
                ref trinketRollCost,
                ref trinketLabel);
        }
        else
        {
            bonusFromMode = ManualBonusRows.Sum(row => row.Percent);
            automaticLines.AddRange(ManualBonusRows.Select(row => row.DisplayText));
        }

        if (canUseTrinkets && UseTrinketMode)
        {
            bonusFromMode += 5;
            calculation.RollCostGrulls = trinketRollCost;
            if (calculation.SetupCostGrulls > 0)
                calculation.SetupCostGrulls = trinketRollCost;
            requirementNotes.Add("Trinket mode enabled: +5% and reduced setup/roll cost.");
            requirementNotes.Add("Trinkets do not have blow-up dates and do not blow up on death.");
        }

        if (canUseReciprocates && UseReciprocatesBinding)
        {
            bonusFromMode -= 5;
            requirementNotes.Add("Reciprocates Binding enabled: -5% and item may be named for no blow-up-on-death.");
        }

        if (IsDraftingMode)
        {
            ApplyDraftingItemSetupStatus();
        }
        else
        {
            var setupAvailability = EvaluateItemSetupAvailability();
            ApplyItemSetupStatus(setupAvailability);
            if (setupAvailability.HasWarning && setupAvailability.Message.Length > 0)
                requirementNotes.Add(setupAvailability.Message);
        }

        var classBonus = ResolveClassBonus();
        var finalChance = Math.Clamp(BaseChancePercent + classBonus + bonusFromMode - calculation.DifficultyPercent, 0, 99);
        var totalRollCost = calculation.RollCostGrulls * calculation.TotalRolls;
        var totalCost = calculation.SetupCostGrulls + totalRollCost;

        ClassBonusPercent = classBonus;
        BonusPercent = bonusFromMode;
        DifficultyPercent = calculation.DifficultyPercent;
        FinalChancePercent = finalChance;
        TotalRolls = calculation.TotalRolls;
        SetupCostGrulls = calculation.SetupCostGrulls;
        RollCostGrulls = calculation.RollCostGrulls;
        TotalCostGrulls = totalCost;
        OverallSuccessChanceText = calculation.TotalRolls <= 0
            ? "0.00%"
            : $"{Math.Pow(finalChance / 100d, calculation.TotalRolls) * 100d:0.00}%";
        RebuildResultRows();

        CanUseTrinketMode = canUseTrinkets;
        CanUseReciprocatesBinding = canUseReciprocates;
        TrinketModeLabel = trinketLabel;

        if (!CanUseTrinketMode && UseTrinketMode)
            UseTrinketMode = false;
        if (!CanUseReciprocatesBinding && UseReciprocatesBinding)
            UseReciprocatesBinding = false;

        AutomatedBonusLines.Clear();
        foreach (var line in automaticLines)
            AutomatedBonusLines.Add(line);

        RequirementLines.Clear();
        RequirementLines.Add($"Rolls required: {calculation.TotalRolls}");
        RequirementLines.Add($"Difficulty total: {calculation.DifficultyPercent}%");
        RequirementLines.Add($"Setup cost: {calculation.SetupCostGrulls} grulls");
        RequirementLines.Add($"Cost per roll: {calculation.RollCostGrulls} grulls");
        RequirementLines.Add($"Total cost (setup + rolls): {totalCost} grulls");

        foreach (var note in calculation.Notes)
            RequirementLines.Add(note);
        foreach (var note in requirementNotes.Distinct(StringComparer.OrdinalIgnoreCase))
            RequirementLines.Add(note);
    }

    private void RebuildResultRows()
    {
        ResultRows.Clear();
        ResultRows.Add(new MakeResultRowVm("Class bonus:", $"{ClassBonusPercent}%"));
        ResultRows.Add(new MakeResultRowVm("Mode bonus:", $"{BonusPercent}%"));
        ResultRows.Add(new MakeResultRowVm("Difficulty:", $"-{DifficultyPercent}%"));
        ResultRows.Add(new MakeResultRowVm("Final roll chance:", $"{FinalChancePercent}%"));
        ResultRows.Add(new MakeResultRowVm("All-roll completion:", OverallSuccessChanceText));
        ResultRows.Add(new MakeResultRowVm("Target rolls:", $"{TotalRolls}"));
        ResultRows.Add(new MakeResultRowVm("Setup cost:", $"{SetupCostGrulls} grulls"));
        ResultRows.Add(new MakeResultRowVm("Cost per roll:", $"{RollCostGrulls} grulls"));
        ResultRows.Add(new MakeResultRowVm("Total cost:", $"{TotalCostGrulls} grulls"));
    }

    private MakeCalculation BuildBaseCalculation()
    {
        var notes = new List<string>();
        var result = new MakeCalculation
        {
            TotalRolls = 0,
            DifficultyPercent = 0,
            SetupCostGrulls = 0,
            RollCostGrulls = 0,
            Notes = notes
        };

        if (string.Equals(SelectedDiscipline, DisciplineMagical, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase))
            {
                result.SetupCostGrulls = 2000;
                result.RollCostGrulls = 2000;
                ApplyWeaponFormula(result);
            }
            else if (string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase))
            {
                result.SetupCostGrulls = 2000;
                result.RollCostGrulls = 2000;
                ApplyArmourFormula(result);
            }
            else if (string.Equals(SelectedItemType, ItemTeaching, StringComparison.OrdinalIgnoreCase))
            {
                ApplyTeachingFormula(result);
            }
            else
            {
                result.SetupCostGrulls = 2000;
                result.RollCostGrulls = 2000;
                ApplyPerPowerFormula(result);
            }
        }
        else if (string.Equals(SelectedDiscipline, DisciplineSpiritual, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase))
            {
                result.SetupCostGrulls = 3000;
                result.RollCostGrulls = 3000;
                ApplyWeaponFormula(result);
            }
            else if (string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase))
            {
                result.SetupCostGrulls = 3000;
                result.RollCostGrulls = 3000;
                ApplyArmourFormula(result);
            }
            else if (string.Equals(SelectedItemType, ItemScriptures, StringComparison.OrdinalIgnoreCase))
            {
                ApplyScriptureFormula(result);
            }
            else
            {
                result.SetupCostGrulls = 3000;
                result.RollCostGrulls = 3000;
                ApplyPerPowerFormula(result);
            }
        }
        else if (string.Equals(SelectedDiscipline, DisciplineEarthpower, StringComparison.OrdinalIgnoreCase))
        {
            result.SetupCostGrulls = 2000;
            result.RollCostGrulls = 2000;
            ApplyPerPowerFormula(result);
        }
        else if (string.Equals(SelectedDiscipline, DisciplineNeuronic, StringComparison.OrdinalIgnoreCase))
        {
            result.SetupCostGrulls = 1000;
            result.RollCostGrulls = 1000;
            if (string.Equals(SelectedItemType, ItemPrisms, StringComparison.OrdinalIgnoreCase))
                ApplyNeuronicPrismFormula(result);
            else
                ApplyNeuronicTorqueFormula(result);
        }
        else if (string.Equals(SelectedDiscipline, DisciplineSmithed, StringComparison.OrdinalIgnoreCase))
        {
            result.SetupCostGrulls = 1000;
            result.RollCostGrulls = 1000;
            ApplySmithedFormula(result);
        }
        else
        {
            result.Notes.Add("No make path selected.");
        }

        return result;
    }

    private void ApplyWeaponFormula(MakeCalculation result)
    {
        var tier = ParseTier(SelectedWeaponTier);
        if (tier <= 0)
        {
            result.TotalRolls += 4;
            result.DifficultyPercent += 10;
        }
        else if (tier == 1)
        {
            result.TotalRolls += 9;
            result.DifficultyPercent += 15;
        }
        else
        {
            result.TotalRolls += 15;
            result.DifficultyPercent += 20;
        }

        if (IsColouredOrAligned)
        {
            result.TotalRolls += 1;
            result.DifficultyPercent += 5;
            result.Notes.Add("Coloured/aligned item: +1 roll, +5% difficulty.");
        }
    }

    private void ApplyArmourFormula(MakeCalculation result)
    {
        switch ((SelectedArmourWeight ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "medium":
                result.TotalRolls += 4;
                result.DifficultyPercent += 8;
                break;
            case "heavy":
                result.TotalRolls += 5;
                result.DifficultyPercent += 12;
                break;
            default:
                result.TotalRolls += 3;
                result.DifficultyPercent += 4;
                break;
        }

        var enhancement = ParseTier(SelectedArmourEnhancement);
        if (enhancement == 1)
        {
            result.TotalRolls += 5;
            result.DifficultyPercent += 10;
        }
        else if (enhancement >= 2)
        {
            result.TotalRolls += 10;
            result.DifficultyPercent += 20;
        }

        if (IsColouredOrAligned)
        {
            result.TotalRolls += 1;
            result.DifficultyPercent += 5;
            result.Notes.Add("Coloured/aligned armour: +1 roll, +5% difficulty.");
        }
    }

    private void ApplyPerPowerFormula(MakeCalculation result)
    {
        var rows = EffectRows.ToList();
        if (rows.Count == 0)
        {
            result.Notes.Add("No effect entries added yet.");
            return;
        }

        foreach (var row in rows)
        {
            var scaledPower = Math.Max(1, row.Power) * Math.Max(1, row.UsesPerDay);
            result.TotalRolls += scaledPower;
            result.DifficultyPercent += scaledPower;
        }
    }

    private void ApplyTeachingFormula(MakeCalculation result)
    {
        var row = EffectRows.FirstOrDefault();
        if (row == null)
        {
            result.Notes.Add("Add a teaching-scroll spell entry to calculate rolls and difficulty.");
            return;
        }

        var scaledPower = Math.Max(1, row.Power) * Math.Max(1, row.UsesPerDay);
        var category = (row.Category ?? string.Empty).Trim();
        var isAdvanced = category.Equals(EffectCategoryAdvanced, StringComparison.OrdinalIgnoreCase);
        var isNonStandard = category.Equals(EffectCategoryNonStandard, StringComparison.OrdinalIgnoreCase);

        var diffPerPower = isNonStandard ? 2 : (isAdvanced ? 1 : 0);
        var rollCost = isNonStandard ? 1500 : (isAdvanced ? 1000 : 100);

        result.RollCostGrulls = rollCost;
        result.SetupCostGrulls = rollCost;
        result.TotalRolls += scaledPower;
        result.DifficultyPercent += diffPerPower * scaledPower;
    }

    private void ApplyScriptureFormula(MakeCalculation result)
    {
        var row = EffectRows.FirstOrDefault();
        if (row == null)
        {
            result.Notes.Add("Add a scripture entry to calculate rolls and difficulty.");
            return;
        }

        var scaledPower = Math.Max(1, row.Power) * Math.Max(1, row.UsesPerDay);
        var category = (row.Category ?? string.Empty).Trim();
        var isAdvanced = category.Equals(EffectCategoryAdvanced, StringComparison.OrdinalIgnoreCase);
        var isNonStandard = category.Equals(EffectCategoryNonStandard, StringComparison.OrdinalIgnoreCase);

        var diffPerPower = isNonStandard ? 2 : (isAdvanced ? 1 : 0);
        var rollCost = isNonStandard ? 2000 : (isAdvanced ? 1500 : 500);

        result.RollCostGrulls = rollCost;
        result.SetupCostGrulls = rollCost;
        result.TotalRolls += scaledPower;
        result.DifficultyPercent += diffPerPower * scaledPower;
    }

    private void ApplyNeuronicPrismFormula(MakeCalculation result)
    {
        var rows = EffectRows.ToList();
        if (rows.Count == 0)
        {
            result.Notes.Add("Add at least one neuronic power entry for prism manufacture.");
            return;
        }

        foreach (var row in rows)
        {
            var tblp = Math.Max(1, row.Power);
            var uses = Math.Max(1, row.UsesPerDay);
            result.TotalRolls += tblp * uses;
            result.DifficultyPercent += (int)Math.Ceiling(tblp / 3d) * uses;
        }
    }

    private void ApplyNeuronicTorqueFormula(MakeCalculation result)
    {
        if (TorqueFocusMind)
        {
            result.TotalRolls += 5;
            result.DifficultyPercent += 5;
            result.Notes.Add("Focus the Mind: +5 rolls, +5% difficulty.");
        }

        switch (NormalizeToken(SelectedTorqueNacOption))
        {
            case "1nac":
            case "+1nac":
                result.TotalRolls += 5;
                result.DifficultyPercent += 5;
                result.Notes.Add("Shield the Mind (+1 NAC): +5 rolls, +5% difficulty.");
                break;
            case "2nac":
            case "+2nac":
                result.TotalRolls += 15;
                result.DifficultyPercent += 10;
                result.Notes.Add("Shield the Mind (+2 NAC): +15 rolls, +10% difficulty.");
                break;
        }

        if (TorquePsionicRetard)
        {
            result.TotalRolls += 10;
            result.DifficultyPercent += 10;
            result.Notes.Add("Psionic Retard: +10 rolls, +10% difficulty.");
        }

        if (result.TotalRolls <= 0)
            result.Notes.Add("Select at least one torque feature to calculate rolls and difficulty.");
    }

    private void ApplySmithedFormula(MakeCalculation result)
    {
        if (SmithedUseTemplates)
        {
            var templateCount = Math.Max(1, SmithedTemplateCount);
            result.TotalRolls += templateCount * 5;
            result.DifficultyPercent += templateCount * 5;
            result.Notes.Add($"Template manufacture x{templateCount}: +{templateCount * 5} rolls and +{templateCount * 5}% difficulty.");
            return;
        }

        var isArmour = string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase);
        var isWeapons = string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase);

        var qualityPenalty = 0;
        var nonArmourRolls = 3;
        switch (NormalizeToken(SelectedSmithedQuality))
        {
            case "journeymen":
                qualityPenalty = 5;
                nonArmourRolls = 6;
                break;
            case "master":
                qualityPenalty = 10;
                nonArmourRolls = 10;
                break;
            default:
                qualityPenalty = 0;
                nonArmourRolls = 3;
                break;
        }

        result.DifficultyPercent += qualityPenalty;
        if (isArmour)
        {
            var ac = Math.Max(1, SmithedArmourAc);
            var armourRolls = nonArmourRolls;
            switch (NormalizeToken(SelectedSmithedQuality))
            {
                case "journeymen":
                    armourRolls = (int)Math.Ceiling((ac * 2d) / 3d);
                    break;
                case "master":
                    armourRolls = ac;
                    break;
                default:
                    armourRolls = (int)Math.Ceiling(ac / 2d);
                    break;
            }

            result.TotalRolls += Math.Max(1, armourRolls);

            var armourEnhancement = ParseTier(SelectedSmithedArmourEnhancement);
            if (armourEnhancement == 1)
            {
                result.TotalRolls += 5;
                result.DifficultyPercent += 5;
            }
            else if (armourEnhancement >= 2)
            {
                result.TotalRolls += 15;
                result.DifficultyPercent += 10;
            }

            if (SmithedArmourWellFitted)
            {
                result.TotalRolls += 5;
                result.DifficultyPercent += 5;
            }
        }
        else
        {
            result.TotalRolls += nonArmourRolls;
        }

        if (isWeapons)
        {
            var weaponEnhancement = ParseTier(SelectedSmithedWeaponEnhancement);
            if (weaponEnhancement == 1)
            {
                result.TotalRolls += 5;
                result.DifficultyPercent += 5;
            }
            else if (weaponEnhancement >= 2)
            {
                result.TotalRolls += 10;
                result.DifficultyPercent += 15;
            }

            if (SmithedWeaponDifferentMaterial)
            {
                result.DifficultyPercent += 5;
                result.SetupCostGrulls *= 2;
                result.RollCostGrulls *= 2;
                result.Notes.Add("Different material: +5% difficulty and setup/roll costs are doubled.");
            }
        }
    }

    private void ApplyAutomatedBonuses(
        MakeCalculation calculation,
        List<string> automaticLines,
        List<string> requirementNotes,
        ref int bonusFromMode,
        ref bool canUseTrinkets,
        ref bool canUseReciprocates,
        ref int trinketRollCost,
        ref string trinketLabel)
    {
        var isMagical = string.Equals(SelectedDiscipline, DisciplineMagical, StringComparison.OrdinalIgnoreCase);
        var isSpiritual = string.Equals(SelectedDiscipline, DisciplineSpiritual, StringComparison.OrdinalIgnoreCase);
        var isEarthpower = string.Equals(SelectedDiscipline, DisciplineEarthpower, StringComparison.OrdinalIgnoreCase);
        var isNeuronic = string.Equals(SelectedDiscipline, DisciplineNeuronic, StringComparison.OrdinalIgnoreCase);
        var isSmithed = string.Equals(SelectedDiscipline, DisciplineSmithed, StringComparison.OrdinalIgnoreCase);
        var isTeaching = string.Equals(SelectedItemType, ItemTeaching, StringComparison.OrdinalIgnoreCase);
        var isScripture = string.Equals(SelectedItemType, ItemScriptures, StringComparison.OrdinalIgnoreCase);
        var isPrism = string.Equals(SelectedItemType, ItemPrisms, StringComparison.OrdinalIgnoreCase);
        var isTorque = string.Equals(SelectedItemType, ItemTorques, StringComparison.OrdinalIgnoreCase);
        var isWeapons = string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase);
        var isArmour = string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase);
        var isSmithedOther = string.Equals(SelectedItemType, ItemShieldsAndOther, StringComparison.OrdinalIgnoreCase);
        var isMagicMisc = string.Equals(SelectedItemType, ItemArtefactMagical, StringComparison.OrdinalIgnoreCase);
        var isSpiritualMisc = string.Equals(SelectedItemType, ItemArtefactSpiritual, StringComparison.OrdinalIgnoreCase);
        var isEarthCharm = string.Equals(SelectedItemType, ItemCharms, StringComparison.OrdinalIgnoreCase);

        AddBonusIf(
            HasAbility("Magical Manufacturer", "ability.make.magical-manufacturer")
            && isMagical,
            2,
            "Magical Manufacturer",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasAbility("Wizard's Study", "ability.make.wizard-s-study")
            && isTeaching,
            2,
            "Wizard's Study",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasAbility("Research Reduction", "ability.make.research-reduction")
            && isTeaching,
            2,
            "Research Reduction",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasAbility("Spiritual Manufacturer", "ability.make.spiritual-manufacturer")
            && isSpiritual,
            2,
            "Spiritual Manufacturer",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasAbility("Scriptorium", "ability.make.scriptorium")
            && isScripture,
            2,
            "Scriptorium",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasAbility("Natural Manufacturer", "ability.make.natural-manufacturer")
            && isEarthpower,
            2,
            "Natural Manufacturer",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasAbility("Craft Speciality (Earthpower)", "ability.make.craft-speciality-earthpower")
            && isEarthpower,
            2,
            "Craft Speciality (Earthpower)",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasCraftSpecialityBonus(
                abilityName: "Craft Speciality (Wizard)",
                abilityRef: "ability.make.craft-speciality-wizard",
                choiceSetRef: ChoiceCraftWizard,
                itemIsWeapons: isMagical && isWeapons,
                itemIsArmour: isMagical && isArmour,
                itemIsMisc: isMagical && isMagicMisc,
                itemIsTeachingOrScripture: isTeaching),
            2,
            "Craft Speciality (Wizard)",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasCraftSpecialityBonus(
                abilityName: "Craft Speciality (Spiritual)",
                abilityRef: "ability.make.craft-speciality-spiritual",
                choiceSetRef: ChoiceCraftSpiritual,
                itemIsWeapons: isSpiritual && isWeapons,
                itemIsArmour: isSpiritual && isArmour,
                itemIsMisc: isSpiritual && isSpiritualMisc,
                itemIsTeachingOrScripture: isScripture),
            2,
            "Craft Speciality (Spiritual)",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasAbility("Master Smith", "ability.make.master-smith")
            && isSmithed,
            2,
            "Master Smith",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasCraftSpecialityBonus(
                abilityName: "Craft Speciality (Warrior)",
                abilityRef: "ability.make.craft-speciality-warrior",
                choiceSetRef: ChoiceCraftWarrior,
                itemIsWeapons: isSmithed && isWeapons,
                itemIsArmour: isSmithed && isArmour,
                itemIsMisc: isSmithed && isSmithedOther,
                itemIsTeachingOrScripture: false,
                itemIsOther: isSmithed && isSmithedOther),
            2,
            "Craft Speciality (Warrior)",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasAbility("Mental Manufacturer", "ability.make.mental-manufacturer")
            && isNeuronic
            && isPrism,
            2,
            "Mental Manufacturer",
            automaticLines,
            ref bonusFromMode);

        AddBonusIf(
            HasCraftSpecialityBonus(
                abilityName: "Craft Speciality (Neuronic)",
                abilityRef: "ability.make.craft-speciality-neuronic",
                choiceSetRef: ChoiceCraftNeuronic,
                itemIsWeapons: false,
                itemIsArmour: false,
                itemIsMisc: false,
                itemIsTeachingOrScripture: false,
                itemIsOther: false,
                itemIsPrism: isNeuronic && isPrism,
                itemIsTorque: isNeuronic && isTorque),
            2,
            "Craft Speciality (Neuronic)",
            automaticLines,
            ref bonusFromMode);

        var magicalArtisanLevel = ResolveMultiClassCraftTrackLevel(McMagicalArtisanRefPrefix);
        var magicalCalligrapherLevel = ResolveMultiClassCraftTrackLevel(McMagicalCalligrapherRefPrefix);
        var spiritualArtisanLevel = ResolveMultiClassCraftTrackLevel(McSpiritualArtisanRefPrefix);
        var spiritualCalligrapherLevel = ResolveMultiClassCraftTrackLevel(McSpiritualCalligrapherRefPrefix);
        var naturalArtisanLevel = ResolveMultiClassCraftTrackLevel(McNaturalArtisanRefPrefix);
        var neuronicArtisanLevel = ResolveMultiClassCraftTrackLevel(McNeuronicArtisanRefPrefix);
        var smithLevel = ResolveMultiClassCraftTrackLevel(McSmithRefPrefix);

        var rollCostReductionPercent = 0;
        var rollCostReductionReason = string.Empty;

        if (isMagical && !isTeaching && magicalArtisanLevel > 0)
        {
            AddBonusIf(
                true,
                magicalArtisanLevel,
                "Magical Artisan (+1% per class level)",
                automaticLines,
                ref bonusFromMode);
            if (magicalArtisanLevel >= 4)
                TryReduceRollCount(calculation, 1, "Magical Artisan (Place of Power)", requirementNotes);

            RegisterRollCostReduction(
                ResolveMultiClassCostReductionPercent(magicalArtisanLevel),
                "Magical Artisan",
                ref rollCostReductionPercent,
                ref rollCostReductionReason);
        }

        if (isMagical && isTeaching && magicalCalligrapherLevel > 0)
        {
            AddBonusIf(
                true,
                magicalCalligrapherLevel,
                "Magical Calligrapher (+1% per class level)",
                automaticLines,
                ref bonusFromMode);
            RegisterRollCostReduction(
                ResolveMultiClassCostReductionPercent(magicalCalligrapherLevel),
                "Magical Calligrapher",
                ref rollCostReductionPercent,
                ref rollCostReductionReason);
        }

        if (isSpiritual && !isScripture && spiritualArtisanLevel > 0)
        {
            AddBonusIf(
                true,
                spiritualArtisanLevel,
                "Spiritual Artisan (+1% per class level)",
                automaticLines,
                ref bonusFromMode);
            if (spiritualArtisanLevel >= 4)
                TryReduceRollCount(calculation, 1, "Spiritual Artisan (Place of Power)", requirementNotes);

            RegisterRollCostReduction(
                ResolveMultiClassCostReductionPercent(spiritualArtisanLevel),
                "Spiritual Artisan",
                ref rollCostReductionPercent,
                ref rollCostReductionReason);
        }

        if (isSpiritual && isScripture && spiritualCalligrapherLevel > 0)
        {
            AddBonusIf(
                true,
                spiritualCalligrapherLevel,
                "Spiritual Calligrapher (+1% per class level)",
                automaticLines,
                ref bonusFromMode);
            RegisterRollCostReduction(
                ResolveMultiClassCostReductionPercent(spiritualCalligrapherLevel),
                "Spiritual Calligrapher",
                ref rollCostReductionPercent,
                ref rollCostReductionReason);
        }

        if (isEarthpower && naturalArtisanLevel > 0)
        {
            AddBonusIf(
                true,
                naturalArtisanLevel,
                "Natural Artisan (+1% per class level)",
                automaticLines,
                ref bonusFromMode);
            if (naturalArtisanLevel >= 4)
                TryReduceRollCount(calculation, 1, "Natural Artisan (Place of Power)", requirementNotes);

            RegisterRollCostReduction(
                ResolveMultiClassCostReductionPercent(naturalArtisanLevel),
                "Natural Artisan",
                ref rollCostReductionPercent,
                ref rollCostReductionReason);
        }

        if (isNeuronic && neuronicArtisanLevel > 0)
        {
            AddBonusIf(
                true,
                neuronicArtisanLevel,
                "Artisan of the Mind (+1% per class level)",
                automaticLines,
                ref bonusFromMode);
            if (neuronicArtisanLevel >= 4)
                TryReduceRollCount(calculation, 1, "Artisan of the Mind (Place of Power)", requirementNotes);

            RegisterRollCostReduction(
                ResolveMultiClassCostReductionPercent(neuronicArtisanLevel),
                "Artisan of the Mind",
                ref rollCostReductionPercent,
                ref rollCostReductionReason);
        }

        if (isSmithed && smithLevel > 0)
        {
            AddBonusIf(
                true,
                smithLevel,
                "Smith (+1% per class level)",
                automaticLines,
                ref bonusFromMode);

            var apprenticeBonus = smithLevel >= 6 ? 10 : (smithLevel >= 4 ? 5 : 0);
            AddBonusIf(
                apprenticeBonus > 0,
                apprenticeBonus,
                "Smith Apprentice Bonus",
                automaticLines,
                ref bonusFromMode);

            RegisterRollCostReduction(
                ResolveMultiClassCostReductionPercent(smithLevel),
                "Smith",
                ref rollCostReductionPercent,
                ref rollCostReductionReason);
        }

        ApplyRollCostReduction(
            calculation,
            rollCostReductionPercent,
            rollCostReductionReason,
            requirementNotes);

        if (HasAbility("Wizard's Laboratory", "ability.make.wizard-s-laboratory")
            && isMagical
            && !isTeaching)
        {
            calculation.SetupCostGrulls = 0;
            requirementNotes.Add("Wizard's Laboratory removes setup cost for magical items.");
        }

        if (HasAbility("Wizard's Study", "ability.make.wizard-s-study") && isTeaching)
        {
            calculation.SetupCostGrulls = 0;
            requirementNotes.Add("Wizard's Study removes setup cost for teaching scrolls.");
        }

        if (HasAbility("Private Shrine", "ability.make.private-shrine")
            && isSpiritual
            && !isScripture)
        {
            calculation.SetupCostGrulls = 0;
            requirementNotes.Add("Private Shrine removes setup cost for spiritual items.");
        }

        if (HasAbility("Tranquil Grove", "ability.make.tranquil-grove") && isEarthpower)
        {
            calculation.SetupCostGrulls = 0;
            requirementNotes.Add("Tranquil Grove removes setup cost for Earthpower charms.");
        }

        if (HasAbility("Silent Chamber", "ability.make.silent-chamber")
            && isNeuronic
            && isPrism)
        {
            calculation.SetupCostGrulls = 0;
            requirementNotes.Add("Silent Chamber removes setup cost for neuronic prisms.");
        }

        if (HasAbility("Grand Smithy", "ability.make.grand-smithy") && isSmithed)
        {
            calculation.SetupCostGrulls = 0;
            requirementNotes.Add("Grand Smithy removes setup cost for smithed items.");
        }

        if (isMagical && isMagicMisc)
        {
            if (HasAbility("Trinkets (Wizard)", "ability.make.trinkets-wizard"))
            {
                canUseTrinkets = true;
                trinketRollCost = 750;
                trinketLabel = "Use Trinkets (Wizard): +5% and 750 grulls per roll/setup.";
            }
        }
        else if (isSpiritual && isSpiritualMisc)
        {
            if (HasAbility("Trinkets (Spiritual)", "ability.make.trinkets-spiritual"))
            {
                canUseTrinkets = true;
                trinketRollCost = 1000;
                trinketLabel = "Use Trinkets (Spiritual): +5% and 1000 grulls per roll/setup.";
            }
        }
        else if (isEarthpower && isEarthCharm)
        {
            if (HasAbility("Trinkets (Druid)", "ability.make.trinkets-druid"))
            {
                canUseTrinkets = true;
                trinketRollCost = 500;
                trinketLabel = "Use Trinkets (Druid): +5% and 500 grulls per roll/setup.";
            }
        }
        else if (isNeuronic && isPrism && HasAbility("Trinkets (Neuronic)", "ability.make.trinkets-neuronic"))
        {
            canUseTrinkets = true;
            trinketRollCost = 500;
            trinketLabel = "Use Trinkets (Neuronic): +5% and 500 grulls per roll/setup.";
        }

        if (isMagical && HasAbility("Reciprocates Binding (Wizard)", "ability.make.reciprocates-binding-wizard"))
            canUseReciprocates = true;
        if (isSpiritual && HasAbility("Reciprocates Binding (Spiritual)", "ability.make.reciprocates-binding-spiritual"))
            canUseReciprocates = true;
        if (isEarthpower && HasAbility("Reciprocates Binding (Druid)", "ability.make.reciprocates-binding-druid"))
            canUseReciprocates = true;
        if (isNeuronic && HasAbility("Reciprocates Binding (Neuronic)", "ability.make.reciprocates-binding-neuronic"))
            canUseReciprocates = true;

        if (isNeuronic && isPrism && !HasAbility("Gem Cutter", "ability.make.gem-cutter"))
            requirementNotes.Add("Gem Cutter ability was not detected. This is normally required for prism manufacture.");
        if (isNeuronic && isTorque && !HasAbility("Torque Crafter", "ability.make.torque-crafter"))
            requirementNotes.Add("Torque Crafter ability was not detected. This is normally required for torque manufacture.");
        if (isSmithed && !HasSmithTypeForCurrentItem())
            requirementNotes.Add("Smith (Type) ability/selection does not match the current smithed item type.");
        if (isSmithed)
            requirementNotes.Add("Smithed items are fitted to a named individual and do not blow up on death.");

        if (automaticLines.Count == 0)
            automaticLines.Add("No automated make bonuses detected from current character/items.");
    }

    private ItemSetupAvailability EvaluateItemSetupAvailability()
    {
        if (string.Equals(SelectedDiscipline, DisciplineMagical, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(SelectedItemType, ItemTeaching, StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateAbilityRequirement(
                    "Make Teaching Scrolls",
                    ("Make Teaching Scrolls", "ability.make-teaching-scrolls"));
            }

            if (string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateAbilityRequirement(
                    "Make Magical Weapons",
                    ("Make Magical Weapons", "ability.make-magical-weapons"));
            }

            if (string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateAbilityRequirement(
                    "Make Magical Armour",
                    ("Make Magical Armour", "ability.make-magical-armour"));
            }

            return EvaluateAbilityRequirement(
                "Make Artefacts",
                ("Make Artefacts", "ability.make-artefacts"));
        }

        if (string.Equals(SelectedDiscipline, DisciplineSpiritual, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(SelectedItemType, ItemScriptures, StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateAbilityRequirement(
                    "Scriptures of Faith",
                    ("Scriptures of Faith", "ability.make-scriptures-of-faith"),
                    ("Make Scriptures of Faith", "ability.make-scriptures-of-faith"),
                    ("Make Minor Scripture", "ability.make-minor-scripture"),
                    ("Make Major Scripture", "ability.make-major-scripture"));
            }

            if (string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateAbilityRequirement(
                    "Make Spiritual Weapons",
                    ("Make Spiritual Weapons", "ability.make-spiritual-weapons"),
                    ("Make Major Weapons", "ability.make-major-weapons"));
            }

            if (string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateAbilityRequirement(
                    "Make Spiritual Armour",
                    ("Make Spiritual Armour", "ability.make-spiritual-armour"),
                    ("Make Major Armour", "ability.make-major-armour"));
            }

            return EvaluateAbilityRequirement(
                "Make Spiritual Artefacts",
                ("Make Spiritual Artefacts", "ability.make-spiritual-artefacts"),
                ("Make Spiritual Artifacts", "ability.make-spiritual-artefacts"),
                ("Make Minor Miscellaneous", "ability.make-minor-miscellaneous"),
                ("Make Major Miscellaneous", "ability.make-major-miscellaneous"));
        }

        if (string.Equals(SelectedDiscipline, DisciplineEarthpower, StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateAbilityRequirement(
                "Crafter of Charms",
                ("Crafter of Charms", "ability.make.crafter-of-charms"));
        }

        if (string.Equals(SelectedDiscipline, DisciplineNeuronic, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(SelectedItemType, ItemPrisms, StringComparison.OrdinalIgnoreCase))
            {
                return EvaluateAbilityRequirement(
                    "Gem Cutter",
                    ("Gem Cutter", "ability.make.gem-cutter"));
            }

            return EvaluateAbilityRequirement(
                "Torque Crafter",
                ("Torque Crafter", "ability.make.torque-crafter"));
        }

        if (string.Equals(SelectedDiscipline, DisciplineSmithed, StringComparison.OrdinalIgnoreCase))
        {
            if (!HasAbility("Smith (Type)", "ability.make.smith-type"))
            {
                return new ItemSetupAvailability(
                    HasRequiredAbility: false,
                    RequiredAbilityLabel: "Smith (Type)",
                    Message: $"Warning: missing required make ability \"Smith (Type)\" for {SelectedItemType}.");
            }

            if (!HasSmithTypeForCurrentItem())
            {
                return new ItemSetupAvailability(
                    HasRequiredAbility: false,
                    RequiredAbilityLabel: "Smith (Type)",
                    Message: $"Warning: Smith (Type) is owned but its selected type does not match {SelectedItemType}.");
            }

            return new ItemSetupAvailability(
                HasRequiredAbility: true,
                RequiredAbilityLabel: "Smith (Type)",
                Message: string.Empty);
        }

        return new ItemSetupAvailability(
            HasRequiredAbility: true,
            RequiredAbilityLabel: string.Empty,
            Message: string.Empty);
    }

    private ItemSetupAvailability EvaluateAbilityRequirement(
        string requiredLabel,
        params (string Name, string AbilityRef)[] abilityMatchers)
    {
        var hasAbility = HasAnyAbility(abilityMatchers);
        if (hasAbility)
        {
            return new ItemSetupAvailability(
                HasRequiredAbility: true,
                RequiredAbilityLabel: requiredLabel,
                Message: string.Empty);
        }

        return new ItemSetupAvailability(
            HasRequiredAbility: false,
            RequiredAbilityLabel: requiredLabel,
            Message: $"Warning: missing required make ability \"{requiredLabel}\" for {SelectedItemType}.");
    }

    private void ApplyItemSetupStatus(ItemSetupAvailability availability)
    {
        if (availability.HasRequiredAbility)
        {
            ItemSetupBorderColor = Color.FromArgb("#62A35A");
            ItemSetupBackgroundColor = Color.FromArgb("#EEF9EC");
            ItemSetupStatusText = availability.RequiredAbilityLabel.Length == 0
                ? "Make setup requirements are satisfied."
                : $"Ready: required make ability \"{availability.RequiredAbilityLabel}\" detected.";
            ItemSetupHasWarning = false;
            return;
        }

        ItemSetupBorderColor = Color.FromArgb("#D1A100");
        ItemSetupBackgroundColor = Color.FromArgb("#FFF9E6");
        ItemSetupStatusText = availability.Message;
        ItemSetupHasWarning = true;
    }

    private void ApplyDraftingItemSetupStatus()
    {
        ItemSetupBorderColor = Color.FromArgb("#D1D5DB");
        ItemSetupBackgroundColor = Color.FromArgb("#FFFFFF");
        ItemSetupStatusText = string.Empty;
        ItemSetupHasWarning = false;
    }

    private static void AddBonusIf(
        bool condition,
        int percent,
        string reason,
        List<string> lines,
        ref int runningTotal)
    {
        if (!condition)
            return;

        runningTotal += percent;
        lines.Add($"{reason} ({FormatSignedPercent(percent)})");
    }

    private int ResolveMultiClassCraftTrackLevel(string abilityRefPrefix)
    {
        var prefix = (abilityRefPrefix ?? string.Empty).Trim();
        if (prefix.Length == 0)
            return 0;

        for (var level = 6; level >= 1; level--)
        {
            if (HasAbility(name: string.Empty, abilityRef: $"{prefix}.l{level}"))
                return level;
        }

        return 0;
    }

    private static int ResolveMultiClassCostReductionPercent(int level)
    {
        if (level >= 5)
            return 20;
        if (level >= 3)
            return 10;
        return 0;
    }

    private static void RegisterRollCostReduction(
        int percent,
        string reason,
        ref int currentPercent,
        ref string currentReason)
    {
        if (percent <= currentPercent)
            return;

        currentPercent = percent;
        currentReason = (reason ?? string.Empty).Trim();
    }

    private static void ApplyRollCostReduction(
        MakeCalculation calculation,
        int percent,
        string reason,
        List<string> requirementNotes)
    {
        if (percent <= 0 || calculation == null || calculation.RollCostGrulls <= 0)
            return;

        var before = Math.Max(0, calculation.RollCostGrulls);
        if (before <= 0)
            return;

        var after = (int)Math.Round(
            before * ((100d - percent) / 100d),
            MidpointRounding.AwayFromZero);
        if (after <= 0)
            after = 1;

        if (after >= before)
            return;

        calculation.RollCostGrulls = after;

        var source = (reason ?? string.Empty).Trim();
        if (source.Length == 0)
            source = "Multi-class";
        requirementNotes.Add($"{source} reduces cost per roll by {percent}% ({before} -> {after} grulls).");
    }

    private static void TryReduceRollCount(
        MakeCalculation calculation,
        int reduceBy,
        string reason,
        List<string> requirementNotes)
    {
        if (calculation == null || reduceBy <= 0 || calculation.TotalRolls <= 0)
            return;

        var before = calculation.TotalRolls;
        var after = Math.Max(1, before - reduceBy);
        if (after >= before)
            return;

        calculation.TotalRolls = after;
        requirementNotes.Add($"{reason} reduces required rolls by {before - after}.");
    }

    private bool HasCraftSpecialityBonus(
        string abilityName,
        string abilityRef,
        string choiceSetRef,
        bool itemIsWeapons,
        bool itemIsArmour,
        bool itemIsMisc,
        bool itemIsTeachingOrScripture,
        bool itemIsOther = false,
        bool itemIsPrism = false,
        bool itemIsTorque = false)
    {
        if (!HasAbility(abilityName, abilityRef))
            return false;

        var selections = GetChoiceSetSelections(choiceSetRef);
        if (selections.Count == 0)
            return true; // Backward-compatible fallback for legacy drafts.

        foreach (var selection in selections)
        {
            var normalized = NormalizeToken(selection);
            if (normalized.Length == 0)
                continue;

            if (itemIsWeapons && normalized.Contains("weapon", StringComparison.OrdinalIgnoreCase))
                return true;
            if (itemIsArmour && normalized.Contains("armour", StringComparison.OrdinalIgnoreCase))
                return true;
            if (itemIsMisc && (normalized.Contains("talisman", StringComparison.OrdinalIgnoreCase)
                               || normalized.Contains("misc", StringComparison.OrdinalIgnoreCase)
                               || normalized.Contains("charm", StringComparison.OrdinalIgnoreCase)
                               || normalized.Contains("artefact", StringComparison.OrdinalIgnoreCase)
                               || normalized.Contains("artifact", StringComparison.OrdinalIgnoreCase)
                               || normalized.Contains("other", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (itemIsTeachingOrScripture
                && (normalized.Contains("teaching", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("scripture", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (itemIsOther
                && (normalized.Contains("other", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("shield", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (itemIsPrism && normalized.Contains("prism", StringComparison.OrdinalIgnoreCase))
                return true;
            if (itemIsTorque && normalized.Contains("torque", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool HasSmithTypeForCurrentItem()
    {
        if (!HasAbility("Smith (Type)", "ability.make.smith-type"))
            return false;

        var selections = GetChoiceSetSelections(ChoiceSmithType);
        if (selections.Count == 0)
            return true;

        foreach (var selection in selections)
        {
            var normalized = NormalizeToken(selection);
            if (normalized.Length == 0)
                continue;

            if (string.Equals(SelectedItemType, ItemWeapons, StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("weapon", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(SelectedItemType, ItemArmour, StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("armour", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(SelectedItemType, ItemShieldsAndOther, StringComparison.OrdinalIgnoreCase)
                && (normalized.Contains("other", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("shield", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<string> GetChoiceSetSelections(string choiceSetRef)
    {
        var suffix = $"::{(choiceSetRef ?? string.Empty).Trim()}";
        if (suffix.Length <= 2)
            return Array.Empty<string>();

        return (_draft.SpecialisationSelections ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            .Where(pair => pair.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(pair => (pair.Value ?? string.Empty).Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int ResolveClassBonus()
    {
        if (!string.Equals(SelectedDiscipline, DisciplineMagical, StringComparison.OrdinalIgnoreCase))
            return 0;

        var className = (CharacterClass ?? string.Empty).Trim();
        if (className.Length == 0)
            return 0;

        if (className.Contains("High Wizard", StringComparison.OrdinalIgnoreCase)
            || className.Equals("High-Wizard", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 0;
    }

    private bool HasAbility(string name, string abilityRef)
    {
        var normalizedName = NormalizeToken(name);
        var normalizedRef = NormalizeToken(abilityRef);
        return _ownedAbilities.Any(entry =>
            (!string.IsNullOrWhiteSpace(normalizedName) && entry.NormalizedName.Equals(normalizedName, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(normalizedRef) && entry.NormalizedRef.Equals(normalizedRef, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(normalizedName) && entry.NormalizedRef.Equals(normalizedName, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(normalizedRef) && entry.NormalizedName.Equals(normalizedRef, StringComparison.Ordinal)));
    }

    private bool HasAnyAbility(params (string Name, string AbilityRef)[] matchers)
    {
        foreach (var matcher in matchers)
        {
            if (HasAbility(matcher.Name, matcher.AbilityRef))
                return true;
        }

        return false;
    }

    private async Task LoadOwnedAbilitiesAsync()
    {
        _ownedAbilities.Clear();
        _ownedAbilityDedupes.Clear();
        OwnedAbilityLines.Clear();

        foreach (var raw in _draft.AdvancementAbilities ?? new List<string>())
        {
            var token = (raw ?? string.Empty).Trim();
            if (token.Length == 0)
                continue;

            var resolved = await AbilityDetailsLookupService.FindByIndexAsync(token);
            if (resolved != null)
            {
                AddOwnedAbility(
                    resolved.Index,
                    resolved.AbilityRef,
                    $"Advancement: {resolved.Index}");
            }
            else
            {
                AddOwnedAbility(token, string.Empty, $"Advancement: {token}");
            }
        }

        foreach (var ability in _draft.Abilities ?? new List<AbilityDraft>())
        {
            if (ability == null)
                continue;

            var name = (ability.Name ?? string.Empty).Trim();
            var key = (ability.AbilityKey ?? string.Empty).Trim();
            if (name.Length == 0 && key.Length == 0)
                continue;

            var resolvedName = name.Length > 0 ? name : key;
            AddOwnedAbility(resolvedName, key, $"Character: {resolvedName}");
        }

        await AddOwnedMultiClassAbilitiesAsync();

        foreach (var token in EnumerateItemGrantedAbilityTokens())
            AddOwnedAbility(token.Name, token.AbilityRef, $"Item: {token.Name}");

        foreach (var entry in _ownedAbilities
                     .Where(IsCraftRelatedAbility)
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            OwnedAbilityLines.Add(entry.DisplayLine);
        }

        if (OwnedAbilityLines.Count == 0)
            OwnedAbilityLines.Add("No crafting-related character/item abilities detected.");
    }

    private async Task AddOwnedMultiClassAbilitiesAsync()
    {
        var selectedLevels = _draft.MultiClassLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (selectedLevels.Count == 0)
            return;

        MultiClassCatalog? catalog = null;
        try
        {
            catalog = await MultiClassService.GetCatalogAsync();
        }
        catch
        {
            return;
        }

        var definitions = catalog?.MultiClasses ?? new Dictionary<string, MultiClassDefinition>(StringComparer.OrdinalIgnoreCase);
        if (definitions.Count == 0)
            return;

        foreach (var pair in selectedLevels)
        {
            var rawKey = (pair.Key ?? string.Empty).Trim();
            if (rawKey.Length == 0 || pair.Value <= 0)
                continue;

            if (!TryResolveMultiClassDefinition(rawKey, definitions, out var resolvedKey, out var definition))
                continue;

            var maxLevel = ResolveMultiClassMaxLevel(definition);
            var selectedLevel = Math.Clamp(pair.Value, 0, maxLevel);
            if (selectedLevel <= 0)
                continue;

            var displayName = (definition.DisplayName ?? string.Empty).Trim();
            if (displayName.Length == 0)
                displayName = resolvedKey;

            var grantedAbilityRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var level = 1; level <= selectedLevel; level++)
            {
                if (!definition.Levels.TryGetValue(level.ToString(), out var levelAbilities) || levelAbilities == null)
                    continue;

                foreach (var ability in levelAbilities)
                {
                    if (ability == null)
                        continue;

                    if (!AreMultiClassAbilityPreReqsSatisfied(ability.PreReqs, grantedAbilityRefs))
                        continue;

                    var abilityName = (ability.Name ?? string.Empty).Trim();
                    var abilityRef = (ability.AbilityRef ?? string.Empty).Trim();
                    if (abilityName.Length == 0 && abilityRef.Length == 0)
                        continue;

                    if (abilityRef.Length > 0)
                        grantedAbilityRefs.Add(abilityRef);

                    var display = abilityName.Length > 0 ? abilityName : abilityRef;
                    AddOwnedAbility(display, abilityRef, $"Multi-class ({displayName} L{level}): {display}");
                }
            }
        }
    }

    private static bool TryResolveMultiClassDefinition(
        string storedKey,
        IReadOnlyDictionary<string, MultiClassDefinition> definitions,
        out string resolvedKey,
        out MultiClassDefinition definition)
    {
        resolvedKey = string.Empty;
        definition = null!;

        var key = (storedKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return false;

        if (definitions.TryGetValue(key, out definition))
        {
            resolvedKey = key;
            return true;
        }

        var normalized = NormalizeToken(key);
        foreach (var pair in definitions)
        {
            var pairKey = (pair.Key ?? string.Empty).Trim();
            if (NormalizeToken(pairKey).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = pairKey;
                definition = pair.Value;
                return true;
            }

            var display = (pair.Value.DisplayName ?? string.Empty).Trim();
            if (display.Length == 0)
                continue;

            if (NormalizeToken(display).Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = pairKey;
                definition = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static int ResolveMultiClassMaxLevel(MultiClassDefinition definition)
    {
        if (definition == null)
            return 0;

        if (definition.MaxLevel > 0)
            return definition.MaxLevel;

        var parsedMax = definition.Levels.Keys
            .Select(level => int.TryParse(level, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(0, parsedMax);
    }

    private static bool AreMultiClassAbilityPreReqsSatisfied(
        IEnumerable<string>? preReqs,
        IReadOnlySet<string> grantedAbilityRefs)
    {
        var entries = preReqs?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();
        if (entries == null || entries.Count == 0)
            return true;

        foreach (var preReq in entries)
        {
            if (grantedAbilityRefs.Contains(preReq))
                continue;

            if (grantedAbilityRefs.Any(existing =>
                    existing.Equals(preReq, StringComparison.OrdinalIgnoreCase)
                    || NormalizeToken(existing).Equals(NormalizeToken(preReq), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsCraftRelatedAbility(OwnedAbilityEntry entry)
    {
        if (entry == null)
            return false;

        var normalizedRef = entry.NormalizedRef ?? string.Empty;
        if (normalizedRef.StartsWith("abilitymake", StringComparison.Ordinal))
            return true;

        var normalizedName = entry.NormalizedName ?? string.Empty;
        if (normalizedName.Length == 0)
            return false;

        return normalizedName.Contains("make", StringComparison.Ordinal)
               || normalizedName.Contains("craft", StringComparison.Ordinal)
               || normalizedName.Contains("manufacturer", StringComparison.Ordinal)
               || normalizedName.Contains("smith", StringComparison.Ordinal)
               || normalizedName.Contains("trinket", StringComparison.Ordinal)
               || normalizedName.Contains("reciprocates", StringComparison.Ordinal)
               || normalizedName.Contains("scripture", StringComparison.Ordinal)
               || normalizedName.Contains("teaching", StringComparison.Ordinal)
               || normalizedName.Contains("artefact", StringComparison.Ordinal)
               || normalizedName.Contains("artifact", StringComparison.Ordinal)
               || normalizedName.Contains("charm", StringComparison.Ordinal)
               || normalizedName.Contains("prism", StringComparison.Ordinal)
               || normalizedName.Contains("torque", StringComparison.Ordinal);
    }

    private IEnumerable<(string Name, string AbilityRef)> EnumerateItemGrantedAbilityTokens()
    {
        var items = LiteDbService.GetItemsAssignedToCharacter(
                _draft.CharacterRecordId,
                _draft.Name,
                _draft.PlayerName)
            .ToList();

        foreach (var item in items)
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            foreach (var ability in payload?.Item?.Abilities ?? new List<CalcResult>())
            {
                if (ability == null)
                    continue;

                var name = (ability.AbilityName ?? string.Empty).Trim();
                if (name.Length > 0)
                    yield return (name, string.Empty);

                foreach (var selected in ExtractSelectedGeneralAbilities(ability))
                {
                    var selectedName = (selected.Name ?? string.Empty).Trim();
                    var selectedRef = (selected.AbilityRef ?? string.Empty).Trim();
                    var selectedKey = (selected.AbilityKey ?? string.Empty).Trim();
                    if (selectedName.Length > 0 || selectedRef.Length > 0 || selectedKey.Length > 0)
                    {
                        var resolvedName = selectedName.Length > 0
                            ? selectedName
                            : (selectedRef.Length > 0 ? selectedRef : selectedKey);
                        var resolvedRef = selectedRef.Length > 0 ? selectedRef : selectedKey;
                        yield return (resolvedName, resolvedRef);
                    }
                }
            }
        }
    }

    private static IEnumerable<SelectedGeneralAbilityToken> ExtractSelectedGeneralAbilities(CalcResult ability)
    {
        if (ability?.Details == null
            || !ability.Details.TryGetValue("selectedGeneralAbilities", out var raw)
            || raw == null)
        {
            yield break;
        }

        if (raw is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var entry in jsonElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;

                var name = ReadJsonString(entry, "name");
                var abilityRef = ReadJsonString(entry, "abilityRef");
                var abilityKey = ReadJsonString(entry, "abilityKey");
                if (abilityKey.Length == 0)
                    abilityKey = ReadJsonString(entry, "key");
                if (name.Length == 0 && abilityRef.Length == 0 && abilityKey.Length == 0)
                    continue;

                yield return new SelectedGeneralAbilityToken(name, abilityRef, abilityKey);
            }

            yield break;
        }

        if (raw is not IEnumerable<object> objectEnumerable)
            yield break;

        foreach (var entry in objectEnumerable)
        {
            var name = ReadObjectString(entry, "name");
            var abilityRef = ReadObjectString(entry, "abilityRef");
            var abilityKey = ReadObjectString(entry, "abilityKey");
            if (abilityKey.Length == 0)
                abilityKey = ReadObjectString(entry, "key");
            if (name.Length == 0 && abilityRef.Length == 0 && abilityKey.Length == 0)
                continue;

            yield return new SelectedGeneralAbilityToken(name, abilityRef, abilityKey);
        }
    }

    private void AddOwnedAbility(string? name, string? abilityRef, string displayLine)
    {
        var normalizedName = NormalizeToken(name);
        var normalizedRef = NormalizeToken(abilityRef);
        if (normalizedName.Length == 0 && normalizedRef.Length == 0)
            return;

        var dedupe = $"{normalizedName}|{normalizedRef}";
        if (!_ownedAbilityDedupes.Add(dedupe))
            return;

        _ownedAbilities.Add(new OwnedAbilityEntry(
            (name ?? string.Empty).Trim(),
            (abilityRef ?? string.Empty).Trim(),
            normalizedName,
            normalizedRef,
            displayLine));
    }

    private static string ReadJsonString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return (property.GetString() ?? string.Empty).Trim();
    }

    private static string ReadObjectString(object? source, string propertyName)
    {
        if (source == null)
            return string.Empty;

        if (source is Dictionary<string, object?> dict
            && dict.TryGetValue(propertyName, out var value)
            && value != null)
        {
            return (value.ToString() ?? string.Empty).Trim();
        }

        var property = source.GetType().GetProperty(propertyName);
        if (property == null)
            return string.Empty;

        var raw = property.GetValue(source);
        return (raw?.ToString() ?? string.Empty).Trim();
    }

    private static int ParseTier(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier))
            return 0;

        var text = tier.Trim().Replace("+", string.Empty);
        return int.TryParse(text, out var parsed) ? Math.Max(0, parsed) : 0;
    }

    private static string NormalizeSelection(string? value, string fallback)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? (fallback ?? string.Empty) : trimmed;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string FormatSignedPercent(int value)
        => value >= 0 ? $"+{value}%" : $"{value}%";

    private static IReadOnlyList<MakeTableRowVm> BuildFailureRows()
    {
        return new List<MakeTableRowVm>
        {
            new("1-75", "No further problems."),
            new("76-90", "Lose 1 success (or item destroyed if none), plus +1 level loss and 1 tblp item perm."),
            new("91-95", "Lose 2 successes (or item destroyed), plus +2 levels and 2 tblp item perm."),
            new("96-99", "Lose 3 successes (or item destroyed), plus +3 levels and 3 tblp item perm."),
            new("100", "Item destroyed. If not a Minor Teaching Scroll, roll on Catastrophic Failure.")
        };
    }

    private static IReadOnlyList<MakeTableRowVm> BuildCriticalFailureRows()
    {
        return new List<MakeTableRowVm>
        {
            new("1-75", "Additional +4 levels lost and 4 tblp item perm."),
            new("76-90", "As above, and no more item/teaching manufacture until 2 dungeon sections and 2 months."),
            new("91-95", "As above, extra lost levels recover only on dungeon; lockout 4 sections and 4 months."),
            new("96-99", "As above, extra lost levels recover only on dungeon; lockout 6 sections and 6 months."),
            new("100", "Lose 10% vitae, +4 levels, 4 tblp item perm; no item/teaching manufacture ever again.")
        };
    }

    private sealed class MakeCalculation
    {
        public int TotalRolls { get; set; }
        public int DifficultyPercent { get; set; }
        public int SetupCostGrulls { get; set; }
        public int RollCostGrulls { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    private sealed record ItemSetupAvailability(
        bool HasRequiredAbility,
        string RequiredAbilityLabel,
        string Message)
    {
        public bool HasWarning => !HasRequiredAbility;
    }

    private sealed record OwnedAbilityEntry(
        string Name,
        string AbilityRef,
        string NormalizedName,
        string NormalizedRef,
        string DisplayLine);

    private sealed record SelectedGeneralAbilityToken(
        string Name,
        string AbilityRef,
        string AbilityKey);
}

public sealed class MakeEffectRowVm
{
    public MakeEffectRowVm(
        string name,
        int power,
        int usesPerDay,
        string category,
        string? sourceType = null,
        bool usesPerDayEnabled = true)
    {
        Name = (name ?? string.Empty).Trim();
        Power = Math.Max(1, power);
        UsesPerDay = Math.Max(1, usesPerDay);
        Category = (category ?? string.Empty).Trim();
        SourceType = (sourceType ?? string.Empty).Trim();
        UsesPerDayEnabled = usesPerDayEnabled;
    }

    public string Name { get; }
    public int Power { get; }
    public int UsesPerDay { get; }
    public string Category { get; }
    public string SourceType { get; }
    public bool UsesPerDayEnabled { get; }
    public bool HasInfoCard => SourceType.Length > 0;

    public string RollSummary => UsesPerDayEnabled
        ? $"{Power} x {UsesPerDay}/day"
        : $"{Power}";

    public string DisplayText
    {
        get
        {
            var categoryText = Category.Length == 0 ? string.Empty : $" [{Category}]";
            var usesText = UsesPerDayEnabled ? $" - {UsesPerDay}/day" : string.Empty;
            return $"{Name}{categoryText} - Cost {Power}{usesText}";
        }
    }
}

public sealed class MakeBonusRowVm : ObservableObject
{
    private readonly Dictionary<string, MakeBonusSpecialisationSelectionVm> _selectedSpecialisations =
        new(StringComparer.OrdinalIgnoreCase);
    private string _specialisationSummary = string.Empty;

    public MakeBonusRowVm(string reason, int percent)
        : this(reason, percent, null, Array.Empty<string>())
    {
    }

    public MakeBonusRowVm(
        string reason,
        int percent,
        EvolutionService.AbilityResult? ability,
        IReadOnlyList<string>? choiceSetRefs)
    {
        Reason = (reason ?? string.Empty).Trim();
        Percent = percent;
        Ability = ability;
        ChoiceSetRefs = (choiceSetRefs ?? Array.Empty<string>())
            .Select(entry => (entry ?? string.Empty).Trim())
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string Reason { get; }
    public int Percent { get; }
    public EvolutionService.AbilityResult? Ability { get; }
    public IReadOnlyList<string> ChoiceSetRefs { get; }
    public bool HasAbilityDetails => Ability != null;
    public bool CanEditSpecialisation => ChoiceSetRefs.Count > 0;

    public string SpecialisationSummary
    {
        get => _specialisationSummary;
        private set => SetProperty(ref _specialisationSummary, value);
    }

    public string DisplayReasonText => $"{Reason}{BuildSpecialisationSuffix()}";

    public string PercentText => $"{(Percent >= 0 ? "+" : string.Empty)}{Percent}%";

    public string DisplayText => $"{DisplayReasonText} {PercentText}";

    public string GetSelectedSpecialisationKey(string? choiceSetRef)
    {
        var key = (choiceSetRef ?? string.Empty).Trim();
        if (key.Length == 0)
            return string.Empty;

        return _selectedSpecialisations.TryGetValue(key, out var selection)
            ? selection.OptionKey
            : string.Empty;
    }

    public void SetSpecialisation(
        string choiceSetRef,
        string choiceSetTitle,
        string optionKey,
        string optionLabel)
    {
        var trimmedChoiceSetRef = (choiceSetRef ?? string.Empty).Trim();
        if (trimmedChoiceSetRef.Length == 0)
            return;

        var trimmedOptionKey = (optionKey ?? string.Empty).Trim();
        var trimmedOptionLabel = (optionLabel ?? string.Empty).Trim();
        if (trimmedOptionLabel.Length == 0)
            trimmedOptionLabel = trimmedOptionKey;

        if (trimmedOptionKey.Length == 0 && trimmedOptionLabel.Length == 0)
        {
            _selectedSpecialisations.Remove(trimmedChoiceSetRef);
        }
        else
        {
            _selectedSpecialisations[trimmedChoiceSetRef] = new MakeBonusSpecialisationSelectionVm(
                trimmedChoiceSetRef,
                (choiceSetTitle ?? string.Empty).Trim(),
                trimmedOptionKey,
                trimmedOptionLabel);
        }

        RebuildSpecialisationSummary();
    }

    private void RebuildSpecialisationSummary()
    {
        if (_selectedSpecialisations.Count == 0)
        {
            SpecialisationSummary = string.Empty;
            Raise(nameof(DisplayReasonText));
            Raise(nameof(DisplayText));
            return;
        }

        var summary = string.Join(", ", _selectedSpecialisations.Values
            .OrderBy(entry => entry.ChoiceSetTitle, StringComparer.OrdinalIgnoreCase)
            .Select(entry =>
            {
                var title = entry.ChoiceSetTitle.Length > 0 ? entry.ChoiceSetTitle : entry.ChoiceSetRef;
                if (title.Length == 0)
                    return entry.OptionLabel;

                return $"{title}: {entry.OptionLabel}";
            }));

        SpecialisationSummary = summary;
        Raise(nameof(DisplayReasonText));
        Raise(nameof(DisplayText));
    }

    private string BuildSpecialisationSuffix()
        => SpecialisationSummary.Length == 0
            ? string.Empty
            : $" [{SpecialisationSummary}]";
}

public sealed record MakeBonusChoiceSetEditorVm(
    string ChoiceSetRef,
    string Title,
    IReadOnlyList<MakeBonusChoiceSetOptionVm> Options,
    string SelectedOptionKey)
{
    public bool HasOptions => Options is { Count: > 0 };
}

public sealed record MakeBonusChoiceSetOptionVm(
    string Key,
    string Label)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Key) || !string.IsNullOrWhiteSpace(Label);
}

public sealed record MakeBonusSpecialisationSelectionVm(
    string ChoiceSetRef,
    string ChoiceSetTitle,
    string OptionKey,
    string OptionLabel);

public sealed class MakeResultRowVm
{
    public MakeResultRowVm(string label, string value)
    {
        Label = (label ?? string.Empty).Trim();
        Value = (value ?? string.Empty).Trim();
    }

    public string Label { get; }
    public string Value { get; }
}

public sealed record MakeTableRowVm(
    string RollRange,
    string Outcome);

public sealed class MakeEffectLookupOption
{
    public MakeEffectLookupOption(string name, int baseCost, string sourceType)
    {
        Name = (name ?? string.Empty).Trim();
        BaseCost = Math.Max(1, baseCost);
        SourceType = (sourceType ?? string.Empty).Trim();
    }

    public string Name { get; }
    public int BaseCost { get; }
    public string SourceType { get; }
}
