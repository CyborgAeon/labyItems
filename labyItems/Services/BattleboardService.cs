// using ClosedXML.Excel;
using ClosedXML.Excel;
using labyItems.Models.Characters;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace labyItems.Services;

public interface IBattleboardExportService
{
    Task<string> ExportAsync(CharacterDraft draft, CancellationToken ct = default);
}

public sealed class BattleboardExportService : IBattleboardExportService
{
    public async Task<string> ExportAsync(CharacterDraft draft, CancellationToken ct = default)
    {
        var pools = (draft.PowerPools ?? new Dictionary<string, int>())
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
            .ToList();
        var powerCount = pools.Count;
        var templateName = powerCount == 0
            ? "people/NonPowerUser.xlsx"
            : powerCount >= 2
                ? "people/Vivomancer.xlsx"
                : "people/PowerUser.xlsx";

        await using var templateStream = await FileSystem.OpenAppPackageFileAsync(templateName);
        using var ms = new MemoryStream();
        await templateStream.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("BBoard");

        var armourBonus = ExtractArmourBonuses(draft.Abilities);
        int pac = Math.Min(draft.MaxAC, draft.WornArmour + armourBonus.Pac);
        int dac = armourBonus.Dac;
        int mac = armourBonus.Mac;
        int sac = armourBonus.Sac;
        int acShown = Math.Min(dac + pac, draft.MaxAC);
        ws.Cell("B2").Value = draft.Name;
        ws.Cell("C3").Value = draft.TBLP;
        ws.Cell("U3").Value = pac;
        ws.Cell("U4").Value = dac;
        ws.Cell("AD3").Value = draft.MaxAC;

        if (mac > 0) ws.Cell("U5").Value = mac;
        if (sac > 0) ws.Cell("U6").Value = sac;

        foreach (var addr in new[] { "W3", "S8", "AB8", "V8", "V17", "V25", "Y25" })
            ws.Cell(addr).Value = draft.Loc;
        foreach (var addr in new[] { "Z3", "T8", "U8", "AA8", "AC8", "AD8", "AA17", "AA25", "Z25", "X25", "W25" })
            ws.Cell(addr).Value = acShown;

        var orderedPools = pools.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).ToList();
        var isVivomancer = templateName.Contains("Vivomancer", StringComparison.OrdinalIgnoreCase);

        if (isVivomancer)
        {
            if (orderedPools.Count > 0)
            {
                ws.Cell("B17").Value = orderedPools[0].Key;
                ws.Cell("C17").Value = orderedPools[0].Value;
            }
            if (orderedPools.Count > 1)
            {
                ws.Cell("B22").Value = orderedPools[1].Key;
                ws.Cell("C22").Value = orderedPools[1].Value;
            }
        }
        else
        {
            var first = orderedPools.FirstOrDefault();
            ws.Cell("B17").Value = first.Key;
            ws.Cell("C17").Value = first.Value;
        }
        ws.Cell("T35").Value = draft.PlayerName;
        ws.Cell("T36").Value = draft.Name;
        ws.Cell("T37").Value = draft.Class ?? "";
        ws.Cell("AA35").Value = draft.Race ?? "";
        ws.Cell("AA36").Value = draft.Alignment.ToString();
        ws.Cell("AA37").Value = draft.Points;

        var guildString = string.Join(", ", draft.Guilds);
        ws.Cell("T38").Value = guildString;

        var combatWary = draft.Abilities.FirstOrDefault(IsCombatWary);
        if (combatWary != null && TryComputeFrequencyRank(combatWary, out var rank) && rank > 0)
        {
            ws.Cell("AA4").Value = "Combat Wary";
            ws.Cell("AD4").Value = rank;
        }
        var atWillAbilities = draft.Abilities
            .Where(a => a.AbilityType == AbilityType.AtWill)
            .Select(FormatAbilityText)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        ws.Cell("R42").Value = string.Join(", ", atWillAbilities);

        var resistanceAbilities = draft.Abilities
            .Where(a => a.AbilityType == AbilityType.Resistance
                        || a.Name.Contains("resistance", StringComparison.OrdinalIgnoreCase))
            .Select(FormatAbilityText)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        WriteResistancesBlock(ws, resistanceAbilities);

        if (draft.ResistancesByType.TryGetValue("Earthpower", out var ep)) ws.Cell("AD20").Value = ep;
        if (draft.ResistancesByType.TryGetValue("Magic", out var маг)) ws.Cell("AD21").Value = маг;
        if (draft.ResistancesByType.TryGetValue("Neuronic", out var neu)) ws.Cell("AD22").Value = neu;

        if (draft.ResistancesByType.TryGetValue("Spirit", out var sp) && !string.IsNullOrWhiteSpace(sp))
        {
            ws.Cell("AD33").Value = sp;
        }

