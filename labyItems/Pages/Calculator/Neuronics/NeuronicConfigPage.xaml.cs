using System.Windows.Input;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;
using labyItems.Services;
using NeuronicCardPage = labyItems.Pages.NeuronicCard.NeuronicCard;

namespace labyItems.Pages.Calculator;

public partial class NeuronicConfigPage : ConfigPageBase<NeuronicConfig>
{
    private bool _neuronicLookupLoaded;
    private bool _isLoading;
    private NeuronicSearchOption? _selectedSearchNeuronic;

    public NeuronicConfigPage()
    {
        AddSelectedNeuronicCommand = new Command<object?>(OnNeuronicResultSelected);
        EditNeuronicCommand = new Command<object?>(OnEditNeuronicRequested);
        ViewNeuronicInfoCommand = new Command<object?>(OnNeuronicInfoRequested);
        DeleteNeuronicCommand = new Command<object?>(OnDeleteNeuronicRequested);
        InitializeComponent();
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddSelectedNeuronicCommand { get; }
    public ICommand EditNeuronicCommand { get; }
    public ICommand ViewNeuronicInfoCommand { get; }
    public ICommand DeleteNeuronicCommand { get; }

    public Dictionary<string, NeuronicSearchOption> NeuronicLookup { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public NeuronicSearchOption? SelectedSearchNeuronic
    {
        get => _selectedSearchNeuronic;
        set
        {
            if (ReferenceEquals(_selectedSearchNeuronic, value))
                return;

            _selectedSearchNeuronic = value;
            OnPropertyChanged(nameof(SelectedSearchNeuronic));
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_neuronicLookupLoaded)
            return;

        _neuronicLookupLoaded = true;
        await LoadNeuronicLookupAsync();
    }

    protected override CalcResult BuildResult(NeuronicConfig cfg)
    {
        var details = new Dictionary<string, object?>();
        var selectedNeuronics = cfg.SelectedNeuronics
            .Select(neuronic => new
            {
                neuronicName = neuronic.NeuronicName,
                power = neuronic.Power,
                type = neuronic.Type == NeuroOptionType.None ? "None" : neuronic.Type.ToString(),
                usesPerDay = neuronic.UsesPerDay,
                addToBaseList = neuronic.AddToBaseList
            })
            .ToList();

        if (selectedNeuronics.Count > 0)
            details["neuronics"] = selectedNeuronics;

        var abilityName = cfg.SelectedNeuronics.Count switch
        {
            0 => "Neuronic",
            1 => cfg.SelectedNeuronics[0].NeuronicName,
            _ => $"Neuronic list ({cfg.SelectedNeuronics.Count})"
        };

        return new CalcResult
        {
            AbilityType = "Neuronic",
            AbilityName = abilityName,
            TotalIsp = cfg.Total,
            Summary = cfg.Breakdown,
            Details = details
        };
    }

    private async Task LoadNeuronicLookupAsync()
    {
        IsLoading = true;
        try
        {
            var neuronics = await NeuronicService.GetAllAsync();
            NeuronicLookup = neuronics
                .Where(neuronic => !string.IsNullOrWhiteSpace(neuronic.name))
                .OrderBy(neuronic => neuronic.power)
                .ThenBy(neuronic => neuronic.name, StringComparer.OrdinalIgnoreCase)
                .Select(neuronic =>
                {
                    var option = new NeuronicSearchOption(neuronic);
                    var display = $"{option.Name} ({option.Power} TBLP - {option.TypeLabel})";
                    return new KeyValuePair<string, NeuronicSearchOption>(display, option);
                })
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);

            OnPropertyChanged(nameof(NeuronicLookup));
            NeuronicSearch.ItemsSource = NeuronicLookup;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Neuronic load failed", ex.Message, "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnNeuronicResultSelected(object? parameter)
    {
        var option = parameter as NeuronicSearchOption ?? SelectedSearchNeuronic;
        if (option == null)
            return;

        if (BindingContext is not NeuronicConfig cfg)
            return;

        cfg.TryAddNeuronic(option.Neuronic);
        SelectedSearchNeuronic = null;
    }

    private async void OnEditNeuronicRequested(object? parameter)
    {
        if (parameter is not NeuronicSelectionEntry entry)
            return;

        var modal = new NeuronicEntryConfigModalPage(entry);
        await Navigation.PushModalAsync(modal);
    }

    private async void OnNeuronicInfoRequested(object? parameter)
    {
        if (parameter is not NeuronicSelectionEntry entry)
            return;

        if (string.IsNullOrWhiteSpace(entry.NeuronicName))
            return;

        await Navigation.PushModalAsync(new NavigationPage(new NeuronicCardPage(entry.Neuronic)));
    }

    private void OnDeleteNeuronicRequested(object? parameter)
    {
        if (parameter is not NeuronicSelectionEntry entry)
            return;

        if (BindingContext is not NeuronicConfig cfg)
            return;

        cfg.RemoveNeuronic(entry);
    }
}
