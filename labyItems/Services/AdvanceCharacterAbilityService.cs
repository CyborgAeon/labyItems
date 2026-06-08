using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using labyItems.Models.Abilities;
using labyItems.Models.Characters;
using labyItems.Models.Rules;
using labyItems.Pages.Characters.ViewModels;

namespace labyItems.Services;

public interface IAdvanceCharacterAbilityService
{
    Task WarmCachesAsync();
    Task WarmAbilityDetailsAsync(IEnumerable<string>? rawKeysOrNames);
    EvolutionService.AbilityResult? TryResolveAbilityDetails(string? rawKeyOrName);
    string ResolveAbilityDisplayName(string? rawKeyOrName);
    Dictionary<string, ManuAbilityOption> BuildAbilityOptionsByName(
        IEnumerable<ManuAbilityService.ManuAbilityEntry> entries,
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races);
    Dictionary<string, ManuAbilityOption> BuildAbilityOptionsWithLabels(IEnumerable<ManuAbilityOption> entries);
    Task<Dictionary<string, ManuAbilityOption>> SearchAbilityOptionsAsync(
        string query,
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races);
    Task<EvolutionService.AbilityResult?> FindAbilityByNameAsync(string? abilityName);
    void CacheAbilityDetails(EvolutionService.AbilityResult ability);
    int ApplyRaceAbilityCostModifiers(
        int baseCost,
        string? abilityName,
        EvolutionService.AbilityResult? abilityDetails,
        string? abilityRef,
        CharacterDraft draft,
        IReadOnlyDictionary<string, PeopleRecord> races);
}

public sealed class AdvanceCharacterAbilityService : IAdvanceCharacterAbilityService
{
    private readonly IAdvanceAbilityLookupService _abilityLookupService;
    private readonly IAbilityAvailabilityService _abilityAvailabilityService;
    private readonly IAdvanceCharacterDataProvider _dataProvider;
    private readonly Dictionary<string, EvolutionService.AbilityResult> _abilityDetailsByKey = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, AbilityDefinition> _abilityDefinitionsByKey =
        new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> IshmaicSpiritCostRuleKeys = new(
        new[]
        {
            "1st Resistance to Touch of Death",
            "2nd Resistance to Touch of Death",
            "3rd Resistance to Touch of Death",
            "1st Resistance to Causing",
            "2nd Resistance to Causing",
            "3rd Resistance to Causing",
            "1st Resistance to Spirit Bolt",
            "2nd Resistance to Spirit Bolt",
            "3rd Resistance to Spirit Bolt",
            "1/2 Effect Halt and Stasis",
            "Immunity to Informational Miracles",
            "Immunity to Protection from Spirits",
            "Immunity to Strongholds",
            "Immunity to Bar Spirit",
            "Immunity to Level Drain",
            "Immunity to Voice of Power",
            "Immunity to Rune of Power",
            "Immunity to Degeneration Miracle",
            "Immunity to Incarna Mortis",
            "Resistance to Destruction Miracle",
            "Immunity to Bless/Curse",
            "Immunity to Diminish Spirit",
            "Immunity to Freeze",
            "Immunity to Paralysis",
            "Immunity to Petrification",
            "Immunity to Beguile",
            "Immunity to Foul Touch",
            "Immunity to Disease",
            "Resistance to Soul Lance",
            "1/2 Damage Wards of Power",
            "Resistance to Avenging Spirit"
        }.Select(NormalizeCostRuleKey),
        StringComparer.Ordinal);

    public AdvanceCharacterAbilityService(
        IAdvanceAbilityLookupService abilityLookupService,
        IAbilityAvailabilityService abilityAvailabilityService,
        IAdvanceCharacterDataProvider dataProvider)
    {
        _abilityLookupService = abilityLookupService ?? throw new ArgumentNullException(nameof(abilityLookupService));
        _abilityAvailabilityService = abilityAvailabilityService ?? throw new ArgumentNullException(nameof(abilityAvailabilityService));
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
    }

