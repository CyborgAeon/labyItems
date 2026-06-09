using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using labyItems.Controls;
using labyItems.Helpers;
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

        var itemName = ResolveItemName(item.Description);
        var physicalRepresentation = ResolveDescriptionField(item.Description, "Phys rep", "Physical representation");
        var description = BuildGrantDescription(itemName, NotationHelper.BuildHumanReadableAbilityLines(abilityList));
        if (description.Length == 0)
            description = item.Description ?? string.Empty;

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
            Description = description,
            Isp = item.Isp,
            CreatedDate = item.CreatedDate,
            Item = new ItemJsonDetail
            {
                DisplayName = itemName,
                PhysicalRepresentation = physicalRepresentation,
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
            .AppendLine($"Type: {FormatPayloadItemTypes(payload.Item.Types)}")
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
            .AppendLine(payload.Description ?? string.Empty)
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
        string to = "brbar@netcompany.com")
        => BuildDeskSubmissionEmailDraft(recipient, payload, to);

    public static EmailDraft BuildDeskSubmissionEmailDraft(
        RecipientInfo recipient,
        MpSubmissionPayload payload,
        string to = "brbar@netcompany.com")
    {
        var playerName = string.IsNullOrWhiteSpace(recipient.PlayerName) ? "unknown" : recipient.PlayerName;
        var sourceFlow = ResolveSubmissionSourceFlow(payload);
        var sourceLabel = ResolveSubmissionSourceLabel(sourceFlow);
        var subject = $"{sourceLabel} item for {playerName}";
        var itemName = ValidateAndNormalizeItemName(recipient.ItemName);
        var itemTypes = DeriveMpItemTypes(payload);
        var abilityText = BuildHumanReadableAbilityText(payload);
        var description = BuildGrantDescription(itemName, abilityText);
        var config = new
        {
            itemName,
            itemType = itemTypes,
            abilities = abilityText,
            description,
            item = new
            {
                name = itemName,
                types = itemTypes,
                abilities = abilityText,
                description,
                physicalRepresentation = payload.PhysicalRepresentation ?? string.Empty,
                sourceFlow,
                monsterPointCost = Math.Max(0, payload.TotalMp)
            },
            ispTotal = payload.TotalIsp,
            mpTotal = payload.TotalMp,
            breakdown = payload.Breakdown?.Select(b => new { b.Id, b.Text, b.RunningTotal }).ToList(),
            ispBreakdown = payload.IspBreakdown?.Select(b => new { b.Id, b.Text, b.RunningTotal }).ToList(),
            recipient,
            submittedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var token = JsonTokenCompressor.CompressToBase64(json);

        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Item name: {itemName}");
        bodyBuilder.AppendLine($"Item type: {FormatPayloadItemTypes(itemTypes)}");
        bodyBuilder.AppendLine($"Description: {description}");
        if (!string.IsNullOrWhiteSpace(payload.PhysicalRepresentation))
            bodyBuilder.AppendLine($"Physical representation: {payload.PhysicalRepresentation.Trim()}");
        bodyBuilder.AppendLine($"ISP total: {payload.TotalIsp}");
        if (payload.TotalMp > 0)
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

    public static List<string> DeriveItemTypes(IEnumerable<CalcResult>? abilities)
    {
        var abilityList = abilities?
            .Where(ability => ability != null)
            .ToList() ?? new List<CalcResult>();

        var types = new List<string>();
        if (abilityList.Any(ability => HasAbilityType(ability, "Spell", "Magic", "Magical")))
            AddUnique(types, "magical");
        if (abilityList.Any(ability => HasAbilityType(ability, "Miracle", "Spirit", "Spiritual")))
            AddUnique(types, "spiritual");
        if (abilityList.Any(ability => HasAbilityType(ability, "Evocation", "EarthPower", "Earthpower")))
            AddUnique(types, "earthpower");
        if (types.Count == 0)
            types.Add("physical");

        return types;
    }

    public static List<string> DeriveMpItemTypes(MpSubmissionPayload payload)
    {
        var explicitTypes = NormalizePayloadItemTypes(payload.ItemTypes);
        if (explicitTypes.Count > 0)
            return explicitTypes;

        var abilityTypes = DeriveItemTypes(payload.Abilities);
        if (abilityTypes.Count > 0
            && !abilityTypes.SequenceEqual(new[] { "physical" }, StringComparer.OrdinalIgnoreCase))
        {
            return abilityTypes;
        }

        var markers = new List<string>();
        AddContributionMarkers(payload.IspBreakdown, markers);
        AddContributionMarkers(payload.Breakdown, markers);

        var types = new List<string>();
        if (markers.Any(IsSpellMarker))
            AddUnique(types, "magical");
        if (markers.Any(IsMiracleMarker))
            AddUnique(types, "spiritual");
        if (markers.Any(IsEvocationMarker))
            AddUnique(types, "earthpower");
        if (types.Count == 0)
            types.Add("physical");

        return types;
    }

    public static string FormatPayloadItemTypes(IEnumerable<string>? types)
    {
        var normalized = NormalizePayloadItemTypes(types);
        if (normalized.Count == 0)
            normalized.Add("physical");

        return string.Join(", ", normalized.Select(ToDisplayItemType));
    }

    public static List<string> BuildHumanReadableAbilityLines(IEnumerable<CalcResult>? abilities)
        => NotationHelper.BuildHumanReadableAbilityLines(abilities);

    private static List<string> BuildHumanReadableAbilityText(MpSubmissionPayload payload)
    {
        if (payload.Abilities?.Count > 0)
        {
            var abilityLines = NotationHelper.BuildHumanReadableAbilityLines(payload.Abilities);
            if (abilityLines.Count > 0)
                return abilityLines;
        }

        var lines = new List<string>();
        AddHumanReadableBreakdownRows(lines, payload.IspBreakdown);
        if (lines.Count == 0)
            AddHumanReadableBreakdownRows(lines, payload.Breakdown);

        return lines;
    }

    private static void AddHumanReadableBreakdownRows<T>(List<string> lines, IEnumerable<T>? rows)
    {
        foreach (var row in rows ?? Enumerable.Empty<T>())
            lines.AddRange(NotationHelper.BuildHumanReadableAbilityLinesFromText(ReadProperty(row, "Text")));
    }

    private static string BuildGrantDescription(string? itemName, IEnumerable<string>? abilities)
    {
        var name = NormalizeWhitespace(itemName);
        var abilityText = JoinWithAnd(abilities);

        if (name.Length > 0 && abilityText.Length > 0)
            return $"{name} - Grants {abilityText}";
        if (abilityText.Length > 0)
            return $"Grants {abilityText}";
        if (name.Length > 0)
            return $"{name} -";

        return string.Empty;
    }

    private static string JoinWithAnd(IEnumerable<string>? values)
    {
        var parts = values?
            .Select(NormalizeWhitespace)
            .Where(value => value.Length > 0)
            .ToList() ?? new List<string>();

        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}"
        };
    }

    private static string ResolveItemName(string? description)
        => ResolveDescriptionField(description, "Item", "Name");

    private static string ResolveSubmissionSourceFlow(MpSubmissionPayload payload)
    {
        var value = NormalizeWhitespace(payload.SourceFlow);
        return value.Length == 0 ? "monster-point" : value.ToLowerInvariant();
    }

    private static string ResolveSubmissionSourceLabel(string sourceFlow)
        => sourceFlow.Trim().ToLowerInvariant() switch
        {
            "isp" => "ISP",
            "monster-point" => "monster point",
            _ => NormalizeWhitespace(sourceFlow).Length == 0
                ? "item"
                : NormalizeWhitespace(sourceFlow).Replace("-", " ")
        };

    private static string ResolveDescriptionField(string? description, params string[] labels)
    {
        foreach (var line in (description ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
                continue;

            var label = line[..separatorIndex].Trim();
            if (!labels.Any(candidate => label.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                continue;

            var value = NormalizeWhitespace(line[(separatorIndex + 1)..]);
            if (value.Length > 0)
                return value;
        }

        return string.Empty;
    }

    private static string NormalizeWhitespace(string? value)
        => Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ");

    private static List<string> NormalizePayloadItemTypes(IEnumerable<string>? types)
    {
        var normalized = new List<string>();
        foreach (var type in types ?? Enumerable.Empty<string>())
        {
            var mapped = NormalizePayloadItemType(type);
            if (mapped.Length > 0)
                AddUnique(normalized, mapped);
        }

        return normalized;
    }

    private static string NormalizePayloadItemType(string? value)
    {
        var token = new string((value ?? string.Empty)
            .Trim()
            .Where(char.IsLetter)
            .ToArray())
            .ToLowerInvariant();

        return token switch
        {
            "earthpower" => "earthpower",
            "physical" => "physical",
            "neuronic" or "neuro" => "neuronic",
            "spiritual" or "spirit" => "spiritual",
            "magical" or "magic" => "magical",
            "mantic" => "mantic",
            "other" or "none" => "physical",
            _ => string.Empty
        };
    }

    private static string ToDisplayItemType(string type)
    {
        return NormalizePayloadItemType(type) switch
        {
            "earthpower" => "Earthpower",
            "physical" => "Physical",
            "neuronic" => "Neuronic",
            "spiritual" => "Spiritual",
            "magical" => "Magic",
            "mantic" => "Mantic",
            _ => "Physical"
        };
    }

    private static bool HasAbilityType(CalcResult ability, params string[] typeNames)
    {
        var type = (ability.AbilityType ?? string.Empty).Trim();
        return typeNames.Any(candidate => type.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddContributionMarkers<T>(IEnumerable<T>? rows, List<string> markers)
    {
        foreach (var row in rows ?? Enumerable.Empty<T>())
        {
            var id = ReadProperty(row, "Id");
            var text = ReadProperty(row, "Text");
            if (id.Length > 0)
                markers.Add(id);
            if (text.Length > 0)
                markers.Add(text);
        }
    }

    private static string ReadProperty(object? instance, string propertyName)
    {
        if (instance == null)
            return string.Empty;

        if (instance is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(propertyName, out var readOnlyValue))
        {
            return (readOnlyValue?.ToString() ?? string.Empty).Trim();
        }

        if (instance is System.Collections.IDictionary dictionary
            && dictionary.Contains(propertyName))
        {
            return (dictionary[propertyName]?.ToString() ?? string.Empty).Trim();
        }

        var property = instance.GetType().GetProperty(propertyName);
        return (property?.GetValue(instance)?.ToString() ?? string.Empty).Trim();
    }

    private static bool IsSpellMarker(string value)
        => StartsWithTypeMarker(value, "spell");

    private static bool IsMiracleMarker(string value)
        => StartsWithTypeMarker(value, "miracle");

    private static bool IsEvocationMarker(string value)
        => StartsWithTypeMarker(value, "evocation");

    private static bool StartsWithTypeMarker(string value, string marker)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.StartsWith(marker, StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith($"isp-{marker}", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddUnique(List<string> types, string type)
    {
        if (!types.Contains(type, StringComparer.OrdinalIgnoreCase))
            types.Add(type);
    }

    private static string ValidateAndNormalizeItemName(string? itemName)
    {
        var value = NormalizeItemName(itemName);
        if (value.Length == 0)
            throw new ArgumentException("Item name is required.", nameof(itemName));
        if (value.Length > 30)
            throw new ArgumentException("Item name must be 30 characters or fewer.", nameof(itemName));
        if (!value.All(character => char.IsLetter(character) || character == ' '))
            throw new ArgumentException("Item name must contain letters and spaces only.", nameof(itemName));

        return value;
    }

    private static string NormalizeItemName(string? itemName)
        => string.Join(' ', (itemName ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public sealed record EmailDraft(string To, string Subject, string Body, string Token, string MailtoUri);
