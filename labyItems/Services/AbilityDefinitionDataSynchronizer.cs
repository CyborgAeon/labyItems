using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using labyItems.Models.Abilities.Effects;
using labyItems.Models.Characters;
using labyItems.Services.AbilityEffects;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public interface IAbilityDefinitionDataSynchronizer
{
    Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default);
}

public sealed class AbilityDefinitionDataSynchronizer : IAbilityDefinitionDataSynchronizer
{
    private const string SeedVersion = "ability-definitions-v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<AbilityDefinitionDataSynchronizer> _logger;

    public AbilityDefinitionDataSynchronizer(ILogger<AbilityDefinitionDataSynchronizer> logger)
    {
        _logger = logger;
    }

    public async Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return;

        var buildId = GetPackagedDataBuildId();

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        EnsureTablesExist(conn);

        var existingChecksum = GetExistingChecksum(conn);
        var existingBuildId = GetExistingBuildId(conn);
        if (!string.IsNullOrWhiteSpace(existingChecksum)
            && string.Equals(existingBuildId, buildId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipped ability definition sync: packaged build id '{BuildId}' already applied.", buildId);
            return;
        }

        var abilitiesJson = await ReadAssetTextAsync("specialisation/abilities.json", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(abilitiesJson))
        {
            _logger.LogWarning("Skipped ability definition sync: could not load packaged specialisation/abilities.json.");
            return;
        }

        var checksum = ComputeChecksum(abilitiesJson);
        var parsed = ParseAbilityDefinitions(abilitiesJson);
        if (parsed.Count == 0)
        {
            _logger.LogWarning("Skipped ability definition sync: no abilities were parsed.");
            return;
        }

        if (string.Equals(existingChecksum, checksum, StringComparison.OrdinalIgnoreCase))
        {
            SaveChecksum(conn, tx: null, checksum, buildId);
            return;
        }

        using var tx = conn.BeginTransaction();
        DeleteExistingDefaults(conn, tx);

        foreach (var row in parsed)
        {
            UpsertDefinition(conn, tx, row);
            ReplaceInstructions(conn, tx, row.AbilityKey, row.Instructions);
        }

        SaveChecksum(conn, tx, checksum, buildId);
        tx.Commit();

        AbilityDefinitionLookupService.InvalidateCache();
        AbilityDetailsLookupService.InvalidateCache();

        _logger.LogInformation("Ability definitions synchronized. Upserted {Count} defaults.", parsed.Count);
    }

    private static List<AbilityDefinitionRow> ParseAbilityDefinitions(string rawJson)
    {
        var rows = new List<AbilityDefinitionRow>();
        using var doc = JsonDocument.Parse(rawJson);

        if (!TryGetProperty(doc.RootElement, "abilities", out var abilitiesElement)
            || abilitiesElement.ValueKind != JsonValueKind.Object)
        {
            return rows;
        }

        foreach (var property in abilitiesElement.EnumerateObject())
        {
            var sourceKey = (property.Name ?? string.Empty).Trim();
            if (sourceKey.Length == 0)
                continue;

            AbilityDefinition definition;
            try
            {
                definition = JsonSerializer.Deserialize<AbilityDefinition>(property.Value.GetRawText(), JsonOptions)
                             ?? new AbilityDefinition();
            }
            catch
            {
                definition = new AbilityDefinition();
            }

            var abilityKey = ResolveAbilityKey(sourceKey, definition);
            if (abilityKey.Length == 0)
                continue;

            var name = (definition.Name ?? string.Empty).Trim();
            if (name.Length == 0)
                name = abilityKey;

            var systemEffects = definition.SystemEffects ?? new List<AbilitySystemEffect>();
            var systemEffectsJson = systemEffects.Count > 0
                ? JsonSerializer.Serialize(systemEffects)
                : null;

            var instructions = BuildInstructionList(definition);
            var metadata = BuildMetadataJson(sourceKey, definition);
            var id = DeterministicGuid($"ability-definition|{abilityKey}");

            rows.Add(new AbilityDefinitionRow(
                Id: id,
                AbilityKey: abilityKey,
                Name: name,
                Type: (definition.Type ?? string.Empty).Trim(),
                Source: (definition.Source ?? string.Empty).Trim(),
                Lore: (definition.Lore ?? string.Empty).Trim(),
                EffectText: (definition.Effect ?? string.Empty).Trim(),
                SystemEffectsJson: systemEffectsJson,
                RawDataJson: property.Value.GetRawText(),
                MetadataJson: metadata,
                Instructions: instructions));
        }

        return rows;
    }

