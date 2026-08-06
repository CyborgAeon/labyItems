using System.Collections.ObjectModel;
using labyItems.Infrastructure;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages
{

    public partial class MakeAbility : ContentPage
    {
        private const string DefaultSourceBookFilter = "Manufacturers Guide";

        public record Result(
            string Name,
            int Cost,
            int Table,
            string Description,
            string Availability,
            IReadOnlyList<RuleClause> AvailabilityRules,
            bool IsAvailable,
            bool CanBuyMultiple,
            IReadOnlyList<string> PreReqs,
            IReadOnlyList<string> ChoiceSetRefs,
            string AbilityRef,
            string SourceBook)
        {
            public EvolutionService.AbilityResult ToAbilityResult()
                => new()
                {
                    Index = Name,
                    Description = Description,
                    Cost = Cost,
                    Table = Table,
                    AbilityRef = AbilityRef,
                    Available = Availability,
                    AvailabilityRules = AvailabilityRules ?? Array.Empty<RuleClause>(),
                    CanBuyMultiple = CanBuyMultiple,
                    PreReqs = PreReqs ?? Array.Empty<string>(),
                    ChoiceSetRefs = ChoiceSetRefs ?? Array.Empty<string>(),
                    SourceBook = SourceBook
                };
        }

        public sealed class Row : ObservableObject
        {
            private bool _isSelected;

            public Row(Result result, string key, bool isSelected)
            {
                AsResult = result;
                Key = key;
                _isSelected = isSelected;
            }

            public string Key { get; }
            public Result AsResult { get; }
            public string Name => AsResult.Name;
            public int Cost => AsResult.Cost;
            public int Table => AsResult.Table;
            public string Description => AsResult.Description;
            public string SourceBook => AsResult.SourceBook;
            public bool IsAvailable => AsResult.IsAvailable;
            public string MetaText => IsAvailable
                ? $"Cost: {Cost} - Table: {Table} - {SourceBook}"
                : $"Cost: {Cost} - Table: {Table} - {SourceBook} - Unavailable";

            public bool IsSelected
            {
                get => _isSelected;
                set => SetProperty(ref _isSelected, value);
            }
        }

        private readonly Dictionary<string, Result> _selectedByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly CharacterDraft _draft;
        private readonly IAbilityAvailabilityService _availabilityService;
        private readonly bool _draftingMode;
        private IReadOnlyDictionary<string, CharacterClassRecord> _classes =
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, PeopleRecord> _races =
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);
        private TaskCompletionSource<IReadOnlyList<Result>>? _multiPickTcs;
        private bool _isClosing;
        private bool _loaded;
        private bool _availabilityContextReady;
        private bool _showAvailableOnly = true;
        private string? _selectedSourceBookFilter = DefaultSourceBookFilter;
        private string? _selectedBracketFilter;
        private bool _suppressSourceBookFilterReload;
        private bool _suppressBracketFilterReload;

        public MakeAbility(
            CharacterDraft? draft = null,
            IAbilityAvailabilityService? availabilityService = null,
            bool draftingMode = false)
        {
            _draft = draft ?? new CharacterDraft();
            _availabilityService = availabilityService
                ?? ServiceHelper.ResolveService<IAbilityAvailabilityService>()
                ?? new AbilityAvailabilityService();
            _draftingMode = draftingMode;
            if (_draftingMode)
                _showAvailableOnly = false;

            InitializeComponent();
            BindingContext = this;
            Rows.CollectionChanged += (_, _) => RaiseStateProperties();
            SourceBookFilterOptions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowSourceBookFilters));
            BracketFilterOptions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowBracketFilters));
        }

        public ObservableCollection<Row> Rows { get; } = new();
        public ObservableCollection<string> SourceBookFilterOptions { get; } = new();
        public ObservableCollection<string> BracketFilterOptions { get; } = new();

        public bool HasRows => Rows.Count > 0;
        public bool HasSelectedRows => _selectedByKey.Count > 0;
        public string SelectionSummaryText => $"Selected: {_selectedByKey.Count}";
        public bool ShowAvailabilityFilter => !_draftingMode;
        public bool ShowSourceBookFilters => SourceBookFilterOptions.Count > 0;
        public bool ShowBracketFilters => _draftingMode && BracketFilterOptions.Count > 0;
        public int SearchColumn => ShowAvailabilityFilter ? 2 : 0;
        public int SearchColumnSpan => ShowAvailabilityFilter ? 1 : 3;

        public string? SelectedSourceBookFilter
        {
            get => _selectedSourceBookFilter;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (string.Equals(_selectedSourceBookFilter, normalized, StringComparison.OrdinalIgnoreCase))
                    return;

                _selectedSourceBookFilter = normalized;
                OnPropertyChanged(nameof(SelectedSourceBookFilter));

                if (!_suppressSourceBookFilterReload)
                    _ = LoadAsync((Search?.Text ?? string.Empty).Trim());
            }
        }

        public string? SelectedBracketFilter
        {
            get => _selectedBracketFilter;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (string.Equals(_selectedBracketFilter, normalized, StringComparison.OrdinalIgnoreCase))
                    return;

                _selectedBracketFilter = normalized;
                OnPropertyChanged(nameof(SelectedBracketFilter));

                if (!_suppressBracketFilterReload)
                    _ = LoadAsync((Search?.Text ?? string.Empty).Trim());
            }
        }

        public bool ShowAvailableOnly
        {
            get => _showAvailableOnly;
            set
            {
                if (_draftingMode)
                    value = false;

                if (_showAvailableOnly == value)
                    return;

                _showAvailableOnly = value;
                RaiseStateProperties();
            }
        }

        public string AvailabilityToggleText => ShowAvailableOnly ? "Available" : "Show All";
        public string ConfirmButtonText => HasSelectedRows
            ? $"Add Selected ({_selectedByKey.Count})"
            : "Add Selected";

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_loaded)
                return;

            _loaded = true;
            try
            {
                await EnsureAvailabilityContextAsync();
                await LoadAsync(string.Empty);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Ability Search", $"Failed to load abilities: {ex.Message}", "OK");
            }
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CompleteAndCloseAsync(Array.Empty<Result>());
            return true;
        }

        public async Task<Result?> PickAsync(INavigation nav)
        {
            var picked = await PickManyAsync(nav);
            return picked.FirstOrDefault();
        }

        public async Task<IReadOnlyList<Result>> PickManyAsync(INavigation nav)
        {
            _multiPickTcs = new TaskCompletionSource<IReadOnlyList<Result>>(TaskCreationOptions.RunContinuationsAsynchronously);
            await nav.PushAsync(this);
            return await _multiPickTcs.Task;
        }

        private async Task LoadAsync(string query)
        {
            var catalog = await ManuAbilityService.GetMergedCatalogAsync();
            RebuildSourceBookFilterOptions(catalog);

            var list = string.IsNullOrWhiteSpace(query)
                ? catalog
                : await ManuAbilityService.SearchMergedCatalogAsync(query);

            var selectedSourceBook = (SelectedSourceBookFilter ?? string.Empty).Trim();
            var sourceFiltered = selectedSourceBook.Length == 0
                ? list
                : list
                    .Where(entry => string.Equals(
                        NormalizeSourceBook(entry.sourceBook),
                        selectedSourceBook,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var results = sourceFiltered
                .Select(entry => new
                {
                    Entry = entry,
                    IsAvailable = _draftingMode || _availabilityService.IsAvailable(
                        entry.availabilityRules,
                        _draft,
                        _classes,
                        _races)
                })
                .Where(item => _draftingMode || !ShowAvailableOnly || item.IsAvailable)
                .Select(item => new
                {
                    item.Entry,
                    item.IsAvailable
                })
                .ToList();

            if (_draftingMode)
            {
                RebuildBracketFilterOptions(results.Select(item => item.Entry));

                var selectedBracket = (SelectedBracketFilter ?? string.Empty).Trim();
                if (selectedBracket.Length > 0)
                {
                    results = results
                        .Where(item => EntryMatchesBracketFilter(item.Entry, selectedBracket))
                        .ToList();
                }
            }

            var orderedResults = results
                .OrderBy(item => item.Entry.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Rows.Clear();
            foreach (var item in orderedResults)
            {
                var entry = item.Entry;
                var result = new Result(
                    entry.name,
                    entry.cost,
                    entry.table,
                    entry.description,
                    entry.availability,
                    entry.availabilityRules,
                    item.IsAvailable,
                    entry.canBuyMultiple,
                    entry.preReqs,
                    entry.choiceSetRefs,
                    entry.abilityRef,
                    NormalizeSourceBook(entry.sourceBook));
                var key = BuildAbilityKey(entry);
                var isSelected = _selectedByKey.ContainsKey(key);
                if (isSelected)
                    _selectedByKey[key] = result;

                Rows.Add(new Row(result, key, isSelected));
            }

            RaiseStateProperties();
        }

        private async Task EnsureAvailabilityContextAsync()
        {
            if (_availabilityContextReady)
                return;

            try
            {
                var classesTask = ClassService.GetAllAsync();
                var racesTask = PeopleService.GetAllAsync();
                await Task.WhenAll(classesTask, racesTask);
                _classes = classesTask.Result;
                _races = racesTask.Result;
            }
            catch
            {
                _classes = new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
                _races = new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);
            }

            _availabilityContextReady = true;
        }

        private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
            => await LoadAsync((e.NewTextValue ?? string.Empty).Trim());

        private async void OnAvailabilityToggleChanged(object? sender, CheckedChangedEventArgs e)
        {
            ShowAvailableOnly = e.Value;
            await LoadAsync((Search?.Text ?? string.Empty).Trim());
        }

        private void OnResultTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is not Row row)
                return;

            row.IsSelected = !row.IsSelected;
            SyncSelection(row);
        }

        private void OnRowCheckedChanged(object? sender, CheckedChangedEventArgs e)
        {
            if (sender is not CheckBox checkBox
                || checkBox.BindingContext is not Row row)
            {
                return;
            }

            SyncSelection(row);
        }

        private async void OnInfoClicked(object? sender, EventArgs e)
        {
            if (sender is not Button button
                || button.CommandParameter is not Row row)
            {
                return;
            }

            await Navigation.PushAsync(new AbilityCardPage(row.AsResult.ToAbilityResult()));
        }

        private async void OnCancelClicked(object? sender, EventArgs e)
            => await CompleteAndCloseAsync(Array.Empty<Result>());

        private async void OnConfirmClicked(object? sender, EventArgs e)
        {
            var selected = _selectedByKey.Values
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            await CompleteAndCloseAsync(selected);
        }

        private void SyncSelection(Row row)
        {
            if (row.IsSelected)
                _selectedByKey[row.Key] = row.AsResult;
            else
                _selectedByKey.Remove(row.Key);

            RaiseStateProperties();
        }

        private async Task CompleteAndCloseAsync(IReadOnlyList<Result> picked)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            try
            {
                _multiPickTcs?.TrySetResult(picked);

                if (Navigation.NavigationStack.LastOrDefault() == this)
                {
                    await Navigation.PopAsync();
                    return;
                }

                if (Navigation.ModalStack.LastOrDefault() == this)
                {
                    await Navigation.PopModalAsync();
                    return;
                }

                if (Shell.Current != null)
                    await Shell.Current.GoToAsync("..");
            }
            finally
            {
                _isClosing = false;
            }
        }

        private void RaiseStateProperties()
        {
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(HasSelectedRows));
            OnPropertyChanged(nameof(SelectionSummaryText));
            OnPropertyChanged(nameof(ConfirmButtonText));
            OnPropertyChanged(nameof(ShowAvailableOnly));
            OnPropertyChanged(nameof(AvailabilityToggleText));
            OnPropertyChanged(nameof(ShowAvailabilityFilter));
            OnPropertyChanged(nameof(ShowSourceBookFilters));
            OnPropertyChanged(nameof(ShowBracketFilters));
            OnPropertyChanged(nameof(SearchColumn));
            OnPropertyChanged(nameof(SearchColumnSpan));
        }

        private void RebuildSourceBookFilterOptions(IEnumerable<ManuAbilityService.ManuAbilityEntry> entries)
        {
            var filters = entries
                .Select(entry => NormalizeSourceBook(entry.sourceBook))
                .Where(sourceBook => sourceBook.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(sourceBook => sourceBook, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var current = SourceBookFilterOptions.ToList();
            if (!current.SequenceEqual(filters, StringComparer.OrdinalIgnoreCase))
            {
                SourceBookFilterOptions.Clear();
                foreach (var filter in filters)
                    SourceBookFilterOptions.Add(filter);
            }

            if (!string.IsNullOrWhiteSpace(SelectedSourceBookFilter)
                && !filters.Any(filter => string.Equals(filter, SelectedSourceBookFilter, StringComparison.OrdinalIgnoreCase)))
            {
                _suppressSourceBookFilterReload = true;
                try
                {
                    SelectedSourceBookFilter = null;
                }
                finally
                {
                    _suppressSourceBookFilterReload = false;
                }
            }

            OnPropertyChanged(nameof(ShowSourceBookFilters));
        }

        private void RebuildBracketFilterOptions(IEnumerable<ManuAbilityService.ManuAbilityEntry> entries)
        {
            var filters = entries
                .SelectMany(ExtractBracketRuleValues)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => NormalizeFilterToken(value), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var current = BracketFilterOptions.ToList();
            if (!current.SequenceEqual(filters, StringComparer.OrdinalIgnoreCase))
            {
                BracketFilterOptions.Clear();
                foreach (var filter in filters)
                    BracketFilterOptions.Add(filter);
            }

            if (!string.IsNullOrWhiteSpace(SelectedBracketFilter)
                && !filters.Any(filter => string.Equals(filter, SelectedBracketFilter, StringComparison.OrdinalIgnoreCase)))
            {
                _suppressBracketFilterReload = true;
                try
                {
                    SelectedBracketFilter = null;
                }
                finally
                {
                    _suppressBracketFilterReload = false;
                }
            }

            OnPropertyChanged(nameof(ShowBracketFilters));
        }

        private static bool EntryMatchesBracketFilter(ManuAbilityService.ManuAbilityEntry entry, string selectedBracket)
        {
            var selectedToken = NormalizeFilterToken(selectedBracket);
            if (selectedToken.Length == 0)
                return true;

            return ExtractBracketRuleValues(entry)
                .Any(value =>
                {
                    var token = NormalizeFilterToken(value);
                    return token.Equals(selectedToken, StringComparison.OrdinalIgnoreCase)
                           || IsAllBracketToken(token);
                });
        }

        private static IEnumerable<string> ExtractBracketRuleValues(ManuAbilityService.ManuAbilityEntry entry)
            => (entry.availabilityRules ?? Array.Empty<RuleClause>())
                .Where(rule => rule != null
                               && rule.IsValid
                               && rule.Field.Equals("Bracket", StringComparison.OrdinalIgnoreCase))
                .SelectMany(rule => rule.Value ?? new List<string>())
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => value.Length > 0);

        private static bool IsAllBracketToken(string token)
            => token.Equals("all", StringComparison.OrdinalIgnoreCase)
               || token.Equals("any", StringComparison.OrdinalIgnoreCase);

        private static string NormalizeFilterToken(string? value)
            => new((value ?? string.Empty)
                .Trim()
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

        private static string NormalizeSourceBook(string? sourceBook)
        {
            var value = (sourceBook ?? string.Empty).Trim();
            return value.Length == 0 ? "Unknown" : value;
        }

        private static string BuildAbilityKey(ManuAbilityService.ManuAbilityEntry entry)
        {
            var abilityRef = NormalizeToken(entry.abilityRef);
            if (abilityRef.Length > 0)
                return $"ref:{abilityRef}";

            return $"{Math.Max(0, entry.table)}|{NormalizeToken(entry.name)}";
        }

        private static string NormalizeToken(string? value)
            => new string((value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)
                .ToArray());
    }
}
