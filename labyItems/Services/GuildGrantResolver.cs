using labyItems.Models.Guilds;
using labyItems.Models.Rules;

namespace labyItems.Services;

public static class GuildGrantResolver
{
    public static GuildGrantResolutionResult Resolve(
        IEnumerable<GuildGrantNode> activeNodes,
        GuildGrantResolutionContext context)
    {
        var nodes = activeNodes.ToArray();
        var state = new Resolution(context, nodes, []);
        state.Visit(nodes, "", null, null, []);
        if (state.CrossScopeUpgrades.Count > 0)
        {
            // Linked higher-tier branches can upgrade grants already awarded by an earlier branch.
            state = new Resolution(context, nodes, state.CrossScopeUpgrades);
            state.Visit(nodes, "", null, null, []);
        }
        return state.BuildResult();
    }

    public static bool IsAdventureSelection(string? timing)
        => timing is "Battleboard" or "OnDayStart" or "OnAdventureStart" or "OnSectionStart" or "OnUse";

    private sealed class Resolution
    {
        private readonly GuildGrantResolutionContext _context;
        private readonly HashSet<string> _declared = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _instances = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<string>> _appliedSelections = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _distinctNestedSelections = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<string>> _migratedSelections = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<string>> _migratedAdventureSelections = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<GuildGrantNode>> _legacyLinks = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _handledLegacyKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ResolvedGuildGrant> _grants = [];
        private readonly List<GuildGrantChoiceState> _choices = [];
        private readonly List<GuildGrantDiagnostic> _diagnostics = [];
        private readonly HashSet<string> _miracleAdditions = new(StringComparer.OrdinalIgnoreCase);
        private readonly IReadOnlyList<GuildGrantNode> _crossScopeUpgrades;
        private readonly Dictionary<GuildGrantNode, int> _progressionOrder = new(ReferenceEqualityComparer.Instance);

        public Resolution(
            GuildGrantResolutionContext context,
            IReadOnlyList<GuildGrantNode> nodes,
            IReadOnlyList<GuildGrantNode> crossScopeUpgrades)
        {
            _context = context;
            _crossScopeUpgrades = crossScopeUpgrades;
            RegisterNodes(nodes);
        }

        public List<GuildGrantNode> CrossScopeUpgrades { get; } = [];

        public GuildGrantResolutionResult BuildResult()
        {
            foreach (var key in _context.LegacySelections.Keys)
            {
                if (key.StartsWith($"{_context.GuildName}::", StringComparison.OrdinalIgnoreCase)
                    && !_handledLegacyKeys.Contains(key))
                    Report("UnmappedLegacySelection", key, "Legacy choice requires review.");
            }

            return new(_grants, _choices, _diagnostics, _migratedSelections, _migratedAdventureSelections, _miracleAdditions.ToArray());
        }

        private void RegisterNodes(IEnumerable<GuildGrantNode> nodes)
        {
            foreach (var node in nodes)
            {
                _progressionOrder.TryAdd(node, _progressionOrder.Count);
                if (node.Id.Length != 0 && node.Kind != "Upgrade")
                    _declared.Add(node.Id);
                if (node.SelectionRef is { } link && node.LegacySelectionKey != null)
                {
                    if (!_legacyLinks.TryGetValue(link, out var choices))
                        _legacyLinks[link] = choices = [];
                    choices.Add(node);
                }

                RegisterNodes(node.Grants);
                if (node.Choice is { } choice)
                    RegisterNodes([choice]);
                if (node.Replacement?.Choice is { } replacementChoice)
                    RegisterNodes([replacementChoice]);
                foreach (var option in node.Options)
                    RegisterNodes(option.Grants);
            }
        }

        public void Visit(
            IReadOnlyList<GuildGrantNode> nodes,
            string path,
            string? timing,
            string? repeatScope,
            IReadOnlyList<GuildGrantNode> inheritedUpgrades)
        {
            var localUpgrades = nodes.Where(node => node.Kind == "Upgrade").Where(IsActiveUpgrade).ToList();
            if (path.Length == 0)
            {
                localUpgrades.AddRange(_crossScopeUpgrades);
            }
            else if (repeatScope == null)
            {
                foreach (var upgrade in localUpgrades.Where(upgrade => !ContainsTarget(nodes, upgrade.TargetGrantId)))
                    CrossScopeUpgrades.Add(upgrade);
                localUpgrades.RemoveAll(upgrade => !ContainsTarget(nodes, upgrade.TargetGrantId));
            }
            var upgrades = localUpgrades.Count == 0
                ? inheritedUpgrades
                : inheritedUpgrades.Concat(localUpgrades).OrderBy(upgrade => _progressionOrder.GetValueOrDefault(upgrade, int.MaxValue)).ToArray();
            foreach (var node in nodes)
            {
                if (node.Kind == "Upgrade" || !IsActive(node))
                    continue;

                var identity = Append(path, node.Id);
                if (node.Id.Length == 0 || !_instances.Add(identity))
                {
                    Report("InvalidGrantId", identity, "Grant IDs must be present and unique within their scope.");
                    continue;
                }

                VisitNode(node, identity, timing, repeatScope, upgrades);
            }
        }

