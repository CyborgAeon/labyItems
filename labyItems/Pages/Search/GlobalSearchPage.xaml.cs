using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
using NeuronicCardPage = labyItems.Pages.NeuronicCard.NeuronicCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using labyItems.Services;

namespace labyItems.Pages.Search;

public partial class GlobalSearchPage : ContentPage
{
    private readonly GlobalSearchVm _vm;
    private bool _isNavigatingBack;
    private bool _isOpeningResult;
    private TaskCompletionSource<IReadOnlyList<GlobalSearchResultVm>>? _pickTcs;
    private bool _isCompleting;

    public GlobalSearchPage() : this(null)
    {
    }

    public GlobalSearchPage(GlobalSearchPickerOptions? picker)
    {
        _vm = new GlobalSearchVm(picker);
        InitializeComponent();
        BindingContext = _vm;
    }

    public async Task<IReadOnlyList<GlobalSearchResultVm>> PickAsync(INavigation navigation)
    {
        _pickTcs = new TaskCompletionSource<IReadOnlyList<GlobalSearchResultVm>>();
        await navigation.PushAsync(this);
        return await _pickTcs.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_vm.IsPickerMode)
            return base.OnBackButtonPressed();

        _ = CompletePickerAsync(save: false);
        return true;
    }

    private async void OnPickerCancelClicked(object? sender, EventArgs e) => await CompletePickerAsync(save: false);

    private async void OnPickerSaveClicked(object? sender, EventArgs e) => await CompletePickerAsync(save: true);

    private async Task CompletePickerAsync(bool save)
    {
        if (_isCompleting)
            return;

        _isCompleting = true;
        try
        {
            IReadOnlyList<GlobalSearchResultVm> picked = save ? _vm.GetPickedResults() : Array.Empty<GlobalSearchResultVm>();
            await Navigation.PopAsync();
            _pickTcs?.TrySetResult(picked);
        }
        finally
        {
            _isCompleting = false;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.EnsureLoadedAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (_isNavigatingBack)
            return;

        _isNavigatingBack = true;
        try
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
                return;
            }

            if (Shell.Current != null)
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isNavigatingBack = false;
        }
    }

    private async void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not GlobalSearchResultVm result)
            return;

        if (_vm.IsPickerMode)
        {
            _vm.ToggleResultSelectionCommand.Execute(result);
            return;
        }

        await OpenResultAsync(result);
    }

    private async void OnResultInfoTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is GlobalSearchResultVm result)
            await OpenResultAsync(result);
    }

    private async Task OpenResultAsync(GlobalSearchResultVm result)
    {
        if (!result.CanOpenDetails || _isOpeningResult)
            return;

        _isOpeningResult = true;
        try
        {
            var page = await CreateDetailPageAsync(result);
            if (page != null)
            {
                await Navigation.PushAsync(page);
                return;
            }

            await DisplayAlert(
                "Details unavailable",
                $"Could not open details for {result.Name}.",
                "OK");
        }
        finally
        {
            _isOpeningResult = false;
        }
    }

    private static async Task<ContentPage?> CreateDetailPageAsync(GlobalSearchResultVm result)
    {
        switch (result.Kind)
        {
            case GlobalSearchKind.Ability:
                var ability = result.Ability ?? await ResolveAbilityAsync(result);
                return ability == null ? null : new AbilityCardPage(ability);

            case GlobalSearchKind.Spell:
                var spell = result.Spell ?? await ResolveSpellAsync(result);
                return spell == null ? null : new SpellCardPage(spell);

            case GlobalSearchKind.Miracle:
                var miracle = result.Miracle ?? await ResolveMiracleAsync(result);
                return miracle == null ? null : new MiracleCardPage(miracle);

            case GlobalSearchKind.Evocation:
                var evocation = result.Evocation ?? await ResolveEvocationAsync(result);
                return evocation == null ? null : new EvocationCardPage(evocation);

            case GlobalSearchKind.Neuronic:
                var neuronic = result.Neuronic ?? await ResolveNeuronicAsync(result);
                return neuronic == null ? null : new NeuronicCardPage(neuronic);

            default:
                return null;
        }
    }

    private static async Task<EvolutionService.AbilityResult?> ResolveAbilityAsync(GlobalSearchResultVm result)
    {
        foreach (var candidate in BuildLookupCandidates(result))
        {
            var resolved = await DetailCardLookupService.ResolveAbilityAsync(candidate);
            if (resolved != null)
                return resolved;
        }

        foreach (var candidate in BuildLookupCandidates(result))
        {
            var resolved = await EvolutionService.FindAbilityAsync(candidate);
            if (resolved != null)
                return resolved;
        }

        return null;
    }

    private static async Task<SpellService.SpellRaw?> ResolveSpellAsync(GlobalSearchResultVm result)
    {
        var all = await SpellService.GetAllAsync();
        return FindByName(all, spell => spell.name, BuildLookupCandidates(result));
    }

    private static async Task<MiracleService.MiracRaw?> ResolveMiracleAsync(GlobalSearchResultVm result)
    {
        var all = await MiracleService.GetAllAsync();
        return FindByName(all, miracle => miracle.name, BuildLookupCandidates(result));
    }

    private static async Task<DruidEvocationService.EvocRaw?> ResolveEvocationAsync(GlobalSearchResultVm result)
    {
        var all = await DruidEvocationService.GetAllAsync();
        return FindByName(all, evocation => evocation.name, BuildLookupCandidates(result));
    }

    private static async Task<NeuronicService.NeuronicRaw?> ResolveNeuronicAsync(GlobalSearchResultVm result)
    {
        var all = await NeuronicService.GetAllAsync();
        return FindByName(all, neuronic => neuronic.name, BuildLookupCandidates(result));
    }

    private static IReadOnlyList<string> BuildLookupCandidates(GlobalSearchResultVm result)
    {
        var candidates = new List<string>();
        AddCandidate(candidates, result.DetailKey);

        if (result.Kind == GlobalSearchKind.Ability
            && result.AbilityTable is { } table
            && !string.IsNullOrWhiteSpace(result.Name))
        {
            AddCandidate(candidates, $"{table}|{result.Name.Trim().ToLowerInvariant()}");
        }

        AddCandidate(candidates, result.Name);
        return candidates;
    }

    private static void AddCandidate(ICollection<string> candidates, string? value)
    {
        var candidate = (value ?? string.Empty).Trim();
        if (candidate.Length == 0
            || candidates.Any(existing => existing.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        candidates.Add(candidate);
    }

    private static T? FindByName<T>(
        IEnumerable<T> items,
        Func<T, string?> getName,
        IEnumerable<string> candidates)
        where T : class
    {
        var candidateList = candidates
            .Select(candidate => (candidate ?? string.Empty).Trim())
            .Where(candidate => candidate.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var candidate in candidateList)
        {
            var exact = items.FirstOrDefault(item =>
                string.Equals((getName(item) ?? string.Empty).Trim(), candidate, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;
        }

        foreach (var candidate in candidateList.Select(NormalizeLookupText).Where(candidate => candidate.Length > 0))
        {
            var normalized = items.FirstOrDefault(item =>
                string.Equals(NormalizeLookupText(getName(item)), candidate, StringComparison.OrdinalIgnoreCase));
            if (normalized != null)
                return normalized;
        }

        return null;
    }

    private static string NormalizeLookupText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();

        return new string(chars);
    }
}
