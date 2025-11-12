using System.Text.RegularExpressions;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using labyItems.Models;
using labyItems.Pages.Configs;

namespace labyItems.Pages
{
    public partial class MakeSheetPage : ContentPage
    {
        private readonly TaskCompletionSource<MakeSheet?> _tcs = new();
        public Task<MakeSheet?> Completion => _tcs.Task;
        public MakeSheetPage(Character character)
        {
            InitializeComponent();

            PlayerNameHeader.Text = character.PlayerName;
            CharacterNameHeader.Text = character.Name;
            CharacterClassHeader.Text = character.Class;

            // BindingContext = new ManuAbilityConfig();
        }
       public ObservableCollection<string> AbilityDescriptions { get; } = new();

        private async void OnSearchAbility(object sender, EventArgs e)
        {
            var picked = await new MakeAbility().PickAsync(Navigation);
            if (picked == null) return;

            var delta = ExtractFirstPercent(picked.Description ?? string.Empty);
            int.TryParse(FinalNormalChancePercent.Text, out int curr);
            FinalNormalChancePercent.Text = (curr + delta).ToString();

            var name = picked.Name ?? "Ability";
            var bullet = delta != 0 ? $"• {name} (+{delta}%)" : $"• {name}";
            AbilityDescriptions.Add(bullet);
        }

        private static int ExtractFirstPercent(string s)
        {
            var m = Regex.Match(s, @"([+-]?\d+)\s*%");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) return n;
            // fallback: capture plain integer if you sometimes store “+2” without %
            m = Regex.Match(s, @"^[\s\p{P}]*([+-]?\d+)\b");
            return m.Success && int.TryParse(m.Groups[1].Value, out n) ? n : 0;
        }

        private async void OnRecalcClicked(object sender, EventArgs e)
        {
            Completion.Result.FinalNormalChancePercent = Completion.Result.ComputeNormalFinalChance();
            OnPropertyChanged(nameof(Completion.Result));
        }

        private bool TryParsePercent(string input, out int value)
        {
            input = input.Trim().Replace("%", string.Empty);
            if (input.StartsWith("+")) input = input.Substring(1);
            return int.TryParse(input, out value);
        }
        // private string BuildSummary(MakeAbility cfg)
        // {
        //     totalBonus
        // }
    }
}
            // var query = await _page.DisplayPromptAsync("Search ability", "Type a keyword (by index)", "Search", "Cancel", "e.g. Rebirth");
            // if (string.IsNullOrWhiteSpace(query)) return;

            // // 2) Query dictionary (by index, any table)
            // var hits = await GeneralTableService.SearchByIndexAsync(query);
            // if (hits.Count == 0)
            // {
            //     await _page.DisplayAlert("No results", $"No abilities found for '{query}'.", "OK");
            //     return;
            // }

            // // 3) Let user pick a hit (ActionSheet shows titles)
            // var options = hits.Select(h => $"Table {h.Table}: {h.Index}").ToList();
            // var pickedText = await _page.DisplayActionSheet("Pick ability", "Cancel", null, options.ToArray());
            // if (string.IsNullOrWhiteSpace(pickedText) || pickedText == "Cancel") return;

            // var picked = hits[options.IndexOf(pickedText)];

            // // 4) Ask for modifier (e.g., 2 or +2%)
            // var modText = await _page.DisplayPromptAsync("Modifier", "Enter modifier percent (e.g., 2 or +2%)", "OK", "Cancel", keyboard: Keyboard.Numeric);
            // if (string.IsNullOrWhiteSpace(modText)) return;

            // if (!TryParsePercent(modText, out var modPercent))
            // {
            //     await _page.DisplayAlert("Invalid value", "Please enter a number like 2 or +2%", "OK");
            //     return;
            // }

            // // 5) Apply modifier to final chance
            // Sheet.FinalNormalChancePercent += modPercent;

            // // 6) Append bullet line: "• Name (+2%)"
            // var bullet = $"• {picked.Index} (+{modPercent}%)";
            // Sheet.AbilityDescriptions.Add(bullet);

            // // Optionally: include table/cost in description
            // // Sheet.AbilityDescriptions.Add($"   (Table {picked.Table}, Cost {picked.Cost})");

            // // Notify bindings
            // OnPropertyChanged(nameof(Sheet));
        // }