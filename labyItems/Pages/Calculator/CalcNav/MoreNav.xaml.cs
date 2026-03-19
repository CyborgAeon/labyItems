using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models;
using labyItems.Pages.Calculator;

namespace labyItems.Pages.Calculator.CalcNav;

public partial class MoreNav : ContentPage
{
    public event Action<CalcContribution>? ContributionAdded;
    private const string MoreContributionId = "more";

    private const int ApprenticeStatusCost = 10;
    private const int JourneymanStatusCost = 15;
    private const int MasterStatusCost = 25;
    private const int AnimateCost = 20;
    private const int BeneficiallyInseparableCost = 20;
    private const int ActivateOnConditionCost = 20;
    private const int ActivateOocCost = 15;
    private const int AdditionalBlowUpDateStepCost = 15;
    private const int ChosenPersonUseCost = 10;
    private const int AlignmentOrBracketUseCost = 100;
    private const int CallsToHandDailyCost = 3;
    private const int UtiliseSpecificTypeCost = 10;

    private const double NoBlowUpOnFirstDeathRate = 0.25d;
    private const double NoBlowUpOnDeathRate = 0.50d;
    private const double UseOnceEverMultiplier = 0.50d;
    private const int ClosedGuildAllMembersMultiplier = 4;
    private const int GuildAnyMemberMultiplier = 3;
    private const int GuildAllMembersMultiplier = 5;

    private static readonly string[] StatusChipOptions =
    [
        "🎓 Apprentice status",
        "🧰 Journeyman status",
        "👑 Master status"
    ];

    private string? _selectedStatus;
    private bool _isAnimate;
    private bool _isBeneficiallyInseparable;
    private bool _activatesOnSpecifiedCondition;
    private string _activationCondition = string.Empty;
    private bool _canActivateOutOfCharacter;
    private bool _doesNotBlowUpOnFirstDeath;
    private bool _doesNotBlowUpOnDeath;
    private int _additionalBlowUpDateSteps;
    private bool _isUseOnceEver;
    private bool _isClosedGuildAllMembersPersonalised;
    private bool _isGuildAnyMemberPersonalised;
    private bool _isGuildAllMembersPersonalised;
    private bool _canBeUsedByChosenPersonForFiveMinutes;
    private bool _canBeUsedByAlignmentOrBracket;
    private int _callsToHandPerDay;
    private bool _grantsUtiliseSpecificType;
    private int _lastPublishedTotalIsp = int.MinValue;
    private string _lastPublishedSummary = string.Empty;

    public IEnumerable<string> StatusOptions => StatusChipOptions;

    public static readonly BindableProperty ReturnToFormCommandProperty = BindableProperty.Create(
        nameof(ReturnToFormCommand),
        typeof(ICommand),
        typeof(MoreNav),
        null);

    public static readonly BindableProperty CalculatorContextProperty = BindableProperty.Create(
        nameof(CalculatorContext),
        typeof(IspCalculator),
        typeof(MoreNav),
        null,
        propertyChanged: static (bindable, oldValue, newValue) =>
        {
            if (bindable is not MoreNav page)
                return;

            page.OnCalculatorContextChanged(oldValue as IspCalculator, newValue as IspCalculator);
        });

    public ICommand? ReturnToFormCommand
    {
        get => (ICommand?)GetValue(ReturnToFormCommandProperty);
        set => SetValue(ReturnToFormCommandProperty, value);
    }

    public IspCalculator? CalculatorContext
    {
        get => (IspCalculator?)GetValue(CalculatorContextProperty);
        set => SetValue(CalculatorContextProperty, value);
    }

    public string? SelectedStatus
    {
        get => _selectedStatus;
        set => SetAndPublish(ref _selectedStatus, value);
    }

    public bool IsAnimate
    {
        get => _isAnimate;
        set => SetAndPublish(ref _isAnimate, value);
    }

    public bool IsBeneficiallyInseparable
    {
        get => _isBeneficiallyInseparable;
        set => SetAndPublish(ref _isBeneficiallyInseparable, value);
    }

