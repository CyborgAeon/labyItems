// =============================
// File: Models/MakeSheet.cs
// =============================
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace labyItems.Models
{
    public class MakeSheet
    {
        // Header
        public Character Character { get; set; }
        // Percentages
        public int BaseMakeChancePercent { get; set; } = 90; // defaults to 90%
        public List<BonusEntry> Bonuses { get; set; } = new(); // +% and reason
        public int FinalNormalChancePercent { get; set; } // calculated or user-entered

        // Specific modifiers (for particular makes: talismans, scroll types, etc.)
        public List<SpecificModifier> SpecificModifiers { get; set; } = new();

        // Notes
        public string CostReductionNotes { get; set; } = string.Empty; // e.g., "20% off from Magical Artisan"
        public string LevelLossReductionNotes { get; set; } = string.Empty; // e.g., "½ level loss from Magical Artisan"

        // Owned advance make types (free text summary)
        public string AdvancedMakesOwned { get; set; } = string.Empty; // e.g., "Make Major Magical Talismans; Make Minor Magical Weapons"

        // Other special cases
        public string OtherNotes { get; set; } = string.Empty; // e.g., "Gains an extra make when playing due to A Master of the Trade"

        // Utility: compute normal final chance from base + bonuses (ignoring specific modifiers)
        public int ComputeNormalFinalChance()
        {
            int total = BaseMakeChancePercent;
            foreach (var b in Bonuses)
                total += b?.Percent ?? 0;
            return Math.Clamp(total, 0, 100);
        }
    }

    public class BonusEntry
    {
        public int Percent { get; set; } // e.g., 6
        public string Reason { get; set; } = string.Empty; // e.g., "Magical Artisan"
    }

    public class SpecificModifier
    {
        public string AppliesTo { get; set; } = string.Empty; // e.g., "Talismans" or "Teaching Scrolls"
        public int PercentBonus { get; set; } // e.g., +2
        public string Reason { get; set; } = string.Empty; // e.g., "Craft speciality"
    }

    public record MakeAbility(
        string Name,
        string Availability,
        int Table,
        int Cost,
        bool CanBuyMultiple,
        string Description
    );

    public static class MakesAbilities
    {
        public static readonly Dictionary<string, MakeAbility> Data = FromJson(MakesAbilitiesJson.Json);
        public static Dictionary<string, MakeAbility> FromJson(string json)
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            return JsonSerializer.Deserialize<Dictionary<string, MakeAbility>>(json, opts) ?? new();
        }
    }
}