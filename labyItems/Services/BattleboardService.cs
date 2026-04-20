using ClosedXML.Excel;
using labyItems.Helpers;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Services.Specialisations;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace labyItems.Services;

public interface IBattleboardExportService
{
    Task<string> ExportAsync(CharacterDraft draft, CancellationToken ct = default);
    // Task<string> ExportPdfAsync(CharacterDraft draft, CancellationToken ct = default);
}

public sealed class BattleboardExportService : IBattleboardExportService
{
    private readonly Func<CharacterDraft, IEnumerable<Item>>? _assignedItemsResolver;

    public BattleboardExportService(Func<CharacterDraft, IEnumerable<Item>>? assignedItemsResolver = null)
    {
        _assignedItemsResolver = assignedItemsResolver;
    }

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
        var assignedItems = (_assignedItemsResolver?.Invoke(draft)
                             ?? BattleboardInnateCalculator.ResolveAssignedItems(draft))
            .ToList();
        var supplementalSpecialisationAbilities = await ResolveSelectedSpecialisationAbilitiesAsync(draft);
        var lifeTotals = BattleboardLifeCalculator.Calculate(draft, assignedItems);
        var itemArmour = BattleboardArmourCalculator.Calculate(draft, assignedItems);
        var resolvedInnates = MergeInnates(
            BattleboardInnateCalculator.Calculate(draft, assignedItems),
            supplementalSpecialisationAbilities);
        var abilityEffects = await BattleboardAbilityEffectResolver.ResolveAsync(draft);
        var advancementEffects = await BattleboardAdvancementEffectResolver.ResolveAsync(draft.AdvancementAbilities);
        var itemEffects = await BattleboardItemEffectResolver.ResolveAsync(draft, assignedItems);
        var effectiveResistanceLevels = BattleboardResistanceLevelService.BuildBaselineRawLevels(
            BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(
                BattleboardAdvancementEffectResolver.ApplyResistanceOverrides(
                    draft.ResistanceLevels,
                    abilityEffects.ResistanceOverrides),
                advancementEffects.ResistanceOverrides),
            itemEffects.ResistanceOverrides);

        var resistanceMultipliers = new Dictionary<string, int>(BattleboardAdvancementEffectResolver.ApplyResistanceMultipliers(
                BattleboardAdvancementEffectResolver.ApplyResistanceMultipliers(
                    abilityEffects.ResistanceMultipliers,
                    advancementEffects.ResistanceMultipliers),
                itemEffects.ResistanceMultipliers),
            StringComparer.OrdinalIgnoreCase);
        var resistancePerSixths = new Dictionary<string, int>(BattleboardAdvancementEffectResolver.ApplyResistancePerSixths(
                BattleboardAdvancementEffectResolver.ApplyResistancePerSixths(
                    abilityEffects.ResistancePerSixths,
                    advancementEffects.ResistancePerSixths),
                itemEffects.ResistancePerSixths),
            StringComparer.OrdinalIgnoreCase);
        var infiniteResistanceTypes = new HashSet<string>(
            BattleboardAdvancementEffectResolver.MergeInfiniteResistanceTypes(
                BattleboardAdvancementEffectResolver.MergeInfiniteResistanceTypes(
                    abilityEffects.InfiniteResistanceTypes,
                    advancementEffects.InfiniteResistanceTypes),
                itemEffects.InfiniteResistanceTypes),
            StringComparer.OrdinalIgnoreCase);
        var displayedResistanceLevels = BattleboardResistanceLevelService.BuildDisplayedLevels(
            effectiveResistanceLevels,
            resistanceMultipliers,
            infiniteResistanceTypes,
            resistancePerSixths);
        var effectiveMaxAc = await MaxAcResolver.ResolveEffectiveForDraftAsync(draft);

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
        int pac = Math.Min(effectiveMaxAc, itemArmour.WornPac + armourBonus.Pac);
        int dac = armourBonus.Dac + itemArmour.ItemDac;
        int mac = armourBonus.Mac + itemArmour.ItemMac;
        int sac = armourBonus.Sac + itemArmour.ItemSac;
        int acShown = Math.Min(dac + pac, effectiveMaxAc);
        ws.Cell("B2").Value = draft.Name;
        ws.Cell("C3").Value = lifeTotals.TotalTblp;
        ws.Cell("U3").Value = pac;
        ws.Cell("U4").Value = dac;
        ws.Cell("AD3").Value = effectiveMaxAc;

        if (mac > 0) ws.Cell("U5").Value = mac;
        if (sac > 0) ws.Cell("U6").Value = sac;

