using labyItems.Models.Characters;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class SpecialisationService
{
    private static Dictionary<string, SpecialisationRecord>? _cache;

    public static async Task<Dictionary<string, SpecialisationRecord>> GetAllAsync()
    {
        if (_cache != null)
            return _cache;

        var index = await SpecialisationDefinitionRepository.GetIndexAsync();
        var records = new Dictionary<string, SpecialisationRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in index.Definitions)
            records[entry.Key] = ConvertDefinition(entry.Value);

        _cache = records;
        return _cache;
    }

    private static SpecialisationRecord ConvertDefinition(SpecialisationDefinition definition)
    {
        var record = new SpecialisationRecord
        {
            Description = ResolveDescription(definition)
        };

        var abilityList = new List<AbilityDefinition>();
        var optionList = new List<AbilityDefinition>();
        Dictionary<string, ColourAbilityRecord>? colourAbilities = null;

        foreach (var grant in definition.PassiveGrants ?? Array.Empty<AbilityGrant>())
        {
            if (grant?.Ability == null || string.IsNullOrWhiteSpace(grant.Ability.Name))
                continue;

            abilityList.Add(CloneAbility(grant.Ability));
        }

        foreach (var choiceSet in definition.ChoiceSets ?? Array.Empty<SpecialisationChoiceSet>())
        {
            switch (choiceSet.Mode)
            {
                case ChoiceMode.MappedSingle:
                    colourAbilities ??= new Dictionary<string, ColourAbilityRecord>(StringComparer.OrdinalIgnoreCase);
                    foreach (var option in choiceSet.Options ?? Array.Empty<ChoiceOption>())
                    {
                        var key = (option.Key ?? option.Label ?? string.Empty).Trim();
                        if (key.Length == 0)
                            continue;

                        colourAbilities[key] = BuildColourAbilityRecord(option);
                    }
                    break;

                case ChoiceMode.Lookup:
                case ChoiceMode.Multi:
                case ChoiceMode.Single:
                {
                    foreach (var option in choiceSet.Options ?? Array.Empty<ChoiceOption>())
                    {
                        var optionName = (option.Label ?? option.Key ?? string.Empty).Trim();
                        if (optionName.Length == 0)
                            continue;

                        if (option.Grants?.Count > 0)
                        {
                            foreach (var grant in option.Grants)
                            {
                                if (grant?.Ability == null)
                                    continue;

                                var cloned = CloneAbility(grant.Ability);
                                if (string.IsNullOrWhiteSpace(cloned.Name))
                                    cloned.Name = optionName;

                                if (string.IsNullOrWhiteSpace(cloned.Effect) && !string.IsNullOrWhiteSpace(option.Description))
                                    cloned.Effect = option.Description;

                                abilityList.Add(cloned);
                            }
                        }
                        else
                        {
                            optionList.Add(new AbilityDefinition
                            {
                                Name = optionName,
                                Effect = option.Description ?? string.Empty,
                                Type = "Static",
                                AsPer = option.AsPer?
                                    .Where(IsNotBlank)
                                    .Select(x => x.Trim())
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList()
                            });
                        }
                    }

                    break;
                }
            }
        }

        record.Abilities = abilityList
            .GroupBy(a => (a.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(ability => !string.IsNullOrWhiteSpace(ability.Name))
            .ToList();

        record.Options = optionList
            .GroupBy(a => (a.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(ability => !string.IsNullOrWhiteSpace(ability.Name))
            .ToList();

        if (record.Abilities.Count == 0)
            record.Abilities = null;

        if (record.Options.Count == 0)
            record.Options = null;

        if (colourAbilities is { Count: > 0 })
            record.ColourAbilities = colourAbilities;

        return record;
    }

    private static string ResolveDescription(SpecialisationDefinition definition)
    {
        var note = definition.Notes?
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        return (note ?? string.Empty).Trim();
    }

    private static ColourAbilityRecord BuildColourAbilityRecord(ChoiceOption option)
    {
        return new ColourAbilityRecord
        {
            Description = (option.Description ?? string.Empty).Trim(),
            AsPer = option.AsPer?.Where(IsNotBlank).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Roleplay = ReadOptionMetadata(option, "Roleplay"),
            Lore = ReadOptionMetadata(option, "Lore"),
            Levels = BuildLevelledAbilityMap(option.Grants),
            LifeScaleOverride = option.Effects?.LifeScaleOverride ?? string.Empty,
            ArmourAvailabilityOverride = option.Effects?.ArmourAvailabilityOverride ?? string.Empty,
            ColourChoiceOverride = option.Effects?.ColourChoiceOverride?.Where(IsNotBlank).ToList(),
            GuildOverrides = option.Effects?.GuildOverrides,
            HedgeOrCircle = option.Effects?.HedgeOrCircle?.Where(IsNotBlank).ToList(),
            ClassRestriction = option.Restrictions?.ClassRestriction?.Where(IsNotBlank).ToList()
        };
    }

    private static Dictionary<string, List<AbilityDefinition>>? BuildLevelledAbilityMap(IReadOnlyList<AbilityGrant>? grants)
    {
        if (grants == null || grants.Count == 0)
            return null;

        var levels = new Dictionary<string, List<AbilityDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var grant in grants)
        {
            if (grant?.Ability == null || string.IsNullOrWhiteSpace(grant.Ability.Name))
                continue;

            var level = grant.Level ?? 1;
            var levelKey = level.ToString();
            if (!levels.TryGetValue(levelKey, out var list))
            {
                list = new List<AbilityDefinition>();
                levels[levelKey] = list;
            }

            list.Add(CloneAbility(grant.Ability));
        }

        if (levels.Count == 0)
            return null;

        foreach (var entry in levels)
        {
            var unique = entry.Value
                .Where(ability => !string.IsNullOrWhiteSpace(ability?.Name))
                .GroupBy(ability => (ability.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            entry.Value.Clear();
            entry.Value.AddRange(unique);
        }

        return levels;
    }

    private static AbilityDefinition CloneAbility(AbilityDefinition source)
    {
        return new AbilityDefinition
        {
            Key = source.Key,
            Name = source.Name,
            Type = source.Type,
            Effect = source.Effect,
            Lore = source.Lore,
            BattleboardNameOverride = source.BattleboardNameOverride,
            UpdateKey = source.UpdateKey,
            Source = source.Source,
            Count = source.Count,
            Amount = source.Amount?.ToList(),
            AsPer = source.AsPer?.ToList(),
            Frequency = source.Frequency,
            OverwriteKey = source.OverwriteKey,
            PreReqs = source.PreReqs?.ToList(),
            GuildOverrides = source.GuildOverrides?.ToList(),
            Customisation = source.Customisation == null
                ? null
                : new AbilityCustomisation
                {
                    OptionEnum = source.Customisation.OptionEnum,
                    CustomValuesPermitted = source.Customisation.CustomValuesPermitted
                }
        };
    }

    private static bool IsNotBlank(string? value)
        => !string.IsNullOrWhiteSpace(value);

    private static string ReadOptionMetadata(ChoiceOption option, string key)
    {
        if (option.Metadata != null
            && option.Metadata.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return string.Empty;
    }
}

public sealed class SpecialisationRecord
{
    public string Description { get; set; } = string.Empty;
    public List<AbilityDefinition>? Abilities { get; set; }
    public List<AbilityDefinition>? Options { get; set; }
    public PowerListRecord? PowerList { get; set; }

    // For tables like ElfColourAbilities
    public Dictionary<string, ColourAbilityRecord>? ColourAbilities { get; set; }
}

public sealed class ColourAbilityRecord
{
    public string Description { get; set; } = string.Empty;
    public List<string>? AsPer { get; set; }
    public string Roleplay { get; set; } = string.Empty;
    public string Lore { get; set; } = string.Empty;
    public Dictionary<string, List<AbilityDefinition>>? Levels { get; set; }
    public string LifeScaleOverride { get; set; } = string.Empty;
    public string ArmourAvailabilityOverride { get; set; } = string.Empty;
    public List<string>? ColourChoiceOverride { get; set; }
    public GuildOverrideRules? GuildOverrides { get; set; }
    public List<string>? HedgeOrCircle { get; set; }
    public List<string>? ClassRestriction { get; set; }
}

public sealed class PowerListRecord
{
    public int Max { get; set; }
    public List<string>? Requirements { get; set; }
}