        // Resistance levels block (physical/magic/neuro/spirit)
        if (draft.ResistanceLevels != null)
        {
            if (draft.ResistanceLevels.TryGetValue("Physical", out var phys))
                ws.Cell("AD20").Value = phys;
            if (draft.ResistanceLevels.TryGetValue("Magic", out var magic))
                ws.Cell("AD21").Value = magic;
            if (draft.ResistanceLevels.TryGetValue("Neuro", out var neuro))
                ws.Cell("AD22").Value = neuro;
            if (draft.ResistanceLevels.TryGetValue("Spirit", out var spirit))
                ws.Cell("AD23").Value = spirit;
        }

        var immunityAbilities = draft.Abilities
            .Where(a => a.AbilityType == AbilityType.Immunity)
            .Select(FormatAbilityText)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
        WriteImmunities(ws, immunityAbilities, startRow: 26, endRow: 33);

        var staticAbilities = draft.Abilities
            .Where(a => a.AbilityType == AbilityType.Static)
            .Where(a => !IsPureArmourToken(a))
            .Select(FormatAbilityText)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        foreach (var entry in BuildInnateArmourEntries(armourBonus))
            staticAbilities.Add(entry);
        WriteStaticAbilities(ws, staticAbilities, startRow: 4, endRow: 54);

        var innateConfig = GetInnatePlacement(templateName, isVivomancer);
        WriteInnates(ws, draft.Innates, nameColumn: innateConfig.NameColumn, startRow: innateConfig.StartRow, endRow: innateConfig.EndRow);

        WriteNotes(ws, draft.Notes, startColumn: "R", endColumn: "AD", startRow: 43, endRow: 53);