    private static List<AbilityEffectInstruction> BuildInstructionList(AbilityDefinition definition)
    {
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<AbilityEffectInstruction>();

        void AddInstruction(AbilityEffectInstruction? instruction)
        {
            if (instruction == null)
                return;

            var serialized = JsonSerializer.Serialize(instruction);
            if (!dedupe.Add(serialized))
                return;

            list.Add(instruction);
        }

        foreach (var instruction in AbilityEffectEvaluator.FromSystemEffects(
                     definition.SystemEffects ?? new List<AbilitySystemEffect>()))
        {
            AddInstruction(instruction);
        }

        // Best-effort textual fallback extraction for effects we haven't fully typed yet.
        foreach (var instruction in TextFallbackEffectApplier.ExtractInstructions(definition.Name))
            AddInstruction(instruction);
        foreach (var instruction in TextFallbackEffectApplier.ExtractInstructions(definition.Effect))
            AddInstruction(instruction);

        return list;
    }

    private static string ResolveAbilityKey(string sourceKey, AbilityDefinition definition)
    {
        var key = (definition.Key ?? string.Empty).Trim();
        if (key.Length > 0)
            return key;

        var abilityRef = (definition.AbilityRef ?? string.Empty).Trim();
        if (abilityRef.Length > 0)
            return abilityRef;

        return sourceKey;
    }

    private static string BuildMetadataJson(string sourceKey, AbilityDefinition definition)
    {
        return JsonSerializer.Serialize(new
        {
            sourceKey,
            key = definition.Key,
            abilityRef = definition.AbilityRef,
            updateKey = definition.UpdateKey,
            overwriteKey = definition.OverwriteKey,
            battleboardNameOverride = definition.BattleboardNameOverride
        });
    }

    private static void DeleteExistingDefaults(SqliteConnection conn, SqliteTransaction tx)
    {
        using var clearInstructions = conn.CreateCommand();
        clearInstructions.Transaction = tx;
        clearInstructions.CommandText = "DELETE FROM ability_effect_instructions WHERE is_default = 1;";
        clearInstructions.ExecuteNonQuery();

        using var clearDefinitions = conn.CreateCommand();
        clearDefinitions.Transaction = tx;
        clearDefinitions.CommandText = "DELETE FROM ability_definitions WHERE is_default = 1;";
        clearDefinitions.ExecuteNonQuery();

        using var clearChoiceSets = conn.CreateCommand();
        clearChoiceSets.Transaction = tx;
        clearChoiceSets.CommandText = "DELETE FROM ability_choice_sets WHERE is_default = 1;";
        clearChoiceSets.ExecuteNonQuery();
    }

