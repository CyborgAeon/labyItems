using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;
using labyItems.Pages;
using labyItems.Services;
using Microsoft.Maui.Graphics;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;

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

    private static readonly ConcurrentDictionary<Type, Task<Dictionary<string, SpellOption>>> SpellLookupCache = new();
    private static readonly ConcurrentDictionary<Type, Task<Dictionary<string, MiracleOption>>> MiracleLookupCache = new();
    private static Task<IReadOnlyList<DruidEvocationService.EvocRaw>>? _evocationCatalogueTask;

    public ObservableCollection<ContributionRow> Breakdown => _breakdown;

    public ICommand ContinueCommand => new Command(async () => await HandleSubmitAsync());
    public Func<MpSubmissionPayload, Task>? CharacterItemSubmitHandler { get; set; }

    public int TotalMp
    {
        get => _totalMp;
        private set => SetProperty(ref _totalMp, value);
    }
    private int _totalMp;

    private static readonly Color RowEvenColor = Colors.White;
    private static readonly Color RowOddColor = Color.FromArgb("#F6F6F6");

    public ObservableCollection<SelectedItemUseVm<SpellOption>> SpellSelections { get; } = new();
    public ObservableCollection<SelectedItemUseVm<MiracleOption>> MiracleSelections { get; } = new();
    public ObservableCollection<SelectedItemUseVm<EvocationOption>> EvocationSelections { get; } = new();

    public bool HasSpellSelections => SpellSelections.Count > 0;
    public bool HasMiracleSelections => MiracleSelections.Count > 0;
    public bool HasEvocationSelections => EvocationSelections.Count > 0;

    public ICommand AddSelectedSpellCommand { get; }
    public ICommand AddSelectedMiracleCommand { get; }
    public ICommand AddSelectedEvocationCommand { get; }
    public ICommand EditSpellSelectionCommand { get; }
    public ICommand EditMiracleSelectionCommand { get; }
    public ICommand EditEvocationSelectionCommand { get; }
    public ICommand ViewSpellInfoCommand { get; }
    public ICommand ViewMiracleInfoCommand { get; }
    public ICommand ViewEvocationInfoCommand { get; }
    public ICommand DeleteSpellSelectionCommand { get; }
    public ICommand DeleteMiracleSelectionCommand { get; }
    public ICommand DeleteEvocationSelectionCommand { get; }

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

    public bool DbEnabled => true;

    protected MpCalculatorPageBase()
    {
        AddSelectedSpellCommand = new Command<object?>(OnSpellResultSelected);
        AddSelectedMiracleCommand = new Command<object?>(OnMiracleResultSelected);
        AddSelectedEvocationCommand = new Command<object?>(OnEvocationResultSelected);
        EditSpellSelectionCommand = new Command<object?>(OnEditSpellSelectionRequested);
        EditMiracleSelectionCommand = new Command<object?>(OnEditMiracleSelectionRequested);
        EditEvocationSelectionCommand = new Command<object?>(OnEditEvocationSelectionRequested);
        ViewSpellInfoCommand = new Command<object?>(OnViewSpellInfoRequested);
        ViewMiracleInfoCommand = new Command<object?>(OnViewMiracleInfoRequested);
        ViewEvocationInfoCommand = new Command<object?>(OnViewEvocationInfoRequested);
        DeleteSpellSelectionCommand = new Command<object?>(OnDeleteSpellSelectionRequested);
        DeleteMiracleSelectionCommand = new Command<object?>(OnDeleteMiracleSelectionRequested);
        DeleteEvocationSelectionCommand = new Command<object?>(OnDeleteEvocationSelectionRequested);

        SpellSelections.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasSpellSelections));
            RefreshSelectionRowStyles(SpellSelections);
            Recalculate();
        };

        MiracleSelections.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasMiracleSelections));
            RefreshSelectionRowStyles(MiracleSelections);
            Recalculate();
        };

        EvocationSelections.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasEvocationSelections));
            RefreshSelectionRowStyles(EvocationSelections);
            Recalculate();
        };

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
        _ = PrimeLookupCachesAsync();
    }

    private async Task HandleSubmitAsync()
    {
        if (TotalMp > 500)
        {
            await DisplayAlert("Limit reached", "Must not exceed limit", "OK");
            return;
        }

        var payload = BuildSubmissionPayload();
        if (CharacterItemSubmitHandler != null)
        {
            try
            {
                await CharacterItemSubmitHandler(payload);
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                RuntimeLog.Write("MP_SAVE_CALLBACK", "Character MP item save callback failed.", ex);
                await DisplayAlert("Save failed", ex.Message, "OK");
            }

            return;
        }

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
        await Task.Yield();

        if (ShouldWarnWhenDbMissing && !await HasEvocationDataAsync())
        {
            await DisplayAlert("Data missing", "Evocation data not found. Please ensure packaged data or laby.db is available.", "OK");
        }

        await LoadLookupDataAsync();
    }

    private static async Task<bool> HasEvocationDataAsync()
    {
        try
        {
            var all = await EvocationCatalogService.GetAllAsync();
            return all.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadLookupDataAsync()
    {
        await Task.WhenAll(
            LoadSpellsAsync(),
            LoadMiraclesAsync(),
            LoadEvocationsAsync());
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
        var pageType = GetType();
        var lookupTask = SpellLookupCache.GetOrAdd(pageType, _ => BuildSpellLookupCoreAsync());
        try
        {
            return await lookupTask;
        }
        catch
        {
            SpellLookupCache.TryRemove(pageType, out _);
            throw;
        }
    }

    protected virtual async Task<Dictionary<string, MiracleOption>> BuildMiracleLookupAsync()
    {
        var pageType = GetType();
        var lookupTask = MiracleLookupCache.GetOrAdd(pageType, _ => BuildMiracleLookupCoreAsync());
        try
        {
            return await lookupTask;
        }
        catch
        {
            MiracleLookupCache.TryRemove(pageType, out _);
            throw;
        }
    }

    protected virtual async Task<Dictionary<string, EvocationOption>> FetchEvocationOptionsAsync(string query)
    {
        var token = (query ?? string.Empty).Trim();
        var evocations = await GetEvocationCatalogueAsync();
        return evocations
            .Where(ev => !string.IsNullOrWhiteSpace(ev?.name))
            .Where(ev => IncludeAdvancedEvocations || !ev.isAdvanced)
            .Where(ev => !MaxEvocationPower.HasValue || ev.power <= MaxEvocationPower.Value)
            .Where(ev =>
                token.Length == 0
                || (ev.name ?? string.Empty).Contains(token, StringComparison.OrdinalIgnoreCase))
            .OrderBy(ev => ev.power)
            .ThenBy(ev => ev.name, StringComparer.OrdinalIgnoreCase)
            .Select(ev =>
            {
                var option = new EvocationOption(ev.name, ev.power, ev.isAdvanced);
                return new KeyValuePair<string, EvocationOption>($"{ev.name} ({ev.power})", option);
            })
            .GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<DruidEvocationService.EvocRaw>> GetEvocationCatalogueAsync()
    {
        var task = _evocationCatalogueTask ??= EvocationCatalogService.GetAllAsync();
        try
        {
            return await task;
        }
        catch
        {
            if (ReferenceEquals(_evocationCatalogueTask, task))
                _evocationCatalogueTask = null;
            throw;
        }
    }

    private Task PrimeLookupCachesAsync()
        => Task.WhenAll(
            BuildSpellLookupAsync(),
            BuildMiracleLookupAsync(),
            GetEvocationCatalogueAsync());

    private async Task<Dictionary<string, SpellOption>> BuildSpellLookupCoreAsync()
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

    private async Task<Dictionary<string, MiracleOption>> BuildMiracleLookupCoreAsync()
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

    private void OnSpellResultSelected(object? parameter)
    {
        if (parameter is SpellOption option)
            AddOrIncrementSpell(option);
    }

    private void OnMiracleResultSelected(object? parameter)
    {
        if (parameter is MiracleOption option)
            AddOrIncrementMiracle(option);
    }

    private void OnEvocationResultSelected(object? parameter)
    {
        if (parameter is EvocationOption option)
            AddOrIncrementEvocation(option);
    }

    private void AddOrIncrementSpell(SpellOption option)
    {
        AddOrIncrementSelection(SpellSelections, option, FormatSpellLabel, OnSpellSelectionUsesChanged);
    }

    private void AddOrIncrementMiracle(MiracleOption option)
    {
        AddOrIncrementSelection(MiracleSelections, option, FormatMiracleLabel, OnMiracleSelectionUsesChanged);
    }

    private void AddOrIncrementEvocation(EvocationOption option)
    {
        AddOrIncrementSelection(EvocationSelections, option, FormatEvocationLabel, OnEvocationSelectionUsesChanged);
    }

    private void OnSpellSelectionUsesChanged(SelectedItemUseVm<SpellOption> entry)
    {
        if (entry.Uses < 1)
            SpellSelections.Remove(entry);

        Recalculate();
    }

    private void OnMiracleSelectionUsesChanged(SelectedItemUseVm<MiracleOption> entry)
    {
        if (entry.Uses < 1)
            MiracleSelections.Remove(entry);

        Recalculate();
    }

    private void OnEvocationSelectionUsesChanged(SelectedItemUseVm<EvocationOption> entry)
    {
        if (entry.Uses < 1)
            EvocationSelections.Remove(entry);

        Recalculate();
    }

    private static void AddOrIncrementSelection<TOption>(
        ObservableCollection<SelectedItemUseVm<TOption>> collection,
        TOption option,
        Func<TOption, string> displayNameFactory,
        Action<SelectedItemUseVm<TOption>> onUsesChanged)
        where TOption : struct
    {
        if (!HasNamedValue(option))
            return;

        var existing = collection.FirstOrDefault(s => s.Option.Equals(option));
        if (existing != null)
        {
            existing.Uses++;
            return;
        }

        collection.Add(new SelectedItemUseVm<TOption>(
            option,
            displayNameFactory(option),
            onUsesChanged));
    }

    private static bool HasNamedValue<TOption>(TOption option)
        where TOption : struct
    {
        var nameProperty = typeof(TOption).GetProperty("Name");
        if (nameProperty?.GetValue(option) is string name)
            return !string.IsNullOrWhiteSpace(name);

        return true;
    }

    private async void OnEditSpellSelectionRequested(object? parameter)
    {
        if (parameter is not SelectedItemUseVm<SpellOption> entry)
            return;

        var page = new MpSelectionEditorPage<SpellOption>(
            title: "Edit spell",
            initialOption: entry.Option,
            initialUses: entry.Uses,
            placeholderText: "Search spells",
            selectionDisplayMemberPath: nameof(SpellOption.Name),
            displayNameFactory: FormatSpellLabel,
            loadOptionsAsync: BuildSpellLookupAsync,
            remoteSearchProvider: null,
            applyChanges: (option, uses) => ApplyEditedSelection(SpellSelections, entry, option, uses, FormatSpellLabel));

        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private async void OnEditMiracleSelectionRequested(object? parameter)
    {
        if (parameter is not SelectedItemUseVm<MiracleOption> entry)
            return;

        var page = new MpSelectionEditorPage<MiracleOption>(
            title: "Edit miracle",
            initialOption: entry.Option,
            initialUses: entry.Uses,
            placeholderText: "Search miracles",
            selectionDisplayMemberPath: nameof(MiracleOption.Name),
            displayNameFactory: FormatMiracleLabel,
            loadOptionsAsync: BuildMiracleLookupAsync,
            remoteSearchProvider: null,
            applyChanges: (option, uses) => ApplyEditedSelection(MiracleSelections, entry, option, uses, FormatMiracleLabel));

        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private async void OnEditEvocationSelectionRequested(object? parameter)
    {
        if (parameter is not SelectedItemUseVm<EvocationOption> entry)
            return;

        var page = new MpSelectionEditorPage<EvocationOption>(
            title: "Edit evocation",
            initialOption: entry.Option,
            initialUses: entry.Uses,
            placeholderText: "Search evocations",
            selectionDisplayMemberPath: nameof(EvocationOption.Name),
            displayNameFactory: FormatEvocationLabel,
            loadOptionsAsync: () => FetchEvocationOptionsAsync(string.Empty),
            remoteSearchProvider: FetchEvocationOptionsAsync,
            applyChanges: (option, uses) => ApplyEditedSelection(EvocationSelections, entry, option, uses, FormatEvocationLabel));

        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private async void OnViewSpellInfoRequested(object? parameter)
    {
        if (parameter is not SelectedItemUseVm<SpellOption> entry)
            return;

        var spell = await ResolveSpellAsync(entry.Option);
        if (spell == null)
            return;

        await Navigation.PushModalAsync(new NavigationPage(new SpellCardPage(spell)));
    }

    private async void OnViewMiracleInfoRequested(object? parameter)
    {
        if (parameter is not SelectedItemUseVm<MiracleOption> entry)
            return;

        var miracle = await ResolveMiracleAsync(entry.Option);
        if (miracle == null)
            return;

        await Navigation.PushModalAsync(new NavigationPage(new MiracleCardPage(miracle)));
    }

    private async void OnViewEvocationInfoRequested(object? parameter)
    {
        if (parameter is not SelectedItemUseVm<EvocationOption> entry)
            return;

        var evocation = await ResolveEvocationAsync(entry.Option);
        if (evocation == null)
            return;

        await Navigation.PushModalAsync(new NavigationPage(new EvocationCardPage(evocation)));
    }

    private void OnDeleteSpellSelectionRequested(object? parameter)
    {
        if (parameter is SelectedItemUseVm<SpellOption> entry)
            SpellSelections.Remove(entry);
    }

    private void OnDeleteMiracleSelectionRequested(object? parameter)
    {
        if (parameter is SelectedItemUseVm<MiracleOption> entry)
            MiracleSelections.Remove(entry);
    }

    private void OnDeleteEvocationSelectionRequested(object? parameter)
    {
        if (parameter is SelectedItemUseVm<EvocationOption> entry)
            EvocationSelections.Remove(entry);
    }

    private static void ApplyEditedSelection<TOption>(
        ObservableCollection<SelectedItemUseVm<TOption>> collection,
        SelectedItemUseVm<TOption> target,
        TOption updatedOption,
        int updatedUses,
        Func<TOption, string> displayNameFactory)
        where TOption : struct
    {
        var sanitizedUses = Math.Max(1, updatedUses);
        var duplicate = collection.FirstOrDefault(item =>
            !ReferenceEquals(item, target) && item.Option.Equals(updatedOption));

        if (duplicate != null)
        {
            duplicate.Uses += sanitizedUses;
            collection.Remove(target);
            return;
        }

        target.Update(updatedOption, displayNameFactory(updatedOption), sanitizedUses);
    }

    private static void RefreshSelectionRowStyles<TOption>(ObservableCollection<SelectedItemUseVm<TOption>> collection)
        where TOption : struct
    {
        for (var i = 0; i < collection.Count; i++)
            collection[i].RowBackgroundColor = i % 2 == 0 ? RowEvenColor : RowOddColor;
    }

    private async Task<SpellService.SpellRaw?> ResolveSpellAsync(SpellOption option)
    {
        var spells = await SpellService.GetAllAsync();
        return spells.FirstOrDefault(spell =>
            string.Equals(spell.name, option.Name, StringComparison.OrdinalIgnoreCase)
            && spell.level == option.Level
            && (spell.isAdvanced ?? false) == option.IsAdvanced);
    }

    private async Task<MiracleService.MiracRaw?> ResolveMiracleAsync(MiracleOption option)
    {
        var miracles = await MiracleService.GetAllAsync();
        return miracles.FirstOrDefault(miracle =>
            string.Equals(miracle.name, option.Name, StringComparison.OrdinalIgnoreCase)
            && miracle.power == option.Power
            && miracle.isAdvanced == option.IsAdvanced);
    }

    private async Task<DruidEvocationService.EvocRaw?> ResolveEvocationAsync(EvocationOption option)
    {
        var evocations = await GetEvocationCatalogueAsync();
        return evocations.FirstOrDefault(evocation =>
            string.Equals(evocation.name, option.Name, StringComparison.OrdinalIgnoreCase)
            && evocation.power == option.Power
            && evocation.isAdvanced == option.IsAdvanced);
    }

    protected virtual string FormatEvocationLabel(EvocationOption option)
        => $"{option.Name} ({option.Power}{(option.IsAdvanced ? " adv" : string.Empty)})";

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

        for (var i = 0; i < SpellSelections.Count; i++)
        {
            var selection = SpellSelections[i];
            var count = Math.Max(0, selection.Uses);
            if (count == 0)
                continue;

            var option = selection.Option;
            var cost = CalculateSpellCost(option, count);
            AddContribution(items, ref running, $"spell-{i}", $"Spell: {FormatSpellLabel(option)} x{count}", cost);
        }

        for (var i = 0; i < MiracleSelections.Count; i++)
        {
            var selection = MiracleSelections[i];
            var count = Math.Max(0, selection.Uses);
            if (count == 0)
                continue;

            var option = selection.Option;
            var cost = CalculateMiracleCost(option, count);
            AddContribution(items, ref running, $"miracle-{i}", $"Miracle: {FormatMiracleLabel(option)} x{count}", cost);
        }

        for (var i = 0; i < EvocationSelections.Count; i++)
        {
            var selection = EvocationSelections[i];
            var count = Math.Max(0, selection.Uses);
            if (count == 0)
                continue;

            var option = selection.Option;
            var cost = CalculateEvocationCost(option, count);
            AddContribution(items, ref running, $"evocation-{i}", $"Evocation: {option.Name} x{count}", cost);
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

        for (var i = 0; i < SpellSelections.Count; i++)
        {
            var selection = SpellSelections[i];
            var count = Math.Max(0, selection.Uses);
            if (count == 0)
                continue;

            var spell = selection.Option;
            var power = Math.Max(0, spell.Level);
            var unit = spell.IsAdvanced ? 3 : 2;
            var cost = unit * power * count;
            AddIspContribution(items, ref running, $"spell-{i}", $"Spell: {FormatSpellLabel(spell)} x{count}", cost);
        }

        for (var i = 0; i < MiracleSelections.Count; i++)
        {
            var selection = MiracleSelections[i];
            var count = Math.Max(0, selection.Uses);
            if (count == 0)
                continue;

            var miracle = selection.Option;
            var power = Math.Max(0, miracle.Power);
            var unit = miracle.IsAdvanced ? 3 : 2;
            var cost = unit * power * count;
            AddIspContribution(items, ref running, $"miracle-{i}", $"Miracle: {FormatMiracleLabel(miracle)} x{count}", cost);
        }

        for (var i = 0; i < EvocationSelections.Count; i++)
        {
            var selection = EvocationSelections[i];
            var count = Math.Max(0, selection.Uses);
            if (count == 0)
                continue;

            var evocation = selection.Option;
            var power = Math.Max(0, evocation.Power);
            var unit = evocation.IsAdvanced ? 3 : 2;
            var cost = unit * power * count;
            AddIspContribution(items, ref running, $"evocation-{i}", $"Evocation: {evocation.Name} x{count}", cost);
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

        var trimmed = value.Trim();
        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace <= 0 || firstSpace >= trimmed.Length - 1)
            return trimmed;

        var firstToken = trimmed[..firstSpace];
        var hasDecorativePrefix = firstToken.Any(ch => !char.IsLetterOrDigit(ch));
        return hasDecorativePrefix ? trimmed[(firstSpace + 1)..].Trim() : trimmed;
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

public sealed class SelectedItemUseVm<TOption> : INotifyPropertyChanged, IConfigSelectionListItem where TOption : struct
{
    private readonly Action<SelectedItemUseVm<TOption>> _onUsesChanged;
    private string _displayName;
    private Color _rowBackgroundColor = Colors.White;

    public TOption Option { get; private set; }
    public string DisplayText => DisplayName;
    public string DisplayName
    {
        get => _displayName;
        private set
        {
            if (_displayName == value)
                return;

            _displayName = value;
            OnPropertyChanged();
        }
    }

    private int _uses;
    public int Uses
    {
        get => _uses;
        set
        {
            var sanitized = Math.Max(0, value);
            if (_uses == sanitized)
                return;

            _uses = sanitized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InlineSummary));
            _onUsesChanged(this);
        }
    }

    public string InlineSummary => Uses == 1 ? "1 use assigned." : $"{Uses} uses assigned.";

    public Color RowBackgroundColor
    {
        get => _rowBackgroundColor;
        set
        {
            if (_rowBackgroundColor == value)
                return;

            _rowBackgroundColor = value;
            OnPropertyChanged();
        }
    }

    public ICommand IncrementCommand { get; }
    public ICommand DecrementCommand { get; }

    public SelectedItemUseVm(
        TOption option,
        string displayText,
        Action<SelectedItemUseVm<TOption>> onUsesChanged,
        int initialUses = 1)
    {
        Option = option;
        _displayName = displayText ?? string.Empty;
        _onUsesChanged = onUsesChanged ?? (_ => { });
        _uses = Math.Max(1, initialUses);

        IncrementCommand = new Command(() => Uses++);
        DecrementCommand = new Command(() => Uses = Math.Max(0, Uses - 1));
    }

    public void Update(TOption option, string displayText, int uses)
    {
        Option = option;
        DisplayName = displayText ?? string.Empty;
        Uses = Math.Max(1, uses);
        OnPropertyChanged(nameof(Option));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class MpSubmissionPayload
{
    public int TotalMp { get; set; }
    public List<ContributionRow> Breakdown { get; set; } = new();
    public int TotalIsp { get; set; }
    public List<ContributionRow> IspBreakdown { get; set; } = new();
}
