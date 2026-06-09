using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public enum GuildCardDetailMode
{
    Full = 0,
    MiracleOnly = 1
}

public sealed class GuildsVm : INotifyPropertyChanged
{
    private const string AllTypeFilterValue = "All";
    private const int SearchDebounceMs = 250;
    private const int InitialResultLimit = 100;
    private const int UiBatchSize = 40;
    private static readonly IReadOnlyDictionary<string, GuildMiracleDefinition> EmptyMiracleLookup =
        new Dictionary<string, GuildMiracleDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex MiracleListLoreBlockRegex = new(
        @"(?is)(?:^|\n\s*\n)[^\n]*?\b(?:[A-Z]+\s+)*MIRACLE LIST\b.*?(?=(\n\s*\n|$))",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled));
    private static readonly Regex DenominationalMiracleLoreBlockRegex = new(
        @"(?is)(?:^|\n\s*\n)[^\n]*?\bDENOMINATIONAL MIRACLE\b.*?(?=(\n\s*\n|$))",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled));
    private static readonly Regex MiracleStatLoreBlockRegex = new(
        @"(?is)(?:^|\n\s*\n)[^\n]*?\bLevel:\b[^\n]*\bAlignment:\b[^\n]*\bDuration:\b[^\n]*\bRange:\b.*?(?=(\n\s*\n|$))",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled));
    private static readonly Regex ListPrefixRegex = new(
        @"^(?:[•\-\*]|(?:\d+[\.\)]\s)|(?:\d+(?:st|nd|rd|th)\b))",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private Dictionary<string, GuildRecord> _guildRecords =
        new(StringComparer.OrdinalIgnoreCase);
    private string _currentClassName = "";
    private HashSet<string> _currentClassBrackets = new(StringComparer.OrdinalIgnoreCase);
    private string _currentRaceName = "";
    private HashSet<string> _currentPeopleTypes = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _currentRaceSelections = new(StringComparer.OrdinalIgnoreCase);
    private GuildSlotRules _slotRules = GuildSlotRules.Default();
    private IReadOnlyDictionary<string, GuildMiracleDefinition>? _miracleLookupCache;
    private Task<IReadOnlyDictionary<string, GuildMiracleDefinition>>? _miracleLookupTask;
    private readonly Dictionary<string, Task> _detailLoadTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _detailLoadGate = new();
    private CancellationTokenSource? _searchDebounceCts;
    private CancellationTokenSource? _searchWorkCts;
    private List<GuildSearchIndexEntry> _searchIndex = new();
    private int _searchVersion;
    private int _appliedSearchVersion;
    private int _loadVersion;
    private bool _isSearchInProgress;
    private string _searchStatus = string.Empty;
    private readonly Func<Task>? _refreshDraftAbilitiesAsync;
    private readonly bool _applyCharacterAvailabilityFilters;
    private readonly bool _allowGuildSelection;
    private readonly bool _searchByNameOnly;
    private readonly bool _useMultiTypeFilters;
    private readonly bool _enforceAvailabilityForSelection;
    private readonly bool _hideUnavailableGuilds;
    private readonly GuildCardDetailMode _detailMode;
    private readonly HashSet<string> _selectedTypeFilters = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLoading;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private static Task RunOnMainThreadAsync(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
            return Task.CompletedTask;
        }

        return MainThread.InvokeOnMainThreadAsync(action);
    }

    private void RaiseSelectedGuildsChanged()
    {
        Raise(nameof(SelectedGuilds));
        Raise(nameof(IsComplete));
    }

    private readonly CharacterDraft _draft;
    private readonly Action _notifyWizardGatingChanged;
    private readonly ICharacterCreationDataService _creationDataService;

    public CharacterDraft Draft => _draft;
    private readonly Func<IEnumerable<AlignmentRule?>> _getNonGuildRules;
    public GuildsVm(
        CharacterDraft draft,
        Action notifyWizardGatingChanged,
        Func<IEnumerable<AlignmentRule?>>? getNonGuildRules = null,
        Func<Task>? refreshDraftAbilitiesAsync = null,
        ICharacterCreationDataService? creationDataService = null,
        bool applyCharacterAvailabilityFilters = true,
        bool allowGuildSelection = true,
        bool searchByNameOnly = false,
        bool useMultiTypeFilters = false,
        bool enforceAvailabilityForSelection = true,
        bool hideUnavailableGuilds = false,
        GuildCardDetailMode detailMode = GuildCardDetailMode.Full,
        bool autoReload = true)
    {
        _draft = draft;
        _notifyWizardGatingChanged = notifyWizardGatingChanged;
        _getNonGuildRules = getNonGuildRules ?? (() => Enumerable.Empty<AlignmentRule?>());
        _refreshDraftAbilitiesAsync = refreshDraftAbilitiesAsync;
        _applyCharacterAvailabilityFilters = applyCharacterAvailabilityFilters;
        _allowGuildSelection = allowGuildSelection;
        _searchByNameOnly = searchByNameOnly;
        _useMultiTypeFilters = useMultiTypeFilters;
        _enforceAvailabilityForSelection = enforceAvailabilityForSelection;
        _hideUnavailableGuilds = hideUnavailableGuilds;
        _detailMode = detailMode;
        _creationDataService = creationDataService
            ?? ServiceHelper.ResolveService<ICharacterCreationDataService>()
            ?? new CharacterCreationDataService();

        TypeFilters = new ObservableCollection<string> { AllTypeFilterValue };
        _selectedTypeFilter = AllTypeFilterValue;

        AllGuilds = new ObservableCollection<GuildCardVm>();
        FilteredGuilds = new ObservableCollection<GuildCardVm>();
        TypeFilterChips = new ObservableCollection<GuildTypeFilterChipVm>();

        ToggleExpandedCommand = new Command<GuildCardVm>(item => _ = ExecuteGuildCardCommandSafeAsync(
            action: () => ToggleExpandedAsync(item),
            operation: "GUILD_TOGGLE_EXPANDED",
            guildName: item?.Name));
        ToggleSelectedCommand = new Command<GuildCardVm>(item => _ = ExecuteGuildCardCommandSafeAsync(
            action: () => ToggleSelectedAsync(item),
            operation: "GUILD_TOGGLE_SELECTED",
            guildName: item?.Name));
        ToggleTypeFilterChipCommand = new Command<GuildTypeFilterChipVm>(ToggleTypeFilterChip);

        SelectTypeFilterCommand = new Command<string>(s =>
        {
            SelectedTypeFilter = string.IsNullOrWhiteSpace(s) ? AllTypeFilterValue : s;
        });

        if (autoReload)
        {
            MainThread.BeginInvokeOnMainThread(() => _ = ExecuteGuildCardCommandSafeAsync(
                action: ReloadAsync,
                operation: "GUILD_AUTO_RELOAD",
                guildName: null));
        }
    }

    public ObservableCollection<string> TypeFilters { get; }

    private string? _selectedTypeFilter;
    public string? SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (!Set(ref _selectedTypeFilter, value))
                return;

            ScheduleRefilter(debounce: false);
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value))
                return;

            ScheduleRefilter(debounce: true);
        }
    }

    public bool IsSearchInProgress
    {
        get => _isSearchInProgress;
        private set
        {
            if (!Set(ref _isSearchInProgress, value))
                return;

            Raise(nameof(IsBusy));
        }
    }

    public string SearchStatus
    {
        get => _searchStatus;
        private set => Set(ref _searchStatus, value);
    }

    public bool IsBusy => IsLoading || IsSearchInProgress;

    public void BeginLoadingState()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsLoading = true;
            IsSearchInProgress = false;
            SearchStatus = string.Empty;
            FilteredGuilds.Clear();
        });
    }

    public async Task ReleaseScreenCacheAsync()
    {
        Interlocked.Increment(ref _loadVersion);
        Interlocked.Increment(ref _searchVersion);
        Volatile.Write(ref _appliedSearchVersion, 0);

        var debounce = Interlocked.Exchange(ref _searchDebounceCts, null);
        debounce?.Cancel();
        debounce?.Dispose();

        var searchWork = Interlocked.Exchange(ref _searchWorkCts, null);
        searchWork?.Cancel();
        searchWork?.Dispose();

        _searchIndex = new List<GuildSearchIndexEntry>();
        _selectedTypeFilters.Clear();

        lock (_detailLoadGate)
            _detailLoadTasks.Clear();

        await RunOnMainThreadAsync(() =>
        {
            AllGuilds.Clear();
            FilteredGuilds.Clear();
            TypeFilterChips.Clear();
            TypeFilters.Clear();
            TypeFilters.Add(AllTypeFilterValue);
            _selectedTypeFilter = AllTypeFilterValue;
            _searchText = string.Empty;
            SearchStatus = string.Empty;
            IsSearchInProgress = false;
            IsLoading = false;
            Raise(nameof(SelectedTypeFilter));
            Raise(nameof(SearchText));
            Raise(nameof(SelectedCount));
            RaiseSelectedGuildsChanged();
        }).ConfigureAwait(false);
    }

    private void ScheduleRefilter(bool debounce)
    {
        var version = Interlocked.Increment(ref _searchVersion);

        var previousDebounce = _searchDebounceCts;
        previousDebounce?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        previousDebounce?.Dispose();
        var debounceToken = _searchDebounceCts.Token;

        var criteria = CaptureSearchCriteriaSnapshot(version);

        var previousWork = Interlocked.Exchange(ref _searchWorkCts, new CancellationTokenSource());
        previousWork?.Cancel();
        previousWork?.Dispose();
        var searchWorkToken = _searchWorkCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                if (debounce)
                    await Task.Delay(SearchDebounceMs, debounceToken).ConfigureAwait(false);

                if (debounceToken.IsCancellationRequested || searchWorkToken.IsCancellationRequested)
                    return;

                await ExecuteSearchAsync(criteria, searchWorkToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Ignore cancelled debounce.
            }
            catch (OperationCanceledException)
            {
                // Ignore cancelled search operations.
            }
            catch (Exception ex)
            {
                RuntimeLog.Write(
                    "GUILD_SEARCH",
                    $"Guild search failed for version={version}.",
                    ex);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SearchStatus = "Search failed. Try again.";
                    IsSearchInProgress = false;
                }).ConfigureAwait(false);
            }
        });
    }

    private GuildSearchCriteria CaptureSearchCriteriaSnapshot(int version)
    {
        var text = (SearchText ?? string.Empty).Trim();
        var selectedTypeFilter = (SelectedTypeFilter ?? AllTypeFilterValue).Trim();
        var typeFilters = _selectedTypeFilters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new GuildSearchCriteria(
            Version: version,
            Text: text,
            SelectedTypeFilter: selectedTypeFilter,
            MultiTypeFilters: typeFilters,
            UseMultiTypeFilters: _useMultiTypeFilters,
            SearchByNameOnly: _searchByNameOnly,
            HideUnavailable: _hideUnavailableGuilds);
    }

    private async Task ExecuteSearchAsync(GuildSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var searchTimer = System.Diagnostics.Stopwatch.StartNew();
        RuntimeLog.Write(
            "GUILD_SEARCH",
            $"Query started version={criteria.Version} text='{criteria.Text}' type='{criteria.SelectedTypeFilter}'. " +
            $"threadId={Environment.CurrentManagedThreadId} mainThread={MainThread.IsMainThread}.");

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            IsSearchInProgress = true;
            SearchStatus = "Searching guilds...";
        }).ConfigureAwait(false);

        try
        {
            var indexSnapshot = _searchIndex;
            var results = await Task.Run(
                    () => SearchIndex(indexSnapshot, criteria, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (criteria.Version != Volatile.Read(ref _searchVersion))
            {
                RuntimeLog.Write(
                    "GUILD_SEARCH",
                    $"Query superseded before UI apply version={criteria.Version}. " +
                    $"latestVersion={Volatile.Read(ref _searchVersion)}.");
                return;
            }

            await ApplySearchResultsAsync(criteria, results, cancellationToken).ConfigureAwait(false);

            searchTimer.Stop();
            RuntimeLog.Write(
                "GUILD_SEARCH",
                $"Query completed version={criteria.Version} results={results.Count} elapsedMs={searchTimer.ElapsedMilliseconds}. " +
                $"threadId={Environment.CurrentManagedThreadId} mainThread={MainThread.IsMainThread}.");
        }
        catch (OperationCanceledException)
        {
            searchTimer.Stop();
            RuntimeLog.Write(
                "GUILD_SEARCH",
                $"Query cancelled version={criteria.Version} elapsedMs={searchTimer.ElapsedMilliseconds}. " +
                $"threadId={Environment.CurrentManagedThreadId} mainThread={MainThread.IsMainThread}.");
            throw;
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (criteria.Version < Volatile.Read(ref _searchVersion))
                    return;

                IsSearchInProgress = false;
            }).ConfigureAwait(false);
        }
    }

    private static List<GuildCardVm> SearchIndex(
        IReadOnlyList<GuildSearchIndexEntry> index,
        GuildSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = NormalizeSearchText(criteria.Text);
        var results = new List<GuildCardVm>(Math.Min(index.Count, 256));

        foreach (var entry in index)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (criteria.HideUnavailable
                && !entry.Card.IsSelected
                && entry.EnforceAvailability
                && !entry.Card.IsSelectable)
            {
                continue;
            }

            if (criteria.UseMultiTypeFilters)
            {
                if (criteria.MultiTypeFilters.Count > 0
                    && !criteria.MultiTypeFilters.Contains(entry.Type))
                {
                    continue;
                }
            }
            else if (!string.Equals(criteria.SelectedTypeFilter, AllTypeFilterValue, StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(entry.Type, criteria.SelectedTypeFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalizedQuery.Length > 0)
            {
                if (criteria.SearchByNameOnly)
                {
                    if (!entry.NameNormalized.Contains(normalizedQuery, StringComparison.Ordinal))
                        continue;
                }
                else if (!entry.SearchCorpusNormalized.Contains(normalizedQuery, StringComparison.Ordinal))
                {
                    continue;
                }
            }

            results.Add(entry.Card);
        }

        results.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    private async Task ApplySearchResultsAsync(
        GuildSearchCriteria criteria,
        IReadOnlyList<GuildCardVm> results,
        CancellationToken cancellationToken)
    {
        var uiApplyTimer = System.Diagnostics.Stopwatch.StartNew();

        var initialCount = Math.Min(results.Count, InitialResultLimit);
        var initialSlice = initialCount == 0
            ? Array.Empty<GuildCardVm>()
            : results.Take(initialCount).ToArray();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (criteria.Version != Volatile.Read(ref _searchVersion))
                return;

            FilteredGuilds.Clear();
            foreach (var guild in initialSlice)
                FilteredGuilds.Add(guild);

            SearchStatus = results.Count > initialCount
                ? $"Showing {initialCount} of {results.Count} guilds..."
                : $"{results.Count} guilds";
            Raise(nameof(SelectedCount));
            Volatile.Write(ref _appliedSearchVersion, criteria.Version);
        }).ConfigureAwait(false);

        uiApplyTimer.Stop();
        RuntimeLog.Write(
            "GUILD_SEARCH",
            $"UI batch apply completed version={criteria.Version} initialCount={initialCount} totalResults={results.Count} " +
            $"uiBatchMs={uiApplyTimer.ElapsedMilliseconds} threadId={Environment.CurrentManagedThreadId} mainThread={MainThread.IsMainThread}.");

        if (results.Count <= initialCount)
            return;

        for (var offset = initialCount; offset < results.Count; offset += UiBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (criteria.Version != Volatile.Read(ref _searchVersion))
                return;

            var batch = results.Skip(offset).Take(UiBatchSize).ToArray();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (criteria.Version != Volatile.Read(ref _searchVersion)
                    || criteria.Version != Volatile.Read(ref _appliedSearchVersion))
                {
                    return;
                }

                foreach (var guild in batch)
                    FilteredGuilds.Add(guild);

                SearchStatus = $"Showing {FilteredGuilds.Count} of {results.Count} guilds...";
            }).ConfigureAwait(false);

            // Yield between UI batches so typing and gestures remain responsive.
            await Task.Delay(16, cancellationToken).ConfigureAwait(false);
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (criteria.Version != Volatile.Read(ref _searchVersion))
                return;

            SearchStatus = $"{results.Count} guilds";
        }).ConfigureAwait(false);
    }

    public ObservableCollection<GuildCardVm> AllGuilds { get; }
    public ObservableCollection<GuildCardVm> FilteredGuilds { get; }
    public ObservableCollection<GuildTypeFilterChipVm> TypeFilterChips { get; }
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!Set(ref _isLoading, value))
                return;

            Raise(nameof(IsBusy));
        }
    }

    public IEnumerable<GuildCardVm> SelectedGuilds =>
        AllGuilds
            .Where(g => g.IsSelected)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    public bool IsComplete =>
        AreRequiredBenefitChoicesComplete(_guildRecords);

    public ICommand ToggleExpandedCommand { get; }
    public ICommand ToggleSelectedCommand { get; }
    public ICommand SelectTypeFilterCommand { get; }
    public ICommand ToggleTypeFilterChipCommand { get; }

    public async Task<IReadOnlyList<GuildBenefitChoiceSummaryRow>> BuildIncompleteChoiceRowsAsync()
    {
        await EnsureGuildRecordsLoadedAsync();
        var requirements = EnumerateAvailableChoiceRequirements(
            _guildRecords,
            onlyIncomplete: true,
            includeUnavailableTiers: false).ToList();
        var rows = new List<GuildBenefitChoiceSummaryRow>(requirements.Count);

        foreach (var requirement in requirements)
        {
            var optionRows = requirement.Options
                .Select(option => new GuildBenefitChoiceOption(
                    BuildOptionLabel(option.Abilities),
                    option.Abilities
                        .Select(FormatBenefit)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList()))
                .ToList();

            if (optionRows.Count == 0)
                continue;

            rows.Add(new GuildBenefitChoiceSummaryRow(
                displayText: $"{requirement.GuildName} ({requirement.TierLabel}) choice {requirement.OptionIndex}",
                selectionKey: requirement.SelectionKey,
                options: optionRows,
                selectedIndex: requirement.SelectedIndex,
                applySelection: selectedIndex =>
                {
                    if (selectedIndex.HasValue
                        && selectedIndex.Value > 0
                        && selectedIndex.Value <= optionRows.Count)
                    {
                        ApplyBenefitOptionSelection(requirement.SelectionKey, selectedIndex.Value);
                    }
                    else
                    {
                        ApplyBenefitOptionSelection(requirement.SelectionKey, null);
                    }
                },
                guildName: requirement.GuildName,
                tier: requirement.TierLabel,
                optionNumber: requirement.OptionIndex,
                isTierAvailableNow: requirement.IsTierAvailableNow));
        }

        return rows;
    }

    public async Task<IReadOnlyList<GuildBenefitChoiceSummaryRow>> BuildChoiceRowsForGuildAsync(string guildName)
    {
        var normalizedGuildName = (guildName ?? string.Empty).Trim();
        if (normalizedGuildName.Length == 0)
            return Array.Empty<GuildBenefitChoiceSummaryRow>();

        await EnsureGuildRecordsLoadedAsync();
        var requirements = EnumerateAvailableChoiceRequirements(
                _guildRecords,
                onlyIncomplete: false,
                includeUnavailableTiers: true)
            .Where(requirement => string.Equals(requirement.GuildName, normalizedGuildName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var rows = new List<GuildBenefitChoiceSummaryRow>(requirements.Count);
        foreach (var requirement in requirements)
        {
            var optionRows = requirement.Options
                .Select(option => new GuildBenefitChoiceOption(
                    BuildOptionLabel(option.Abilities),
                    option.Abilities
                        .Select(FormatBenefit)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList()))
                .ToList();

            if (optionRows.Count == 0)
                continue;

            rows.Add(new GuildBenefitChoiceSummaryRow(
                displayText: $"{requirement.GuildName} ({requirement.TierLabel}) choice {requirement.OptionIndex}",
                selectionKey: requirement.SelectionKey,
                options: optionRows,
                selectedIndex: requirement.SelectedIndex,
                applySelection: selectedIndex =>
                {
                    if (selectedIndex.HasValue
                        && selectedIndex.Value > 0
                        && selectedIndex.Value <= optionRows.Count)
                    {
                        ApplyBenefitOptionSelection(requirement.SelectionKey, selectedIndex.Value);
                    }
                    else
                    {
                        ApplyBenefitOptionSelection(requirement.SelectionKey, null);
                    }
                },
                guildName: requirement.GuildName,
                tier: requirement.TierLabel,
                optionNumber: requirement.OptionIndex,
                isTierAvailableNow: requirement.IsTierAvailableNow));
        }

        return rows;
    }

    public async Task<IReadOnlyList<GuildReviewSummaryRowVm>> BuildSelectedGuildReviewRowsAsync()
    {
        await EnsureGuildRecordsLoadedAsync();
        var requirementsByGuild = EnumerateAvailableChoiceRequirements(
                _guildRecords,
                onlyIncomplete: false,
                includeUnavailableTiers: false)
            .GroupBy(requirement => requirement.GuildName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var rows = new List<GuildReviewSummaryRowVm>();
        foreach (var guildName in _draft.Guilds
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (!requirementsByGuild.TryGetValue(guildName, out var requirementsForGuild))
                requirementsForGuild = new List<GuildChoiceRequirement>();

            var totalChoices = requirementsForGuild.Count;
            var missingChoices = requirementsForGuild.Count(requirement => !requirement.HasValidSelection);
            var hasChoices = totalChoices > 0;
            var hasMissingChoices = missingChoices > 0;

            var status = hasChoices
                ? hasMissingChoices
                    ? $"{missingChoices} choice{(missingChoices == 1 ? string.Empty : "s")} required"
                    : "Complete"
                : "No choices required";

            var guildType = _guildRecords.TryGetValue(guildName, out var record)
                ? (record?.Type ?? string.Empty).Trim()
                : string.Empty;

            rows.Add(new GuildReviewSummaryRowVm(
                guildName: guildName,
                guildType: guildType,
                hasChoices: hasChoices,
                hasMissingChoices: hasMissingChoices,
                statusText: status));
        }

        return rows;
    }

    public async Task<GuildCardVm?> BuildGuildDetailCardAsync(string guildName, bool expand = true)
    {
        var normalizedGuildName = (guildName ?? string.Empty).Trim();
        if (normalizedGuildName.Length == 0)
            return null;

        await EnsureGuildRecordsLoadedAsync();
        if (!_guildRecords.TryGetValue(normalizedGuildName, out var record) || record == null)
            return null;

        var isSelected = _draft.Guilds.Contains(normalizedGuildName, StringComparer.OrdinalIgnoreCase);
        var availability = EvaluateAvailabilityForCurrentContext(record, normalizedGuildName);
        var selectable = availability.Allowed;
        var cardSelectable = !_allowGuildSelection
            || (!_enforceAvailabilityForSelection)
            || (selectable || isSelected);

        var card = new GuildCardVm
        {
            Name = normalizedGuildName,
            Type = record.Type ?? string.Empty,
            Logo = NormalizeLogoPath(record.Logo),
            Icon = IconForType(record.Type ?? string.Empty),
            IsSelected = isSelected,
            IsSelectable = cardSelectable,
            NotSelectableReason = cardSelectable
                ? string.Empty
                : (_allowGuildSelection && _enforceAvailabilityForSelection ? availability.Reason : string.Empty),
            IsLocked = _slotRules.IsGuildLocked(record.Type ?? string.Empty, normalizedGuildName),
            HasAnyChoiceOptions = HasAvailableChoiceOptions(record)
        };

        await EnsureCardDetailsLoadedCoreAsync(card, record);
        card.IsExpanded = expand;
        return card;
    }

    public async Task<IReadOnlyList<string>> BuildIncompleteChoiceWarningsAsync()
    {
        await EnsureGuildRecordsLoadedAsync();
        var requirements = EnumerateAvailableChoiceRequirements(
            _guildRecords,
            onlyIncomplete: true,
            includeUnavailableTiers: false).ToList();
        return requirements
            .GroupBy(requirement => $"{requirement.GuildName} ({requirement.TierLabel})", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var count = group.Count();
                var suffix = count == 1 ? string.Empty : "s";
                return $"{group.Key}: {count} choice{suffix} still required.";
            })
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task ReloadAsync()
    {
        var loadVersion = Interlocked.Increment(ref _loadVersion);
        var loadTimer = System.Diagnostics.Stopwatch.StartNew();
        RuntimeLog.Write(
            "GUILD_SEARCH",
            $"GuildSearch load started. threadId={Environment.CurrentManagedThreadId} mainThread={MainThread.IsMainThread}.");

        bool IsLatestLoad() => loadVersion == Volatile.Read(ref _loadVersion);

        await RunOnMainThreadAsync(() =>
        {
            IsLoading = true;
            SearchStatus = string.Empty;
        }).ConfigureAwait(false);
        try
        {
            _guildRecords = _detailMode == GuildCardDetailMode.MiracleOnly
                ? await _creationDataService.GetGuildsForMiracleSearchAsync().ConfigureAwait(false)
                : await _creationDataService.GetGuildsAsync().ConfigureAwait(false);
            _guildRecords ??= new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);
            lock (_detailLoadGate)
                _detailLoadTasks.Clear();

            if (!IsLatestLoad())
                return;

            await RefreshContextAsync().ConfigureAwait(false);

            ApplyAvailabilityToCurrentSelection();

            var (hasCityBound, cityName) = GetCityBoundInfo();
            if (hasCityBound && !string.IsNullOrWhiteSpace(cityName))
            {
                var matchedGuildName = _guildRecords.Keys
                    .FirstOrDefault(k => string.Equals(k, cityName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(matchedGuildName))
                {
                    if (!_draft.Guilds.Any(g => string.Equals(g, matchedGuildName, StringComparison.OrdinalIgnoreCase)))
                        _draft.Guilds.Add(matchedGuildName);
                }
            }

            ApplyAvailabilityToCurrentSelection();
            var types = _detailMode == GuildCardDetailMode.MiracleOnly
                ? _guildRecords.Values
                    .Select(record => (record?.Type ?? string.Empty).Trim())
                    .Where(type => !string.IsNullOrWhiteSpace(type))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : await _creationDataService.GetGuildTypesAsync().ConfigureAwait(false);

            if (!IsLatestLoad())
                return;

            await RunOnMainThreadAsync(() =>
            {
                if (!IsLatestLoad())
                    return;

                TypeFilters.Clear();
                TypeFilters.Add(AllTypeFilterValue);
                foreach (var t in types)
                    TypeFilters.Add(t);

                if (string.IsNullOrWhiteSpace(SelectedTypeFilter) || !TypeFilters.Contains(SelectedTypeFilter))
                    SelectedTypeFilter = AllTypeFilterValue;

                if (_useMultiTypeFilters)
                    RebuildTypeFilterChips();
                else
                    TypeFilterChips.Clear();
            }).ConfigureAwait(false);

            var ordered = _guildRecords
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var prepared = await Task.Run(() =>
            {
                var cards = new List<GuildCardVm>(ordered.Count);
                var index = new List<GuildSearchIndexEntry>(ordered.Count);

                for (var i = 0; i < ordered.Count; i++)
                {
                    var name = ordered[i].Key;
                    var rec = ordered[i].Value ?? new GuildRecord();

                    var isSelected = _draft.Guilds.Contains(name, StringComparer.OrdinalIgnoreCase);
                    var availability = EvaluateAvailabilityForCurrentContext(rec, name);
                    var selectable = availability.Allowed;
                    var reason = availability.Reason;
                    var cardSelectable = !_allowGuildSelection
                        || (!_enforceAvailabilityForSelection)
                        || (selectable || isSelected);

                    var vm = new GuildCardVm
                    {
                        Id = i + 1,
                        Name = name,
                        Type = rec.Type ?? string.Empty,
                        Logo = NormalizeLogoPath(rec.Logo),
                        IsSelected = isSelected,
                        IsExpanded = false,
                        IsSelectable = cardSelectable,
                        NotSelectableReason = cardSelectable
                            ? string.Empty
                            : (_allowGuildSelection && _enforceAvailabilityForSelection ? reason : string.Empty),
                        IsLocked = _slotRules.IsGuildLocked(rec.Type ?? string.Empty, name),
                        HasAnyChoiceOptions = HasAvailableChoiceOptions(rec),
                        Icon = IconForType(rec.Type ?? string.Empty)
                    };

                    cards.Add(vm);
                    index.Add(new GuildSearchIndexEntry(
                        Card: vm,
                        Type: vm.Type,
                        NameNormalized: NormalizeSearchText(vm.Name),
                        SearchCorpusNormalized: BuildSearchCorpus(name, rec),
                        IsSelected: vm.IsSelected,
                        IsSelectable: vm.IsSelectable,
                        EnforceAvailability: _allowGuildSelection && _enforceAvailabilityForSelection));
                }

                return (Cards: cards, SearchIndex: index);
            }).ConfigureAwait(false);

            if (!IsLatestLoad())
                return;

            var preloadDetails = new List<Task>();
            await RunOnMainThreadAsync(() =>
            {
                if (!IsLatestLoad())
                    return;

                AllGuilds.Clear();
                for (var i = 0; i < prepared.Cards.Count; i++)
                {
                    var vm = prepared.Cards[i];
                    AllGuilds.Add(vm);

                    if (vm.IsSelected && _guildRecords.TryGetValue(vm.Name, out var rec))
                        preloadDetails.Add(EnsureCardDetailsLoadedAsync(vm, rec));
                }

                _searchIndex = prepared.SearchIndex;
            }).ConfigureAwait(false);

            if (preloadDetails.Count > 0)
                await Task.WhenAll(preloadDetails).ConfigureAwait(false);

            if (!IsLatestLoad())
                return;

            ScheduleRefilter(debounce: false);
            await RunOnMainThreadAsync(() =>
            {
                if (!IsLatestLoad())
                    return;

                RecomputeDraftAlignmentOptionsOnly();
                _notifyWizardGatingChanged();
            }).ConfigureAwait(false);

            loadTimer.Stop();
            RuntimeLog.Write(
                "GUILD_SEARCH",
                $"GuildSearch load completed. guildCount={_searchIndex.Count} elapsedMs={loadTimer.ElapsedMilliseconds} " +
                $"threadId={Environment.CurrentManagedThreadId} mainThread={MainThread.IsMainThread}.");
        }
        catch (OperationCanceledException)
        {
            loadTimer.Stop();
            RuntimeLog.Write(
                "GUILD_SEARCH",
                $"GuildSearch load cancelled elapsedMs={loadTimer.ElapsedMilliseconds}. " +
                $"threadId={Environment.CurrentManagedThreadId} mainThread={MainThread.IsMainThread}.");
            throw;
        }
        catch (Exception ex)
        {
            loadTimer.Stop();
            RuntimeLog.Write(
                "GUILD_SEARCH",
                $"GuildSearch load failed elapsedMs={loadTimer.ElapsedMilliseconds}.",
                ex);
            await RunOnMainThreadAsync(() => SearchStatus = "Guild load failed. Retry by reopening this screen.").ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (loadVersion == Volatile.Read(ref _loadVersion))
                await RunOnMainThreadAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    private async Task EnsureCardDetailsLoadedAsync(GuildCardVm card, GuildRecord? record = null)
    {
        if (card.DetailsLoaded)
            return;

        Task pendingTask;
        lock (_detailLoadGate)
        {
            if (card.DetailsLoaded)
                return;

            if (_detailLoadTasks.TryGetValue(card.Name, out var existingTask))
            {
                pendingTask = existingTask;
            }
            else
            {
                pendingTask = EnsureCardDetailsLoadedCoreAsync(card, record);
                _detailLoadTasks[card.Name] = pendingTask;
            }
        }

        await pendingTask;
    }

    private async Task EnsureCardDetailsLoadedCoreAsync(GuildCardVm card, GuildRecord? record)
    {
        await MainThread.InvokeOnMainThreadAsync(() => card.IsDetailsLoading = true);
        try
        {
            var rec = record;
            if (rec == null && !_guildRecords.TryGetValue(card.Name, out rec))
                rec = new GuildRecord();

            var details = await BuildCardDetailsAsync(card.Name, rec ?? new GuildRecord());
            await MainThread.InvokeOnMainThreadAsync(() => card.ApplyDetails(details));
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(
                "GUILD_DETAIL_LOAD",
                $"Failed to load guild details for '{card.Name}'.",
                ex);

            if (!card.DetailsLoaded)
                await MainThread.InvokeOnMainThreadAsync(() => card.ApplyDetails(new GuildCardDetailsVm()));
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => card.IsDetailsLoading = false);
            lock (_detailLoadGate)
                _detailLoadTasks.Remove(card.Name);
        }
    }

    private static async Task ExecuteGuildCardCommandSafeAsync(
        Func<Task> action,
        string operation,
        string? guildName)
    {
        if (action == null)
            return;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            var normalizedGuildName = string.IsNullOrWhiteSpace(guildName)
                ? "Unknown guild"
                : guildName.Trim();

            RuntimeLog.Write(
                operation,
                $"Guild interaction failed for '{normalizedGuildName}'.",
                ex);
        }
    }

    private async Task<GuildCardDetailsVm> BuildCardDetailsAsync(string guildName, GuildRecord record)
    {
        var rec = record ?? new GuildRecord();
        var benefits = rec.Benefits ?? new GuildBenefits();
        var guildType = (rec.Type ?? string.Empty).Trim();
        var applyTierAvailability = _allowGuildSelection;
        var includeBasic = !applyTierAvailability
            || GuildBenefitTierService.IsTierAvailable(_draft, guildType, GuildBenefitTier.Basic);
        var includeIntermediate = !applyTierAvailability
            || GuildBenefitTierService.IsTierAvailable(_draft, guildType, GuildBenefitTier.Intermediate);
        var includeAdvanced = !applyTierAvailability
            || GuildBenefitTierService.IsTierAvailable(_draft, guildType, GuildBenefitTier.Advanced);

        var denominatorRef = (rec.DenominationalMiracle?.Ref ?? string.Empty).Trim();
        var miracleLookup = denominatorRef.Length > 0
            ? await GetMiracleLookupAsync()
            : EmptyMiracleLookup;

        return await Task.Run(() =>
        {
            if (_detailMode == GuildCardDetailMode.MiracleOnly)
            {
                return new GuildCardDetailsVm
                {
                    MiracleRows = BuildMiracleRows(rec.MiracleList),
                    DenominationalMiracle = BuildDenominationalMiracle(rec, miracleLookup)
                };
            }

            return new GuildCardDetailsVm
            {
                PreRequisites = rec.PreRequisites ?? string.Empty,
                Restrictions = rec.Restrictions ?? string.Empty,
                Ethos = rec.Ethos ?? string.Empty,
                Background = rec.Background ?? string.Empty,
                LoreSections = BuildLoreSections(rec),
                BasicBenefits = includeBasic ? FormatBenefitList(benefits.Basic) : new List<string>(),
                IntermediateBenefits = includeIntermediate ? FormatBenefitList(benefits.Intermediate) : new List<string>(),
                AdvancedBenefits = includeAdvanced ? FormatBenefitList(benefits.Advanced) : new List<string>(),
                BasicBenefitRows = includeBasic
                    ? BuildBenefitRows(guildName, "Basic", benefits.Basic)
                    : new List<GuildBenefitRowVm>(),
                IntermediateBenefitRows = includeIntermediate
                    ? BuildBenefitRows(guildName, "Intermediate", benefits.Intermediate)
                    : new List<GuildBenefitRowVm>(),
                AdvancedBenefitRows = includeAdvanced
                    ? BuildBenefitRows(guildName, "Advanced", benefits.Advanced)
                    : new List<GuildBenefitRowVm>(),
                BasicOptionGroups = includeBasic
                    ? BuildBenefitOptionGroups(guildName, "Basic", benefits.Basic)
                    : new List<GuildBenefitOptionGroupVm>(),
                IntermediateOptionGroups = includeIntermediate
                    ? BuildBenefitOptionGroups(guildName, "Intermediate", benefits.Intermediate)
                    : new List<GuildBenefitOptionGroupVm>(),
                AdvancedOptionGroups = includeAdvanced
                    ? BuildBenefitOptionGroups(guildName, "Advanced", benefits.Advanced)
                    : new List<GuildBenefitOptionGroupVm>(),
                CityBenefits = FormatCityBenefits(rec.CityBenefits),
                MiracleRows = BuildMiracleRows(rec.MiracleList),
                DenominationalMiracle = BuildDenominationalMiracle(rec, miracleLookup)
            };
        }).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, GuildMiracleDefinition>> GetMiracleLookupAsync()
    {
        if (_miracleLookupCache != null)
            return _miracleLookupCache;

        var task = _miracleLookupTask ??= LoadMiracleLookupAsync();
        try
        {
            _miracleLookupCache = await task;
            return _miracleLookupCache;
        }
        catch
        {
            if (ReferenceEquals(_miracleLookupTask, task))
                _miracleLookupTask = null;
            throw;
        }
    }

    private (bool HasCityBound, string? CityName) GetCityBoundInfo()
    {
        var match = _draft.Abilities
            .Select(a => a?.Name ?? "")
            .FirstOrDefault(n => n.Contains("city bound", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(match))
            return (false, null);

        var lower = match.ToLowerInvariant();
        if (!lower.Contains("city bound"))
            return (false, null);
        var open = match.IndexOf('(');
        var close = match.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            var city = match.Substring(open + 1, close - open - 1).Trim();
            if (!string.IsNullOrWhiteSpace(city))
                return (true, city);
        }

        return (true, null);
    }

    private async Task RefreshContextAsync()
    {
        _currentClassName = (_draft.Class ?? string.Empty).Trim();
        _currentRaceName = (_draft.Race ?? string.Empty).Trim();
        _currentClassBrackets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _currentRaceSelections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _currentPeopleTypes.Clear();

        if (_currentClassName.Length > 0)
        {
            var classMap = await _creationDataService.GetClassesAsync();
            if (_creationDataService.TryGetByName(classMap, _currentClassName, out var rec) && rec != null)
            {
                AddBrackets(rec.Brackets);

                foreach (var pathClassName in ExtractPathClassNames(rec))
                {
                    if (!_creationDataService.TryGetByName(classMap, pathClassName, out var pathRecord) || pathRecord == null)
                        continue;

                    AddBrackets(pathRecord.Brackets);
                }
            }
        }

        if (_currentRaceName.Length > 0)
        {
            var peopleMap = await _creationDataService.GetPeopleAsync();
            if (_creationDataService.TryGetByName(peopleMap, _currentRaceName, out var rec) && rec != null)
            {
                foreach (var t in rec.PeopleType ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(t))
                        _currentPeopleTypes.Add(t.Trim());
                }
            }
        }

        if (HasBarbarianPeopleType(_draft))
            _currentPeopleTypes.Add("Tribal");

        var subtypePeopleType = (_draft.RaceSubtypeValue ?? _draft.RaceSubtype ?? string.Empty).Trim();
        if (subtypePeopleType.Equals("Verdant Heart", StringComparison.OrdinalIgnoreCase))
            _currentPeopleTypes.Add("Tribal");

        var subtype = (_draft.RaceSubtypeValue ?? _draft.RaceSubtype ?? string.Empty).Trim();
        if (subtype.Length > 0)
            _currentRaceSelections.Add(subtype);

        foreach (var selection in _draft.SpecialisationSelections.Values)
        {
            var s = (selection ?? string.Empty).Trim();
            if (s.Length > 0)
                _currentRaceSelections.Add(s);
        }

        void AddBrackets(IEnumerable<string>? brackets)
        {
            foreach (var b in brackets ?? Enumerable.Empty<string>())
            {
                var normalized = NormalizeLookupKey(b);
                if (normalized.Length > 0)
                    _currentClassBrackets.Add(normalized);
            }
        }
    }

    private static bool HasBarbarianPeopleType(CharacterDraft? draft)
    {
        if (draft?.SpecialisationSelections == null || draft.SpecialisationSelections.Count == 0)
            return false;

        if (draft.SpecialisationSelections.TryGetValue("Barbarian", out var directSelection)
            && string.Equals((directSelection ?? string.Empty).Trim(), "Barbarian", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return draft.SpecialisationSelections.Values.Any(selection =>
            string.Equals((selection ?? string.Empty).Trim(), "Barbarian", StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyAvailabilityToCurrentSelection()
    {
        if (!_applyCharacterAvailabilityFilters)
            return;

        RecomputeSlotRules();
        _slotRules.ApplyToCurrentSelection(_draft, _guildRecords);

        var kept = new List<string>();
        foreach (var g in _draft.Guilds)
        {
            var result = EvaluateAvailability(g);
            if (result.Allowed)
                kept.Add(g);
        }

        _draft.Guilds.Clear();
        foreach (var g in kept.Distinct(StringComparer.OrdinalIgnoreCase))
            _draft.Guilds.Add(g);
    }
    private AvailabilityResult EvaluateAvailability(string guildName)
    {
        if (string.IsNullOrWhiteSpace(guildName))
            return AvailabilityResult.Ok();

        if (_guildRecords.TryGetValue(guildName, out var rec) && rec != null)
            return EvaluateAvailability(rec, guildName);

        return AvailabilityResult.Ok();
    }

    private AvailabilityResult EvaluateAvailability(GuildRecord rec, string guildName)
    {
        var availability = rec.Availability ?? new GuildAvailability();

        if (!string.IsNullOrWhiteSpace(availability.RequiredGuild)
            && !_draft.Guilds.Contains(availability.RequiredGuild, StringComparer.OrdinalIgnoreCase))
        {
            return new AvailabilityResult(false, $"Requires {availability.RequiredGuild}.");
        }

        if (availability.Rules is { Count: > 0 })
        {
            if (!MeetsAvailabilityRules(availability.Rules))
                return new AvailabilityResult(false, "Does not meet availability rule.");

            return AvailabilityResult.Ok();
        }

        return AvailabilityResult.Ok();
    }

    private AvailabilityResult EvaluateAvailabilityForCurrentContext(GuildRecord rec, string guildName)
    {
        if (!_applyCharacterAvailabilityFilters)
            return AvailabilityResult.Ok();

        var baseResult = EvaluateAvailability(rec, guildName);
        if (!baseResult.Allowed)
            return baseResult;

        var slotResult = EvaluateSlotSelectability(guildName, rec);
        if (!slotResult.Allowed)
            return new AvailabilityResult(false, slotResult.Reason);

        return AvailabilityResult.Ok();
    }

    private AvailabilityResult EvaluateAvailabilityForCurrentContext(string guildName)
    {
        if (!_applyCharacterAvailabilityFilters)
            return AvailabilityResult.Ok();

        if (!_guildRecords.TryGetValue(guildName, out var rec) || rec == null)
            return EvaluateAvailability(guildName);

        return EvaluateAvailabilityForCurrentContext(rec, guildName);
    }

    private void RecomputeSlotRules()
    {
        _slotRules = GuildSlotRules.FromDraft(_draft);
        _slotRules.ResolvePeopleTypeOverrides(_guildRecords);
    }

    private GuildSelectability EvaluateSlotSelectability(string guildName, GuildRecord? rec = null)
    {
        var guild = rec;
        if (guild == null && !_guildRecords.TryGetValue(guildName, out guild))
            return new GuildSelectability(true, string.Empty);

        var type = guild?.Type ?? string.Empty;
        return _slotRules.CanSelect(type, guildName, _draft.Guilds, _guildRecords);
    }

    private bool MeetsAvailabilityRules(IEnumerable<RuleClause> rules)
    {
        var availableAlignments = CharacterDraft.ComputeAvailableAlignments(_getNonGuildRules());
        if (availableAlignments.Count == 0 && _draft.Alignment is Alignment selectedAlignment)
            availableAlignments.Add(selectedAlignment);

        IEnumerable<string> ResolveValues(string field)
        {
            var tokens = SplitRuleFieldTokens(field);
            if (tokens.Count == 0)
                return Array.Empty<string>();

            if (tokens.Count == 1)
                return ResolveSingleField(tokens[0]);

            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in tokens)
            {
                foreach (var value in ResolveSingleField(token))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        merged.Add(value);
                }
            }

            return merged;

            IEnumerable<string> ResolveSingleField(string normalizedField)
                => normalizedField switch
                {
                    "class" or "classes" => ToSingleValue(_currentClassName),
                    "bracket" or "brackets" => _currentClassBrackets,
                    "race" or "races" => ToSingleValue(_currentRaceName),
                    "racesubtype" or "subtype" => _currentRaceSelections,
                    "peopletype" or "peopletypes" => _currentPeopleTypes,
                    "alignmentorder" => availableAlignments.Select(a => a.Order.ToString()),
                    "alignmentmoral" => availableAlignments.Select(a => a.Moral.ToString()),
                    "status" or "statuses" => Array.Empty<string>(),
                    "guild" or "guilds" => _draft.Guilds ?? new List<string>(),
                    _ => Array.Empty<string>()
                };
        }

        return RuleTreeEvaluator.Evaluate(rules, ResolveValues, NormalizeLookupKey);
    }

    private static List<string> SplitRuleFieldTokens(string? field)
    {
        var raw = (field ?? string.Empty).Trim();
        if (raw.Length == 0)
            return new List<string>();

        return raw
            .Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeRuleField)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> ExtractPathClassNames(CharacterClassRecord classRecord)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddName(classRecord.Path);

        foreach (var buyAs in classRecord.BuyAs ?? Enumerable.Empty<string>())
            AddName(ParsePathClassFromBuyAs(buyAs));

        return names;

        void AddName(string? value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length > 0)
                names.Add(trimmed);
        }
    }

    private static string ParsePathClassFromBuyAs(string? buyAs)
    {
        var raw = (buyAs ?? string.Empty).Trim();
        if (raw.Length == 0)
            return string.Empty;

        var match = Regex.Match(raw, @"\bclass\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return raw;
    }

    private static string NormalizeLookupKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static string NormalizeRuleField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static IEnumerable<string> ToSingleValue(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0
            ? Array.Empty<string>()
            : new[] { trimmed };
    }

    private static string IconForType(string type)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();
        if (t == "political") return "🏛️";
        if (t == "professional") return "🛠️";
        if (t.Contains("relig")) return "⛪";
        return "📜";
    }

    private static string NormalizeLogoPath(string? rawPath)
    {
        var value = (rawPath ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        value = value.Replace('\\', '/');

        if (value.StartsWith("~/", StringComparison.Ordinal))
            value = value[2..];

        const string imagesPrefix = "Resources/Images/";
        if (value.StartsWith(imagesPrefix, StringComparison.OrdinalIgnoreCase))
            value = value[imagesPrefix.Length..];

        var lastSlash = value.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < value.Length - 1)
            value = value[(lastSlash + 1)..];

        return value.Trim();
    }

    private bool HasAvailableChoiceOptions(GuildRecord record)
    {
        if (record == null)
            return false;

        var benefits = record.Benefits ?? new GuildBenefits();
        var guildType = (record.Type ?? string.Empty).Trim();
        var includeBasic = !_allowGuildSelection
            || GuildBenefitTierService.IsTierAvailable(_draft, guildType, GuildBenefitTier.Basic);
        var includeIntermediate = !_allowGuildSelection
            || GuildBenefitTierService.IsTierAvailable(_draft, guildType, GuildBenefitTier.Intermediate);
        var includeAdvanced = !_allowGuildSelection
            || GuildBenefitTierService.IsTierAvailable(_draft, guildType, GuildBenefitTier.Advanced);

        if (includeBasic && TierHasChoiceOptions(benefits.Basic))
            return true;
        if (includeIntermediate && TierHasChoiceOptions(benefits.Intermediate))
            return true;
        if (includeAdvanced && TierHasChoiceOptions(benefits.Advanced))
            return true;

        return false;
    }

    private static bool TierHasChoiceOptions(IEnumerable<GuildBenefitEntry>? benefits)
        => (benefits ?? Enumerable.Empty<GuildBenefitEntry>())
            .Any(entry => entry?.Options is { Count: > 0 });

    private static List<GuildMiracleRowVm> BuildMiracleRows(Dictionary<string, List<string>>? miracleList)
    {
        if (miracleList == null || miracleList.Count == 0)
            return new List<GuildMiracleRowVm>();

        var ordered = miracleList
            .Select(kvp =>
            {
                var key = (kvp.Key ?? string.Empty).Trim();
                int? level = int.TryParse(key, out var parsed) ? parsed : null;
                var miracles = kvp.Value ?? new List<string>();
                return (Key: key, Level: level, Miracles: miracles);
            })
            .Where(x => x.Key.Length > 0)
            .OrderBy(x => x.Level ?? int.MaxValue)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<GuildMiracleRowVm>();
        foreach (var entry in ordered)
        {
            var names = entry.Miracles
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
                continue;

            foreach (var name in names)
            {
                rows.Add(new GuildMiracleRowVm
                {
                    LevelText = entry.Level?.ToString() ?? entry.Key,
                    Name = name
                });
            }
        }

        for (var i = 0; i < rows.Count; i++)
            rows[i].RowBackgroundColor = i % 2 == 0 ? "#FFFFFF" : "#F9FAFB";

        return rows;
    }

    private static async Task<IReadOnlyDictionary<string, GuildMiracleDefinition>> LoadMiracleLookupAsync()
    {
        try
        {
            var json = await ServiceHelper.ReadPackageTextAsync("words_from_above/miracles.json");
            var list = JsonSerializer.Deserialize<List<GuildMiracleDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<GuildMiracleDefinition>();

            return list
                .Where(m => !string.IsNullOrWhiteSpace(m.name))
                .GroupBy(m => m.name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, GuildMiracleDefinition>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static GuildDenominationalMiracleVm? BuildDenominationalMiracle(
        GuildRecord record,
        IReadOnlyDictionary<string, GuildMiracleDefinition> miracleLookup)
    {
        var reference = (record.DenominationalMiracle?.Ref ?? string.Empty).Trim();
        if (reference.Length == 0)
            return null;

        var note = (record.DenominationalMiracleNote ?? string.Empty).Trim();
        if (!miracleLookup.TryGetValue(reference, out var miracle))
        {
            return new GuildDenominationalMiracleVm
            {
                Name = reference,
                Cost = "todo",
                Alignment = "todo",
                Duration = "todo",
                Range = "todo",
                Description = "todo",
                Verbal = "todo",
                Sphere = "todo",
                Gesture = "todo",
                IsAdvancedText = "todo",
                Note = note
            };
        }

        var cost = (miracle.level ?? string.Empty).Trim();
        if (cost.Length == 0 && miracle.power > 0)
            cost = $"{miracle.power}sp";

        return new GuildDenominationalMiracleVm
        {
            Name = (miracle.name ?? string.Empty).Trim(),
            Cost = cost,
            Alignment = (miracle.alignment ?? string.Empty).Trim(),
            Duration = (miracle.duration ?? string.Empty).Trim(),
            Range = (miracle.range ?? string.Empty).Trim(),
            Description = (miracle.description ?? string.Empty).Trim(),
            Verbal = (miracle.verbal ?? string.Empty).Trim(),
            Sphere = (miracle.sphere ?? string.Empty).Trim(),
            Gesture = (miracle.gesture ?? string.Empty).Trim(),
            IsAdvancedText = miracle.isAdvanced ? "Yes" : "No",
            Note = note
        };
    }

    private static List<string> FormatBenefitList(IEnumerable<GuildBenefitEntry>? benefits)
    {
        var list = new List<string>();
        foreach (var benefit in benefits ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            if (benefit?.Ability == null)
                continue;

            var display = FormatBenefit(benefit.Ability);
            if (!string.IsNullOrWhiteSpace(display))
                list.Add(display);
        }
        return list;
    }

    private static List<GuildCityBenefitVm> FormatCityBenefits(IEnumerable<GuildCityBenefit>? cityBenefits)
    {
        var list = new List<GuildCityBenefitVm>();
        foreach (var cityBenefit in cityBenefits ?? Enumerable.Empty<GuildCityBenefit>())
        {
            if (cityBenefit == null)
                continue;

            var name = (cityBenefit.Name ?? string.Empty).Trim();
            var effects = (cityBenefit.Effects ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            if (name.Length == 0 && effects.Count == 0)
                continue;

            list.Add(new GuildCityBenefitVm
            {
                Name = name.Length > 0 ? name : "City Benefit",
                Effects = effects
            });
        }

        return list;
    }

    private static string FormatBenefit(AbilityDefinition? benefit)
    {
        if (benefit == null)
            return string.Empty;

        var name = (benefit.Name ?? string.Empty).Trim();
        var effect = (benefit.Effect ?? string.Empty).Trim();
        var frequency = (benefit.Frequency ?? string.Empty).Trim();
        var display = name;

        if (!string.IsNullOrWhiteSpace(effect))
            display = display.Length > 0 ? $"{display}: {effect}" : effect;

        if (benefit.Count.HasValue && benefit.Count.Value > 1)
            display = display.Length > 0 ? $"{display} (x{benefit.Count.Value})" : $"x{benefit.Count.Value}";

        if (!string.IsNullOrWhiteSpace(frequency))
            display = display.Length > 0 ? $"{display} [{frequency}]" : frequency;

        return display;
    }

    private void RebuildTypeFilterChips()
    {
        var distinctTypes = TypeFilters
            .Where(t => !string.IsNullOrWhiteSpace(t) && !string.Equals(t, AllTypeFilterValue, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _selectedTypeFilters.RemoveWhere(type => !distinctTypes.Contains(type, StringComparer.OrdinalIgnoreCase));
        var isAllSelected = _selectedTypeFilters.Count == 0;

        TypeFilterChips.Clear();
        TypeFilterChips.Add(new GuildTypeFilterChipVm(AllTypeFilterValue, isAllSelected));
        foreach (var type in distinctTypes)
        {
            TypeFilterChips.Add(new GuildTypeFilterChipVm(
                type,
                _selectedTypeFilters.Contains(type)));
        }
    }

    private void ToggleTypeFilterChip(GuildTypeFilterChipVm? chip)
    {
        if (!_useMultiTypeFilters || chip == null)
            return;

        if (string.Equals(chip.Type, AllTypeFilterValue, StringComparison.OrdinalIgnoreCase))
        {
            _selectedTypeFilters.Clear();
        }
        else
        {
            if (!_selectedTypeFilters.Add(chip.Type))
                _selectedTypeFilters.Remove(chip.Type);
        }

        RebuildTypeFilterChips();
        ScheduleRefilter(debounce: false);
    }
    public int SelectedCount => _draft.Guilds.Count;

    private void RecomputeDraftAlignments()
    {
        RecomputeSlotRules();
        _draft.SetAvailableAlignmentsFromRules(_getNonGuildRules());

        // Refresh selectability now that the world changed
        foreach (var card in AllGuilds)
        {
            card.IsSelected = _draft.Guilds.Contains(card.Name, StringComparer.OrdinalIgnoreCase);
            var availability = EvaluateAvailabilityForCurrentContext(card.Name);
            var selectable = availability.Allowed;
            if (_guildRecords.TryGetValue(card.Name, out var rec) && rec != null)
                card.IsLocked = _slotRules.IsGuildLocked(rec.Type ?? string.Empty, card.Name);
            else
                card.IsLocked = false;

            // Allow already-selected guilds to stay selectable so the user can deselect them
            card.IsSelectable = !_allowGuildSelection
                || !_enforceAvailabilityForSelection
                || (selectable || card.IsSelected);

            if (_enforceAvailabilityForSelection && !availability.Allowed)
                card.NotSelectableReason = availability.Reason;
            else
                card.NotSelectableReason = "";

            if (!_allowGuildSelection)
                card.NotSelectableReason = "";
        }

        RaiseSelectedGuildsChanged();
        if (_hideUnavailableGuilds)
            ScheduleRefilter(debounce: false);
    }

    private void RecomputeDraftAlignmentOptionsOnly()
    {
        RecomputeSlotRules();
        _draft.SetAvailableAlignmentsFromRules(_getNonGuildRules());
        RaiseSelectedGuildsChanged();
        if (_hideUnavailableGuilds)
            ScheduleRefilter(debounce: false);
    }

    private async Task ToggleExpandedAsync(GuildCardVm? item)
    {
        if (item == null)
            return;

        var shouldExpand = !item.IsExpanded;

        foreach (var g in FilteredGuilds)
        {
            if (!ReferenceEquals(g, item) && g.IsExpanded)
                g.IsExpanded = false;
        }

        item.IsExpanded = shouldExpand;

        if (shouldExpand)
            _ = EnsureCardDetailsLoadedAsync(item);
    }

    private static bool RecordMatchesSearch(string guildName, GuildRecord record, string text)
    {
        if (guildName.Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        var rec = record ?? new GuildRecord();
        if ((rec.PreRequisites ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.Restrictions ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.Ethos ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.Background ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.DenominationalMiracle?.Ref ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if ((rec.DenominationalMiracleNote ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;

        if (MiracleListContainsText(rec.MiracleList, text))
            return true;

        if (CityBenefitsContainText(rec.CityBenefits, text))
            return true;

        var benefits = rec.Benefits ?? new GuildBenefits();
        return BenefitEntriesContainText(benefits.Basic, text)
               || BenefitEntriesContainText(benefits.Intermediate, text)
               || BenefitEntriesContainText(benefits.Advanced, text);
    }

    private static string BuildSearchCorpus(string guildName, GuildRecord record)
    {
        var builder = new StringBuilder(512);
        builder.Append(guildName).Append('|');

        var rec = record ?? new GuildRecord();
        AppendCorpus(builder, rec.PreRequisites);
        AppendCorpus(builder, rec.Restrictions);
        AppendCorpus(builder, rec.Ethos);
        AppendCorpus(builder, rec.Background);
        AppendCorpus(builder, rec.DenominationalMiracle?.Ref);
        AppendCorpus(builder, rec.DenominationalMiracleNote);

        foreach (var pair in rec.MiracleList ?? new Dictionary<string, List<string>>())
        {
            AppendCorpus(builder, pair.Key);
            foreach (var value in pair.Value ?? new List<string>())
                AppendCorpus(builder, value);
        }

        foreach (var cityBenefit in rec.CityBenefits ?? Enumerable.Empty<GuildCityBenefit>())
        {
            AppendCorpus(builder, cityBenefit?.Name);
            foreach (var effect in cityBenefit?.Effects ?? new List<string>())
                AppendCorpus(builder, effect);
        }

        var benefits = rec.Benefits ?? new GuildBenefits();
        AppendBenefitCorpus(builder, benefits.Basic);
        AppendBenefitCorpus(builder, benefits.Intermediate);
        AppendBenefitCorpus(builder, benefits.Advanced);

        return NormalizeSearchText(builder.ToString());
    }

    private static void AppendBenefitCorpus(StringBuilder builder, IEnumerable<GuildBenefitEntry>? entries)
    {
        foreach (var entry in entries ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            AppendCorpus(builder, FormatBenefit(entry?.Ability));
            foreach (var option in entry?.Options ?? new List<GuildBenefitOption>())
            {
                foreach (var ability in option.Abilities ?? new List<AbilityDefinition>())
                    AppendCorpus(builder, FormatBenefit(ability));
            }
        }
    }

    private static void AppendCorpus(StringBuilder builder, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return;

        builder.Append(text).Append('|');
    }

    private static string NormalizeSearchText(string value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private sealed record GuildSearchIndexEntry(
        GuildCardVm Card,
        string Type,
        string NameNormalized,
        string SearchCorpusNormalized,
        bool IsSelected,
        bool IsSelectable,
        bool EnforceAvailability);

    private sealed record GuildSearchCriteria(
        int Version,
        string Text,
        string SelectedTypeFilter,
        HashSet<string> MultiTypeFilters,
        bool UseMultiTypeFilters,
        bool SearchByNameOnly,
        bool HideUnavailable);

    private static bool MiracleListContainsText(Dictionary<string, List<string>>? miracleList, string text)
    {
        foreach (var pair in miracleList ?? new Dictionary<string, List<string>>())
        {
            if ((pair.Key ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var name in pair.Value ?? new List<string>())
            {
                if ((name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool BenefitEntriesContainText(IEnumerable<GuildBenefitEntry>? entries, string text)
    {
        foreach (var entry in entries ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            var direct = FormatBenefit(entry?.Ability);
            if (direct.Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var option in entry?.Options ?? new List<GuildBenefitOption>())
            {
                foreach (var ability in option.Abilities ?? new List<AbilityDefinition>())
                {
                    var optionLine = FormatBenefit(ability);
                    if (optionLine.Contains(text, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool CityBenefitsContainText(IEnumerable<GuildCityBenefit>? cityBenefits, string text)
    {
        foreach (var cityBenefit in cityBenefits ?? Enumerable.Empty<GuildCityBenefit>())
        {
            if ((cityBenefit?.Name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var effect in cityBenefit?.Effects ?? new List<string>())
            {
                if ((effect ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private List<GuildBenefitRowVm> BuildBenefitRows(
        string guildName,
        string tier,
        IEnumerable<GuildBenefitEntry>? benefits)
    {
        var rows = new List<GuildBenefitRowVm>();
        var selections = EnsureGuildBenefitSelections();
        var optionIndex = 0;

        foreach (var entry in benefits ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            if (entry?.Ability != null)
            {
                var ability = entry.Ability;
                var display = BuildBenefitNameText(ability);
                if (string.IsNullOrWhiteSpace(display))
                    display = FormatBenefit(ability);
                if (string.IsNullOrWhiteSpace(display))
                    continue;

                rows.Add(new GuildBenefitRowVm
                {
                    DisplayText = display,
                    DisplayItems = new List<GuildBenefitDisplayItemVm>
                    {
                        new() { Text = display }
                    },
                    Abilities = new List<AbilityDefinition> { ability },
                    IsChoiceOption = false,
                    IsSelectedChoice = false,
                    ChoiceLabel = string.Empty
                });
                continue;
            }

            if (entry?.Options == null || entry.Options.Count == 0)
                continue;

            optionIndex++;
            var selectionKey = GuildBenefitKeys.BuildSelectionKey(guildName, tier, optionIndex);
            var selectedIndex = selections.TryGetValue(selectionKey, out var savedSelection)
                ? savedSelection
                : (int?)null;

            var optionDisplays = new List<GuildBenefitDisplayItemVm>();
            var rowAbilities = new List<AbilityDefinition>();
            var seenAbilityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var optionPosition = 0; optionPosition < entry.Options.Count; optionPosition++)
            {
                var option = entry.Options[optionPosition];
                var optionAbilities = (option?.Abilities ?? new List<AbilityDefinition>())
                    .Where(ability => ability != null)
                    .Select(ability => ability)
                    .ToList();

                if (optionAbilities.Count == 0)
                    continue;

                var display = BuildOptionNameSummary(optionAbilities);
                if (string.IsNullOrWhiteSpace(display))
                    display = BuildOptionLabel(optionAbilities);
                if (string.IsNullOrWhiteSpace(display))
                    continue;

                optionDisplays.Add(new GuildBenefitDisplayItemVm
                {
                    Text = display,
                    IsSelected = selectedIndex.HasValue && selectedIndex.Value == optionPosition + 1,
                    ShowTrailingDivider = false
                });

                foreach (var optionAbility in optionAbilities)
                {
                    var dedupeKey = BuildGuildBenefitAbilityKey(optionAbility);
                    if (!seenAbilityKeys.Add(dedupeKey))
                        continue;

                    rowAbilities.Add(optionAbility);
                }
            }

            if (optionDisplays.Count == 0)
                continue;

            for (var displayIndex = 0; displayIndex < optionDisplays.Count; displayIndex++)
            {
                optionDisplays[displayIndex].ShowTrailingDivider = displayIndex < optionDisplays.Count - 1;
            }

            rows.Add(new GuildBenefitRowVm
            {
                DisplayText = selectedIndex.HasValue && selectedIndex.Value > 0 && selectedIndex.Value <= optionDisplays.Count
                    ? optionDisplays[selectedIndex.Value - 1].Text
                    : "Choose one option",
                DisplayItems = optionDisplays,
                ChildRows = optionDisplays
                    .Select((item, childIndex) => new GuildBenefitChildRowVm
                    {
                        Text = item.Text,
                        IsSelected = item.IsSelected,
                        IsLastChild = childIndex == optionDisplays.Count - 1
                    })
                    .ToList(),
                Abilities = rowAbilities,
                IsChoiceOption = true,
                IsSelectedChoice = selectedIndex.HasValue,
                ChoiceLabel = $"Choice {optionIndex}"
            });
        }

        for (var i = 0; i < rows.Count; i++)
            rows[i].RowBackgroundColor = i % 2 == 0 ? "#FFFFFF" : "#F9FAFB";

        return rows;
    }

    private static string BuildBenefitNameText(AbilityDefinition? ability)
    {
        var name = (ability?.Name ?? string.Empty).Trim();
        if (name.Length > 0)
            return name;

        var battleboardName = (ability?.BattleboardNameOverride ?? string.Empty).Trim();
        if (battleboardName.Length > 0)
            return battleboardName;

        var updateKey = (ability?.UpdateKey ?? string.Empty).Trim();
        if (updateKey.Length > 0)
            return updateKey;

        var key = (ability?.AbilityRef ?? ability?.Key ?? string.Empty).Trim();
        return key;
    }

    private static string BuildOptionNameSummary(IEnumerable<AbilityDefinition> abilities)
    {
        var names = (abilities ?? Enumerable.Empty<AbilityDefinition>())
            .Select(BuildBenefitNameText)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
            return string.Empty;

        return string.Join(", ", names);
    }

    private List<GuildBenefitOptionGroupVm> BuildBenefitOptionGroups(
        string guildName,
        string tier,
        IEnumerable<GuildBenefitEntry>? benefits)
    {
        var list = new List<GuildBenefitOptionGroupVm>();
        if (string.IsNullOrWhiteSpace(guildName))
            return list;

        var selections = EnsureGuildBenefitSelections();
        var optionIndex = 0;
        foreach (var entry in benefits ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            if (entry?.Options == null || entry.Options.Count == 0)
                continue;

            optionIndex++;
            var key = GuildBenefitKeys.BuildSelectionKey(guildName, tier, optionIndex);
            var options = entry.Options
                .Select(o => BuildOptionVm(o))
                .Where(o => o != null)
                .Cast<GuildBenefitOptionVm>()
                .ToList();

            if (options.Count == 0)
                continue;

            var selected = selections.TryGetValue(key, out var saved)
                ? saved
                : (int?)null;

            var vm = new GuildBenefitOptionGroupVm(key, options, selected, OnBenefitOptionSelectionChanged);
            list.Add(vm);
        }

        return list;
    }

    private GuildBenefitOptionVm? BuildOptionVm(GuildBenefitOption option)
    {
        if (option?.Abilities == null || option.Abilities.Count == 0)
            return null;

        var label = BuildOptionLabel(option.Abilities);
        var lines = option.Abilities
            .Select(FormatBenefit)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return new GuildBenefitOptionVm
        {
            Label = label,
            Abilities = option.Abilities,
            Lines = lines
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

    private static string BuildGuildBenefitAbilityKey(AbilityDefinition? ability)
    {
        if (ability == null)
            return string.Empty;

        return string.Join(
            "|",
            new[]
            {
                (ability.Key ?? string.Empty).Trim(),
                (ability.AbilityRef ?? string.Empty).Trim(),
                (ability.Name ?? string.Empty).Trim(),
                (ability.UpdateKey ?? string.Empty).Trim()
            });
    }

    private static List<GuildLoreSectionVm> BuildLoreSections(GuildRecord rec)
    {
        var sections = new List<GuildLoreSectionVm>();

        AddIfPresent("PRE-REQUISITES", rec.PreRequisites);
        AddIfPresent("RESTRICTIONS", rec.Restrictions);
        AddIfPresent("ETHOS", rec.Ethos);
        AddIfPresent("BACKGROUND", rec.Background);

        return sections;

        void AddIfPresent(string header, string? text)
        {
            var value = CleanLoreText(text);
            if (value.Length == 0)
                return;

            sections.Add(new GuildLoreSectionVm(header, value));
        }
    }

    private static string CleanLoreText(string? text)
    {
        var value = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (value.Length == 0)
            return string.Empty;

        value = MiracleListLoreBlockRegex.Replace(value, "\n");
        value = DenominationalMiracleLoreBlockRegex.Replace(value, "\n");
        value = MiracleStatLoreBlockRegex.Replace(value, "\n");
        value = Regex.Replace(value, @"\n{3,}", "\n\n");
        value = ExpandParagraphSpacing(value);
        return value.Trim();
    }

    private static string ExpandParagraphSpacing(string value)
    {
        var lines = value.Split('\n');
        if (lines.Length < 2)
            return value;

        var sb = new StringBuilder(value.Length + 64);
        for (var i = 0; i < lines.Length; i++)
        {
            var current = lines[i];
            sb.Append(current);

            if (i >= lines.Length - 1)
                continue;

            var currentTrimmed = current.Trim();
            var nextTrimmed = lines[i + 1].Trim();

            if (currentTrimmed.Length == 0 || nextTrimmed.Length == 0)
            {
                sb.Append('\n');
                continue;
            }

            var currentIsList = ListPrefixRegex.IsMatch(currentTrimmed);
            var nextIsList = ListPrefixRegex.IsMatch(nextTrimmed);
            sb.Append(currentIsList || nextIsList ? '\n' : "\n\n");
        }

        return Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n");
    }

    private void OnBenefitOptionSelectionChanged(GuildBenefitOptionGroupVm group)
    {
        if (group == null)
            return;

        ApplyBenefitOptionSelection(group.SelectionKey, group.SelectedIndex);
    }

    private async Task EnsureGuildRecordsLoadedAsync()
    {
        if (_guildRecords.Count > 0)
            return;

        _guildRecords = _detailMode == GuildCardDetailMode.MiracleOnly
            ? await _creationDataService.GetGuildsForMiracleSearchAsync()
            : await _creationDataService.GetGuildsAsync();
        _guildRecords ??= new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, int> EnsureGuildBenefitSelections()
    {
        if (_draft.GuildBenefitSelections == null)
            _draft.GuildBenefitSelections = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        return _draft.GuildBenefitSelections;
    }

    private void ApplyBenefitOptionSelection(string selectionKey, int? selectedIndex)
    {
        var key = (selectionKey ?? string.Empty).Trim();
        if (key.Length == 0)
            return;

        var selections = EnsureGuildBenefitSelections();
        if (selectedIndex.HasValue && selectedIndex.Value > 0)
            selections[key] = selectedIndex.Value;
        else
            selections.Remove(key);

        Raise(nameof(IsComplete));
        _notifyWizardGatingChanged();

        if (_refreshDraftAbilitiesAsync != null)
            MainThread.BeginInvokeOnMainThread(() => _ = ExecuteGuildCardCommandSafeAsync(
                action: _refreshDraftAbilitiesAsync,
                operation: "GUILD_REFRESH_DRAFT_ABILITIES",
                guildName: null));
    }

    private bool AreRequiredBenefitChoicesComplete(IReadOnlyDictionary<string, GuildRecord> records)
    {
        foreach (var requirement in EnumerateAvailableChoiceRequirements(
                     records,
                     onlyIncomplete: false,
                     includeUnavailableTiers: false))
        {
            if (!requirement.HasValidSelection)
                return false;
        }

        return true;
    }

    private IEnumerable<GuildChoiceRequirement> EnumerateAvailableChoiceRequirements(
        IReadOnlyDictionary<string, GuildRecord> records,
        bool onlyIncomplete,
        bool includeUnavailableTiers)
    {
        var selections = EnsureGuildBenefitSelections();

        foreach (var guildName in _draft.Guilds
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Select(name => name.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!records.TryGetValue(guildName, out var record) || record == null)
                continue;

            var guildType = (record.Type ?? string.Empty).Trim();
            var benefits = record.Benefits ?? new GuildBenefits();

            foreach (var tier in new[] { GuildBenefitTier.Basic, GuildBenefitTier.Intermediate, GuildBenefitTier.Advanced })
            {
                var isTierAvailableNow = GuildBenefitTierService.IsTierAvailable(_draft, guildType, tier);
                if (!includeUnavailableTiers && !isTierAvailableNow)
                    continue;

                var tierName = ToTierName(tier);
                var optionIndex = 0;
                foreach (var entry in GetTierBenefits(benefits, tier))
                {
                    if (entry?.Options == null || entry.Options.Count == 0)
                        continue;

                    optionIndex++;
                    var selectionKey = GuildBenefitKeys.BuildSelectionKey(guildName, tierName, optionIndex);
                    var selectedIndex = selections.TryGetValue(selectionKey, out var saved)
                        ? saved
                        : (int?)null;
                    var requirement = new GuildChoiceRequirement(
                        guildName,
                        tierName,
                        optionIndex,
                        selectionKey,
                        entry.Options,
                        selectedIndex,
                        isTierAvailableNow);

                    if (onlyIncomplete && requirement.HasValidSelection)
                        continue;

                    yield return requirement;
                }
            }
        }
    }

    private static IEnumerable<GuildBenefitEntry> GetTierBenefits(GuildBenefits benefits, GuildBenefitTier tier)
        => tier switch
        {
            GuildBenefitTier.Basic => benefits.Basic ?? new List<GuildBenefitEntry>(),
            GuildBenefitTier.Intermediate => benefits.Intermediate ?? new List<GuildBenefitEntry>(),
            GuildBenefitTier.Advanced => benefits.Advanced ?? new List<GuildBenefitEntry>(),
            _ => new List<GuildBenefitEntry>()
        };

    private static string ToTierName(GuildBenefitTier tier)
        => tier switch
        {
            GuildBenefitTier.Basic => "Basic",
            GuildBenefitTier.Intermediate => "Intermediate",
            GuildBenefitTier.Advanced => "Advanced",
            _ => "Basic"
        };

    private sealed record GuildChoiceRequirement(
        string GuildName,
        string TierLabel,
        int OptionIndex,
        string SelectionKey,
        IReadOnlyList<GuildBenefitOption> Options,
        int? SelectedIndex,
        bool IsTierAvailableNow)
    {
        public bool HasValidSelection =>
            SelectedIndex.HasValue
            && SelectedIndex.Value > 0
            && SelectedIndex.Value <= Options.Count;
    }

    private async Task ToggleSelectedAsync(GuildCardVm? item)
    {
        if (!_allowGuildSelection)
        {
            await ToggleExpandedAsync(item);
            return;
        }

        if (item is null || (!item.IsSelectable && !item.IsSelected))
            return;

        var availability = EvaluateAvailabilityForCurrentContext(item.Name);
        if (_enforceAvailabilityForSelection && !item.IsSelected && !availability.Allowed)
        {
            item.NotSelectableReason = availability.Reason;
            return;
        }

        var idx = _draft.Guilds.FindIndex(g => string.Equals(g, item.Name, StringComparison.OrdinalIgnoreCase));
        var nowSelected = idx < 0;

        if (nowSelected)
        {
            _draft.Guilds.Add(item.Name);
            item.IsSelected = true;
            if (_guildRecords.TryGetValue(item.Name, out var rec) && rec != null)
                await EnsureCardDetailsLoadedAsync(item, rec);
            else
                await EnsureCardDetailsLoadedAsync(item);
        }
        else
        {
            _draft.Guilds.RemoveAt(idx);
            item.IsSelected = false;
        }

        ApplyAvailabilityToCurrentSelection();

        Raise(nameof(SelectedCount));
        RecomputeDraftAlignments();
        _notifyWizardGatingChanged();

        if (_refreshDraftAbilitiesAsync != null)
            await _refreshDraftAbilitiesAsync();
    }
}

public sealed class GuildTypeFilterChipVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isSelected;

    public GuildTypeFilterChipVm(string type, bool isSelected)
    {
        Type = (type ?? string.Empty).Trim();
        _isSelected = isSelected;
    }

    public string Type { get; }
    public string Label => Type;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed class GuildCardDetailsVm
{
    public string PreRequisites { get; init; } = string.Empty;
    public string Restrictions { get; init; } = string.Empty;
    public string Ethos { get; init; } = string.Empty;
    public string Background { get; init; } = string.Empty;
    public List<GuildLoreSectionVm> LoreSections { get; init; } = new();
    public List<string> BasicBenefits { get; init; } = new();
    public List<string> IntermediateBenefits { get; init; } = new();
    public List<string> AdvancedBenefits { get; init; } = new();
    public List<GuildBenefitRowVm> BasicBenefitRows { get; init; } = new();
    public List<GuildBenefitRowVm> IntermediateBenefitRows { get; init; } = new();
    public List<GuildBenefitRowVm> AdvancedBenefitRows { get; init; } = new();
    public List<GuildBenefitOptionGroupVm> BasicOptionGroups { get; init; } = new();
    public List<GuildBenefitOptionGroupVm> IntermediateOptionGroups { get; init; } = new();
    public List<GuildBenefitOptionGroupVm> AdvancedOptionGroups { get; init; } = new();
    public List<GuildCityBenefitVm> CityBenefits { get; init; } = new();
    public List<GuildMiracleRowVm> MiracleRows { get; init; } = new();
    public GuildDenominationalMiracleVm? DenominationalMiracle { get; init; }
}

public sealed class GuildCardVm : INotifyPropertyChanged
{
    public GuildCardVm()
    {
        ToggleMiracleListCommand = new Command(() => IsMiracleListExpanded = !IsMiracleListExpanded);
        ToggleBenefitsCommand = new Command(() => IsBenefitsExpanded = !IsBenefitsExpanded);
        ToggleCityBenefitsCommand = new Command(() => IsCityBenefitsExpanded = !IsCityBenefitsExpanded);
        ToggleDenominationalMiracleCommand = new Command(() => IsDenominationalMiracleExpanded = !IsDenominationalMiracleExpanded);
    }

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked == value) return;
            _isLocked = value;
            Raise();
        }
    }

    private bool _isSelectable = true;
    public bool IsSelectable
    {
        get => _isSelectable;
        set
        {
            if (_isSelectable == value) return;
            _isSelectable = value;
            Raise();
            Raise(nameof(CanToggleSelection));
            Raise(nameof(HasNotSelectableReason));
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
            Raise(nameof(HasNotSelectableReason));
        }
    }
    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public int Id { get; set; }

    private string _name = "";
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value ?? string.Empty;
            Raise();
            Raise(nameof(DisplayName));
        }
    }

    public string DisplayName => _name.Length > 15 ? $"{_name[..12]}..." : _name;
    public string SelectionActionGlyph => IsSelected ? "\uf068" : "\uf067";
    public bool CanToggleSelection => IsSelectable || IsSelected;
    public bool HasNotSelectableReason => !CanToggleSelection && !string.IsNullOrWhiteSpace(NotSelectableReason);
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "📜";

    private string _logo = "";
    public string Logo
    {
        get => _logo;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_logo, normalized, StringComparison.Ordinal))
                return;

            _logo = normalized;
            Raise();
            Raise(nameof(HasLogo));
            Raise(nameof(ShowLogo));
            Raise(nameof(ShowIcon));
        }
    }

    public bool HasLogo => !string.IsNullOrWhiteSpace(Logo);
    public bool ShowLogo => HasLogo && (IsExpanded || IsSelected);
    public bool ShowIcon => !ShowLogo;

    private bool _detailsLoaded;
    public bool DetailsLoaded
    {
        get => _detailsLoaded;
        private set
        {
            if (_detailsLoaded == value)
                return;

            _detailsLoaded = value;
            Raise();
        }
    }

    private bool _isDetailsLoading;
    public bool IsDetailsLoading
    {
        get => _isDetailsLoading;
        set
        {
            if (_isDetailsLoading == value)
                return;

            _isDetailsLoading = value;
            Raise();
        }
    }

    public string PreRequisites { get; set; } = "";
    public string Restrictions { get; set; } = "";
    public string Ethos { get; set; } = "";
    public string Background { get; set; } = "";
    public List<GuildLoreSectionVm> LoreSections { get; set; } = new();
    public List<string> BasicBenefits { get; set; } = new();
    public List<string> IntermediateBenefits { get; set; } = new();
    public List<string> AdvancedBenefits { get; set; } = new();
    public List<GuildBenefitRowVm> BasicBenefitRows { get; set; } = new();
    public List<GuildBenefitRowVm> IntermediateBenefitRows { get; set; } = new();
    public List<GuildBenefitRowVm> AdvancedBenefitRows { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> BasicOptionGroups { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> IntermediateOptionGroups { get; set; } = new();
    public List<GuildBenefitOptionGroupVm> AdvancedOptionGroups { get; set; } = new();
    public List<GuildCityBenefitVm> CityBenefits { get; set; } = new();
    public List<GuildMiracleRowVm> MiracleRows { get; set; } = new();
    public GuildDenominationalMiracleVm? DenominationalMiracle { get; set; }

    public void ApplyDetails(GuildCardDetailsVm? details)
    {
        var source = details ?? new GuildCardDetailsVm();

        PreRequisites = source.PreRequisites ?? string.Empty;
        Restrictions = source.Restrictions ?? string.Empty;
        Ethos = source.Ethos ?? string.Empty;
        Background = source.Background ?? string.Empty;
        LoreSections = source.LoreSections ?? new List<GuildLoreSectionVm>();
        BasicBenefits = source.BasicBenefits ?? new List<string>();
        IntermediateBenefits = source.IntermediateBenefits ?? new List<string>();
        AdvancedBenefits = source.AdvancedBenefits ?? new List<string>();
        BasicBenefitRows = source.BasicBenefitRows ?? new List<GuildBenefitRowVm>();
        IntermediateBenefitRows = source.IntermediateBenefitRows ?? new List<GuildBenefitRowVm>();
        AdvancedBenefitRows = source.AdvancedBenefitRows ?? new List<GuildBenefitRowVm>();
        HasAnyChoiceOptions = BasicBenefitRows.Any(row => row.IsChoiceOption)
            || IntermediateBenefitRows.Any(row => row.IsChoiceOption)
            || AdvancedBenefitRows.Any(row => row.IsChoiceOption);
        BasicOptionGroups = source.BasicOptionGroups ?? new List<GuildBenefitOptionGroupVm>();
        IntermediateOptionGroups = source.IntermediateOptionGroups ?? new List<GuildBenefitOptionGroupVm>();
        AdvancedOptionGroups = source.AdvancedOptionGroups ?? new List<GuildBenefitOptionGroupVm>();
        CityBenefits = source.CityBenefits ?? new List<GuildCityBenefitVm>();
        MiracleRows = source.MiracleRows ?? new List<GuildMiracleRowVm>();
        DenominationalMiracle = source.DenominationalMiracle;

        DetailsLoaded = true;

        Raise(nameof(PreRequisites));
        Raise(nameof(Restrictions));
        Raise(nameof(Ethos));
        Raise(nameof(Background));
        Raise(nameof(LoreSections));
        Raise(nameof(BasicBenefits));
        Raise(nameof(IntermediateBenefits));
        Raise(nameof(AdvancedBenefits));
        Raise(nameof(BasicBenefitRows));
        Raise(nameof(IntermediateBenefitRows));
        Raise(nameof(AdvancedBenefitRows));
        Raise(nameof(BasicOptionGroups));
        Raise(nameof(IntermediateOptionGroups));
        Raise(nameof(AdvancedOptionGroups));
        Raise(nameof(CityBenefits));
        Raise(nameof(MiracleRows));
        Raise(nameof(DenominationalMiracle));
        Raise(nameof(HasLore));
        Raise(nameof(HasRestrictions));
        Raise(nameof(HasBasic));
        Raise(nameof(HasIntermediate));
        Raise(nameof(HasAdvanced));
        Raise(nameof(HasMiracles));
        Raise(nameof(HasDenominationalMiracle));
        Raise(nameof(HasAnyBenefits));
        Raise(nameof(HasCityBenefits));
        Raise(nameof(HasBasicOptions));
        Raise(nameof(HasIntermediateOptions));
        Raise(nameof(HasAdvancedOptions));
        Raise(nameof(AreBenefitOptionsComplete));
        Raise(nameof(HasAnyChoiceOptions));
        Raise(nameof(ChoiceSetNoticeText));
    }

    public bool HasLore => LoreSections.Count > 0;
    public bool HasRestrictions => !string.IsNullOrWhiteSpace(Restrictions);

    public bool HasBasic => BasicBenefitRows.Count > 0;
    public bool HasIntermediate => IntermediateBenefitRows.Count > 0;
    public bool HasAdvanced => AdvancedBenefitRows.Count > 0;
    public bool HasCityBenefits => CityBenefits.Count > 0;
    public bool HasMiracles => MiracleRows.Count > 0;
    public bool HasDenominationalMiracle => DenominationalMiracle != null;

    public bool HasAnyBenefits => HasBasic || HasIntermediate || HasAdvanced;
    public bool HasBasicOptions => BasicBenefitRows.Any(row => row.IsChoiceOption);
    public bool HasIntermediateOptions => IntermediateBenefitRows.Any(row => row.IsChoiceOption);
    public bool HasAdvancedOptions => AdvancedBenefitRows.Any(row => row.IsChoiceOption);
    private bool _hasAnyChoiceOptions;
    public bool HasAnyChoiceOptions
    {
        get => _hasAnyChoiceOptions || HasBasicOptions || HasIntermediateOptions || HasAdvancedOptions;
        set
        {
            if (_hasAnyChoiceOptions == value)
                return;

            _hasAnyChoiceOptions = value;
            Raise();
            Raise(nameof(ChoiceSetNoticeText));
        }
    }

    public string ChoiceSetNoticeText => HasAnyChoiceOptions
        ? "Guild options available"
        : string.Empty;
    public bool AreBenefitOptionsComplete =>
        BasicOptionGroups.All(g => g.HasSelection)
        && IntermediateOptionGroups.All(g => g.HasSelection)
        && AdvancedOptionGroups.All(g => g.HasSelection);

    public double ChevronRotation => IsExpanded ? 180 : 0;
    public double MiracleListChevronRotation => IsMiracleListExpanded ? 180 : 0;
    public double BenefitsChevronRotation => IsBenefitsExpanded ? 180 : 0;
    public double CityBenefitsChevronRotation => IsCityBenefitsExpanded ? 180 : 0;
    public double DenominationalMiracleChevronRotation => IsDenominationalMiracleExpanded ? 180 : 0;

    public ICommand ToggleMiracleListCommand { get; }
    public ICommand ToggleBenefitsCommand { get; }
    public ICommand ToggleCityBenefitsCommand { get; }
    public ICommand ToggleDenominationalMiracleCommand { get; }

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
            Raise(nameof(ShowLogo));
            Raise(nameof(ShowIcon));
        }
    }

    private bool _isMiracleListExpanded;
    public bool IsMiracleListExpanded
    {
        get => _isMiracleListExpanded;
        set
        {
            if (_isMiracleListExpanded == value) return;
            _isMiracleListExpanded = value;
            Raise();
            Raise(nameof(MiracleListChevronRotation));
        }
    }

    private bool _isBenefitsExpanded = true;
    public bool IsBenefitsExpanded
    {
        get => _isBenefitsExpanded;
        set
        {
            if (_isBenefitsExpanded == value) return;
            _isBenefitsExpanded = value;
            Raise();
            Raise(nameof(BenefitsChevronRotation));
        }
    }

    private bool _isCityBenefitsExpanded = true;
    public bool IsCityBenefitsExpanded
    {
        get => _isCityBenefitsExpanded;
        set
        {
            if (_isCityBenefitsExpanded == value) return;
            _isCityBenefitsExpanded = value;
            Raise();
            Raise(nameof(CityBenefitsChevronRotation));
        }
    }

    private bool _isDenominationalMiracleExpanded = true;
    public bool IsDenominationalMiracleExpanded
    {
        get => _isDenominationalMiracleExpanded;
        set
        {
            if (_isDenominationalMiracleExpanded == value) return;
            _isDenominationalMiracleExpanded = value;
            Raise();
            Raise(nameof(DenominationalMiracleChevronRotation));
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
            Raise(nameof(SelectionActionGlyph));
            Raise(nameof(CanToggleSelection));
            Raise(nameof(HasNotSelectableReason));
            Raise(nameof(ShowLogo));
            Raise(nameof(ShowIcon));
        }
    }
}

public sealed class GuildReviewSummaryRowVm
{
    public GuildReviewSummaryRowVm(
        string guildName,
        string guildType,
        bool hasChoices,
        bool hasMissingChoices,
        string statusText)
    {
        GuildName = (guildName ?? string.Empty).Trim();
        GuildType = (guildType ?? string.Empty).Trim();
        HasChoices = hasChoices;
        HasMissingChoices = hasMissingChoices;
        StatusText = (statusText ?? string.Empty).Trim();
    }

    public string GuildName { get; }
    public string GuildType { get; }
    public bool HasChoices { get; }
    public bool HasMissingChoices { get; }
    public string StatusText { get; }
    public bool HasStatusText => StatusText.Length > 0;
    public string RowBackgroundColor => HasMissingChoices ? "#FFF8E1" : "Transparent";
    public string StatusTextColor => HasMissingChoices ? "#D97706" : "#15803D";
}

public sealed class GuildBenefitRowVm
{
    public string DisplayText { get; init; } = string.Empty;
    public List<GuildBenefitDisplayItemVm> DisplayItems { get; init; } = new();
    public List<GuildBenefitChildRowVm> ChildRows { get; init; } = new();
    public List<AbilityDefinition> Abilities { get; init; } = new();
    public bool IsChoiceOption { get; init; }
    public bool IsSelectedChoice { get; init; }
    public string ChoiceLabel { get; init; } = string.Empty;
    public string RowBackgroundColor { get; set; } = "#FFFFFF";

    public bool HasDetails => Abilities.Count > 0;
    public bool HasChoiceLabel => !string.IsNullOrWhiteSpace(ChoiceLabel);
    public bool HasDisplayItems => DisplayItems.Count > 0;
    public bool HasChildRows => ChildRows.Count > 0;
}

public sealed class GuildBenefitChildRowVm
{
    public string Text { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public bool IsLastChild { get; init; }
    public bool ShowLowerBranch => !IsLastChild;
}

public sealed class GuildBenefitDisplayItemVm
{
    public string Text { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
    public bool ShowTrailingDivider { get; set; }
}

public sealed class GuildBenefitOptionGroupVm : INotifyPropertyChanged
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

    private readonly Action<GuildBenefitOptionGroupVm> _onSelectionChanged;
    private bool _suppressNotify;
    private string? _selectedOption;
    private int? _selectedIndex;

    public string SelectionKey { get; }
    public ObservableCollection<string> OptionLabels { get; }
    public IReadOnlyList<GuildBenefitOptionVm> Options { get; }

    public string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0)
                normalized = null;

            if (!Set(ref _selectedOption, normalized))
                return;

            _selectedIndex = ResolveIndex(normalized);
            Raise(nameof(SelectedIndex));
            Raise(nameof(HasSelection));
            Raise(nameof(SelectedLines));

            if (!_suppressNotify)
                _onSelectionChanged(this);
        }
    }

    public int? SelectedIndex => _selectedIndex;

    public bool HasSelection => _selectedIndex.HasValue;

    public IEnumerable<string> SelectedLines
    {
        get
        {
            if (!_selectedIndex.HasValue)
                return Enumerable.Empty<string>();

            var idx = _selectedIndex.Value - 1;
            if (idx < 0 || idx >= Options.Count)
                return Enumerable.Empty<string>();

            return Options[idx].Lines;
        }
    }

    public GuildBenefitOptionGroupVm(
        string selectionKey,
        IReadOnlyList<GuildBenefitOptionVm> options,
        int? initialSelectionIndex,
        Action<GuildBenefitOptionGroupVm> onSelectionChanged)
    {
        SelectionKey = selectionKey ?? string.Empty;
        Options = options ?? new List<GuildBenefitOptionVm>();
        OptionLabels = new ObservableCollection<string>(Options.Select(o => o.Label));
        _onSelectionChanged = onSelectionChanged;

        if (initialSelectionIndex.HasValue)
        {
            var idx = initialSelectionIndex.Value - 1;
            if (idx >= 0 && idx < OptionLabels.Count)
            {
                _suppressNotify = true;
                SelectedOption = OptionLabels[idx];
                _suppressNotify = false;
            }
        }
    }

    private int? ResolveIndex(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        for (var i = 0; i < OptionLabels.Count; i++)
        {
            if (string.Equals(OptionLabels[i], label, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return null;
    }
}

public sealed class GuildBenefitOptionVm
{
    public string Label { get; init; } = string.Empty;
    public List<AbilityDefinition> Abilities { get; init; } = new();
    public List<string> Lines { get; init; } = new();
}

public sealed class GuildCityBenefitVm
{
    public string Name { get; init; } = string.Empty;
    public List<string> Effects { get; init; } = new();
}

public sealed class GuildMiracleRowVm
{
    public string LevelText { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string RowBackgroundColor { get; set; } = "#FFFFFF";
}

public sealed class GuildDenominationalMiracleVm
{
    public string Name { get; init; } = string.Empty;
    public string Cost { get; init; } = string.Empty;
    public string Alignment { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string Range { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Verbal { get; init; } = string.Empty;
    public string Sphere { get; init; } = string.Empty;
    public string Gesture { get; init; } = string.Empty;
    public string IsAdvancedText { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public bool Contains(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return Name.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Cost.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Alignment.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Duration.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Range.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Description.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Verbal.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Sphere.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Gesture.Contains(text, StringComparison.OrdinalIgnoreCase)
               || IsAdvancedText.Contains(text, StringComparison.OrdinalIgnoreCase)
               || Note.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class GuildMiracleDefinition
{
    public int power { get; set; }
    public string name { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string verbal { get; set; } = string.Empty;
    public string range { get; set; } = string.Empty;
    public string duration { get; set; } = string.Empty;
    public string gesture { get; set; } = string.Empty;
    public string level { get; set; } = string.Empty;
    public string sphere { get; set; } = string.Empty;
    public bool isAdvanced { get; set; }
    public string alignment { get; set; } = string.Empty;
}

public sealed class GuildLoreSectionVm : INotifyPropertyChanged
{
    private const int CollapsedLines = 4;

    private bool _isExpanded;

    public GuildLoreSectionVm(string header, string text)
    {
        Header = (header ?? string.Empty).Trim();
        Text = (text ?? string.Empty).Trim();
        ToggleCommand = new Command(ToggleExpanded);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Header { get; }
    public string Text { get; }
    public ICommand ToggleCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxLines)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowToggle)));
        }
    }

    public int MaxLines => IsExpanded ? -1 : CollapsedLines;

    public bool ShowToggle => CanExpand || IsExpanded;
    public string ToggleLabel => IsExpanded ? "... see less" : "... see more";

    private bool CanExpand
    {
        get
        {
            if (Text.Length > 260)
                return true;

            var lineCount = Text.Count(c => c == '\n') + 1;
            return lineCount > CollapsedLines;
        }
    }

    private void ToggleExpanded()
        => IsExpanded = !IsExpanded;
}

public sealed record GuildSelectability(bool Allowed, string Reason);
public sealed record AvailabilityResult(bool Allowed, string Reason)
{
    public static AvailabilityResult Ok() => new(true, "");
}

public sealed class GuildSlotRules
{
    public int PoliticalSlots { get; private set; } = 1;
    public int SocialSlots { get; private set; } = 1;
    public int ProfessionalSlots { get; private set; } = 1;
    public int CitySlots { get; private set; } = 0;

    public bool CityOnly { get; private set; }
    public bool AllGuildsBlocked { get; private set; }

    private readonly Dictionary<string, HashSet<string>> _allowedByType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _forcedByType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _peopleTypeByType = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex TradeCityRegex = new(
        @"trade\s+(political|social|professional)\s+for\s+city\s+(.+)",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));
    public HashSet<string> ForcedCityNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string[]> TypeTokenMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["baronial"] = new[] { "Claws of the Circle" }
    };

    public static GuildSlotRules Default() => new();

    public static GuildSlotRules FromAbilities(IEnumerable<AbilityDraft> abilities)
    {
        var rules = new GuildSlotRules();

        foreach (var ability in abilities ?? Array.Empty<AbilityDraft>())
        {
            var name = (ability?.Name ?? string.Empty).Trim();
            if (name.Length == 0) continue;

            var lower = name.ToLowerInvariant();

            if (ability?.GuildOverrides is { Count: > 0 })
            {
                rules.ApplyGuildOverrides(ability.GuildOverrides);
            }
            else if (ability?.AbilityType == AbilityType.GuildOverride)
            {
                rules.ApplyGuildOverride(name);
            }

            var tradeMatch = TradeCityRegex.Match(name);
            if (tradeMatch.Success)
            {
                var slot = tradeMatch.Groups[1].Value;
                var city = tradeMatch.Groups[2].Value.Trim().TrimEnd('.', '!');

                rules.TradeSlotForCity(slot, city);
                continue;
            }

            if (lower.Contains("city bound", StringComparison.OrdinalIgnoreCase))
            {
                rules.CityOnly = true;
                var city = ExtractCityName(name);
                if (!string.IsNullOrWhiteSpace(city))
                    rules.ForcedCityNames.Add(city);
            }

            if (ContainsAllGuildBan(lower))
            {
                rules.AllGuildsBlocked = true;
                continue;
            }

            if (ContainsTypeBan(lower, "political"))
                rules.PoliticalSlots = 0;
            if (ContainsTypeBan(lower, "social"))
                rules.SocialSlots = 0;
            if (ContainsTypeBan(lower, "professional"))
                rules.ProfessionalSlots = 0;
        }

        rules.Normalize();
        return rules;
    }

    public static GuildSlotRules FromDraft(CharacterDraft draft)
    {
        var rules = FromAbilities(draft.Abilities);
        rules.ApplyOverrides(draft.GuildOverrideRules);
        return rules;
    }

    public void ApplyToCurrentSelection(CharacterDraft draft, Dictionary<string, GuildRecord> records)
    {
        if (AllGuildsBlocked)
        {
            draft.Guilds.Clear();
            return;
        }

        var kept = new List<string>();
        foreach (var existing in draft.Guilds)
        {
            var type = GetTypeForGuild(existing, records);
            var canKeep = CanSelect(type, existing, kept, records);
            if (canKeep.Allowed || ForcedCityNames.Contains(existing, StringComparer.OrdinalIgnoreCase))
                kept.Add(existing);
        }

        foreach (var forcedCity in ForcedCityNames)
        {
            var currentCityCount = CountOfType("city", kept, records);
            if (currentCityCount >= GetLimit("city"))
                break;

            if (records.ContainsKey(forcedCity) && !kept.Contains(forcedCity, StringComparer.OrdinalIgnoreCase))
                kept.Add(forcedCity);
        }

        foreach (var kvp in _forcedByType)
        {
            var type = kvp.Key;
            ApplyForcedByType(type, kvp.Value, kept, records);
        }

        draft.Guilds.Clear();
        foreach (var g in kept.Distinct(StringComparer.OrdinalIgnoreCase))
            draft.Guilds.Add(g);
    }

    public bool ShouldShowGuild(string type, string guildName, IReadOnlyList<string> selected)
    {
        var typeNorm = NormalizeType(type);
        var isSelected = selected.Contains(guildName, StringComparer.OrdinalIgnoreCase);

        if (AllGuildsBlocked)
            return isSelected;

        if (CityOnly && !IsCity(typeNorm))
            return isSelected;

        var limit = GetLimit(typeNorm);
        if (limit <= 0)
            return isSelected;

        if (IsCity(typeNorm) && ForcedCityNames.Count > 0 && !ForcedCityNames.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return isSelected;

        if (_allowedByType.TryGetValue(typeNorm, out var allowed) && allowed.Count > 0)
        {
            if (!allowed.Contains(guildName, StringComparer.OrdinalIgnoreCase))
                return isSelected;
        }

        return true;
    }

    public bool IsGuildLocked(string type, string guildName)
    {
        var typeNorm = NormalizeType(type);
        if (AllGuildsBlocked)
            return false;

        if (IsCity(typeNorm) && ForcedCityNames.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return true;

        if (_forcedByType.TryGetValue(typeNorm, out var forced) && forced.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public GuildSelectability CanSelect(string type, string guildName, IReadOnlyList<string> selected, Dictionary<string, GuildRecord> records)
    {
        var typeNorm = NormalizeType(type);
        if (AllGuildsBlocked)
            return new GuildSelectability(false, "No guild slots available.");

        if (CityOnly && !IsCity(typeNorm))
            return new GuildSelectability(false, "City-bound: only city guilds are allowed.");

        if (_allowedByType.TryGetValue(typeNorm, out var allowed) && allowed.Count > 0
            && !allowed.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return new GuildSelectability(false, $"Only {string.Join(", ", allowed)} allowed.");

        var limit = GetLimit(typeNorm);
        if (limit <= 0)
            return new GuildSelectability(false, $"No {DisplayType(typeNorm)} guild slots available.");

        var isCity = IsCity(typeNorm);

        if (isCity && ForcedCityNames.Count > 0 && !ForcedCityNames.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return new GuildSelectability(false, $"City slot reserved for {string.Join(" or ", ForcedCityNames)}.");

        var currentCount = CountOfType(typeNorm, selected, records);
        var alreadySelected = selected.Contains(guildName, StringComparer.OrdinalIgnoreCase);

        if (!alreadySelected && currentCount >= limit)
            return new GuildSelectability(false, $"All {DisplayType(typeNorm)} guild slots are filled.");

        if (_forcedByType.TryGetValue(typeNorm, out var forced) && forced.Contains(guildName, StringComparer.OrdinalIgnoreCase))
            return new GuildSelectability(true, "");

        return new GuildSelectability(true, "");
    }

    private void TradeSlotForCity(string slot, string cityName)
    {
        var type = NormalizeType(slot);
        DecrementType(type);
        CitySlots++;

        if (!string.IsNullOrWhiteSpace(cityName))
            ForcedCityNames.Add(cityName.Trim());
    }

    public void ForceCity(string cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
            return;

        ForcedCityNames.Add(cityName.Trim());
        CitySlots = Math.Max(1, CitySlots);
        Normalize();
    }

    private void DecrementType(string type)
    {
        if (IsPolitical(type) && PoliticalSlots > 0) PoliticalSlots--;
        else if (IsSocial(type) && SocialSlots > 0) SocialSlots--;
        else if (IsProfessional(type) && ProfessionalSlots > 0) ProfessionalSlots--;
    }

    private void Normalize()
    {
        if (AllGuildsBlocked)
        {
            PoliticalSlots = 0;
            SocialSlots = 0;
            ProfessionalSlots = 0;
            CitySlots = 0;
            ForcedCityNames.Clear();
            CityOnly = false;
            return;
        }

        if (CityOnly)
        {
            CitySlots = 1;
            PoliticalSlots = 0;
            SocialSlots = 0;
            ProfessionalSlots = 0;
        }

        PoliticalSlots = Math.Max(0, PoliticalSlots);
        SocialSlots = Math.Max(0, SocialSlots);
        ProfessionalSlots = Math.Max(0, ProfessionalSlots);
        CitySlots = Math.Max(0, CitySlots);
    }

    private void ApplyGuildOverride(string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0) return;

        if (text.Equals("city bound", StringComparison.OrdinalIgnoreCase))
        {
            CityOnly = true;
            AllGuildsBlocked = false;
            return;
        }

        var parts = text.Split(':', 2);
        if (parts.Length == 0) return;

        var type = NormalizeTypePrefix(parts[0]);
        if (string.IsNullOrWhiteSpace(type))
            return;

        var value = parts.Length > 1 ? (parts[1] ?? string.Empty).Trim() : string.Empty;
        ApplyTypeOverride(type, value);
    }

    private void ApplyTypeOverride(string type, string value)
    {
        var typeNorm = NormalizeType(type);

        if (string.IsNullOrWhiteSpace(value))
        {
            SetLimitForType(typeNorm, 0);
            _allowedByType[typeNorm] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var lower = value.Trim();
        if (lower.StartsWith("type-", StringComparison.OrdinalIgnoreCase))
        {
            var token = lower.Substring(5).Trim();
            var allowed = ResolveTokenGuilds(token);
            if (allowed.Count > 0)
            {
                _allowedByType[typeNorm] = allowed;
                EnsureSlotsForType(typeNorm, Math.Max(1, allowed.Count));
            }
            else if (!string.IsNullOrWhiteSpace(token))
            {
                _peopleTypeByType[typeNorm] = token;
            }
            return;
        }

        var forced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { value };
        _forcedByType[typeNorm] = forced;
        _allowedByType[typeNorm] = forced;
        EnsureSlotsForType(typeNorm, forced.Count);
    }

    private void ApplyOverrides(GuildOverrideRules? overrides)
    {
        if (overrides == null)
            return;

        if (overrides.IsCityBound)
        {
            CityOnly = true;
            AllGuildsBlocked = false;
        }

        ApplyChannelOverride("political", overrides.Political);
        ApplyChannelOverride("professional", overrides.Professional);
        ApplyChannelOverride("social", overrides.Social);

        Normalize();
    }

    private void ApplyChannelOverride(string type, GuildOverrideChannel? channel)
    {
        if (channel == null) return;

        var typeNorm = NormalizeType(type);

        if (channel.CanJoin == false)
        {
            SetLimitForType(typeNorm, 0);
            return;
        }

        if (channel.ReplacedBy is { Count: > 0 })
        {
            var set = new HashSet<string>(channel.ReplacedBy.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            _forcedByType[typeNorm] = set;
            _allowedByType[typeNorm] = set;
            EnsureSlotsForType(typeNorm, Math.Max(1, set.Count));
            return;
        }

        if (!string.IsNullOrWhiteSpace(channel.GuildPeople))
        {
            _peopleTypeByType[typeNorm] = channel.GuildPeople.Trim();
            return;
        }
    }

    public void ResolvePeopleTypeOverrides(Dictionary<string, GuildRecord> records)
    {
        if (records == null || records.Count == 0 || _peopleTypeByType.Count == 0)
            return;

        foreach (var kvp in _peopleTypeByType)
        {
            var typeNorm = NormalizeType(kvp.Key);
            var peopleType = kvp.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(peopleType))
                continue;

            var normalizedToken = NormalizePeopleTypeToken(peopleType);
            var allowed = CollectAllowedGuildsByPeopleType(records, typeNorm, normalizedToken);

            if (allowed.Count == 0)
            {
                SetLimitForType(typeNorm, 0);
                _allowedByType[typeNorm] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _forcedByType.Remove(typeNorm);
                continue;
            }

            _allowedByType[typeNorm] = allowed;
            _forcedByType.Remove(typeNorm);
            EnsureSlotsForType(typeNorm, Math.Max(1, allowed.Count));
        }
    }

    private void ApplyGuildOverrides(IEnumerable<string> overrides)
    {
        foreach (var entry in overrides ?? Array.Empty<string>())
            ApplyGuildOverride(entry);
    }

    private void ApplyForcedByType(
        string type,
        IEnumerable<string> forcedByType,
        IList<string> kept,
        Dictionary<string, GuildRecord> records)
    {
        var limit = GetLimit(type);
        foreach (var forced in forcedByType ?? Array.Empty<string>())
        {
            if (CountOfType(type, kept, records) >= limit)
                break;

            if (records.ContainsKey(forced) && !kept.Contains(forced, StringComparer.OrdinalIgnoreCase))
                kept.Add(forced);
        }
    }

    private static HashSet<string> CollectAllowedGuildsByPeopleType(
        Dictionary<string, GuildRecord> records,
        string typeNorm,
        string normalizedToken)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rec in records)
        {
            var guild = rec.Value;
            if (guild == null)
                continue;

            var guildType = NormalizeType(guild.Type ?? string.Empty);
            if (!string.Equals(guildType, typeNorm, StringComparison.OrdinalIgnoreCase))
                continue;

            var availability = guild.Availability ?? new GuildAvailability();
            if (RuleTreeEvaluator.ContainsPositiveInValue(
                    availability.Rules,
                    field: "PeopleType",
                    value: normalizedToken,
                    normalizeField: NormalizePeopleTypeToken,
                    normalizeValue: NormalizePeopleTypeToken))
            {
                allowed.Add(rec.Key);
            }
        }

        return allowed;
    }

    private static string NormalizePeopleTypeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static string NormalizeTypePrefix(string prefix)
    {
        var p = NormalizeType(prefix);
        return p switch
        {
            "po" or "political" => "political",
            "pr" or "professional" => "professional",
            "so" or "social" => "social",
            _ => string.Empty
        };
    }

    private HashSet<string> ResolveTokenGuilds(string token)
    {
        if (TypeTokenMap.TryGetValue(token.Trim(), out var names))
            return new HashSet<string>(names ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private void SetLimitForType(string type, int value)
    {
        if (IsPolitical(type)) PoliticalSlots = value;
        else if (IsSocial(type)) SocialSlots = value;
        else if (IsProfessional(type)) ProfessionalSlots = value;
    }

    private void EnsureSlotsForType(string type, int minimum)
    {
        if (IsPolitical(type)) PoliticalSlots = Math.Max(PoliticalSlots, minimum);
        else if (IsSocial(type)) SocialSlots = Math.Max(SocialSlots, minimum);
        else if (IsProfessional(type)) ProfessionalSlots = Math.Max(ProfessionalSlots, minimum);
    }

    private static bool ContainsAllGuildBan(string lower)
    {
        return lower.Contains("cannot join any guild")
               || lower.Contains("can not join any guild")
               || lower.Contains("may not join any guild")
               || lower.Contains("no guilds");
    }

    private static bool ContainsTypeBan(string lower, string type)
    {
        var key = type.ToLowerInvariant();
        return lower.Contains($"cannot join {key} guild")
               || lower.Contains($"can not join {key} guild")
               || lower.Contains($"may not join {key} guild")
               || lower.Contains($"no {key} guild");
    }

    private static string ExtractCityName(string abilityName)
    {
        if (string.IsNullOrWhiteSpace(abilityName))
            return string.Empty;

        var open = abilityName.IndexOf('(');
        var close = abilityName.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            var city = abilityName.Substring(open + 1, close - open - 1).Trim();
            if (!string.IsNullOrWhiteSpace(city))
                return city;
        }

        return string.Empty;
    }

    private static int CountOfType(string type, IEnumerable<string> selected, Dictionary<string, GuildRecord> records)
    {
        var typeNorm = NormalizeType(type);

        var count = 0;
        foreach (var g in selected)
        {
            var t = NormalizeType(GetTypeForGuild(g, records));
            if (t == typeNorm)
                count++;
        }

        return count;
    }

    private int GetLimit(string type)
    {
        var typeNorm = NormalizeType(type);
        if (IsPolitical(typeNorm)) return PoliticalSlots;
        if (IsSocial(typeNorm)) return SocialSlots;
        if (IsProfessional(typeNorm)) return ProfessionalSlots;
        if (IsCity(typeNorm)) return CitySlots;
        return 0;
    }

    private static string GetTypeForGuild(string guildName, Dictionary<string, GuildRecord> records)
    {
        if (records.TryGetValue(guildName, out var rec) && rec != null)
            return rec.Type ?? string.Empty;

        return string.Empty;
    }

    private static string NormalizeType(string type)
        => (type ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsCity(string type) => string.Equals(type, "city", StringComparison.OrdinalIgnoreCase);
    private static bool IsPolitical(string type) => string.Equals(type, "political", StringComparison.OrdinalIgnoreCase);
    private static bool IsSocial(string type) => string.Equals(type, "social", StringComparison.OrdinalIgnoreCase);
    private static bool IsProfessional(string type) => string.Equals(type, "professional", StringComparison.OrdinalIgnoreCase);

    private static string DisplayType(string type)
    {
        var t = NormalizeType(type);
        return t switch
        {
            "city" => "city",
            "political" => "political",
            "social" => "social",
            "professional" => "professional",
            _ => "guild"
        };
    }
}
