using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using labyItems.Controls;
using labyItems.Models;
using labyItems.Pages.Calculator;

namespace labyItems.Services;

public static class ItemEmailService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ItemJsonPayload BuildItemPayload(Item item, IEnumerable<CalcResult>? abilities = null)
    {
        var abilityList = abilities?.ToList() ?? new List<CalcResult>();
        if (abilityList.Count == 0 && item.Isp > 0)
        {
            abilityList.Add(new CalcResult
            {
                AbilityType = "Base",
                AbilityName = "Manual ISP entry",
                TotalIsp = item.Isp,
                Details = new() { ["source"] = "Item" }
            });
        }

        var payload = new ItemJsonPayload
        {
            WitnessName = item.WitnessName ?? string.Empty,
            Recipient = string.IsNullOrWhiteSpace(item.RecipientPlayerName)
                        && string.IsNullOrWhiteSpace(item.RecipientCharacterName)
                        && string.IsNullOrWhiteSpace(item.RecipientCharacterClass)
                ? null
                : new RecipientPayload
                {
                    PlayerName = item.RecipientPlayerName ?? string.Empty,
                    CharacterName = item.RecipientCharacterName ?? string.Empty,
                    CharacterClass = item.RecipientCharacterClass ?? string.Empty
                },
            Description = item.Description ?? string.Empty,
            Isp = item.Isp,
            CreatedDate = item.CreatedDate,
            Item = new ItemJsonDetail
            {
                Types = DeriveItemTypes(abilityList),
                Abilities = abilityList,
                Status = "TODO",
                Modifiers = new List<object>()
            }
        };

        return payload;
    }

    public static string SerializeItemPayload(ItemJsonPayload payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    public static ItemJsonPayload? TryDeserializeItemPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ItemJsonPayload>(payloadJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static EmailDraft BuildItemEmailDraft(
        Item item,
        ItemJsonPayload payload,
        string to = "isp@labyrinthe.com",
        string subject = "automated ISP item")
    {
        var json = SerializeItemPayload(payload);
        var token = JsonTokenCompressor.CompressToBase64(json);

        var body = new StringBuilder()
            .AppendLine("Automated ISP item")
            .AppendLine()
            .AppendLine($"Type: {item.ItemType}")
            .AppendLine($"Maker player: {item.Maker?.PlayerName}")
            .AppendLine($"Maker character: {item.Maker?.Name}")
            .AppendLine($"Witness: {item.WitnessName}")
            .AppendLine($"Recipient player: {item.RecipientPlayerName}")
            .AppendLine($"Recipient character: {item.RecipientCharacterName}")
            .AppendLine($"Recipient class: {item.RecipientCharacterClass}")
            .AppendLine($"ISP: {item.Isp}")
            .AppendLine($"DNBUOD: {item.DoesNotBlowUpOnDeath}")
            .AppendLine($"Created: {item.CreatedDate:dd MMM yyyy HH:mm}")
            .AppendLine()
            .AppendLine("Description:")
            .AppendLine(item.Description ?? string.Empty)
            .AppendLine()
            .AppendLine("Machine-readable JSON:")
            .AppendLine(json)
            .AppendLine()
            .AppendLine("Compressed token:")
            .AppendLine(token)
            .ToString();

        var mailto = $"mailto:{to}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
        return new EmailDraft(to, subject, body, token, mailto);
    }

    public static EmailDraft BuildMpSubmissionEmailDraft(
        RecipientInfo recipient,
        MpSubmissionPayload payload,
        string to = "items@labyrinthe.com")
    {
        var playerName = string.IsNullOrWhiteSpace(recipient.PlayerName) ? "unknown" : recipient.PlayerName;
        var subject = $"monster point item for {playerName}";
        var config = new
        {
            ispTotal = payload.TotalIsp,
            mpTotal = payload.TotalMp,
            breakdown = payload.Breakdown?.Select(b => new { b.Id, b.Text, b.RunningTotal }).ToList(),
            ispBreakdown = payload.IspBreakdown?.Select(b => new { b.Id, b.Text, b.RunningTotal }).ToList(),
            recipient
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var token = JsonTokenCompressor.CompressToBase64(json);

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"ISP total: {payload.TotalIsp}");
        bodyBuilder.AppendLine($"MP cost: {payload.TotalMp}");
        bodyBuilder.AppendLine($"Recipient player: {recipient.PlayerName}");
        bodyBuilder.AppendLine($"Recipient character: {recipient.CharacterName}");
        bodyBuilder.AppendLine($"Recipient class: {recipient.CharacterClass}");

        var mpBreakdownLines = payload.Breakdown?.Select(b => b.Text).ToList() ?? new List<string>();
        if (mpBreakdownLines.Count > 0)
        {
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine("MP breakdown:");
            bodyBuilder.AppendLine(string.Join("\n", mpBreakdownLines));
        }

        var ispBreakdownLines = payload.IspBreakdown?.Select(b => b.Text).ToList() ?? new List<string>();
        if (ispBreakdownLines.Count > 0)
        {
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine("ISP breakdown:");
            bodyBuilder.AppendLine(string.Join("\n", ispBreakdownLines));
        }

        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Machine-readable JSON:");
        bodyBuilder.AppendLine(json);
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Compressed token:");
        bodyBuilder.AppendLine(token);

        var body = bodyBuilder.ToString();
        var mailto = $"mailto:{to}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
        return new EmailDraft(to, subject, body, token, mailto);
    }

    private static List<ItemTypeEnum> DeriveItemTypes(IReadOnlyCollection<CalcResult> abilities)
    {
        bool isSpiritual = abilities.Any(IsSpiritualAbility);
        bool isEarthpower = abilities.Any(IsEarthpowerAbility);

        var types = new List<ItemTypeEnum>();
        if (isEarthpower) types.Add(ItemTypeEnum.EarthPower);
        if (isSpiritual) types.Add(ItemTypeEnum.Spirit);
        if (types.Count == 0) types.Add(ItemTypeEnum.None);
        return types;
    }

    private static bool IsSpiritualAbility(CalcResult result)
    {
        if (result.AbilityType.Equals("Miracle", StringComparison.OrdinalIgnoreCase))
        {
            return HasPositive(result, "basicPerDay", "advancedPerDay", "generalSpiritStore", "sphereSpiritStore",
                               "turnBasicUpTo5thMantic", "turnBasicMantic", "turnAdvancedUpTo6thMantic", "turnAdvancedAbove6thMantic", "trueBeliever")
                   || HasTrue(result, "addBasicToList", "addAdvancedToList", "addWithPrep30", "isTeachingScroll");
        }

        if (result.AbilityType.Equals("General", StringComparison.OrdinalIgnoreCase))
        {
            var spiritPrayer = GetString(result, "prayerPowerbase")?.Equals("Spirit", StringComparison.OrdinalIgnoreCase) == true
                               && GetInt(result, "prayerTimesPerDay") > 0;

            return HasTrue(result, "empowerWeaponSpirit", "empowerWeaponMantic", "undeadTouchEffect", "gaseousForm", "walkThroughWalls", "planeShift")
                   || spiritPrayer;
        }

        return false;
    }

    private static bool IsEarthpowerAbility(CalcResult result)
    {
        if (result.AbilityType.Equals("Evocation", StringComparison.OrdinalIgnoreCase))
        {
            return HasPositive(result, "basicPerDay", "advancedPerDay", "drawOnEpPerDay")
                   || HasTrue(result, "addBasicToList", "addAdvancedToList", "addWithPrep30");
        }

        return false;
    }

    private static bool HasPositive(CalcResult res, params string[] keys) =>
        keys.Any(k => GetInt(res, k) > 0);

    private static bool HasTrue(CalcResult res, params string[] keys) =>
        keys.Any(k => GetBool(res, k));

    private static int GetInt(CalcResult res, string key)
    {
        if (res.Details.TryGetValue(key, out var v))
        {
            if (v is int i) return i;
            if (v is long l) return (int)l;
        }
        return 0;
    }

    private static bool GetBool(CalcResult res, string key)
    {
        if (res.Details.TryGetValue(key, out var v))
        {
            if (v is bool b) return b;
            if (v is int i) return i > 0;
            if (v is long l) return l > 0;
            if (v is string s && bool.TryParse(s, out var parsed)) return parsed;
        }
        return false;
    }

    private static string? GetString(CalcResult res, string key)
    {
        if (res.Details.TryGetValue(key, out var v))
            return v?.ToString();
        return null;
    }
}

public sealed record EmailDraft(string To, string Subject, string Body, string Token, string MailtoUri);
