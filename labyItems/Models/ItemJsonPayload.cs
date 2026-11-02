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
