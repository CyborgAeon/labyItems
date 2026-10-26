using System.Collections;
using System.Collections.Specialized;

namespace labyItems.Controls.Specialisations;

public sealed class SpecialisationGridView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(SpecialisationGridView),
        propertyChanged: OnItemsChanged);

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(string), typeof(SpecialisationGridView), string.Empty,
        BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    private readonly Grid _grid = new() { ColumnSpacing = 8, RowSpacing = 6 };
    private INotifyCollectionChanged? _observableItems;

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string SelectedItem { get => (string?)GetValue(SelectedItemProperty) ?? string.Empty; set => SetValue(SelectedItemProperty, value); }

    public SpecialisationGridView()
    {
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Content = _grid;
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (SpecialisationGridView)bindable;
        if (view._observableItems != null)
            view._observableItems.CollectionChanged -= view.OnCollectionChanged;
        view._observableItems = newValue as INotifyCollectionChanged;
        if (view._observableItems != null)
            view._observableItems.CollectionChanged += view.OnCollectionChanged;
        view.Rebuild();
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
        => ((SpecialisationGridView)bindable).RefreshSelection();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        var values = ItemsSource?.Cast<object>().Select(x => x?.ToString() ?? string.Empty)
            .Where(x => x.Length > 0).ToList() ?? new List<string>();
        for (var index = 0; index < values.Count; index++)
        {
            var row = index / 2;
            if (_grid.RowDefinitions.Count <= row)
                _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var choice = new SpecialisationChoiceView { Text = values[index] };
            choice.SelectionRequested += OnSelectionRequested;
            Grid.SetRow(choice, row);
            Grid.SetColumn(choice, index % 2);
            if (index == values.Count - 1 && values.Count % 2 == 1)
                Grid.SetColumnSpan(choice, 2);
            _grid.Children.Add(choice);
        }
        RefreshSelection();
    }

    private void OnSelectionRequested(object? sender, EventArgs e)
    {
        if (sender is SpecialisationChoiceView choice)
            SelectedItem = choice.IsSelected ? string.Empty : choice.Text;
    }

    private void RefreshSelection()
    {
        foreach (var choice in _grid.Children.OfType<SpecialisationChoiceView>())
            choice.IsSelected = string.Equals(choice.Text, SelectedItem, StringComparison.OrdinalIgnoreCase);
    }
}