        foreach (var addr in new[] { "W3", "S8", "AB8", "V8", "V17", "V25", "Y25" })
            ws.Cell(addr).Value = lifeTotals.TotalLoc;
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
        ws.Cell("T37").Value = CharacterDisplayNameHelper.BuildClassDisplayName(draft);
        ws.Cell("AA35").Value = CharacterDisplayNameHelper.BuildRaceDisplayName(draft, draft.Abilities);
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
        var atWillAbilities = (draft.Abilities ?? new List<AbilityDraft>())
            .Concat(supplementalSpecialisationAbilities)
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
        resistanceAbilities.AddRange(FormatResistanceOverrideEntries(abilityEffects.ResistanceOverrides));
        resistanceAbilities.AddRange(FormatResistanceOverrideEntries(advancementEffects.ResistanceOverrides));
        resistanceAbilities.AddRange(FormatResistanceOverrideEntries(itemEffects.ResistanceOverrides));
        resistanceAbilities.AddRange((draft.AdvancementAbilities ?? new List<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => name.Contains("resistance", StringComparison.OrdinalIgnoreCase)));
        resistanceAbilities = resistanceAbilities
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        WriteResistancesBlock(ws, resistanceAbilities);

        if (draft.ResistancesByType.TryGetValue("Spirit", out var sp) && !string.IsNullOrWhiteSpace(sp))
        {
            ws.Cell("AD33").Value = sp;
        }

        // Resistance levels block (physical/magic/neuronic/spirit)
        if (displayedResistanceLevels != null)
        {
            if (displayedResistanceLevels.TryGetValue("Physical", out var phys))
                ws.Cell("AD20").Value = FormatResistanceCellValue(phys);
            if (displayedResistanceLevels.TryGetValue("Magic", out var magic))
                ws.Cell("AD21").Value = FormatResistanceCellValue(magic);
            if (displayedResistanceLevels.TryGetValue("Neuronic", out var neuronic))
                ws.Cell("AD22").Value = FormatResistanceCellValue(neuronic);
            else if (displayedResistanceLevels.TryGetValue("Neuro", out var neuro))
                ws.Cell("AD22").Value = FormatResistanceCellValue(neuro);
            if (displayedResistanceLevels.TryGetValue("Spirit", out var spirit))
                ws.Cell("AD23").Value = FormatResistanceCellValue(spirit);
        }

        var immunityAbilities = draft.Abilities
            .Where(IsImmunityAbility)
            .Select(FormatAbilityText)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(StripImmunityPrefix)
            .Concat((abilityEffects.Immunities ?? Array.Empty<string>())
                .Select(StripImmunityPrefix))
            .Concat((advancementEffects.Immunities ?? Array.Empty<string>())
                .Select(StripImmunityPrefix))
            .Concat((itemEffects.Immunities ?? Array.Empty<string>())
                .Select(StripImmunityPrefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
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
        WriteInnates(
            ws,
            resolvedInnates,
            nameColumn: innateConfig.NameColumn,
            startRow: innateConfig.StartRow,
            endRow: innateConfig.EndRow);

        WriteNotes(ws, draft.Notes, startColumn: "R", endColumn: "AD", startRow: 43, endRow: 53);

        var advancementDamageTexts = await ResolveAdvancementDamageTextsAsync(draft.AdvancementAbilities);
        WriteDamageSheet(wb, draft, assignedItems, advancementDamageTexts);

        var outPath = Path.Combine(FileSystem.CacheDirectory, $"Battleboard_{Sanitize(draft.Name)}.xlsx");
        wb.SaveAs(outPath);
        return outPath;
    }

    private static IEnumerable<string> FormatResistanceOverrideEntries(IReadOnlyDictionary<string, int>? overrides)
    {
        foreach (var pair in overrides ?? new Dictionary<string, int>())
        {
            var type = BattleboardAdvancementEffectResolver.NormalizeResistanceType(pair.Key);
            var level = Math.Max(0, pair.Value);
            if (type.Length == 0 || level <= 0)
                continue;

            yield return $"{level}th Level Resistance to {type}";
        }
    }

    private static async Task<List<AbilityDraft>> ResolveSelectedSpecialisationAbilitiesAsync(CharacterDraft draft)
    {
        if (draft?.SpecialisationSelections == null || draft.SpecialisationSelections.Count == 0)
            return new List<AbilityDraft>();

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        var lookup = await AbilityDefinitionLookupService.GetLookupAsync();
        var resolved = new List<AbilityDraft>();
        var achievedTable = CharacterProgressionTables.GetHighestTableReached(draft.Points);

        foreach (var selection in draft.SpecialisationSelections)
        {
            if (!TryResolveSpecialisationDefinition(index.Definitions, selection.Key, out var definition))
                continue;

            foreach (var grant in definition.PassiveGrants ?? Array.Empty<AbilityGrant>())
                AddGrantedAbilityDraft(resolved, grant, lookup, achievedTable);

            var selectedTokens = ParseSelectionTokens(selection.Value);
            if (selectedTokens.Count == 0)
                continue;

            foreach (var choiceSet in definition.ChoiceSets ?? Array.Empty<SpecialisationChoiceSet>())
            {
                foreach (var option in choiceSet.Options ?? Array.Empty<ChoiceOption>())
                {
                    if (!SelectionMatchesOption(selectedTokens, option))
                        continue;

                    foreach (var grant in option.Grants ?? Array.Empty<AbilityGrant>())
                        AddGrantedAbilityDraft(resolved, grant, lookup, achievedTable);
                }
            }
        }

        return resolved;
    }

    private static List<InnateAbilityDraft> MergeInnates(
        IReadOnlyList<InnateAbilityDraft> baseInnates,
        IReadOnlyList<AbilityDraft> supplementalAbilities)
    {
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void AddInnate(string? rawName, int rank)
        {
            var name = (rawName ?? string.Empty).Trim();
            if (name.Length == 0 || rank <= 0)
                return;

            totals[name] = totals.TryGetValue(name, out var existing)
                ? existing + rank
                : rank;
        }

        foreach (var innate in baseInnates ?? Array.Empty<InnateAbilityDraft>())
            AddInnate(innate?.Name, innate?.Rank ?? 0);

        foreach (var ability in supplementalAbilities ?? Array.Empty<AbilityDraft>())
        {
            if (ability?.AbilityType != AbilityType.Innate)
                continue;

            var name = !string.IsNullOrWhiteSpace(ability.BattleboardNameOverride)
                ? ability.BattleboardNameOverride
                : ability.Name;
            var rank = Math.Max(1, AbilityDraftBuilder.ResolveInnateRank(ability, achievedLevel: 8));
            AddInnate(name, rank);
        }

        return totals
            .Where(kvp => kvp.Value > 0)
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new InnateAbilityDraft
            {
                Name = kvp.Key,
                Rank = kvp.Value
            })
            .ToList();
    }

