// Services/LiteDbService.cs
using System.Linq;
using System.Text.Json;
using labyItems.Models;
using labyItems.Models.Characters;
using LiteDB;

namespace labyItems.Services;

public static class LiteDbService
{
    private static LiteDatabase? _db;

    private static LiteDatabase GetDb()
    {
        if (_db != null)
            return _db;
        var path = Path.Combine(FileSystem.AppDataDirectory, "items.db");
        _db = new LiteDatabase($"Filename={path};Connection=shared");

        _db.GetCollection<Item>("items").EnsureIndex(x => x.CreatedDate);
        _db.GetCollection<Character>("characters").EnsureIndex(x => x.Name);
        return _db;
    }

    // Items
    public static void InsertItem(Item item) => GetDb().GetCollection<Item>("items").Insert(item);

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

    // Characters
    public static IEnumerable<Character> GetCharacters() =>
        GetDb().GetCollection<Character>("characters").FindAll().OrderBy(c => c.Name);

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
        var normalizedName = NormalizeKey(draft.Name);
        var normalizedPlayer = NormalizeKey(draft.PlayerName);

        // LiteDB cannot translate custom helper calls inside LINQ to BsonExpression; fall back to
        // client-side match on normalized name/player to avoid runtime NotSupportedException.
        var existing = col.FindAll()
            .FirstOrDefault(c =>
                NormalizeKey(c.Name) == normalizedName &&
                NormalizeKey(c.PlayerName) == normalizedPlayer);

        var entity = MapFromDraft(draft, existing?.Id);
        col.Upsert(entity);
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

    public static CharacterDraft? ToDraft(Character character)
    {
        if (character == null)
            return null;

        if (!string.IsNullOrWhiteSpace(character.DraftSnapshot))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<CharacterDraft>(character.DraftSnapshot);
            }
            catch
            {
                // fall back to manual mapping below
            }
        }

        var draft = new CharacterDraft
        {
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
