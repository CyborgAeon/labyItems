using System.Collections.ObjectModel;
using System.Collections.Specialized;
using labyItems.Models;
using labyItems.Pages.Calculator;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Trade;

public partial class TradeEntryEditModalPage : ContentPage
{
    private readonly TradeEntryVm _draft;
    private readonly TaskCompletionSource<TradeEntryEditResult> _resultTcs = new();
    private bool _resultSet;
    private bool _isAbilitySearchFocused;
    private string _abilitySearchText = string.Empty;
    private TradeAbilityOption? _selectedAbilityOption;

    public TradeEntryEditModalPage(
        TradeEntryVm source,
        Dictionary<string, TradeAbilityOption> abilityLookup)
    {
        InitializeComponent();
        _draft = source?.Clone() ?? throw new ArgumentNullException(nameof(source));
        AbilityLookup = abilityLookup ?? new Dictionary<string, TradeAbilityOption>(StringComparer.OrdinalIgnoreCase);
        _selectedAbilityOption = ResolveMatchingOption(_draft, AbilityLookup);

        BindingContext = _draft;
        FilteredAbilityOptions.CollectionChanged += OnFilteredAbilityOptionsChanged;
        AbilitySearchText = _selectedAbilityOption?.Name ?? string.Empty;
        OverrideCostEntry.Text = _draft.CostOverride?.ToString() ?? string.Empty;
    }

    public Dictionary<string, TradeAbilityOption> AbilityLookup { get; }
    public ObservableCollection<AbilitySearchResultRow> FilteredAbilityOptions { get; } = new();

    public string AbilitySearchText
    {
        get => _abilitySearchText;
        set
        {
            var next = (value ?? string.Empty).Trim();
            if (string.Equals(_abilitySearchText, next, StringComparison.Ordinal))
                return;

            _abilitySearchText = next;
            OnPropertyChanged(nameof(AbilitySearchText));
            RefreshFilteredAbilityOptions();
        }
    }

    public bool ShowAbilityResults => _isAbilitySearchFocused && FilteredAbilityOptions.Count > 0;

    public TradeAbilityOption? SelectedAbilityOption
    {
        get => _selectedAbilityOption;
        set
        {
            if (ReferenceEquals(_selectedAbilityOption, value))
                return;

            _selectedAbilityOption = value;
            OnPropertyChanged(nameof(SelectedAbilityOption));
        }
    }

    public Task<TradeEntryEditResult> GetResultAsync() => _resultTcs.Task;

    private void OnAbilitySearchFocused(object? sender, FocusEventArgs e)
    {
        _isAbilitySearchFocused = true;
        RefreshFilteredAbilityOptions();
        OnPropertyChanged(nameof(ShowAbilityResults));
    }

    private async void OnAbilitySearchUnfocused(object? sender, FocusEventArgs e)
    {
        await Task.Delay(140);
        _isAbilitySearchFocused = false;
        OnPropertyChanged(nameof(ShowAbilityResults));
    }

    private void OnAbilitySearchResultTapped(object? sender, EventArgs e)
    {
        if (sender is not BindableObject bindable
            || bindable.BindingContext is not AbilitySearchResultRow row)
        {
            return;
        }

        SelectedAbilityOption = row.Option;
        AbilitySearchText = row.Option.Name;
        AbilitySearchEntry.Unfocus();
    }

    private async void OnOpenIspCalculatorClicked(object sender, EventArgs e)
    {
        if (!_draft.IsCustom)
            return;

        var result = await OpenIspCalculatorAsync(_draft.CustomAbilities);
        if (result == null)
            return;

        _draft.ApplyCustomIspResult(result);
    }