    public bool ActivatesOnSpecifiedCondition
    {
        get => _activatesOnSpecifiedCondition;
        set
        {
            if (!SetAndPublish(ref _activatesOnSpecifiedCondition, value))
                return;

            OnPropertyChanged(nameof(ShowActivationConditionField));
            if (!value && !string.IsNullOrWhiteSpace(_activationCondition))
            {
                _activationCondition = string.Empty;
                NotifyPropertyChanged(nameof(ActivationCondition));
                PublishContribution();
            }
        }
    }

    public string ActivationCondition
    {
        get => _activationCondition;
        set => SetAndPublish(ref _activationCondition, value ?? string.Empty);
    }

    public bool ShowActivationConditionField => ActivatesOnSpecifiedCondition;

    public bool CanActivateOutOfCharacter
    {
        get => _canActivateOutOfCharacter;
        set => SetAndPublish(ref _canActivateOutOfCharacter, value);
    }

    public bool DoesNotBlowUpOnFirstDeath
    {
        get => _doesNotBlowUpOnFirstDeath;
        set => SetAndPublish(ref _doesNotBlowUpOnFirstDeath, value);
    }

    public bool DoesNotBlowUpOnDeath
    {
        get => _doesNotBlowUpOnDeath;
        set => SetAndPublish(ref _doesNotBlowUpOnDeath, value);
    }

    public int AdditionalBlowUpDateSteps
    {
        get => _additionalBlowUpDateSteps;
        set
        {
            var sanitized = Math.Max(0, value);
            if (!SetAndPublish(ref _additionalBlowUpDateSteps, sanitized))
                return;

            NotifyPropertyChanged(nameof(AdditionalBlowUpDateSummary));
        }
    }

    public string AdditionalBlowUpDateSummary
        => AdditionalBlowUpDateSteps <= 0
            ? "No additional blow-up date set."
            : $"Additional blow-up date: {AdditionalBlowUpDateSteps * 3} months";

    public bool IsUseOnceEver
    {
        get => _isUseOnceEver;
        set => SetAndPublish(ref _isUseOnceEver, value);
    }

    public bool IsClosedGuildAllMembersPersonalised
    {
        get => _isClosedGuildAllMembersPersonalised;
        set
        {
            if (_isClosedGuildAllMembersPersonalised == value)
                return;

            _isClosedGuildAllMembersPersonalised = value;
            NotifyPropertyChanged();

            if (value)
            {
                if (_isGuildAnyMemberPersonalised)
                {
                    _isGuildAnyMemberPersonalised = false;
                    NotifyPropertyChanged(nameof(IsGuildAnyMemberPersonalised));
                }

                if (_isGuildAllMembersPersonalised)
                {
                    _isGuildAllMembersPersonalised = false;
                    NotifyPropertyChanged(nameof(IsGuildAllMembersPersonalised));
                }
            }

            PublishContribution();
        }
    }

    public bool IsGuildAnyMemberPersonalised
    {
        get => _isGuildAnyMemberPersonalised;
        set
        {
            if (_isGuildAnyMemberPersonalised == value)
                return;

            _isGuildAnyMemberPersonalised = value;
            NotifyPropertyChanged();

            if (value)
            {
                if (_isClosedGuildAllMembersPersonalised)
                {
                    _isClosedGuildAllMembersPersonalised = false;
                    NotifyPropertyChanged(nameof(IsClosedGuildAllMembersPersonalised));
                }

                if (_isGuildAllMembersPersonalised)
                {
                    _isGuildAllMembersPersonalised = false;
                    NotifyPropertyChanged(nameof(IsGuildAllMembersPersonalised));
                }
            }

            PublishContribution();
        }
    }

    public bool IsGuildAllMembersPersonalised
    {
        get => _isGuildAllMembersPersonalised;
        set
        {
            if (_isGuildAllMembersPersonalised == value)
                return;

            _isGuildAllMembersPersonalised = value;
            NotifyPropertyChanged();

            if (value)
            {
                if (_isClosedGuildAllMembersPersonalised)
                {
                    _isClosedGuildAllMembersPersonalised = false;
                    NotifyPropertyChanged(nameof(IsClosedGuildAllMembersPersonalised));
                }

                if (_isGuildAnyMemberPersonalised)
                {
                    _isGuildAnyMemberPersonalised = false;
                    NotifyPropertyChanged(nameof(IsGuildAnyMemberPersonalised));
                }
            }

            PublishContribution();
        }
    }

