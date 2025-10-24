using labyItems.Services;

namespace labyItems.Pages
{

    public partial class MakeAbility : ContentPage
    {
        public record Result(string Name, int Cost, int Table, string Description);
        public class Row
        {
            public string Name { get; init; } = string.Empty;
            public string Cost { get; init; } = string.Empty;
            public string Table { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public Result AsResult { get; init; } = new(string.Empty, 0, 0, string.Empty);
        }
        private readonly List<Row> _rows = new();
        private TaskCompletionSource<Result?>? _tcs;

        public MakeAbility()
        {
            InitializeComponent();
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await Load("");
        }

        private async Task Load(string q)
        {
            var list = await ManuAbilityService.SearchAsync(q);
            _rows.Clear();
            _rows.AddRange(list.Select(e => new Row
            {
                Name = e.name,
                Table = $"Table: {e.table}",
                Cost = $"Cost: {e.cost}",
                Description = e.description,
                AsResult = new Result(e.name, e.cost, e.table, e.description)
            }));
            Results.ItemsSource = null;
            Results.ItemsSource = _rows;
        }

        private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
        {
            await Load(e.NewTextValue ?? "");
        }

        private async void OnPick(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is Row row)
            {
                _tcs?.TrySetResult(row.AsResult);
                ((CollectionView)sender!).SelectedItem = null;
                await Navigation.PopAsync();
            }
        }

        public async Task<Result?> PickAsync(INavigation nav)
        {
            _tcs = new TaskCompletionSource<Result?>();
            await nav.PushAsync(this);
            return await _tcs.Task;
        }
    }
}