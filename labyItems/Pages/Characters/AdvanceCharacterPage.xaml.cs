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

        MainThread.BeginInvokeOnMainThread(async () => await _vm.InitializeAsync());
    }
}
