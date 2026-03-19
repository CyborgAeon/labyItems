using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Services.Specialisations;

namespace labyItems.Services;

public static class GuildsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static readonly SemaphoreSlim MiracleSearchCacheLock = new(1, 1);
    private static Dictionary<string, GuildRecord>? _cache;
    private static Dictionary<string, GuildRecord>? _miracleSearchCache;

    public static async Task<Dictionary<string, GuildRecord>> GetAllAsync()
    {
        if (_cache != null)
            return _cache;

        await CacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cache != null)
                return _cache;

            var json = await ServiceHelper.ReadPackageTextAsync("people/guilds.json").ConfigureAwait(false);
            var records = await Task.Run(() =>
                    JsonSerializer.Deserialize<Dictionary<string, GuildRecord>>(json, _jsonOptions)
                    ?? new Dictionary<string, GuildRecord>())
                .ConfigureAwait(false);

            await NormalizeGuildBenefitsAsync(records).ConfigureAwait(false);
            _cache = records;
            return _cache;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static async Task NormalizeGuildBenefitsAsync(Dictionary<string, GuildRecord> records)
    {
        if (records == null || records.Count == 0)
            return;

        try
        {
            var index = await SpecialisationDefinitionRepository.GetIndexAsync();
            var abilityRefs = index?.AbilityReferences
                              ?? new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
            var choiceSetRefs = index?.ChoiceSetTemplates
                               ?? new Dictionary<string, SpecialisationChoiceSet>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in records.Values)
            {
                record.Benefits ??= new GuildBenefits();
                var grantsById = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
                record.Benefits.Basic = NormalizeBenefitTier(record.Benefits.Basic, abilityRefs, choiceSetRefs, grantsById);
                record.Benefits.Intermediate = NormalizeBenefitTier(record.Benefits.Intermediate, abilityRefs, choiceSetRefs, grantsById);
                record.Benefits.Advanced = NormalizeBenefitTier(record.Benefits.Advanced, abilityRefs, choiceSetRefs, grantsById);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuildsService] Failed to normalize guild ability references: {ex.Message}");
        }
    }

    private static List<GuildBenefitEntry> NormalizeBenefitTier(
        IEnumerable<GuildBenefitEntry>? tier,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs,
        IReadOnlyDictionary<string, SpecialisationChoiceSet> choiceSetRefs,
        IDictionary<string, AbilityDefinition> grantsById)
    {
        var normalized = new List<GuildBenefitEntry>();
        foreach (var entry in tier ?? Enumerable.Empty<GuildBenefitEntry>())
        {
            if (entry == null)
                continue;

            if (entry.Ability != null)
            {
                var ability = NormalizeBenefitAbility(entry.Ability, abilityRefs, grantsById);
                if (ability == null)
                    continue;

                if (ability.ChoiceSetRefs is { Count: > 0 })
                {
                    var prompt = CloneAbility(ability);
                    prompt.ChoiceSetRefs = null;
                    prompt.ChoiceSetRef = null;
                    if (!string.IsNullOrWhiteSpace(prompt.Name))
                        normalized.Add(new GuildBenefitEntry { Ability = prompt });

                    var optionsEntry = BuildChoiceSetOptionEntry(ability.ChoiceSetRefs, abilityRefs, choiceSetRefs);
                    if (optionsEntry != null)
                        normalized.Add(optionsEntry);

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(ability.Name))
                    normalized.Add(new GuildBenefitEntry { Ability = ability });

                RegisterGrant(ability, grantsById);

                continue;
            }

            if (entry.Options == null || entry.Options.Count == 0)
                continue;

            var options = new List<GuildBenefitOption>();
            foreach (var option in entry.Options)
            {
                var abilities = (option?.Abilities ?? new List<AbilityDefinition>())
                    .Where(a => a != null)
                    .Select(a => NormalizeBenefitAbility(a, abilityRefs, grantsById))
                    .Where(a => a != null)
                    .Cast<AbilityDefinition>()
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                    .ToList();

                if (abilities.Count == 0)
                    continue;

                options.Add(new GuildBenefitOption { Abilities = abilities });
                foreach (var ability in abilities)
                    RegisterGrant(ability, grantsById);
            }

            if (options.Count > 0)
                normalized.Add(new GuildBenefitEntry { Options = options });
        }

        return normalized;
    }

    private static AbilityDefinition? NormalizeBenefitAbility(
        AbilityDefinition? raw,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs,
        IDictionary<string, AbilityDefinition> grantsById)
    {
        if (raw == null)
            return null;

        if (!string.IsNullOrWhiteSpace(raw.UpgradeGrantRef)
            && (raw.ReplaceWith != null || raw.Modify != null))
        {
            return NormalizeStructuredUpgrade(raw, abilityRefs, grantsById);
        }

        return NormalizeAbility(raw, abilityRefs);
    }

    private static AbilityDefinition NormalizeStructuredUpgrade(
        AbilityDefinition raw,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs,
        IDictionary<string, AbilityDefinition> grantsById)
    {
        AbilityDefinition ability;
        if (raw.ReplaceWith != null)
        {
            ability = NormalizeAbility(raw.ReplaceWith, abilityRefs);
        }
        else if (!string.IsNullOrWhiteSpace(raw.UpgradeGrantRef)
                 && grantsById.TryGetValue(raw.UpgradeGrantRef.Trim(), out var priorGrant))
        {
            ability = CloneAbility(priorGrant);
        }
        else
        {
            ability = new AbilityDefinition();
        }

        ApplyGrantOverlay(ability, raw);

        var countDelta = raw.Modify?.CountDelta;
        if (countDelta.HasValue && countDelta.Value != 0)
        {
            var baseCount = ability.Count ?? 0;
            ability.Count = Math.Max(0, baseCount + countDelta.Value);
        }

        if (!string.IsNullOrWhiteSpace(raw.UpgradeGrantRef))
            ability.UpgradeGrantRef = raw.UpgradeGrantRef;

        return ability;
    }

    private static void RegisterGrant(AbilityDefinition? ability, IDictionary<string, AbilityDefinition> grantsById)
    {
        var grantId = (ability?.GrantId ?? string.Empty).Trim();
        if (grantId.Length == 0)
            return;

        grantsById[grantId] = CloneAbility(ability!);
    }

    private static GuildBenefitEntry? BuildChoiceSetOptionEntry(
        IEnumerable<string> choiceSetRefs,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs,
        IReadOnlyDictionary<string, SpecialisationChoiceSet> choiceSets)
    {
        var options = new List<GuildBenefitOption>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var choiceSetRefRaw in choiceSetRefs ?? Enumerable.Empty<string>())
        {
            var choiceSetRef = (choiceSetRefRaw ?? string.Empty).Trim();
            if (choiceSetRef.Length == 0 || !choiceSets.TryGetValue(choiceSetRef, out var choiceSet))
                continue;

            foreach (var option in choiceSet.Options ?? Array.Empty<ChoiceOption>())
            {
                var abilities = new List<AbilityDefinition>();
                foreach (var grant in option.Grants ?? Array.Empty<AbilityGrant>())
                {
                    var ability = NormalizeAbility(grant.Ability ?? new AbilityDefinition(), abilityRefs);
                    if (!string.IsNullOrWhiteSpace(ability.Name))
                        abilities.Add(ability);
                }

                if (abilities.Count == 0)
                    continue;

                var signature = string.Join("|", abilities
                    .Select(a => !string.IsNullOrWhiteSpace(a.Key)
                        ? a.Key!.Trim()
                        : (a.Name ?? string.Empty).Trim().ToLowerInvariant()));

                if (!seen.Add(signature))
                    continue;

                options.Add(new GuildBenefitOption { Abilities = abilities });
            }
        }

        return options.Count == 0 ? null : new GuildBenefitEntry { Options = options };
    }

    private static AbilityDefinition NormalizeAbility(
        AbilityDefinition? raw,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs)
    {
        if (raw == null)
            return new AbilityDefinition();

        var resolved = TryResolveReferencedAbility(raw, abilityRefs);
        var ability = resolved != null ? CloneAbility(resolved) : new AbilityDefinition();
        ApplyGrantOverlay(ability, raw);

        if (string.IsNullOrWhiteSpace(ability.AbilityRef))
            ability.AbilityRef = resolved?.Key;
        if (string.IsNullOrWhiteSpace(ability.Key))
            ability.Key = resolved?.Key ?? ability.AbilityRef;
        if (string.IsNullOrWhiteSpace(ability.Key) && !string.IsNullOrWhiteSpace(ability.Name))
            ability.Key = $"ability.guild.{NormalizeIdentityToken(ability.Name)}";
        if (string.IsNullOrWhiteSpace(ability.AbilityRef))
            ability.AbilityRef = ability.Key;
        if (string.IsNullOrWhiteSpace(ability.GrantType) && !string.IsNullOrWhiteSpace(ability.Type))
            ability.GrantType = ability.Type;
        if (string.IsNullOrWhiteSpace(ability.Type) && !string.IsNullOrWhiteSpace(ability.GrantType))
            ability.Type = ability.GrantType;

        return ability;
    }

    private static void ApplyGrantOverlay(AbilityDefinition target, AbilityDefinition source)
    {
        if (!string.IsNullOrWhiteSpace(source.Key))
            target.Key = source.Key;
        if (!string.IsNullOrWhiteSpace(source.AbilityRef))
            target.AbilityRef = source.AbilityRef;
        if (!string.IsNullOrWhiteSpace(source.GrantId))
            target.GrantId = source.GrantId;
        if (!string.IsNullOrWhiteSpace(source.GrantType))
        {
            target.GrantType = source.GrantType;
            target.Type = source.GrantType;
        }
        if (!string.IsNullOrWhiteSpace(source.Duration))
            target.Duration = source.Duration;
        if (!string.IsNullOrWhiteSpace(source.UpgradeGrantRef))
            target.UpgradeGrantRef = source.UpgradeGrantRef;
        if (source.Modify != null)
            target.Modify = CloneGrantModify(source.Modify);
        if (source.ReplaceWith != null)
            target.ReplaceWith = CloneAbility(source.ReplaceWith);

        if (!string.IsNullOrWhiteSpace(source.Name))
            target.Name = source.Name;
        if (!string.IsNullOrWhiteSpace(source.BattleboardNameOverride))
            target.BattleboardNameOverride = source.BattleboardNameOverride;
        if (!string.IsNullOrWhiteSpace(source.UpdateKey))
            target.UpdateKey = source.UpdateKey;
        if (!string.IsNullOrWhiteSpace(source.Type) && string.IsNullOrWhiteSpace(source.GrantType))
            target.Type = source.Type;
        if (!string.IsNullOrWhiteSpace(source.Effect))
            target.Effect = source.Effect;
        if (!string.IsNullOrWhiteSpace(source.Lore))
            target.Lore = source.Lore;
        if (!string.IsNullOrWhiteSpace(source.Source))
            target.Source = source.Source;
        if (source.Count.HasValue)
            target.Count = source.Count;
        if (source.Progression != null)
            target.Progression = CloneProgression(source.Progression);
        if (source.Amount is { Count: > 0 })
            target.Amount = source.Amount.ToList();
        if (!string.IsNullOrWhiteSpace(source.Frequency))
            target.Frequency = source.Frequency;
        if (!string.IsNullOrWhiteSpace(source.OverwriteKey))
            target.OverwriteKey = source.OverwriteKey;
        if (source.PreReqs is { Count: > 0 })
            target.PreReqs = source.PreReqs.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (source.GuildOverrides is { Count: > 0 })
            target.GuildOverrides = source.GuildOverrides.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (source.Customisation != null)
            target.Customisation = CloneCustomisation(source.Customisation);
        if (source.SystemEffects is { Count: > 0 })
            target.SystemEffects = source.SystemEffects.Select(CloneSystemEffect).ToList();

        var refs = new List<string>();
        if (source.ChoiceSetRefs is { Count: > 0 })
        {
            refs.AddRange(source.ChoiceSetRefs
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(source.ChoiceSetRef))
            refs.Add(source.ChoiceSetRef.Trim());
        if (refs.Count > 0)
        {
            target.ChoiceSetRefs = refs
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            target.ChoiceSetRef = target.ChoiceSetRefs.FirstOrDefault();
        }

        if (source.Overrides != null)
        {
            target.Overrides = CloneGrantOverrides(source.Overrides);
            ApplyGrantOverrides(target, source.Overrides);
        }
    }

    private static void ApplyGrantOverrides(AbilityDefinition ability, GuildGrantOverrides overrides)
    {
        if (!string.IsNullOrWhiteSpace(overrides.DisplayName))
            ability.Name = overrides.DisplayName;
        if (!string.IsNullOrWhiteSpace(overrides.Effect))
            ability.Effect = overrides.Effect;
        if (!string.IsNullOrWhiteSpace(overrides.Source))
            ability.Source = overrides.Source;
        if (!string.IsNullOrWhiteSpace(overrides.GrantType))
        {
            ability.GrantType = overrides.GrantType;
            ability.Type = overrides.GrantType;
        }
        if (overrides.Count.HasValue)
            ability.Count = overrides.Count;
        if (!string.IsNullOrWhiteSpace(overrides.Frequency))
            ability.Frequency = overrides.Frequency;
        if (!string.IsNullOrWhiteSpace(overrides.Duration))
            ability.Duration = overrides.Duration;
    }

    private static AbilityDefinition? TryResolveReferencedAbility(
        AbilityDefinition raw,
        IReadOnlyDictionary<string, AbilityDefinition> abilityRefs)
    {
        foreach (var tokenRaw in new[] { raw.AbilityRef, raw.Key })
        {
            var token = (tokenRaw ?? string.Empty).Trim();
            if (token.Length == 0)
                continue;

            if (abilityRefs.TryGetValue(token, out var found))
                return found;
        }

        var normalizedName = NormalizeIdentityToken(raw.Name);
        if (normalizedName.Length == 0)
            return null;

        foreach (var candidate in abilityRefs.Values)
        {
            if (candidate == null)
                continue;

            if (NormalizeIdentityToken(candidate.Name) == normalizedName)
                return candidate;
        }

        return null;
    }

    private static AbilityDefinition CloneAbility(AbilityDefinition source)
    {
        return new AbilityDefinition
        {
            Key = source.Key,
            AbilityRef = source.AbilityRef,
            GrantId = source.GrantId,
            GrantType = source.GrantType,
            Duration = source.Duration,
            Overrides = CloneGrantOverrides(source.Overrides),
            UpgradeGrantRef = source.UpgradeGrantRef,
            ReplaceWith = source.ReplaceWith != null ? CloneAbility(source.ReplaceWith) : null,
            Modify = CloneGrantModify(source.Modify),
            Name = source.Name ?? string.Empty,
            BattleboardNameOverride = source.BattleboardNameOverride,
            UpdateKey = source.UpdateKey,
            Type = source.Type ?? string.Empty,
            Effect = source.Effect,
            Lore = source.Lore,
            Source = source.Source,
            Count = source.Count,
            Progression = CloneProgression(source.Progression),
            Amount = source.Amount?.ToList(),
            Frequency = source.Frequency,
            OverwriteKey = source.OverwriteKey,
            PreReqs = source.PreReqs?.ToList(),
            GuildOverrides = source.GuildOverrides?.ToList(),
            Customisation = CloneCustomisation(source.Customisation),
            ChoiceSetRef = source.ChoiceSetRef,
            ChoiceSetRefs = source.ChoiceSetRefs?.ToList(),
            SystemEffects = source.SystemEffects?.Select(CloneSystemEffect).ToList()
        };
    }

    private static GuildGrantOverrides? CloneGrantOverrides(GuildGrantOverrides? source)
    {
        if (source == null)
            return null;

        return new GuildGrantOverrides
        {
            DisplayName = source.DisplayName,
            Verbal = source.Verbal,
            Effect = source.Effect,
            Source = source.Source,
            GrantType = source.GrantType,
            Count = source.Count,
            Frequency = source.Frequency,
            Duration = source.Duration
        };
    }

    private static GuildGrantModify? CloneGrantModify(GuildGrantModify? source)
    {
        if (source == null)
            return null;

        return new GuildGrantModify
        {
            CountDelta = source.CountDelta
        };
    }

    private static AbilityCountProgression? CloneProgression(AbilityCountProgression? source)
    {
        if (source == null)
            return null;

        return new AbilityCountProgression
        {
            Amount = source.Amount,
            PerLevels = source.PerLevels,
            Minimum = source.Minimum,
            Maximum = source.Maximum
        };
    }

    private static AbilityCustomisation? CloneCustomisation(AbilityCustomisation? source)
    {
        if (source == null)
            return null;

        return new AbilityCustomisation
        {
            OptionEnum = source.OptionEnum,
            CustomValuesPermitted = source.CustomValuesPermitted
        };
    }

    private static AbilitySystemEffect CloneSystemEffect(AbilitySystemEffect source)
    {
        return new AbilitySystemEffect
        {
            EffectType = source.EffectType ?? string.Empty,
            DisplayName = source.DisplayName ?? string.Empty,
            ResistanceType = source.ResistanceType,
            Level = source.Level,
            ImmunityName = source.ImmunityName
        };
    }

    private static string NormalizeIdentityToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    public static async Task<IReadOnlyList<string>> GetGuildNamesAsync()
    {
        var all = await GetAllAsync();
        var keys = new List<string>(all.Keys);
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        return keys;
    }

    public static async Task<Dictionary<string, GuildRecord>> GetMiracleSearchAsync()
    {
        if (_miracleSearchCache != null)
            return _miracleSearchCache;

        await MiracleSearchCacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_miracleSearchCache != null)
                return _miracleSearchCache;

            var json = await ServiceHelper.ReadPackageTextAsync("people/guilds.json").ConfigureAwait(false);
            _miracleSearchCache = await Task.Run(() => ParseMiracleSearch(json)).ConfigureAwait(false);
            return _miracleSearchCache;
        }
        finally
        {
            MiracleSearchCacheLock.Release();
        }
    }

    public static async Task<IReadOnlyList<string>> GetTypesAsync()
    {
        var all = await GetAllAsync();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in all)
        {
            var t = kv.Value?.Type?.Trim();
            if (!string.IsNullOrWhiteSpace(t))
                set.Add(t);
        }

        var list = new List<string>(set);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public static AlignmentRule? GetAlignmentRule(GuildRecord? record)
    {
        if (record == null)
            return null;

        var availability = record.Availability;
        if (availability?.Rules is { Count: > 0 })
        {
            var fromRules = BuildAlignmentRuleFromRules(availability.Rules);
            if (fromRules != null)
                return fromRules;
        }

        return null;
    }

    private static AlignmentRule? BuildAlignmentRuleFromRules(
        IEnumerable<RuleClause> rules)
    {
        var items = (rules ?? Array.Empty<RuleClause>())
            .Where(r => r != null && r.IsValid)
            .ToList();

        if (items.Count == 0)
            return null;

        HashSet<OrderAxis>? orderConstraint = null;
        HashSet<MoralAxis>? moralConstraint = null;

        foreach (var rule in items)
        {
            var normalizedField = NormalizeRuleField(rule.Field);
            if (normalizedField != "alignmentorder" && normalizedField != "alignmentmoral")
                continue;

            if (rule.Operator != RuleComparisonOp.In)
                return null;

            if (normalizedField == "alignmentorder")
            {
                MergeConstraint(ref orderConstraint, ParseOrders(rule.Value));
            }
            else
            {
                MergeConstraint(ref moralConstraint, ParseMorals(rule.Value));
            }
        }

        var orders = orderConstraint ?? new HashSet<OrderAxis>();
        var morals = moralConstraint ?? new HashSet<MoralAxis>();
        if (orders.Count == 0 && morals.Count == 0)
            return null;

        return new AlignmentRule
        {
            Mode = "restrict",
            Allowed = new AllowedAxes
            {
                Order = orders.ToList(),
                Moral = morals.ToList()
            }
        };
    }

    private static Dictionary<string, GuildRecord> ParseMiracleSearch(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var map = new Dictionary<string, GuildRecord>(StringComparer.OrdinalIgnoreCase);

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return map;

        foreach (var guildProperty in doc.RootElement.EnumerateObject())
        {
            if (guildProperty.Value.ValueKind != JsonValueKind.Object)
                continue;

            var guildObject = guildProperty.Value;
            var record = new GuildRecord();

            if (guildObject.TryGetProperty("Type", out var typeElement)
                && typeElement.ValueKind == JsonValueKind.String)
            {
                record.Type = typeElement.GetString() ?? string.Empty;
            }

            if (guildObject.TryGetProperty("Logo", out var logoElement)
                && logoElement.ValueKind == JsonValueKind.String)
            {
                record.Logo = logoElement.GetString() ?? string.Empty;
            }

            if (guildObject.TryGetProperty("MiracleList", out var miracleListElement))
            {
                record.MiracleList = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
                    miracleListElement.GetRawText(),
                    _jsonOptions) ?? new Dictionary<string, List<string>>();
            }

            if (guildObject.TryGetProperty("DenominationalMiracle", out var denominationalElement))
            {
                record.DenominationalMiracle = JsonSerializer.Deserialize<GuildMiracleReference>(
                    denominationalElement.GetRawText(),
                    _jsonOptions);
            }

            if (guildObject.TryGetProperty("DenominationalMiracleNote", out var denominationalNoteElement)
                && denominationalNoteElement.ValueKind == JsonValueKind.String)
            {
                record.DenominationalMiracleNote = denominationalNoteElement.GetString() ?? string.Empty;
            }

            map[guildProperty.Name] = record;
        }

        return map;
    }

    private static string NormalizeRuleField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static void MergeConstraint<TEnum>(ref HashSet<TEnum>? existing, HashSet<TEnum> incoming)
        where TEnum : struct, Enum
    {
        if (incoming.Count == 0)
            return;

        if (existing == null)
        {
            existing = incoming;
            return;
        }

        existing.IntersectWith(incoming);
    }

    private static HashSet<OrderAxis> ParseOrders(IEnumerable<string>? values)
    {
        var set = new HashSet<OrderAxis>();
        foreach (var v in values ?? Array.Empty<string>())
        {
            if (Enum.TryParse<OrderAxis>(v, true, out var parsed))
                set.Add(parsed);
        }
        return set;
    }

    private static HashSet<MoralAxis> ParseMorals(IEnumerable<string>? values)
    {
        var set = new HashSet<MoralAxis>();
        foreach (var v in values ?? Array.Empty<string>())
        {
            if (Enum.TryParse<MoralAxis>(v, true, out var parsed))
                set.Add(parsed);
        }
        return set;
    }
}

