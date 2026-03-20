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
            string AbilityRef)
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
                    ChoiceSetRefs = ChoiceSetRefs ?? Array.Empty<string>()
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
            public bool IsAvailable => AsResult.IsAvailable;
            public string MetaText => IsAvailable
                ? $"Cost: {Cost} · Table: {Table}"
                : $"Cost: {Cost} · Table: {Table} · Unavailable";

            public bool IsSelected
            {
                get => _isSelected;
                set => SetProperty(ref _isSelected, value);
            }
        }

        private readonly Dictionary<string, Result> _selectedByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly CharacterDraft _draft;
        private readonly IAbilityAvailabilityService _availabilityService;
        private IReadOnlyDictionary<string, CharacterClassRecord> _classes =
            new Dictionary<string, CharacterClassRecord>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, PeopleRecord> _races =
            new Dictionary<string, PeopleRecord>(StringComparer.OrdinalIgnoreCase);
        private TaskCompletionSource<IReadOnlyList<Result>>? _multiPickTcs;
        private bool _isClosing;
        private bool _loaded;
        private bool _availabilityContextReady;
        private bool _showAvailableOnly = true;

        public MakeAbility(
            CharacterDraft? draft = null,
            IAbilityAvailabilityService? availabilityService = null)
        {
            _draft = draft ?? new CharacterDraft();
            _availabilityService = availabilityService
                ?? ServiceHelper.ResolveService<IAbilityAvailabilityService>()
                ?? new AbilityAvailabilityService();
            InitializeComponent();
            BindingContext = this;
            Rows.CollectionChanged += (_, _) => RaiseStateProperties();
        }

        public ObservableCollection<Row> Rows { get; } = new();

        public bool HasRows => Rows.Count > 0;
        public bool HasSelectedRows => _selectedByKey.Count > 0;
        public string SelectionSummaryText => $"Selected: {_selectedByKey.Count}";
        public bool ShowAvailableOnly
        {
            get => _showAvailableOnly;
            set
            {
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
            var list = await ManuAbilityService.SearchAsync(query);
            var results = list
                .Select(entry => new
                {
                    Entry = entry,
                    IsAvailable = _availabilityService.IsAvailable(
                        entry.availabilityRules,
                        _draft,
                        _classes,
                        _races)
                })
                .Where(item => !ShowAvailableOnly || item.IsAvailable)
                .Select(item => new
                {
                    item.Entry,
                    item.IsAvailable
                })
                .OrderBy(item => item.Entry.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Rows.Clear();
            foreach (var item in results)
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
                    entry.abilityRef);
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
