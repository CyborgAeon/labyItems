using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages.Characters;

public partial class AdvanceCharacterPage : TabbedPage
{
    private readonly AdvanceCharacterVm _vm;

    public AdvanceCharacterPage(Character character)
        : this(LiteDbService.ToDraft(character) ?? new CharacterDraft())
    {
        Title = string.IsNullOrWhiteSpace(character?.Name) ? "Advance Character" : $"Advance {character.Name}";
    }

    public AdvanceCharacterPage(CharacterDraft draft)
    {
        InitializeComponent();

        _vm = new AdvanceCharacterVm(draft);
        BindingContext = _vm;
        foreach (var child in Children)
            child.BindingContext = _vm;

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Save",
            Command = _vm.SaveCommand
        });

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _vm.InitializeAsync();
            ApplyTabVisibility();
        });
    }

    private void ApplyTabVisibility()
    {
        if (!_vm.ShowSpellsTab && Children.Contains(SpellsTab))
            Children.Remove(SpellsTab);

        if (!_vm.ShowMiraclesTab && Children.Contains(MiraclesTab))
            Children.Remove(MiraclesTab);

        if (!_vm.ShowEvocationsTab && Children.Contains(EvocsTab))
            Children.Remove(EvocsTab);

        if (Children.Contains(DetailsTab))
            CurrentPage = DetailsTab;
    }
}