public sealed class GuildRecord
{
    public string Type { get; set; } = "";
    public string Logo { get; set; } = "";
    public string PreRequisites { get; set; } = "";
    public string Restrictions { get; set; } = "";
    public string Ethos { get; set; } = "";
    public string Background { get; set; } = "";

    public GuildBenefits Benefits { get; set; } = new();

    [JsonPropertyName("alignmentRule")]
    public AlignmentRule? AlignmentRule { get; set; }

    public Dictionary<string, List<string>> MiracleList { get; set; } = new();
    public GuildMiracleReference? DenominationalMiracle { get; set; }
    public string DenominationalMiracleNote { get; set; } = string.Empty;

    public GuildAvailability Availability { get; set; } = new();
}

[JsonConverter(typeof(GuildMiracleReferenceConverter))]
public sealed class GuildMiracleReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;
}

public sealed class GuildBenefits
{
    public List<GuildBenefitEntry> Basic { get; set; } = new();
    public List<GuildBenefitEntry> Intermediate { get; set; } = new();
    public List<GuildBenefitEntry> Advanced { get; set; } = new();
}

public static class GuildBenefitKeys
{
    public static string BuildSelectionKey(string guildName, string tier, int optionIndex)
    {
        var guild = (guildName ?? string.Empty).Trim();
        var level = (tier ?? string.Empty).Trim();
        return $"{guild}::{level}::{optionIndex}";
    }
}

