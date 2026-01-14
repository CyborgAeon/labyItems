using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public readonly record struct SpellOption(string Name, int Level);
public readonly record struct MiracleOption(string Name, int Power);
public readonly record struct EvocationOption(string Name, int Power);

public partial class MpBasicPage : ContentPage, INotifyPropertyChanged
{
    private bool DbEnabled => EarthPowerService.HasDatabase;
    private bool _dataLoaded;
    private readonly ObservableCollection<ContributionRow> _breakdown = new();

    // Costs (tweak easily)
    private const int SpellCostPerLevel = 10;
    private const int MiracleCostPerPower = 12;
    private const int EvocationCostPerPower = 10;
    private const int RepelGoodEvilPerUse = 6;
    private const int RepelLifePerUse = 10;
    private const int ApprenticeCost = 20;

    private static readonly string[] ApprenticeTypeChipOptions = { "🛡️ Shield", "🗡️ Weapon" };
    private static readonly string[] RepelGoodEvilChipOptions = { "👼 Good", "😈 Evil" };

    public ObservableCollection<ContributionRow> Breakdown => _breakdown;

    public ICommand ContinueCommand => new Command(async () => await Navigation.PopAsync());

    public int TotalMp
    {
        get => _totalMp;
        private set => SetProperty(ref _totalMp, value);
    }
    private int _totalMp;

    public int SpellCount { get => _spellCount; set { if (SetProperty(ref _spellCount, value)) Recalculate(); } }
    private int _spellCount;

    public int MiracleCount { get => _miracleCount; set { if (SetProperty(ref _miracleCount, value)) Recalculate(); } }
    private int _miracleCount;

    public int EvocationCount { get => _evocationCount; set { if (SetProperty(ref _evocationCount, value)) Recalculate(); } }
    private int _evocationCount;

    public int ActiveNeuroCount { get => _activeNeuroCount; set { if (SetProperty(ref _activeNeuroCount, value)) Recalculate(); } }
    private int _activeNeuroCount;

    public int PassiveNeuroCount { get => _passiveNeuroCount; set { if (SetProperty(ref _passiveNeuroCount, value)) Recalculate(); } }
    private int _passiveNeuroCount;

    public int RepelGoodEvilCount
    {
        get => _repelGoodEvilCount;
        set
        {
            var sanitized = string.IsNullOrWhiteSpace(RepelGoodEvilTarget) ? 0 : value;
            if (SetProperty(ref _repelGoodEvilCount, sanitized))
                Recalculate();
        }
    }
    private int _repelGoodEvilCount;

    public IEnumerable<string> RepelGoodEvilOptions => RepelGoodEvilChipOptions;

    public string? RepelGoodEvilTarget
    {
        get => _repelGoodEvilTarget;
        set
        {
            if (SetProperty(ref _repelGoodEvilTarget, value))
            {
                if (string.IsNullOrWhiteSpace(value))
                    RepelGoodEvilCount = 0;
                Recalculate();
            }
        }
    }
    private string? _repelGoodEvilTarget;

    public int RepelLifeCount { get => _repelLifeCount; set { if (SetProperty(ref _repelLifeCount, value)) Recalculate(); } }
    private int _repelLifeCount;

    public bool PacChecked { get => _pacChecked; set { if (SetProperty(ref _pacChecked, value)) Recalculate(); } }
    private bool _pacChecked;
    public bool DacChecked { get => _dacChecked; set { if (SetProperty(ref _dacChecked, value)) Recalculate(); } }
    private bool _dacChecked;
    public bool MacChecked { get => _macChecked; set { if (SetProperty(ref _macChecked, value)) Recalculate(); } }
    private bool _macChecked;
    public bool SacChecked { get => _sacChecked; set { if (SetProperty(ref _sacChecked, value)) Recalculate(); } }
    private bool _sacChecked;

    public SpellOption? SelectedSpellOption { get => _selectedSpellOption; set { if (SetProperty(ref _selectedSpellOption, value)) { OnPropertyChanged(nameof(SelectedSpellText)); Recalculate(); } } }
    private SpellOption? _selectedSpellOption;

