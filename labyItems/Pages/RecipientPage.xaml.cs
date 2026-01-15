using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Calculator;
using Microsoft.Maui.ApplicationModel;

namespace labyItems.Pages;

public partial class RecipientPage : ContentPage
{
    private readonly TaskCompletionSource<RecipientInfo> _tcs = new();
    private readonly MpSubmissionPayload? _submission;

    public ObservableCollection<string> SubmissionSummaryLines { get; } = new();
    public bool HasSubmissionSummary => SubmissionSummaryLines.Count > 0;

    public RecipientPage(RecipientInfo? existing = null, MpSubmissionPayload? submission = null)
    {
        InitializeComponent();
        _submission = submission;
        BuildSummaryLines(submission);
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

    private async Task SendSubmissionEmailAsync(RecipientInfo recipient, MpSubmissionPayload payload)
    {
        var playerName = string.IsNullOrWhiteSpace(recipient.PlayerName) ? "unknown" : recipient.PlayerName;
        var subject = $"monster point item for {playerName}";

        int isp = payload.TotalIsp;
        var mpBreakdownLines = payload.Breakdown?.Select(b => b.Text).ToList() ?? new List<string>();
        var ispBreakdownLines = payload.IspBreakdown?.Select(b => b.Text).ToList() ?? new List<string>();

        var summaryBuilder = new StringBuilder();
        summaryBuilder.AppendLine($"ISP total: {isp}");
        summaryBuilder.AppendLine($"MP cost: {payload.TotalMp}");
        summaryBuilder.AppendLine($"Recipient player: {recipient.PlayerName}");
        summaryBuilder.AppendLine($"Recipient character: {recipient.CharacterName}");
        summaryBuilder.AppendLine($"Recipient class: {recipient.CharacterClass}");
        if (mpBreakdownLines.Count > 0)
        {
            summaryBuilder.AppendLine();
            summaryBuilder.AppendLine("MP breakdown:");
            summaryBuilder.AppendLine(string.Join("\n", mpBreakdownLines));
        }
        if (ispBreakdownLines.Count > 0)
        {
            summaryBuilder.AppendLine();
            summaryBuilder.AppendLine("ISP breakdown:");
            summaryBuilder.AppendLine(string.Join("\n", ispBreakdownLines));
        }

        var config = new
        {
            ispTotal = isp,
            mpTotal = payload.TotalMp,
            breakdown = payload.Breakdown?.Select(b => new { b.Id, b.Text, b.RunningTotal }).ToList(),
            ispBreakdown = payload.IspBreakdown?.Select(b => new { b.Id, b.Text, b.RunningTotal }).ToList(),
            recipient = recipient
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var token = JsonTokenCompressor.CompressToBase64(json);

        summaryBuilder.AppendLine();
        summaryBuilder.AppendLine("Encrypted token:");
        summaryBuilder.AppendLine(token);

        var mailto =
            $"mailto:items@labyrinthe.com"
            + $"?subject={Uri.EscapeDataString(subject)}"
            + $"&body={Uri.EscapeDataString(summaryBuilder.ToString())}";

        try
        {
            await Launcher.OpenAsync(mailto);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not open mail client: {ex.Message}", "OK");
        }
    }

    private void BuildSummaryLines(MpSubmissionPayload? payload)
    {
        SubmissionSummaryLines.Clear();
        if (payload == null)
        {
            OnPropertyChanged(nameof(HasSubmissionSummary));
            return;
        }

        if (payload.TotalMp > 0)
            SubmissionSummaryLines.Add($"MP total: {payload.TotalMp}");
        if (payload.Breakdown?.Count > 0)
        {
            SubmissionSummaryLines.Add("MP breakdown:");
            foreach (var line in payload.Breakdown.Select(b => b.Text))
                SubmissionSummaryLines.Add(line);
        }

        if (payload.TotalIsp > 0)
            SubmissionSummaryLines.Add($"ISP total: {payload.TotalIsp}");
        if (payload.IspBreakdown?.Count > 0)
        {
            SubmissionSummaryLines.Add("ISP breakdown:");
            foreach (var line in payload.IspBreakdown.Select(b => b.Text))
                SubmissionSummaryLines.Add(line);
        }

        OnPropertyChanged(nameof(HasSubmissionSummary));
    }
}