[JsonConverter(typeof(GuildBenefitEntryConverter))]
public sealed class GuildBenefitEntry
{
    public AbilityDefinition? Ability { get; set; }
    public List<GuildBenefitOption> Options { get; set; } = new();
    public bool IsOptionGroup => Options.Count > 0;
}

public sealed class GuildBenefitOption
{
    public List<AbilityDefinition> Abilities { get; set; } = new();
}

public sealed class GuildBenefitEntryConverter : JsonConverter<GuildBenefitEntry>
{
    public override GuildBenefitEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return ParseEntry(doc.RootElement, options);
    }

    private static GuildBenefitEntry ParseEntry(JsonElement el, JsonSerializerOptions options)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                var entry = new GuildBenefitEntry();
                foreach (var optionEl in el.EnumerateArray())
                {
                    var abilities = new List<AbilityDefinition>();
                    AddAbilitiesFromElement(optionEl, abilities, options);
                    if (abilities.Count > 0)
                        entry.Options.Add(new GuildBenefitOption { Abilities = abilities });
                }
                return entry;
            case JsonValueKind.Object:
            case JsonValueKind.String:
                var ability = ParseAbility(el, options);
                return new GuildBenefitEntry { Ability = ability };
            default:
                return new GuildBenefitEntry();
        }
    }

    private static void AddAbilitiesFromElement(JsonElement element, List<AbilityDefinition> abilities, JsonSerializerOptions options)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                AddAbilitiesFromElement(child, abilities, options);
            return;
        }

        var ability = ParseAbility(element, options);
        if (ability != null && !string.IsNullOrWhiteSpace(ability.Name))
            abilities.Add(ability);
    }

    private static AbilityDefinition? ParseAbility(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.String)
            return null;

        try
        {
            return JsonSerializer.Deserialize<AbilityDefinition>(element.GetRawText(), options);
        }
        catch
        {
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, GuildBenefitEntry value, JsonSerializerOptions options)
    {
        if (value?.Ability != null)
        {
            JsonSerializer.Serialize(writer, value.Ability, options);
            return;
        }

        writer.WriteStartArray();
        foreach (var option in value?.Options ?? new List<GuildBenefitOption>())
        {
            writer.WriteStartArray();
            foreach (var ability in option.Abilities ?? new List<AbilityDefinition>())
                JsonSerializer.Serialize(writer, ability, options);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
}

public sealed class GuildAvailability
{
    public List<RuleClause> Rules { get; set; } = new();
    public string? RequiredGuild { get; set; }
}

public sealed class GuildMiracleReferenceConverter : JsonConverter<GuildMiracleReference>
{
    public override GuildMiracleReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new GuildMiracleReference { Ref = reader.GetString() ?? string.Empty };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing guild miracle reference.");

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var reference = new GuildMiracleReference();

        if (root.TryGetProperty("$ref", out var refEl))
            reference.Ref = refEl.GetString() ?? string.Empty;
        else if (root.TryGetProperty("ref", out var compatRef))
            reference.Ref = compatRef.GetString() ?? string.Empty;
        else if (root.TryGetProperty("Name", out var nameEl))
            reference.Ref = nameEl.GetString() ?? string.Empty;

        return reference;
    }

    public override void Write(Utf8JsonWriter writer, GuildMiracleReference value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("$ref", value?.Ref ?? string.Empty);
        writer.WriteEndObject();
    }
}
