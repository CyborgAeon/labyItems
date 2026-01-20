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
    private Dictionary<string, GuildRecord> _guildRecords =
        new(StringComparer.OrdinalIgnoreCase);
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
    private readonly Func<IEnumerable<AlignmentRule?>> _getNonGuildRules;
    public GuildsVm(CharacterDraft draft, Action notifyWizardGatingChanged, Func<IEnumerable<AlignmentRule?>>? getNonGuildRules = null)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;
        _getNonGuildRules = getNonGuildRules ?? (() => Enumerable.Empty<AlignmentRule?>());

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
        _guildRecords = await GuildsService.GetAllAsync() ?? new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);

        var types = await GuildsService.GetTypesAsync();
        TypeFilters.Clear();
        TypeFilters.Add("All");
        foreach (var t in types)
            TypeFilters.Add(t);

        if (string.IsNullOrWhiteSpace(SelectedTypeFilter) || !TypeFilters.Contains(SelectedTypeFilter))
            SelectedTypeFilter = "All";

        // Items
        var ordered = _guildRecords.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();

        AllGuilds.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var name = ordered[i].Key;
            var rec = ordered[i].Value ?? new GuildRecord();

            var selectable = WouldStillHaveAnyAlignmentIfSelected(name);

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
                IsExpanded = false,
                IsSelectable = selectable,
                NotSelectableReason = selectable ? "" : "Conflicts with current alignment restrictions.",
            };

            vm.Icon = IconForType(vm.Type);
            AllGuilds.Add(vm);
        }

        Refilter();
        RecomputeDraftAlignments();
    }

    private bool WouldStillHaveAnyAlignmentIfSelected(string guildName)
    {
        var rules = new List<AlignmentRule?>();

        // non-guild rules (race/class/subtype/specs)
        rules.AddRange(_getNonGuildRules());

        // currently selected guild rules
        foreach (var g in _draft.Guilds)
            rules.Add(GetGuildRule(g));

        // plus this guild if not already selected
        if (!_draft.Guilds.Any(x => string.Equals(x, guildName, StringComparison.OrdinalIgnoreCase)))
            rules.Add(GetGuildRule(guildName));

        var available = CharacterDraft.ComputeAvailableAlignments(rules);
        return available.Count > 0;
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
    private AlignmentRule? GetGuildRule(string guildName)
    {
        if (string.IsNullOrWhiteSpace(guildName))
            return null;

        return _guildRecords.TryGetValue(guildName, out var rec)
            ? rec.AlignmentRule
            : null;
    }

    public int SelectedCount => _draft.Guilds.Count;

    private void RecomputeDraftAlignments()
    {
        var rules = new List<AlignmentRule?>();

        // Non-guild rules
        rules.AddRange(_getNonGuildRules());

        // Selected guild rules
        foreach (var g in _draft.Guilds)
            rules.Add(GetGuildRule(g));

        _draft.SetAvailableAlignmentsFromRules(rules);

        // Refresh selectability now that the world changed
        foreach (var card in AllGuilds)
        {
            var selectable = WouldStillHaveAnyAlignmentIfSelected(card.Name);

            // Allow already-selected guilds to stay selectable so the user can deselect them
            card.IsSelectable = selectable || card.IsSelected;
            card.NotSelectableReason = card.IsSelectable ? "" : "Conflicts with current alignment restrictions.";
        }
    }

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
        if (!item.IsSelectable && !item.IsSelected)
            return;

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
        RecomputeDraftAlignments();
        _notifyWizardGatingChanged();
    }
}

public sealed class GuildCardVm : INotifyPropertyChanged
{
    private bool _isSelectable = true;
    public bool IsSelectable
    {
        get => _isSelectable;
        set
        {
            if (_isSelectable == value) return;
            _isSelectable = value;
            Raise();
        }
    }

    private string _notSelectableReason = "";
    public string NotSelectableReason
    {
        get => _notSelectableReason;
        set
        {
            if (_notSelectableReason == value) return;
            _notSelectableReason = value;
            Raise();
        }
    }
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
