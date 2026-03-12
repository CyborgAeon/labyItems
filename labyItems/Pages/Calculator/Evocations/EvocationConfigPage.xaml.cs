using System.Windows.Input;
using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;

namespace labyItems.Pages.Calculator;

public partial class EvocationConfigPage : ConfigPageBase<EvocationConfig>
{
    private bool _evocationLookupLoaded;
    private EvocationSearchOption? _selectedSearchEvocation;

    public EvocationConfigPage()
    {
        AddSelectedEvocationCommand = new Command<object?>(OnEvocationResultSelected);
        EditEvocationCommand = new Command<object?>(OnEditEvocationRequested);
        ViewEvocationInfoCommand = new Command<object?>(OnEvocationInfoRequested);
        DeleteEvocationCommand = new Command<object?>(OnDeleteEvocationRequested);
        InitializeComponent();
    }

    public ICommand AddSelectedEvocationCommand { get; }
    public ICommand EditEvocationCommand { get; }
    public ICommand ViewEvocationInfoCommand { get; }
    public ICommand DeleteEvocationCommand { get; }

    public Dictionary<string, EvocationSearchOption> EvocationLookup { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public EvocationSearchOption? SelectedSearchEvocation
    {
        get => _selectedSearchEvocation;
        set
        {
            if (ReferenceEquals(_selectedSearchEvocation, value))
                return;

            _selectedSearchEvocation = value;
            OnPropertyChanged(nameof(SelectedSearchEvocation));
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_evocationLookupLoaded)
            return;

        _evocationLookupLoaded = true;
        await LoadEvocationLookupAsync();
    }

    protected override CalcResult BuildResult(EvocationConfig cfg)
    {
        var details = new Dictionary<string, object?>();
        var selectedEvocations = cfg.SelectedEvocations
            .Select(evocation => new
            {
                evocationName = evocation.EvocationName,
                power = evocation.Power,
                isAdvanced = evocation.IsAdvanced,
                fields = evocation.Fields,
                basicPerDay = evocation.BasicPerDay,
                advancedPerDay = evocation.AdvancedPerDay,
                addBasicToBaseList = evocation.AddBasicToBaseList,
                addAdvancedToBaseList = evocation.AddAdvancedToBaseList,
                addWithPrep30 = evocation.AddWithPrep30
            })
            .ToList();

        if (selectedEvocations.Count > 0)
            details["evocations"] = selectedEvocations;

        if (cfg.DrawOnEpPerDay > 0)
            details["drawOnEpPerDay"] = cfg.DrawOnEpPerDay;
        if (cfg.GeneralEpStore > 0)
            details["generalEpStore"] = cfg.GeneralEpStore;
        if (cfg.FieldEpStore > 0 && cfg.SelectedField.HasValue)
            details["fieldEpStore"] = new { field = cfg.SelectedField.Value.ToString(), amount = cfg.FieldEpStore };

        var abilityName = cfg.SelectedEvocations.Count switch
        {
            0 => "Evocation",
            1 => cfg.SelectedEvocations[0].EvocationName,
            _ => $"Evocation list ({cfg.SelectedEvocations.Count})"
        };

        return new CalcResult
        {
            AbilityType = "Evocation",
            AbilityName = abilityName,
            TotalIsp = cfg.Total,
            Summary = cfg.Breakdown,
            Details = details
        };
    }

    private async Task LoadEvocationLookupAsync()
    {
        try
        {
            var evocations = await DruidEvocationService.GetAllAsync();
            EvocationLookup = evocations
                .Where(evocation => !string.IsNullOrWhiteSpace(evocation.name))
                .OrderBy(evocation => evocation.power)
                .ThenBy(evocation => evocation.name, StringComparer.OrdinalIgnoreCase)
                .Select(evocation =>
                {
                    var option = new EvocationSearchOption(evocation);
                    var advancedToken = option.IsAdvanced ? "advanced" : "basic";
                    var fieldToken = string.IsNullOrWhiteSpace(option.FieldsSummary) ? "no field" : option.FieldsSummary;
                    var display = $"{option.Name} (P{option.Power} · {advancedToken} · {fieldToken})";
                    return new KeyValuePair<string, EvocationSearchOption>(display, option);
                })
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);

            OnPropertyChanged(nameof(EvocationLookup));
            EvocationSearch.ItemsSource = EvocationLookup;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Evocation load failed", ex.Message, "OK");
        }
    }

    private async void OnEvocationResultSelected(object? parameter)
    {
        var option = parameter as EvocationSearchOption ?? SelectedSearchEvocation;
        if (option == null)
            return;

        if (BindingContext is not EvocationConfig cfg)
            return;

        var added = cfg.TryAddEvocation(option.Evocation);
        var entry = cfg.SelectedEvocations.FirstOrDefault(evocation =>
            string.Equals(evocation.EvocationName, option.Name, StringComparison.OrdinalIgnoreCase)
            && evocation.Power == option.Power
            && evocation.IsAdvanced == option.IsAdvanced);
        if (entry == null && added)
            entry = cfg.SelectedEvocations.LastOrDefault();

        SelectedSearchEvocation = null;

        if (entry != null)
            await Navigation.PushModalAsync(new EvocationEntryConfigModalPage(entry));
    }

    private async void OnEditEvocationRequested(object? parameter)
    {
        if (parameter is not EvocationSelectionEntry entry)
            return;

        var modal = new EvocationEntryConfigModalPage(entry);
        await Navigation.PushModalAsync(modal);
    }

    private async void OnEvocationInfoRequested(object? parameter)
    {
        if (parameter is not EvocationSelectionEntry entry)
            return;

        if (string.IsNullOrWhiteSpace(entry.EvocationName))
            return;

        await Navigation.PushModalAsync(new NavigationPage(new EvocationCardPage(entry.Evocation)));
    }

    private void OnDeleteEvocationRequested(object? parameter)
    {
        if (parameter is not EvocationSelectionEntry entry)
            return;

        if (BindingContext is not EvocationConfig cfg)
            return;

        cfg.RemoveEvocation(entry);
    }
}
