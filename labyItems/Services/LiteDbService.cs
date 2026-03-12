// Services/LiteDbService.cs
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using labyItems.Models;
using labyItems.Models.Characters;
using LiteDB;

namespace labyItems.Services;

public static class LiteDbService
{
    private static LiteDatabase? _db;
    private static readonly object DbSync = new();

    private static LiteDatabase GetDb()
    {
        if (_db != null)
            return _db;

        lock (DbSync)
        {
            if (_db != null)
                return _db;

            var path = Path.Combine(FileSystem.AppDataDirectory, "items.db");
            _db = OpenDatabaseWithRecovery(path);
            EnsureIndexes(_db);
            return _db;
        }
    }

    private static LiteDatabase OpenDatabaseWithRecovery(string path)
    {
        try
        {
            return new LiteDatabase($"Filename={path};Connection=shared");
        }
        catch (Exception firstOpenEx)
        {
            Debug.WriteLine($"[LiteDbService] Failed opening items.db at '{path}': {firstOpenEx}");
            TryQuarantineBrokenDatabase(path);

            return new LiteDatabase($"Filename={path};Connection=shared");
        }
    }

    private static void EnsureIndexes(LiteDatabase db)
    {
        if (ShouldSkipIndexesForAot())
        {
            Debug.WriteLine("[LiteDbService] Skipping index creation on AOT platform.");
            return;
        }

        try
        {
            db.GetCollection<Item>("items").EnsureIndex(nameof(Item.CreatedDate));
            db.GetCollection<Item>("items").EnsureIndex(nameof(Item.AssignedCharacterName));
            db.GetCollection<Item>("items").EnsureIndex(nameof(Item.AssignedCharacterPlayerName));
            db.GetCollection<Character>("characters").EnsureIndex(nameof(Character.Name));
        }
        catch (TypeInitializationException ex) when ((ex.TypeName ?? string.Empty).Contains("LiteDB.BsonExpression", StringComparison.OrdinalIgnoreCase))
        {
            // LiteDB expression initialization can require JIT; iOS full-AOT cannot support that.
            Debug.WriteLine($"[LiteDbService] Skipping indexes due to AOT-incompatible LiteDB expression init: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiteDbService] Failed creating indexes: {ex.Message}");
        }
    }

    private static bool ShouldSkipIndexesForAot()
    {
        if (OperatingSystem.IsIOS())
            return true;

        return false;
    }

