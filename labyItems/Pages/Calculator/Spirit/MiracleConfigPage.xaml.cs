using System.Windows.Input;
using labyItems.Models;
using labyItems.Pages.Calculator;
using labyItems.Services;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;

namespace labyItems.Pages.Configs;

public partial class MiracleConfigPage : ConfigPageBase<MiracleConfig>
{
    private bool _miracleLookupLoaded;
    private MiracleSearchOption? _selectedSearchMiracle;
    private string? _selectedTrueBelieverGuild;

    public MiracleConfigPage()
    {
        AddSelectedMiracleCommand = new Command<object?>(OnMiracleResultSelected);
        AddTrueBelieverGuildCommand = new Command<object?>(OnTrueBelieverGuildResultSelected);
        EditMiracleCommand = new Command<object?>(OnEditMiracleRequested);
        ViewMiracleInfoCommand = new Command<object?>(OnMiracleInfoRequested);
        DeleteMiracleCommand = new Command<object?>(OnDeleteMiracleRequested);
        InitializeComponent();
    }

    public ICommand AddSelectedMiracleCommand { get; }
    public ICommand AddTrueBelieverGuildCommand { get; }
    public ICommand EditMiracleCommand { get; }
    public ICommand ViewMiracleInfoCommand { get; }
    public ICommand DeleteMiracleCommand { get; }

    public Dictionary<string, MiracleSearchOption> MiracleLookup { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> TrueBelieverGuildLookup { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public MiracleSearchOption? SelectedSearchMiracle
    {
        get => _selectedSearchMiracle;
        set
        {
            if (ReferenceEquals(_selectedSearchMiracle, value))
                return;

            _selectedSearchMiracle = value;
            OnPropertyChanged(nameof(SelectedSearchMiracle));
        }
    }

    public string? SelectedTrueBelieverGuild
    {
        get => _selectedTrueBelieverGuild;
        set
        {
            if (string.Equals(_selectedTrueBelieverGuild, value, StringComparison.Ordinal))
                return;

            _selectedTrueBelieverGuild = value;
            OnPropertyChanged(nameof(SelectedTrueBelieverGuild));
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_miracleLookupLoaded)
            return;

        _miracleLookupLoaded = true;
        await LoadMiracleLookupAsync();
        await LoadTrueBelieverGuildLookupAsync();
    }

    protected override CalcResult BuildResult(MiracleConfig cfg)
    {
        var details = new Dictionary<string, object?>();
        var selectedMiracles = cfg.SelectedMiracles
            .Select(miracle => new
            {
                miracleName = miracle.MiracleName,
                power = miracle.Power,
                alignment = miracle.Alignment,
                isAdvanced = miracle.IsAdvanced,
                basicPerDay = miracle.BasicPerDay,
                advancedPerDay = miracle.AdvancedPerDay,
                innateIsMantic = miracle.InnateIsMantic,
                addBasicToBaseList = miracle.AddBasicToBaseList,
                addAdvancedToBaseList = miracle.AddAdvancedToBaseList,
                addWithPrep30 = miracle.AddWithPrep30,
                isTeachingScroll = miracle.IsTeachingScroll
            })
            .ToList();

        if (selectedMiracles.Count > 0)
            details["miracles"] = selectedMiracles;

        if (cfg.GeneralSpiritStore > 0)
            details["generalSpiritStore"] = cfg.GeneralSpiritStore;
        if (cfg.SphereSpiritStore > 0 && cfg.AdditionalSphereSelected.HasValue)
            details["sphereSpiritStore"] = new
            {
                sphere = cfg.AdditionalSphereSelected.Value.ToString(),
                amount = cfg.SphereSpiritStore
            };
        if (cfg.SpiritStoreRegenerates)
            details["spiritStoreRegenerates"] = true;
        if (cfg.TurnBasicUpTo5thMantic > 0)
            details["turnBasicUpTo5thMantic"] = cfg.TurnBasicUpTo5thMantic;
        if (cfg.TurnBasicMantic > 0)
            details["turnBasicMantic"] = cfg.TurnBasicMantic;
        if (cfg.TurnAdvancedUpTo6thMantic > 0)
            details["turnAdvancedUpTo6thMantic"] = cfg.TurnAdvancedUpTo6thMantic;
        if (cfg.TurnAdvancedAbove6thMantic > 0)
            details["turnAdvancedAbove6thMantic"] = cfg.TurnAdvancedAbove6thMantic;
        if (cfg.TrueBeliever > 0)
            details["trueBeliever"] = cfg.TrueBeliever;
        if (cfg.TrueBelieverGuilds.Count > 0)
        {
            details["trueBelieverGuilds"] = cfg.TrueBelieverGuilds
                .Where(x => x.Count > 0)
                .Select(x => new { guild = x.GuildName, count = x.Count })
                .ToList();
        }

        var abilityName = cfg.SelectedMiracles.Count switch
        {
            0 => "Miracle",
            1 => cfg.SelectedMiracles[0].MiracleName,
            _ => $"Miracle list ({cfg.SelectedMiracles.Count})"
        };

        return new CalcResult
        {
            AbilityType = "Miracle",
            AbilityName = abilityName,
            TotalIsp = cfg.Total,
            Summary = cfg.Breakdown,
            Details = details
        };
    }

    private async Task LoadMiracleLookupAsync()
    {
        try
        {
            var miracles = await MiracleService.GetAllAsync();
            MiracleLookup = miracles
                .Where(miracle => !string.IsNullOrWhiteSpace(miracle.name))
                .OrderBy(miracle => miracle.power)
                .ThenBy(miracle => miracle.name, StringComparer.OrdinalIgnoreCase)
                .Select(miracle =>
                {
                    var option = new MiracleSearchOption(miracle);
                    var advancedToken = option.IsAdvanced ? "advanced" : "basic";
                    var alignmentToken = string.IsNullOrWhiteSpace(option.Alignment) ? "ALL" : option.Alignment;
                    var display = $"{option.Name} (P{option.Power} · {advancedToken} · {alignmentToken})";
                    return new KeyValuePair<string, MiracleSearchOption>(display, option);
                })
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);

            OnPropertyChanged(nameof(MiracleLookup));
            MiracleSearch.ItemsSource = MiracleLookup;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Miracle load failed", ex.Message, "OK");
        }
    }

    private async Task LoadTrueBelieverGuildLookupAsync()
    {
        try
        {
            var guildNames = await GuildsService.GetGuildNamesAsync();
            TrueBelieverGuildLookup = guildNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name =>
                    name.TrimStart().StartsWith("church", StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : 1)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);

            OnPropertyChanged(nameof(TrueBelieverGuildLookup));
            TrueBelieverGuildSearch.ItemsSource = TrueBelieverGuildLookup;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Guild load failed", ex.Message, "OK");
        }
    }

