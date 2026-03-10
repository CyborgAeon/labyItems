using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace labyItems.Services;

public enum NonStandardEntityType
{
    CharacterClass,
    CharacterRace,
    Ability,
    Miracle,
    Spell,
    Evocation
}

public sealed record NonStandardTemplate(
    string Name,
    string Json,
    string Subtitle);

public sealed record NonStandardFieldAlternative(
    string SourceName,
    string ValueJson,
    string DisplayText);

public sealed class NonStandardSaveRequest
{
    public NonStandardEntityType EntityType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public string? LifeScaleRaceName { get; set; }
    public string? LifeScaleClassName { get; set; }
    public IReadOnlyList<LifeScalePoint>? LifeScalePoints { get; set; }
}

public static class NonStandardContentService
{
    private const int NgramSize = 3;

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    public static async Task<IReadOnlyList<NonStandardTemplate>> GetTemplatesAsync(NonStandardEntityType entityType)
    {
        return entityType switch
        {
            NonStandardEntityType.CharacterClass => await GetClassTemplatesAsync(),
            NonStandardEntityType.CharacterRace => await GetRaceTemplatesAsync(),
            NonStandardEntityType.Ability => await GetAbilityTemplatesAsync(),
            NonStandardEntityType.Miracle => await GetMiracleTemplatesAsync(),
            NonStandardEntityType.Spell => await GetSpellTemplatesAsync(),
            NonStandardEntityType.Evocation => await GetEvocationTemplatesAsync(),
            _ => Array.Empty<NonStandardTemplate>()
        };
    }

