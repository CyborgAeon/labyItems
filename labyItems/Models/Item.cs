using System.Collections.Generic;
namespace labyItems.Models;

public class Character
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string RaceSubtype { get; set; } = string.Empty;
    public string RaceSubtypeKey { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string DraftSnapshot { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public string AvatarStorageKind { get; set; } = string.Empty;
    public string AvatarPersistentReference { get; set; } = string.Empty;
    public string AvatarSourceUri { get; set; } = string.Empty;
    public string AvatarAccessReference { get; set; } = string.Empty;
    public string AvatarDisplayPath { get; set; } = string.Empty;
    public string AvatarFileName { get; set; } = string.Empty;
    public string AvatarContentType { get; set; } = string.Empty;
    public List<string> Guilds { get; set; } = new();
    public Dictionary<string, string> Specialisations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public long Points { get; set; } = 0;
    public List<string> PointsApps { get; set; } = new List<string>();
    public MakeSheet? MakeSheet { get; set; }
    public List<Item> ItemTemplates {get;set;   } = new List<Item>();
}

public class Item
{
    public string Id { get; set; } = string.Empty;

    public ItemTypeEnum ItemType { get; set; }
    public Character Maker { get; set; } = new();
    public string WitnessName { get; set; } = string.Empty;
    public string RecipientPlayerName { get; set; } = string.Empty;
    public string RecipientCharacterName { get; set; } = string.Empty;
    public string RecipientCharacterClass { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public int Isp { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public bool DoesNotBlowUpOnDeath { get; set; }
    public string AssignedCharacterId { get; set; } = string.Empty;
    public string AssignedCharacterName { get; set; } = string.Empty;
    public string AssignedCharacterPlayerName { get; set; } = string.Empty;
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
