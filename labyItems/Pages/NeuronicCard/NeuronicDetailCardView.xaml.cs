using System.Collections.ObjectModel;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.NeuronicCard;

public partial class NeuronicDetailCardView : ContentView
{
    public static readonly BindableProperty NeuronicProperty = BindableProperty.Create(
        nameof(Neuronic),
        typeof(NeuronicService.NeuronicRaw),
        typeof(NeuronicDetailCardView),
        default(NeuronicService.NeuronicRaw),
        propertyChanged: OnNeuronicChanged);

    public NeuronicService.NeuronicRaw? Neuronic
    {
        get => (NeuronicService.NeuronicRaw?)GetValue(NeuronicProperty);
        set => SetValue(NeuronicProperty, value);
    }

    public string NeuronicName => ReadOrFallback(Neuronic?.name, "Unnamed Neuronic");
    public string PowerDisplayText => $"{Math.Max(0, Neuronic?.power ?? 0)} TBLP";
    public string TypeDisplayText => Neuronic?.Type switch
    {
        NeuroOptionType.Active => "Active",
        NeuroOptionType.Passive => "Passive",
        _ => "Unclassified"
    };

    public string DescriptionText => ReadOrFallback(Neuronic?.description, "No description provided.");
    public string AsPerText => (Neuronic?.asPer ?? string.Empty).Trim();
    public bool HasAsPer => AsPerText.Length > 0;
    public string NotesText => (Neuronic?.notes ?? string.Empty).Trim();
    public bool HasNotes => NotesText.Length > 0;
    public string TodoText => (Neuronic?.todo ?? string.Empty).Trim();
    public bool HasTodo => TodoText.Length > 0;
    public string DamageSummaryText => BuildDamageSummary(Neuronic);
    public bool HasDamageSummary => DamageSummaryText.Length > 0;

    public ObservableCollection<NeuronicMetaChipVm> MetaChips { get; } = new();
    public bool HasMetaChips => MetaChips.Count > 0;

    public NeuronicDetailCardView()
    {
        InitializeComponent();
    }

    private static void OnNeuronicChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not NeuronicDetailCardView view)
            return;

        view.RebuildMetaChips();
        view.RaiseComputedProperties();
    }

    private void RebuildMetaChips()
    {
        MetaChips.Clear();

        AddMetaChip("Range", Neuronic?.range);
        AddMetaChip("Duration", Neuronic?.duration);
        AddMetaChip("Immunities", Neuronic?.immunities);

        OnPropertyChanged(nameof(HasMetaChips));
    }

    private void AddMetaChip(string label, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return;

        MetaChips.Add(new NeuronicMetaChipVm($"{label}: {text}"));
    }

    private void RaiseComputedProperties()
    {
        OnPropertyChanged(nameof(NeuronicName));
        OnPropertyChanged(nameof(PowerDisplayText));
        OnPropertyChanged(nameof(TypeDisplayText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(AsPerText));
        OnPropertyChanged(nameof(HasAsPer));
        OnPropertyChanged(nameof(NotesText));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(TodoText));
        OnPropertyChanged(nameof(HasTodo));
        OnPropertyChanged(nameof(DamageSummaryText));
        OnPropertyChanged(nameof(HasDamageSummary));
        OnPropertyChanged(nameof(HasMetaChips));
    }

    private static string BuildDamageSummary(NeuronicService.NeuronicRaw? neuronic)
    {
        if (neuronic == null)
            return string.Empty;

        var damage = neuronic.GetDamageAmounts();
        if (damage.Count == 0)
            return string.Empty;

        var types = neuronic.GetDamageTypes();
        var armourApplies = neuronic.GetArmourApplies();
        var armourType = neuronic.GetArmourType();
        var parts = new List<string>();

        for (var i = 0; i < damage.Count; i++)
        {
            var amount = damage[i];
            var tblp = At(amount, 0);
            var loc = At(amount, 1);
            var type = OrLast(types, i);
            var text = $"{(string.IsNullOrWhiteSpace(type) ? "Missile" : type)}: {tblp}/{loc}";

            if (!string.IsNullOrWhiteSpace(armourType))
            {
                var armour = OrDefault(armourApplies, i, Array.Empty<int>());
                var armTblp = At(armour, 0);
                var armLoc = At(armour, 1);
                if (armTblp > 0 || armLoc > 0)
                    text += $" [{armourType} {armTblp}/{armLoc}]";
            }

            parts.Add(text);
        }

        return string.Join("; ", parts);
    }

    private static string ReadOrFallback(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }

    private static int At(int[]? arr, int index)
        => arr != null && index >= 0 && index < arr.Length ? arr[index] : 0;

    private static T? OrLast<T>(IReadOnlyList<T> list, int index)
        => list.Count == 0 ? default : index < list.Count ? list[index] : list[^1];

    private static T OrDefault<T>(IReadOnlyList<T> list, int index, T fallback)
        => list.Count == 0 ? fallback : index < list.Count ? list[index] : list[^1];
}

public sealed class NeuronicMetaChipVm
{
    public string Text { get; }

    public NeuronicMetaChipVm(string text)
    {
        Text = text;
    }
}
