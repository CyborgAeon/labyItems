using System.Collections.ObjectModel;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models.Enums;
using labyItems.Services;

namespace labyItems.Pages.NeuronicCard;

public partial class NeuronicDetailCardView : ContentView
{
    public event EventHandler<NeuronicFieldEditRequestedEventArgs>? FieldEditRequested;
    public static readonly BindableProperty IsEditingProperty = BindableProperty.Create(nameof(IsEditing), typeof(bool), typeof(NeuronicDetailCardView), false,
        propertyChanged: (bindable, _, _) => ((NeuronicDetailCardView)bindable).RaiseComputedProperties());
    public bool IsEditing { get => (bool)GetValue(IsEditingProperty); set => SetValue(IsEditingProperty, value); }
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

    public string NeuronicName => ReadOrFallback(Neuronic?.name, IsEditing ? "Tap to add a name" : "Unnamed Neuronic");
    public string PowerDisplayText => $"{Math.Max(0, Neuronic?.power ?? 0)} TBLP";
    public string RangeDisplayText => ReadOrFallback(Neuronic?.range, IsEditing ? "Tap to set" : "—");
    public string DurationDisplayText => ReadOrFallback(Neuronic?.duration, IsEditing ? "Tap to set" : "—");
    public string ImmunityDisplayText => ReadFirst(JoinValues(Neuronic?.immunityName), Neuronic?.immunities, JoinValues(Neuronic?.immunityIndex));
    public bool HasImmunity => ImmunityDisplayText.Length > 0;
    public bool ShowImmunity => IsEditing || HasImmunity;
    public bool HasImmunityLink => ImmunityNavigationHelper.HasIndex(Neuronic?.immunityIndex);
    public string TypeDisplayText => Neuronic?.Type switch
    {
        NeuroOptionType.Active => "Active Neuronic",
        NeuroOptionType.Passive => "Passive Neuronic",
        _ => "Unclassified Neuronic"
    };

    public string DescriptionText => ReadOrFallback(Neuronic?.description, IsEditing ? "Tap to add a description" : "No description provided.");
    public string AsPerText => (Neuronic?.asPer ?? string.Empty).Trim();
    public bool HasAsPer => AsPerText.Length > 0;
    public bool ShowAsPer => IsEditing || HasAsPer;
    public ObservableCollection<NeuronicAsPerEntryVm> AsPerEntries { get; } = new();
    public string NotesText => (Neuronic?.notes ?? string.Empty).Trim();
    public bool HasNotes => NotesText.Length > 0;
    public bool ShowNotes => IsEditing || HasNotes;
    public string TodoText => (Neuronic?.todo ?? string.Empty).Trim();
    public bool HasTodo => TodoText.Length > 0;
    public bool ShowTodo => IsEditing || HasTodo;
    public string DamageSummaryText => BuildDamageSummary(Neuronic);
    public bool HasDamageSummary => DamageSummaryText.Length > 0;
    public bool ShowDamage => IsEditing || HasDamageSummary;
    public string HealSummaryText => Neuronic?.GetHealAmounts().Count > 0 ? string.Join("; ", Neuronic.GetHealAmounts().Select((p,i) => $"{Neuronic.GetHealTypes().ElementAtOrDefault(i) ?? "Worst"}: {p.ElementAtOrDefault(0)}/{p.ElementAtOrDefault(1)}")) : string.Empty;
    public bool ShowHeal => IsEditing || HealSummaryText.Length > 0;
    public CombatQuickFactsVm DamageQuickFacts => CombatQuickFactsFormatter.Build(
        Neuronic?.Damage != null,
        Neuronic?.GetDamageAmounts() ?? new List<int[]>(),
        Neuronic?.GetDamageTypes() ?? new List<string>(),
        Neuronic?.GetDamageCategories(), Neuronic?.GetArmourApplies(), Neuronic?.GetArmourType());
    public bool HasDamageQuickFacts => DamageQuickFacts.HasValue;