        private void VisitNode(
            GuildGrantNode node,
            string identity,
            string? timing,
            string? repeatScope,
            IReadOnlyList<GuildGrantNode> upgrades)
        {
            var effective = node;
            var patches = new List<GuildGrantPatch>();
            foreach (var upgrade in upgrades)
            {
                if (!Same(upgrade.TargetGrantId, node.Id))
                    continue;

                switch (upgrade.Mode)
                {
                    case "Remove":
                        return;
                    case "Replace" when node.Kind == "Grant":
                        effective = upgrade.Replacement ?? upgrade with { Kind = "Grant" };
                        patches.Clear();
                        break;
                    case "Patch" when upgrade.Patch != null:
                        patches.Add(upgrade.Patch);
                        break;
                    default:
                        Report("IncompatibleUpgrade", upgrade.Id, $"Cannot apply '{upgrade.Mode}' to '{node.Id}'.");
                        break;
                }
            }

            switch (effective.Kind)
            {
                case "Grant":
                    AddGrant(node.Id, identity, effective, patches);
                    if (effective.Choice is { } nested)
                        Visit([nested], identity, timing, repeatScope, upgrades);
                    break;
                case "Choice":
                    ResolveChoice(effective, identity, timing, repeatScope, upgrades, patches);
                    break;
                case "Conditional":
                    Visit(effective.Grants, identity, timing, repeatScope, upgrades);
                    break;
                case "MiracleListAdd":
                    if (!string.IsNullOrWhiteSpace(effective.MiracleKey))
                        _miracleAdditions.Add(effective.MiracleKey);
                    else
                        Report("MissingMiracle", node.Id, "The miracle access grant has no miracle key.");
                    break;
                default:
                    Report("UnknownGrantKind", node.Id, $"Unknown grant kind '{node.Kind}'.");
                    break;
            }
        }

        private void AddGrant(
            string id,
            string identity,
            GuildGrantNode node,
            IReadOnlyList<GuildGrantPatch> patches)
        {
            if (string.IsNullOrWhiteSpace(node.Ability.AbilityRef)
                && string.IsNullOrWhiteSpace(node.Ability.Name))
            {
                Report("MissingAbility", id, "The grant has no ability reference.");
                return;
            }

            _grants.Add(new(id, $"{_context.GuildName}::{identity}", node.Ability, patches));
        }

