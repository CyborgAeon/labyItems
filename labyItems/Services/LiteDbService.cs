// Services/LiteDbService.cs
using labyItems.Models;
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
        col.Upsert(c);
    }
}
