using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using labyItems.Models;
using labyItems.Pages.Calculator;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Trade;

public partial class TradePage : ContentPage
{
    private bool _lookupLoaded;
    private TradeAbilityOption? _selectedAbility;

    public TradePage()
    {
        InitializeComponent();
        BindingContext = this;

        LeftEntries.CollectionChanged += OnEntriesCollectionChanged;
        RightEntries.CollectionChanged += OnEntriesCollectionChanged;
    }

    public Dictionary<string, TradeAbilityOption> AbilityLookup { get; private set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<TradeEntryVm> LeftEntries { get; } = new();
    public ObservableCollection<TradeEntryVm> RightEntries { get; } = new();

    public TradeAbilityOption? SelectedAbility
    {
        get => _selectedAbility;
        set
        {
            if (ReferenceEquals(_selectedAbility, value))
                return;

            _selectedAbility = value;
            OnPropertyChanged(nameof(SelectedAbility));
            OnPropertyChanged(nameof(CanAddSelectedAbility));
        }
    }

    public bool CanAddSelectedAbility => SelectedAbility != null;

    public bool ShowNoLeftEntries => LeftEntries.Count == 0;
    public bool ShowNoRightEntries => RightEntries.Count == 0;

    public int LeftTotal => LeftEntries.Sum(entry => entry.ResolvedCost);
    public int RightTotal => RightEntries.Sum(entry => entry.ResolvedCost);
    public int OverallDifference => LeftTotal - RightTotal;

    public string LeftHeaderText => $"{LeftTotal:N0} grulls • Δ {FormatDifference(LeftTotal - RightTotal)}";

    public string RightHeaderText => $"{RightTotal:N0} grulls • Δ {FormatDifference(RightTotal - LeftTotal)}";

    public string OverallDifferenceText =>
        $"Overall difference (left-right): {FormatDifference(OverallDifference)} grulls";

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_lookupLoaded)
            return;