    public MiracleOption? SelectedMiracleOption { get => _selectedMiracleOption; set { if (SetProperty(ref _selectedMiracleOption, value)) { OnPropertyChanged(nameof(SelectedMiracleText)); Recalculate(); } } }
    private MiracleOption? _selectedMiracleOption;

    public EvocationOption? SelectedEvocationOption { get => _selectedEvocationOption; set { if (SetProperty(ref _selectedEvocationOption, value)) { OnPropertyChanged(nameof(SelectedEvocationText)); Recalculate(); } } }
    private EvocationOption? _selectedEvocationOption;

    public string SelectedSpellText => SelectedSpellOption is SpellOption s ? $"{s.Name} ({s.Level})" : "No spell chosen";
    public string SelectedMiracleText => SelectedMiracleOption is MiracleOption m ? $"{m.Name} ({m.Power})" : "No miracle chosen";
    public string SelectedEvocationText => SelectedEvocationOption is EvocationOption e ? $"{e.Name} ({e.Power})" : "No evocation chosen";

    public IEnumerable<string> ApprenticeTypeOptions => ApprenticeTypeChipOptions;

    public string? ApprenticeType
    {
        get => _apprenticeType;
        set
        {
            if (SetProperty(ref _apprenticeType, value))
            {
                if (!IsApprenticeWeapon && SelectedWeapon.HasValue)
                    SelectedWeapon = null;
                Recalculate();
                OnPropertyChanged(nameof(IsApprenticeWeapon));
            }
        }
    }
    private string? _apprenticeType;

    public WeaponType? SelectedWeapon { get => _selectedWeapon; set { if (SetProperty(ref _selectedWeapon, value)) Recalculate(); } }
    private WeaponType? _selectedWeapon;

    public bool IsApprenticeWeapon => string.Equals(TrimChipLabel(ApprenticeType), "Weapon", StringComparison.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _lifeOptions = new()
    {
        { "0", 0 },
        { "3/1", 4 },
        { "4/2", 6 },
        { "6/2", 9 },
        { "9/3", 14 }
    };

    public MpBasicPage()
    {
        InitializeComponent();
        BindingContext = this;

        LifeSlider.ItemsSource = _lifeOptions;
        WeaponSearch.ItemsSource = new Dictionary<string, WeaponType>(
            Enum.GetValues(typeof(WeaponType))
                .Cast<WeaponType>()
                .ToDictionary(
                    v => EnumDisplayFormatter.FormatName(v.ToString()),
                    v => v));

        EvocationSearch.RemoteSearchProvider = FetchEvocationOptionsAsync;

        LifeSlider.SelectedIndex = 0;
        Recalculate();
    }

    private async Task LoadLookupDataAsync()
    {
        try
        {
            var spells = (await SpellService.GetAllAsync())
                .Where(s => s.level <= 6 && (s.isAdvanced ?? false) == false)
                .OrderBy(s => s.level)
                .ThenBy(s => s.name)
                .Select(s => new KeyValuePair<string, SpellOption>($"{s.name} (lvl {s.level})", new SpellOption(s.name, s.level)))
                .GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
            SpellSearch.ItemsSource = spells;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load spells: {ex.Message}", "OK");
        }

        try
        {
            var miracles = (await MiracleService.GetAllAsync())
                .Where(m => m.power >= 1 && m.power <= 5 && m.isAdvanced == false)
                .OrderBy(m => m.power)
                .ThenBy(m => m.name)
                .Select(m => new KeyValuePair<string, MiracleOption>($"{m.name} (power {m.power})", new MiracleOption(m.name, m.power)))
                .GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
            MiracleSearch.ItemsSource = miracles;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load miracles: {ex.Message}", "OK");
        }

        try
        {
            EvocationSearch.ItemsSource = await FetchEvocationOptionsAsync(string.Empty);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load evocations: {ex.Message}", "OK");
        }
    }

    private async Task<Dictionary<string, EvocationOption>> FetchEvocationOptionsAsync(string query)
    {
        var evocs = await EarthPowerService.SearchAsync(query ?? string.Empty, includeAdvanced: false, maxPower: 4);
        return evocs
            .Where(ev => ev.power <= 4 && ev.isAdvanced == false)
            .OrderBy(ev => ev.power)
            .ThenBy(ev => ev.name)
            .Select(ev => new KeyValuePair<string, EvocationOption>($"{ev.name} (power {ev.power})", new EvocationOption(ev.name, ev.power)))
            .GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_dataLoaded)
            return;

        _dataLoaded = true;

        if (!EarthPowerService.HasDatabase)
        {
            await DisplayAlert("Data missing", "Evocation database not found. Please ensure default.db is copied to the app data directory.", "OK");
        }

        await LoadLookupDataAsync();
    }

