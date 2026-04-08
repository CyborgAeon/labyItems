using System.ComponentModel;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;

namespace labyItems.Pages.Characters;

public partial class GuildCardView : ContentView
{
    public static readonly BindableProperty ToggleExpandedCommandProperty =
        BindableProperty.Create(nameof(ToggleExpandedCommand), typeof(ICommand), typeof(GuildCardView));

    public ICommand ToggleExpandedCommand
    {
        get => (ICommand)GetValue(ToggleExpandedCommandProperty);
        set => SetValue(ToggleExpandedCommandProperty, value);
    }

    public static readonly BindableProperty SelectCommandProperty =
        BindableProperty.Create(nameof(SelectCommand), typeof(ICommand), typeof(GuildCardView));

    public ICommand SelectCommand
    {
        get => (ICommand)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public GuildCardView()
    {
        InitializeComponent();
    }

    private INotifyPropertyChanged? _boundVm;

    protected override void OnBindingContextChanged()
    {
        if (_boundVm != null)
            _boundVm.PropertyChanged -= OnVmPropertyChanged;

        base.OnBindingContextChanged();

        _boundVm = BindingContext as INotifyPropertyChanged;
        if (_boundVm != null)
            _boundVm.PropertyChanged += OnVmPropertyChanged;

        ApplyExpandedState((BindingContext as GuildCardVm)?.IsExpanded == true);
    }

    protected override void OnParentSet()
    {
        if (Parent == null)
        {
            if (_boundVm != null)
                _boundVm.PropertyChanged -= OnVmPropertyChanged;
            _boundVm = null;
        }

        base.OnParentSet();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuildCardVm.IsSelected))
            Dispatcher.Dispatch(async () => await AnimateSelectionAsync());