        var outPath = Path.Combine(FileSystem.CacheDirectory, $"Battleboard_{Sanitize(draft.Name)}.xlsx");
        wb.SaveAs(outPath);
        return outPath;
    }

    private static readonly Regex ArmourTokenRegex = new(@"([+-]?\d+)\s*(PAC|DAC|MAC|SAC)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static (int Pac, int Dac, int Mac, int Sac) ExtractArmourBonuses(IEnumerable<AbilityDraft> abilities)
    {
        int pac = 0, dac = 0, mac = 0, sac = 0;

        foreach (var ability in abilities ?? Enumerable.Empty<AbilityDraft>())
        {
            if (ability == null)
                continue;

            if (TryApplyArmourType(ability, ref pac, ref dac, ref mac, ref sac))
                continue;

            var effect = ability.Effect ?? string.Empty;
            if (TryApplyArmourTokens(effect, ref pac, ref dac, ref mac, ref sac))
                continue;

            var name = ability.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
                TryApplyArmourTokens(name, ref pac, ref dac, ref mac, ref sac);
        }

        return (pac, dac, mac, sac);
    }

    private static bool TryApplyArmourType(AbilityDraft ability, ref int pac, ref int dac, ref int mac, ref int sac)
    {
        if (ability == null)
            return false;

        string? stat = ability.AbilityType switch
        {
            AbilityType.Pac => "PAC",
            AbilityType.Dac => "DAC",
            AbilityType.Mac => "MAC",
            AbilityType.Sac => "SAC",
            _ => null
        };

        if (stat == null)
            return false;

        var value = ability.Count ?? 0;
        if (value == 0)
        {
            var parsed = ParseArmourTokenValue(ability.Effect, stat);
            if (parsed == 0)
                parsed = ParseArmourTokenValue(ability.Name, stat);
            value = parsed;
        }

        if (value == 0)
            return false;

        switch (stat)
        {
            case "PAC":
                pac += value;
                break;
            case "DAC":
                dac += value;
                break;
            case "MAC":
                mac += value;
                break;
            case "SAC":
                sac += value;
                break;
        }

        return true;
    }

    private static int ParseArmourTokenValue(string? text, string stat)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        foreach (Match m in ArmourTokenRegex.Matches(text))
        {
            if (!int.TryParse(m.Groups[1].Value, out var value))
                continue;

            var key = m.Groups[2].Value.ToUpperInvariant();
            if (!string.Equals(key, stat, StringComparison.OrdinalIgnoreCase))
                continue;

            return value;
        }

        return 0;
    }

    private static bool TryApplyArmourTokens(string text, ref int pac, ref int dac, ref int mac, ref int sac)
    {
        var matched = false;
        foreach (Match m in ArmourTokenRegex.Matches(text))
        {
            if (!int.TryParse(m.Groups[1].Value, out var value))
                continue;

            var key = m.Groups[2].Value.ToUpperInvariant();
            matched = true;
            switch (key)
            {
                case "PAC":
                    pac += value;
                    break;
                case "DAC":
                    dac += value;
                    break;
                case "MAC":
                    mac += value;
                    break;
                case "SAC":
                    sac += value;
                    break;
            }
        }

        return matched;
    }

    private static bool IsPureArmourToken(AbilityDraft ability)
    {
        if (ability == null)
            return false;

        return IsPureArmourToken(ability.Name) || IsPureArmourToken(ability.Effect);
    }

    private static bool IsPureArmourToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return ArmourTokenRegex.IsMatch(text.Trim()) && ArmourTokenRegex.Matches(text.Trim()).Count == 1 && ArmourTokenRegex.Replace(text.Trim(), "").Length == 0;
    }

    private static IEnumerable<string> BuildInnateArmourEntries((int Pac, int Dac, int Mac, int Sac) totals)
    {
        var list = new List<string>();
        if (totals.Pac > 0) list.Add($"Innate PAC {totals.Pac}");
        if (totals.Dac > 0) list.Add($"Innate DAC {totals.Dac}");
        if (totals.Mac > 0) list.Add($"Innate MAC {totals.Mac}");
        if (totals.Sac > 0) list.Add($"Innate SAC {totals.Sac}");
        return list;
    }

    private static bool IsCombatWary(AbilityDraft ability)
    {
        var name = (ability?.Name ?? string.Empty).Trim();
        if (name.Length == 0) return false;

        var normalized = name.Replace("-", " ").Replace("  ", " ").Trim();
        return string.Equals(normalized, "Combat Wary", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Combat-Wary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryComputeFrequencyRank(AbilityDraft ability, out int rank)
    {
        rank = 0;
        if (ability == null || !ability.LevelGained.HasValue)
            return false;

        if (!TryParseFrequency(ability.Frequency, out var freq) || freq <= 0)
            return false;

        var remaining = Math.Max(0, 8 - ability.LevelGained.Value);
        rank = remaining / freq;
        return rank > 0;
    }

    private static bool TryParseFrequency(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return int.TryParse(raw.Trim(), out value);
    }

    private static string FormatAbilityText(AbilityDraft ability)
    {
        if (ability == null) return string.Empty;

        var effect = ability.ShortStringValue ?? string.Empty;
        var name = ability.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(effect) && !string.Equals(effect, name, StringComparison.OrdinalIgnoreCase))
            return effect;

        return name;
    }

    private static void WriteResistancesBlock(IXLWorksheet ws, List<string> values)
    {
        int row = 20;
        int i = 0;

        while (row <= 33)
        {
            ws.Cell($"R{row}").Value = i < values.Count ? values[i] : "";
            i++;
            row++;
        }
    }

    private static void WriteImmunities(IXLWorksheet ws, List<string> values, int startRow, int endRow)
    {
        int row = startRow;
        int i = 0;

        while (row <= endRow)
        {
            ws.Cell($"AC{row}").Value = i < values.Count ? values[i] : "";
            i++;
            row++;
        }
    }

    private static void WriteStaticAbilities(IXLWorksheet ws, List<string> values, int startRow, int endRow)
    {
        int row = startRow;
        int i = 0;

        while (row <= endRow)
        {
            ws.Cell($"N{row}").Value = i < values.Count ? values[i] : "";
            i++;
            row++;
        }
    }

    private static (string NameColumn, int StartRow, int EndRow) GetInnatePlacement(string templateName, bool isVivomancer)
    {
        const int endRow = 54;
        var isPowerUser = templateName.Contains("PowerUser", StringComparison.OrdinalIgnoreCase);

        if (isVivomancer || templateName.Contains("Vivomancer", StringComparison.OrdinalIgnoreCase))
            return ("B", 27, endRow);

        if (isPowerUser)
            return ("B", 22, endRow);

        return ("B", 17, endRow);
    }

    private static void WriteInnates(IXLWorksheet ws, List<InnateAbilityDraft> innates, string nameColumn, int startRow, int endRow)
    {
        var columns = new[] { "E", "F", "G", "H", "I", "J", "K", "L" };
        var rightToLeft = columns.Reverse().ToArray();

        for (int row = startRow; row <= endRow; row++)
        {
            int idx = row - startRow;

            var innate = idx < innates.Count ? innates[idx] : null;
            ws.Cell($"{nameColumn}{row}").Value = innate?.Name ?? "";

            foreach (var col in columns)
            {
                ws.Cell($"{col}{row}").Style.Fill.BackgroundColor = XLColor.NoColor;
            }

            int rank = innate?.Rank ?? 0;
            rank = Math.Clamp(rank, 0, 8);

            int toBlack = 8 - rank;
            for (int j = 0; j < toBlack; j++)
            {
                var col = rightToLeft[j];
                ws.Cell($"{col}{row}").Style.Fill.BackgroundColor = XLColor.Black;
            }
        }
    }

    private static void WriteNotes(IXLWorksheet ws, string notes, string startColumn, string endColumn, int startRow, int endRow)
    {
        var lines = (notes ?? string.Empty)
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.None)
            .Select(l => l.TrimEnd())
            .ToList();

        var startColNum = XLHelper.GetColumnNumberFromLetter(startColumn);
        var endColNum = XLHelper.GetColumnNumberFromLetter(endColumn);
        var columns = Enumerable.Range(startColNum, endColNum - startColNum + 1)
            .Select(col => XLHelper.GetColumnLetterFromNumber(col))
            .ToList();

        int row = startRow;
        int colIdx = 0;

        foreach (var line in lines)
        {
            if (row > endRow) break;
            ws.Cell($"{columns[colIdx]}{row}").Value = line;
            colIdx++;
            if (colIdx >= columns.Count)
            {
                colIdx = 0;
                row++;
            }
        }
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "Character" : name;
    }
}
