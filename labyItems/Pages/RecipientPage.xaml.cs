using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Calculator;
using labyItems.Services;
using Microsoft.Maui.ApplicationModel;
using System.Text;

namespace labyItems.Pages;

public partial class RecipientPage : ContentPage
{
    private readonly TaskCompletionSource<RecipientInfo> _tcs = new();
    private readonly MpSubmissionPayload? _submission;
    private bool _hasSubmissionSummary;
    private string _summaryEmptyText = StickyFooterContentBuilder.DefaultEmptyMessage;

    public ObservableCollection<ContributionRow> SubmissionSummaryRows { get; } = new();
    public bool HasSubmissionSummary
    {
        get => _hasSubmissionSummary;
        private set
        {
            if (_hasSubmissionSummary == value)
                return;

            _hasSubmissionSummary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSummaryEmptyStateVisible));
        }
    }

    public bool HasSubmissionSummaryRows => SubmissionSummaryRows.Count > 0;
    public bool IsSummaryEmptyStateVisible => HasSubmissionSummary && !HasSubmissionSummaryRows;
    public string SummaryEmptyText => _summaryEmptyText;
    public bool RequiresItemName => _submission != null;
    public string BreakdownTitle => ResolveSubmissionSourceFlow(_submission) == "isp"
        ? "ISP Item Breakdown"
        : "MP Item Breakdown";

    public RecipientPage(RecipientInfo? existing = null, MpSubmissionPayload? submission = null)
    {
        InitializeComponent();
        _submission = submission;
        SubmissionSummaryRows.CollectionChanged += OnSummaryRowsChanged;
        BuildSummaryRows(submission);
        if (existing != null)
        {
            ItemNameEntry.Text = existing.ItemName;
            RecipientPlayerNameEntry.Text = existing.PlayerName;
            RecipientCharacterNameEntry.Text = existing.CharacterName;
            RecipientCharacterClassEntry.Text = existing.CharacterClass;
        }
        else if (!string.IsNullOrWhiteSpace(submission?.ItemName))
        {
            ItemNameEntry.Text = submission.ItemName.Trim();
        }
        BindingContext = this;
    }

    // Wait for result from parent
    public Task<RecipientInfo> GetRecipientAsync() => _tcs.Task;

    private async void OnSubmit(object sender, EventArgs e)
    {
        var itemName = NormalizeItemName(ItemNameEntry.Text);
        if (RequiresItemName && !TryValidateItemName(itemName, out var validationMessage))
        {
            await DisplayAlert("Item name", validationMessage, "OK");
            return;
        }

        var result = new RecipientInfo
        {
            ItemName = itemName,
            PlayerName = RecipientPlayerNameEntry.Text?.Trim() ?? string.Empty,
            CharacterName = RecipientCharacterNameEntry.Text?.Trim() ?? string.Empty,
            CharacterClass = RecipientCharacterClassEntry.Text?.Trim() ?? string.Empty
        };

        _tcs.TrySetResult(result);

        if (_submission != null)
        {
            try
            {
                SaveSubmissionToWallet(_submission, itemName, result);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save failed", ex.Message, "OK");
                return;
            }

            await SendSubmissionEmailAsync(result, _submission);
            await Navigation.PopToRootAsync();
            return;
        }

        await Navigation.PopAsync();
    }

    private static bool TryValidateItemName(string itemName, out string message)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            message = "Enter an item name.";
            return false;
        }

        if (itemName.Length > 30)
        {
            message = "Item name must be 30 characters or fewer.";
            return false;
        }

        if (!itemName.All(character => char.IsLetter(character) || character == ' '))
        {
            message = "Item name must contain letters and spaces only.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string NormalizeItemName(string? itemName)
        => string.Join(' ', (itemName ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_submission == null)
        {
            await Navigation.PopAsync();
            return;
        }

        try
        {
            var itemName = NormalizeItemName(ItemNameEntry.Text);
            if (RequiresItemName && !TryValidateItemName(itemName, out var validationMessage))
            {
                await DisplayAlert("Item name", validationMessage, "OK");
                return;
            }

            SaveSubmissionToWallet(_submission, itemName, new RecipientInfo
            {
                ItemName = itemName,
                PlayerName = RecipientPlayerNameEntry.Text?.Trim() ?? string.Empty,
                CharacterName = RecipientCharacterNameEntry.Text?.Trim() ?? string.Empty,
                CharacterClass = RecipientCharacterClassEntry.Text?.Trim() ?? string.Empty
            });

            await DisplayAlert("Saved", $"{ResolveSubmissionSourceLabel(_submission)} item saved to wallet.", "OK");
            await Navigation.PopToRootAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private static void SaveSubmissionToWallet(MpSubmissionPayload payload, string? itemName, RecipientInfo? recipient)
    {
        var item = BuildWalletItem(payload, itemName, recipient);
        if (!LiteDbService.UpdateItem(item))
            LiteDbService.InsertItem(item);
    }

    private async Task SendSubmissionEmailAsync(RecipientInfo recipient, MpSubmissionPayload payload)
    {
        var emailDraft = ItemEmailService.BuildDeskSubmissionEmailDraft(recipient, payload);

        try
        {
            await Launcher.OpenAsync(emailDraft.MailtoUri);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not open mail client: {ex.Message}", "OK");
        }
    }

    private static Item BuildWalletItem(MpSubmissionPayload payload, string? itemName = null, RecipientInfo? recipient = null)
    {
        var displayName = (itemName ?? payload.ItemName ?? string.Empty).Trim();
        if (displayName.Length == 0)
            displayName = ResolveSubmissionSourceFlow(payload) == "isp"
                ? $"ISP Item ({payload.TotalIsp} ISP)"
                : $"MP Item ({payload.TotalMp} MP)";

        var itemTypes = ItemEmailService.DeriveMpItemTypes(payload);
        var sourceLabel = ResolveSubmissionSourceLabel(payload);
        var descriptionBuilder = new StringBuilder()
            .AppendLine($"Name: {displayName}")
            .AppendLine($"Source: {sourceLabel} item");
        if (payload.TotalMp > 0)
            descriptionBuilder.AppendLine($"MP cost: {payload.TotalMp}");
        if (!string.IsNullOrWhiteSpace(payload.PhysicalRepresentation))
            descriptionBuilder.AppendLine($"Phys rep: {payload.PhysicalRepresentation.Trim()}");

        descriptionBuilder
            .AppendLine($"ISP total: {payload.TotalIsp}")
            .AppendLine();

        if (payload.IspBreakdown?.Count > 0)
        {
            descriptionBuilder
                .AppendLine("ISP breakdown:")
                .AppendLine(string.Join(Environment.NewLine, payload.IspBreakdown.Select(row => row.Text)));
        }

        if (payload.Breakdown?.Count > 0)
        {
            descriptionBuilder
                .AppendLine()
                .AppendLine("MP breakdown:")
                .AppendLine(string.Join(Environment.NewLine, payload.Breakdown.Select(row => row.Text)));
        }

        var description = descriptionBuilder.ToString().Trim();

        var item = new Item
        {
            ItemType = ResolvePrimaryItemType(itemTypes),
            Description = description,
            Isp = Math.Max(0, payload.TotalIsp),
            RecipientPlayerName = recipient?.PlayerName ?? string.Empty,
            RecipientCharacterName = recipient?.CharacterName ?? string.Empty,
            RecipientCharacterClass = recipient?.CharacterClass ?? string.Empty,
            CreatedDate = DateTime.Now,
            PayloadJson = ItemEmailService.SerializeItemPayload(new ItemJsonPayload
            {
                Description = description,
                Isp = Math.Max(0, payload.TotalIsp),
                CreatedDate = DateTime.Now,
                WitnessName = string.Empty,
                Recipient = recipient == null
                    ? null
                    : new RecipientPayload
                    {
                        PlayerName = recipient.PlayerName ?? string.Empty,
                        CharacterName = recipient.CharacterName ?? string.Empty,
                        CharacterClass = recipient.CharacterClass ?? string.Empty
                    },
                Item = new ItemJsonDetail
                {
                    DisplayName = displayName,
                    SourceFlow = ResolveSubmissionSourceFlow(payload),
                    MonsterPointCost = Math.Max(0, payload.TotalMp),
                    PhysicalRepresentation = payload.PhysicalRepresentation ?? string.Empty,
                    Types = itemTypes,
                    Abilities = payload.Abilities ?? new List<CalcResult>(),
                    Status = "saved",
                    Modifiers = new List<object>()
                }
            })
        };

        return item;
    }

    private static ItemTypeEnum ResolvePrimaryItemType(IReadOnlyList<string> itemTypes)
    {
        var primary = itemTypes.FirstOrDefault() ?? "physical";
        return primary.Trim().ToLowerInvariant() switch
        {
            "earthpower" => ItemTypeEnum.EarthPower,
            "magical" or "magic" => ItemTypeEnum.Magic,
            "spiritual" or "spirit" => ItemTypeEnum.Spirit,
            "neuronic" => ItemTypeEnum.Neuronic,
            "mantic" => ItemTypeEnum.Other,
            "physical" => ItemTypeEnum.Physical,
            _ => ItemTypeEnum.Physical
        };
    }

    private void BuildSummaryRows(MpSubmissionPayload? payload)
    {
        SubmissionSummaryRows.Clear();
        HasSubmissionSummary = payload != null;

        if (payload == null)
        {
            _summaryEmptyText = StickyFooterContentBuilder.DefaultEmptyMessage;
            OnPropertyChanged(nameof(SummaryEmptyText));
            return;
        }

        var sourceRows = new List<ContributionRow>();

        if (payload.Breakdown?.Count > 0)
            sourceRows.AddRange(payload.Breakdown);

        if (payload.IspBreakdown?.Count > 0)
        {
            sourceRows.Add(new ContributionRow
            {
                Id = "isp-breakdown-header",
                Text = "ISP breakdown",
                RunningTotal = payload.TotalIsp
            });
            sourceRows.AddRange(payload.IspBreakdown);
        }

        if (sourceRows.Count == 0 && (payload.TotalMp > 0 || payload.TotalIsp > 0))
        {
            sourceRows.Add(new ContributionRow
            {
                Id = ResolveSubmissionSourceFlow(payload) == "isp" ? "isp-total-only" : "mp-total-only",
                Text = ResolveSubmissionSourceFlow(payload) == "isp" ? "ISP total" : "MP total",
                RunningTotal = ResolveSubmissionSourceFlow(payload) == "isp" ? payload.TotalIsp : payload.TotalMp
            });
        }

        var content = StickyFooterContentBuilder.Build(sourceRows, StickyFooterContentBuilder.DefaultEmptyMessage);
        foreach (var row in content.Rows)
            SubmissionSummaryRows.Add(row);

        _summaryEmptyText = content.EmptyMessage;
        OnPropertyChanged(nameof(SummaryEmptyText));
        OnPropertyChanged(nameof(HasSubmissionSummaryRows));
        OnPropertyChanged(nameof(IsSummaryEmptyStateVisible));
    }

    private void OnSummaryRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSubmissionSummaryRows));
        OnPropertyChanged(nameof(IsSummaryEmptyStateVisible));
    }

    private static string ResolveSubmissionSourceFlow(MpSubmissionPayload? payload)
    {
        var sourceFlow = (payload?.SourceFlow ?? string.Empty).Trim();
        return sourceFlow.Length == 0 ? "monster-point" : sourceFlow.ToLowerInvariant();
    }

    private static string ResolveSubmissionSourceLabel(MpSubmissionPayload payload)
        => ResolveSubmissionSourceFlow(payload) switch
        {
            "isp" => "ISP",
            "monster-point" => "MP",
            _ => ResolveSubmissionSourceFlow(payload).Replace("-", " ")
        };
}