    private static void UpsertDefinition(SqliteConnection conn, SqliteTransaction tx, AbilityDefinitionRow row)
    {
        var now = DateTime.UtcNow.ToString("o");
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO ability_definitions
(id, ability_key, name, type, source, lore, effect_text, system_effects_json, raw_data_json, metadata_json, is_default, created_at, updated_at)
VALUES ($id, $abilityKey, $name, $type, $source, $lore, $effectText, $systemEffectsJson, $rawDataJson, $metadataJson, 1, $createdAt, $updatedAt)
ON CONFLICT(ability_key) DO UPDATE SET
    id = excluded.id,
    name = excluded.name,
    type = excluded.type,
    source = excluded.source,
    lore = excluded.lore,
    effect_text = excluded.effect_text,
    system_effects_json = excluded.system_effects_json,
    raw_data_json = excluded.raw_data_json,
    metadata_json = excluded.metadata_json,
    is_default = 1,
    updated_at = excluded.updated_at;";
        cmd.Parameters.AddWithValue("$id", row.Id);
        cmd.Parameters.AddWithValue("$abilityKey", row.AbilityKey);
        cmd.Parameters.AddWithValue("$name", row.Name);
        cmd.Parameters.AddWithValue("$type", row.Type);
        cmd.Parameters.AddWithValue("$source", row.Source);
        cmd.Parameters.AddWithValue("$lore", row.Lore);
        cmd.Parameters.AddWithValue("$effectText", row.EffectText);
        cmd.Parameters.AddWithValue("$systemEffectsJson", (object?)row.SystemEffectsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rawDataJson", row.RawDataJson);
        cmd.Parameters.AddWithValue("$metadataJson", row.MetadataJson);
        cmd.Parameters.AddWithValue("$createdAt", now);
        cmd.Parameters.AddWithValue("$updatedAt", now);
        cmd.ExecuteNonQuery();
    }

    private static void ReplaceInstructions(
        SqliteConnection conn,
        SqliteTransaction tx,
        string abilityKey,
        IReadOnlyList<AbilityEffectInstruction> instructions)
    {
        using (var delete = conn.CreateCommand())
        {
            delete.Transaction = tx;
            delete.CommandText = "DELETE FROM ability_effect_instructions WHERE ability_key = $abilityKey;";
            delete.Parameters.AddWithValue("$abilityKey", abilityKey);
            delete.ExecuteNonQuery();
        }

        if (instructions == null || instructions.Count == 0)
            return;

        using var insert = conn.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = @"
INSERT INTO ability_effect_instructions
(id, ability_key, instruction_json, is_default, created_at, updated_at)
VALUES ($id, $abilityKey, $instructionJson, 1, $createdAt, $updatedAt);";

        var idParam = insert.Parameters.Add("$id", SqliteType.Text);
        var keyParam = insert.Parameters.Add("$abilityKey", SqliteType.Text);
        var jsonParam = insert.Parameters.Add("$instructionJson", SqliteType.Text);
        var createdAtParam = insert.Parameters.Add("$createdAt", SqliteType.Text);
        var updatedAtParam = insert.Parameters.Add("$updatedAt", SqliteType.Text);

        var now = DateTime.UtcNow.ToString("o");
        keyParam.Value = abilityKey;
        createdAtParam.Value = now;
        updatedAtParam.Value = now;

        var index = 0;
        foreach (var instruction in instructions)
        {
            idParam.Value = DeterministicGuid($"ability-effect|{abilityKey}|{index}");
            jsonParam.Value = JsonSerializer.Serialize(instruction);
            insert.ExecuteNonQuery();
            index++;
        }
    }

    private static void SaveChecksum(SqliteConnection conn, SqliteTransaction? tx, string checksum, string buildId)
    {
        using (var delete = conn.CreateCommand())
        {
            if (tx != null)
                delete.Transaction = tx;
            delete.CommandText = "DELETE FROM seed_metadata WHERE seed_version = $seedVersion;";
            delete.Parameters.AddWithValue("$seedVersion", SeedVersion);
            delete.ExecuteNonQuery();
        }

        using var insert = conn.CreateCommand();
        if (tx != null)
            insert.Transaction = tx;
        insert.CommandText = @"
INSERT INTO seed_metadata (seed_version, schema_version, build_id, checksum, created_at)
VALUES ($seedVersion, $schemaVersion, $buildId, $checksum, $createdAt);";
        insert.Parameters.AddWithValue("$seedVersion", SeedVersion);
        insert.Parameters.AddWithValue("$schemaVersion", 1);
        insert.Parameters.AddWithValue("$buildId", buildId);
        insert.Parameters.AddWithValue("$checksum", checksum);
        insert.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));
        insert.ExecuteNonQuery();
    }

    private static string? GetExistingBuildId(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT build_id
FROM seed_metadata
WHERE seed_version = $seedVersion
ORDER BY rowid DESC
LIMIT 1;";
        cmd.Parameters.AddWithValue("$seedVersion", SeedVersion);
        return cmd.ExecuteScalar()?.ToString();
    }

    private static string? GetExistingChecksum(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT checksum
FROM seed_metadata
WHERE seed_version = $seedVersion
ORDER BY rowid DESC
LIMIT 1;";
        cmd.Parameters.AddWithValue("$seedVersion", SeedVersion);
        var scalar = cmd.ExecuteScalar();
        return scalar?.ToString();
    }

    private static void EnsureTablesExist(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS ability_definitions (
  id TEXT PRIMARY KEY,
  ability_key TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL,
  type TEXT,
  source TEXT,
  lore TEXT,
  effect_text TEXT,
  system_effects_json TEXT,
  raw_data_json TEXT,
  metadata_json TEXT,
  is_default INTEGER,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_ability_definitions_key ON ability_definitions(ability_key);

CREATE TABLE IF NOT EXISTS ability_choice_sets (
  id TEXT PRIMARY KEY,
  choice_set_key TEXT NOT NULL UNIQUE,
  raw_data_json TEXT,
  metadata_json TEXT,
  is_default INTEGER,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_ability_choice_sets_key ON ability_choice_sets(choice_set_key);

CREATE TABLE IF NOT EXISTS ability_effect_instructions (
  id TEXT PRIMARY KEY,
  ability_key TEXT NOT NULL,
  instruction_json TEXT NOT NULL,
  is_default INTEGER,
  created_at TEXT,
  updated_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_ability_effect_instructions_key ON ability_effect_instructions(ability_key);

CREATE TABLE IF NOT EXISTS seed_metadata (
  seed_version TEXT,
  schema_version INTEGER,
  build_id TEXT,
  checksum TEXT,
  created_at TEXT
);";
        cmd.ExecuteNonQuery();
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            value = property.Value;
            return true;
        }

        return false;
    }

    private static async Task<string> ReadAssetTextAsync(string logicalName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
            return string.Empty;

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(logicalName);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ComputeChecksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string DeterministicGuid(string input)
    {
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes((input ?? string.Empty).ToLowerInvariant()));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes).ToString();
    }

    private static string GetPackagedDataBuildId()
        => $"{AppInfo.Current.VersionString}+{AppInfo.Current.BuildString}";

    private sealed record AbilityDefinitionRow(
        string Id,
        string AbilityKey,
        string Name,
        string Type,
        string Source,
        string Lore,
        string EffectText,
        string? SystemEffectsJson,
        string RawDataJson,
        string MetadataJson,
        IReadOnlyList<AbilityEffectInstruction> Instructions);
}