    public static async Task<IReadOnlyList<NonStandardFieldAlternative>> GetFieldAlternativesAsync(
        NonStandardEntityType entityType,
        string fieldName)
    {
        var normalizedField = NormalizeFieldKey(fieldName);
        if (normalizedField.Length == 0)
            return Array.Empty<NonStandardFieldAlternative>();

        var templates = await GetTemplatesAsync(entityType);
        var options = new List<NonStandardFieldAlternative>();
        foreach (var template in templates)
        {
            if (!TryGetObject(template.Json, out var root))
                continue;

            if (!TryGetProperty(root, normalizedField, out var property))
                continue;

            var rawValue = property.Value.GetRawText();
            var preview = BuildValuePreview(property.Value);
            options.Add(new NonStandardFieldAlternative(
                SourceName: template.Name,
                ValueJson: rawValue,
                DisplayText: $"{template.Name}: {preview}"));
        }

        return options
            .OrderBy(o => o.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task<IReadOnlyList<NonStandardFieldAlternative>> GetArrayEntryAlternativesAsync(
        NonStandardEntityType entityType,
        string fieldName)
    {
        var normalizedField = NormalizeFieldKey(fieldName);
        if (normalizedField.Length == 0)
            return Array.Empty<NonStandardFieldAlternative>();

        var templates = await GetTemplatesAsync(entityType);
        var unique = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            if (!TryGetObject(template.Json, out var root))
                continue;

            if (!TryGetProperty(root, normalizedField, out var property)
                || property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in property.Value.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String)
                    continue;

                var text = (entry.GetString() ?? string.Empty).Trim();
                if (text.Length == 0)
                    continue;

                if (!unique.ContainsKey(text))
                    unique[text] = template.Name;
            }
        }

        return unique
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new NonStandardFieldAlternative(
                SourceName: kvp.Value,
                ValueJson: JsonSerializer.Serialize(new[] { kvp.Key }),
                DisplayText: $"{kvp.Key} ({kvp.Value})"))
            .ToList();
    }

    public static Task SaveAsync(NonStandardSaveRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Name is required.");

        var payload = BuildPayload(request.EntityType, name, request.DataJson);
        var dbPath = ServiceHelper.EnsureDbPath();
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new InvalidOperationException("Database path is unavailable.");

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        EnsureTables(conn);

        switch (request.EntityType)
        {
            case NonStandardEntityType.CharacterClass:
                UpsertClass(conn, name, payload);
                SaveLifeScaleForClass(conn, name, request);
                ClassService.InvalidateCache();
                LifeScalesService.InvalidateCache();
                break;
            case NonStandardEntityType.CharacterRace:
                UpsertRace(conn, name, payload);
                SaveLifeScaleForRace(conn, name, request);
                PeopleService.InvalidateCache();
                LifeScalesService.InvalidateCache();
                break;
            case NonStandardEntityType.Ability:
                UpsertAbility(conn, name, payload);
                EvolutionService.InvalidateCache();
                break;
            case NonStandardEntityType.Miracle:
                UpsertMiracle(conn, name, payload);
                MiracleService.InvalidateCache();
                break;
            case NonStandardEntityType.Spell:
                UpsertSpell(conn, name, payload);
                SpellService.InvalidateCache();
                break;
            case NonStandardEntityType.Evocation:
                UpsertEvocation(conn, name, payload);
                DruidEvocationService.InvalidateCache();
                break;
            default:
                throw new InvalidOperationException($"Unsupported entity type: {request.EntityType}");
        }

        return Task.CompletedTask;
    }

    private static async Task<IReadOnlyList<NonStandardTemplate>> GetClassTemplatesAsync()
    {
        var all = await ClassService.GetAllAsync();
        return all
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new NonStandardTemplate(
                Name: kvp.Key,
                Json: BuildClassTemplateJson(kvp.Value),
                Subtitle: kvp.Value.NonStandard ? "Non-standard" : "Standard"))
            .ToList();
    }

    private static async Task<IReadOnlyList<NonStandardTemplate>> GetRaceTemplatesAsync()
    {
        var all = await PeopleService.GetAllAsync();
        return all
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new NonStandardTemplate(
                Name: kvp.Key,
                Json: JsonSerializer.Serialize(kvp.Value, PrettyJson),
                Subtitle: kvp.Value.NonStandard ? "Non-standard" : "Standard"))
            .ToList();
    }

    private static async Task<IReadOnlyList<NonStandardTemplate>> GetAbilityTemplatesAsync()
    {
        var all = await EvolutionService.GetAllAbilitiesAsync();
        return all
            .OrderBy(a => a.Table)
            .ThenBy(a => a.Index, StringComparer.OrdinalIgnoreCase)
            .Select(a =>
            {
                var model = new JsonObject
                {
                    ["index"] = a.Index,
                    ["desc"] = a.Description,
                    ["cost"] = a.Cost,
                    ["table"] = a.Table,
                    ["canBuyMultiple"] = a.CanBuyMultiple,
                    ["preReqs"] = JsonSerializer.SerializeToNode(a.PreReqs) ?? new JsonArray(),
                    ["nonStandard"] = a.IsNonStandard,
                    ["NonStandard"] = a.IsNonStandard
                };

                if (!string.IsNullOrWhiteSpace(a.Available))
                    model["available"] = ParseNodeOrString(a.Available);

                if (a.MaxAvailable is { } max && max > 0)
                    model["maxAvailable"] = max;

                return new NonStandardTemplate(
                    Name: a.Index,
                    Json: model.ToJsonString(PrettyJson),
                    Subtitle: a.IsNonStandard ? "Non-standard" : $"Table {a.Table}");
            })
            .ToList();
    }

    private static async Task<IReadOnlyList<NonStandardTemplate>> GetSpellTemplatesAsync()
    {
        var all = await SpellService.GetAllAsync();
        return all
            .OrderBy(s => s.level)
            .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new NonStandardTemplate(
                Name: s.name,
                Json: JsonSerializer.Serialize(s, PrettyJson),
                Subtitle: s.nonStandard ? "Non-standard" : $"Lvl {Math.Max(0, s.level)}"))
            .ToList();
    }

    private static async Task<IReadOnlyList<NonStandardTemplate>> GetMiracleTemplatesAsync()
    {
        var all = await MiracleService.GetAllAsync();
        return all
            .OrderBy(m => m.power)
            .ThenBy(m => m.name, StringComparer.OrdinalIgnoreCase)
            .Select(m => new NonStandardTemplate(
                Name: m.name,
                Json: JsonSerializer.Serialize(m, PrettyJson),
                Subtitle: m.nonStandard ? "Non-standard" : $"Power {Math.Max(0, m.power)}"))
            .ToList();
    }

    private static async Task<IReadOnlyList<NonStandardTemplate>> GetEvocationTemplatesAsync()
    {
        var all = await DruidEvocationService.GetAllAsync();
        return all
            .OrderBy(e => e.power)
            .ThenBy(e => e.name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new NonStandardTemplate(
                Name: e.name,
                Json: JsonSerializer.Serialize(e, PrettyJson),
                Subtitle: e.nonStandard ? "Non-standard" : $"{Math.Max(0, e.power)} EP"))
            .ToList();
    }

    private static JsonObject BuildPayload(NonStandardEntityType entityType, string name, string rawJson)
    {
        if (!TryParseObject(rawJson, out var payload))
            throw new InvalidOperationException("Payload JSON must be a valid JSON object.");

        payload["nonStandard"] = true;
        payload["NonStandard"] = true;
        payload["is_default"] = 0;

        switch (entityType)
        {
            case NonStandardEntityType.Ability:
                payload["index"] = name;
                break;
            case NonStandardEntityType.Spell:
            case NonStandardEntityType.Miracle:
            case NonStandardEntityType.Evocation:
                payload["name"] = name;
                break;
        }

        return payload;
    }

    private static string BuildClassTemplateJson(CharacterClassRecord record)
    {
        var model = new JsonObject
        {
            ["Brackets"] = JsonSerializer.SerializeToNode(record.Brackets) ?? new JsonArray(),
            ["Levels"] = JsonSerializer.SerializeToNode(record.Levels) ?? new JsonObject(),
            ["Max AC"] = ReadJsonElementNode(record.MaxAC) ?? 0,
            ["CasterLevel"] = record.CasterLevel ?? 8,
            ["nonStandard"] = record.NonStandard,
            ["NonStandard"] = record.NonStandard
        };

        if (record.BuyAs is { Count: > 0 })
            model["Buy as"] = JsonSerializer.SerializeToNode(record.BuyAs);

        if (record.Powerbase is { Count: > 0 })
            model["Powerbase"] = JsonSerializer.SerializeToNode(record.Powerbase);

        if (record.PowerCalculations is { Count: > 0 })
            model["PowerCalculations"] = JsonSerializer.SerializeToNode(record.PowerCalculations);

        if (record.AlignmentRule != null)
            model["alignmentRule"] = JsonSerializer.SerializeToNode(record.AlignmentRule);

        if (record.GuildOverrides != null)
            model["GuildOverrides"] = JsonSerializer.SerializeToNode(record.GuildOverrides);

        if (record.Armour != null)
            model["Armour"] = JsonSerializer.SerializeToNode(record.Armour);

        var powerPerLevel = ReadJsonElementNode(record.PowerPerLevel);
        if (powerPerLevel != null)
            model["PowerPerLevel"] = powerPerLevel;

        return model.ToJsonString(PrettyJson);
    }

    private static void SaveLifeScaleForClass(SqliteConnection conn, string className, NonStandardSaveRequest request)
    {
        var race = (request.LifeScaleRaceName ?? string.Empty).Trim();
        if (race.Length == 0)
            throw new InvalidOperationException("A race must be selected for class life-scale mapping.");

        var points = NormalizeLifeScalePoints(request.LifeScalePoints);
        UpsertLifeScale(conn, race, className, points);
    }

    private static void SaveLifeScaleForRace(SqliteConnection conn, string raceName, NonStandardSaveRequest request)
    {
        var className = (request.LifeScaleClassName ?? string.Empty).Trim();
        if (className.Length == 0)
            throw new InvalidOperationException("A class must be selected for race life-scale mapping.");

        var points = NormalizeLifeScalePoints(request.LifeScalePoints);
        UpsertLifeScale(conn, raceName, className, points);
    }

    private static List<LifeScalePoint> NormalizeLifeScalePoints(IReadOnlyList<LifeScalePoint>? points)
    {
        var normalized = (points ?? Array.Empty<LifeScalePoint>())
            .Select(p => new LifeScalePoint(Math.Max(0, p.Body), Math.Max(0, p.Loc)))
            .ToList();

        if (normalized.Count < 8)
            throw new InvalidOperationException("Life-scale must include 8 levels.");

        return normalized;
    }

    private static void UpsertLifeScale(SqliteConnection conn, string raceName, string className, IReadOnlyList<LifeScalePoint> points)
    {
        Execute(conn,
            "DELETE FROM lifescales WHERE lower(race)=lower($race) AND lower(\"class\")=lower($class);",
            ("$race", raceName),
            ("$class", className));

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            Execute(conn,
                "INSERT INTO lifescales (race, \"class\", idx, body, loc) VALUES ($race, $class, $idx, $body, $loc);",
                ("$race", raceName),
                ("$class", className),
                ("$idx", index + 1),
                ("$body", point.Body),
                ("$loc", point.Loc));
        }
    }

    private static void UpsertClass(SqliteConnection conn, string name, JsonObject payload)
    {
        var id = FindIdByName(conn, "classes", name) ?? Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o");
        var json = payload.ToJsonString();

        if (ExistsById(conn, "classes", id))
        {
            Execute(conn,
                "UPDATE classes SET name=$name, name_lower=$lower, data_json=$data, updated_at=$updated WHERE id=$id;",
                ("$name", name),
                ("$lower", name.ToLowerInvariant()),
                ("$data", json),
                ("$updated", now),
                ("$id", id));
            return;
        }

        Execute(conn,
            "INSERT INTO classes (id, name, name_lower, data_json, created_at, updated_at) VALUES ($id, $name, $lower, $data, $created, $updated);",
            ("$id", id),
            ("$name", name),
            ("$lower", name.ToLowerInvariant()),
            ("$data", json),
            ("$created", now),
            ("$updated", now));
    }

    private static void UpsertRace(SqliteConnection conn, string name, JsonObject payload)
    {
        var id = FindIdByName(conn, "races", name) ?? Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o");
        var json = payload.ToJsonString();

        if (ExistsById(conn, "races", id))
        {
            Execute(conn,
                "UPDATE races SET name=$name, name_lower=$lower, data_json=$data, updated_at=$updated WHERE id=$id;",
                ("$name", name),
                ("$lower", name.ToLowerInvariant()),
                ("$data", json),
                ("$updated", now),
                ("$id", id));
            return;
        }

        Execute(conn,
            "INSERT INTO races (id, name, name_lower, data_json, created_at, updated_at) VALUES ($id, $name, $lower, $data, $created, $updated);",
            ("$id", id),
            ("$name", name),
            ("$lower", name.ToLowerInvariant()),
            ("$data", json),
            ("$created", now),
            ("$updated", now));
    }

    private static void UpsertAbility(SqliteConnection conn, string defaultName, JsonObject payload)
    {
        var index = ReadString(payload, "index", "idx", "name");
        if (index.Length == 0)
            index = defaultName;

        var table = ReadInt(payload, 1, "table", "table_id");
        var description = ReadString(payload, "desc", "description");
        var cost = ReadInt(payload, 0, "cost");
        var canBuyMultiple = ReadBool(payload, "canBuyMultiple", "can_buy_multiple") ? 1 : 0;
        var available = ReadNode(payload, "available")?.ToJsonString() ?? string.Empty;
        if (ReadNode(payload, "available") is JsonValue availableValue
            && availableValue.TryGetValue<string>(out var availableText))
        {
            available = availableText ?? string.Empty;
        }

        var preReqs = ReadNode(payload, "preReqs", "prereqs", "preReq")?.ToJsonString() ?? "[]";
        var now = DateTime.UtcNow.ToString("o");
        var dataJson = payload.ToJsonString();

        var id = FindAbilityId(conn, index, table) ?? Guid.NewGuid().ToString();

        if (ExistsById(conn, "evolution", id))
        {
            Execute(conn,
                @"UPDATE evolution
SET idx=$idx,
    idx_lower=$lower,
    description=$description,
    cost=$cost,
    available=$available,
    table_id=$table,
    can_buy_multiple=$multi,
    prereqs_json=$prereqs,
    data_json=$data,
    is_default=0,
    updated_at=$updated
WHERE id=$id;",
                ("$idx", index),
                ("$lower", index.ToLowerInvariant()),
                ("$description", description),
                ("$cost", cost),
                ("$available", available),
                ("$table", table),
                ("$multi", canBuyMultiple),
                ("$prereqs", preReqs),
                ("$data", dataJson),
                ("$updated", now),
                ("$id", id));
        }
        else
        {
            Execute(conn,
                @"INSERT INTO evolution
(id, idx, idx_lower, description, cost, available, table_id, can_buy_multiple, prereqs_json, data_json, is_default, created_at, updated_at)
VALUES ($id, $idx, $lower, $description, $cost, $available, $table, $multi, $prereqs, $data, 0, $created, $updated);",
                ("$id", id),
                ("$idx", index),
                ("$lower", index.ToLowerInvariant()),
                ("$description", description),
                ("$cost", cost),
                ("$available", available),
                ("$table", table),
                ("$multi", canBuyMultiple),
                ("$prereqs", preReqs),
                ("$data", dataJson),
                ("$created", now),
                ("$updated", now));
        }

        ReplaceNgramRows(conn, "evolution_ngrams", "evolution_id", id, $"{index} {description}");
    }

    private static void UpsertSpell(SqliteConnection conn, string defaultName, JsonObject payload)
    {
        var name = ReadString(payload, "name");
        if (name.Length == 0)
            name = defaultName;

        var level = ReadInt(payload, 0, "level");
        var colour = ReadString(payload, "colour");
        var range = ReadString(payload, "range");
        var duration = ReadString(payload, "duration");
        var verbal = ReadString(payload, "verbal");
        var description = ReadString(payload, "description");
        var isAdvanced = ReadBool(payload, "isAdvanced", "is_advanced") ? 1 : 0;
        var now = DateTime.UtcNow.ToString("o");
        var dataJson = payload.ToJsonString();

        var id = FindIdByName(conn, "spells", name) ?? Guid.NewGuid().ToString();
        if (ExistsById(conn, "spells", id))
        {
            Execute(conn,
                @"UPDATE spells
SET name=$name,
    name_lower=$lower,
    level=$level,
    colour=$colour,
    range=$range,
    duration=$duration,
    verbal=$verbal,
    description=$description,
    is_advanced=$isAdvanced,
    data_json=$data,
    updated_at=$updated
WHERE id=$id;",
                ("$name", name),
                ("$lower", name.ToLowerInvariant()),
                ("$level", level),
                ("$colour", colour),
                ("$range", range),
                ("$duration", duration),
                ("$verbal", verbal),
                ("$description", description),
                ("$isAdvanced", isAdvanced),
                ("$data", dataJson),
                ("$updated", now),
                ("$id", id));
            return;
        }

        Execute(conn,
            @"INSERT INTO spells
(id, name, name_lower, level, colour, range, duration, verbal, description, is_advanced, data_json, created_at, updated_at)
VALUES ($id, $name, $lower, $level, $colour, $range, $duration, $verbal, $description, $isAdvanced, $data, $created, $updated);",
            ("$id", id),
            ("$name", name),
            ("$lower", name.ToLowerInvariant()),
            ("$level", level),
            ("$colour", colour),
            ("$range", range),
            ("$duration", duration),
            ("$verbal", verbal),
            ("$description", description),
            ("$isAdvanced", isAdvanced),
            ("$data", dataJson),
            ("$created", now),
            ("$updated", now));
    }

    private static void UpsertMiracle(SqliteConnection conn, string defaultName, JsonObject payload)
    {
        var name = ReadString(payload, "name");
        if (name.Length == 0)
            name = defaultName;

        var power = ReadInt(payload, 0, "power");
        var sphere = ReadString(payload, "sphere");
        var alignment = ReadString(payload, "alignment");
        var description = ReadString(payload, "description");
        var isAdvanced = ReadBool(payload, "isAdvanced", "is_advanced") ? 1 : 0;
        var now = DateTime.UtcNow.ToString("o");
        var dataJson = payload.ToJsonString();

        var id = FindIdByName(conn, "miracles", name) ?? Guid.NewGuid().ToString();
        if (ExistsById(conn, "miracles", id))
        {
            Execute(conn,
                @"UPDATE miracles
SET name=$name,
    name_lower=$lower,
    power=$power,
    sphere=$sphere,
    alignment=$alignment,
    description=$description,
    is_advanced=$isAdvanced,
    data_json=$data,
    updated_at=$updated
WHERE id=$id;",
                ("$name", name),
                ("$lower", name.ToLowerInvariant()),
                ("$power", power),
                ("$sphere", sphere),
                ("$alignment", alignment),
                ("$description", description),
                ("$isAdvanced", isAdvanced),
                ("$data", dataJson),
                ("$updated", now),
                ("$id", id));
            return;
        }

        Execute(conn,
            @"INSERT INTO miracles
(id, name, name_lower, power, sphere, alignment, description, is_advanced, data_json, created_at, updated_at)
VALUES ($id, $name, $lower, $power, $sphere, $alignment, $description, $isAdvanced, $data, $created, $updated);",
            ("$id", id),
            ("$name", name),
            ("$lower", name.ToLowerInvariant()),
            ("$power", power),
            ("$sphere", sphere),
            ("$alignment", alignment),
            ("$description", description),
            ("$isAdvanced", isAdvanced),
            ("$data", dataJson),
            ("$created", now),
            ("$updated", now));
    }

    private static void UpsertEvocation(SqliteConnection conn, string defaultName, JsonObject payload)
    {
        var name = ReadString(payload, "name");
        if (name.Length == 0)
            name = defaultName;

        var power = ReadInt(payload, 0, "power");
        var range = ReadString(payload, "range");
        var duration = ReadString(payload, "duration");
        var verbal = ReadString(payload, "verbal");
        var description = ReadString(payload, "description");
        var fields = ReadNode(payload, "fields")?.ToJsonString() ?? "[]";
        var isAdvanced = ReadBool(payload, "isAdvanced", "is_advanced") ? 1 : 0;
        var now = DateTime.UtcNow.ToString("o");
        var dataJson = payload.ToJsonString();

        var id = FindIdByName(conn, "evocs", name) ?? Guid.NewGuid().ToString();
        if (ExistsById(conn, "evocs", id))
        {
            Execute(conn,
                @"UPDATE evocs
SET name=$name,
    name_lower=$lower,
    power=$power,
    range=$range,
    duration=$duration,
    verbal=$verbal,
    fields_json=$fields,
    description=$description,
    is_advanced=$isAdvanced,
    data_json=$data,
    is_default=0,
    updated_at=$updated
WHERE id=$id;",
                ("$name", name),
                ("$lower", name.ToLowerInvariant()),
                ("$power", power),
                ("$range", range),
                ("$duration", duration),
                ("$verbal", verbal),
                ("$fields", fields),
                ("$description", description),
                ("$isAdvanced", isAdvanced),
                ("$data", dataJson),
                ("$updated", now),
                ("$id", id));
        }
        else
        {
            Execute(conn,
                @"INSERT INTO evocs
(id, name, name_lower, power, range, duration, verbal, fields_json, description, is_advanced, data_json, is_default, created_at, updated_at)
VALUES ($id, $name, $lower, $power, $range, $duration, $verbal, $fields, $description, $isAdvanced, $data, 0, $created, $updated);",
                ("$id", id),
                ("$name", name),
                ("$lower", name.ToLowerInvariant()),
                ("$power", power),
                ("$range", range),
                ("$duration", duration),
                ("$verbal", verbal),
                ("$fields", fields),
                ("$description", description),
                ("$isAdvanced", isAdvanced),
                ("$data", dataJson),
                ("$created", now),
                ("$updated", now));
        }

        ReplaceNgramRows(conn, "evoc_ngrams", "evoc_id", id, $"{name} {description}");
    }

    private static void ReplaceNgramRows(
        SqliteConnection conn,
        string table,
        string idColumnFallback,
        string rowId,
        string searchableText)
    {
        var tokenColumn = ResolveColumnName(conn, table, "token", "ngram");
        if (tokenColumn == null)
            return;

        var idColumn = ResolveColumnName(conn, table, idColumnFallback) ?? idColumnFallback;

        Execute(conn, $"DELETE FROM {table} WHERE {idColumn}=$id;", ("$id", rowId));

        var normalized = ServiceHelper.NormalizeForNgrams((searchableText ?? string.Empty).ToLowerInvariant());
        var tokens = ServiceHelper.GenerateNGrams(normalized, NgramSize)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var token in tokens)
        {
            Execute(conn,
                $"INSERT INTO {table} ({tokenColumn}, {idColumn}) VALUES ($token, $id);",
                ("$token", token),
                ("$id", rowId));
        }
    }

    private static string? ResolveColumnName(SqliteConnection conn, string tableName, params string[] candidates)
    {
        var wanted = new HashSet<string>(candidates.Select(NormalizeFieldKey), StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.FieldCount < 2)
                continue;

            var columnName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (wanted.Contains(NormalizeFieldKey(columnName)))
                return columnName;
        }

        return null;
    }

    private static void EnsureTables(SqliteConnection conn)
    {
        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS classes (
id TEXT PRIMARY KEY,
name TEXT NOT NULL,
name_lower TEXT,
data_json TEXT,
created_at TEXT,
updated_at TEXT
);");

        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS races (
id TEXT PRIMARY KEY,
name TEXT NOT NULL,
name_lower TEXT,
data_json TEXT,
created_at TEXT,
updated_at TEXT
);");

        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS lifescales (
race TEXT NOT NULL,
""class"" TEXT NOT NULL,
idx INTEGER NOT NULL,
body INTEGER NOT NULL,
loc INTEGER NOT NULL
);");

        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS evolution (
id TEXT PRIMARY KEY,
idx TEXT NOT NULL,
idx_lower TEXT,
description TEXT,
cost INTEGER,
available TEXT,
table_id INTEGER,
can_buy_multiple INTEGER,
prereqs_json TEXT,
data_json TEXT,
is_default INTEGER,
created_at TEXT,
updated_at TEXT
);");

        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS evolution_ngrams (
token TEXT,
evolution_id TEXT
);");

        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS spells (
id TEXT PRIMARY KEY,
name TEXT NOT NULL,
name_lower TEXT,
level INTEGER,
colour TEXT,
range TEXT,
duration TEXT,
verbal TEXT,
description TEXT,
is_advanced INTEGER,
data_json TEXT,
created_at TEXT,
updated_at TEXT
);");

        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS miracles (
id TEXT PRIMARY KEY,
name TEXT NOT NULL,
name_lower TEXT,
power INTEGER,
sphere TEXT,
alignment TEXT,
description TEXT,
is_advanced INTEGER,
data_json TEXT,
created_at TEXT,
updated_at TEXT
);");

        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS evocs (
id TEXT PRIMARY KEY,
name TEXT NOT NULL,
name_lower TEXT,
power INTEGER,
range TEXT,
duration TEXT,
verbal TEXT,
fields_json TEXT,
description TEXT,
is_advanced INTEGER,
data_json TEXT,
is_default INTEGER,
created_at TEXT,
updated_at TEXT
);");

        Execute(conn,
            @"CREATE TABLE IF NOT EXISTS evoc_ngrams (
token TEXT,
evoc_id TEXT
);");
    }

    private static JsonNode ParseNodeOrString(string text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
            return string.Empty;

        try
        {
            return JsonNode.Parse(raw) ?? raw;
        }
        catch
        {
            return raw;
        }
    }

    private static JsonNode? ReadJsonElementNode(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        try
        {
            return JsonNode.Parse(element.GetRawText());
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetObject(string json, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            root = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseObject(string json, out JsonObject payload)
    {
        payload = new JsonObject();
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                payload = obj;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement root, string normalizedField, out JsonProperty property)
    {
        foreach (var candidate in root.EnumerateObject())
        {
            if (!string.Equals(NormalizeFieldKey(candidate.Name), normalizedField, StringComparison.OrdinalIgnoreCase))
                continue;

            property = candidate;
            return true;
        }

        property = default;
        return false;
    }

    private static string BuildValuePreview(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => (value.GetString() ?? string.Empty).Trim(),
            JsonValueKind.Array => $"Array[{value.GetArrayLength()}] {Truncate(value.GetRawText(), 64)}",
            JsonValueKind.Object => $"Object {Truncate(value.GetRawText(), 64)}",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => value.GetRawText()
        };
    }

    private static string Truncate(string text, int max)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length <= max)
            return value;

        return value[..max] + "...";
    }

    private static string NormalizeFieldKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }

    private static JsonNode? ReadNode(JsonObject payload, params string[] keys)
    {
        var wanted = new HashSet<string>(keys.Select(NormalizeFieldKey), StringComparer.OrdinalIgnoreCase);
        foreach (var property in payload)
        {
            if (wanted.Contains(NormalizeFieldKey(property.Key)))
                return property.Value;
        }

        return null;
    }

    private static string ReadString(JsonObject payload, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return (text ?? string.Empty).Trim();

            if (value.TryGetValue<int>(out var number))
                return number.ToString();

            if (value.TryGetValue<double>(out var decimalNumber))
                return decimalNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (value.TryGetValue<bool>(out var boolean))
                return boolean ? "true" : "false";
        }

        return node?.ToJsonString() ?? string.Empty;
    }

    private static int ReadInt(JsonObject payload, int fallback, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;

            if (value.TryGetValue<double>(out var decimalNumber))
                return (int)Math.Truncate(decimalNumber);

            if (value.TryGetValue<string>(out var text)
                && int.TryParse((text ?? string.Empty).Trim(), out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static bool ReadBool(JsonObject payload, params string[] keys)
    {
        var node = ReadNode(payload, keys);
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolean))
                return boolean;

            if (value.TryGetValue<int>(out var number))
                return number != 0;

            if (value.TryGetValue<string>(out var text))
            {
                var trimmed = (text ?? string.Empty).Trim();
                if (bool.TryParse(trimmed, out var parsedBool))
                    return parsedBool;

                if (int.TryParse(trimmed, out var parsedInt))
                    return parsedInt != 0;
            }
        }

        return false;
    }

    private static string? FindIdByName(SqliteConnection conn, string table, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id FROM {table} WHERE lower(name)=lower($name) LIMIT 1;";
        cmd.Parameters.AddWithValue("$name", name);
        var scalar = cmd.ExecuteScalar();
        return scalar?.ToString();
    }

    private static string? FindAbilityId(SqliteConnection conn, string index, int table)
    {
        using (var exact = conn.CreateCommand())
        {
            exact.CommandText = "SELECT id FROM evolution WHERE lower(idx)=lower($idx) AND table_id=$table LIMIT 1;";
            exact.Parameters.AddWithValue("$idx", index);
            exact.Parameters.AddWithValue("$table", table);
            var byTable = exact.ExecuteScalar();
            if (byTable != null)
                return byTable.ToString();
        }

        using var fallback = conn.CreateCommand();
        fallback.CommandText = "SELECT id FROM evolution WHERE lower(idx)=lower($idx) LIMIT 1;";
        fallback.Parameters.AddWithValue("$idx", index);
        var scalar = fallback.ExecuteScalar();
        return scalar?.ToString();
    }

    private static bool ExistsById(SqliteConnection conn, string table, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM {table} WHERE id=$id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id);
        var scalar = cmd.ExecuteScalar();
        return scalar != null;
    }

    private static void Execute(SqliteConnection conn, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        cmd.ExecuteNonQuery();
    }
}
