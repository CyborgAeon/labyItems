using labyItems.Helpers;
using System.ComponentModel;
using labyItems.Controls;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Pages.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.GuildCard;

public partial class GuildDetailCardView : ContentView, INotifyPropertyChanged
{
    private static readonly string[] HiddenAvailabilityFieldTypes =
    [
        "Race",
        "Class",
        "PeopleType"
    ];
    public static readonly BindableProperty GuildVmProperty = BindableProperty.Create(
        nameof(GuildVm),
        typeof(GuildCardVm),
        typeof(GuildDetailCardView),
        null,
        propertyChanged: OnGuildVmChanged);

    public static readonly BindableProperty GuildProperty = BindableProperty.Create(
        nameof(Guild),
        typeof(GuildRecord),
        typeof(GuildDetailCardView),
        null,
        propertyChanged: OnGuildChanged);

    public GuildCardVm? GuildVm
    {
        get => (GuildCardVm?)GetValue(GuildVmProperty);
        set => SetValue(GuildVmProperty, value);
    }

    public GuildRecord? Guild
    {
        get => (GuildRecord?)GetValue(GuildProperty);
        set => SetValue(GuildProperty, value);
    }

    private GuildCardVm? _internalVm;
    private GuildRecord? _guildRecord;
    private bool _isMiraclesExpanded = true;
    private bool _isLoadingDetails;

    public GuildDetailCardView()
    {
        InitializeComponent();
    }