    private static string JoinValues(IEnumerable<string>? values) =>
        string.Join(", ", values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()) ?? Array.Empty<string>());

    private static string ReadFirst(params string?[] values) => values
        .Select(value => (value ?? string.Empty).Trim())
        .FirstOrDefault(value => value.Length > 0) ?? string.Empty;

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
        _ = view.RebuildAsPerEntriesAsync();
    }

    private void RebuildMetaChips()
    {
        MetaChips.Clear();

        AddMetaChip("Range", Neuronic?.range);
        AddMetaChip("Duration", Neuronic?.duration);
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
        OnPropertyChanged(nameof(RangeDisplayText));
        OnPropertyChanged(nameof(DurationDisplayText));
        OnPropertyChanged(nameof(ImmunityDisplayText));
        OnPropertyChanged(nameof(HasImmunity));
        OnPropertyChanged(nameof(ShowImmunity));
        OnPropertyChanged(nameof(HasImmunityLink));
        OnPropertyChanged(nameof(TypeDisplayText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(AsPerText));
        OnPropertyChanged(nameof(HasAsPer));
        OnPropertyChanged(nameof(ShowAsPer));
        OnPropertyChanged(nameof(NotesText));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(ShowNotes));
        OnPropertyChanged(nameof(TodoText));
        OnPropertyChanged(nameof(HasTodo));
        OnPropertyChanged(nameof(ShowTodo));
        OnPropertyChanged(nameof(DamageSummaryText));
        OnPropertyChanged(nameof(HasDamageSummary));
        OnPropertyChanged(nameof(ShowDamage));
        OnPropertyChanged(nameof(HealSummaryText)); OnPropertyChanged(nameof(ShowHeal));
        OnPropertyChanged(nameof(DamageQuickFacts));
        OnPropertyChanged(nameof(HasDamageQuickFacts));
        OnPropertyChanged(nameof(HasMetaChips));
    }

    private async void OnImmunityTapped(object sender, TappedEventArgs e)
    {
        if (RequestEdit("immunity")) return;
        await ImmunityNavigationHelper.OpenFirstAvailableAsync(Navigation, Neuronic?.immunityIndex);
    }
    private async void OnImmunityInfoClicked(object sender, EventArgs e) => await ImmunityNavigationHelper.OpenFirstAvailableAsync(Navigation, Neuronic?.immunityIndex);

    private bool RequestEdit(string field) { if (!IsEditing) return false; FieldEditRequested?.Invoke(this, new(field)); return true; }
    private void OnNameTapped(object sender, TappedEventArgs e) => RequestEdit("name");
    private void OnTypeTapped(object sender, TappedEventArgs e) => RequestEdit("tree");
    private void OnPowerTapped(object sender, TappedEventArgs e) => RequestEdit("power");
    private void OnRangeTapped(object sender, TappedEventArgs e) => RequestEdit("range");
    private void OnDurationTapped(object sender, TappedEventArgs e) => RequestEdit("duration");
    private void OnDescriptionTapped(object sender, TappedEventArgs e) => RequestEdit("description");
    private void OnDamageTapped(object sender, TappedEventArgs e) => RequestEdit("Damage");
    private void OnHealTapped(object sender, TappedEventArgs e) => RequestEdit("Heal");
    private void OnAsPerTapped(object sender, TappedEventArgs e) => RequestEdit("asPer");
    private void OnNotesTapped(object sender, TappedEventArgs e) => RequestEdit("notes");
    private void OnTodoTapped(object sender, TappedEventArgs e) => RequestEdit("todo");

    private async Task RebuildAsPerEntriesAsync()
    {
        AsPerEntries.Clear();
        var raw = AsPerText;
        if (raw.Length == 0) return;
        var kind = raw.StartsWith("$spell.", StringComparison.OrdinalIgnoreCase) ? "spell" : "neuronic";
        var name = raw.Replace("$spell.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$neuro.", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        object? target = kind == "spell"
            ? (await SpellService.SearchAsync(name)).FirstOrDefault(x => x.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            : (await NeuronicService.SearchAsync(name)).FirstOrDefault(x => x.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        AsPerEntries.Add(new NeuronicAsPerEntryVm(name, kind, target));
    }

    private async void OnAsPerInfoClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: NeuronicAsPerEntryVm entry } || entry.Target == null || Navigation == null) return;
        if (entry.Target is SpellService.SpellRaw spell)
            await Navigation.PushAsync(new global::labyItems.Pages.SpellCard.SpellCard(spell));
        else if (entry.Target is NeuronicService.NeuronicRaw neuronic)
            await Navigation.PushAsync(new NeuronicCard(neuronic));
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

public sealed record NeuronicAsPerEntryVm(string Name, string Kind, object? Target);
public sealed class NeuronicFieldEditRequestedEventArgs(string field) : EventArgs { public string Field { get; } = field; }
