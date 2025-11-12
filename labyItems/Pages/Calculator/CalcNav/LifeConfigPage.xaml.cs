using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using labyItems.Models;

namespace labyItems.Pages.Calculator.CalcNav;
public partial class LifeConfigPage : ContentPage
    {
        // Parent (IspCalculator) listens to this just like other tabs
        public event Action<CalcContribution>? ContributionAdded;

        public LifeConfigPage(Dictionary<string, int>? items = null)
        {
            InitializeComponent();

            // Default mapping (your keys/values)
            var data = items ?? new Dictionary<string, int>
            {
                new("3/1",  4),
                new("6/2",  9),
                new("9/3",  14),
                new("12/4", 20),
                new("15/5", 28),
                new("18/6", 38),
                new("21/7", 50),
                new("24/8", 65),
            };

            LifeSlider.ItemsSource = data;
        }

        private void OnReturnToCalculator(object sender, EventArgs e)
        {
            var key   = LifeSlider.SelectedKey;   // e.g. "12/4"
            var value = LifeSlider.SelectedValue; // e.g. 20

            // EXACT string you requested (note trailing \n):
            var summary = $"+{key} Item-Life -> {value} ISP\n";

            // Tell the parent
            ContributionAdded?.Invoke(new CalcContribution(
                Source: "Life",
                Label:  summary,
                Isp:    value
            ));

            // (No Navigation.PopAsync here — this page is the tab)
        }
    }