using ClosedXML.Excel;
using labyItems.Helpers;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace labyItems.Services;

public interface IBattleboardExportService
{
    Task<string> ExportAsync(CharacterDraft draft, CancellationToken ct = default);
    // Task<string> ExportPdfAsync(CharacterDraft draft, CancellationToken ct = default);
}

public sealed class BattleboardExportService : IBattleboardExportService
{
    // public async Task<string> ExportPdfAsync(CharacterDraft draft, CancellationToken ct = default)
    // {
    //     // 1) Generate the XLSX using your current template logic
    //     var xlsxPath = await ExportAsync(draft, ct);

    //     // 2) Convert XLSX -> PDF using Syncfusion renderer
    //     await using var excelFileStream = File.OpenRead(xlsxPath);

    //     using var excelEngine = new ExcelEngine();
    //     var app = excelEngine.Excel;
    //     app.DefaultVersion = ExcelVersion.Xlsx;

    //     using var workbook = app.Workbooks.Open(excelFileStream);

    //     using var renderer = new XlsIORenderer();
    //     using PdfDocument pdfDocument = renderer.ConvertToPDF(workbook);

    //     var pdfPath = Path.Combine(
    //         FileSystem.CacheDirectory,
    //         $"Battleboard_{Sanitize(draft.Name)}.pdf"
    //     );

    //     await using var pdfFileStream = File.Create(pdfPath);
    //     pdfDocument.Save(pdfFileStream);

    //     return pdfPath;
    // }