        private void ResolveChoice(
            GuildGrantNode node,
            string identity,
            string? inheritedTiming,
            string? repeatScope,
            IReadOnlyList<GuildGrantNode> upgrades,
            IReadOnlyList<GuildGrantPatch> patches)
        {
            var timing = node.Timing ?? inheritedTiming;
            var choiceIndex = _choices.Count;
            if (node.Mode == "Review")
                Report("UnverifiedChoice", identity, "This choice requires source review.");
            var options = GetOptions(node);
            var distinct = GetDistinctSelections(node, repeatScope);
            var allowance = node.Select ?? 1;
            var grantAll = false;
            foreach (var patch in patches)
            {
                allowance = patch.Select ?? allowance;
                grantAll |= patch.GrantAll;
            }
            if (allowance < 0 || (!grantAll && allowance > options.Count && options.All(option => !option.Option.Repeatable)))
            {
                Report("InvalidChoiceCount", identity, $"Cannot select {allowance} from the declared options.");
                allowance = Math.Clamp(allowance, 0, options.Count);
            }

            if (!string.IsNullOrWhiteSpace(node.Ability.AbilityRef))
                AddGrant(node.Id, identity, node, patches);

            var selected = GetSelections(node, identity, timing, options, grantAll, out var migrated);
            var applied = new List<string>();
            var slots = new string[Math.Min(selected.Count, grantAll ? options.Count : allowance)];
            Array.Fill(slots, "");
            var occurrenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var selectable = distinct is { Count: > 0 }
                ? options.Select(option => distinct.Contains(option.Option.Id)
                    ? option with { Eligibility = GuildGrantEligibility.Unavailable }
                    : option).ToArray()
                : options;
            for (var slot = 0; slot < selected.Count; slot++)
            {
                var optionId = selected[slot];
                if (string.IsNullOrWhiteSpace(optionId))
                    continue;
                if (slot >= slots.Length)
                {
                    Report("ChoiceAllowanceExceeded", identity, $"Option '{optionId}' exceeds the selection allowance.");
                    continue;
                }
                var state = options.FirstOrDefault(candidate => Same(candidate.Option.Id, optionId));
                if (state == null)
                {
                    Report("InvalidSelection", identity, $"Option '{optionId}' is not declared.");
                    continue;
                }
                if (state.Eligibility != GuildGrantEligibility.Available)
                {
                    Report("OptionEligibility", identity, $"Option '{optionId}' is {state.Eligibility.ToString().ToLowerInvariant()}.");
                    continue;
                }
                var option = state.Option;
                var previous = occurrenceCounts.GetValueOrDefault(option.Id);
                if ((previous > 0 && !option.Repeatable) || (distinct != null && !distinct.Add(option.Id)))
                {
                    Report("DuplicateSelection", identity, $"Option '{optionId}' cannot be selected again.");
                    continue;
                }

                applied.Add(option.Id);
                slots[slot] = option.Id;
                occurrenceCounts[option.Id] = previous + 1;
                var optionPath = Append(identity, $"{option.Id}~{(option.Repeatable ? slot + 1 : 1)}");
                var optionRepeatScope = option.Repeatable ? Append(identity, option.Id) : repeatScope;
                var optionPatches = patches
                    .Where(patch => patch.OptionOverrides != null)
                    .SelectMany(patch => patch.OptionOverrides!)
                    .Where(pair => Same(pair.Key, option.Id))
                    .Select(pair => new GuildGrantPatch { Overrides = pair.Value })
                    .ToArray();
                var optionUpgrades = option.Grants.Count == 1 && optionPatches.Length != 0
                    ? upgrades.Concat(optionPatches.Select(patch => new GuildGrantNode
                    {
                        Kind = "Upgrade",
                        Mode = "Patch",
                        TargetGrantId = option.Grants[0].Id,
                        Patch = patch
                    })).ToArray()
                    : upgrades;
                if (option.Grants.Count != 1 && optionPatches.Length != 0)
                    Report("AmbiguousOptionUpgrade", identity, $"Option '{optionId}' does not identify one grant to modify.");

                Visit(option.Grants, optionPath, timing, optionRepeatScope, optionUpgrades);
            }

            _appliedSelections[identity] = applied;
            if (migrated && applied.Count > 0)
            {
                var migrations = IsAdventureSelection(timing) ? _migratedAdventureSelections : _migratedSelections;
                migrations[identity] = applied;
            }
            if (node.SelectionRef == null)
                _choices.Insert(choiceIndex, new(identity, node, timing, allowance, selectable, slots, grantAll));
        }

        private IReadOnlyList<GuildGrantChoiceOptionState> GetOptions(GuildGrantNode node)
        {
            var options = new List<GuildGrantChoiceOptionState>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Add(node.Options);
            foreach (var reference in node.ChoiceSetRefs)
            {
                var set = _context.ResolveChoiceSet(reference);
                if (set == null)
                {
                    Report("MissingChoiceSet", node.Id, $"Choice set '{reference}' could not be resolved.");
                    continue;
                }

                Add(set);
            }
            return options;

            void Add(IReadOnlyList<GuildGrantOption> entries)
            {
                foreach (var option in entries)
                {
                    if (option.Id.Length == 0 || !seen.Add(option.Id))
                    {
                        Report("InvalidOptionId", node.Id, $"Option ID '{option.Id}' must be present and unique.");
                        continue;
                    }
                    options.Add(new(option, Evaluate(option.Rules)));
                }
            }
        }

