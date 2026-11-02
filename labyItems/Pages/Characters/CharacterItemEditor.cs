using labyItems.Models.Items;
using labyItems.Models;
using labyItems.Models.Characters;
using labyItems.Controls;
using labyItems.Helpers;
using labyItems.Pages.Calculator;
using labyItems.Pages.Characters.ViewModels;
using labyItems.Services;
using System.Text.RegularExpressions;

namespace labyItems.Pages.Characters;

internal sealed class CharacterItemEditor
{
    private readonly Page _owner;
    private readonly AdvanceCharacterVm _vm;
    private readonly Action? _saved;
    public CharacterItemEditor(Page owner, AdvanceCharacterVm vm, Action? saved = null)
    {
        _owner = owner;
        _vm = vm;
        _saved = saved;
    }
    private static readonly Regex ContributionCostRegex = new(
        @"=\s*(?<cost>-?\d+)\s*$",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled | RegexOptions.CultureInvariant));
    private static readonly Regex MonsterPointCastingRegex = new(
        @"^(?<kind>Spell|Miracle|Evocation)\s*:\s*(?<name>.+?)\s*x(?<count>\d+)\s*$",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    private static readonly Regex MonsterPointLifeRegex = new(
        @"^Life\s+(?<life>\d+\s*/\s*\d+)\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    private static readonly Regex MonsterPointArmourTokenRegex = new(
        @"(?<amount>[+-]?\d+)\s*(?<stat>PAC|DAC|MAC|SAC)\b",
        RegexOptionsCompat.ForRuntime(RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    public async Task OpenIspItemCalculatorAsync(Item? existingItem = null)
    {
        var existingAbilities = existingItem == null
            ? null
            : ItemDisplayHelper.GetAbilities(existingItem);

        var calculator = new IspCalculator(
            baseTotal: 0,
            existingAbilities: existingAbilities,
            onSave: async result => await SaveIspItemForCharacterAsync(result, existingItem));
        await _owner.Navigation.PushAsync(calculator);
    }

    public async Task OpenMonsterPointCalculatorAsync(Item? existingItem = null)
    {
        var calculator = new MpCalculator(async payload =>
            await SaveMonsterPointItemForCharacterAsync(payload, existingItem), existingItem);
        await _owner.Navigation.PushAsync(calculator);
    }

    private async Task SaveIspItemForCharacterAsync(IspCalculationResult result, Item? existingItem)
    {
        var abilities = (result.Abilities ?? new List<CalcResult>())
            .Where(ability => ability != null)
            .ToList();

        await SaveAssignedItemAsync(
            existingItem: existingItem,
            abilities: abilities,
            totalIsp: Math.Max(0, result.TotalIsp),
            sourceFlow: "isp",
            monsterPointCost: 0,
            mpPayload: null);
    }

    private async Task SaveMonsterPointItemForCharacterAsync(MpSubmissionPayload payload, Item? existingItem)
    {
        var abilities = BuildMonsterPointAbilityList(payload);
        await SaveAssignedItemAsync(
            existingItem: existingItem,
            abilities: abilities,
            totalIsp: Math.Max(0, payload.TotalIsp),
            sourceFlow: "monster-point",
            monsterPointCost: Math.Max(0, payload.TotalMp),
            mpPayload: payload);
    }

    private async Task SaveAssignedItemAsync(
        Item? existingItem,
        List<CalcResult> abilities,
        int totalIsp,
        string sourceFlow,
        int monsterPointCost,
        MpSubmissionPayload? mpPayload)
    {
        var item = existingItem ?? new Item
        {
            CreatedDate = DateTime.Now
        };

        var autoPhysicalRep = ItemDisplayHelper.GetAutoPhysicalRep(abilities);
        var physicalRep = autoPhysicalRep.Length > 0
            ? autoPhysicalRep
            : ItemDisplayHelper.GetStoredPhysicalRep(item);

        ApplyAssignedItemState(item, abilities, totalIsp, sourceFlow, monsterPointCost, physicalRep, mpPayload);
        UpsertItem(item);

        if (ItemDisplayHelper.RequiresPhysicalRepPrompt(abilities))
        {
            var enteredPhysicalRep = await _owner.DisplayPromptAsync(
                "Physical representation",
                "Add a phys-rep for this item (for example belt, ring, pendant).",
                "Save",
                "Skip",
                initialValue: physicalRep);

            var trimmed = (enteredPhysicalRep ?? string.Empty).Trim();
            if (trimmed.Length > 0 && !trimmed.Equals(physicalRep, StringComparison.OrdinalIgnoreCase))
            {
                physicalRep = trimmed;
                ApplyAssignedItemState(item, abilities, totalIsp, sourceFlow, monsterPointCost, physicalRep, mpPayload);
                UpsertItem(item);
            }
        }

        _vm.RefreshItems();
        _saved?.Invoke();
    }

    private void ApplyAssignedItemState(
        Item item,
        IReadOnlyList<CalcResult> abilities,
        int totalIsp,
        string sourceFlow,
        int monsterPointCost,
        string physicalRep,
        MpSubmissionPayload? mpPayload)
    {
        var displayName = ItemDisplayHelper.BuildDisplayName(abilities, physicalRep);

        item.ItemType = ResolveWalletItemType(abilities);
        item.Maker = BuildMakerCharacter();
        item.Description = BuildAssignedItemDescription(
            displayName: displayName,
            totalIsp: totalIsp,
            abilities: abilities,
            sourceFlow: sourceFlow,
            monsterPointCost: monsterPointCost,
            mpPayload: mpPayload);
        item.Isp = totalIsp;
        item.CreatedDate = item.CreatedDate == default ? DateTime.Now : item.CreatedDate;
        item.AssignedCharacterId = (_vm.Draft.CharacterRecordId ?? string.Empty).Trim();
        item.AssignedCharacterName = (_vm.Draft.Name ?? string.Empty).Trim();
        item.AssignedCharacterPlayerName = (_vm.Draft.PlayerName ?? string.Empty).Trim();
        item.RecipientPlayerName = item.AssignedCharacterPlayerName;
        item.RecipientCharacterName = item.AssignedCharacterName;
        item.RecipientCharacterClass = (_vm.Draft.Class ?? string.Empty).Trim();

        var payload = ItemEmailService.BuildItemPayload(item, abilities);
        payload.Item.DisplayName = displayName;
        payload.Item.PhysicalRepresentation = (physicalRep ?? string.Empty).Trim();
        payload.Item.SourceFlow = (sourceFlow ?? string.Empty).Trim();
        payload.Item.MonsterPointCost = Math.Max(0, monsterPointCost);
        payload.Item.MpEditorState = mpPayload?.EditorState ?? payload.Item.MpEditorState;
        item.PayloadJson = ItemEmailService.SerializeItemPayload(payload);
    }

    private Character BuildMakerCharacter()
    {
        var maker = new Character
        {
            Name = (_vm.Draft.Name ?? string.Empty).Trim(),
            PlayerName = (_vm.Draft.PlayerName ?? string.Empty).Trim(),
            Class = (_vm.Draft.Class ?? string.Empty).Trim(),
            Race = (_vm.Draft.Race ?? string.Empty).Trim()
        };

        maker.Id = (_vm.Draft.CharacterRecordId ?? string.Empty).Trim();

        return maker;
    }

    private static void UpsertItem(Item item)
    {
        if (!LiteDbService.UpdateItem(item))
            LiteDbService.InsertItem(item);
    }

    private static string BuildAssignedItemDescription(
        string displayName,
        int totalIsp,
        IReadOnlyList<CalcResult> abilities,
        string sourceFlow,
        int monsterPointCost,
        MpSubmissionPayload? mpPayload)
    {
        var lines = new List<string>
        {
            $"Name: {displayName}",
            $"Source: {ResolveSourceLabel(sourceFlow)}"
        };

        if (monsterPointCost > 0)
            lines.Add($"MP cost: {monsterPointCost}");

        lines.Add($"ISP total: {totalIsp}");
        lines.Add("ISP breakdown:");
        var abilityLines = abilities
            .Select(ability => (ability?.Summary ?? string.Empty).Trim())
            .Where(text => text.Length > 0)
            .ToList();
        if (abilityLines.Count == 0)
            lines.Add($"Manual ISP entry = {totalIsp}");
        else
            lines.AddRange(abilityLines);

        if (mpPayload?.Breakdown.Count > 0)
        {
            lines.Add("MP breakdown:");
            lines.AddRange(mpPayload.Breakdown
                .Select(row => (row?.Text ?? string.Empty).Trim())
                .Where(text => text.Length > 0));
        }

        return string.Join("\n", lines);
    }

    private static string ResolveSourceLabel(string sourceFlow)
    {
        return (sourceFlow ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "monster-point" => "Monster point builder",
            "isp" => "ISP calculator",
            _ => "Item builder"
        };
    }

    private static ItemTypeEnum ResolveWalletItemType(IEnumerable<CalcResult>? abilities)
    {
        var list = (abilities ?? Enumerable.Empty<CalcResult>()).ToList();
        if (list.Count == 0)
            return ItemTypeEnum.None;

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Weapon", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Shield", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Armour", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Armor", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Physical", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Physical;
        }

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Spell", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Magic", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Magic;
        }

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Miracle", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Spirit", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Spirit;
        }

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Evocation", StringComparison.OrdinalIgnoreCase)
                || (ability.AbilityType ?? string.Empty).Equals("Earthpower", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.EarthPower;
        }

        if (list.Any(ability =>
                (ability.AbilityType ?? string.Empty).Equals("Neuronic", StringComparison.OrdinalIgnoreCase)))
        {
            return ItemTypeEnum.Neuronic;
        }

        return ItemTypeEnum.Other;
    }

    private static List<CalcResult> BuildMonsterPointAbilityList(MpSubmissionPayload payload)
    {
        var abilities = new List<CalcResult>();
        foreach (var row in payload.IspBreakdown ?? new List<ContributionRow>())
        {
            var ability = BuildAbilityFromContribution(row);
            if (ability == null)
                continue;

            abilities.Add(ability);
        }

        if (abilities.Count == 0 && payload.TotalIsp > 0)
        {
            abilities.Add(new CalcResult
            {
                AbilityType = "Base",
                AbilityName = "Monster point item",
                TotalIsp = payload.TotalIsp,
                Summary = $"ISP total: {payload.TotalIsp}",
                Details = new Dictionary<string, object?>
                {
                    ["source"] = "monster-point"
                }
            });
        }

        return abilities;
    }

    private static CalcResult? BuildAbilityFromContribution(ContributionRow? row)
    {
        var summary = (row?.Text ?? string.Empty).Trim();
        if (summary.Length == 0)
            return null;

        var label = summary;
        var equalsIndex = summary.IndexOf('=');
        if (equalsIndex > 0)
            label = summary[..equalsIndex].Trim();

        var totalIsp = ParseContributionCost(summary);

        if (TryBuildMonsterPointCastingAbility(label, summary, totalIsp, out var castingAbility))
            return castingAbility;

        if (TryBuildMonsterPointLifeAbility(label, summary, totalIsp, out var lifeAbility))
            return lifeAbility;

        var abilityType = "MonsterPoint";
        var abilityName = label;
        if (label.Contains("shield", StringComparison.OrdinalIgnoreCase))
        {
            abilityType = "Shield";
            abilityName = "Shield";
        }
        else if (label.Contains("armour", StringComparison.OrdinalIgnoreCase)
                 || label.Contains("armor", StringComparison.OrdinalIgnoreCase))
        {
            abilityType = "Armour";
            abilityName = "Armour";
        }
        else if (label.Contains("weapon", StringComparison.OrdinalIgnoreCase))
        {
            abilityType = "Weapon";
            abilityName = "Weapon";
        }

        var details = new Dictionary<string, object?>
        {
            ["source"] = "monster-point"
        };
        AddMonsterPointArmourTokenDetails(details, label);

        return new CalcResult
        {
            AbilityType = abilityType,
            AbilityName = abilityName,
            TotalIsp = totalIsp,
            Summary = summary,
            Details = details
        };
    }

    private static bool TryBuildMonsterPointCastingAbility(
        string label,
        string summary,
        int totalIsp,
        out CalcResult ability)
    {
        ability = new CalcResult();
        var match = MonsterPointCastingRegex.Match(label);
        if (!match.Success)
            return false;

        var kind = (match.Groups["kind"].Value ?? string.Empty).Trim();
        var rawName = (match.Groups["name"].Value ?? string.Empty).Trim();
        if (!int.TryParse(match.Groups["count"].Value, out var count))
            count = 0;

        var resolvedName = NormalizeMonsterPointAbilityName(rawName);
        if (resolvedName.Length == 0)
            return false;

        var details = new Dictionary<string, object?>
        {
            ["source"] = "monster-point",
            ["count"] = Math.Max(0, count)
        };

        var uses = Math.Max(0, count);
        var perDayEntry = new Dictionary<string, object?>
        {
            ["basicPerDay"] = uses,
            ["advancedPerDay"] = 0
        };

        if (kind.Equals("Spell", StringComparison.OrdinalIgnoreCase))
        {
            perDayEntry["spellName"] = resolvedName;
            details["spells"] = new[] { perDayEntry };
        }
        else if (kind.Equals("Miracle", StringComparison.OrdinalIgnoreCase))
        {
            perDayEntry["miracleName"] = resolvedName;
            details["miracles"] = new[] { perDayEntry };
        }
        else
        {
            perDayEntry["evocationName"] = resolvedName;
            details["evocations"] = new[] { perDayEntry };
        }

        ability = new CalcResult
        {
            AbilityType = kind,
            AbilityName = resolvedName,
            TotalIsp = totalIsp,
            Summary = summary,
            Details = details
        };

        return true;
    }

    private static bool TryBuildMonsterPointLifeAbility(
        string label,
        string summary,
        int totalIsp,
        out CalcResult ability)
    {
        ability = new CalcResult();
        var match = MonsterPointLifeRegex.Match(label);
        if (!match.Success)
            return false;

        var life = (match.Groups["life"].Value ?? string.Empty).Replace(" ", string.Empty).Trim();
        if (life.Length == 0)
            return false;

        ability = new CalcResult
        {
            AbilityType = "Life",
            AbilityName = life,
            TotalIsp = totalIsp,
            Summary = summary,
            Details = new Dictionary<string, object?>
            {
                ["source"] = "monster-point",
                ["life"] = life
            }
        };

        return true;
    }

    private static void AddMonsterPointArmourTokenDetails(Dictionary<string, object?> details, string label)
    {
        foreach (Match match in MonsterPointArmourTokenRegex.Matches(label ?? string.Empty))
        {
            if (!int.TryParse(match.Groups["amount"].Value, out var amount) || amount <= 0)
                continue;

            var key = (match.Groups["stat"].Value ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0)
                continue;

            var existing = 0;
            if (details.TryGetValue(key, out var rawExisting)
                && rawExisting != null
                && int.TryParse(rawExisting.ToString(), out var parsedExisting))
            {
                existing = parsedExisting;
            }

            details[key] = existing + amount;
        }
    }

    private static string NormalizeMonsterPointAbilityName(string rawName)
    {
        var value = (rawName ?? string.Empty).Trim();
        if (value.Length == 0)
            return string.Empty;

        var parenIndex = value.IndexOf(" (", StringComparison.Ordinal);
        if (parenIndex > 0)
            value = value[..parenIndex].Trim();

        return value;
    }

    private static int ParseContributionCost(string line)
    {
        var match = ContributionCostRegex.Match(line ?? string.Empty);
        if (!match.Success)
            return 0;

        return int.TryParse(match.Groups["cost"].Value, out var cost) ? cost : 0;
    }

}