    private static bool TryResolveSpecialisationDefinition(
        IReadOnlyDictionary<string, SpecialisationDefinition> definitions,
        string? selectionKey,
        out SpecialisationDefinition definition)
    {
        definition = null!;

        var raw = (selectionKey ?? string.Empty).Trim();
        if (raw.Length == 0 || definitions.Count == 0)
            return false;

        if (definitions.TryGetValue(raw, out var direct) && direct != null)
        {
            definition = direct;
            return true;
        }

        var wanted = AbilityDefinitionLookupService.NormalizeKey(raw);
        foreach (var pair in definitions)
        {
            if (AbilityDefinitionLookupService.NormalizeKey(pair.Key).Equals(wanted, StringComparison.OrdinalIgnoreCase))
            {
                definition = pair.Value;
                return true;
            }

            if (AbilityDefinitionLookupService.NormalizeKey(pair.Value.Key).Equals(wanted, StringComparison.OrdinalIgnoreCase))
            {
                definition = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static List<string> ParseSelectionTokens(string? raw)
    {
        return (raw ?? string.Empty)
            .Split(new[] { '|', ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool SelectionMatchesOption(IReadOnlyCollection<string> selectedTokens, ChoiceOption option)
    {
        foreach (var token in selectedTokens)
        {
            if (token.Equals(option.Key, StringComparison.OrdinalIgnoreCase)
                || token.Equals(option.Label, StringComparison.OrdinalIgnoreCase)
                || AbilityDefinitionLookupService.NormalizeKey(token)
                    .Equals(AbilityDefinitionLookupService.NormalizeKey(option.Key), StringComparison.OrdinalIgnoreCase)
                || AbilityDefinitionLookupService.NormalizeKey(token)
                    .Equals(AbilityDefinitionLookupService.NormalizeKey(option.Label), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddGrantedAbilityDraft(
        ICollection<AbilityDraft> target,
        AbilityGrant? grant,
        IReadOnlyDictionary<string, AbilityDefinition> lookup,
        int achievedTable)
    {
        var definition = ResolveGrantedAbilityDefinition(grant, lookup);
        if (definition == null)
            return;

        foreach (var draft in AbilityDraftBuilder.ParseAbility(
                     definition,
                     levelGained: grant?.Level,
                     achievedLevel: 8,
                     tableGained: grant?.Table,
                     achievedTable: achievedTable))
        {
            if (draft == null)
                continue;

            target.Add(draft);
        }
    }

    private static AbilityDefinition? ResolveGrantedAbilityDefinition(
        AbilityGrant? grant,
        IReadOnlyDictionary<string, AbilityDefinition> lookup)
    {
        var candidate = grant?.Ability;
        if (candidate == null)
            return null;

        if (!string.IsNullOrWhiteSpace(candidate.Name)
            || !string.IsNullOrWhiteSpace(candidate.Type)
            || !string.IsNullOrWhiteSpace(candidate.Effect))
        {
            return candidate;
        }

        return AbilityDefinitionLookupService.Find(
            lookup,
            candidate.AbilityRef ?? candidate.Key ?? candidate.Name);
    }

    private static XLCellValue FormatResistanceCellValue(int level)
        => level == int.MaxValue ? "\u221E" : level;

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

    private static bool IsImmunityAbility(AbilityDraft ability)
    {
        if (ability == null)
            return false;

        if (ability.AbilityType == AbilityType.Immunity)
            return true;

        var text = (ability.Name ?? ability.ShortStringValue ?? string.Empty).Trim();
        return text.StartsWith("Immunity to ", StringComparison.OrdinalIgnoreCase)
               || text.StartsWith("Total Immunity to ", StringComparison.OrdinalIgnoreCase);
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

    private static string StripImmunityPrefix(string text)
{        var value = (text ?? string.Empty).Trim();
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

    private sealed record WeaponMasteryContribution(
        string WeaponType,
        string Label,
        int Value,
        int? Ordinal,
        bool IsFirst);

    private sealed record StrengthContribution(
        string Label,
        int Value,
        int? GradeOrdinal);

    private sealed record ItemWeaponBonusContribution(
        string WeaponType,
        string Label,
        int Value);

    private sealed record DamageTableDefinition(
        string Key,
        string DisplayName,
        int BaseValue,
        bool IsBastard);

    private static readonly Regex NumberedWeaponMasteryRegex = new(
        @"(?<ord>\d+)(?:st|nd|rd|th)\s+weapon\s+mastery(?:\s*\((?<type>[^)]+)\)|\s+(?<type2>[A-Za-z][A-Za-z\s\/\-\']+))?",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex FlatWeaponMasteryRegex = new(
        @"\+\s*(?<value>\d+)\s*(?:cumulative\s+)?weapon\s+mastery(?:\s+with)?\s*(?<type>[A-Za-z][A-Za-z\s\/\-\']+)?",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex GenericWeaponMasteryRegex = new(
        @"\bweapon\s+mastery\b(?:\s*\((?<type>[^)]+)\)|\s+(?<type2>[A-Za-z][A-Za-z\s\/\-\']+))?",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex GradeOfStrengthRegex = new(
        @"(?<ord>\d+)(?:st|nd|rd|th)\s+grade\s+of\s+strength",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex PlusStrengthRegex = new(
        @"\+\s*(?<value>\d+)\s*(?:str|strength)\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static readonly Regex BonusValueRegex = new(
        @"\+\s*(?<value>\d+)",
        RegexOptionsCompat.ForRuntime(RegexOptions.IgnoreCase | RegexOptions.Compiled));

    private static async Task<List<string>> ResolveAdvancementDamageTextsAsync(IEnumerable<string>? advancementAbilities)
    {
        var result = new List<string>();
        var lookup = await AbilityDetailsLookupService.GetLookupAsync();

        foreach (var raw in advancementAbilities ?? Array.Empty<string>())
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0)
                continue;

            result.Add(text);
            var resolved = AbilityDetailsLookupService.FindByIndex(lookup, text);
            var display = (resolved?.Index ?? string.Empty).Trim();
            if (display.Length > 0
                && !display.Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(display);
            }
        }

        return result;
    }

    private static void WriteDamageSheet(
        XLWorkbook workbook,
        CharacterDraft draft,
        IEnumerable<Item> assignedItems,
        IReadOnlyList<string> advancementDamageTexts)
    {
        var existing = workbook.Worksheets
            .FirstOrDefault(sheet => sheet.Name.Equals("damage sheet", StringComparison.OrdinalIgnoreCase));
        existing?.Delete();

        var ws = workbook.Worksheets.Add("damage sheet");
        ws.Cell("A1").Value = "Damage Sheet";
        ws.Cell("A2").Value = "Character";
        ws.Cell("B2").Value = draft.Name;
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A2").Style.Font.Bold = true;

        var masteryContributions = CollectWeaponMasteryContributions(draft, advancementDamageTexts, assignedItems);
        var gradeStrengthContributions = CollectGradeOfStrengthContributions(draft, advancementDamageTexts, assignedItems);
        var itemStrengthContributions = CollectItemStrengthContributions(assignedItems);
        var itemWeaponBonusContributions = CollectItemWeaponBonusContributions(assignedItems);

        var tables = new Dictionary<string, DamageTableDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var mastery in masteryContributions)
            AddDamageTable(tables, ResolveDamageTable(mastery.WeaponType));
        foreach (var itemBonus in itemWeaponBonusContributions)
            AddDamageTable(tables, ResolveDamageTable(itemBonus.WeaponType));

        int row = 4;
        if (tables.Count == 0)
        {
            ws.Cell(row, 1).Value = "No weapon damage modifiers found.";
            ws.Columns(1, 4).AdjustToContents();
            return;
        }

        foreach (var table in tables.Values.OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            ws.Cell(row, 1).Value = table.DisplayName;
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = "Source";
            ws.Cell(row, 2).Value = "Bonus";
            ws.Cell(row, 3).Value = "Result";
            ws.Range(row, 1, row, 3).Style.Font.Bold = true;
            row++;

            var running = 0;
            running = WriteDamageRow(
                ws,
                row++,
                "BWT Base",
                table.BaseValue,
                table.IsBastard,
                running);

            var tableMasteries = masteryContributions
                .Where(entry => MatchesDamageTable(entry.WeaponType, table.Key))
                .OrderBy(entry => entry.IsFirst ? 0 : (entry.Ordinal.HasValue ? 1 : 2))
                .ThenBy(entry => entry.Ordinal ?? int.MaxValue)
                .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var firstMasteryApplied = false;
            var numberedMasteries = new HashSet<int>();
            foreach (var mastery in tableMasteries)
            {
                if (mastery.IsFirst)
                {
                    if (firstMasteryApplied)
                        continue;
                    firstMasteryApplied = true;
                }
                else if (mastery.Ordinal is { } ordinal && ordinal > 1)
                {
                    if (!numberedMasteries.Add(ordinal))
                        continue;
                }

                running = WriteDamageRow(
                    ws,
                    row++,
                    mastery.Label,
                    Math.Max(0, mastery.Value),
                    table.IsBastard,
                    running);
            }

            var firstStrengthApplied = false;
            var numberedStrengths = new HashSet<int>();
            foreach (var strength in gradeStrengthContributions
                         .OrderBy(entry => entry.GradeOrdinal ?? int.MaxValue)
                         .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase))
            {
                if (strength.GradeOrdinal is { } gradeOrdinal)
                {
                    if (gradeOrdinal == 1)
                    {
                        if (firstStrengthApplied)
                            continue;
                        firstStrengthApplied = true;
                    }
                    else if (gradeOrdinal > 1)
                    {
                        if (!numberedStrengths.Add(gradeOrdinal))
                            continue;
                    }
                }

                running = WriteDamageRow(
                    ws,
                    row++,
                    strength.Label,
                    Math.Max(0, strength.Value),
                    table.IsBastard,
                    running);
            }

            foreach (var itemStrength in itemStrengthContributions)
            {
                running = WriteDamageRow(
                    ws,
                    row++,
                    itemStrength.Label,
                    Math.Max(0, itemStrength.Value),
                    table.IsBastard,
                    running);
            }

            foreach (var itemBonus in itemWeaponBonusContributions
                         .Where(entry => MatchesDamageTable(entry.WeaponType, table.Key))
                         .OrderBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase))
            {
                running = WriteDamageRow(
                    ws,
                    row++,
                    itemBonus.Label,
                    Math.Max(0, itemBonus.Value),
                    table.IsBastard,
                    running);
            }

            ws.Cell(row, 1).Value = "Total";
            ws.Cell(row, 2).Value = running;
            ws.Cell(row, 3).Value = FormatDamageResult(running, table.IsBastard);
            ws.Range(row, 1, row, 3).Style.Font.Bold = true;
            row += 2;
        }

        ws.Columns(1, 4).AdjustToContents();
    }

    private static int WriteDamageRow(
        IXLWorksheet ws,
        int row,
        string label,
        int bonus,
        bool isBastard,
        int runningTotal)
    {
        var updatedTotal = runningTotal + Math.Max(0, bonus);
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 2).Value = Math.Max(0, bonus);
        ws.Cell(row, 3).Value = FormatDamageResult(updatedTotal, isBastard);
        return updatedTotal;
    }

    private static void AddDamageTable(
        IDictionary<string, DamageTableDefinition> tables,
        DamageTableDefinition table)
    {
        if (table.Key.Length == 0)
            return;

        if (tables.ContainsKey(table.Key))
            return;

        tables[table.Key] = table;
    }

    private static bool MatchesDamageTable(string weaponType, string tableKey)
        => ResolveDamageTable(weaponType).Key.Equals(tableKey, StringComparison.OrdinalIgnoreCase);

    private static List<WeaponMasteryContribution> CollectWeaponMasteryContributions(
        CharacterDraft draft,
        IReadOnlyList<string> advancementDamageTexts,
        IEnumerable<Item> assignedItems)
    {
        var entries = new List<WeaponMasteryContribution>();

        foreach (var ability in draft.Abilities ?? new List<AbilityDraft>())
        {
            if (ability == null)
                continue;

            if (TryParseWeaponMasteryContribution((ability.BattleboardNameOverride ?? string.Empty).Trim(), out var fromOverride))
            {
                entries.Add(fromOverride);
                continue;
            }

            if (TryParseWeaponMasteryContribution((ability.Name ?? string.Empty).Trim(), out var fromName))
            {
                entries.Add(fromName);
                continue;
            }

            if (TryParseWeaponMasteryContribution((ability.Effect ?? string.Empty).Trim(), out var fromEffect))
                entries.Add(fromEffect);
        }

        foreach (var advancement in advancementDamageTexts ?? Array.Empty<string>())
        {
            if (TryParseWeaponMasteryContribution(advancement, out var parsed))
                entries.Add(parsed);
        }

        foreach (var item in assignedItems ?? Array.Empty<Item>())
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            foreach (var ability in payload?.Item?.Abilities ?? new List<CalcResult>())
            {
                foreach (var candidate in EnumerateItemTextCandidates(ability))
                {
                    if (TryParseWeaponMasteryContribution(candidate, out var parsed))
                        entries.Add(parsed);
                }
            }
        }

        return entries;
    }

    private static List<StrengthContribution> CollectGradeOfStrengthContributions(
        CharacterDraft draft,
        IReadOnlyList<string> advancementDamageTexts,
        IEnumerable<Item> assignedItems)
    {
        var entries = new List<StrengthContribution>();

        foreach (var ability in draft.Abilities ?? new List<AbilityDraft>())
        {
            if (TryParseGradeOfStrengthContribution(ability?.BattleboardNameOverride, out var fromOverride))
            {
                entries.Add(fromOverride);
                continue;
            }

            if (TryParseGradeOfStrengthContribution(ability?.Name, out var fromName))
            {
                entries.Add(fromName);
                continue;
            }

            if (TryParseGradeOfStrengthContribution(ability?.Effect, out var fromEffect))
                entries.Add(fromEffect);
        }

        foreach (var advancement in advancementDamageTexts ?? Array.Empty<string>())
        {
            if (TryParseGradeOfStrengthContribution(advancement, out var parsed))
                entries.Add(parsed);
        }

        foreach (var item in assignedItems ?? Array.Empty<Item>())
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            foreach (var ability in payload?.Item?.Abilities ?? new List<CalcResult>())
            {
                foreach (var candidate in EnumerateItemTextCandidates(ability))
                {
                    if (TryParseGradeOfStrengthContribution(candidate, out var parsed))
                        entries.Add(parsed);
                }
            }
        }

        return entries;
    }

    private static List<StrengthContribution> CollectItemStrengthContributions(IEnumerable<Item> assignedItems)
    {
        var entries = new List<StrengthContribution>();

        foreach (var item in assignedItems ?? Array.Empty<Item>())
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            foreach (var ability in payload?.Item?.Abilities ?? new List<CalcResult>())
            {
                var addedFromAbility = false;
                foreach (var candidate in EnumerateItemTextCandidates(ability))
                {
                    if (!TryParseFlatStrengthContribution(candidate, out var parsed))
                        continue;

                    entries.Add(parsed);
                    addedFromAbility = true;
                    break;
                }

                if (addedFromAbility)
                    continue;
            }
        }

        return entries;
    }

    private static List<ItemWeaponBonusContribution> CollectItemWeaponBonusContributions(IEnumerable<Item> assignedItems)
    {
        var entries = new List<ItemWeaponBonusContribution>();

        foreach (var item in assignedItems ?? Array.Empty<Item>())
        {
            var payload = ItemEmailService.TryDeserializeItemPayload(item?.PayloadJson);
            foreach (var ability in payload?.Item?.Abilities ?? new List<CalcResult>())
            {
                if (ability == null)
                    continue;

                var type = (ability.AbilityType ?? string.Empty).Trim();
                if (!type.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
                    continue;

                var bonus = ResolveItemWeaponBonus(ability);
                if (bonus <= 0)
                    continue;

                var weaponType = ResolveItemWeaponType(ability);
                var label = $"Item Weapon Bonus (+{bonus} {weaponType})";
                entries.Add(new ItemWeaponBonusContribution(weaponType, label, bonus));
            }
        }

        return entries;
    }

    private static IEnumerable<string> EnumerateItemTextCandidates(CalcResult? ability)
    {
        if (ability == null)
            yield break;

        var abilityName = (ability.AbilityName ?? string.Empty).Trim();
        if (abilityName.Length > 0)
            yield return abilityName;

        var summary = (ability.Summary ?? string.Empty).Trim();
        if (summary.Length > 0)
            yield return summary;

        foreach (var selected in ExtractSelectedGeneralAbilityNames(ability))
            yield return selected;
    }

    private static IEnumerable<string> ExtractSelectedGeneralAbilityNames(CalcResult ability)
    {
        if (ability?.Details == null
            || !ability.Details.TryGetValue("selectedGeneralAbilities", out var raw)
            || raw == null)
        {
            yield break;
        }

        if (raw is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var entry in element.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    var parsed = (entry.GetString() ?? string.Empty).Trim();
                    if (parsed.Length > 0)
                        yield return parsed;
                    continue;
                }

                if (entry.ValueKind != JsonValueKind.Object)
                    continue;

                var parsedName = ReadJsonProperty(entry, "name");
                if (parsedName.Length > 0)
                    yield return parsedName;
            }

            yield break;
        }

        if (raw is IEnumerable<object> objects)
        {
            foreach (var entry in objects)
            {
                if (entry is Dictionary<string, object?> dict
                    && dict.TryGetValue("name", out var nameValue))
                {
                    var parsed = (nameValue?.ToString() ?? string.Empty).Trim();
                    if (parsed.Length > 0)
                        yield return parsed;
                }
            }
        }
    }

    private static string ReadJsonProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return (value.GetString() ?? string.Empty).Trim();
    }

    private static bool TryParseWeaponMasteryContribution(string? rawText, out WeaponMasteryContribution contribution)
    {
        contribution = default!;
        var text = (rawText ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        if (text.Contains("loss of weapon master", StringComparison.OrdinalIgnoreCase)
            || text.Contains("weapon mastery shield", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var flatMatch = FlatWeaponMasteryRegex.Match(text);
        if (flatMatch.Success)
        {
            if (!int.TryParse(flatMatch.Groups["value"].Value, out var flatValue))
                flatValue = 0;

            if (flatValue <= 0)
                return false;

            var weaponType = ResolveMasteryWeaponType(flatMatch.Groups["type"].Value);
            contribution = new WeaponMasteryContribution(
                weaponType,
                BuildMasteryLabel(text, weaponType),
                flatValue,
                Ordinal: null,
                IsFirst: false);
            return true;
        }

        var numberedMatch = NumberedWeaponMasteryRegex.Match(text);
        if (numberedMatch.Success)
        {
            if (!int.TryParse(numberedMatch.Groups["ord"].Value, out var ordinal))
                ordinal = 0;

            if (ordinal <= 0)
                return false;

            var weaponType = ResolveMasteryWeaponType(numberedMatch.Groups["type"].Value, numberedMatch.Groups["type2"].Value);
            contribution = new WeaponMasteryContribution(
                weaponType,
                BuildMasteryLabel(text, weaponType),
                Value: 1,
                Ordinal: ordinal,
                IsFirst: ordinal == 1);
            return true;
        }

        var genericMatch = GenericWeaponMasteryRegex.Match(text);
        if (!genericMatch.Success)
            return false;

        var genericType = ResolveMasteryWeaponType(genericMatch.Groups["type"].Value, genericMatch.Groups["type2"].Value);
        contribution = new WeaponMasteryContribution(
            genericType,
            BuildMasteryLabel(text, genericType),
            Value: 1,
            Ordinal: null,
            IsFirst: false);
        return true;
    }

    private static string ResolveMasteryWeaponType(params string[] candidates)
    {
        foreach (var candidate in candidates ?? Array.Empty<string>())
        {
            var value = NormalizeWeaponType(candidate);
            if (value.Length > 0)
                return value;
        }

        return "Unspecified Weapon";
    }

    private static string BuildMasteryLabel(string sourceText, string weaponType)
    {
        var label = (sourceText ?? string.Empty).Trim();
        if (label.Length == 0)
            label = "Weapon Mastery";

        if (label.Contains("weapon mastery", StringComparison.OrdinalIgnoreCase)
            && !label.Contains('('))
        {
            if (!label.Contains(weaponType, StringComparison.OrdinalIgnoreCase))
                label = $"{label} ({weaponType})";
        }

        return label;
    }

    private static bool TryParseGradeOfStrengthContribution(string? rawText, out StrengthContribution contribution)
    {
        contribution = default!;
        var text = (rawText ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var match = GradeOfStrengthRegex.Match(text);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["ord"].Value, out var ordinal))
            ordinal = 0;

        if (ordinal <= 0)
            return false;

        var label = text;
        contribution = new StrengthContribution(label, Value: 1, GradeOrdinal: ordinal);
        return true;
    }

    private static bool TryParseFlatStrengthContribution(string? rawText, out StrengthContribution contribution)
    {
        contribution = default!;
        var text = (rawText ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        var match = PlusStrengthRegex.Match(text);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["value"].Value, out var value))
            value = 0;

        if (value <= 0)
            return false;

        contribution = new StrengthContribution(
            $"Item Strength (+{value})",
            value,
            GradeOrdinal: null);
        return true;
    }

    private static int ResolveItemWeaponBonus(CalcResult ability)
    {
        if (ability?.Details != null && ability.Details.TryGetValue("base", out var rawBase))
        {
            var token = (rawBase?.ToString() ?? string.Empty).Trim();
            if (token.Length > 0)
            {
                if (token.Contains("plus2", StringComparison.OrdinalIgnoreCase))
                    return 2;
                if (token.Contains("plus1", StringComparison.OrdinalIgnoreCase))
                    return 1;
                if (token.Contains("0plus1", StringComparison.OrdinalIgnoreCase))
                    return 1;
            }
        }

        foreach (var candidate in EnumerateItemTextCandidates(ability))
        {
            var match = BonusValueRegex.Match(candidate);
            if (!match.Success)
                continue;

            if (int.TryParse(match.Groups["value"].Value, out var parsed) && parsed > 0)
                return parsed;
        }

        return 0;
    }

    private static string ResolveItemWeaponType(CalcResult ability)
    {
        if (ability?.Details != null && ability.Details.TryGetValue("type", out var rawType))
        {
            var parsed = NormalizeWeaponType(rawType?.ToString());
            if (parsed.Length > 0)
                return parsed;
        }

        foreach (var candidate in EnumerateItemTextCandidates(ability))
        {
            var parsed = NormalizeWeaponType(candidate);
            if (parsed.Length > 0 && !parsed.Equals("Unspecified Weapon", StringComparison.OrdinalIgnoreCase))
                return parsed;
        }

        return "Unspecified Weapon";
    }

    private static DamageTableDefinition ResolveDamageTable(string? rawWeaponType)
    {
        var weaponType = NormalizeWeaponType(rawWeaponType);
        if (IsRangedWeaponType(weaponType))
        {
            return new DamageTableDefinition(
                Key: "ranged",
                DisplayName: "Ranged",
                BaseValue: 1,
                IsBastard: false);
        }

        var lower = weaponType.ToLowerInvariant();
        var isBastard = lower.Contains("bastard", StringComparison.Ordinal)
                        || lower.Contains("bstd", StringComparison.Ordinal);
        var isGreat = lower.Contains("great", StringComparison.Ordinal);
        var baseValue = (isBastard || isGreat) ? 2 : 1;

        var key = BuildWeaponTableKey(weaponType);
        return new DamageTableDefinition(
            Key: key,
            DisplayName: weaponType,
            BaseValue: baseValue,
            IsBastard: isBastard);
    }

    private static string BuildWeaponTableKey(string weaponType)
    {
        var normalized = new string((weaponType ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        return normalized.Length == 0 ? "unspecified" : normalized;
    }

    private static bool IsRangedWeaponType(string weaponType)
    {
        var lower = (weaponType ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Length == 0)
            return false;

        return lower.Contains("bow", StringComparison.Ordinal)
               || lower.Contains("crossbow", StringComparison.Ordinal)
               || lower.Contains("blowpipe", StringComparison.Ordinal);
    }

    private static string NormalizeWeaponType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
            return "Unspecified Weapon";

        if (value.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(5).Trim();

        if (value.StartsWith("weapon ", StringComparison.OrdinalIgnoreCase))
            value = value.Substring("weapon ".Length).Trim();

        value = value.Trim('(', ')');
        if (value.Length == 0)
            return "Unspecified Weapon";

        if (value.Contains("sword or dagger", StringComparison.OrdinalIgnoreCase))
            return "Sword or Dagger";

        value = value
            .Replace("only", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("weapons", "weapon", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (value.Equals("O", StringComparison.OrdinalIgnoreCase))
            return "O class";
        if (value.Equals("H", StringComparison.OrdinalIgnoreCase))
            return "H class";
        if (value.Equals("B", StringComparison.OrdinalIgnoreCase))
            return "B class";
        if (value.Equals("MP", StringComparison.OrdinalIgnoreCase))
            return "MP class";

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            && value.Length > 3
            && !value.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 1);
        }

        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value.Length == 0 ? "Unspecified Weapon" : value;
    }

    private static string FormatDamageResult(int value, bool isBastard)
    {
        var word = value switch
        {
            <= 0 => "none",
            1 => "single",
            2 => "double",
            3 => "triple",
            4 => "quad",
            5 => "quin",
            6 => "six",
            7 => "seven",
            8 => "eight",
            9 => "nine",
            10 => "ten",
            11 => "eleven",
            12 => "twelve",
            13 => "thirteen",
            14 => "fourteen",
            15 => "fifteen",
            16 => "sixteen",
            17 => "seventeen",
            18 => "eighteen",
            19 => "nineteen",
            20 => "twenty",
            _ => value.ToString()
        };

        return isBastard ? $"bstd {word}" : word;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "Character" : name;
    }
}