    public bool CanBeUsedByChosenPersonForFiveMinutes
    {
        get => _canBeUsedByChosenPersonForFiveMinutes;
        set => SetAndPublish(ref _canBeUsedByChosenPersonForFiveMinutes, value);
    }

    public bool CanBeUsedByAlignmentOrBracket
    {
        get => _canBeUsedByAlignmentOrBracket;
        set => SetAndPublish(ref _canBeUsedByAlignmentOrBracket, value);
    }

    public int CallsToHandPerDay
    {
        get => _callsToHandPerDay;
        set => SetAndPublish(ref _callsToHandPerDay, Math.Max(0, value));
    }

    public bool GrantsUtiliseSpecificType
    {
        get => _grantsUtiliseSpecificType;
        set => SetAndPublish(ref _grantsUtiliseSpecificType, value);
    }

    public MoreNav()
    {
        InitializeComponent();
        BindingContext = this;
        PublishContribution();
    }

    private bool SetAndPublish<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        NotifyPropertyChanged(propertyName);
        PublishContribution();
        return true;
    }

    private void OnCalculatorContextChanged(IspCalculator? oldContext, IspCalculator? newContext)
    {
        if (oldContext != null)
            oldContext.TotalChanged -= OnCalculatorTotalChanged;

        if (newContext != null)
            newContext.TotalChanged += OnCalculatorTotalChanged;

        _lastPublishedTotalIsp = int.MinValue;
        _lastPublishedSummary = string.Empty;
        NotifyPropertyChanged(nameof(CalculatorContext));
        PublishContribution();
    }

    private void OnCalculatorTotalChanged(int _) => PublishContribution();

    private void PublishContribution()
    {
        var details = new Dictionary<string, object?>();
        var summaryBits = new List<string>();
        var baseSubtotal = CalculatorContext?.GetTotalExcludingContribution(MoreContributionId) ?? 0;
        var runningTotal = Math.Max(0, baseSubtotal);
        details["baseSubtotal"] = runningTotal;

        var status = NormalizeChipLabel(SelectedStatus);
        var statusCost = ResolveStatusCost(status);
        if (!string.IsNullOrWhiteSpace(status) && statusCost > 0)
        {
            details["status"] = status;
            details["statusCost"] = statusCost;
            runningTotal += statusCost;
            summaryBits.Add($"{status} +{statusCost}");
        }

        var isMasterStatus = status.Equals("Master status", StringComparison.OrdinalIgnoreCase);
        if (IsAnimate)
        {
            details["animate"] = true;
            if (isMasterStatus)
            {
                runningTotal += AnimateCost;
                details["animateCost"] = AnimateCost;
                summaryBits.Add($"Animate +{AnimateCost}");
            }
            else
            {
                details["animateRequiresMaster"] = true;
                summaryBits.Add("Animate selected (requires Master status)");
            }
        }

        if (IsBeneficiallyInseparable)
        {
            details["beneficiallyInseparable"] = true;
            if (isMasterStatus)
            {
                runningTotal += BeneficiallyInseparableCost;
                details["beneficiallyInseparableCost"] = BeneficiallyInseparableCost;
                summaryBits.Add($"Beneficially inseparable +{BeneficiallyInseparableCost}");
            }
            else
            {
                details["beneficiallyInseparableRequiresMaster"] = true;
                summaryBits.Add("Beneficially inseparable selected (requires Master status)");
            }
        }

        if (ActivatesOnSpecifiedCondition)
        {
            details["activatesOnCondition"] = true;
            details["activatesOnConditionCost"] = ActivateOnConditionCost;
            runningTotal += ActivateOnConditionCost;
            var condition = (ActivationCondition ?? string.Empty).Trim();
            if (condition.Length > 0)
            {
                details["activationCondition"] = condition;
                summaryBits.Add($"Condition activation +{ActivateOnConditionCost} ({condition})");
            }
            else
            {
                summaryBits.Add($"Condition activation +{ActivateOnConditionCost}");
            }
        }

        if (CanActivateOutOfCharacter)
        {
            runningTotal += ActivateOocCost;
            details["activateOoc"] = true;
            details["activateOocCost"] = ActivateOocCost;
            summaryBits.Add($"Activate OOC +{ActivateOocCost}");
        }

        if (AdditionalBlowUpDateSteps > 0)
        {
            var months = AdditionalBlowUpDateSteps * 3;
            var cost = AdditionalBlowUpDateSteps * AdditionalBlowUpDateStepCost;
            details["additionalBlowUpMonths"] = months;
            details["additionalBlowUpDateCost"] = cost;
            runningTotal += cost;
            summaryBits.Add($"+{months} months blow-up +{cost}");
        }

        if (CanBeUsedByChosenPersonForFiveMinutes)
        {
            runningTotal += ChosenPersonUseCost;
            details["chosenPersonFiveMinutes"] = true;
            details["chosenPersonFiveMinutesCost"] = ChosenPersonUseCost;
            summaryBits.Add($"Chosen person use +{ChosenPersonUseCost}");
        }

        if (CanBeUsedByAlignmentOrBracket)
        {
            runningTotal += AlignmentOrBracketUseCost;
            details["alignmentOrBracketUse"] = true;
            details["alignmentOrBracketUseCost"] = AlignmentOrBracketUseCost;
            summaryBits.Add($"Alignment/bracket use +{AlignmentOrBracketUseCost}");
        }

        if (CallsToHandPerDay > 0)
        {
            var cost = CallsToHandPerDay * CallsToHandDailyCost;
            details["callsToHandPerDay"] = CallsToHandPerDay;
            details["callsToHandCost"] = cost;
            runningTotal += cost;
            summaryBits.Add($"Calls to hand {CallsToHandPerDay}/day +{cost}");
        }

        if (GrantsUtiliseSpecificType)
        {
            runningTotal += UtiliseSpecificTypeCost;
            details["grantsUtiliseSpecificType"] = true;
            details["grantsUtiliseSpecificTypeCost"] = UtiliseSpecificTypeCost;
            summaryBits.Add($"Grants utilise +{UtiliseSpecificTypeCost}");
        }

        details["subtotalBeforePercentages"] = runningTotal;

        if (DoesNotBlowUpOnFirstDeath)
        {
            var cost = CalculatePercentCost(runningTotal, NoBlowUpOnFirstDeathRate);
            runningTotal += cost;
            details["noBlowUpFirstDeath"] = true;
            details["noBlowUpFirstDeathCost"] = cost;
            summaryBits.Add($"No blow-up on 1st death +25% ({FormatSigned(cost)})");
        }

        if (DoesNotBlowUpOnDeath)
        {
            var cost = CalculatePercentCost(runningTotal, NoBlowUpOnDeathRate);
            runningTotal += cost;
            details["noBlowUpDeath"] = true;
            details["noBlowUpDeathCost"] = cost;
            summaryBits.Add($"No blow-up on death +50% ({FormatSigned(cost)})");
        }

        details["subtotalBeforeMultipliers"] = runningTotal;

        double combinedMultiplier = 1d;
        if (IsUseOnceEver)
        {
            combinedMultiplier *= UseOnceEverMultiplier;
            details["useOnceEver"] = true;
            details["useOnceEverMultiplier"] = UseOnceEverMultiplier;
            summaryBits.Add("Use once ever x0.5");
        }

        var guildMultiplier = ResolveGuildMultiplier(details, summaryBits);
        if (guildMultiplier > 1)
            combinedMultiplier *= guildMultiplier;

        details["combinedMultiplier"] = combinedMultiplier;

        var finalWithMore = ApplyMultiplier(runningTotal, combinedMultiplier);
        var totalIsp = finalWithMore - baseSubtotal;
        details["totalWithMore"] = finalWithMore;
        details["moreIspDelta"] = totalIsp;

        var summaryCore = summaryBits.Count == 0
            ? "No additional modifiers selected."
            : string.Join(" · ", summaryBits);
        var summary = $"{summaryCore} · Δ ISP {FormatSigned(totalIsp)}";

        if (_lastPublishedTotalIsp == totalIsp
            && string.Equals(_lastPublishedSummary, summary, StringComparison.Ordinal))
        {
            return;
        }

        _lastPublishedTotalIsp = totalIsp;
        _lastPublishedSummary = summary;

        ContributionAdded?.Invoke(new CalcContribution(
            Id: MoreContributionId,
            Source: "More",
            Result: new CalcResult
            {
                AbilityType = "More",
                AbilityName = "Additional modifiers",
                TotalIsp = totalIsp,
                Details = details,
                Summary = summary
            },
            OnRemove: ResetSelections));
    }

    private void ResetSelections()
    {
        SelectedStatus = null;
        IsAnimate = false;
        IsBeneficiallyInseparable = false;
        ActivatesOnSpecifiedCondition = false;
        ActivationCondition = string.Empty;
        CanActivateOutOfCharacter = false;
        DoesNotBlowUpOnFirstDeath = false;
        DoesNotBlowUpOnDeath = false;
        AdditionalBlowUpDateSteps = 0;
        IsUseOnceEver = false;
        IsClosedGuildAllMembersPersonalised = false;
        IsGuildAnyMemberPersonalised = false;
        IsGuildAllMembersPersonalised = false;
        CanBeUsedByChosenPersonForFiveMinutes = false;
        CanBeUsedByAlignmentOrBracket = false;
        CallsToHandPerDay = 0;
        GrantsUtiliseSpecificType = false;
    }

    private static string NormalizeChipLabel(string? value)
    {
        var token = (value ?? string.Empty).Trim();
        if (token.Length == 0)
            return string.Empty;

        var firstSpace = token.IndexOf(' ');
        if (firstSpace > 0 && firstSpace < token.Length - 1)
            return token[(firstSpace + 1)..].Trim();

        return token;
    }

    private static int ResolveStatusCost(string statusLabel)
    {
        if (statusLabel.Equals("Apprentice status", StringComparison.OrdinalIgnoreCase))
            return ApprenticeStatusCost;

        if (statusLabel.Equals("Journeyman status", StringComparison.OrdinalIgnoreCase))
            return JourneymanStatusCost;

        if (statusLabel.Equals("Master status", StringComparison.OrdinalIgnoreCase))
            return MasterStatusCost;

        return 0;
    }

    private int ResolveGuildMultiplier(Dictionary<string, object?> details, List<string> summaryBits)
    {
        if (IsGuildAllMembersPersonalised)
        {
            details["guildAllMembersBenefit"] = true;
            details["guildMultiplier"] = GuildAllMembersMultiplier;
            summaryBits.Add($"Guild all members benefit x{GuildAllMembersMultiplier}");
            return GuildAllMembersMultiplier;
        }

        if (IsClosedGuildAllMembersPersonalised)
        {
            details["closedGuildAllMembers"] = true;
            details["guildMultiplier"] = ClosedGuildAllMembersMultiplier;
            summaryBits.Add($"Closed guild all members benefit x{ClosedGuildAllMembersMultiplier}");
            return ClosedGuildAllMembersMultiplier;
        }

        if (IsGuildAnyMemberPersonalised)
        {
            details["guildAnyMemberUse"] = true;
            details["guildMultiplier"] = GuildAnyMemberMultiplier;
            summaryBits.Add($"Guild any member use x{GuildAnyMemberMultiplier}");
            return GuildAnyMemberMultiplier;
        }

        return 1;
    }

    private static int CalculatePercentCost(int amount, double rate)
    {
        if (amount <= 0 || rate <= 0)
            return 0;

        return (int)Math.Ceiling(amount * rate);
    }

    private static int ApplyMultiplier(int amount, double multiplier)
    {
        if (amount <= 0)
            return 0;

        if (Math.Abs(multiplier - 1d) < 0.0001d)
            return amount;

        return (int)Math.Round(amount * multiplier, MidpointRounding.AwayFromZero);
    }

    private static string FormatSigned(int value)
        => value >= 0 ? $"+{value}" : value.ToString();

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        => base.OnPropertyChanged(propertyName);
}
