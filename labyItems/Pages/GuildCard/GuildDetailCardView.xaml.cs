using System.ComponentModel;
using labyItems.Models.Characters;
using labyItems.Pages.Characters;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using MiracleCardPage = labyItems.Pages.MiracleCard.MiracleCard;
using AbilityCardPage = labyItems.Pages.AbilityCard.AbilityCard;

namespace labyItems.Pages.GuildCard;

public partial class GuildDetailCardView : ContentView, INotifyPropertyChanged
{
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
        var guildName = name ?? record.Type ?? "Guild";
        var guildsVm = new GuildsVm(
            draft: new CharacterDraft(),
            notifyWizardGatingChanged: () => { },
            applyCharacterAvailabilityFilters: false,
            allowGuildSelection: false,
            searchByNameOnly: true);

        await guildsVm.ReloadAsync();
        var card = guildsVm.AllGuilds.FirstOrDefault(g =>
            string.Equals(g.Name, guildName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(g.Type, record.Type, StringComparison.OrdinalIgnoreCase));

        if (card != null)
        {
            GuildVm = card;
        }
        else
        {
            _internalVm = new GuildCardVm
            {
                Name = guildName,
                Type = record.Type ?? string.Empty,
                Logo = record.Logo ?? string.Empty,
                Icon = GuildsVm.IconForType(record.Type ?? string.Empty)
            };
            NotifyAllProperties();
        }
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
                searchByNameOnly: true);

            await guildsVm.ReloadAsync();
            var matched = guildsVm.AllGuilds.FirstOrDefault(g =>
                string.Equals(g.Name, _internalVm.Name, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                guildsVm.ToggleExpandedCommand.Execute(matched);
                _internalVm.ApplyDetails(new GuildCardDetailsVm
                {
                    PreRequisites = matched.PreRequisites,
                    Restrictions = matched.Restrictions,
                    Ethos = matched.Ethos,
                    Background = matched.Background,
                    LoreSections = matched.LoreSections,
                    BasicBenefitRows = matched.BasicBenefitRows,
                    IntermediateBenefitRows = matched.IntermediateBenefitRows,
                    AdvancedBenefitRows = matched.AdvancedBenefitRows,
                    CityBenefits = matched.CityBenefits,
                    MiracleRows = matched.MiracleRows,
                    DenominationalMiracle = matched.DenominationalMiracle
                });
            }
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

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Type));
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
    }

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
        if (sender is not Button button || button.CommandParameter is not GuildBenefitRowVm row)
            return;

        var abilityDef = row.Abilities.FirstOrDefault();
        var abilityName = (abilityDef?.Name ?? row.DisplayText ?? string.Empty).Trim();
        if (abilityName.Length == 0)
            return;

        var ability = await DetailCardLookupService.ResolveAbilityAsync(abilityName)
            ?? await EvolutionService.FindAbilityAsync(abilityName);

        if (ability != null)
        {
            await Navigation.PushAsync(new AbilityCardPage(ability));
        }
    }
}