    public async Task<string> ExportAsync(CharacterDraft draft, CancellationToken ct = default)
    {
        var pools = (draft.PowerPools ?? new Dictionary<string, int>())
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
            .Where(kvp => kvp.Value > 0)
            .ToList();
        var powerCount = pools.Count;
        var templateName = powerCount == 0
            ? "people/NonPowerUser.xlsx"
            : powerCount == 1
                ? "people/PowerUser.xlsx"
                : "people/Vivomancer.xlsx";

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
        ws.Cell("T37").Value = BuildClassDisplayName(draft);
        ws.Cell("AA35").Value = BuildRaceDisplayName(draft, draft.Abilities);
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

        if (draft.ResistancesByType.TryGetValue("Spirit", out var sp) && !string.IsNullOrWhiteSpace(sp))
        {
            ws.Cell("AD33").Value = sp;
        }

        // Resistance levels block (physical/magic/neuronic/spirit)
        if (draft.ResistanceLevels != null)
        {
            if (draft.ResistanceLevels.TryGetValue("Physical", out var phys))
                ws.Cell("AD20").Value = phys;
            if (draft.ResistanceLevels.TryGetValue("Magic", out var magic))
                ws.Cell("AD21").Value = magic;
            if (draft.ResistanceLevels.TryGetValue("Neuronic", out var neuronic))
                ws.Cell("AD22").Value = neuronic;
            else if (draft.ResistanceLevels.TryGetValue("Neuro", out var neuro))
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
            .Where(a => !IsCombatWary(a))
            .Where(a => !IsFaerieColourSelection(a))
            .Where(a => !IsRaceSubtypeSelection(a, draft))
            .Select(FormatAbilityText)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        var pacEntry = BuildInnatePacEntry(armourBonus.Pac);
        if (!string.IsNullOrWhiteSpace(pacEntry))
            staticAbilities.Add(pacEntry);
        WriteStaticAbilities(ws, staticAbilities, startRow: 4, endRow: 54);

        var innateConfig = GetInnatePlacement(templateName, isVivomancer);
        WriteInnates(ws, draft.Innates, nameColumn: innateConfig.NameColumn, startRow: innateConfig.StartRow, endRow: innateConfig.EndRow);

        WriteNotes(ws, draft.Notes, startColumn: "R", endColumn: "AD", startRow: 43, endRow: 53);

        var outPath = Path.Combine(FileSystem.CacheDirectory, $"Battleboard_{Sanitize(draft.Name)}.xlsx");
        wb.SaveAs(outPath);
        return outPath;
    }

    private static readonly Regex ArmourTokenRegex = new(
        @"([+-]?\d+)\s*(PAC|DAC|MAC|SAC)",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static (int Pac, int Dac, int Mac, int Sac) ExtractArmourBonuses(IEnumerable<AbilityDraft> abilities)
    {
        var totals = ArmourBonusResolver.ResolveMaxPerSourceTotals(abilities);
        return (totals.Pac, totals.Dac, totals.Mac, totals.Sac);
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

    private static string BuildInnatePacEntry(int pac)
        => pac > 0 ? $"Innate PAC {pac}" : string.Empty;

    private static bool IsCombatWary(AbilityDraft ability)
    {
        var name = (ability?.Name ?? string.Empty).Trim();
        if (name.Length == 0) return false;

        var normalized = name.Replace("-", " ").Replace("  ", " ").Trim();
        return string.Equals(normalized, "Combat Wary", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Combat-Wary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFaerieColourSelection(AbilityDraft ability)
    {
        var source = (ability?.Source ?? string.Empty).Trim();
        return string.Equals(source, "Specialisation:Faerie Colour", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRaceSubtypeSelection(AbilityDraft ability, CharacterDraft draft)
    {
        if (ability == null || draft == null)
            return false;

        var subtype = (draft.RaceSubtypeValue ?? draft.RaceSubtype ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(subtype))
            return false;

        var name = (ability.Name ?? string.Empty).Trim();
        return string.Equals(name, subtype, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> GetFaerieColourSelections(IEnumerable<AbilityDraft> abilities)
    {
        return (abilities ?? Enumerable.Empty<AbilityDraft>())
            .Where(IsFaerieColourSelection)
            .Select(a => (a?.Name ?? string.Empty).Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildRaceDisplayName(CharacterDraft draft, IEnumerable<AbilityDraft> abilities)
    {
        var race = (draft?.Race ?? string.Empty).Trim();
        var suffixes = new List<string>();

        var subtype = (draft?.RaceSubtypeValue ?? draft?.RaceSubtype ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(subtype) && !string.Equals(subtype, "Standard", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = TrimSubtypeLabel(subtype);
            if (!string.IsNullOrWhiteSpace(trimmed))
                suffixes.Add(trimmed);
        }

        if (string.Equals(race, "Faerie", StringComparison.OrdinalIgnoreCase))
        {
            var faerieColours = GetFaerieColourSelections(abilities);
            foreach (var colour in faerieColours)
            {
                if (!suffixes.Any(s => string.Equals(s, colour, StringComparison.OrdinalIgnoreCase)))
                    suffixes.Add(colour);
            }
        }

        if (suffixes.Count == 0)
            return race;

        if (string.IsNullOrWhiteSpace(race))
            return string.Join(", ", suffixes);

        return $"{race} ({string.Join(", ", suffixes)})";
    }

    private static bool IsElfRace(string race)
    {
        if (string.IsNullOrWhiteSpace(race))
            return false;

        return string.Equals(race, "Elf", StringComparison.OrdinalIgnoreCase)
               || string.Equals(race, "Half Elf", StringComparison.OrdinalIgnoreCase)
               || string.Equals(race, "Half-Elf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryComputeFrequencyRank(AbilityDraft ability, out int rank)
    {
        return AbilityDraftBuilder.TryResolveFrequencySkillRank(ability, out rank, achievedLevel: 8);
    }

    private static string FormatAbilityText(AbilityDraft ability)
    {
        if (ability == null) return string.Empty;

        var name = ability.Name ?? string.Empty;
        var overrideName = ability.BattleboardNameOverride ?? string.Empty;
        var text = !string.IsNullOrWhiteSpace(overrideName) ? overrideName : name;
        if (string.IsNullOrWhiteSpace(text))
            text = ability.ShortStringValue ?? string.Empty;

        if (ability.AbilityType == AbilityType.Immunity)
            text = StripImmunityPrefix(text);

        return text;
    }

    private static string BuildClassDisplayName(CharacterDraft draft)
    {
        var cls = (draft.Class ?? string.Empty).Trim();
        if (cls.Length == 0)
            return cls;

        if (!IsWizardClassName(cls))
            return cls;

        var colour = TryGetWizardColour(draft);
        if (!colour.HasValue)
            return cls;

        var colourName = colour.Value.ToString();
        if (cls.StartsWith(colourName, StringComparison.OrdinalIgnoreCase))
            return cls;

        return $"{colourName} {cls}";
    }

    private static bool IsWizardClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;

        return className.Equals("Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("High-Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("High Wizard", StringComparison.OrdinalIgnoreCase)
               || className.Equals("Warlock", StringComparison.OrdinalIgnoreCase)
               || className.Equals("Rogue", StringComparison.OrdinalIgnoreCase);
    }

    private static MagicColours? TryGetWizardColour(CharacterDraft draft)
    {
        if (draft?.SpecialisationSelections != null)
        {
            var kvp = draft.SpecialisationSelections.FirstOrDefault(x =>
                x.Key.Contains("Wizard Colour", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(x.Value));

            if (!string.IsNullOrWhiteSpace(kvp.Value)
                && Enum.TryParse<MagicColours>(kvp.Value.Trim().Replace(" ", string.Empty), true, out var colour))
                return colour;
        }

        if (draft?.Abilities != null)
        {
            var ability = draft.Abilities.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a?.Source)
                && a.Source.Contains("Specialisation:Wizard Colour", StringComparison.OrdinalIgnoreCase));

            var name = ability?.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name)
                && Enum.TryParse<MagicColours>(name.Trim().Replace(" ", string.Empty), true, out var fromAbility))
                return fromAbility;
        }

        return null;
    }

    private static string StripImmunityPrefix(string text)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length == 0)
            return value;

        const string prefix = "Immunity to ";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value.Substring(prefix.Length).Trim();

        return value;
    }

    private static string TrimSubtypeLabel(string subtype)
    {
        var value = (subtype ?? string.Empty).Trim();
        if (value.Length == 0)
            return value;

        var parenIndex = value.IndexOf('(');
        if (parenIndex >= 0)
            value = value[..parenIndex].Trim();

        if (value.Length == 0)
            return value;

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return value;

        var end = parts.Length;
        while (end > 1 && IsAllLower(parts[end - 1]))
            end--;

        return string.Join(' ', parts.Take(end));
    }

    private static bool IsAllLower(string token)
    {
        var hasLetter = false;
        foreach (var ch in token)
        {
            if (!char.IsLetter(ch))
                continue;

            hasLetter = true;
            if (!char.IsLower(ch))
                return false;
        }

        return hasLetter;
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
