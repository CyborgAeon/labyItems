using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Pages;
using labyItems.Pages.Configs;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

public partial class IspCalculator : ContentPage
{
    private TaskCompletionSource<IspCalculationResult?>? _tcsCalc;
    private readonly Func<IspCalculationResult, Task>? _onSave;
    private int _baseIsp;
    private string _itemName = string.Empty;
    private string _physicalRepresentation = string.Empty;
    private bool _isBreakdownExpanded;
    private int _total;

    public event Action<int>? TotalChanged;

    partial void ApplyPlatformTabFontSize(double fontSize);

    public ICommand? ReturnToFormCommand { get; set; }
    public ICommand RemoveContributionCommand { get; }
    public ObservableCollection<ContributionRow> BreakdownItems { get; } = new();
    public ObservableCollection<ComponentCardVm> ComponentCards { get; } = new();
    public int BaseTotal => _baseIsp;
    public string SelectedBodyComponentTitle =>
        ComponentCards.FirstOrDefault(component => IsBodyComponentKind(component.Kind))?.Title ?? string.Empty;

    public string ItemName
    {
        get => _itemName;
        set
        {
            if (_itemName == value)
                return;

            _itemName = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string PhysicalRepresentation
    {
        get => _physicalRepresentation;
        set
        {
            if (_physicalRepresentation == value)
                return;

            _physicalRepresentation = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public bool IsBreakdownExpanded
    {
        get => _isBreakdownExpanded;
        set
        {
            if (_isBreakdownExpanded == value)
                return;

            _isBreakdownExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BreakdownChevronGlyph));
        }
    }

    public string BreakdownChevronGlyph => FontAwesomeGlyphs.Chevron;

    public int Total
    {
        get => _total;
        private set
        {
            if (_total == value)
                return;

            _total = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedTotal));
        }
    }

    public string FormattedTotal => $"Total ISP: {Total}";

    public IspCalculator(
        int baseTotal,
        IEnumerable<CalcResult>? existingAbilities = null,
        Func<IspCalculationResult, Task>? onSave = null)
    {
        InitializeComponent();
        BindingContext = this;

        _baseIsp = baseTotal;
        _onSave = onSave;
        Total = baseTotal;

        ReturnToFormCommand = new Command(async () => await SaveOrReturnAsync());
        RemoveContributionCommand = new Command<string>(RemoveContributionById);

        if (existingAbilities != null)
            SeedExisting(existingAbilities);

        RefreshCollections();
    }

    public async Task<IspCalculationResult?> GetResultAsync(INavigation nav)
    {
        _tcsCalc = new TaskCompletionSource<IspCalculationResult?>();
        await nav.PushAsync(this);
        return await _tcsCalc.Task;
    }

    public int GetTotalExcludingContribution(string? contributionId)
    {
        var targetId = contributionId ?? string.Empty;
        return Math.Max(
            0,
            _baseIsp + ComponentCards
                .Where(component => !string.Equals(component.Id, targetId, StringComparison.Ordinal))
                .Sum(component => component.Result.TotalIsp));
    }

    public bool HasComponentKind(IspComponentKind kind)
        => FindComponentByKind(kind) != null;

    public bool IsComponentKindAvailable(IspComponentKind kind)
    {
        if (!IsBodyComponentKind(kind))
            return true;

        var selectedBodyComponent = ComponentCards.FirstOrDefault(component => IsBodyComponentKind(component.Kind));
        return selectedBodyComponent == null || selectedBodyComponent.Kind == kind;
    }

    private ComponentCardVm? FindComponentByKind(IspComponentKind kind)
        => ComponentCards.FirstOrDefault(component => component.Kind == kind);

    private static bool IsBodyComponentKind(IspComponentKind kind)
        => kind is IspComponentKind.Shield or IspComponentKind.Armour or IspComponentKind.Weapon;

    private static string BuildComponentId(IspComponentKind kind)
        => kind switch
        {
            IspComponentKind.Utility => "more",
            _ => kind.ToString().ToLowerInvariant()
        };