    public async Task WarmCachesAsync()
    {
        if (_abilityDetailsByKey.Count > 0 && _abilityDefinitionsByKey.Count > 0)
            return;

        try
        {
            if (_abilityDetailsByKey.Count == 0)
            {
                var abilities = await EvolutionService.GetAllAbilitiesAsync();
                foreach (var ability in abilities ?? Array.Empty<EvolutionService.AbilityResult>())
                {
                    if (ability == null)
                        continue;

                    var displayName = EvolutionService.NormalizeAbilityDisplayText(ability.Index);
                    if (!string.IsNullOrWhiteSpace(displayName))
                        _abilityDetailsByKey[AbilityDetailsLookupService.NormalizeKey(displayName)] = ability;

                    var abilityKey = AbilityKey.Build(ability);
                    if (!string.IsNullOrWhiteSpace(abilityKey))
                        _abilityDetailsByKey[AbilityDetailsLookupService.NormalizeKey(abilityKey)] = ability;

                    var legacyKey = AbilityKey.BuildEvolutionFallback(ability);
                    if (!string.IsNullOrWhiteSpace(legacyKey)
                        && !string.Equals(legacyKey, abilityKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _abilityDetailsByKey[AbilityDetailsLookupService.NormalizeKey(legacyKey)] = ability;
                    }
                }
            }
        }
        catch
        {
            // If this fails we fall back to showing raw keys/names.
        }

        try
        {
            if (_abilityDefinitionsByKey.Count == 0)
                _abilityDefinitionsByKey = await AbilityDefinitionLookupService.GetLookupAsync();
        }
        catch
        {
            _abilityDefinitionsByKey = new Dictionary<string, AbilityDefinition>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task WarmAbilityDetailsAsync(IEnumerable<string>? rawKeysOrNames)
    {
        var values = (rawKeysOrNames ?? Array.Empty<string>())
            .Select(value => (value ?? string.Empty).Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var value in values)
        {
            if (TryResolveAbilityDetails(value) != null)
                continue;

            var ability = await _abilityLookupService.FindByNameAsync(value);
            if (ability != null)
                CacheAbilityDetails(ability);
        }
    }

    public EvolutionService.AbilityResult? TryResolveAbilityDetails(string? rawKeyOrName)
    {
        var normalized = AbilityDetailsLookupService.NormalizeKey(rawKeyOrName);
        if (normalized.Length == 0)
            return null;

        return _abilityDetailsByKey.TryGetValue(normalized, out var resolved) ? resolved : null;
    }

    public string ResolveAbilityDisplayName(string? rawKeyOrName)
    {
        var trimmed = (rawKeyOrName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        var normalized = AbilityDetailsLookupService.NormalizeKey(trimmed);
        if (_abilityDetailsByKey.TryGetValue(normalized, out var resolved))
            return EvolutionService.NormalizeAbilityDisplayText(resolved.Index);

        return trimmed;
    }

    public Dictionary<string, ManuAbilityOption> BuildAbilityOptionsByName(
        IEnumerable<ManuAbilityService.ManuAbilityEntry> entries,
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races)
    {
        return entries
            .Where(e => _abilityAvailabilityService.IsAvailable(
                e.availabilityRules,
                draft,
                classes,
                races))
            .GroupBy(a => a.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var entry = g.First();
                    var details = ResolveAbilityDetails(entry.name, entry.abilityRef);
                    var adjustedCost = ApplyRaceAbilityCostModifiers(
                        Math.Max(0, entry.cost),
                        entry.name,
                        details,
                        entry.abilityRef,
                        draft,
                        races);
                    return new ManuAbilityOption(
                        entry.name ?? string.Empty,
                        adjustedCost,
                        entry.table,
                        entry.availability ?? string.Empty,
                        entry.availabilityRules ?? Array.Empty<RuleClause>(),
                        entry.description ?? string.Empty);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, ManuAbilityOption> BuildAbilityOptionsWithLabels(IEnumerable<ManuAbilityOption> entries)
    {
        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .ToDictionary(
                e => BuildAbilityOptionLabel(e),
                e => e,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, ManuAbilityOption>> SearchAbilityOptionsAsync(
        string query,
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races)
    {
        var results = await _dataProvider.SearchAbilitiesAsync(query ?? string.Empty);
        var byName = BuildAbilityOptionsByName(results, draft, classes, races);
        return BuildAbilityOptionsWithLabels(byName.Values);
    }

    public async Task<EvolutionService.AbilityResult?> FindAbilityByNameAsync(string? abilityName)
    {
        var key = AbilityDetailsLookupService.NormalizeKey(abilityName);
        if (key.Length == 0)
            return null;

        if (_abilityDetailsByKey.TryGetValue(key, out var cached))
            return cached;

        var ability = await _abilityLookupService.FindByNameAsync(abilityName);
        if (ability != null)
            _abilityDetailsByKey[key] = ability;

        return ability;
    }

    public void CacheAbilityDetails(EvolutionService.AbilityResult ability)
    {
        if (ability == null)
            return;

        var displayName = EvolutionService.NormalizeAbilityDisplayText(ability.Index);
        if (!string.IsNullOrWhiteSpace(displayName))
            _abilityDetailsByKey[AbilityDetailsLookupService.NormalizeKey(displayName)] = ability;

        var abilityKey = AbilityKey.Build(ability);
        if (!string.IsNullOrWhiteSpace(abilityKey))
            _abilityDetailsByKey[AbilityDetailsLookupService.NormalizeKey(abilityKey)] = ability;

        var legacyKey = AbilityKey.BuildEvolutionFallback(ability);
        if (!string.IsNullOrWhiteSpace(legacyKey)
            && !string.Equals(legacyKey, abilityKey, StringComparison.OrdinalIgnoreCase))
        {
            _abilityDetailsByKey[AbilityDetailsLookupService.NormalizeKey(legacyKey)] = ability;
        }
    }

    public int ApplyRaceAbilityCostModifiers(
        int baseCost,
        string? abilityName,
        EvolutionService.AbilityResult? abilityDetails,
        string? abilityRef,
        CharacterDraft draft,
        IReadOnlyDictionary<string, PeopleRecord> races)
    {
        var cost = Math.Max(0, baseCost);
        if (cost <= 0)
            return 0;

        var applyHalfElfIncrease = IsHalfElfRace(draft)
                                   && IsSpiritualAbilityForHalfElf(abilityName, abilityDetails, abilityRef, draft, races);
        var applyIshmaicIncrease = IsIshmaicRace(draft, races)
                                   && IsIshmaicCostRuleAbility(abilityName, abilityDetails);

        if (!applyHalfElfIncrease && !applyIshmaicIncrease)
            return cost;

        return ScaleCostByFiftyPercent(cost);
    }

    private static string BuildAbilityOptionLabel(ManuAbilityOption option)
    {
        var name = option.Name ?? string.Empty;
        return $"{name} ({option.Cost})";
    }

    private static bool IsHalfElfRace(CharacterDraft draft)
        => NormalizeCostRuleKey(draft.Race).Equals("halfelf", StringComparison.Ordinal);

    private static bool IsIshmaicRace(CharacterDraft draft, IReadOnlyDictionary<string, PeopleRecord> races)
    {
        var raceName = (draft.Race ?? string.Empty).Trim();
        if (raceName.Length == 0)
            return false;

        if (races.TryGetValue(raceName, out var raceRecord))
        {
            return raceRecord.PeopleType.Any(type =>
                string.Equals((type ?? string.Empty).Trim(), "Ishmaic", StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private bool IsSpiritualAbilityForHalfElf(
        string? abilityName,
        EvolutionService.AbilityResult? abilityDetails,
        string? abilityRef,
        CharacterDraft draft,
        IReadOnlyDictionary<string, PeopleRecord> races)
    {
        if (IsIshmaicCostRuleAbility(abilityName, abilityDetails))
            return true;

        var definition = ResolveAbilityDefinition(abilityName, abilityDetails, abilityRef);
        if (definition != null)
        {
            if (ContainsSpiritOrMiracleToken(definition.Type)
                || ContainsSpiritOrMiracleToken(definition.Name)
                || ContainsSpiritOrMiracleToken(definition.Effect)
                || HasSpiritSystemEffects(definition))
            {
                return true;
            }
        }

        if (ContainsSpiritOrMiracleToken(abilityName)
            || ContainsSpiritOrMiracleToken(abilityRef)
            || ContainsSpiritOrMiracleToken(abilityDetails?.AbilityRef)
            || ContainsSpiritOrMiracleToken(abilityDetails?.Description))
        {
            return true;
        }

        return false;
    }

    private bool IsIshmaicCostRuleAbility(
        string? abilityName,
        EvolutionService.AbilityResult? abilityDetails)
    {
        if (IshmaicSpiritCostRuleKeys.Contains(NormalizeCostRuleKey(abilityName)))
            return true;

        if (abilityDetails != null)
        {
            if (IshmaicSpiritCostRuleKeys.Contains(NormalizeCostRuleKey(abilityDetails.Index)))
                return true;

            if (IshmaicSpiritCostRuleKeys.Contains(NormalizeCostRuleKey(abilityDetails.AbilityRef)))
                return true;
        }

        return false;
    }

    private AbilityDefinition? ResolveAbilityDefinition(
        string? abilityName,
        EvolutionService.AbilityResult? abilityDetails,
        string? abilityRef)
    {
        if (_abilityDefinitionsByKey == null || _abilityDefinitionsByKey.Count == 0)
            return null;

        var byExplicitRef = AbilityDefinitionLookupService.Find(_abilityDefinitionsByKey, abilityRef);
        if (byExplicitRef != null)
            return byExplicitRef;

        var byDetailsRef = AbilityDefinitionLookupService.Find(_abilityDefinitionsByKey, abilityDetails?.AbilityRef);
        if (byDetailsRef != null)
            return byDetailsRef;

        var byName = AbilityDefinitionLookupService.Find(_abilityDefinitionsByKey, abilityName);
        if (byName != null)
            return byName;

        return AbilityDefinitionLookupService.Find(_abilityDefinitionsByKey, abilityDetails?.Index);
    }

    private static bool HasSpiritSystemEffects(AbilityDefinition definition)
    {
        if (definition?.SystemEffects is not { Count: > 0 } effects)
            return false;

        foreach (var effect in effects)
        {
            if (effect == null)
                continue;

            if (ContainsSpiritOrMiracleToken(effect.EffectType)
                || ContainsSpiritOrMiracleToken(effect.DisplayName)
                || ContainsSpiritOrMiracleToken(effect.ResistanceType)
                || ContainsSpiritOrMiracleToken(effect.ImmunityName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSpiritOrMiracleToken(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        return text.Contains("spirit", StringComparison.OrdinalIgnoreCase)
               || text.Contains("spiritual", StringComparison.OrdinalIgnoreCase)
               || text.Contains("miracle", StringComparison.OrdinalIgnoreCase);
    }

    private static int ScaleCostByFiftyPercent(int cost)
        => (int)Math.Ceiling(Math.Max(0, cost) * 1.5d);

    private static string NormalizeCostRuleKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace("½", "half", StringComparison.Ordinal)
            .Replace("1/2", "half", StringComparison.Ordinal)
            .Replace("&", "and", StringComparison.Ordinal)
            .Replace("resistances", "resistance", StringComparison.Ordinal);

        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    private EvolutionService.AbilityResult? ResolveAbilityDetails(string? rawNameOrKey, string? abilityRef)
    {
        var byRef = TryResolveAbilityDetails(abilityRef);
        if (byRef != null)
            return byRef;

        return TryResolveAbilityDetails(rawNameOrKey);
    }
}
