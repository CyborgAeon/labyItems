using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace labyItems.Models;

public class ItemJsonPayload
{
    public string WitnessName { get; set; } = string.Empty;
    public RecipientPayload? Recipient { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Isp { get; set; }
    public DateTime CreatedDate { get; set; }
    public ItemJsonDetail Item { get; set; } = new();
}

public class RecipientPayload
{
    public string PlayerName { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string CharacterClass { get; set; } = string.Empty;
}

public class ItemJsonDetail
{
    public List<ItemTypeEnum> Types { get; set; } = new();
    public List<CalcResult> Abilities { get; set; } = new();
    public string Status { get; set; } = "TODO"; // TODO: replace with real status model when available.
    public List<object> Modifiers { get; set; } = new(); // TODO: define Modifier model when it exists.
}
