using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models.Characters;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public sealed class GuildsVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private readonly CharacterDraft _draft;
    private readonly Action _notifyWizardGatingChanged;

    public CharacterDraft Draft => _draft;

    public GuildsVm(CharacterDraft draft, Action notifyWizardGatingChanged)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;

        TypeFilters = new ObservableCollection<string> { "All" };
        _selectedTypeFilter = "All";

        AllGuilds = new ObservableCollection<GuildCardVm>();
        FilteredGuilds = new ObservableCollection<GuildCardVm>();

        ToggleExpandedCommand = new Command<GuildCardVm>(ToggleExpanded);
        ToggleSelectedCommand = new Command<GuildCardVm>(ToggleSelected);

        SelectTypeFilterCommand = new Command<string>(s =>
        {
            SelectedTypeFilter = string.IsNullOrWhiteSpace(s) ? "All" : s;
        });

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadAsync();
        });
    }

    public ObservableCollection<string> TypeFilters { get; }

    private string? _selectedTypeFilter;
    public string? SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set { if (Set(ref _selectedTypeFilter, value)) Refilter(); }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (Set(ref _searchText, value)) Refilter(); }
    }

    public ObservableCollection<GuildCardVm> AllGuilds { get; }
    public ObservableCollection<GuildCardVm> FilteredGuilds { get; }

    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleSelectedCommand { get; }
    public ICommand SelectTypeFilterCommand { get; }

    private async Task LoadAsync()
    {
        var all = await GuildsService.GetAllAsync();

        // Filters
        var types = await GuildsService.GetTypesAsync();
        TypeFilters.Clear();
        TypeFilters.Add("All");
        foreach (var t in types)
            TypeFilters.Add(t);

        if (string.IsNullOrWhiteSpace(SelectedTypeFilter) || !TypeFilters.Contains(SelectedTypeFilter))
            SelectedTypeFilter = "All";

        // Items
        var ordered = all.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();

        AllGuilds.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var name = ordered[i].Key;
            var rec = ordered[i].Value ?? new GuildRecord();

            var vm = new GuildCardVm
            {
                Id = i + 1,
                Name = name,
                Type = rec.Type ?? "",
                Restrictions = rec.Restrictions ?? "",
                BasicBenefits = rec.Benefits?.Basic?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new(),
                IntermediateBenefits = rec.Benefits?.Intermediate?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new(),
                AdvancedBenefits = rec.Benefits?.Advanced?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new(),
                IsSelected = _draft.Guilds.Contains(name, StringComparer.OrdinalIgnoreCase),
                IsExpanded = false
            };

            vm.Icon = IconForType(vm.Type);
            AllGuilds.Add(vm);
        }

        Refilter();
    }

    private static string IconForType(string type)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();
        if (t == "political") return "🏛️";
        if (t == "professional") return "🛠️";
        if (t.Contains("relig")) return "⛪";
        return "📜";
    }

    private void Refilter()
    {
        var text = (SearchText ?? "").Trim();
        var type = (SelectedTypeFilter ?? "All").Trim();

        bool Matches(GuildCardVm g)
        {
            if (type != "All" && !string.Equals(g.Type, type, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(text))
                return true;

            return (g.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (g.Restrictions?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                   || g.BasicBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))
                   || g.IntermediateBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))
                   || g.AdvancedBenefits.Any(x => x.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        var list = AllGuilds.Where(Matches)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FilteredGuilds.Clear();
        foreach (var g in list)
            FilteredGuilds.Add(g);

        Raise(nameof(SelectedCount));
    }

    public int SelectedCount => _draft.Guilds.Count;

    private void ToggleExpanded(GuildCardVm? item)
    {
        if (item == null) return;

        // Optional UX: keep only one expanded at a time (matches your existing card behavior)
        foreach (var g in FilteredGuilds)
        {
            if (!ReferenceEquals(g, item) && g.IsExpanded)
                g.IsExpanded = false;
        }

        item.IsExpanded = !item.IsExpanded;
    }

    private void ToggleSelected(GuildCardVm? item)
    {
        if (item == null) return;

        // Toggle membership in Draft.Guilds (multi-select)
        var exists = _draft.Guilds.Any(x => string.Equals(x, item.Name, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            _draft.Guilds.RemoveAll(x => string.Equals(x, item.Name, StringComparison.OrdinalIgnoreCase));
            item.IsSelected = false;
        }
        else
        {
            _draft.Guilds.Add(item.Name);
            item.IsSelected = true;
        }

        Raise(nameof(SelectedCount));

        // Guilds step is optional, but notifying wizard keeps UI consistent.
        _notifyWizardGatingChanged();
    }
}

public sealed class GuildCardVm : INotifyPropertyChanged
{
    // (existing INotifyPropertyChanged boilerplate)
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "📜";

    public string Restrictions { get; set; } = "";

    public List<string> BasicBenefits { get; set; } = new();
    public List<string> IntermediateBenefits { get; set; } = new();
    public List<string> AdvancedBenefits { get; set; } = new();

    public bool HasRestrictions => !string.IsNullOrWhiteSpace(Restrictions);

    public bool HasBasic => BasicBenefits.Count > 0;
    public bool HasIntermediate => IntermediateBenefits.Count > 0;
    public bool HasAdvanced => AdvancedBenefits.Count > 0;

    public bool HasAnyBenefits => HasBasic || HasIntermediate || HasAdvanced;

    public double ChevronRotation => IsExpanded ? 180 : 0;

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            Raise();
            Raise(nameof(ChevronRotation));
        }
    }

    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            Raise();
        }
    }
}
