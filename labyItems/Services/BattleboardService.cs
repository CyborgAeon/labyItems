// using ClosedXML.Excel;
using ClosedXML.Excel;
using labyItems.Models.Characters;
namespace labyItems.Services;

public interface IBattleboardExportService
{
    Task<string> ExportAsync(CharacterDraft draft, CancellationToken ct = default);
}

public sealed class BattleboardExportService : IBattleboardExportService
{
    public async Task<string> ExportAsync(CharacterDraft draft, CancellationToken ct = default)
    {
        await using var templateStream = await FileSystem.OpenAppPackageFileAsync("people/Template.xlsx");
        using var ms = new MemoryStream();
        await templateStream.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("BBoard");

        int pac = Math.Min(draft.MaxAC, draft.WornArmour + draft.ClassRaceArmour);
        int acShown = Math.Min(draft.DAC + pac, draft.MaxAC);

        ws.Cell("C3").Value = draft.TBLP;
        ws.Cell("U3").Value = pac;
        ws.Cell("AD3").Value = draft.MaxAC;

        if (draft.SAC is not null) ws.Cell("U5").Value = draft.SAC.Value;
        if (draft.MAC is not null) ws.Cell("U6").Value = draft.MAC.Value;

        foreach (var addr in new[] { "W3", "S8", "AB8", "V8", "V17", "V25", "Y25" })
            ws.Cell(addr).Value = draft.Loc;
        foreach (var addr in new[] { "Z3", "T8", "U8", "AA8", "AC8", "AD8", "AA17", "AA25", "Z25", "X25", "W25" })
            ws.Cell(addr).Value = acShown;

        ws.Cell("B17").Value = draft.PowerType;
        ws.Cell("C17").Value = draft.PowerAmount;
        ws.Cell("T35").Value = draft.PlayerName;
        ws.Cell("T36").Value = draft.Name;
        ws.Cell("T37").Value = draft.Class ?? "";
        ws.Cell("AA35").Value = draft.Race ?? "";
        ws.Cell("AA36").Value = draft.Alignment.ToString();
        ws.Cell("AA37").Value = draft.Points;

        var guildString = string.Join(", ", draft.Guilds.Select(g => "{" + g + "}"));
        ws.Cell("T38").Value = guildString;
        var resistanceAbilities = draft.Abilities
            .Where(a => a.Name.Contains("resistance", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.ShortStringValue)
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
        WriteInnates(ws, draft.Innates);

        var outPath = Path.Combine(FileSystem.CacheDirectory, $"Battleboard_{Sanitize(draft.Name)}.xlsx");
        wb.SaveAs(outPath);
        return outPath;
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

    private static void WriteInnates(IXLWorksheet ws, List<InnateAbilityDraft> innates)
    {
        int startRow = 21;
        int endRow = 54;

        var columns = new[] { "E", "F", "G", "H", "I", "J", "K", "L" };
        var rightToLeft = columns.Reverse().ToArray();

        for (int row = startRow; row <= endRow; row++)
        {
            int idx = row - startRow;

            var innate = idx < innates.Count ? innates[idx] : null;
            ws.Cell($"A{row}").Value = innate?.Name ?? "";

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

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "Character" : name;
    }
}
