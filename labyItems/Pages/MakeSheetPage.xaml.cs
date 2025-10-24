using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using labyItems.Models;

namespace labyItems.Pages
{
    public partial class MakeSheetPage : ContentPage
    {
        private readonly TaskCompletionSource<MakeSheet> _tcs = new();

        public MakeSheetViewModel VM { get; } = new MakeSheetViewModel();
        public MakeSheetPage(Character character)
        {
            InitializeComponent();

            PlayerNameHeader.Text = character.PlayerName;
            CharacterNameHeader.Text = character.Name;
            CharacterClassHeader.Text = character.Class;

            BindingContext = VM;
        }

    }

    public class MakeSheetViewModel : BindableObject
    {
        public MakeSheet Sheet { get; set; } = new();

        public ICommand AddBonusCommand => new Command(() =>
        {
            Sheet.Bonuses.Add(new BonusEntry { Percent = 0, Reason = string.Empty });
            OnPropertyChanged(nameof(Sheet));
        });

        public ICommand RemoveBonusCommand => new Command<BonusEntry>((b) =>
        {
            if (b != null) Sheet.Bonuses.Remove(b);
            OnPropertyChanged(nameof(Sheet));
        });

        public ICommand AddSpecificCommand => new Command(() =>
        {
            Sheet.SpecificModifiers.Add(new SpecificModifier { AppliesTo = string.Empty, PercentBonus = 0, Reason = string.Empty });
            OnPropertyChanged(nameof(Sheet));
        });

        public ICommand RecalcCommand => new Command(() =>
        {
            Sheet.FinalNormalChancePercent = Sheet.ComputeNormalFinalChance();
            OnPropertyChanged(nameof(Sheet));
        });

        public ICommand SaveJsonCommand => new Command(async () =>
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
            var json = JsonSerializer.Serialize(Sheet, options);
            var path = Path.Combine(FileSystem.AppDataDirectory, "make-sheet.json");
            File.WriteAllText(path, json);
            await Application.Current.MainPage.DisplayAlert("Saved", $"Saved to {path}", "OK");
        });

        public ICommand LoadJsonCommand => new Command(async () =>
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "make-sheet.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
                Sheet = JsonSerializer.Deserialize<MakeSheet>(json, options) ?? new();
                OnPropertyChanged(nameof(Sheet));
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Not found", "No saved sheet yet.", "OK");
            }
        });

        public ICommand ClearCommand => new Command(() =>
        {
            Sheet = new MakeSheet();
            OnPropertyChanged(nameof(Sheet));
        });
    }

}