    private async void OnRemoveClicked(object sender, EventArgs e)
    {
        SetResult(new TradeEntryEditResult(TradeEntryEditAction.Delete, null));
        await CloseAsync();
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        SetResult(new TradeEntryEditResult(TradeEntryEditAction.Cancel, null));
        await CloseAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var overrideText = (OverrideCostEntry.Text ?? string.Empty).Trim();
        if (overrideText.Length == 0)
        {
            _draft.CostOverride = null;
        }
        else
        {
            if (!int.TryParse(overrideText, out var parsed) || parsed < 0)
            {
                await DisplayAlert("Invalid override", "Enter a whole number (0 or greater).", "OK");
                return;
            }

            _draft.CostOverride = parsed;
        }

        if (!_draft.IsCustom && SelectedAbilityOption != null && !_draft.MatchesAbility(SelectedAbilityOption))
            _draft.SetAbility(SelectedAbilityOption, preserveExistingMode: true);

        SetResult(new TradeEntryEditResult(TradeEntryEditAction.Save, _draft));
        await CloseAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_resultSet)
            SetResult(new TradeEntryEditResult(TradeEntryEditAction.Cancel, null));
    }

    private void SetResult(TradeEntryEditResult result)
    {
        if (_resultSet)
            return;

        _resultSet = true;
        _resultTcs.TrySetResult(result);
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Count > 0)
        {
            await Navigation.PopModalAsync();
            return;
        }

        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private async Task<IspCalculationResult?> OpenIspCalculatorAsync(IEnumerable<CalcResult> existingAbilities)
    {
        var existing = (existingAbilities ?? Enumerable.Empty<CalcResult>())
            .Where(ability => ability != null)
            .ToList();

        var tcs = new TaskCompletionSource<IspCalculationResult?>();
        var calculator = new IspCalculator(
            baseTotal: 0,
            existingAbilities: existing,
            onSave: result =>
            {
                tcs.TrySetResult(result);
                return Task.CompletedTask;
            });

        void HandleDisappearing(object? _, EventArgs __)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(25);
                var stillInStack = Navigation?.NavigationStack?.Contains(calculator) == true;
                if (!stillInStack && !tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            });
        }

        calculator.Disappearing += HandleDisappearing;
        try
        {
            await Navigation.PushAsync(calculator);
            return await tcs.Task;
        }
        finally
        {
            calculator.Disappearing -= HandleDisappearing;
        }
    }

    private static TradeAbilityOption? ResolveMatchingOption(
        TradeEntryVm draft,
        Dictionary<string, TradeAbilityOption> lookup)
    {
        if (draft == null || draft.IsCustom || lookup.Count == 0)
            return null;

        foreach (var option in lookup.Values)
        {
            if (draft.MatchesAbility(option))
                return option;
        }

        return null;
    }

    private void RefreshFilteredAbilityOptions()
    {
        var query = AbilitySearchText;
        IEnumerable<TradeAbilityOption> options = AbilityLookup.Values
            .Where(option => option != null)
            .GroupBy(BuildAbilityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        if (!string.IsNullOrWhiteSpace(query))
        {
            options = options.Where(option =>
                option.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.SearchLabel.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var rows = options
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Kind)
            .ThenBy(option => option.Power)
            .Take(30)
            .Select(option => new AbilitySearchResultRow(option))
            .ToList();

        FilteredAbilityOptions.Clear();
        foreach (var row in rows)
            FilteredAbilityOptions.Add(row);

        OnPropertyChanged(nameof(ShowAbilityResults));
    }

    private void OnFilteredAbilityOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(ShowAbilityResults));

    private static string BuildAbilityKey(TradeAbilityOption option)
        => $"{option.Kind}|{option.Name}|{option.Power}|{option.IsAdvanced}|{option.IsNonStandard}";

    public sealed class AbilitySearchResultRow
    {
        public AbilitySearchResultRow(TradeAbilityOption option)
        {
            Option = option ?? throw new ArgumentNullException(nameof(option));
        }

        public TradeAbilityOption Option { get; }
        public string Title => Option.Name;

        public string Meta
        {
            get
            {
                var tier = Option.IsNonStandard
                    ? "non-standard"
                    : (Option.IsAdvanced ? "advanced" : "basic");

                return Option.Kind switch
                {
                    TradeAbilityKind.Spell => $"Spell • lvl {Option.Power} • {tier}",
                    TradeAbilityKind.Miracle => $"Miracle • P{Option.Power} • {tier}",
                    TradeAbilityKind.Evocation => $"Evocation • P{Option.Power} • {tier}",
                    _ => "Ability"
                };
            }
        }
    }
}
