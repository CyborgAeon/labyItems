using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace labyItems.Controls
{
    public partial class StickyFooterControl : ContentView
    {
        public StickyFooterControl()
        {
            InitializeComponent();
            UpdateFormattedTotal();
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
            BindableProperty.Create(nameof(BreakdownItems), typeof(IEnumerable<ContributionRow>), typeof(StickyFooterControl), Enumerable.Empty<ContributionRow>());
        public IEnumerable<ContributionRow> BreakdownItems { get => (IEnumerable<ContributionRow>)GetValue(BreakdownItemsProperty); set => SetValue(BreakdownItemsProperty, value); }

        public static readonly BindableProperty RemoveContributionCommandProperty =
            BindableProperty.Create(nameof(RemoveContributionCommand), typeof(ICommand), typeof(StickyFooterControl), null);
        public ICommand? RemoveContributionCommand { get => (ICommand?)GetValue(RemoveContributionCommandProperty); set => SetValue(RemoveContributionCommandProperty, value); }

        public static readonly BindableProperty IsExpandedProperty =
            BindableProperty.Create(nameof(IsExpanded), typeof(bool), typeof(StickyFooterControl), false);
        public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

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
            IsExpanded = !IsExpanded;
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
