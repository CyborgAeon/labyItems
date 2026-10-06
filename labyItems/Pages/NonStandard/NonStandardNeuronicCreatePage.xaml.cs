using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using labyItems.Models.Enums;
using labyItems.Pages.Calculator;
using labyItems.Pages.NeuronicCard;
using labyItems.Services;

namespace labyItems.Pages.NonStandard;

public partial class NonStandardNeuronicCreatePage : ContentPage, INotifyPropertyChanged
{
    private NeuronicService.NeuronicRaw _draft = new();
    private CharacterAssignmentOptionVm? _character;
    private bool _isBusy;
    public new event PropertyChangedEventHandler? PropertyChanged;
    public NonStandardNeuronicCreatePage()
    {
        BackCommand = new Command(async () => await Navigation.PopAsync());
        SearchCommand = new Command(async () => await SearchAsync(), () => !IsBusy);
        ChooseCharacterCommand = new Command(async () => await ChooseCharacterAsync(), () => !IsBusy);
        SaveCommand = new Command(async () => await SaveAsync(), () => CanSave);
        InitializeComponent(); BindingContext = this;
    }
    public NeuronicService.NeuronicRaw Draft { get => _draft; private set { _draft = value; Raise(); Raise(nameof(CanSave)); Refresh(); } }
    public string CharacterName => _character?.Name ?? "No character selected";
    public bool CanSave => !IsBusy && _character != null && !string.IsNullOrWhiteSpace(Draft.name);
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; Raise(); Raise(nameof(CanSave)); Refresh(); } }
    public ICommand BackCommand { get; } public ICommand SearchCommand { get; } public ICommand ChooseCharacterCommand { get; } public ICommand SaveCommand { get; }
    protected override void OnAppearing() { base.OnAppearing(); if (_character != null) return; var c = LiteDbService.GetCharacters().OrderBy(x => x.Name).FirstOrDefault(); if (c != null) _character = new(c.Id,c.Name,c.PlayerName,c.Class); Raise(nameof(CharacterName)); Raise(nameof(CanSave)); Refresh(); }
    private async Task SearchAsync() { var picked = await MpCatalogSearch.PickNeuronicTemplateAsync(Navigation, await NeuronicService.GetAllAsync()); if (picked != null) Draft = Clone(picked); }
    private async Task ChooseCharacterAsync() { var all=LiteDbService.GetCharacters().OrderBy(x=>x.Name).ToList(); var labels=all.Select(x=>x.Name).ToArray(); var picked=await DisplayActionSheet("Assign to character","Cancel",null,labels); var i=Array.IndexOf(labels,picked); if(i<0)return; var c=all[i]; _character=new(c.Id,c.Name,c.PlayerName,c.Class); Raise(nameof(CharacterName)); Raise(nameof(CanSave)); Refresh(); }
    private async void OnFieldEditRequested(object? sender, NeuronicFieldEditRequestedEventArgs e)
    {
        var d=Clone(Draft);
        switch(e.Field)
        {
            case "name": d.name=await Prompt("Name","Neuronic name",d.name); break;
            case "power": var p=await Prompt("Power","TBLP cost",d.power.ToString(),Keyboard.Numeric); if(int.TryParse(p,out var n))d.power=Math.Max(0,n); break;
            case "range": d.range=await Prompt("Range","Range",d.range); break;
            case "duration": d.duration=await Prompt("Duration","Duration",d.duration); break;
            case "description": d.description=await Prompt("Description","Description",d.description); break;
            case "notes": d.notes=await Prompt("Notes","Notes",d.notes); break;
            case "todo": d.todo=await Prompt("Todo","Todo",d.todo); break;
            case "asPer": d.asPer=await Prompt("As per","Spell or neuronic reference",d.asPer); break;
            case "tree": d.Type=await DisplayAlert("Neuronic type","Select its type","Active","Passive")?NeuroOptionType.Active:NeuroOptionType.Passive; d.tree=NeuronicService.FormatType(d.Type); break;
            case "immunity": var a=await MpCatalogSearch.PickAbilityAsync(Navigation,await EvolutionService.GetAllAbilitiesAsync()); if(a!=null){var name=string.IsNullOrWhiteSpace(a.DisplayName)?a.Index:a.DisplayName;d.immunities=name;d.immunityName=[name];d.immunityIndex=[a.Index];} break;
            case "Damage": case "Heal": var edited=await NeuronicCombatEditorPage.EditAsync(Navigation,d,e.Field=="Damage"); if(edited!=null)d=edited; break;
        }
        Draft=d;
    }
    private async Task SaveAsync(){if(!CanSave||_character==null)return;IsBusy=true;try{Draft.tree=NeuronicService.FormatType(Draft.Type==NeuroOptionType.None?NeuronicService.ParseType(Draft.tree):Draft.Type);await NonStandardContentService.SaveAsync(new(){EntityType=NonStandardEntityType.Neuronic,Name=Draft.name.Trim(),DataJson=JsonSerializer.Serialize(Draft),AssignedCharacterId=_character.Id,AssignedCharacterName=_character.Name,AssignedCharacterPlayerName=_character.PlayerName});await DisplayAlert("Neuronic saved",$"'{Draft.name.Trim()}' was saved for {_character.Name}.","OK");await Navigation.PopAsync();}catch(Exception ex){await DisplayAlert("Save failed",ex.Message,"OK");}finally{IsBusy=false;}}
    private async Task<string> Prompt(string title,string message,string initial,Keyboard? keyboard=null)=>(await DisplayPromptAsync(title,message,"Apply","Cancel",initialValue:initial??"",keyboard:keyboard??Keyboard.Default))??initial??"";
    private static NeuronicService.NeuronicRaw Clone(NeuronicService.NeuronicRaw value)
    {
        var clone = JsonSerializer.Deserialize<NeuronicService.NeuronicRaw>(JsonSerializer.Serialize(value)) ?? new();
        clone.Type = value.Type != NeuroOptionType.None ? value.Type : NeuronicService.ParseType(clone.tree);
        return clone;
    }
    private void Refresh(){((Command)SearchCommand).ChangeCanExecute();((Command)ChooseCharacterCommand).ChangeCanExecute();((Command)SaveCommand).ChangeCanExecute();}
    private void Raise([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
