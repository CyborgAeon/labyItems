using System.Collections.Generic;
using System.Linq;
using labyItems.Models;
using labyItems.Models.Enums;
using labyItems.Pages.Configs;

namespace labyItems.Services;

/// <summary>
/// Centralized helpers to build detail dictionaries and human-readable summaries
/// from config objects. Keeps CalcResult creation consistent across pages.
/// </summary>
public static class ConfigSummaryService
{
    public static CalcResult BuildWeaponResult(WeaponConfig cfg)
    {
        var details = new Dictionary<string, object?> { ["base"] = cfg.WeaponBase.ToString() };
        var lines = new List<string> { $"Base: {cfg.WeaponBase}" };

        if (!string.IsNullOrWhiteSpace(cfg.WeaponTypeText))
        {
            details["type"] = cfg.WeaponTypeText;
            lines.Add($"Type: {cfg.WeaponTypeText}");
        }

        if (cfg.IsMagicBase && cfg.MagicalColoursCount > 0)
        {
            details["magicalColours"] = cfg.MagicalColoursCount;
            lines.Add($"Magical colours: {cfg.MagicalColoursCount}");
        }
        if (cfg.IsSpiritBase)
        {
            details["spiritual"] = true;
            lines.Add("Spiritual base");
        }
        if (cfg.SpiritTurnsPureDailyCount > 0)
        {
            details["spiritTurnsPureDaily"] = true;
            lines.Add($"Spirit turns pure per day: {cfg.SpiritTurnsPureDailyCount}");
        }
        if (cfg.MagicTurnsPureDailyCount > 0)
        {
            details["magicTurnsPureDaily"] = true;
            lines.Add($"Magic turns pure per day: {cfg.MagicTurnsPureDailyCount}");
        }
        if (cfg.ManticTurnsPureDailyCount > 0)
        {
            details["manticTurnsPureDaily"] = true;
            lines.Add($"Mantic turns pure per day: {cfg.ManticTurnsPureDailyCount}");
        }
        if (cfg.AdventurePermDamageDailyCount > 0)
        {
            details["adventurePermDamageDaily"] = true;
            lines.Add($"Adventure perm damage per day: {cfg.AdventurePermDamageDailyCount}");
        }
        if (cfg.ThruPacAlways)
        {
            details["thruPacAlways"] = true;
            lines.Add("Inflicts damage thru PAC (always)");
        }
        if (cfg.BladeSharpenTooGreat)
        {
            details["bladeSharpenTooGreat"] = true;
            lines.Add("Oversized weapon can be blade sharpened");
        }
        if (cfg.SupernaturalBladeSharpen)
        {
            details["supernaturalBladeSharpen"] = true;
            lines.Add("Supernatural weapon can be blade sharpened");
        }
        if (cfg.CutThroughAuraDaily > 0)
        {
            details["cutThroughAuraDaily"] = true;
            lines.Add($"Cut through Aura of Defence per day: {cfg.CutThroughAuraDaily}");
        }

        AddStringDetail(cfg.MagicVsType, "magicVsType", "Magic vs type", details, lines);
        AddStringDetail(cfg.MagicVsGroup, "magicVsGroup", "Magic vs group", details, lines);
        AddStringDetail(cfg.SpiritVsType, "spiritVsType", "Spirit vs type", details, lines);
        AddStringDetail(cfg.SpiritVsGroup, "spiritVsGroup", "Spirit vs group", details, lines);

        if ((cfg.IsMagicBase || cfg.IsManticBase) && cfg.ExtraColoursCount > 1)
        {
            details["extraColours"] = cfg.ExtraColoursSummary;
            lines.Add($"Extra colours: {cfg.ExtraColoursSummary}");
        }
        if ((cfg.IsSpiritBase || cfg.IsManticBase) && cfg.ExtraAlignmentsCount > 1)
        {
            details["extraAlignments"] = cfg.ExtraAlignmentsSummary;
            lines.Add($"Extra alignments: {cfg.ExtraAlignmentsSummary}");
        }

        var summary = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));

        return new CalcResult
        {
            AbilityType = "Weapon",
            AbilityName = cfg.WeaponBase.ToString(),
            TotalIsp = cfg.Total,
            Details = details,
            Summary = summary
        };
    }

    public static CalcResult BuildArmourResult(ArmourConfig cfg)
    {
        var details = new Dictionary<string, object?>();
        var lines = new List<string>();

        var kind = cfg.SelectedArmour switch
        {
            SupernaturalTypes.Magic => "Magical MC Armour",
            SupernaturalTypes.Spirit => "Spiritual MC Armour",
            SupernaturalTypes.Mantic => "Mantic MC Armour",
            _ => "No Armour"
        };

        if (cfg.ACBase > 0)
        {
            details["AC"] = cfg.ACBase;
            lines.Add($"AC: {cfg.ACBase} ({kind})");
        }

        if (cfg.MagicalColoursCount > 0)
        {
            details["magicalColours"] = cfg.MagicalColoursCount;
            lines.Add($"Magical colours: {cfg.MagicalColoursCount}");
        }

        if (cfg.SpiritualNonOpposite)
        {
            details["spiritualNonOpposite"] = true;
            lines.Add("Spiritual alignment (non-opposite)");
        }

        AppendIfPositive(cfg.PAC, "PAC", details, lines);
        AppendIfPositive(cfg.DAC, "DAC", details, lines);
        AppendIfPositive(cfg.MAC, "MAC", details, lines);
        AppendIfPositive(cfg.SAC, "SAC", details, lines);

        var summary = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));

        return new CalcResult
        {
            AbilityType = "Armour",
            AbilityName = kind,
            TotalIsp = cfg.Total,
            Details = details,
            Summary = summary
        };
    }

    private static void AddStringDetail(string? value, string key, string label, Dictionary<string, object?> details, List<string> lines)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        details[key] = value;
        lines.Add($"{label}: {value}");
    }

    private static void AppendIfPositive(int value, string label, Dictionary<string, object?> details, List<string> lines)
    {
        if (value <= 0) return;
        details[label.ToLower()] = value;
        lines.Add($"{label}: {value}");
    }
}
