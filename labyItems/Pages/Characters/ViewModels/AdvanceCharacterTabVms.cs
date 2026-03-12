using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Graphics;

namespace labyItems.Pages.Characters.ViewModels;

public abstract class AdvanceCharacterTabVmBase : INotifyPropertyChanged, IDisposable
{
    protected AdvanceCharacterVm Root { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected AdvanceCharacterTabVmBase(AdvanceCharacterVm root)
    {
        Root = root;
        Root.PropertyChanged += OnRootPropertyChanged;
    }

    protected virtual void OnRootPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
        => Root.PropertyChanged -= OnRootPropertyChanged;
}

public sealed class AdvanceCharacterDetailsTabVm : AdvanceCharacterTabVmBase
{
    public AdvanceCharacterDetailsTabVm(AdvanceCharacterVm root) : base(root)
    {
    }

    public int Points { get => Root.Points; set => Root.Points = value; }
    public int CurrentVitae { get => Root.CurrentVitae; set => Root.CurrentVitae = value; }
    public string Notes { get => Root.Notes; set => Root.Notes = value; }
    public Dictionary<string, ManuAbilityOption> AbilityOptions => Root.AbilityOptions;
    public ManuAbilityOption? SelectedAbilityOption { get => Root.SelectedAbilityOption; set => Root.SelectedAbilityOption = value; }
    public bool CanAddAbility => Root.CanAddAbility;
    public ObservableCollection<AbilityEntryVm> Abilities => Root.Abilities;
    public string AbilityPointsSummary => Root.AbilityPointsSummary;
    public ObservableCollection<ItemLineVm> Items => Root.Items;
    public ICommand AddAbilityCommand => Root.AddAbilityCommand;
    public ICommand RemoveAbilityCommand => Root.RemoveAbilityCommand;
    public ICommand AddItemCommand => Root.AddItemCommand;
    public ICommand RemoveItemCommand => Root.RemoveItemCommand;
    public Task<Dictionary<string, ManuAbilityOption>> SearchAbilityOptionsAsync(string query)
        => Root.SearchAbilityOptionsAsync(query);
}

public sealed class AdvanceCharacterSpellsTabVm : AdvanceCharacterTabVmBase
{
    public AdvanceCharacterSpellsTabVm(AdvanceCharacterVm root) : base(root)
    {
    }

    public ObservableCollection<SpellListVm> SpellLists => Root.SpellLists;
    public IReadOnlyList<SpecialistSlotSegmentVm> SpecialistSlotSegments => Root.SpecialistSlotSegments;
    public IReadOnlyList<SpecialistSlotLegendVm> SpecialistSlotLegendItems => Root.SpecialistSlotLegendItems;
    public string SpecialistSlotsSummary => Root.SpecialistSlotsSummary;
    public Color SpecialistSlotsSummaryColor => Root.SpecialistSlotsSummaryColor;
    public bool ShowSpecialistSlotsBar => Root.ShowSpecialistSlotsBar;
    public bool ShowSpecialistSlotsLegend => Root.ShowSpecialistSlotsLegend;
    public ICommand ExportSpellsToExcelCommand => Root.ExportSpellsToExcelCommand;
    public ICommand CopySpellsCommand => Root.CopySpellsCommand;
    public ICommand SaveSpellsTextCommand => Root.SaveSpellsTextCommand;
}

public sealed class AdvanceCharacterMiraclesTabVm : AdvanceCharacterTabVmBase
{
    public AdvanceCharacterMiraclesTabVm(AdvanceCharacterVm root) : base(root)
    {
    }

    public bool ShowEvilStairway => Root.ShowEvilStairway;
    public EvilStairwayVm? EvilStairway => Root.EvilStairway;
    public bool ShowPriestMiracleLists => Root.ShowPriestMiracleLists;
    public ObservableCollection<MiracleListVm> MiracleLists => Root.MiracleLists;
    public ICommand ExportMiraclesToExcelCommand => Root.ExportMiraclesToExcelCommand;
    public ICommand CopyMiraclesCommand => Root.CopyMiraclesCommand;
    public ICommand SaveMiraclesTextCommand => Root.SaveMiraclesTextCommand;
}

public sealed class AdvanceCharacterEvocationsTabVm : AdvanceCharacterTabVmBase
{
    public AdvanceCharacterEvocationsTabVm(AdvanceCharacterVm root) : base(root)
    {
    }

    public ObservableCollection<EvocationListVm> EvocationLists => Root.EvocationLists;
}
