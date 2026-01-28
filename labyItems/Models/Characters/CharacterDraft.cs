using System;
using System.Collections.Generic;
using System.Linq;

namespace labyItems.Models.Characters;

public sealed class CharacterDraft
{
    public CharacterDraft()
    {
        AvailableAlignments = AllAlignments().OrderBy(a => a.Order).ThenBy(a => a.Moral).ToList();
    }
    public string? Race { get; set; }
    public string? Class { get; set; }
    public string Name { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Notes { get; set; } = "";
    public string RaceSubtypeKey { get; set; } = "";
    public string RaceSubtypeValue { get; set; } = "";
    public string LifeScaleKeyOverride { get; set; } = "";
    public string? RaceSubtype { get; set; }

    public List<string> Guilds { get; set; } = new();
    public Dictionary<string, string> SpecialisationSelections { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> GuildBenefitSelections { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<string> AdvancementAbilities { get; set; } = new();
    public List<string> AdvancementItems { get; set; } = new();
    public List<MiracleListDraft> MiracleLists { get; set; } = new();
    public List<SpellListDraft> SpellLists { get; set; } = new();
    public List<EvocationListDraft> EvocationLists { get; set; } = new();
    public Dictionary<string, int> PowerPools { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public GuildOverrideRules? GuildOverrideRules { get; set; } = new();
    public Alignment? Alignment { get; set; }
    public List<Alignment> AvailableAlignments { get; private set; } = new();

    public int Points { get; set; }
    public int CurrentVitae { get; set; }

    public int TBLP { get; set; }
    public int Loc { get; set; }
    public int MaxAC { get; set; }
    public int DAC { get; set; }
    public int ClassRaceArmour { get; set; }
    public int WornArmour { get; set; } = 0;
    public int? SAC { get; set; }
    public int? MAC { get; set; }
    public string ArmourAvailability { get; set; } = "";
    public string ArmourAvailabilityOverride { get; set; } = "";
    public List<string> ColourChoiceOverride { get; } = new();
    public Dictionary<string, int> ResistanceLevels { get; set; } = new Dictionary<string, int> {
        { "Spirit", 8 },
        { "Magic", 8 },
        { "Physical", 8 },
        { "Neuro", 8 }
    };
    public Dictionary<string, string> ResistancesByType { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<AbilityDraft> Abilities { get; set; } = new();
    public List<InnateAbilityDraft> Innates { get; set; } = new();

    public bool IsRaceAndClassSelected =>
        !string.IsNullOrWhiteSpace(Race) && !string.IsNullOrWhiteSpace(Class);

    public void SetAvailableAlignmentsFromRules(IEnumerable<AlignmentRule?> rules)
    {
        var set = ComputeAvailableAlignments(rules);
        AvailableAlignments = set.OrderBy(a => a.Order).ThenBy(a => a.Moral).ToList();

        // If the current selection is no longer valid, clear it
        if (Alignment is Alignment current && !set.Contains(current))
            Alignment = null;
    }

    public static HashSet<Alignment> ComputeAvailableAlignments(IEnumerable<AlignmentRule?> rules)
    {
        var universe = AllAlignments();
        var nonNullRules = rules.Where(r => r is not null).Cast<AlignmentRule>().ToList();
        var setRules = nonNullRules
            .Where(r => r.Mode.Equals("set", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (setRules.Count > 0)
        {
            var setAllowed = new HashSet<Alignment>(universe);
            foreach (var r in setRules)
                setAllowed.IntersectWith(ToAllowedSet(r, universe));

            universe = setAllowed;
        }

        foreach (var r in nonNullRules.Where(r => !r.Mode.Equals("set", StringComparison.OrdinalIgnoreCase)))
            universe.IntersectWith(ToAllowedSet(r, universe));

        return universe;
    }

    private static HashSet<Alignment> ToAllowedSet(AlignmentRule rule, HashSet<Alignment> universe)
    {
        var allowed = new HashSet<Alignment>(universe);
        if (rule.Allowed is not null)
        {
            if (rule.Allowed.Moral is { Count: > 0 } morals)
                allowed.RemoveWhere(a => !morals.Contains(a.Moral));

            if (rule.Allowed.Order is { Count: > 0 } orders)
                allowed.RemoveWhere(a => !orders.Contains(a.Order));
        }

        if (rule.AllowedPairs is { Count: > 0 } pairs)
        {
            var pairSet = ParsePairs(pairs);
            allowed.IntersectWith(pairSet);
        }

        return allowed;
    }

    private static HashSet<Alignment> AllAlignments()
    {
        var set = new HashSet<Alignment>();
        foreach (var order in Enum.GetValues<OrderAxis>())
            foreach (var moral in Enum.GetValues<MoralAxis>())
                set.Add(new Alignment(order, moral));

        return set;
    }

    private static HashSet<Alignment> ParsePairs(IEnumerable<string> pairs)
    {
        var set = new HashSet<Alignment>();

        foreach (var raw in pairs)
        {
            var s = (raw ?? "").Trim();
            if (s.Length == 0) continue;

            if (s.Equals("True Neutral", StringComparison.OrdinalIgnoreCase))
            {
                set.Add(new Alignment(OrderAxis.Neutral, MoralAxis.Neutral));
                continue;
            }

            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new FormatException($"Invalid alignment pair '{raw}'. Expected 'Order Moral' e.g. 'Lawful Good'.");

            if (!Enum.TryParse<OrderAxis>(parts[0], ignoreCase: true, out var order))
                throw new FormatException($"Invalid order axis '{parts[0]}' in alignment pair '{raw}'.");

            if (!Enum.TryParse<MoralAxis>(parts[1], ignoreCase: true, out var moral))
                throw new FormatException($"Invalid moral axis '{parts[1]}' in alignment pair '{raw}'.");

            set.Add(new Alignment(order, moral));
        }

        return set;
    }
}