        _lookupLoaded = true;
        await LoadAbilityLookupAsync();
    }

    private async Task LoadAbilityLookupAsync()
    {
        var options = new List<TradeAbilityOption>();
        var failures = new List<string>();

        try
        {
            var spells = await SpellService.GetAllAsync();
            options.AddRange(spells
                .Where(spell => !string.IsNullOrWhiteSpace(spell?.name))
                .Select(TradeAbilityOption.FromSpell));
        }
        catch (Exception ex)
        {
            failures.Add($"Spells: {ex.Message}");
        }

        try
        {
            var miracles = await MiracleService.GetAllAsync();
            options.AddRange(miracles
                .Where(miracle => !string.IsNullOrWhiteSpace(miracle?.name))
                .Select(TradeAbilityOption.FromMiracle));
        }
        catch (Exception ex)
        {
            failures.Add($"Miracles: {ex.Message}");
        }

        try
        {
            var evocations = await DruidEvocationService.GetAllAsync();
            options.AddRange(evocations
                .Where(evocation => !string.IsNullOrWhiteSpace(evocation?.name))
                .Select(TradeAbilityOption.FromEvocation));
        }
        catch (Exception ex)
        {
            failures.Add($"Evocations: {ex.Message}");
        }

        AbilityLookup = BuildLookup(options);
        OnPropertyChanged(nameof(AbilityLookup));
        AbilitySearch.ItemsSource = AbilityLookup;

        if (failures.Count > 0)
        {
            var message = string.Join("\n", failures);
            await DisplayAlert("Trade setup", $"Some lookups failed to load:\n{message}", "OK");
        }
    }

    private void OnAddLeftClicked(object sender, EventArgs e)
        => AddSelectedAbilityToSide(isLeft: true);

    private void OnAddRightClicked(object sender, EventArgs e)
        => AddSelectedAbilityToSide(isLeft: false);

    private void AddSelectedAbilityToSide(bool isLeft)
    {
        if (SelectedAbility == null)
            return;

        var entry = new TradeEntryVm(SelectedAbility);
        AddEntry(isLeft, entry);

        // Reset search selection/text so the next add starts from a blank search box.
        SelectedAbility = null;
    }

    private async void OnAddCustomItemClicked(object sender, EventArgs e)
    {
        var side = await PickSideAsync("Add custom item");
        if (!side.HasValue)
            return;

        var result = await OpenIspCalculatorAsync(existingAbilities: null);
        if (result == null)
            return;

        var entry = TradeEntryVm.CreateCustom(result);
        AddEntry(side.Value, entry);
    }

    private async void OnEditEntryClicked(object sender, EventArgs e)
    {
        var entry = ResolveEntry(sender);
        if (entry == null)
            return;

        var modal = new TradeEntryEditModalPage(entry, AbilityLookup);
        await Navigation.PushModalAsync(new NavigationPage(modal));

        var result = await modal.GetResultAsync();
        if (result.Action == TradeEntryEditAction.Cancel)
            return;

        if (result.Action == TradeEntryEditAction.Delete)
        {
            RemoveEntry(entry);
            return;
        }

        if (result.Action == TradeEntryEditAction.Save && result.Draft != null)
        {
            entry.ApplyFrom(result.Draft);
            RefreshTotals();
        }
    }

    private void AddEntry(bool isLeft, TradeEntryVm entry)
    {
        if (entry == null)
            return;

        if (isLeft)
            LeftEntries.Add(entry);
        else
            RightEntries.Add(entry);

        RefreshTotals();
    }

    private void RemoveEntry(TradeEntryVm entry)
    {
        if (entry == null)
            return;

        LeftEntries.Remove(entry);
        RightEntries.Remove(entry);
        RefreshTotals();
    }

    private async Task<bool?> PickSideAsync(string title)
    {
        var choice = await DisplayActionSheet(
            title,
            "Cancel",
            null,
            "Left",
            "Right");

        if (string.IsNullOrWhiteSpace(choice)
            || choice.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return choice.Equals("Left", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IspCalculationResult?> OpenIspCalculatorAsync(IEnumerable<CalcResult>? existingAbilities)
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

    private void OnEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var entry in e.OldItems.OfType<TradeEntryVm>())
                entry.PropertyChanged -= OnEntryPropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (var entry in e.NewItems.OfType<TradeEntryVm>())
                entry.PropertyChanged += OnEntryPropertyChanged;
        }

        RefreshTotals();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TradeEntryVm)
            return;

        RefreshTotals();
    }

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(ShowNoLeftEntries));
        OnPropertyChanged(nameof(ShowNoRightEntries));
        OnPropertyChanged(nameof(LeftTotal));
        OnPropertyChanged(nameof(RightTotal));
        OnPropertyChanged(nameof(OverallDifference));
        OnPropertyChanged(nameof(LeftHeaderText));
        OnPropertyChanged(nameof(RightHeaderText));
        OnPropertyChanged(nameof(OverallDifferenceText));
    }

    private static TradeEntryVm? ResolveEntry(object sender)
    {
        if (sender is Button button)
        {
            if (button.CommandParameter is TradeEntryVm fromParam)
                return fromParam;
            if (button.BindingContext is TradeEntryVm fromContext)
                return fromContext;
        }

        if (sender is BindableObject bindable && bindable.BindingContext is TradeEntryVm vm)
            return vm;

        return null;
    }

    private static Dictionary<string, TradeAbilityOption> BuildLookup(IEnumerable<TradeAbilityOption> source)
    {
        var lookup = new Dictionary<string, TradeAbilityOption>(StringComparer.OrdinalIgnoreCase);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in source
                     .OrderBy(entry => entry.Kind)
                     .ThenBy(entry => entry.Power)
                     .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            var baseKey = option.SearchLabel;
            if (baseKey.Length == 0)
                continue;

            var key = baseKey;
            if (seen.TryGetValue(baseKey, out var count))
            {
                count++;
                seen[baseKey] = count;
                key = $"{baseKey} #{count}";
            }
            else
            {
                seen[baseKey] = 1;
            }

            while (lookup.ContainsKey(key))
                key = $"{key}#";

            lookup[key] = option;
        }

        return lookup;
    }

    private static string FormatDifference(int value)
        => value >= 0 ? $"+{value:N0}" : $"{value:N0}";
}
