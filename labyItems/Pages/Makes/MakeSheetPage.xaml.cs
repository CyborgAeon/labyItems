using System.Text.RegularExpressions;
using labyItems.Models;
using labyItems.Pages.Makes;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;
using EvocationCardPage = labyItems.Pages.EvocationCard.EvocationCard;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using SpellCardPage = labyItems.Pages.SpellCard.SpellCard;

namespace labyItems.Pages;

public partial class MakeSheetPage : ContentPage
{
    private readonly MakeSheetViewModel _vm;

    public MakeSheetPage()
    {
        InitializeComponent();
        _vm = new MakeSheetViewModel();
        Title = _vm.PageTitle;
        BindingContext = _vm;
    }

    public MakeSheetPage(Character character)
    {
        InitializeComponent();
        _vm = new MakeSheetViewModel(character);
        Title = _vm.PageTitle;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _vm.EnsureLoadedAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Make Sheet", $"Failed to load make data: {ex.Message}", "OK");
        }
    }

    private void OnBuildTabClicked(object? sender, EventArgs e)
        => _vm.ActivateBuildTab();

    private void OnConsequencesTabClicked(object? sender, EventArgs e)
        => _vm.ActivateConsequencesTab();

    private void OnCriticalTabClicked(object? sender, EventArgs e)
        => _vm.ActivateCriticalTab();

    private async void OnAddEffectClicked(object? sender, EventArgs e)
    {
        var error = _vm.AddEffect();
        if (!string.IsNullOrWhiteSpace(error))
            await DisplayAlert("Invalid Effect", error, "OK");
    }

    private void OnRemoveEffectClicked(object? sender, EventArgs e)
    {
        var row = ResolveEffectRow(sender);
        _vm.RemoveEffect(row);
    }

    private void OnEditEffectClicked(object? sender, EventArgs e)
    {
        var row = ResolveEffectRow(sender);
        _vm.EditEffect(row);
    }

    private async void OnEffectInfoClicked(object? sender, EventArgs e)
    {
        var row = ResolveEffectRow(sender);
        if (row == null || row.SourceType.Length == 0)
            return;

        if (row.SourceType.Equals("Spell", StringComparison.OrdinalIgnoreCase))
        {
            var spell = await _vm.FindSpellByNameAsync(row.Name);
            if (spell != null)
                await Navigation.PushAsync(new SpellCardPage(spell));
            return;
        }

        if (row.SourceType.Equals("Miracle", StringComparison.OrdinalIgnoreCase))
        {
            var miracle = await _vm.FindMiracleByNameAsync(row.Name);
            if (miracle != null)
                await Navigation.PushAsync(new MiracleCardPage(miracle));
            return;
        }

        if (row.SourceType.Equals("Evocation", StringComparison.OrdinalIgnoreCase))
        {
            var evocation = await _vm.FindEvocationByNameAsync(row.Name);
            if (evocation != null)
                await Navigation.PushAsync(new EvocationCardPage(evocation));
        }
    }

    private async void OnAddManualBonusClicked(object? sender, EventArgs e)
    {
        var error = _vm.AddManualBonus();
        if (!string.IsNullOrWhiteSpace(error))
            await DisplayAlert("Invalid Bonus", error, "OK");
    }

    private void OnRemoveManualBonusClicked(object? sender, EventArgs e)
    {
        var row = ResolveManualBonusRow(sender);
        _vm.RemoveManualBonus(row);
    }

    private async void OnSearchAbilityClicked(object? sender, EventArgs e)
    {
        try
        {
            _vm.SelectedBonusMode = "Manual";
            var picked = await new MakeAbility(_vm.Draft, draftingMode: _vm.IsDraftingMode)
                .PickManyAsync(Navigation);
            if (picked.Count == 0)
                return;

            foreach (var ability in picked)
            {
                _vm.AddManualBonusFromAbility(
                    ability.Name,
                    ability.Description,
                    ability.ToAbilityResult(),
                    ability.ChoiceSetRefs);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ability Search", $"Failed to apply ability: {ex.Message}", "OK");
        }
    }

    private async void OnEditManualBonusClicked(object? sender, EventArgs e)
    {
        var row = ResolveManualBonusRow(sender);
        if (row == null)
            return;

        try
        {
            var editors = (await _vm.GetManualBonusChoiceSetEditorsAsync(row))
                .Where(editor => editor.HasOptions)
                .ToList();
            if (editors.Count == 0)
                return;

            var selectedEditor = await PickChoiceSetEditorAsync(editors);
            if (selectedEditor == null)
                return;

            var selectedOption = await PickChoiceSetOptionAsync(selectedEditor);
            if (selectedOption == null)
                return;

            _vm.SetManualBonusSpecialisation(
                row,
                selectedEditor.ChoiceSetRef,
                selectedEditor.Title,
                selectedOption.Key,
                selectedOption.Label);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Specialisation", $"Failed to update specialisation: {ex.Message}", "OK");
        }
    }

    private async void OnManualBonusInfoClicked(object? sender, EventArgs e)
    {
        var row = ResolveManualBonusRow(sender);
        if (row?.Ability == null)
            return;

        await Navigation.PushAsync(new AbilityCardPage(row.Ability));
    }

    private async void OnExportMakeSheetClicked(object? sender, EventArgs e)
    {
        var exportText = _vm.BuildExportText();

        try
        {
            var safeCharacter = Regex.Replace(
                string.IsNullOrWhiteSpace(_vm.CharacterName) ? "character" : _vm.CharacterName,
                @"[^A-Za-z0-9_-]+",
                "-");
            var filename = $"{safeCharacter}-make-sheet-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
            var path = Path.Combine(FileSystem.CacheDirectory, filename);
            await File.WriteAllTextAsync(path, exportText);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Make Sheet",
                File = new ShareFile(path)
            });
        }
        catch
        {
            try
            {
                await Clipboard.Default.SetTextAsync(exportText);
                await DisplayAlert(
                    "Export Make Sheet",
                    "Could not open share sheet. The make sheet export was copied to your clipboard.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Make Sheet", $"Export failed: {ex.Message}", "OK");
            }
        }
    }

    private static MakeBonusRowVm? ResolveManualBonusRow(object? sender)
        => (sender as Button)?.CommandParameter as MakeBonusRowVm
           ?? (sender as BindableObject)?.BindingContext as MakeBonusRowVm;

    private static MakeEffectRowVm? ResolveEffectRow(object? sender)
        => (sender as Button)?.CommandParameter as MakeEffectRowVm
           ?? (sender as BindableObject)?.BindingContext as MakeEffectRowVm;

    private async Task<MakeBonusChoiceSetEditorVm?> PickChoiceSetEditorAsync(
        IReadOnlyList<MakeBonusChoiceSetEditorVm> editors)
    {
        if (editors.Count == 0)
            return null;

        if (editors.Count == 1)
            return editors[0];

        var labels = editors
            .Select(editor => editor.Title)
            .ToArray();
        var pickedTitle = await DisplayActionSheet(
            "Select Specialisation",
            "Cancel",
            null,
            labels);
        if (string.IsNullOrWhiteSpace(pickedTitle)
            || string.Equals(pickedTitle, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return editors.FirstOrDefault(editor =>
            string.Equals(editor.Title, pickedTitle, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<MakeBonusChoiceSetOptionVm?> PickChoiceSetOptionAsync(MakeBonusChoiceSetEditorVm editor)
    {
        if (editor.Options.Count == 0)
            return null;

        var entries = editor.Options
            .Select(option => new
            {
                Option = option,
                Display = string.Equals(option.Key, editor.SelectedOptionKey, StringComparison.OrdinalIgnoreCase)
                    ? $"{option.Label} (Current)"
                    : option.Label
            })
            .ToList();

        var picked = await DisplayActionSheet(
            editor.Title,
            "Cancel",
            null,
            entries.Select(entry => entry.Display).ToArray());
        if (string.IsNullOrWhiteSpace(picked)
            || string.Equals(picked, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return entries
            .FirstOrDefault(entry => string.Equals(entry.Display, picked, StringComparison.Ordinal))
            ?.Option;
    }
}