        private IReadOnlyList<string> GetSelections(
            GuildGrantNode node,
            string identity,
            string? timing,
            IReadOnlyList<GuildGrantChoiceOptionState> options,
            bool grantAll,
            out bool migrated)
        {
            migrated = false;
            if (grantAll)
                return options.Select(option => option.Option.Id).ToArray();

            if (!string.IsNullOrWhiteSpace(node.SelectionRef))
            {
                if (_appliedSelections.TryGetValue(node.SelectionRef, out var linkedSelection))
                    return linkedSelection;
                Report("UnresolvedChoiceLink", node.Id, $"Choice '{node.SelectionRef}' is not active.");
                return [];
            }

            var adventure = IsAdventureSelection(timing);
            var selections = adventure ? _context.AdventureSelections : _context.Selections;
            if (selections.TryGetValue(identity, out var saved))
            {
                CheckLinkedLegacySelections(node, ReadLegacySelection(node));
                return saved;
            }

            if (adventure && _context.CompletedAdventureMigrations.Contains(identity))
            {
                CheckLinkedLegacySelections(node, ReadLegacySelection(node));
                return [];
            }

            var basic = ReadLegacySelection(node);
            var linked = CheckLinkedLegacySelections(node, basic);
            var candidate = basic ?? linked;
            if (candidate != null && options.Any(option => Same(option.Option.Id, candidate)))
            {
                migrated = true;
                return [candidate];
            }
            if (candidate != null)
                Report("UnmappedLegacySelection", node.Id, $"Legacy option '{candidate}' requires review.");
            return [];
        }

        private string? ReadLegacySelection(GuildGrantNode node)
        {
            if (node.LegacySelectionKey is not { } key || !_context.LegacySelections.TryGetValue(key, out var index))
                return null;

            _handledLegacyKeys.Add(key);
            if (index > 0 && index <= node.LegacyOptionIds.Count)
                return node.LegacyOptionIds[index - 1];

            Report("UnmappedLegacySelection", node.Id, $"Legacy choice '{key}' requires review.");
            return null;
        }

        private string? CheckLinkedLegacySelections(GuildGrantNode node, string? primary)
        {
            if (!_legacyLinks.TryGetValue(node.Id, out var links))
                return null;

            var linked = links.Select(ReadLegacySelection).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (linked.Length > 1 || (primary != null && linked.Any(value => !Same(value, primary))))
            {
                Report("ConflictingLegacySelection", node.Id, "Saved linked choices disagree and require review.");
                return null;
            }
            return linked.FirstOrDefault();
        }

        private HashSet<string>? GetDistinctSelections(GuildGrantNode node, string? repeatScope)
        {
            if (!node.DistinctAcrossRepeats || repeatScope == null)
                return null;
            var key = Append(repeatScope, node.Id);
            if (!_distinctNestedSelections.TryGetValue(key, out var options))
            {
                options = new(StringComparer.OrdinalIgnoreCase);
                _distinctNestedSelections.Add(key, options);
            }
            return options;
        }

        private bool IsActiveUpgrade(GuildGrantNode node)
        {
            if (!IsActive(node))
                return false;
            if (node.Mode is "Legacy" or "LegacyOverwrite" or "Review")
            {
                Report("LegacyUpgrade", node.Id, "This upgrade needs a verified target and operation.");
                return false;
            }
            if (node.TargetGrantId == null || !_declared.Contains(node.TargetGrantId))
            {
                Report("MissingUpgradeTarget", node.Id, $"Upgrade target '{node.TargetGrantId}' is not declared in active progression.");
                return false;
            }
            return true;
        }

        private bool IsActive(GuildGrantNode node)
        {
            var eligibility = Evaluate(node.Rules);
            if (eligibility == GuildGrantEligibility.Unknown)
                Report("UnknownEligibility", node.Id, "Required rule context is unavailable.");
            return eligibility == GuildGrantEligibility.Available;
        }

        private GuildGrantEligibility Evaluate(IReadOnlyList<RuleClause> rules)
        {
            var unknown = false;
            foreach (var rule in rules)
            {
                var fields = rule.Field.Split(['|', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var values = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in fields)
                {
                    var fieldValues = _context.ResolveRuleField(field);
                    if (fieldValues != null)
                        values[field] = fieldValues;
                }

                if (values.Count != fields.Length)
                {
                    unknown = true;
                    continue;
                }
                if (!RuleTreeEvaluator.Evaluate([rule], field => values[field]))
                    return GuildGrantEligibility.Unavailable;
            }

            return unknown ? GuildGrantEligibility.Unknown : GuildGrantEligibility.Available;
        }

        private void Report(string code, string id, string message) => _diagnostics.Add(new(code, id, message));

        private static bool Same(string? left, string? right)
            => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private static bool ContainsTarget(IReadOnlyList<GuildGrantNode> nodes, string? id)
            => nodes.Any(node => node.Kind != "Upgrade" && Same(node.Id, id)
                || ContainsTarget(node.Grants, id)
                || node.Choice is { } choice && ContainsTarget([choice], id)
                || node.Options.Any(option => ContainsTarget(option.Grants, id)));

        private static string Append(string path, string segment)
            => path.Length == 0 ? segment : $"{path}/{segment}";
    }
}
