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
using labyItems.Pages;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public readonly record struct SpellOption(string Name, int Level, bool IsAdvanced);
public readonly record struct MiracleOption(string Name, int Power, bool IsAdvanced);
public readonly record struct EvocationOption(string Name, int Power, bool IsAdvanced);

public abstract partial class MpCalculatorPageBase : ContentPage, INotifyPropertyChanged
{
    private bool _dataLoaded;
    private bool _isReady;
    private readonly ObservableCollection<ContributionRow> _breakdown = new();

    private const int DefaultRepelGoodEvilPerUse = 40;
    private const int DefaultRepelLifePerUse = 50;
    private const int DefaultApprenticeCost = 60;

    private const int DefaultPacCost = 40;
    private const int DefaultDacCost = 60;
    private const int DefaultMacCost = 80;
    private const int DefaultSacCost = 100;

    private static readonly Dictionary<string, int> LifeIspLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        { "0", 0 },
        { "3/1", 4 },
        { "4/2", 6 },
        { "6/2", 9 },
        { "9/3", 14 },
        { "12/4", 20 },
        { "15/5", 28 }
    };

    private static readonly string[] ApprenticeTypeChipOptions = { "🛡️ Shield", "🗡️ Weapon" };
    private static readonly string[] RepelGoodEvilChipOptions = { "👼 Good", "😈 Evil" };

    public ObservableCollection<ContributionRow> Breakdown => _breakdown;

    public ICommand ContinueCommand => new Command(async () => await HandleSubmitAsync());

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

    public string SelectedSpellText => SelectedSpellOption is SpellOption s ? FormatSpellLabel(s) : "No spell chosen";
    public string SelectedMiracleText => SelectedMiracleOption is MiracleOption m ? FormatMiracleLabel(m) : "No miracle chosen";
    public string SelectedEvocationText => SelectedEvocationOption is EvocationOption e ? $"{e.Name} ({e.Power}{(e.IsAdvanced ? " adv" : string.Empty)})" : "No evocation chosen";

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

    protected virtual Dictionary<string, int> LifeOptions => new()
    {
        { "0", 0 },
        { "3/1", 30 },
        { "4/2", 40 },
        { "6/2", 60 },
        { "9/3", 90 }
    };

    protected virtual int RepelGoodEvilPerUse => DefaultRepelGoodEvilPerUse;
    protected virtual int RepelLifePerUse => DefaultRepelLifePerUse;
    protected virtual int ApprenticeCost => DefaultApprenticeCost;
    protected virtual int PacCost => DefaultPacCost;
    protected virtual int DacCost => DefaultDacCost;
    protected virtual int MacCost => DefaultMacCost;
    protected virtual int SacCost => DefaultSacCost;
    protected virtual bool ShouldWarnWhenDbMissing => true;

    protected virtual bool IncludeAdvancedEvocations => false;
    protected virtual int? MaxEvocationPower => 4;

    protected abstract DictionarySearchBar<SpellOption> SpellSearchControl { get; }
    protected abstract DictionarySearchBar<MiracleOption> MiracleSearchControl { get; }
    protected abstract DictionarySearchBar<EvocationOption> EvocationSearchControl { get; }
    protected abstract DictionarySlider LifeSliderControl { get; }
    protected abstract DictionarySearchBar<WeaponType> WeaponSearchControl { get; }

    public bool DbEnabled => EarthPowerService.HasDatabase;

    protected MpCalculatorPageBase()
    {
        BindingContext = this;
    }

    protected void InitializeCalculatorPage()
    {
        LifeSliderControl.ItemsSource = LifeOptions;
        WeaponSearchControl.ItemsSource = new Dictionary<string, WeaponType>(
            Enum.GetValues(typeof(WeaponType))
                .Cast<WeaponType>()
                .ToDictionary(
                    v => EnumDisplayFormatter.FormatName(v.ToString()),
                    v => v));

        EvocationSearchControl.RemoteSearchProvider = FetchEvocationOptionsAsync;

        LifeSliderControl.SelectedIndex = 0;
        _isReady = true;
        Recalculate();
    }

    private async Task HandleSubmitAsync()
    {
        if (TotalMp > 500)
        {
            await DisplayAlert("Limit reached", "Must not exceed limit", "OK");
            return;
        }

        var payload = BuildSubmissionPayload();
        await Navigation.PushAsync(new RecipientPage(null, payload));
    }

    protected virtual MpSubmissionPayload BuildSubmissionPayload()
    {
        var ispBreakdown = BuildIspBreakdown(out var totalIsp);
        return new MpSubmissionPayload
        {
            TotalMp = TotalMp,
            Breakdown = Breakdown.ToList(),
            TotalIsp = totalIsp,
            IspBreakdown = ispBreakdown
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_dataLoaded)
            return;

        _dataLoaded = true;

        if (ShouldWarnWhenDbMissing && !EarthPowerService.HasDatabase)
        {
            await DisplayAlert("Data missing", "Evocation database not found. Please ensure laby.db is copied to the app data directory.", "OK");
        }

        await LoadLookupDataAsync();
    }

    private async Task LoadLookupDataAsync()
    {
        await LoadSpellsAsync();
        await LoadMiraclesAsync();
        await LoadEvocationsAsync();
    }

    protected virtual async Task LoadSpellsAsync()
    {
        try
        {
            SpellSearchControl.ItemsSource = await BuildSpellLookupAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load spells: {ex.Message}", "OK");
        }
    }

    protected virtual async Task LoadMiraclesAsync()
    {
        try
        {
            MiracleSearchControl.ItemsSource = await BuildMiracleLookupAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load miracles: {ex.Message}", "OK");
        }
    }

    protected virtual async Task LoadEvocationsAsync()
    {
        try
        {
            EvocationSearchControl.ItemsSource = await FetchEvocationOptionsAsync(string.Empty);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load evocations: {ex.Message}", "OK");
        }
    }

    protected virtual async Task<Dictionary<string, SpellOption>> BuildSpellLookupAsync()
    {
        var spells = (await SpellService.GetAllAsync())
            .Where(ShouldIncludeSpell)
            .OrderBy(s => s.level)
            .ThenBy(s => s.name)
            .Select(s =>
            {
                var option = new SpellOption(s.name, s.level, s.isAdvanced ?? false);
                return new KeyValuePair<string, SpellOption>(FormatSpellLabel(option), option);
            })
            .GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

        return spells;
    }

    protected virtual async Task<Dictionary<string, MiracleOption>> BuildMiracleLookupAsync()
    {
        var miracles = (await MiracleService.GetAllAsync())
            .Where(ShouldIncludeMiracle)
            .OrderBy(m => m.power)
            .ThenBy(m => m.name)
            .Select(m =>
            {
                var option = new MiracleOption(m.name, m.power, m.isAdvanced);
                return new KeyValuePair<string, MiracleOption>(FormatMiracleLabel(option), option);
            })
            .GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

        return miracles;
    }

    protected virtual async Task<Dictionary<string, EvocationOption>> FetchEvocationOptionsAsync(string query)
    {
        var evocs = await EarthPowerService.SearchAsync(query ?? string.Empty, includeAdvanced: IncludeAdvancedEvocations, maxPower: MaxEvocationPower);
        return evocs
            .Where(ev => !MaxEvocationPower.HasValue || ev.power <= MaxEvocationPower.Value)
            .OrderBy(ev => ev.power)
            .ThenBy(ev => ev.name)
            .Select(ev =>
            {
                var option = new EvocationOption(ev.name, ev.power, ev.isAdvanced);
                return new KeyValuePair<string, EvocationOption>($"{ev.name} ({ev.power})", option);
            })
            .GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
    }

    protected virtual bool ShouldIncludeSpell(SpellService.SpellRaw spell) => spell.level <= 6 && (spell.isAdvanced ?? false) == false;
    protected virtual bool ShouldIncludeMiracle(MiracleService.MiracRaw miracle) => miracle.power >= 1 && miracle.power <= 5 && miracle.isAdvanced == false;

    protected virtual string FormatSpellLabel(SpellOption option)
    {
        return $"{option.Name} (lvl {option.Level})";
    }

    protected virtual string FormatMiracleLabel(MiracleOption option)
    {
        return $"{option.Name} ({option.Power})";
    }

    protected void OnLifeSelectionChanged(object sender, DictionarySelectionChangedEventArgs e)
    {
        Recalculate();
    }

    protected void Recalculate()
    {
        if (!_isReady || LifeSliderControl == null)
            return;

        DictionaryOverlayRegistry.DismissAll();

        var items = new List<ContributionRow>();
        int running = 0;

        if (SelectedSpellOption is SpellOption s && SpellCount > 0)
        {
            var cost = CalculateSpellCost(s, SpellCount);
            AddContribution(items, ref running, "spell", $"Spell: {FormatSpellLabel(s)} x{SpellCount}", cost);
        }

        if (SelectedMiracleOption is MiracleOption m && MiracleCount > 0)
        {
            var cost = CalculateMiracleCost(m, MiracleCount);
            AddContribution(items, ref running, "miracle", $"Miracle: {FormatMiracleLabel(m)} x{MiracleCount}", cost);
        }

        if (SelectedEvocationOption is EvocationOption ev && EvocationCount > 0)
        {
            var cost = CalculateEvocationCost(ev, EvocationCount);
            AddContribution(items, ref running, "evocation", $"Evocation: {ev.Name} x{EvocationCount}", cost);
        }

        if (ActiveNeuroCount > 0)
        {
            AddContribution(items, ref running, "neuro-active", $"Active neuro x{ActiveNeuroCount}", 0);
        }
        if (PassiveNeuroCount > 0)
        {
            AddContribution(items, ref running, "neuro-passive", $"Passive neuro x{PassiveNeuroCount}", 0);
        }

        if (LifeSliderControl.SelectedIndex >= 0)
        {
            var key = LifeSliderControl.SelectedKey;
            var value = LifeSliderControl.SelectedValue;
            AddContribution(items, ref running, "life", $"Life {key}", value);
        }

        if (PacChecked) AddContribution(items, ref running, "pac", "+1 PAC", PacCost);
        if (DacChecked) AddContribution(items, ref running, "dac", "+1 DAC", DacCost);
        if (MacChecked) AddContribution(items, ref running, "mac", "+1 MAC", MacCost);
        if (SacChecked) AddContribution(items, ref running, "sac", "+1 SAC", SacCost);

        var repelTarget = TrimChipLabel(RepelGoodEvilTarget) ?? RepelGoodEvilTarget;
        if (RepelGoodEvilCount > 0 && !string.IsNullOrWhiteSpace(repelTarget))
        {
            int cost = RepelGoodEvilCount * RepelGoodEvilPerUse;
            AddContribution(items, ref running, "repel-ge", $"Repel {repelTarget} x{RepelGoodEvilCount}", cost);
        }
        if (RepelLifeCount > 0)
        {
            int cost = RepelLifeCount * RepelLifePerUse;
            AddContribution(items, ref running, "repel-life", $"Repel Life x{RepelLifeCount}", cost);
        }

        if (!string.IsNullOrWhiteSpace(ApprenticeType))
        {
            var typeLabel = TrimChipLabel(ApprenticeType) ?? ApprenticeType;
            var label = $"Apprentice crafted {typeLabel}";
            if (IsApprenticeWeapon && SelectedWeapon.HasValue)
                label += $" ({EnumDisplayFormatter.FormatName(SelectedWeapon.Value.ToString())})";
            AddContribution(items, ref running, "apprentice", label, ApprenticeCost);
        }

        AddCustomContributions(items, ref running);

        _breakdown.Clear();
        foreach (var item in items)
            _breakdown.Add(item);

        TotalMp = running;
    }

    protected virtual void AddCustomContributions(List<ContributionRow> items, ref int running)
    {
    }

    private List<ContributionRow> BuildIspBreakdown(out int total)
    {
        var items = new List<ContributionRow>();
        int running = 0;

        var spellCount = Math.Max(0, SpellCount);
        if (SelectedSpellOption is SpellOption spell && spellCount > 0)
        {
            var power = Math.Max(0, spell.Level);
            var unit = spell.IsAdvanced ? 3 : 2;
            var cost = unit * power * spellCount;
            AddIspContribution(items, ref running, "spell", $"Spell: {FormatSpellLabel(spell)} x{spellCount}", cost);
        }

        var miracleCount = Math.Max(0, MiracleCount);
        if (SelectedMiracleOption is MiracleOption miracle && miracleCount > 0)
        {
            var power = Math.Max(0, miracle.Power);
            var unit = miracle.IsAdvanced ? 3 : 2;
            var cost = unit * power * miracleCount;
            AddIspContribution(items, ref running, "miracle", $"Miracle: {FormatMiracleLabel(miracle)} x{miracleCount}", cost);
        }

        var evocationCount = Math.Max(0, EvocationCount);
        if (SelectedEvocationOption is EvocationOption evocation && evocationCount > 0)
        {
            var power = Math.Max(0, evocation.Power);
            var unit = evocation.IsAdvanced ? 3 : 2;
            var cost = unit * power * evocationCount;
            AddIspContribution(items, ref running, "evocation", $"Evocation: {evocation.Name} x{evocationCount}", cost);
        }

        if (LifeSliderControl.SelectedIndex >= 0)
        {
            var key = LifeSliderControl.SelectedKey;
            var cost = GetLifeIspCost(key);
            AddIspContribution(items, ref running, "life", $"Life {key}", cost);
        }

        if (PacChecked) AddIspContribution(items, ref running, "pac", "+1 PAC", 4);
        if (DacChecked) AddIspContribution(items, ref running, "dac", "+1 DAC", 6);
        if (MacChecked) AddIspContribution(items, ref running, "mac", "+1 MAC", 8);
        if (SacChecked) AddIspContribution(items, ref running, "sac", "+1 SAC", 6);

        var repelTarget = TrimChipLabel(RepelGoodEvilTarget) ?? RepelGoodEvilTarget;
        if (RepelGoodEvilCount > 0 && !string.IsNullOrWhiteSpace(repelTarget))
        {
            int cost = RepelGoodEvilCount * 8;
            AddIspContribution(items, ref running, "repel-ge", $"Repel {repelTarget} x{RepelGoodEvilCount}", cost);
        }
        if (RepelLifeCount > 0)
        {
            int cost = RepelLifeCount * 10;
            AddIspContribution(items, ref running, "repel-life", $"Repel Life x{RepelLifeCount}", cost);
        }

        if (!string.IsNullOrWhiteSpace(ApprenticeType))
        {
            var typeLabel = TrimChipLabel(ApprenticeType) ?? ApprenticeType;
            var label = $"Apprentice crafted {typeLabel}";
            if (IsApprenticeWeapon && SelectedWeapon.HasValue)
                label += $" ({EnumDisplayFormatter.FormatName(SelectedWeapon.Value.ToString())})";
            AddIspContribution(items, ref running, "apprentice", label, 10);
        }

        AddCustomIspContributions(items, ref running);

        total = running;
        return items;
    }

    protected virtual void AddCustomIspContributions(List<ContributionRow> items, ref int running)
    {
    }

    protected virtual int GetLifeIspCost(string? lifeKey)
    {
        if (string.IsNullOrWhiteSpace(lifeKey))
            return 0;

        return LifeIspLookup.TryGetValue(lifeKey.Trim(), out var isp) ? isp : 0;
    }

    protected virtual int CalculateSpellCost(SpellOption option, int count)
    {
        int sanitizedLevel = Math.Max(0, option.Level);
        int sanitizedCount = Math.Max(0, count);
        if (sanitizedCount == 0)
            return 0;

        if (sanitizedLevel == 0)
            return 5 * sanitizedCount;

        if (sanitizedLevel <= 4)
            return 10 * sanitizedLevel * sanitizedCount;

        return 10 + 10 * sanitizedLevel * sanitizedCount;
    }

    protected virtual int CalculateMiracleCost(MiracleOption option, int count)
    {
        int sanitizedPower = Math.Max(0, option.Power);
        int sanitizedCount = Math.Max(0, count);
        if (sanitizedPower == 0 || sanitizedCount == 0)
            return 0;

        if (sanitizedPower <= 3)
            return 10 + 10 * sanitizedPower * sanitizedCount;

        return 20 + 10 * sanitizedPower * sanitizedCount;
    }

    protected virtual int CalculateEvocationCost(EvocationOption option, int count)
    {
        int sanitizedPower = Math.Max(0, option.Power);
        int sanitizedCount = Math.Max(0, count);
        if (sanitizedCount == 0)
            return 0;

        return 10 + 10 * sanitizedPower * sanitizedCount;
    }

    protected static void AddContribution(List<ContributionRow> items, ref int running, string id, string label, int cost)
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

    protected static void AddIspContribution(List<ContributionRow> items, ref int running, string id, string label, int cost, bool includeWhenZero = false)
    {
        if (cost <= 0 && !includeWhenZero) return;
        running += cost;
        items.Add(new ContributionRow
        {
            Id = $"isp-{id}",
            Text = $"{label} = {cost}",
            RunningTotal = running
        });
    }

    protected static string? TrimChipLabel(string? value)
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

public class MpSubmissionPayload
{
    public int TotalMp { get; set; }
    public List<ContributionRow> Breakdown { get; set; } = new();
    public int TotalIsp { get; set; }
    public List<ContributionRow> IspBreakdown { get; set; } = new();
}
