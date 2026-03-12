using labyItems.Models.Characters;
using labyItems.Pages.Characters;

namespace labyItems.Pages.Configs;

public partial class MiracleGuildSearchPage : ContentPage
{
    private readonly GuildsVm _vm;
    private TaskCompletionSource<IReadOnlyList<string>>? _tcs;
    private bool _isCompleting;

    public MiracleGuildSearchPage(IEnumerable<string>? initiallySelectedGuilds = null)
    {
        InitializeComponent();

        var draft = new CharacterDraft();
        foreach (var guildName in (initiallySelectedGuilds ?? Array.Empty<string>())
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            draft.Guilds.Add(guildName);
        }

        _vm = new GuildsVm(
            draft: draft,
            notifyWizardGatingChanged: () => { },
            applyCharacterAvailabilityFilters: false,
            allowGuildSelection: true,
            searchByNameOnly: true,
            useMultiTypeFilters: true,
            enforceAvailabilityForSelection: false,
            detailMode: GuildCardDetailMode.MiracleOnly);

        _vm.PropertyChanged += OnGuildsVmPropertyChanged;

        GuildsHost.Content = new Guilds(_vm)
        {
            ShowBackButton = true,
            UseTypePills = true,
            BackCommand = new Command(async () => await CompleteAndCloseAsync())
        };

        BindingContext = this;
    }

    public string DoneButtonText => _vm.SelectedCount > 0
        ? $"Done ({_vm.SelectedCount})"
        : "Done";

    public async Task<IReadOnlyList<string>> PickAsync(INavigation navigation)
    {
        _tcs = new TaskCompletionSource<IReadOnlyList<string>>();
        await navigation.PushAsync(this);
        return await _tcs.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CompleteAndCloseAsync();
        return true;
    }

    private void OnGuildsVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuildsVm.SelectedCount) || e.PropertyName == nameof(GuildsVm.SelectedGuilds))
            OnPropertyChanged(nameof(DoneButtonText));
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await CompleteAndCloseAsync();
    }

    private async Task CompleteAndCloseAsync()
    {
        if (_isCompleting)
            return;

        _isCompleting = true;
        try
        {
            var selectedGuilds = _vm.SelectedGuilds
                .Select(guild => guild.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _tcs?.TrySetResult(selectedGuilds);

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
            _isCompleting = false;
        }
    }
}
