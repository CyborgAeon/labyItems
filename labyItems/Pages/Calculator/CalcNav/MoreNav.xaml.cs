using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Models;
using labyItems.Pages.Calculator;

namespace labyItems.Pages.Calculator.CalcNav;

public partial class MoreNav : ContentPage
{
    public event Action<CalcContribution>? ContributionAdded;

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
        propertyChanged: static (bindable, _, __) =>
        {
            if (bindable is MoreNav page)
            {
                page.NotifyPropertyChanged(nameof(CalculatorContext));
            }
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
        set => SetAndPublish(ref _isClosedGuildAllMembersPersonalised, value);
    }

    public bool IsGuildAnyMemberPersonalised
    {
        get => _isGuildAnyMemberPersonalised;
        set => SetAndPublish(ref _isGuildAnyMemberPersonalised, value);
    }

    public bool IsGuildAllMembersPersonalised
    {
        get => _isGuildAllMembersPersonalised;
        set => SetAndPublish(ref _isGuildAllMembersPersonalised, value);
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

    private void PublishContribution()
    {
        var details = new Dictionary<string, object?>();
        var summaryBits = new List<string>();

        var status = NormalizeChipLabel(SelectedStatus);
        if (!string.IsNullOrWhiteSpace(status))
        {
            details["status"] = status;
            summaryBits.Add(status);
        }

        AddBoolean("animate", IsAnimate, "Animate");
        AddBoolean("beneficiallyInseparable", IsBeneficiallyInseparable, "Beneficially inseparable");
        AddBoolean("activateOoc", CanActivateOutOfCharacter, "Activate OOC");
        AddBoolean("noBlowUpFirstDeath", DoesNotBlowUpOnFirstDeath, "No blow-up on first death");
        AddBoolean("noBlowUpDeath", DoesNotBlowUpOnDeath, "No blow-up on death");
        AddBoolean("useOnceEver", IsUseOnceEver, "Use once ever");
        AddBoolean("closedGuildAllMembers", IsClosedGuildAllMembersPersonalised, "Closed guild: all members benefit");
        AddBoolean("guildAnyMemberUse", IsGuildAnyMemberPersonalised, "Guild: any member can use");
        AddBoolean("guildAllMembersBenefit", IsGuildAllMembersPersonalised, "Guild: all members benefit");
        AddBoolean("chosenPersonFiveMinutes", CanBeUsedByChosenPersonForFiveMinutes, "Chosen person can use (5 mins, 1/day)");
        AddBoolean("alignmentOrBracketUse", CanBeUsedByAlignmentOrBracket, "Alignment or bracket use");
        AddBoolean("grantsUtiliseSpecificType", GrantsUtiliseSpecificType, "Grants utilise (specific type)");

        if (ActivatesOnSpecifiedCondition)
        {
            details["activatesOnCondition"] = true;
            var condition = (ActivationCondition ?? string.Empty).Trim();
            if (condition.Length > 0)
            {
                details["activationCondition"] = condition;
                summaryBits.Add($"Condition: {condition}");
            }
            else
            {
                summaryBits.Add("Condition-based activation");
            }
        }

        if (AdditionalBlowUpDateSteps > 0)
        {
            var months = AdditionalBlowUpDateSteps * 3;
            details["additionalBlowUpMonths"] = months;
            summaryBits.Add($"+{months} months blow-up date");
        }

        if (CallsToHandPerDay > 0)
        {
            details["callsToHandPerDay"] = CallsToHandPerDay;
            summaryBits.Add($"Calls to hand {CallsToHandPerDay}/day");
        }

        var summary = summaryBits.Count == 0
            ? "No additional modifiers selected."
            : string.Join(" · ", summaryBits);

        ContributionAdded?.Invoke(new CalcContribution(
            Id: "more",
            Source: "More",
            Result: new CalcResult
            {
                AbilityType = "More",
                AbilityName = "Additional modifiers",
                TotalIsp = 0,
                Details = details,
                Summary = summary
            },
            OnRemove: ResetSelections));

        void AddBoolean(string key, bool enabled, string summaryLabel)
        {
            if (!enabled)
                return;

            details[key] = true;
            summaryBits.Add(summaryLabel);
        }
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

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        => base.OnPropertyChanged(propertyName);
}
