using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using labyItems.Services;

namespace labyItems.Pages.Search;

public partial class GlobalSearchPage : ContentPage
{
    private readonly GlobalSearchVm _vm = new();
    private bool _isNavigatingBack;
    private bool _isOpeningResult;

    public GlobalSearchPage()
    {
        InitializeComponent();
        BindingContext = _vm;
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

    private void OnFilterChipTapped(object? sender, TappedEventArgs e)
    {
        var chip = e.Parameter as GlobalSearchFilterChipVm
            ?? (sender as BindableObject)?.BindingContext as GlobalSearchFilterChipVm;
        if (chip == null)
            return;

        _vm.ApplyFilterChip(chip);
    }

    private async void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not GlobalSearchResultVm result
            || !result.CanOpenDetails
            || _isOpeningResult)
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
