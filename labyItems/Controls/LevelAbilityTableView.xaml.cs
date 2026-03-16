using System.Collections;
using System.Collections.Specialized;
using labyItems.Models.ViewModels;
using labyItems.Models.Characters;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Controls;

public partial class LevelAbilityTableView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(LevelAbilityTableView),
        default(IEnumerable),
        propertyChanged: OnItemsSourceChanged);

    public static readonly BindableProperty ShowBodyAndLocProperty = BindableProperty.Create(
        nameof(ShowBodyAndLoc),
        typeof(bool),
        typeof(LevelAbilityTableView),
        true);

    public static readonly BindableProperty AbilitiesHeaderTextProperty = BindableProperty.Create(
        nameof(AbilitiesHeaderText),
        typeof(string),
        typeof(LevelAbilityTableView),
        "Abilities");

    public static readonly BindableProperty ShowWeaponSkillsColumnProperty = BindableProperty.Create(
        nameof(ShowWeaponSkillsColumn),
        typeof(bool),
        typeof(LevelAbilityTableView),
        true);

    public static readonly BindableProperty WeaponSkillColumnWidthProperty = BindableProperty.Create(
        nameof(WeaponSkillColumnWidth),
        typeof(GridLength),
        typeof(LevelAbilityTableView),
        new GridLength(56));

    private INotifyCollectionChanged? _itemsSourceNotifier;

    public LevelAbilityTableView()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool ShowBodyAndLoc
    {
        get => (bool)GetValue(ShowBodyAndLocProperty);
        set => SetValue(ShowBodyAndLocProperty, value);
    }

    public string AbilitiesHeaderText
    {
        get => (string)GetValue(AbilitiesHeaderTextProperty);
        set => SetValue(AbilitiesHeaderTextProperty, value);
    }

    public bool ShowWeaponSkillsColumn
    {
        get => (bool)GetValue(ShowWeaponSkillsColumnProperty);
        private set => SetValue(ShowWeaponSkillsColumnProperty, value);
    }

    public GridLength WeaponSkillColumnWidth
    {
        get => (GridLength)GetValue(WeaponSkillColumnWidthProperty);
        private set => SetValue(WeaponSkillColumnWidthProperty, value);
    }

    private async void OnInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not LevelAbilityRowVm row)
            return;

        var detailOptions = BuildAbilityDetailOptions(row);
        if (detailOptions.Count == 0)
            return;

        var hostPage = ResolveHostPage();
        if (hostPage == null)
            return;

        var title = row.Level > 0
            ? $"Level {row.Level} abilities"
            : "Abilities";

        var picked = detailOptions.Count == 1
            ? detailOptions[0]
            : await PickAbilityDetailOptionAsync(hostPage, title, detailOptions);
        if (picked == null)
            return;

        var ability = await ResolveAbilityAsync(picked.LookupKey);
        if (ability == null
            && !string.Equals(picked.LookupKey, picked.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            ability = await ResolveAbilityAsync(picked.DisplayName);
        }

        if (ability == null)
        {
            await hostPage.DisplayAlert("No Ability Card", $"Could not find a detail card for \"{picked.DisplayName}\".", "OK");
            return;
        }

        var navigation = ResolveNavigation();
        if (navigation == null)
            return;

        await navigation.PushAsync(new AbilityCardPage(ability));
    }

    private static async Task<AbilityDetailOption?> PickAbilityDetailOptionAsync(
        Page hostPage,
        string title,
        IReadOnlyList<AbilityDetailOption> options)
    {
        var labels = options
            .Select(option => option.DisplayName)
            .ToArray();

        var picked = await hostPage.DisplayActionSheet(title, "Cancel", null, labels);
        if (string.IsNullOrWhiteSpace(picked) || picked.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var option in options)
        {
            if (option.DisplayName.Equals(picked, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return null;
    }

    private static List<AbilityDetailOption> BuildAbilityDetailOptions(LevelAbilityRowVm row)
    {
        var names = (row.AbilityNames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToList();

        var keys = (row.AbilityDetailKeys ?? Array.Empty<string>())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .ToList();

        var options = new List<AbilityDetailOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var pairedCount = Math.Min(names.Count, keys.Count);
        for (var i = 0; i < pairedCount; i++)
        {
            var displayName = names[i];
            var lookupKey = keys[i];
            if (displayName.Length == 0 && lookupKey.Length == 0)
                continue;

            var dedupe = $"{displayName}::{lookupKey}";
            if (!seen.Add(dedupe))
                continue;

            options.Add(new AbilityDetailOption(
                displayName.Length > 0 ? displayName : lookupKey,
                lookupKey.Length > 0 ? lookupKey : displayName));
        }

        foreach (var key in keys.Skip(pairedCount))
        {
            if (seen.Contains($"::{key}"))
                continue;

            seen.Add($"::{key}");
            options.Add(new AbilityDetailOption(key, key));
        }

        foreach (var name in names.Skip(pairedCount))
        {
            if (seen.Contains($"{name}::{name}"))
                continue;

            seen.Add($"{name}::{name}");
            options.Add(new AbilityDetailOption(name, name));
        }

        return options;
    }

    private static async Task<EvolutionService.AbilityResult?> ResolveAbilityAsync(string selectedAbility)
    {
        var ability = await AbilityDetailsLookupService.FindByIndexAsync(selectedAbility);
        if (ability != null)
            return ability;

        var specialisationAbility = await DetailCardLookupService.FindSpecialisationAbilityAsync(selectedAbility);
        if (specialisationAbility.Ability == null)
            return null;

        return ToAbilityResult(
            specialisationAbility.Ability,
            specialisationAbility.Key,
            selectedAbility);
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
            Available = source.Source ?? "ALL",
            CanBuyMultiple = false,
            PreReqs = source.PreReqs is { Count: > 0 } preReqs
                ? preReqs
                : Array.Empty<string>(),
            MaxAvailable = source.Count
        };
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not LevelAbilityTableView view)
            return;

        view.DetachFromItemsSource(oldValue);
        view.AttachToItemsSource(newValue);
        view.UpdateWeaponSkillsColumnVisibility();
    }

    private void AttachToItemsSource(object? source)
    {
        if (source is not INotifyCollectionChanged changed)
            return;

        _itemsSourceNotifier = changed;
        _itemsSourceNotifier.CollectionChanged += OnItemsSourceCollectionChanged;
    }

    private void DetachFromItemsSource(object? source)
    {
        var notifier = source as INotifyCollectionChanged ?? _itemsSourceNotifier;
        if (notifier == null)
            return;

        notifier.CollectionChanged -= OnItemsSourceCollectionChanged;
        if (ReferenceEquals(notifier, _itemsSourceNotifier))
            _itemsSourceNotifier = null;
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateWeaponSkillsColumnVisibility();

    private void UpdateWeaponSkillsColumnVisibility()
    {
        var hasWeaponSkills = (ItemsSource ?? Enumerable.Empty<object>())
            .OfType<LevelAbilityRowVm>()
            .Any(row => !string.IsNullOrWhiteSpace(row.WeaponSkills));

        ShowWeaponSkillsColumn = hasWeaponSkills;
        WeaponSkillColumnWidth = hasWeaponSkills
            ? new GridLength(56)
            : new GridLength(0);
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

        return Shell.Current?.CurrentPage ?? Application.Current?.MainPage;
    }

    private INavigation? ResolveNavigation()
    {
        if (Navigation?.NavigationStack is { Count: > 0 })
            return Navigation;

        if (Shell.Current?.Navigation is { } shellNav)
            return shellNav;

        return Application.Current?.MainPage?.Navigation;
    }

    private sealed record AbilityDetailOption(string DisplayName, string LookupKey);
}
