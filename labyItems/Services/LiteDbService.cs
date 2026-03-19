using System.Globalization;
using System.Linq;
using System.Text.Json;
using labyItems.Models;
using labyItems.Models.Characters;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class LiteDbService
{
    private const string CharactersTableName = "wallet_characters";
    private const string ItemsTableName = "wallet_items";
    private static readonly object SchemaSync = new();
    private static bool _schemaEnsured;

    public static void InsertItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.Id = EnsureId(item.Id);
        item.CreatedDate = EnsureCreatedDate(item.CreatedDate);

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
INSERT INTO {ItemsTableName}
(
    id,
    item_type,
    maker_id,
    maker_name,
    maker_player_name,
    maker_class,
    maker_race,
    maker_race_subtype,
    maker_race_subtype_key,
    maker_notes,
    maker_points,
    witness_name,
    recipient_player_name,
    recipient_character_name,
    recipient_character_class,
    description,
    payload_json,
    isp,
    created_date,
    does_not_blow_up_on_death,
    assigned_character_id,
    assigned_character_name,
    assigned_character_player_name
)
VALUES
(
    @id,
    @item_type,
    @maker_id,
    @maker_name,
    @maker_player_name,
    @maker_class,
    @maker_race,
    @maker_race_subtype,
    @maker_race_subtype_key,
    @maker_notes,
    @maker_points,
    @witness_name,
    @recipient_player_name,
    @recipient_character_name,
    @recipient_character_class,
    @description,
    @payload_json,
    @isp,
    @created_date,
    @does_not_blow_up_on_death,
    @assigned_character_id,
    @assigned_character_name,
    @assigned_character_player_name
)
ON CONFLICT(id) DO UPDATE SET
    item_type = excluded.item_type,
    maker_id = excluded.maker_id,
    maker_name = excluded.maker_name,
    maker_player_name = excluded.maker_player_name,
    maker_class = excluded.maker_class,
    maker_race = excluded.maker_race,
    maker_race_subtype = excluded.maker_race_subtype,
    maker_race_subtype_key = excluded.maker_race_subtype_key,
    maker_notes = excluded.maker_notes,
    maker_points = excluded.maker_points,
    witness_name = excluded.witness_name,
    recipient_player_name = excluded.recipient_player_name,
    recipient_character_name = excluded.recipient_character_name,
    recipient_character_class = excluded.recipient_character_class,
    description = excluded.description,
    payload_json = excluded.payload_json,
    isp = excluded.isp,
    created_date = excluded.created_date,
    does_not_blow_up_on_death = excluded.does_not_blow_up_on_death,
    assigned_character_id = excluded.assigned_character_id,
    assigned_character_name = excluded.assigned_character_name,
    assigned_character_player_name = excluded.assigned_character_player_name;";

        BindItemParameters(cmd, item);
        cmd.ExecuteNonQuery();
    }

    public static bool UpdateItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var id = (item.Id ?? string.Empty).Trim();
        if (id.Length == 0)
            return false;

        item.Id = id;
        item.CreatedDate = EnsureCreatedDate(item.CreatedDate);

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
UPDATE {ItemsTableName} SET
    item_type = @item_type,
    maker_id = @maker_id,
    maker_name = @maker_name,
    maker_player_name = @maker_player_name,
    maker_class = @maker_class,
    maker_race = @maker_race,
    maker_race_subtype = @maker_race_subtype,
    maker_race_subtype_key = @maker_race_subtype_key,
    maker_notes = @maker_notes,
    maker_points = @maker_points,
    witness_name = @witness_name,
    recipient_player_name = @recipient_player_name,
    recipient_character_name = @recipient_character_name,
    recipient_character_class = @recipient_character_class,
    description = @description,
    payload_json = @payload_json,
    isp = @isp,
    created_date = @created_date,
    does_not_blow_up_on_death = @does_not_blow_up_on_death,
    assigned_character_id = @assigned_character_id,
    assigned_character_name = @assigned_character_name,
    assigned_character_player_name = @assigned_character_player_name
