using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace labyItems.Controls
{
    public partial class StickyFooterControl : ContentView
    {
        private INotifyCollectionChanged? _breakdownCollectionChangedSource;
        public ObservableCollection<ContributionRow> VisibleBreakdownItems { get; } = new();

        public StickyFooterControl()
        {
            InitializeComponent();
            UpdateFormattedTotal();
            RefreshVisibleBreakdownItems();
        }

        // Total (int)
        public static readonly BindableProperty TotalProperty =
            BindableProperty.Create(
                nameof(Total), typeof(int), typeof(StickyFooterControl), 0,
                propertyChanged: (b, o, n) => ((StickyFooterControl)b).UpdateFormattedTotal());
        public int Total { get => (int)GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

        // TotalFormat (string), e.g. "Total ISP: {0}"
        public static readonly BindableProperty TotalFormatProperty =
            BindableProperty.Create(
                nameof(TotalFormat), typeof(string), typeof(StickyFooterControl), "Total ISP: {0}",
                propertyChanged: (b, o, n) => ((StickyFooterControl)b).UpdateFormattedTotal());
        public string TotalFormat { get => (string)GetValue(TotalFormatProperty); set => SetValue(TotalFormatProperty, value); }

        // Computed text that the Label binds to
        public static readonly BindableProperty FormattedTotalProperty =
            BindableProperty.Create(
                nameof(FormattedTotal), typeof(string), typeof(StickyFooterControl), "Total ISP: 0");
        public string FormattedTotal { get => (string)GetValue(FormattedTotalProperty); private set => SetValue(FormattedTotalProperty, value); }

        public static readonly BindableProperty BreakdownItemsProperty =
            BindableProperty.Create(
                nameof(BreakdownItems),
                typeof(IEnumerable<ContributionRow>),
                typeof(StickyFooterControl),
                Enumerable.Empty<ContributionRow>(),
                propertyChanged: OnBreakdownItemsChanged);
        public IEnumerable<ContributionRow> BreakdownItems { get => (IEnumerable<ContributionRow>)GetValue(BreakdownItemsProperty); set => SetValue(BreakdownItemsProperty, value); }

        public static readonly BindableProperty RemoveContributionCommandProperty =
            BindableProperty.Create(nameof(RemoveContributionCommand), typeof(ICommand), typeof(StickyFooterControl), null);
        public ICommand? RemoveContributionCommand { get => (ICommand?)GetValue(RemoveContributionCommandProperty); set => SetValue(RemoveContributionCommandProperty, value); }

        public static readonly BindableProperty IsExpandedProperty =
            BindableProperty.Create(
                nameof(IsExpanded),
                typeof(bool),
                typeof(StickyFooterControl),
                false,
                propertyChanged: (bindable, _, _) =>
                {
                    if (bindable is StickyFooterControl control)
                        control.OnPropertyChanged(nameof(IsBreakdownEmptyStateVisible));
                });
        public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

        public static readonly BindableProperty EmptyBreakdownTextProperty =
            BindableProperty.Create(
                nameof(EmptyBreakdownText),
                typeof(string),
                typeof(StickyFooterControl),
                StickyFooterContentBuilder.DefaultEmptyMessage,
                propertyChanged: (bindable, _, _) =>
                {
                    if (bindable is StickyFooterControl control)
                        control.OnPropertyChanged(nameof(ResolvedEmptyBreakdownText));
                });
        public string EmptyBreakdownText { get => (string)GetValue(EmptyBreakdownTextProperty); set => SetValue(EmptyBreakdownTextProperty, value); }

        public string ResolvedEmptyBreakdownText =>
            string.IsNullOrWhiteSpace(EmptyBreakdownText)
                ? StickyFooterContentBuilder.DefaultEmptyMessage
                : EmptyBreakdownText.Trim();

        private bool _hasVisibleBreakdownItems;
        public bool HasVisibleBreakdownItems
        {
            get => _hasVisibleBreakdownItems;
            private set
            {
                if (_hasVisibleBreakdownItems == value)
                    return;

                _hasVisibleBreakdownItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBreakdownEmptyStateVisible));
            }
        }

        public bool IsBreakdownEmptyStateVisible => IsExpanded && !HasVisibleBreakdownItems;

        void UpdateFormattedTotal()
        {
            try
            {
                var fmt = string.IsNullOrWhiteSpace(TotalFormat) ? "Total ISP: {0}" : TotalFormat;
                FormattedTotal = string.Format(fmt, Total);
            }
            catch
            {
                // fall back if format string is invalid
                FormattedTotal = $"Total: {Total}";
            }
        }

        // Button text
        public static readonly BindableProperty ButtonTextProperty =
            BindableProperty.Create(nameof(ButtonText), typeof(string), typeof(StickyFooterControl), "Return");
        public string ButtonText { get => (string)GetValue(ButtonTextProperty); set => SetValue(ButtonTextProperty, value); }

        public static readonly BindableProperty IsReturnEnabledProperty =
            BindableProperty.Create(nameof(IsReturnEnabled), typeof(bool), typeof(StickyFooterControl), true);
        public bool IsReturnEnabled { get => (bool)GetValue(IsReturnEnabledProperty); set => SetValue(IsReturnEnabledProperty, value); }

        public static readonly BindableProperty ReturnCommandProperty =
            BindableProperty.Create(nameof(ReturnCommand), typeof(ICommand), typeof(StickyFooterControl), null);
        public ICommand? ReturnCommand { get => (ICommand?)GetValue(ReturnCommandProperty); set => SetValue(ReturnCommandProperty, value); }

        public static readonly BindableProperty ReturnCommandParameterProperty =
            BindableProperty.Create(nameof(ReturnCommandParameter), typeof(object), typeof(StickyFooterControl), null);
        public object? ReturnCommandParameter { get => GetValue(ReturnCommandParameterProperty); set => SetValue(ReturnCommandParameterProperty, value); }

        // Optional Shell fallback route
        public static readonly BindableProperty ShellFallbackRouteProperty =
            BindableProperty.Create(nameof(ShellFallbackRoute), typeof(string), typeof(StickyFooterControl), "..");
        public string ShellFallbackRoute { get => (string)GetValue(ShellFallbackRouteProperty); set => SetValue(ShellFallbackRouteProperty, value); }

        private async void OnReturnClicked(object sender, EventArgs e)
        {
            if (ReturnCommand?.CanExecute(ReturnCommandParameter) == true)
            {
                ReturnCommand.Execute(ReturnCommandParameter);
                return;
            }
            
            var page = FindParentPage();
            if (page is null) return;
            await DefaultNavigateAsync(page, ShellFallbackRoute);
        }

        private void OnToggleExpanded(object sender, EventArgs e)
        {
            DictionaryOverlayRegistry.DismissAll();
            IsExpanded = !IsExpanded;
        }

        private static void OnBreakdownItemsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is not StickyFooterControl control)
                return;

            control.DetachBreakdownCollectionChanged(oldValue);
            control.AttachBreakdownCollectionChanged(newValue);
            control.RefreshVisibleBreakdownItems();
        }

        private void AttachBreakdownCollectionChanged(object source)
        {
            if (source is not INotifyCollectionChanged collection)
                return;

            _breakdownCollectionChangedSource = collection;
            _breakdownCollectionChangedSource.CollectionChanged += OnBreakdownCollectionChanged;
        }

        private void DetachBreakdownCollectionChanged(object source)
        {
            if (_breakdownCollectionChangedSource != null)
            {
                _breakdownCollectionChangedSource.CollectionChanged -= OnBreakdownCollectionChanged;
                _breakdownCollectionChangedSource = null;
            }
        }

        private void OnBreakdownCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => RefreshVisibleBreakdownItems();

        private void RefreshVisibleBreakdownItems()
        {
            var content = StickyFooterContentBuilder.Build(BreakdownItems, EmptyBreakdownText);

            VisibleBreakdownItems.Clear();
            foreach (var row in content.Rows)
                VisibleBreakdownItems.Add(row);

            HasVisibleBreakdownItems = content.HasRows;
        }


        public static async Task DefaultNavigateAsync(Page page, string shellFallbackRoute = "..")
        {
            if (page?.Navigation?.NavigationStack?.Count > 1)
            {
                await page.Navigation.PopAsync();
                return;
            }
            if (Shell.Current is not null && !string.IsNullOrWhiteSpace(shellFallbackRoute))
            {
                await Shell.Current.GoToAsync(shellFallbackRoute);
            }
        }

        private Page? FindParentPage()
        {
            Element? e = this;
            while (e is not null && e is not Page) e = e.Parent;
            return e as Page;
        }
    }
}