    private static async void OnGuildVmChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GuildDetailCardView view)
        {
            if (oldValue is GuildCardVm oldVm)
                oldVm.PropertyChanged -= view.OnInternalVmPropertyChanged;

            view._internalVm = newValue as GuildCardVm;
            if (view._internalVm != null)
            {
                view._internalVm.PropertyChanged += view.OnInternalVmPropertyChanged;
                await view.EnsureDetailsLoadedAsync();
            }
            view.NotifyAllProperties();
        }
    }

    private static async void OnGuildChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is GuildDetailCardView view && newValue is GuildRecord record)
        {
            await view.LoadFromRecordAsync(record);
        }
    }

    private void OnInternalVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyAllProperties();
    }

    public async Task LoadFromRecordAsync(GuildRecord record, string? name = null)
    {
        _guildRecord = record;
        var guildName = name ?? record.Type ?? "Guild";
        var guildsVm = new GuildsVm(
            draft: new CharacterDraft(),
            notifyWizardGatingChanged: () => { },
            applyCharacterAvailabilityFilters: false,
            allowGuildSelection: false,
            searchByNameOnly: true,
            autoReload: false);

        var card = new GuildCardVm
        {
            Name = guildName,
            Type = record.Type ?? string.Empty,
            Logo = record.Logo ?? string.Empty,
            Icon = GuildsVm.IconForType(record.Type ?? string.Empty)
        };
        _internalVm = card;
        _internalVm.PropertyChanged += OnInternalVmPropertyChanged;
        await guildsVm.LoadCardDetailsAsync(card, record);
        NotifyAllProperties();
    }

    private async Task EnsureDetailsLoadedAsync()
    {
        if (_internalVm == null || _internalVm.DetailsLoaded || _isLoadingDetails)
            return;

        _isLoadingDetails = true;
        OnPropertyChanged(nameof(IsDetailsLoading));

        try
        {
            var guildsVm = new GuildsVm(
                draft: new CharacterDraft(),
                notifyWizardGatingChanged: () => { },
                applyCharacterAvailabilityFilters: false,
                allowGuildSelection: false,
                searchByNameOnly: true,
                autoReload: false);

            var records = await GuildsService.GetAllAsync();
            records.TryGetValue(_internalVm.Name, out _guildRecord);
            await guildsVm.LoadCardDetailsAsync(_internalVm, _guildRecord);
        }
        catch
        {
            // Best effort
        }
        finally
        {
            _isLoadingDetails = false;
            OnPropertyChanged(nameof(IsDetailsLoading));
            NotifyAllProperties();
        }
    }

    public string Name => _internalVm?.Name ?? string.Empty;
    public string Type => _internalVm?.Type ?? string.Empty;
    public string TypeDisplayText => string.IsNullOrWhiteSpace(Type)
        ? "Guild"
        : $"{char.ToUpperInvariant(Type.Trim()[0])}{Type.Trim()[1..]}";
    public string Icon => _internalVm?.Icon ?? "📜";
    public string Logo => _internalVm?.Logo ?? string.Empty;
    public bool HasLogo => !string.IsNullOrWhiteSpace(Logo);
    public bool ShowLogo => HasLogo;
    public bool ShowIcon => !ShowLogo;
    public string ChoiceSetNoticeText => _internalVm?.ChoiceSetNoticeText ?? string.Empty;
    public bool HasChoiceSetNotice => !string.IsNullOrWhiteSpace(ChoiceSetNoticeText);

    public bool IsDetailsLoading => _isLoadingDetails || (_internalVm?.IsDetailsLoading ?? false);

    public string Ethos => _internalVm?.Ethos ?? string.Empty;
    public bool HasEthos => !string.IsNullOrWhiteSpace(Ethos);

    public string PreRequisites => _internalVm?.PreRequisites ?? string.Empty;
    public bool HasPreRequisites => !string.IsNullOrWhiteSpace(PreRequisites);

    public string Restrictions => _internalVm?.Restrictions ?? string.Empty;
    public bool HasRestrictions => !string.IsNullOrWhiteSpace(Restrictions);

    public bool HasOverviewInfo => HasEthos || HasPreRequisites || HasRestrictions;
    public string Background => _internalVm?.Background ?? _guildRecord?.Background ?? string.Empty;
    public bool HasBackground => !string.IsNullOrWhiteSpace(Background);
    public bool Has30KGift => _guildRecord?.Has30KGift == true;

    public IReadOnlyList<GuildLoreSectionVm> LoreSections => _internalVm?.LoreSections ?? (IReadOnlyList<GuildLoreSectionVm>)Array.Empty<GuildLoreSectionVm>();
    public bool HasLore => LoreSections.Count > 0;

    public IReadOnlyList<GuildMiracleRowVm> MiracleRows => _internalVm?.MiracleRows ?? (IReadOnlyList<GuildMiracleRowVm>)Array.Empty<GuildMiracleRowVm>();
    public bool HasMiracles => MiracleRows.Count > 0;

    public bool IsMiraclesExpanded
    {
        get => _isMiraclesExpanded;
        set
        {
            if (_isMiraclesExpanded == value) return;
            _isMiraclesExpanded = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<GuildBenefitRowVm> BasicBenefitRows => _internalVm?.BasicBenefitRows ?? (IReadOnlyList<GuildBenefitRowVm>)Array.Empty<GuildBenefitRowVm>();
    public bool HasBasic => BasicBenefitRows.Count > 0;

    public IReadOnlyList<GuildBenefitRowVm> IntermediateBenefitRows => _internalVm?.IntermediateBenefitRows ?? (IReadOnlyList<GuildBenefitRowVm>)Array.Empty<GuildBenefitRowVm>();
    public bool HasIntermediate => IntermediateBenefitRows.Count > 0;

    public IReadOnlyList<GuildBenefitRowVm> AdvancedBenefitRows => _internalVm?.AdvancedBenefitRows ?? (IReadOnlyList<GuildBenefitRowVm>)Array.Empty<GuildBenefitRowVm>();
    public bool HasAdvanced => AdvancedBenefitRows.Count > 0;

    public IReadOnlyList<GuildCityBenefitVm> CityBenefits => _internalVm?.CityBenefits ?? (IReadOnlyList<GuildCityBenefitVm>)Array.Empty<GuildCityBenefitVm>();
    public bool HasCityBenefits => CityBenefits.Count > 0;
    public GuildDenominationalMiracleVm? DenominationalMiracle => _internalVm?.DenominationalMiracle;
    public bool HasDenominationalMiracle => DenominationalMiracle != null;
    private bool _isAvailabilityExpanded;
    public bool IsAvailabilityExpanded
    {
        get => _isAvailabilityExpanded;
        set
        {
            if (_isAvailabilityExpanded == value) return;
            _isAvailabilityExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PrimaryAvailabilityText));
        }
    }
    public IReadOnlyList<AvailabilityRequirementVm> AvailabilityRequirements => BuildAvailabilityRequirements();
    public AvailabilityRequirementVm PrimaryAvailability => AvailabilityRequirements.First();
    public string PrimaryAvailabilityText => HasMultipleAvailabilityRequirements && !IsAvailabilityExpanded
        ? $"{PrimaryAvailability.Text} …"
        : PrimaryAvailability.Text;
    public IReadOnlyList<AvailabilityRequirementVm> AdditionalAvailabilityRequirements => AvailabilityRequirements.Skip(1).ToList();
    public bool HasMultipleAvailabilityRequirements => AvailabilityRequirements.Count > 1;

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(TypeDisplayText));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(Logo));
        OnPropertyChanged(nameof(HasLogo));
        OnPropertyChanged(nameof(ShowLogo));
        OnPropertyChanged(nameof(ShowIcon));
        OnPropertyChanged(nameof(ChoiceSetNoticeText));
        OnPropertyChanged(nameof(HasChoiceSetNotice));
        OnPropertyChanged(nameof(IsDetailsLoading));
        OnPropertyChanged(nameof(Ethos));
        OnPropertyChanged(nameof(HasEthos));
        OnPropertyChanged(nameof(PreRequisites));
        OnPropertyChanged(nameof(HasPreRequisites));
        OnPropertyChanged(nameof(Restrictions));
        OnPropertyChanged(nameof(HasRestrictions));
        OnPropertyChanged(nameof(HasOverviewInfo));
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(HasBackground));
        OnPropertyChanged(nameof(Has30KGift));
        OnPropertyChanged(nameof(LoreSections));
        OnPropertyChanged(nameof(HasLore));
        OnPropertyChanged(nameof(MiracleRows));
        OnPropertyChanged(nameof(HasMiracles));
        OnPropertyChanged(nameof(BasicBenefitRows));
        OnPropertyChanged(nameof(HasBasic));
        OnPropertyChanged(nameof(IntermediateBenefitRows));
        OnPropertyChanged(nameof(HasIntermediate));
        OnPropertyChanged(nameof(AdvancedBenefitRows));
        OnPropertyChanged(nameof(HasAdvanced));
        OnPropertyChanged(nameof(CityBenefits));
        OnPropertyChanged(nameof(HasCityBenefits));
        OnPropertyChanged(nameof(DenominationalMiracle));
        OnPropertyChanged(nameof(HasDenominationalMiracle));
        OnPropertyChanged(nameof(AvailabilityRequirements));
        OnPropertyChanged(nameof(PrimaryAvailability));
        OnPropertyChanged(nameof(PrimaryAvailabilityText));
        OnPropertyChanged(nameof(AdditionalAvailabilityRequirements));
        OnPropertyChanged(nameof(HasMultipleAvailabilityRequirements));
    }

    private IReadOnlyList<AvailabilityRequirementVm> BuildAvailabilityRequirements()
    {
        var availability = _guildRecord?.Availability;
        if (availability == null)
            return new[] { new AvailabilityRequirementVm(FontAwesomeGlyphs.Race, "Any") };

        var rows = new List<AvailabilityRequirementVm>();
        var orders = ValuesFor(availability.Rules, "Alignment.Order");
        var morals = ValuesFor(availability.Rules, "Alignment.Moral");
        var alignment = FormatAlignment(orders, morals);
        if (alignment.Length > 0)
            rows.Add(new AvailabilityRequirementVm(AlignmentGlyph(morals), alignment));
        AddExcludedAlignment(rows, availability.Rules, "Alignment.Order");
        AddExcludedAlignment(rows, availability.Rules, "Alignment.Moral");

        AddRequirement(rows, availability.Rules, "Bracket", "Brackets", FontAwesomeGlyphs.GraduationCap);
        AddRequirement(rows, availability.Rules, "Class", "Classes", FontAwesomeGlyphs.GraduationCap);
        AddRequirement(rows, availability.Rules, "Class|Bracket", "Classes or brackets", FontAwesomeGlyphs.GraduationCap);
        AddRequirement(rows, availability.Rules, "Race|Class", "Races or classes", FontAwesomeGlyphs.GraduationCap);
        AddRequirement(rows, availability.Rules, "Race", "Races", FontAwesomeGlyphs.Race);
        AddRequirement(rows, availability.Rules, "PeopleType", "People types", FontAwesomeGlyphs.Race);
        AddRequirement(rows, availability.Rules, "RaceSubtype", "Race subtypes", FontAwesomeGlyphs.Race);
        AddRequirement(rows, availability.Rules, "Guild", "Guilds", FontAwesomeGlyphs.Guild);

        if (!string.IsNullOrWhiteSpace(availability.RequiredGuild))
            rows.Add(new AvailabilityRequirementVm(FontAwesomeGlyphs.Guild, $"Guild: {availability.RequiredGuild.Trim()}"));

        return rows.Count == 0
            ? new[] { new AvailabilityRequirementVm(FontAwesomeGlyphs.Race, "Any") }
            : rows;
    }

    private static List<string> ValuesFor(IEnumerable<RuleClause> rules, string field) =>
        rules.Where(rule => string.Equals(rule.Field, field, StringComparison.OrdinalIgnoreCase))
            .Where(rule => rule.Operator is RuleComparisonOp.In or RuleComparisonOp.Only)
            .SelectMany(rule => rule.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string FormatAlignment(IReadOnlyCollection<string> orders, IReadOnlyCollection<string> morals)
    {
        var allOrders = ContainsAll(orders, "Lawful", "Neutral", "Chaotic");
        var allMorals = ContainsAll(morals, "Good", "Neutral", "Evil");
        if (orders.Count == 0 && morals.Count == 0)
            return string.Empty;
        if ((orders.Count == 0 || allOrders) && (morals.Count == 0 || allMorals))
            return "Any alignment";
        if (orders.Count == 0 || allOrders)
            return $"Any {string.Join("/", morals)}";
        if (morals.Count == 0 || allMorals)
            return $"Any {string.Join("/", orders)}";
        return $"{string.Join("/", orders)} · {string.Join("/", morals)}";
    }

    private static bool ContainsAll(IReadOnlyCollection<string> values, params string[] expected) =>
        expected.All(item => values.Contains(item, StringComparer.OrdinalIgnoreCase));

    private static void AddExcludedAlignment(
        ICollection<AvailabilityRequirementVm> output,
        IEnumerable<RuleClause> rules,
        string field)
    {
        var values = rules
            .Where(rule => rule.Operator == RuleComparisonOp.NotIn
                && string.Equals(rule.Field, field, StringComparison.OrdinalIgnoreCase))
            .SelectMany(rule => rule.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (values.Count > 0)
            output.Add(new AvailabilityRequirementVm(AlignmentGlyph(values), $"Not {string.Join(", ", values)}"));
    }

    private static void AddRequirement(
        ICollection<AvailabilityRequirementVm> output,
        IEnumerable<RuleClause> rules,
        string fieldToken,
        string label,
        string glyph)
    {
        var matchingRules = rules
            .Where(rule => string.Equals(rule.Field, fieldToken, StringComparison.OrdinalIgnoreCase))
            .GroupBy(rule => rule.Operator);

        foreach (var operatorGroup in matchingRules)
        {
            var values = operatorGroup.SelectMany(rule => rule.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (values.Count == 0)
                continue;

            var valueText = string.Join(", ", values);
            var hideFieldName = HiddenAvailabilityFieldTypes.Contains(fieldToken, StringComparer.OrdinalIgnoreCase);
            var text = operatorGroup.Key switch
            {
                RuleComparisonOp.NotIn => $"Not {valueText}",
                RuleComparisonOp.Only when hideFieldName => $"Only {valueText}",
                RuleComparisonOp.Only => $"{label}: only {valueText}",
                _ when hideFieldName => valueText,
                _ => $"{label}: {valueText}"
            };
            output.Add(new AvailabilityRequirementVm(glyph, text));
        }
    }

    private static string AlignmentGlyph(IReadOnlyCollection<string> morals)
    {
        var hasGood = morals.Contains("Good", StringComparer.OrdinalIgnoreCase);
        var hasEvil = morals.Contains("Evil", StringComparer.OrdinalIgnoreCase);
        if (hasGood && !hasEvil) return FontAwesomeGlyphs.Goodly;
        if (hasEvil && !hasGood) return FontAwesomeGlyphs.Evil;
        return FontAwesomeGlyphs.Neutral;
    }

    private void OnAvailabilityTapped(object? sender, TappedEventArgs e)
    {
        if (HasMultipleAvailabilityRequirements)
            IsAvailabilityExpanded = !IsAvailabilityExpanded;
    }

    public sealed record AvailabilityRequirementVm(string Glyph, string Text);

    private void OnToggleMiraclesTapped(object? sender, TappedEventArgs e)
    {
        IsMiraclesExpanded = !IsMiraclesExpanded;
    }

    private async void OnMiracleInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not GuildMiracleRowVm row)
            return;

        var miracleName = (row.Name ?? string.Empty).Trim();
        if (miracleName.Length == 0)
            return;

        var miracles = await MiracleService.GetAllAsync();
        var miracle = miracles.FirstOrDefault(m =>
            string.Equals(m?.name ?? string.Empty, miracleName, StringComparison.OrdinalIgnoreCase));

        if (miracle == null)
            return;

        await Navigation.PushAsync(new MiracleCardPage(miracle));
    }

    private async void OnBenefitInfoClicked(object? sender, EventArgs e)
    {
        var row = sender switch
        {
            Button { CommandParameter: GuildBenefitRowVm buttonRow } => buttonRow,
            ListItemRowView { ActionCommandParameter: GuildBenefitRowVm listRow } => listRow,
            _ => null
        };
        if (row == null)
            return;

        var abilityDef = row.Abilities.FirstOrDefault();
        var abilityName = (abilityDef?.Name ?? row.DisplayText ?? string.Empty).Trim();
        if (abilityName.Length == 0)
            return;

        var abilityKey = (abilityDef?.AbilityRef ?? abilityDef?.Key ?? abilityName).Trim();
        var ability = await DetailCardLookupService.ResolveAbilityAsync(abilityKey)
            ?? await DetailCardLookupService.ResolveAbilityAsync(abilityName)
            ?? await EvolutionService.FindAbilityAsync(abilityName);

        if (ability != null)
        {
            await Navigation.PushAsync(new AbilityCardPage(ability));
        }
    }
}
