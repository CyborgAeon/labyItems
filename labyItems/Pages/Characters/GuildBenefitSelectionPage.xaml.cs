using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using labyItems.Helpers;
using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public partial class GuildBenefitSelectionPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _doneTcs = new();

    public GuildBenefitSelectionPage(GuildBenefitChoiceSummaryRow row)
        : this(new[] { row }, row?.GuildName)
    {
    }

    public GuildBenefitSelectionPage(
        IReadOnlyList<GuildBenefitChoiceSummaryRow> rows,
        string? guildName = null)
    {
        InitializeComponent();

        var normalizedRows = (rows ?? Array.Empty<GuildBenefitChoiceSummaryRow>())
            .Where(row => row != null)
            .ToList();
        if (normalizedRows.Count == 0)
            throw new ArgumentException("At least one guild choice row is required.", nameof(rows));

        var resolvedGuildName = (guildName ?? normalizedRows[0].GuildName ?? string.Empty).Trim();
        BindingContext = new GuildBenefitSelectionPageVm(normalizedRows, resolvedGuildName);
    }

    public Task<bool> Result => _doneTcs.Task;

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        _doneTcs.TrySetResult(true);
        await Navigation.PopModalAsync().ConfigureAwait(false);
    }

    private sealed class GuildBenefitSelectionPageVm
    {
        public GuildBenefitSelectionPageVm(
            IReadOnlyList<GuildBenefitChoiceSummaryRow> rows,
            string guildName)
        {
            Header = guildName.Length == 0
                ? "Set guild benefit choices"
                : guildName;

            Sections = BuildSections(rows);
        }

        public string Header { get; }
        public IReadOnlyList<GuildBenefitTierSectionVm> Sections { get; }

        private static IReadOnlyList<GuildBenefitTierSectionVm> BuildSections(
            IReadOnlyList<GuildBenefitChoiceSummaryRow> rows)
        {
            var orderedRows = rows
                .Where(row => row != null)
                .OrderBy(row => ResolveTierSortKey(row.Tier))
                .ThenBy(row => row.OptionNumber ?? int.MaxValue)
                .ThenBy(row => row.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return orderedRows
                .GroupBy(row => NormalizeTier(row.Tier), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var tierTitle = ToTierTitle(group.Key);
                    var tierAvailableNow = group.All(row => row.IsTierAvailableNow);
                    var rowsForTier = group
                        .Select(row => new GuildBenefitChoiceRowVm(row))
                        .ToList();
                    return new GuildBenefitTierSectionVm(
                        tierTitle: tierTitle,
                        availabilityText: tierAvailableNow
                            ? "Available now."
                            : "Locked by points right now. You can still set this early.",
                        isExpanded: tierAvailableNow,
                        choiceRows: rowsForTier);
                })
                .ToList();
        }

        private static int ResolveTierSortKey(string? tier)
            => NormalizeTier(tier) switch
            {
                "basic" => 0,
                "intermediate" => 1,
                "advanced" => 2,
                _ => 3
            };

        private static string NormalizeTier(string? tier)
            => (tier ?? string.Empty).Trim().ToLowerInvariant();

        private static string ToTierTitle(string normalizedTier)
            => normalizedTier switch
            {
                "basic" => "Basic",
                "intermediate" => "Intermediate",
                "advanced" => "Advanced",
                _ => "Other"
            };
    }

    private sealed class GuildBenefitTierSectionVm : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public GuildBenefitTierSectionVm(
            string tierTitle,
            string availabilityText,
            bool isExpanded,
            IReadOnlyList<GuildBenefitChoiceRowVm> choiceRows)
        {
            TierTitle = tierTitle;
            AvailabilityText = availabilityText;
            _isExpanded = isExpanded;
            ChoiceRows = new ObservableCollection<GuildBenefitChoiceRowVm>(choiceRows ?? Array.Empty<GuildBenefitChoiceRowVm>());
            ToggleExpandedCommand = new Command(() => IsExpanded = !IsExpanded);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string TierTitle { get; }
        public string AvailabilityText { get; }
        public IReadOnlyList<GuildBenefitChoiceRowVm> ChoiceRows { get; }
        public ICommand ToggleExpandedCommand { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (!Set(ref _isExpanded, value))
                    return;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChevronText)));
            }
        }

        public string ChevronText => FontAwesomeGlyphs.Chevron;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    private sealed class GuildBenefitChoiceRowVm : INotifyPropertyChanged
    {
        private readonly GuildBenefitChoiceSummaryRow _row;

        public GuildBenefitChoiceRowVm(GuildBenefitChoiceSummaryRow row)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
            RowTitle = BuildRowTitle(_row);
            Options = new ObservableCollection<GuildBenefitSelectionOptionVm>(
                _row.Options.Select((option, index) =>
                {
                    var vm = new GuildBenefitSelectionOptionVm(option.Label, option.Lines, index, SelectOption);
                    vm.IsSelected = _row.SelectedIndex.HasValue && _row.SelectedIndex.Value - 1 == index;
                    return vm;
                }));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string RowTitle { get; }
        public IReadOnlyList<GuildBenefitSelectionOptionVm> Options { get; }
        public bool HasSelection =>
            _row.SelectedIndex.HasValue
            && _row.SelectedIndex.Value > 0
            && _row.SelectedIndex.Value <= Options.Count;

        private void SelectOption(int index)
        {
            _row.ApplySelection(index + 1);
            foreach (var option in Options)
                option.IsSelected = option.Index == index;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSelection)));
        }

        private static string BuildRowTitle(GuildBenefitChoiceSummaryRow row)
        {
            if (row.OptionNumber.HasValue && row.OptionNumber.Value > 0)
                return $"Choice {row.OptionNumber.Value}";

            var text = (row.DisplayText ?? string.Empty).Trim();
            if (text.Length == 0)
                return "Choice";

            var marker = text.LastIndexOf("choice ", StringComparison.OrdinalIgnoreCase);
            return marker >= 0
                ? text[marker..].Trim()
                : text;
        }
    }

    private sealed class GuildBenefitSelectionOptionVm : INotifyPropertyChanged
    {
        private bool _isSelected;

        public GuildBenefitSelectionOptionVm(
            string label,
            IReadOnlyList<string> lines,
            int index,
            Action<int> select)
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
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectButtonText)));
            }
        }

        public string SelectButtonText => IsSelected ? "Selected" : "Select";
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
        Action<int?> applySelection,
        string? guildName = null,
        string? tier = null,
        int? optionNumber = null,
        bool isTierAvailableNow = true)
    {
        DisplayText = string.IsNullOrWhiteSpace(displayText)
            ? "Choose one guild benefit"
            : displayText.Trim();
        SelectionKey = selectionKey?.Trim() ?? string.Empty;
        Options = options ?? Array.Empty<GuildBenefitChoiceOption>();
        SelectedIndex = selectedIndex;
        GuildName = (guildName ?? string.Empty).Trim();
        Tier = (tier ?? string.Empty).Trim();
        OptionNumber = optionNumber;
        IsTierAvailableNow = isTierAvailableNow;
        _applySelection = applySelection ?? throw new ArgumentNullException(nameof(applySelection));
    }

    public string DisplayText { get; }
    public string SelectionKey { get; }
    public IReadOnlyList<GuildBenefitChoiceOption> Options { get; }
    public int? SelectedIndex { get; private set; }
    public string GuildName { get; }
    public string Tier { get; }
    public int? OptionNumber { get; }
    public bool IsTierAvailableNow { get; }

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
