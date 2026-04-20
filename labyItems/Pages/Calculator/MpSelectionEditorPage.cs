using labyItems.Controls;

namespace labyItems.Pages.Calculator;

public sealed class MpSelectionEditorPage<TOption> : ContentPage
    where TOption : struct
{
    private readonly TOption _initialOption;
    private readonly Action<TOption, int> _applyChanges;
    private readonly DictionarySearchBar<TOption> _searchBar;
    private readonly MpSelectionEditorVm _vm;
    private bool _optionsLoaded;
    private readonly Func<Task<Dictionary<string, TOption>>>? _loadOptionsAsync;

    public MpSelectionEditorPage(
        string title,
        TOption initialOption,
        int initialUses,
        string placeholderText,
        string selectionDisplayMemberPath,
        Func<TOption, string> displayNameFactory,
        Func<Task<Dictionary<string, TOption>>>? loadOptionsAsync = null,
        Func<string, Task<Dictionary<string, TOption>>>? remoteSearchProvider = null,
        Action<TOption, int>? applyChanges = null)
    {
        _initialOption = initialOption;
        _applyChanges = applyChanges ?? ((_, _) => { });
        _loadOptionsAsync = loadOptionsAsync;
        _vm = new MpSelectionEditorVm(
            displayNameFactory(initialOption),
            Math.Max(1, initialUses),
            "Change the selected record and adjust the number of uses.");

        Title = title;

        _searchBar = new DictionarySearchBar<TOption>
        {
            PlaceholderText = placeholderText,
            SelectionDisplayMemberPath = selectionDisplayMemberPath,
            SelectedValue = initialOption,
            ClearAfterResultSelection = false,
            KeepFocusOnResultSelection = false,
            KeyboardAvoidanceEnabled = true,
            RemoteSearchProvider = remoteSearchProvider
        };

        var usesControl = new PlusMinusControl
        {
            AllowMultiple = true,
            Min = 1,
            LabelText = "Uses"
        };
        usesControl.SetBinding(PlusMinusControl.CountProperty, nameof(MpSelectionEditorVm.Uses), mode: BindingMode.TwoWay);

        var summaryLabel = new Label
        {
            FontSize = 12,
            Opacity = 0.75
        };
        summaryLabel.SetBinding(Label.TextProperty, nameof(MpSelectionEditorVm.Summary));

        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Clicked += async (_, _) => await CloseAsync();

        var saveButton = new Button { Text = "Save" };
        saveButton.Clicked += async (_, _) =>
        {
            var selected = _searchBar.GetValue(DictionarySearchBar<TOption>.SelectedValueProperty);
            var option = selected is TOption typed ? typed : _initialOption;
            _applyChanges(option, _vm.Uses);
            await CloseAsync();
        };

        var contentGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = _vm.DisplayName,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = 18
                    },
                    summaryLabel,
                    _searchBar,
                    usesControl
                }
            }
        };

        var footer = new Grid
        {
            Padding = 16,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };

        footer.Children.Add(cancelButton);
        footer.Children.Add(saveButton);
        Grid.SetColumn(cancelButton, 0);
        Grid.SetColumn(saveButton, 1);

        contentGrid.Children.Add(scroll);
        contentGrid.Children.Add(footer);
        Grid.SetRow(footer, 1);

        Content = contentGrid;

        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_optionsLoaded || _loadOptionsAsync == null)
            return;

        _optionsLoaded = true;
        _searchBar.ItemsSource = await _loadOptionsAsync();
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Count > 0)
        {
            await Navigation.PopModalAsync();
            return;
        }

        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }

    private sealed class MpSelectionEditorVm : BindableObject
    {
        private int _uses;
        private string _summary;

        public MpSelectionEditorVm(string displayName, int uses, string summary)
        {
            DisplayName = displayName;
            _uses = uses;
            _summary = summary;
        }

        public string DisplayName { get; }

        public int Uses
        {
            get => _uses;
            set
            {
                var sanitized = Math.Max(1, value);
                if (_uses == sanitized)
                    return;

                _uses = sanitized;
                OnPropertyChanged();
            }
        }

        public string Summary
        {
            get => _summary;
            set
            {
                if (_summary == value)
                    return;

                _summary = value;
                OnPropertyChanged();
            }
        }
    }
}