    private async void OnMiracleResultSelected(object? parameter)
    {
        var option = parameter as MiracleSearchOption ?? SelectedSearchMiracle;
        if (option == null)
            return;

        if (BindingContext is not MiracleConfig cfg)
            return;

        var added = cfg.TryAddMiracle(option.Miracle);
        var entry = cfg.SelectedMiracles.FirstOrDefault(miracle =>
            string.Equals(miracle.MiracleName, option.Name, StringComparison.OrdinalIgnoreCase)
            && miracle.Power == option.Power
            && miracle.IsAdvanced == option.IsAdvanced);
        if (entry == null && added)
            entry = cfg.SelectedMiracles.LastOrDefault();

        SelectedSearchMiracle = null;

        if (entry != null)
            await Navigation.PushModalAsync(new MiracleEntryConfigModalPage(entry));
    }

    private void OnTrueBelieverGuildResultSelected(object? parameter)
    {
        var guildName = parameter as string ?? SelectedTrueBelieverGuild;
        if (string.IsNullOrWhiteSpace(guildName))
            return;

        if (BindingContext is not MiracleConfig cfg)
            return;

        cfg.TryAddTrueBelieverGuild(guildName);
        SelectedTrueBelieverGuild = null;
    }

    private async void OnEditMiracleRequested(object? parameter)
    {
        if (parameter is not MiracleSelectionEntry entry)
            return;

        var modal = new MiracleEntryConfigModalPage(entry);
        await Navigation.PushModalAsync(modal);
    }

    private async void OnMiracleInfoRequested(object? parameter)
    {
        if (parameter is not MiracleSelectionEntry entry)
            return;

        if (string.IsNullOrWhiteSpace(entry.MiracleName))
            return;

        await Navigation.PushModalAsync(new NavigationPage(new MiracleCardPage(entry.Miracle)));
    }

    private void OnDeleteMiracleRequested(object? parameter)
    {
        if (parameter is not MiracleSelectionEntry entry)
            return;

        if (BindingContext is not MiracleConfig cfg)
            return;

        cfg.RemoveMiracle(entry);
    }

    private void OnDeleteTrueBelieverGuildClicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not TrueBelieverGuildEntry entry)
            return;

        if (BindingContext is not MiracleConfig cfg)
            return;

        cfg.RemoveTrueBelieverGuild(entry);
    }
}
