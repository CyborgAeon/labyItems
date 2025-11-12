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
public LifeConfigPage() : this(null) { }
        public LifeConfigPage(Dictionary<string, int>? items = null)
        {
            InitializeComponent();

            // Default mapping (your keys/values)
            var data = items ?? new Dictionary<string, int>
            {
                {"3/1",  4},
                {"6/2",  9},
                {"9/3",  14},
                {"12/4", 20},
                {"15/5", 28},
                {"18/6", 38},
                {"21/7", 50},
                {"24/8", 65},
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