    private static void TryQuarantineBrokenDatabase(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;

            var backupPath = Path.Combine(
                Path.GetDirectoryName(path) ?? FileSystem.AppDataDirectory,
                $"items.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}.db");

            File.Move(path, backupPath, overwrite: true);
            Debug.WriteLine($"[LiteDbService] Moved unreadable items.db to '{backupPath}'.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiteDbService] Could not quarantine unreadable items.db: {ex}");
        }
    }

    public static void InsertItem(Item item) => GetDb().GetCollection<Item>("items").Insert(item);
    public static bool UpdateItem(Item item) => GetDb().GetCollection<Item>("items").Update(item);
    public static bool DeleteItem(ObjectId id) =>
        GetDb().GetCollection<Item>("items").Delete(id);

    public static void DeleteChar(ObjectId id) =>
        GetDb().GetCollection<Character>("characters").Delete(id);

    private static string NormalizeKey(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    public static IEnumerable<Item> GetTemplatesByCharacterId(ObjectId characterId)
    {
        var items = GetDb()
            .GetCollection<Item>("items")
            .FindAll()
            .OrderByDescending(x => x.CreatedDate)
            .ToList();
        return items
            .DistinctBy(x => $"{x.ItemType}::{NormalizeKey(x.Description)}")
            .Where(w => w.Maker.Id == characterId)
            .OrderBy(x => x.ItemType)
            .ThenBy(x => NormalizeKey(x.Description));
    }

    public static IEnumerable<Item> GetItems() =>
        GetDb()
            .GetCollection<Item>("items")
            .FindAll()
            .OrderByDescending(item => item.CreatedDate);

    public static IEnumerable<Item> GetItemsAssignedToCharacter(string? characterName, string? playerName)
    {
        var name = NormalizeKey(characterName ?? string.Empty);
        var player = NormalizeKey(playerName ?? string.Empty);
        if (name.Length == 0)
            return Enumerable.Empty<Item>();

        return GetDb()
            .GetCollection<Item>("items")
            .FindAll()
            .Where(item =>
            {
                var assignedName = NormalizeKey(item.AssignedCharacterName ?? string.Empty);
                if (!string.Equals(assignedName, name, StringComparison.Ordinal))
                    return false;

                if (player.Length == 0)
                    return true;

                var assignedPlayer = NormalizeKey(item.AssignedCharacterPlayerName ?? string.Empty);
                return string.Equals(assignedPlayer, player, StringComparison.Ordinal);
            })
            .OrderByDescending(item => item.CreatedDate);
    }

    public static IEnumerable<Character> GetCharacters() =>
        GetDb().GetCollection<Character>("characters").FindAll().OrderBy(c => c.Name);

    public static Character? GetCharacterById(ObjectId id) =>
        GetDb().GetCollection<Character>("characters").FindById(id);

    public static void UpsertCharacter(Character c)
    {
        var col = GetDb().GetCollection<Character>("characters");
        c.Id = EnsureId(c.Id);
        c.UpdatedUtc = DateTime.UtcNow;
        col.Upsert(c);
    }

    public static Character UpsertDraft(CharacterDraft draft)
    {
        var col = GetDb().GetCollection<Character>("characters");
        ObjectId? idOverride = null;
        var rawId = (draft.CharacterRecordId ?? string.Empty).Trim();
        if (rawId.Length > 0 && TryParseObjectId(rawId, out var parsedId))
        {
            var existing = col.FindById(parsedId);
            if (existing != null)
                idOverride = parsedId;
        }

        var entity = MapFromDraft(draft, idOverride);
        col.Upsert(entity);
        draft.CharacterRecordId = entity.Id.ToString();
        return entity;
    }

    private static Character MapFromDraft(CharacterDraft draft, ObjectId? idOverride)
    {
        return new Character
        {
            Id = EnsureId(idOverride ?? ObjectId.Empty),
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
            DraftSnapshot = System.Text.Json.JsonSerializer.Serialize(draft),
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private static ObjectId EnsureId(ObjectId id)
        => id == ObjectId.Empty ? ObjectId.NewObjectId() : id;

    private static bool TryParseObjectId(string rawId, out ObjectId value)
    {
        try
        {
            value = new ObjectId(rawId);
            return value != ObjectId.Empty;
        }
        catch
        {
            value = ObjectId.Empty;
            return false;
        }
    }

    public static CharacterDraft? ToDraft(Character character)
    {
        if (character == null)
            return null;

        if (!string.IsNullOrWhiteSpace(character.DraftSnapshot))
        {
            try
            {
                var _draft = System.Text.Json.JsonSerializer.Deserialize<CharacterDraft>(character.DraftSnapshot);
                if (_draft != null)
                {
                    _draft.CharacterRecordId = character.Id.ToString();
                    foreach (var kvp in character.Specialisations ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
                        _draft.SpecialisationSelections[kvp.Key] = kvp.Value;
                    return _draft;
                }
            }
            catch { }
        }

        var draft = new CharacterDraft
        {
            CharacterRecordId = character.Id.ToString(),
            Name = character.Name ?? string.Empty,
            PlayerName = character.PlayerName ?? string.Empty,
            Class = character.Class ?? string.Empty,
            Race = character.Race ?? string.Empty,
            RaceSubtype = character.RaceSubtype ?? string.Empty,
            RaceSubtypeKey = character.RaceSubtypeKey ?? string.Empty,
            Notes = character.Notes ?? string.Empty,
            Points = (int)character.Points
        };

        draft.Guilds = character.Guilds?.ToList() ?? new List<string>();
        foreach (var kvp in character.Specialisations ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
            draft.SpecialisationSelections[kvp.Key] = kvp.Value;

        return draft;
    }

}
