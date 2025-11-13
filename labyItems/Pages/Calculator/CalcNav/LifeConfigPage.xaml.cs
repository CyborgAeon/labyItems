using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using labyItems.Models;

namespace labyItems.Pages.Calculator.CalcNav;
public partial class LifeConfigPage : ContentPage
    {
        public event Action<CalcContribution>? ContributionAdded;
        public static readonly BindableProperty TotalProperty =
        BindableProperty.Create(
            nameof(Total),
            typeof(int),
            typeof(LifeConfigPage),
            0);

    public int Total
    {
        get => (int)GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }
        public LifeConfigPage()
        {
            InitializeComponent();
            ComputeTotal();
            // Default mapping (your keys/values)
            var data = new Dictionary<string, int>
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

            var summary = $"+{key} Item-Life -> {value} ISP\n";

            ContributionAdded?.Invoke(new CalcContribution(
                Source: "Life",
                Label:  summary,
                Isp:    value
            ));
        }
        private int ComputeTotal()
        {
            return LifeSlider.SelectedValue;
        }
    }