    private void OnLifeSelectionChanged(object sender, DictionarySelectionChangedEventArgs e)
    {
        Recalculate();
    }

    private void Recalculate()
    {
        var items = new List<ContributionRow>();
        int running = 0;

        void Add(string id, string label, int cost)
        {
            if (cost <= 0) return;
            running += cost;
            items.Add(new ContributionRow
            {
                Id = id,
                Text = $"{label} = {cost}",
                RunningTotal = running
            });
        }

        if (SelectedSpellOption is SpellOption s && SpellCount > 0)
        {
            int cost = SpellCount * Math.Max(0, s.Level) * SpellCostPerLevel;
            Add("spell", $"Spell: {s.Name} x{SpellCount}", cost);
        }

        if (SelectedMiracleOption is MiracleOption m && MiracleCount > 0)
        {
            int cost = MiracleCount * Math.Max(0, m.Power) * MiracleCostPerPower;
            Add("miracle", $"Miracle: {m.Name} x{MiracleCount}", cost);
        }

        if (SelectedEvocationOption is EvocationOption ev && EvocationCount > 0)
        {
            int cost = EvocationCount * Math.Max(0, ev.Power) * EvocationCostPerPower;
            Add("evocation", $"Evocation: {ev.Name} x{EvocationCount}", cost);
        }

        // Neuro (kept for completeness; disabled by request)
        if (ActiveNeuroCount > 0)
        {
            Add("neuro-active", $"Active neuro x{ActiveNeuroCount}", 0);
        }
        if (PassiveNeuroCount > 0)
        {
            Add("neuro-passive", $"Passive neuro x{PassiveNeuroCount}", 0);
        }

        // Life slider
        if (LifeSlider.SelectedIndex >= 0)
        {
            var key = LifeSlider.SelectedKey;
            var value = LifeSlider.SelectedValue;
            Add("life", $"Life {key}", value);
        }

        // AC bumps
        if (PacChecked) Add("pac", "+1 PAC", ArmourConfig.PacTable[1]);
        if (DacChecked) Add("dac", "+1 DAC", ArmourConfig.DacTable[1]);
        if (MacChecked) Add("mac", "+1 MAC", ArmourConfig.MacTable[1]);
        if (SacChecked) Add("sac", "+1 SAC", ArmourConfig.SacTable[1]);

        // Repels
        var repelTarget = TrimChipLabel(RepelGoodEvilTarget) ?? RepelGoodEvilTarget;
        if (RepelGoodEvilCount > 0 && !string.IsNullOrWhiteSpace(repelTarget))
        {
            int cost = RepelGoodEvilCount * RepelGoodEvilPerUse;
            Add("repel-ge", $"Repel {repelTarget} x{RepelGoodEvilCount}", cost);
        }
        if (RepelLifeCount > 0)
        {
            int cost = RepelLifeCount * RepelLifePerUse;
            Add("repel-life", $"Repel Life x{RepelLifeCount}", cost);
        }

        // Apprentice crafted
        if (!string.IsNullOrWhiteSpace(ApprenticeType))
        {
            var typeLabel = TrimChipLabel(ApprenticeType) ?? ApprenticeType;
            var label = $"Apprentice crafted {typeLabel}";
            if (IsApprenticeWeapon && SelectedWeapon.HasValue)
                label += $" ({EnumDisplayFormatter.FormatName(SelectedWeapon.Value.ToString())})";
            Add("apprentice", label, ApprenticeCost);
        }

        _breakdown.Clear();
        foreach (var item in items)
            _breakdown.Add(item);

        TotalMp = running;
    }

    private static string? TrimChipLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var parts = value.Split(' ', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? parts[1] : value.Trim();
    }

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion
}
