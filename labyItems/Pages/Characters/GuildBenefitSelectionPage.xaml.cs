using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Pages.Characters.ViewModels;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public partial class GuildBenefitSelectionPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _doneTcs = new();
    private readonly GuildBenefitChoiceSummaryRow _row;

    public GuildBenefitSelectionPage(GuildBenefitChoiceSummaryRow row)
    {
        InitializeComponent();
        _row = row ?? throw new ArgumentNullException(nameof(row));
        BindingContext = new GuildBenefitSelectionPageVm(_row);
    }

    public Task<bool> Result => _doneTcs.Task;

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        _doneTcs.TrySetResult(true);
        await Navigation.PopModalAsync().ConfigureAwait(false);
    }

    private sealed class GuildBenefitSelectionPageVm : INotifyPropertyChanged
    {
        private readonly GuildBenefitChoiceSummaryRow _row;

        public GuildBenefitSelectionPageVm(GuildBenefitChoiceSummaryRow row)
        {
            _row = row;
            Options = new ObservableCollection<GuildBenefitSelectionOptionVm>(row.Options.Select((option, index) =>
            {
                var vm = new GuildBenefitSelectionOptionVm(option.Label, option.Lines, index, SelectOption);
                vm.IsSelected = _row.SelectedIndex.HasValue && _row.SelectedIndex.Value - 1 == index;
                return vm;
            }));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Header => _row.DisplayText;
        public IReadOnlyList<GuildBenefitSelectionOptionVm> Options { get; }

        private void SelectOption(int index)
        {
            _row.ApplySelection(index + 1);
            foreach (var option in Options)
                option.IsSelected = option.Index == index;
        }
    }

    private sealed class GuildBenefitSelectionOptionVm : INotifyPropertyChanged
    {
        private bool _isSelected;

        public GuildBenefitSelectionOptionVm(string label, IReadOnlyList<string> lines, int index, Action<int> select)
        {
            Label = label;
            Lines = lines;
            Index = index;
            SelectCommand = new Command(() => select(index));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Label { get; }
        public IReadOnlyList<string> Lines { get; }
        public int Index { get; }
        public ICommand SelectCommand { get; }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
}

public sealed class GuildBenefitChoiceSummaryRow
{
    private readonly Action<int?> _applySelection;

    public GuildBenefitChoiceSummaryRow(
        string displayText,
        string selectionKey,
        IReadOnlyList<GuildBenefitChoiceOption> options,
        int? selectedIndex,
        Action<int?> applySelection)
    {
        DisplayText = string.IsNullOrWhiteSpace(displayText)
            ? "Choose one guild benefit"
            : displayText.Trim();
        SelectionKey = selectionKey?.Trim() ?? string.Empty;
        Options = options ?? Array.Empty<GuildBenefitChoiceOption>();
        SelectedIndex = selectedIndex;
        _applySelection = applySelection ?? throw new ArgumentNullException(nameof(applySelection));
    }

    public string DisplayText { get; }
    public string SelectionKey { get; }
    public IReadOnlyList<GuildBenefitChoiceOption> Options { get; }
    public int? SelectedIndex { get; private set; }

    public void ApplySelection(int selectedIndex)
    {
        SelectedIndex = selectedIndex;
        _applySelection(selectedIndex);
    }
}

public sealed class GuildBenefitChoiceOption
{
    public GuildBenefitChoiceOption(string label, IReadOnlyList<string> lines)
    {
        Label = label ?? string.Empty;
        Lines = lines ?? Array.Empty<string>();
    }

    public string Label { get; }
    public IReadOnlyList<string> Lines { get; }
}