    private static IspComponentKind? ResolveComponentKind(string? abilityType)
    {
        var type = (abilityType ?? string.Empty).Trim();
        if (type.Equals("Shield", StringComparison.OrdinalIgnoreCase))
            return IspComponentKind.Shield;
        if (type.Equals("Armour", StringComparison.OrdinalIgnoreCase))
            return IspComponentKind.Armour;
        if (type.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
            return IspComponentKind.Weapon;
        if (type.Equals("Miracle", StringComparison.OrdinalIgnoreCase))
            return IspComponentKind.Miracle;
        if (type.Equals("Spell", StringComparison.OrdinalIgnoreCase))
            return IspComponentKind.Spell;
        if (type.Equals("Evocation", StringComparison.OrdinalIgnoreCase))
            return IspComponentKind.Evocation;
        if (type.Equals("General", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Abilities", StringComparison.OrdinalIgnoreCase))
        {
            return IspComponentKind.Abilities;
        }
        if (type.Equals("Life", StringComparison.OrdinalIgnoreCase))
            return IspComponentKind.Life;
        if (type.Equals("More", StringComparison.OrdinalIgnoreCase)
            || type.Equals("Utility", StringComparison.OrdinalIgnoreCase))
        {
            return IspComponentKind.Utility;
        }
        if (type.Equals("Neuronic", StringComparison.OrdinalIgnoreCase))
            return IspComponentKind.Neuronic;

        return null;
    }

    public async Task BeginAddComponentAsync(IspComponentKind kind)
    {
        if (!IsComponentKindAvailable(kind))
        {
            await DisplayAlert(
                "Component unavailable",
                $"Remove {SelectedBodyComponentTitle.ToLowerInvariant()} before selecting another body component.",
                "OK");
            return;
        }

        var existing = FindComponentByKind(kind);
        if (existing != null)
        {
            await OpenExistingComponentAsync(existing);
            return;
        }

        switch (kind)
        {
            case IspComponentKind.Shield:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    IspComponentKind.Shield,
                    "Shield",
                    new ShieldConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Armour:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    IspComponentKind.Armour,
                    "Armour",
                    new ArmourConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Weapon:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    IspComponentKind.Weapon,
                    "Weapon",
                    new WeaponConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Miracle:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    IspComponentKind.Miracle,
                    "Miracle",
                    new MiracleConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Spell:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    IspComponentKind.Spell,
                    "Spell",
                    new SpellConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Evocation:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    IspComponentKind.Evocation,
                    "Evocation",
                    new EvocationConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Abilities:
                var abilitiesPage = new GeneralConfigPage();
                await OpenConfigComponentAsync(CreateConfigComponent(
                    IspComponentKind.Abilities,
                    "Abilities",
                    abilitiesPage,
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Life:
                await OpenLifeComponentAsync();
                break;
            case IspComponentKind.Utility:
                await OpenUtilityComponentAsync();
                break;
            case IspComponentKind.Neuronic:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    IspComponentKind.Neuronic,
                    "Neuronic",
                    new NeuronicConfigPage(),
                    page => page.ResetConfig()));
                break;
        }
    }

    private async Task OpenConfigComponentAsync(ComponentCardVm component)
    {
        var result = await component.OpenEditorAsync();
        if (result == null || !IsConfiguredResult(result))
            return;

        component.Update(result);
        UpsertComponent(component);
    }

    private async Task OpenExistingComponentAsync(ComponentCardVm component)
    {
        var result = await component.OpenEditorAsync();
        if (result != null)
        {
            if (!IsConfiguredResult(result))
            {
                RemoveComponentMatches(component);
                RefreshCollections();
                return;
            }

            component.Update(result);
            UpsertComponent(component);
            return;
        }

        RefreshCollections();
    }

    private async Task OpenLifeComponentAsync()
    {
        var id = BuildComponentId(IspComponentKind.Life);
        var page = new CalcNav.LifeConfigPage
        {
            BindingContext = this
        };

        ComponentCardVm? component = null;
        component = new ComponentCardVm(
            id,
            IspComponentKind.Life,
            "Life",
            new CalcResult
            {
                AbilityType = "Life",
                AbilityName = "Life",
                Summary = "Life not configured yet.",
                TotalIsp = 0
            },
            async () => await OpenLiveContributionEditorAsync(component!, page));

        page.ContributionAdded += contribution =>
        {
            component!.Update(contribution.Result);
            UpsertComponent(component);
        };

        await component.OpenEditorAsync();
    }

    private async Task OpenUtilityComponentAsync()
    {
        var id = BuildComponentId(IspComponentKind.Utility);
        var page = new CalcNav.MoreNav
        {
            CalculatorContext = this
        };

        ComponentCardVm? component = null;
        component = new ComponentCardVm(
            id,
            IspComponentKind.Utility,
            "Utility",
            new CalcResult
            {
                AbilityType = "More",
                AbilityName = "Utility",
                Summary = "Utility not configured yet.",
                TotalIsp = 0
            },
            async () => await OpenLiveContributionEditorAsync(component!, page));

        page.ContributionAdded += contribution =>
        {
            component!.Update(contribution.Result);
            UpsertComponent(component);
        };

        await component.OpenEditorAsync();
    }

    private async Task<CalcResult?> OpenLiveContributionEditorAsync(ComponentCardVm component, Page page)
    {
        var originalResult = FindComponentByKind(component.Kind)?.Result;
        var committed = false;

        switch (page)
        {
            case CalcNav.LifeConfigPage lifePage:
                lifePage.ReturnToFormCommand = new Command(async () =>
                {
                    committed = true;
                    await Navigation.PopAsync();
                });
                break;
            case CalcNav.MoreNav morePage:
                morePage.ReturnToFormCommand = new Command(async () =>
                {
                    committed = true;
                    await Navigation.PopAsync();
                });
                break;
        }

        await Navigation.PushAsync(page);

        if (committed)
        {
            if (IsConfiguredResult(component.Result))
                UpsertComponent(component);
            else
                RemoveComponentMatches(component);

            RefreshCollections();
            return null;
        }

        RemoveComponentMatches(component);
        if (originalResult != null && IsConfiguredResult(originalResult))
        {
            component.Update(originalResult);
            ComponentCards.Add(component);
        }

        RefreshCollections();
        return null;
    }

    private ComponentCardVm CreateConfigComponent<TConfig>(
        IspComponentKind kind,
        string title,
        ConfigPageBase<TConfig> page,
        Action<ConfigPageBase<TConfig>> resetAction)
        where TConfig : ConfigBase, new()
    {
        var id = BuildComponentId(kind);
        page.CalculatorContext = this;
        page.ApplyBaseTotal(GetTotalExcludingContribution(id));

        return new ComponentCardVm(
            id,
            kind,
            title,
            new CalcResult
            {
                AbilityType = title,
                AbilityName = title,
                Summary = $"{title} not configured yet.",
                TotalIsp = 0
            },
            async () =>
            {
                page.CalculatorContext = this;
                page.ApplyBaseTotal(GetTotalExcludingContribution(id));
                var completion = page.Completion;
                await Navigation.PushAsync(page);
                return await completion;
            },
            () => resetAction(page));
    }

    private void SeedExisting(IEnumerable<CalcResult> abilities)
    {
        foreach (var ability in abilities)
        {
            if (string.Equals(ability.AbilityType, "Base", StringComparison.OrdinalIgnoreCase))
            {
                _baseIsp = ability.TotalIsp;
                continue;
            }

            var kind = ResolveComponentKind(ability.AbilityType);
            if (kind.HasValue && FindComponentByKind(kind.Value) != null)
                continue;

            ComponentCards.Add(new ComponentCardVm(
                kind.HasValue ? BuildComponentId(kind.Value) : Guid.NewGuid().ToString("N"),
                kind ?? IspComponentKind.Utility,
                ability.AbilityType,
                ability,
                () => Task.FromResult<CalcResult?>(null)));
        }
    }

    private void UpsertComponent(ComponentCardVm component)
    {
        if (!IsConfiguredResult(component.Result))
        {
            RemoveComponentMatches(component);
            RefreshCollections();
            return;
        }

        RemoveComponentMatches(component);

        ComponentCards.Add(component);
        RefreshCollections();
    }

    private void RemoveComponentMatches(ComponentCardVm component)
    {
        var matches = ComponentCards
            .Where(item =>
                string.Equals(item.Id, component.Id, StringComparison.Ordinal)
                || item.Kind == component.Kind)
            .ToList();

        foreach (var match in matches)
            ComponentCards.Remove(match);
    }

    private static bool IsConfiguredResult(CalcResult? result)
    {
        if (result == null)
            return false;

        if (result.TotalIsp != 0)
            return true;

        return NotationHelper.BuildHumanReadableAbilityLines(new[] { result }).Count > 0;
    }

    private void RemoveContributionById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var existing = ComponentCards.FirstOrDefault(component => string.Equals(component.Id, id, StringComparison.Ordinal));
        if (existing == null)
            return;

        existing.Reset();
        ComponentCards.Remove(existing);
        RefreshCollections();
    }

    private void RefreshCollections()
    {
        BreakdownItems.Clear();

        var running = 0;
        if (_baseIsp > 0)
        {
            running += _baseIsp;
            BreakdownItems.Add(new ContributionRow
            {
                Id = "base",
                Text = $"Base ISP: {_baseIsp}",
                RunningTotal = running
            });
        }

        foreach (var component in ComponentCards)
        {
            running += component.Result.TotalIsp;
            BreakdownItems.Add(new ContributionRow
            {
                Id = component.Id,
                Text = component.Result.Summary,
                RunningTotal = running
            });
        }

        Total = Math.Max(0, running);
        TotalChanged?.Invoke(Total);
    }

    private IspCalculationResult BuildCalculationResult()
    {
        var abilities = ComponentCards
            .Select(component => component.Result)
            .Where(IsConfiguredResult)
            .ToList();
        if (_baseIsp > 0)
        {
            abilities.Insert(0, new CalcResult
            {
                AbilityType = "Base",
                AbilityName = "Manual ISP entry",
                TotalIsp = _baseIsp,
                Details = new() { ["source"] = "ItemForm" }
            });
        }

        return new IspCalculationResult
        {
            TotalIsp = Total,
            Abilities = abilities,
            SummaryText = string.Join("\n", BreakdownItems.Select(row => row.Text))
        };
    }

    private async Task SaveOrReturnAsync()
    {
        var result = BuildCalculationResult();

        if (_tcsCalc != null)
        {
            _tcsCalc.TrySetResult(result);
            await Navigation.PopAsync();
            return;
        }

        if (_onSave != null)
        {
            await _onSave(result);
            await Navigation.PopAsync();
            return;
        }

        await SaveItemToWalletAsync(result);
    }

    private async Task SaveItemToWalletAsync(IspCalculationResult result)
    {
        try
        {
            var item = BuildWalletItem(result);
            LiteDbService.InsertItem(item);
            await DisplayAlert("Saved", "Item saved to wallet.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private Item BuildWalletItem(IspCalculationResult result)
    {
        var descriptionLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(ItemName))
            descriptionLines.Add($"Item: {ItemName.Trim()}");
        if (!string.IsNullOrWhiteSpace(PhysicalRepresentation))
            descriptionLines.Add($"Phys rep: {PhysicalRepresentation.Trim()}");
        descriptionLines.Add($"ISP total: {result.TotalIsp}");
        if (BreakdownItems.Count > 0)
        {
            descriptionLines.Add("ISP breakdown:");
            descriptionLines.AddRange(BreakdownItems.Select(row => row.Text));
        }

        var item = new Item
        {
            ItemType = ResolveWalletItemType(result.Abilities),
            Maker = new Character
            {
                Name = "Quick ISP",
                PlayerName = string.Empty
            },
            Description = string.Join("\n", descriptionLines),
            Isp = result.TotalIsp,
            CreatedDate = DateTime.Now
        };

        var payload = ItemEmailService.BuildItemPayload(item, result.Abilities);
        item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);
        return item;
    }

    private static ItemTypeEnum ResolveWalletItemType(IEnumerable<CalcResult>? abilities)
    {
        if (abilities == null)
            return ItemTypeEnum.None;

        if (abilities.Any(a => string.Equals(a.AbilityType, "Spell", StringComparison.OrdinalIgnoreCase)))
            return ItemTypeEnum.Magic;

        if (abilities.Any(a => string.Equals(a.AbilityType, "Miracle", StringComparison.OrdinalIgnoreCase)))
            return ItemTypeEnum.Spirit;

        if (abilities.Any(a => string.Equals(a.AbilityType, "Evocation", StringComparison.OrdinalIgnoreCase)))
            return ItemTypeEnum.EarthPower;

        if (abilities.Any(a => string.Equals(a.AbilityType, "Neuronic", StringComparison.OrdinalIgnoreCase)))
            return ItemTypeEnum.Neuronic;

        return ItemTypeEnum.Other;
    }

    private async void OnAddComponentClicked(object sender, EventArgs e)
        => await Navigation.PushAsync(new IspComponentTypePage(this));

    private void OnToggleBreakdownClicked(object sender, EventArgs e)
        => IsBreakdownExpanded = !IsBreakdownExpanded;

    private async void OnSaveItemClicked(object sender, EventArgs e)
        => await SaveOrReturnAsync();

    private async void OnEmailDeskClicked(object sender, EventArgs e)
    {
        var result = BuildCalculationResult();
        var payload = BuildDeskSubmissionPayload(result);
        var recipient = new RecipientInfo
        {
            ItemName = payload.ItemName
        };

        await Navigation.PushAsync(new RecipientPage(recipient, payload));
    }

    private MpSubmissionPayload BuildDeskSubmissionPayload(IspCalculationResult result)
    {
        var abilities = (result.Abilities ?? new List<CalcResult>())
            .Where(ability => ability != null)
            .ToList();

        var payload = new MpSubmissionPayload
        {
            ItemName = (ItemName ?? string.Empty).Trim(),
            SourceFlow = "isp",
            PhysicalRepresentation = (PhysicalRepresentation ?? string.Empty).Trim(),
            TotalIsp = Math.Max(0, result.TotalIsp),
            TotalMp = 0,
            Abilities = abilities,
            IspBreakdown = BreakdownItems.ToList()
        };
        payload.ItemTypes = ItemEmailService.DeriveMpItemTypes(payload);
        return payload;
    }

    private async void OnEditComponentClicked(object sender, EventArgs e)
    {
        var component = ResolveComponentFromSender(sender);
        if (component == null)
            return;

        await OpenExistingComponentAsync(component);
    }

    private void OnDeleteComponentClicked(object sender, EventArgs e)
    {
        var component = ResolveComponentFromSender(sender);
        if (component == null)
            return;

        RemoveContributionById(component.Id);
    }

    private static ComponentCardVm? ResolveComponentFromSender(object sender)
    {
        if (sender is not BindableObject bindable)
            return null;

        return (bindable as Button)?.CommandParameter as ComponentCardVm
            ?? bindable.BindingContext as ComponentCardVm;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => base.OnPropertyChanged(propertyName);

    public sealed class ComponentCardVm : INotifyPropertyChanged
    {
        private CalcResult _result;

        public ComponentCardVm(
            string id,
            IspComponentKind kind,
            string title,
            CalcResult result,
            Func<Task<CalcResult?>> openEditorAsync,
            Action? resetAction = null)
        {
            Id = id;
            Kind = kind;
            Title = title;
            _result = result;
            OpenEditorAsync = openEditorAsync;
            ResetAction = resetAction;
        }

        public string Id { get; }
        public IspComponentKind Kind { get; }
        public string Title { get; }
        public CalcResult Result => _result;
        public Func<Task<CalcResult?>> OpenEditorAsync { get; }
        public Action? ResetAction { get; }
        public string DisplayTitle => Title;
        public string GrantText
        {
            get
            {
                var grantText = NotationHelper.BuildGrantSummary(new[] { _result });
                if (!string.IsNullOrWhiteSpace(grantText))
                    return grantText;

                return "No choices selected";
            }
        }
        public string Summary => (_result.Summary ?? string.Empty).Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
        public string TotalText => $"ISP: {_result.TotalIsp}";
        public string AppliedConfigurationText => string.IsNullOrWhiteSpace(Summary)
            ? TotalText
            : Summary;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Update(CalcResult result)
        {
            _result = result;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Result)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GrantText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppliedConfigurationText)));
        }

        public void Reset()
            => ResetAction?.Invoke();
    }
}