WHERE id = @id;";

        BindItemParameters(cmd, item);
        var affected = cmd.ExecuteNonQuery();
        return affected > 0;
    }

    public static bool DeleteItem(string id)
    {
        var normalizedId = (id ?? string.Empty).Trim();
        if (normalizedId.Length == 0)
            return false;

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {ItemsTableName} WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", normalizedId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static void DeleteChar(string id)
    {
        var normalizedId = (id ?? string.Empty).Trim();
        if (normalizedId.Length == 0)
            return;

        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();

        using (var deleteCharacter = conn.CreateCommand())
        {
            deleteCharacter.Transaction = tx;
            deleteCharacter.CommandText = $"DELETE FROM {CharactersTableName} WHERE id = @id;";
            deleteCharacter.Parameters.AddWithValue("@id", normalizedId);
            deleteCharacter.ExecuteNonQuery();
        }

        using (var clearAssignments = conn.CreateCommand())
        {
            clearAssignments.Transaction = tx;
            clearAssignments.CommandText = $@"
UPDATE {ItemsTableName}
SET assigned_character_id = '',
    assigned_character_name = '',
    assigned_character_player_name = ''
WHERE assigned_character_id = @id;";
            clearAssignments.Parameters.AddWithValue("@id", normalizedId);
            clearAssignments.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static string NormalizeKey(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public static void RepairItemsCollection()
    {
        // Legacy LiteDB corruption-recovery hook. SQLite storage no longer needs collection-level repair.
        using var conn = OpenConnection();
        _ = conn;
    }

    public static IEnumerable<Item> GetTemplatesByCharacterId(string characterId)
    {
        var normalizedCharacterId = NormalizeKey(characterId ?? string.Empty);
        if (normalizedCharacterId.Length == 0)
            return Enumerable.Empty<Item>();

        var items = GetItems()
            .Where(item => string.Equals(
                NormalizeKey(item.Maker?.Id ?? string.Empty),
                normalizedCharacterId,
                StringComparison.Ordinal))
            .OrderByDescending(item => item.CreatedDate)
            .ToList();

        return items
            .DistinctBy(item => $"{item.ItemType}::{NormalizeKey(item.Description)}")
            .OrderBy(item => item.ItemType)
            .ThenBy(item => NormalizeKey(item.Description));
    }

    public static IEnumerable<Item> GetItems()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
SELECT
    id,
    item_type,
    maker_id,
    maker_name,
    maker_player_name,
    maker_class,
    maker_race,
    maker_race_subtype,
    maker_race_subtype_key,
    maker_notes,
    maker_points,
    witness_name,
    recipient_player_name,
    recipient_character_name,
    recipient_character_class,
    description,
    payload_json,
    isp,
    created_date,
    does_not_blow_up_on_death,
    assigned_character_id,
    assigned_character_name,
    assigned_character_player_name
FROM {ItemsTableName}
ORDER BY datetime(created_date) DESC, rowid DESC;";

        using var reader = cmd.ExecuteReader();
        var results = new List<Item>();
        while (reader.Read())
            results.Add(ReadItem(reader));

        return results;
    }

    public static IEnumerable<Item> GetItemsAssignedToCharacter(
        string? characterRecordId,
        string? characterName,
        string? playerName)
    {
        var recordId = NormalizeKey(characterRecordId ?? string.Empty);
        var name = NormalizeKey(characterName ?? string.Empty);
        var player = NormalizeKey(playerName ?? string.Empty);

        if (recordId.Length == 0 && name.Length == 0)
            return Enumerable.Empty<Item>();

        return GetItems()
            .Where(item =>
            {
                var assignedId = NormalizeKey(item.AssignedCharacterId ?? string.Empty);
                if (recordId.Length > 0 && assignedId.Length > 0)
                    return string.Equals(assignedId, recordId, StringComparison.Ordinal);

                if (name.Length == 0)
                    return false;

                var assignedName = NormalizeKey(item.AssignedCharacterName ?? string.Empty);
                if (!string.Equals(assignedName, name, StringComparison.Ordinal))
                    return false;

                if (player.Length == 0)
                    return true;

                var assignedPlayer = NormalizeKey(item.AssignedCharacterPlayerName ?? string.Empty);
                return string.Equals(assignedPlayer, player, StringComparison.Ordinal);
            })
            .OrderByDescending(item => item.CreatedDate)
            .ToList();
    }

    public static IEnumerable<Character> GetCharacters()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
SELECT
    id,
    name,
    race,
    race_subtype,
    race_subtype_key,
    class,
    player_name,
    notes,
    draft_snapshot,
    updated_utc,
    points,
    guilds_json,
    points_apps_json,
    specialisations_json
FROM {CharactersTableName}
ORDER BY name COLLATE NOCASE ASC, rowid ASC;";

        using var reader = cmd.ExecuteReader();
        var results = new List<Character>();
        while (reader.Read())
            results.Add(ReadCharacter(reader));

        return results;
    }

    public static Character? GetCharacterById(string id)
    {
        var normalizedId = (id ?? string.Empty).Trim();
        if (normalizedId.Length == 0)
            return null;

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
SELECT
    id,
    name,
    race,
    race_subtype,
    race_subtype_key,
    class,
    player_name,
    notes,
    draft_snapshot,
    updated_utc,
    points,
    guilds_json,
    points_apps_json,
    specialisations_json
FROM {CharactersTableName}
WHERE id = @id
LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", normalizedId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadCharacter(reader) : null;
    }

    public static void UpsertCharacter(Character c)
    {
        ArgumentNullException.ThrowIfNull(c);

        c.Id = EnsureId(c.Id);
        c.UpdatedUtc = DateTime.UtcNow;

        using var conn = OpenConnection();
        UpsertCharacter(conn, c);
    }

    public static Character UpsertDraft(CharacterDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        using var conn = OpenConnection();

        var requestedId = (draft.CharacterRecordId ?? string.Empty).Trim();
        string? idOverride = null;
        if (requestedId.Length > 0 && CharacterExists(conn, requestedId))
            idOverride = requestedId;

        var entity = MapFromDraft(draft, idOverride);

        try
        {
            UpsertCharacter(conn, entity);
        }
        catch (Exception ex)
        {
            Helpers.RuntimeLog.Write(
                "SQLITE_UPSERT_DRAFT",
                $"Failed upserting character draft. Character='{draft.Name}' Player='{draft.PlayerName}' RecordId='{draft.CharacterRecordId}'.",
                ex);
            throw;
        }

        draft.CharacterRecordId = entity.Id;
        return entity;
    }

    private static Character MapFromDraft(CharacterDraft draft, string? idOverride)
    {
        return new Character
        {
            Id = EnsureId(idOverride),
            Name = draft.Name ?? string.Empty,
            PlayerName = draft.PlayerName ?? string.Empty,
            Class = draft.Class ?? string.Empty,
            Race = draft.Race ?? string.Empty,
            RaceSubtype = draft.RaceSubtype ?? string.Empty,
            RaceSubtypeKey = draft.RaceSubtypeKey ?? string.Empty,
            Notes = draft.Notes ?? string.Empty,
            Guilds = new List<string>(draft.Guilds ?? new List<string>()),
            Specialisations = new Dictionary<string, string>(draft.SpecialisationSelections, StringComparer.OrdinalIgnoreCase),
            Points = draft.Points,
            DraftSnapshot = JsonSerializer.Serialize(draft),
            UpdatedUtc = DateTime.UtcNow
        };
    }

    public static CharacterDraft? ToDraft(Character character)
    {
        if (character == null)
            return null;

        if (!string.IsNullOrWhiteSpace(character.DraftSnapshot))
        {
            try
            {
                var draft = JsonSerializer.Deserialize<CharacterDraft>(character.DraftSnapshot);
                if (draft != null)
                {
                    draft.CharacterRecordId = character.Id;
                    foreach (var kvp in character.Specialisations ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
                        draft.SpecialisationSelections[kvp.Key] = kvp.Value;
                    return draft;
                }
            }
            catch
            {
                // Fall back to manual draft reconstruction.
            }
        }

        var reconstructed = new CharacterDraft
        {
            CharacterRecordId = character.Id,
            Name = character.Name ?? string.Empty,
            PlayerName = character.PlayerName ?? string.Empty,
            Class = character.Class ?? string.Empty,
            Race = character.Race ?? string.Empty,
            RaceSubtype = character.RaceSubtype ?? string.Empty,
            RaceSubtypeKey = character.RaceSubtypeKey ?? string.Empty,
            Notes = character.Notes ?? string.Empty,
            Points = (int)character.Points
        };

        reconstructed.Guilds = character.Guilds?.ToList() ?? new List<string>();
        foreach (var kvp in character.Specialisations ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            reconstructed.SpecialisationSelections[kvp.Key] = kvp.Value;

        return reconstructed;
    }

    private static void BindItemParameters(SqliteCommand cmd, Item item)
    {
        var maker = item.Maker ?? new Character();

        cmd.Parameters.AddWithValue("@id", EnsureId(item.Id));
        cmd.Parameters.AddWithValue("@item_type", (int)item.ItemType);
        cmd.Parameters.AddWithValue("@maker_id", (maker.Id ?? string.Empty).Trim());
        cmd.Parameters.AddWithValue("@maker_name", maker.Name ?? string.Empty);
        cmd.Parameters.AddWithValue("@maker_player_name", maker.PlayerName ?? string.Empty);
        cmd.Parameters.AddWithValue("@maker_class", maker.Class ?? string.Empty);
        cmd.Parameters.AddWithValue("@maker_race", maker.Race ?? string.Empty);
        cmd.Parameters.AddWithValue("@maker_race_subtype", maker.RaceSubtype ?? string.Empty);
        cmd.Parameters.AddWithValue("@maker_race_subtype_key", maker.RaceSubtypeKey ?? string.Empty);
        cmd.Parameters.AddWithValue("@maker_notes", maker.Notes ?? string.Empty);
        cmd.Parameters.AddWithValue("@maker_points", maker.Points);
        cmd.Parameters.AddWithValue("@witness_name", item.WitnessName ?? string.Empty);
        cmd.Parameters.AddWithValue("@recipient_player_name", item.RecipientPlayerName ?? string.Empty);
        cmd.Parameters.AddWithValue("@recipient_character_name", item.RecipientCharacterName ?? string.Empty);
        cmd.Parameters.AddWithValue("@recipient_character_class", item.RecipientCharacterClass ?? string.Empty);
        cmd.Parameters.AddWithValue("@description", item.Description ?? string.Empty);
        cmd.Parameters.AddWithValue("@payload_json", item.PayloadJson ?? string.Empty);
        cmd.Parameters.AddWithValue("@isp", item.Isp);
        cmd.Parameters.AddWithValue("@created_date", ToStorageDate(item.CreatedDate));
        cmd.Parameters.AddWithValue("@does_not_blow_up_on_death", item.DoesNotBlowUpOnDeath ? 1 : 0);
        cmd.Parameters.AddWithValue("@assigned_character_id", item.AssignedCharacterId ?? string.Empty);
        cmd.Parameters.AddWithValue("@assigned_character_name", item.AssignedCharacterName ?? string.Empty);
        cmd.Parameters.AddWithValue("@assigned_character_player_name", item.AssignedCharacterPlayerName ?? string.Empty);

        item.Maker = maker;
        item.Id = EnsureId(item.Id);
    }

    private static Character ReadCharacter(SqliteDataReader reader)
    {
        return new Character
        {
            Id = ReadString(reader, "id"),
            Name = ReadString(reader, "name"),
            Race = ReadString(reader, "race"),
            RaceSubtype = ReadString(reader, "race_subtype"),
            RaceSubtypeKey = ReadString(reader, "race_subtype_key"),
            Class = ReadString(reader, "class"),
            PlayerName = ReadString(reader, "player_name"),
            Notes = ReadString(reader, "notes"),
            DraftSnapshot = ReadString(reader, "draft_snapshot"),
            UpdatedUtc = ParseStorageDate(ReadString(reader, "updated_utc"), DateTime.UtcNow),
            Points = ReadInt64(reader, "points"),
            Guilds = DeserializeStringList(ReadString(reader, "guilds_json")),
            PointsApps = DeserializeStringList(ReadString(reader, "points_apps_json")),
            Specialisations = DeserializeStringDictionary(ReadString(reader, "specialisations_json"))
        };
    }

    private static Item ReadItem(SqliteDataReader reader)
    {
        return new Item
        {
            Id = ReadString(reader, "id"),
            ItemType = ReadItemType(ReadInt32(reader, "item_type")),
            Maker = new Character
            {
                Id = ReadString(reader, "maker_id"),
                Name = ReadString(reader, "maker_name"),
                PlayerName = ReadString(reader, "maker_player_name"),
                Class = ReadString(reader, "maker_class"),
                Race = ReadString(reader, "maker_race"),
                RaceSubtype = ReadString(reader, "maker_race_subtype"),
                RaceSubtypeKey = ReadString(reader, "maker_race_subtype_key"),
                Notes = ReadString(reader, "maker_notes"),
                Points = ReadInt64(reader, "maker_points")
            },
            WitnessName = ReadString(reader, "witness_name"),
            RecipientPlayerName = ReadString(reader, "recipient_player_name"),
            RecipientCharacterName = ReadString(reader, "recipient_character_name"),
            RecipientCharacterClass = ReadString(reader, "recipient_character_class"),
            Description = ReadString(reader, "description"),
            PayloadJson = ReadString(reader, "payload_json"),
            Isp = ReadInt32(reader, "isp"),
            CreatedDate = ParseStorageDate(ReadString(reader, "created_date"), DateTime.Now),
            DoesNotBlowUpOnDeath = ReadInt32(reader, "does_not_blow_up_on_death") != 0,
            AssignedCharacterId = ReadString(reader, "assigned_character_id"),
            AssignedCharacterName = ReadString(reader, "assigned_character_name"),
            AssignedCharacterPlayerName = ReadString(reader, "assigned_character_player_name")
        };
    }

    private static ItemTypeEnum ReadItemType(int raw)
        => Enum.IsDefined(typeof(ItemTypeEnum), raw) ? (ItemTypeEnum)raw : ItemTypeEnum.None;

    private static bool CharacterExists(SqliteConnection conn, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM {CharactersTableName} WHERE id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteScalar() != null;
    }

    private static void UpsertCharacter(SqliteConnection conn, Character c)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
INSERT INTO {CharactersTableName}
(
    id,
    name,
    race,
    race_subtype,
    race_subtype_key,
    class,
    player_name,
    notes,
    draft_snapshot,
    updated_utc,
    points,
    guilds_json,
    points_apps_json,
    specialisations_json
)
VALUES
(
    @id,
    @name,
    @race,
    @race_subtype,
    @race_subtype_key,
    @class,
    @player_name,
    @notes,
    @draft_snapshot,
    @updated_utc,
    @points,
    @guilds_json,
    @points_apps_json,
    @specialisations_json
)
ON CONFLICT(id) DO UPDATE SET
    name = excluded.name,
    race = excluded.race,
    race_subtype = excluded.race_subtype,
    race_subtype_key = excluded.race_subtype_key,
    class = excluded.class,
    player_name = excluded.player_name,
    notes = excluded.notes,
    draft_snapshot = excluded.draft_snapshot,
    updated_utc = excluded.updated_utc,
    points = excluded.points,
    guilds_json = excluded.guilds_json,
    points_apps_json = excluded.points_apps_json,
    specialisations_json = excluded.specialisations_json;";

        cmd.Parameters.AddWithValue("@id", c.Id);
        cmd.Parameters.AddWithValue("@name", c.Name ?? string.Empty);
        cmd.Parameters.AddWithValue("@race", c.Race ?? string.Empty);
        cmd.Parameters.AddWithValue("@race_subtype", c.RaceSubtype ?? string.Empty);
        cmd.Parameters.AddWithValue("@race_subtype_key", c.RaceSubtypeKey ?? string.Empty);
        cmd.Parameters.AddWithValue("@class", c.Class ?? string.Empty);
        cmd.Parameters.AddWithValue("@player_name", c.PlayerName ?? string.Empty);
        cmd.Parameters.AddWithValue("@notes", c.Notes ?? string.Empty);
        cmd.Parameters.AddWithValue("@draft_snapshot", c.DraftSnapshot ?? string.Empty);
        cmd.Parameters.AddWithValue("@updated_utc", ToStorageDate(c.UpdatedUtc));
        cmd.Parameters.AddWithValue("@points", c.Points);
        cmd.Parameters.AddWithValue("@guilds_json", SerializeStringList(c.Guilds));
        cmd.Parameters.AddWithValue("@points_apps_json", SerializeStringList(c.PointsApps));
        cmd.Parameters.AddWithValue("@specialisations_json", SerializeStringDictionary(c.Specialisations));

        cmd.ExecuteNonQuery();
    }

    private static SqliteConnection OpenConnection()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, ServiceHelper.DbFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? FileSystem.AppDataDirectory);
        EnsureWritableDatabaseFiles(dbPath);
        try
        {
            return OpenConnectionInternal(dbPath);
        }
        catch (SqliteException ex) when (IsReadonlySqlite(ex))
        {
            Helpers.RuntimeLog.Write(
                "SQLITE_RO_RECOVER",
                $"Readonly SQLite write failure at '{dbPath}'. FileState: {DescribeFileState(dbPath)}. Retrying with writable clone.",
                ex);
            RecoverWritableDatabase(dbPath);
            return OpenConnectionInternal(dbPath);
        }
    }

    private static void EnsureWritableDatabaseFiles(string dbPath)
    {
        EnsureWritableFile(dbPath);
        EnsureWritableFile(dbPath + "-wal");
        EnsureWritableFile(dbPath + "-shm");
    }

    private static void EnsureWritableFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == 0)
                return;

            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
        catch (Exception ex)
        {
            Helpers.RuntimeLog.Write("SQLITE_FILE_ATTR", $"Failed ensuring writable file attributes for '{path}'.", ex);
        }
    }

    private static SqliteConnection OpenConnectionInternal(string dbPath)
    {
        var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadWriteCreate");
        conn.Open();
        EnsureSchema(conn);
        return conn;
    }

    private static bool IsReadonlySqlite(SqliteException ex)
        => ex.SqliteErrorCode == 8
           || ex.SqliteExtendedErrorCode == 8
           || ex.Message.Contains("readonly", StringComparison.OrdinalIgnoreCase);

    private static void RecoverWritableDatabase(string dbPath)
    {
        var backupPath = dbPath + ".rwcopy";
        try
        {
            EnsureWritableDatabaseFiles(dbPath);

            if (File.Exists(dbPath))
            {
                using (var source = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var target = new FileStream(backupPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    source.CopyTo(target);
                }

                EnsureWritableFile(dbPath);
                File.Delete(dbPath);
                File.Move(backupPath, dbPath, true);
            }

            EnsureWritableDatabaseFiles(dbPath);
            Helpers.RuntimeLog.Write(
                "SQLITE_RO_RECOVER",
                $"SQLite writable recovery completed for '{dbPath}'. NewState: {DescribeFileState(dbPath)}");
        }
        catch (Exception ex)
        {
            Helpers.RuntimeLog.Write(
                "SQLITE_RO_RECOVER",
                $"SQLite writable recovery failed for '{dbPath}'. State: {DescribeFileState(dbPath)}",
                ex);
        }
        finally
        {
            try
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch
            {
                // best effort
            }
        }
    }

    private static string DescribeFileState(string dbPath)
    {
        try
        {
            var exists = File.Exists(dbPath);
            var attributes = exists ? File.GetAttributes(dbPath).ToString() : "<missing>";
            var length = exists ? new FileInfo(dbPath).Length : 0;
            var dir = Path.GetDirectoryName(dbPath) ?? FileSystem.AppDataDirectory;
            var dirWritable = CanWriteDirectory(dir);
            return $"exists={exists}, attrs={attributes}, length={length}, dir={dir}, dirWritable={dirWritable}";
        }
        catch (Exception ex)
        {
            return $"state-error:{ex.GetType().Name}:{ex.Message}";
        }
    }

    private static bool CanWriteDirectory(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            var probe = Path.Combine(directoryPath, $".laby_rw_probe_{Guid.NewGuid():N}");
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureSchema(SqliteConnection conn)
    {
        if (_schemaEnsured)
            return;

        lock (SchemaSync)
        {
            if (_schemaEnsured)
                return;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {CharactersTableName}
(
    id TEXT PRIMARY KEY NOT NULL,
    name TEXT NOT NULL DEFAULT '',
    race TEXT NOT NULL DEFAULT '',
    race_subtype TEXT NOT NULL DEFAULT '',
    race_subtype_key TEXT NOT NULL DEFAULT '',
    class TEXT NOT NULL DEFAULT '',
    player_name TEXT NOT NULL DEFAULT '',
    notes TEXT NOT NULL DEFAULT '',
    draft_snapshot TEXT NOT NULL DEFAULT '',
    updated_utc TEXT NOT NULL DEFAULT '',
    points INTEGER NOT NULL DEFAULT 0,
    guilds_json TEXT NOT NULL DEFAULT '[]',
    points_apps_json TEXT NOT NULL DEFAULT '[]',
    specialisations_json TEXT NOT NULL DEFAULT '{{}}'
);

CREATE TABLE IF NOT EXISTS {ItemsTableName}
(
    id TEXT PRIMARY KEY NOT NULL,
    item_type INTEGER NOT NULL DEFAULT 0,
    maker_id TEXT NOT NULL DEFAULT '',
    maker_name TEXT NOT NULL DEFAULT '',
    maker_player_name TEXT NOT NULL DEFAULT '',
    maker_class TEXT NOT NULL DEFAULT '',
    maker_race TEXT NOT NULL DEFAULT '',
    maker_race_subtype TEXT NOT NULL DEFAULT '',
    maker_race_subtype_key TEXT NOT NULL DEFAULT '',
    maker_notes TEXT NOT NULL DEFAULT '',
    maker_points INTEGER NOT NULL DEFAULT 0,
    witness_name TEXT NOT NULL DEFAULT '',
    recipient_player_name TEXT NOT NULL DEFAULT '',
    recipient_character_name TEXT NOT NULL DEFAULT '',
    recipient_character_class TEXT NOT NULL DEFAULT '',
    description TEXT NOT NULL DEFAULT '',
    payload_json TEXT NOT NULL DEFAULT '',
    isp INTEGER NOT NULL DEFAULT 0,
    created_date TEXT NOT NULL DEFAULT '',
    does_not_blow_up_on_death INTEGER NOT NULL DEFAULT 0,
    assigned_character_id TEXT NOT NULL DEFAULT '',
    assigned_character_name TEXT NOT NULL DEFAULT '',
    assigned_character_player_name TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS idx_wallet_characters_name
    ON {CharactersTableName}(name COLLATE NOCASE);

CREATE INDEX IF NOT EXISTS idx_wallet_items_created_date
    ON {ItemsTableName}(created_date DESC);

CREATE INDEX IF NOT EXISTS idx_wallet_items_assigned_character_id
    ON {ItemsTableName}(assigned_character_id);

CREATE INDEX IF NOT EXISTS idx_wallet_items_assigned_character_name
    ON {ItemsTableName}(assigned_character_name COLLATE NOCASE);

CREATE INDEX IF NOT EXISTS idx_wallet_items_assigned_character_player_name
    ON {ItemsTableName}(assigned_character_player_name COLLATE NOCASE);

CREATE INDEX IF NOT EXISTS idx_wallet_items_maker_id
    ON {ItemsTableName}(maker_id);";

            cmd.ExecuteNonQuery();
            _schemaEnsured = true;
        }
    }

    private static string EnsureId(string? raw)
    {
        var id = (raw ?? string.Empty).Trim();
        return id.Length == 0 ? Guid.NewGuid().ToString("N") : id;
    }

    private static DateTime EnsureCreatedDate(DateTime value)
        => value == default ? DateTime.Now : value;

    private static string ToStorageDate(DateTime value)
        => (value == default ? DateTime.UtcNow : value).ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseStorageDate(string? raw, DateTime fallback)
    {
        if (DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse(raw, out parsed))
            return parsed;

        return fallback;
    }

    private static string ReadString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
            return string.Empty;

        return reader.GetString(ordinal);
    }

    private static int ReadInt32(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
            return 0;

        var value = reader.GetValue(ordinal);
        return value switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }

    private static long ReadInt64(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
            return 0;

        var value = reader.GetValue(ordinal);
        return value switch
        {
            long l => l,
            int i => i,
            _ => long.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }

    private static string SerializeStringList(IEnumerable<string>? values)
        => JsonSerializer.Serialize((values ?? Enumerable.Empty<string>())
            .Select(v => v ?? string.Empty)
            .ToList());

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)
                   ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string SerializeStringDictionary(IDictionary<string, string>? values)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (values != null)
        {
            foreach (var pair in values)
            {
                var key = (pair.Key ?? string.Empty).Trim();
                if (key.Length == 0)
                    continue;

                dict[key] = pair.Value ?? string.Empty;
            }
        }

        return JsonSerializer.Serialize(dict);
    }

    private static Dictionary<string, string> DeserializeStringDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                         ?? new Dictionary<string, string>();
            return new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
