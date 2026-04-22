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

    public RecipientPage(RecipientInfo? existing = null, MpSubmissionPayload? submission = null)
    {
        InitializeComponent();
        _submission = submission;
        SubmissionSummaryRows.CollectionChanged += OnSummaryRowsChanged;
        BuildSummaryRows(submission);
        if (existing != null)
        {
            RecipientPlayerNameEntry.Text = existing.PlayerName;
            RecipientCharacterNameEntry.Text = existing.CharacterName;
            RecipientCharacterClassEntry.Text = existing.CharacterClass;
        }
        BindingContext = this;
    }

    // Wait for result from parent
    public Task<RecipientInfo> GetRecipientAsync() => _tcs.Task;

    private async void OnSubmit(object sender, EventArgs e)
    {
        var result = new RecipientInfo
        {
            PlayerName = RecipientPlayerNameEntry.Text?.Trim() ?? string.Empty,
            CharacterName = RecipientCharacterNameEntry.Text?.Trim() ?? string.Empty,
            CharacterClass = RecipientCharacterClassEntry.Text?.Trim() ?? string.Empty
        };

        _tcs.TrySetResult(result);

        if (_submission != null)
        {
            await SendSubmissionEmailAsync(result, _submission);
            await Navigation.PopToRootAsync();
            return;
        }

        await Navigation.PopAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_submission == null)
        {
            await Navigation.PopAsync();
            return;
        }

        try
        {
            var item = BuildWalletItem(_submission);
            if (!LiteDbService.UpdateItem(item))
                LiteDbService.InsertItem(item);

            await DisplayAlert("Saved", "MP item saved to wallet.", "OK");
            await Navigation.PopToRootAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
    }

    private async Task SendSubmissionEmailAsync(RecipientInfo recipient, MpSubmissionPayload payload)
    {
        var emailDraft = ItemEmailService.BuildMpSubmissionEmailDraft(recipient, payload);

        try
        {
            await Launcher.OpenAsync(emailDraft.MailtoUri);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not open mail client: {ex.Message}", "OK");
        }
    }

    private static Item BuildWalletItem(MpSubmissionPayload payload)
    {
        var description = new StringBuilder()
            .AppendLine($"Source: Monster point item")
            .AppendLine($"MP cost: {payload.TotalMp}")
            .AppendLine($"ISP total: {payload.TotalIsp}")
            .AppendLine()
            .AppendLine("MP breakdown:")
            .AppendLine(string.Join(Environment.NewLine, payload.Breakdown.Select(row => row.Text)))
            .ToString()
            .Trim();

        var item = new Item
        {
            ItemType = ItemTypeEnum.Other,
            Description = description,
            Isp = Math.Max(0, payload.TotalIsp),
            CreatedDate = DateTime.Now,
            PayloadJson = ItemEmailService.SerializeItemPayload(new ItemJsonPayload
            {
                Description = description,
                Isp = Math.Max(0, payload.TotalIsp),
                CreatedDate = DateTime.Now,
                WitnessName = string.Empty,
                Item = new ItemJsonDetail
                {
                    DisplayName = $"MP Item ({payload.TotalMp} MP)",
                    SourceFlow = "monster-point",
                    MonsterPointCost = Math.Max(0, payload.TotalMp),
                    PhysicalRepresentation = string.Empty,
                    Types = new List<ItemTypeEnum> { ItemTypeEnum.Other },
                    Abilities = new List<CalcResult>(),
                    Status = "saved",
                    Modifiers = new List<object>()
                }
            })
        };

        return item;
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

        if (sourceRows.Count == 0 && payload.TotalMp > 0)
        {
            sourceRows.Add(new ContributionRow
            {
                Id = "mp-total-only",
                Text = "MP total",
                RunningTotal = payload.TotalMp
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
}
