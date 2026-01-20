using System.Collections.Generic;
namespace labyItems.Models;
using LiteDB;

public class Character {
    [BsonId]
    public ObjectId Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string RaceSubtype { get; set; } = string.Empty;
    public string RaceSubtypeKey { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string DraftSnapshot { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<string> Guilds { get; set; } = new();
    public Dictionary<string, string> Specialisations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public long Points { get; set; } = 0;
    public List<string> PointsApps { get; set; } = new List<string>();
    public MakeSheet? MakeSheet { get; set; }
    public List<Item> ItemTemplates {get;set;   } = new List<Item>();
}

public class Item
{
    [BsonId]
    public ObjectId Id { get; set; }

    public ItemTypeEnum ItemType { get; set; }
    public Character Maker { get; set; } = new();
    public string WitnessName { get; set; } = string.Empty;
    public string RecipientPlayerName { get; set; } = string.Empty;
    public string RecipientCharacterName { get; set; } = string.Empty;
    public string RecipientCharacterClass { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Isp { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public bool DoesNotBlowUpOnDeath { get; set; }
}

public class ItemForm {
    public ItemTypeEnum ItemType { get; set; }
    public string MakerPlayerName { get; set; }          = string.Empty;
    public string MakerCharacterName { get; set; } = string.Empty;
    public string WitnessName { get; set; } = string.Empty;
    public string RecipientPlayerName { get; set; } = string.Empty;
    public string RecipientCharacterName { get; set; } = string.Empty;
    public string RecipientCharacterClass { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Isp { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public bool DoesNotBlowUpOnDeath { get; set; }
}