        if (e.PropertyName == nameof(GuildCardVm.IsExpanded))
            Dispatcher.Dispatch(() =>
            {
                if (BindingContext is GuildCardVm vm)
                    ApplyExpandedState(vm.IsExpanded);
            });
    }

    private async Task AnimateSelectionAsync()
    {
        if (BindingContext is not GuildCardVm vm) return;

        if (vm.IsSelected)
        {
            await CardFrame.ScaleTo(1.02, 110, Easing.CubicOut);
            await CardFrame.ScaleTo(1.0, 110, Easing.CubicOut);
        }
    }

    private void ApplyExpandedState(bool expand)
    {
        if (ExpandedContent == null)
            return;

        ExpandedContent.AbortAnimation("expand");
        ExpandedContent.IsVisible = expand;
        ExpandedContent.HeightRequest = -1;
        ExpandedContent.Opacity = 1;
    }

    private async void OnGuildMiracleInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not GuildMiracleRowVm row)
            return;

        var miracleName = (row.Name ?? string.Empty).Trim();
        if (miracleName.Length == 0)
            return;

        var miracles = await MiracleService.GetAllAsync();
        var miracle = miracles.FirstOrDefault(m =>
            string.Equals(m?.name ?? string.Empty, miracleName, StringComparison.OrdinalIgnoreCase));

        if (miracle == null)
            return;

        var navigation = Navigation;
        if (navigation == null)
            return;

        await navigation.PushModalAsync(new NavigationPage(new MiracleCardPage(miracle)));
    }

    private async void OnGuildBenefitInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not GuildBenefitRowVm row)
            return;

        var detailOptions = BuildBenefitDetailOptions(row);
        if (detailOptions.Count == 0)
            return;

        GuildBenefitDetailOption? pickedOption;
        var hostPage = ResolveHostPage();
        if (detailOptions.Count == 1)
        {
            pickedOption = detailOptions[0];
        }
        else
        {
            if (hostPage == null)
                return;

            var labels = detailOptions
                .Select(option => option.DisplayName)
                .ToArray();
            var picked = await hostPage.DisplayActionSheet("Guild benefit details", "Cancel", null, labels);
            if (string.IsNullOrWhiteSpace(picked) || picked.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                return;

            pickedOption = detailOptions.FirstOrDefault(option =>
                option.DisplayName.Equals(picked, StringComparison.OrdinalIgnoreCase));
            if (pickedOption == null)
                return;
        }

        var abilityResult = await ResolveAbilityFromDefinitionAsync(pickedOption.Ability);
        if (abilityResult == null)
            return;

        var navigation = ResolveNavigation();
        if (navigation == null)
            return;

        await navigation.PushAsync(new AbilityCardPage(abilityResult));
    }

    private INavigation? ResolveNavigation()
    {
        if (Navigation?.NavigationStack is { Count: > 0 })
            return Navigation;

        if (Shell.Current?.Navigation is { } shellNav)
            return shellNav;

        return Application.Current?.MainPage?.Navigation;
    }

    private Page? ResolveHostPage()
    {
        Element? current = this;
        while (current != null)
        {
            if (current is Page page)
                return page;

            current = current.Parent;
        }

        return Application.Current?.MainPage;
    }

    private static List<GuildBenefitDetailOption> BuildBenefitDetailOptions(GuildBenefitRowVm row)
    {
        var options = new List<GuildBenefitDetailOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ability in row.Abilities ?? new List<AbilityDefinition>())
        {
            if (ability == null)
                continue;

            var displayName = (ability.Name ?? string.Empty).Trim();
            if (displayName.Length == 0)
                displayName = "Ability";

            var dedupeKey = $"{displayName}|{ability.Key}|{ability.AbilityRef}";
            if (!seen.Add(dedupeKey))
                continue;

            options.Add(new GuildBenefitDetailOption(displayName, ability));
        }

        return options;
    }

    private static async Task<EvolutionService.AbilityResult?> ResolveAbilityFromDefinitionAsync(AbilityDefinition? definition)
    {
        if (definition == null)
            return null;

        foreach (var lookupKey in EnumerateAbilityLookupKeys(definition))
        {
            var resolved = await AbilityDetailsLookupService.FindByIndexAsync(lookupKey);
            if (resolved != null)
                return resolved;

            var specialisationMatch = await DetailCardLookupService.FindSpecialisationAbilityAsync(lookupKey);
            if (specialisationMatch.Ability != null)
                return ToAbilityResult(specialisationMatch.Ability, specialisationMatch.Key, lookupKey);
        }

        var fallbackKey = (definition.Key ?? definition.AbilityRef ?? definition.Name ?? string.Empty).Trim();
        return ToAbilityResult(definition, definition.Key ?? definition.AbilityRef ?? string.Empty, fallbackKey);
    }

    private static IEnumerable<string> EnumerateAbilityLookupKeys(AbilityDefinition definition)
    {
        var candidates = new[]
        {
            definition.Key,
            definition.AbilityRef,
            definition.Name,
            definition.UpdateKey,
            definition.BattleboardNameOverride,
            definition.OverwriteKey
        };

        foreach (var candidate in candidates)
        {
            var normalized = (candidate ?? string.Empty).Trim();
            if (normalized.Length == 0)
                continue;

            yield return normalized;
        }
    }

    private static EvolutionService.AbilityResult ToAbilityResult(
        AbilityDefinition source,
        string resolvedKey,
        string requestedKey)
    {
        var name = (source.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (resolvedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = (requestedKey ?? string.Empty).Trim();
        if (name.Length == 0)
            name = "Ability";

        return new EvolutionService.AbilityResult
        {
            Index = name,
            Description = source.Effect ?? string.Empty,
            Cost = 0,
            Table = 0,
            AbilityRef = (source.Key ?? source.AbilityRef ?? string.Empty).Trim(),
            Available = source.Source ?? "ALL",
            CanBuyMultiple = false,
            PreReqs = source.PreReqs is { Count: > 0 } preReqs
                ? preReqs
                : Array.Empty<string>(),
            MaxAvailable = source.Count
        };
    }

    private static string BuildOptionLabel(IEnumerable<AbilityDefinition> abilities)
    {
        var names = abilities
            .Select(a => (a?.Name ?? string.Empty).Trim())
            .Where(n => n.Length > 0)
            .ToList();

        if (names.Count == 0)
            return "Option";

        if (names.Count == 1)
            return names[0];

        return $"{names[0]} +{names.Count - 1}";
    }

    private sealed record GuildBenefitDetailOption(string DisplayName, AbilityDefinition Ability);
}
