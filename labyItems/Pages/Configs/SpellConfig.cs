using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using labyItems.Pages.Calculator;
namespace labyItems.Pages.Configs;

public class SpellConfig : INotifyPropertyChanged
{
    private string _spellName = "Spell";
    public string SpellName
    {
        get => _spellName;
        set { if (_spellName != value) { _spellName = value; OnPropertyChanged(); OnPropertyChanged(nameof(Title)); } }
    }

    private int _power;
    public int Power
    {
        get => _power;
        set
        {
            var v = Math.Max(0, value); if (_power != v)
            {
                _power = v;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Power));
                Recalculate();
            }
        }
    }
    private bool? _isAdvanced;
    public bool? IsAdvanced
    {
        get => _isAdvanced;
        set { if (_isAdvanced != value) { _isAdvanced = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAdvanced)); } }
    }
    private string _colour;
    public string Colour
    {
        get => _colour;
        set { if (_colour != value) { _colour = value; OnPropertyChanged(); OnPropertyChanged(nameof(Colour)); } }
    }

    public string Title => $"{SpellName} (Power {Power})";

    private int _basicPerDay;
    public int BasicPerDay { get => _basicPerDay; set { var v = Math.Max(0, value); if (_basicPerDay != v) { _basicPerDay = v; OnPropertyChanged(); Recalculate(); } } }

    private int _publishedPerDay; // “wizard’s any published spell x/day”
    public int PublishedPerDay { get => _publishedPerDay; set { var v = Math.Max(0, value); if (_publishedPerDay != v) { _publishedPerDay = v; OnPropertyChanged(); Recalculate(); } } }

    private bool _innateIsMantic; // ×4 to the innate block
    public bool InnateIsMantic { get => _innateIsMantic; set { if (_innateIsMantic != value) { _innateIsMantic = value; OnPropertyChanged(); Recalculate(); } } }

    // ===== Additional mana =====
    private int _additionalGenericMana;
    public int AdditionalGenericMana
    {
        get => _additionalGenericMana; set
        {
            var v = Math.Max(0, value);
            if (v > 12) return;
            if (_additionalGenericMana != v) { _additionalGenericMana = v; OnPropertyChanged(); Recalculate(); }
        }
    }

    private int _additionalManaOfColour;
    public int AdditionalManaOfColour
    {
        get => _additionalManaOfColour;
        set
        {
            var v = Math.Max(0, value);
            if (v > 12) return;
            if (_additionalManaOfColour != v) { _additionalManaOfColour = v; OnPropertyChanged(); Recalculate(); }
        }
    }

    private string _additionalManaColour = "";
    public string AdditionalManaColour
    {
        get => _additionalManaColour;
        set
        {
            var v = value ?? "";
            if (_additionalManaColour != v) { _additionalManaColour = v; OnPropertyChanged(); /* purely cosmetic */ }
        }
    }

    // ===== Base list / power store =====
    private bool _powerStoreRegenerates;
    public bool PowerStoreRegenerates { get => _powerStoreRegenerates; set { if (_powerStoreRegenerates != value) { _powerStoreRegenerates = value; OnPropertyChanged(); Recalculate(); } } }

    private bool _addBasicToBaseList;
    public bool AddBasicToBaseList { get => _addBasicToBaseList; set { if (_addBasicToBaseList != value) { _addBasicToBaseList = value; OnPropertyChanged(); Recalculate(); } } }

    private bool _addAdvancedToBaseList;
    public bool AddAdvancedToBaseList { get => _addAdvancedToBaseList; set { if (_addAdvancedToBaseList != value) { _addAdvancedToBaseList = value; OnPropertyChanged(); Recalculate(); } } }

    // ===== Mantic conversions =====
    private int _turnHandbookToSixthMantic;
    public int TurnHandbookToSixthMantic { get => _turnHandbookToSixthMantic; set { var v = Math.Max(0, value); if (_turnHandbookToSixthMantic != v) { _turnHandbookToSixthMantic = v; OnPropertyChanged(); Recalculate(); } } }

    private int _turnAnyBasicMantic;
    public int TurnAnyBasicMantic { get => _turnAnyBasicMantic; set { var v = Math.Max(0, value); if (_turnAnyBasicMantic != v) { _turnAnyBasicMantic = v; OnPropertyChanged(); Recalculate(); } } }

    private int _turnAnyPublishedToSixthMantic;
    public int TurnAnyPublishedToSixthMantic { get => _turnAnyPublishedToSixthMantic; set { var v = Math.Max(0, value); if (_turnAnyPublishedToSixthMantic != v) { _turnAnyPublishedToSixthMantic = v; OnPropertyChanged(); Recalculate(); } } }

    private int _turnAnyPublishedMantic;
    public int TurnAnyPublishedMantic { get => _turnAnyPublishedMantic; set { var v = Math.Max(0, value); if (_turnAnyPublishedMantic != v) { _turnAnyPublishedMantic = v; OnPropertyChanged(); Recalculate(); } } }

    // ===== Teaching scroll/scripture =====
    private bool _isTeachingScroll;
    public bool IsTeachingScroll { get => _isTeachingScroll; set { if (_isTeachingScroll != value) { _isTeachingScroll = value; OnPropertyChanged(); Recalculate(); } } }

    private int _total;
    public int Total { get => _total; private set { if (_total != value) { _total = value; OnPropertyChanged(); } } }

    private string _breakdown = "";
    public string Breakdown { get => _breakdown; private set { if (_breakdown != value) { _breakdown = value; OnPropertyChanged(); } } }

    public void ApplySpell(Spell.Result picked)
    {
        SpellName = string.IsNullOrWhiteSpace(picked.Name) ? "Spell" : picked.Name;
        Power = Math.Max(1, picked.Power);
        IsAdvanced = picked.IsAdvanced;
        Colour = picked.Colour;
    }

    public void Recalculate()
    {
        int total = 0;
        var sb = new StringBuilder();
        int innates =
            (2 * Power * Math.Max(0, BasicPerDay)) +
            (3 * Power * Math.Max(0, PublishedPerDay));

        if (innates > 0)
        {
            if (InnateIsMantic)
            {
                int before = innates;
                innates *= 4;

                sb.AppendLine($"{BasicPerDay} innates of mantic {SpellName} ({Power}) × 4 \nRegular = {before} Mantic = {innates}");
            }
            else
            {
                sb.AppendLine($"{BasicPerDay} innates of {SpellName} ({Power}): 2×{Power}×{Math.Max(BasicPerDay, PublishedPerDay)} = {innates}");
            }
            total += innates;
        }

        // Additional mana
        if (AdditionalGenericMana > 0)
        {
            int c = 4 * AdditionalGenericMana;
            total += c;
            sb.AppendLine($"+ Additional mana: 4 × {AdditionalGenericMana} = {c}");
        }

        if (AdditionalManaOfColour > 0)
        {
            int c = 3 * AdditionalManaOfColour;
            total += c;
            var col = string.IsNullOrWhiteSpace(AdditionalManaColour) ? "" : $" ({AdditionalManaColour})";
            sb.AppendLine($"+ Additional mana of colour {col}: 3 × {AdditionalManaOfColour} = {c}");
        }

        // Power store / base list flags
        if (PowerStoreRegenerates) { total += 25; sb.AppendLine("+ Power store regenerates at 1/15 minutes: 25"); }
        if (AddBasicToBaseList) { total += 15; sb.AppendLine("+ Add basic spell to base list: 15"); }
        if (AddAdvancedToBaseList) { total += 18; sb.AppendLine("+ Add advanced spell to base list: 18"); }

        // Mantic conversions
        if (TurnHandbookToSixthMantic > 0)
        {
            int c = 40 * TurnHandbookToSixthMantic;
            total += c;
            sb.AppendLine($"+ Turn handbook spell to 6th mantic: 40 × {TurnHandbookToSixthMantic} = {c}");
        }

        if (TurnAnyBasicMantic > 0)
        {
            int c = 50 * TurnAnyBasicMantic;
            total += c;
            sb.AppendLine($"+ Turn any basic spell mantic: 50 × {TurnAnyBasicMantic} = {c}");
        }

        if (TurnAnyPublishedToSixthMantic > 0)
        {
            int c = 60 * TurnAnyPublishedToSixthMantic;
            total += c;
            sb.AppendLine($"+ Turn any published spell to 6th mantic: 60 × {TurnAnyPublishedToSixthMantic} = {c}");
        }

        if (TurnAnyPublishedMantic > 0)
        {
            int c = 80 * TurnAnyPublishedMantic;
            total += c;
            sb.AppendLine($"+ Turn any published spell mantic: 80 × {TurnAnyPublishedMantic} = {c}");
        }

        // Teaching scroll/scripture
        if (IsTeachingScroll)
        {
            int c = 2 * Power;
            total += c;
            sb.AppendLine($"+ Teaching scroll/scripture of chosen spell: 2 × Power ({Power}) = {c}");
        }

        Total = total;
        Breakdown = sb.ToString().TrimEnd();
    }

    // INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
