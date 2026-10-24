using labyItems.Models.Search;
using labyItems.Pages.Search;
using labyItems.Services;

namespace labyItems.Pages.Characters;

public partial class GuildPickerSearchView : ContentView
{
    private readonly GuildsVm _guildsVm;
    private readonly HashSet<string> _allowedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedNames = new(StringComparer.OrdinalIgnoreCase);
    private GlobalSearchVm? _searchVm;
    private bool _loaded;

    public GuildPickerSearchView(GuildsVm guildsVm)
    {
        _guildsVm = guildsVm;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (_loaded)
            return;

        _loaded = true;
        await _guildsVm.ReloadAsync();

        foreach (var guild in _guildsVm.FilteredGuilds)
            _allowedNames.Add(guild.Name);
        foreach (var guild in _guildsVm.SelectedGuilds)
            _selectedNames.Add(guild.Name);

        _searchVm = new GlobalSearchVm(new GlobalSearchPickerOptions
        {
            Kind = GlobalSearchKind.Guild,
            Title = "Guilds",
            AllowedNames = _allowedNames,
            SelectionOptions = new SelectionOptions {
                SelectionType = SelectionType.Multiple,
                SelectedKeys = _selectedNames,
                Max = 3
            }
        });
        BindingContext = _searchVm;
        await _searchVm.EnsureLoadedAsync();
    }

    private void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not GlobalSearchResultVm result)
            return;

        var card = _guildsVm.AllGuilds.FirstOrDefault(guild =>
            string.Equals(guild.Name, result.Name, StringComparison.OrdinalIgnoreCase));
        if (card == null || (!card.IsSelectable && !card.IsSelected))
            return;

        var willSelect = !card.IsSelected;
        _guildsVm.ToggleSelectedCommand.Execute(card);
        result.IsSelected = willSelect;
        if (willSelect)
            _selectedNames.Add(result.Name);
        else
            _selectedNames.Remove(result.Name);
    }

    private async void OnInfoTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not GlobalSearchResultVm result)
            return;

        var guilds = await GuildsService.GetAllAsync();
        if (guilds.TryGetValue(result.Name, out var record))
            await Navigation.PushAsync(new global::labyItems.Pages.GuildCard.GuildCard(record, result.Name));
    }
}
