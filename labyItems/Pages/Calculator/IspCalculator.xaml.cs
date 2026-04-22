using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Configs;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;

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

    public string BreakdownChevronGlyph => IsBreakdownExpanded ? "\uf077" : "\uf078";

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

    public async Task BeginAddComponentAsync(IspComponentKind kind)
    {
        switch (kind)
        {
            case IspComponentKind.Shield:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    "Shield",
                    new ShieldConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Armour:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    "Armour",
                    new ArmourConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Weapon:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    "Weapon",
                    new WeaponConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Miracle:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    "Miracle",
                    new MiracleConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Spell:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    "Spell",
                    new SpellConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Evocation:
                await OpenConfigComponentAsync(CreateConfigComponent(
                    "Evocation",
                    new EvocationConfigPage(),
                    page => page.ResetConfig()));
                break;
            case IspComponentKind.Life:
                await OpenLifeComponentAsync();
                break;
            case IspComponentKind.Utility:
                await OpenUtilityComponentAsync();
                break;
            case IspComponentKind.Neuronic:
                await DisplayAlert("Neuronic", "Neuronic item configuration is not wired yet.", "OK");
                break;
        }
    }

    private async Task OpenConfigComponentAsync(ComponentCardVm component)
    {
        var result = await component.OpenEditorAsync();
        if (result == null)
            return;

        component.Update(result);
        UpsertComponent(component);
    }

    private async Task OpenLifeComponentAsync()
    {
        var id = Guid.NewGuid().ToString("N");
        var page = new CalcNav.LifeConfigPage
        {
            BindingContext = this
        };
        page.ReturnToFormCommand = new Command(async () => await Navigation.PopAsync());

        var component = new ComponentCardVm(
            id,
            "Life",
            new CalcResult
            {
                AbilityType = "Life",
                AbilityName = "Life",
                Summary = "Life not configured yet.",
                TotalIsp = 0
            },
            async () =>
            {
                await Navigation.PushAsync(page);
                return null;
            });

        page.ContributionAdded += contribution =>
        {
            component.Update(contribution.Result);
            UpsertComponent(component);
        };

        await component.OpenEditorAsync();
    }

    private async Task OpenUtilityComponentAsync()
    {
        var id = Guid.NewGuid().ToString("N");
        var page = new CalcNav.MoreNav
        {
            CalculatorContext = this
        };
        page.ReturnToFormCommand = new Command(async () => await Navigation.PopAsync());

        var component = new ComponentCardVm(
            id,
            "Utility",
            new CalcResult
            {
                AbilityType = "More",
                AbilityName = "Utility",
                Summary = "Utility not configured yet.",
                TotalIsp = 0
            },
            async () =>
            {
                await Navigation.PushAsync(page);
                return null;
            });

        page.ContributionAdded += contribution =>
        {
            component.Update(contribution.Result);
            UpsertComponent(component);
        };

        await component.OpenEditorAsync();
    }

    private ComponentCardVm CreateConfigComponent<TConfig>(
        string title,
        ConfigPageBase<TConfig> page,
        Action<ConfigPageBase<TConfig>> resetAction)
        where TConfig : ConfigBase, new()
    {
        var id = Guid.NewGuid().ToString("N");
        page.CalculatorContext = this;
        page.ApplyBaseTotal(GetTotalExcludingContribution(id));

        return new ComponentCardVm(
            id,
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

            ComponentCards.Add(new ComponentCardVm(
                Guid.NewGuid().ToString("N"),
                ability.AbilityType,
                ability,
                () => Task.FromResult<CalcResult?>(null)));
        }
    }

    private void UpsertComponent(ComponentCardVm component)
    {
        var existing = ComponentCards.FirstOrDefault(item => string.Equals(item.Id, component.Id, StringComparison.Ordinal));
        if (existing != null)
            ComponentCards.Remove(existing);

        ComponentCards.Add(component);
        RefreshCollections();
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
        var abilities = ComponentCards.Select(component => component.Result).ToList();
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

    private async Task EmailItemAsync(IspCalculationResult result)
    {
        try
        {
            var item = BuildWalletItem(result);
            var payload = ItemEmailService.BuildItemPayload(item, result.Abilities);
            item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);
            var draft = ItemEmailService.BuildItemEmailDraft(item, payload);
            await Launcher.OpenAsync(draft.MailtoUri);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Email failed", ex.Message, "OK");
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
        => await EmailItemAsync(BuildCalculationResult());

    private async void OnEditComponentClicked(object sender, EventArgs e)
    {
        var component = ResolveComponentFromSender(sender);
        if (component == null)
            return;

        var result = await component.OpenEditorAsync();
        if (result != null)
        {
            component.Update(result);
            UpsertComponent(component);
            return;
        }

        RefreshCollections();
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
            string title,
            CalcResult result,
            Func<Task<CalcResult?>> openEditorAsync,
            Action? resetAction = null)
        {
            Id = id;
            Title = title;
            _result = result;
            OpenEditorAsync = openEditorAsync;
            ResetAction = resetAction;
        }

        public string Id { get; }
        public string Title { get; }
        public CalcResult Result => _result;
        public Func<Task<CalcResult?>> OpenEditorAsync { get; }
        public Action? ResetAction { get; }
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppliedConfigurationText)));
        }

        public void Reset()
            => ResetAction?.Invoke();
    }
}
