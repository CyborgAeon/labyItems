using labyItems.Models.Characters;
using labyItems.Models.Enums;
using labyItems.Models.Rules;

namespace labyItems.Services;

public sealed class AbilityAvailabilityService : IAbilityAvailabilityService
{
    private const string WarriorBracket = "🛡️ Warrior";
    private const string ScoutBracket = "⚔ Scout";
    private const string PriestBracket = "🙏 Priest";
    private const string WizardBracket = "🧙 Wizard";
    private const string DruidBracket = "🌿 Druid";
    private const string NeuroBracket = "🧠 Neuro";

    public bool IsAvailable(
        IReadOnlyList<RuleClause>? rules,
        CharacterDraft draft,
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        IReadOnlyDictionary<string, PeopleRecord> races)
    {
        var list = (rules ?? Array.Empty<RuleClause>())
            .Where(rule => rule != null && rule.IsValid)
            .ToList();
        if (list.Count == 0)
            return true;

        var className = (draft?.Class ?? string.Empty).Trim();
        var raceName = (draft?.Race ?? string.Empty).Trim();

        var classRecord = ResolveClassRecord(classes, className);
        var firstClassBrackets = ExpandClassBrackets(classRecord?.Brackets);

        var secondClasses = ResolveSecondClasses(draft);
        var secondClassBrackets = ResolveSecondClassBrackets(secondClasses, classes);

        var anyClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (className.Length > 0)
            anyClasses.Add(className);

        foreach (var second in secondClasses)
            anyClasses.Add(second);

        var anyClassBrackets = new HashSet<string>(firstClassBrackets, StringComparer.OrdinalIgnoreCase);
        foreach (var bracket in secondClassBrackets)
            anyClassBrackets.Add(bracket);

        var peopleTypes = ResolvePeopleTypesForRace(races, raceName, draft);
        var raceTags = ResolveRaceTagsForRace(races, raceName);
        var specialisationTokens = ResolveSpecialisationTokens(draft);
        var manaColours = ResolveManaColours(draft);
        var priestStyles = ResolvePriestStyles(className, classRecord, secondClasses, classes);

        var alignmentOrder = draft?.Alignment?.Order.ToString() ?? string.Empty;
        var alignmentMoral = draft?.Alignment?.Moral.ToString() ?? string.Empty;

        var legacyStatuses = ResolveLegacyStatuses(
            className,
            raceName,
            anyClasses,
            firstClassBrackets,
            secondClassBrackets,
            anyClassBrackets,
            raceTags,
            manaColours,
            priestStyles,
            specialisationTokens);

        IEnumerable<string> ResolveValues(RuleClause rule, string field)
        {
            var normalizedField = NormalizeRuleField(field);
            var specialisationValues = ResolveSpecialisationTokensByKey(draft, rule?.SpecialisationKey, specialisationTokens);

            if (rule?.Operator == RuleComparisonOp.Only
                && (normalizedField == "bracket"
                    || normalizedField == "brackets"
                    || normalizedField == "firstclassbracket"
                    || normalizedField == "firstclassbrackets"))
            {
                return anyClassBrackets;
            }

            return normalizedField switch
            {
                "class" or "classes" or "sourceclass" or "sourceclasses" or "firstclass" or "firstclasses" or "originalclass" or "originalclasses" => ToSingleValue(className),
                "secondclass" or "secondclasses" => secondClasses,
                "anyclass" or "anyclasses" => anyClasses,
                "bracket" or "brackets" or "firstclassbracket" or "firstclassbrackets" => firstClassBrackets,
                "secondclassbracket" or "secondclassbrackets" => secondClassBrackets,
                "anyclassbracket" or "anyclassbrackets" => anyClassBrackets,
                "race" or "races" or "baserace" or "baseraces" => ToSingleValue(raceName),
                "peopletype" or "peopletypes" => peopleTypes,
                "racetag" or "racetags" => raceTags,
                "specialisation" or "specialisations" or "specialization" or "specializations" => specialisationValues,
                "chosenfield" or "evocationfield" => specialisationValues,
                "manacolour" or "manacolours" or "magiccolour" or "magiccolours" or "wizardcolour" or "wizardcolours" => manaColours,
                "prieststyle" or "prieststyles" => priestStyles,
                "alignmentorder" => ToSingleValue(alignmentOrder),
                "alignmentmoral" => ToSingleValue(alignmentMoral),
                "status" or "statuses" => legacyStatuses,
                _ => Array.Empty<string>()
            };
        }

        return RuleTreeEvaluator.Evaluate(list, ResolveValues, NormalizeRuleField);
    }

    private static CharacterClassRecord? ResolveClassRecord(
        IReadOnlyDictionary<string, CharacterClassRecord> classes,
        string className)
    {
        if (className.Length == 0 || classes.Count == 0)
            return null;

        if (classes.TryGetValue(className, out var direct))
            return direct;

        var normalizedClassName = NormalizeClassKey(className);
        foreach (var pair in classes)
        {
            if (string.Equals(pair.Key, className, StringComparison.OrdinalIgnoreCase))
                return pair.Value;

            if (NormalizeClassKey(pair.Key) == normalizedClassName)
                return pair.Value;
        }

        foreach (var pair in classes)
        {
            if (className.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;

            var normalizedKey = NormalizeClassKey(pair.Key);
            if (normalizedClassName.EndsWith(normalizedKey, StringComparison.Ordinal))
                return pair.Value;
        }

        foreach (var pair in classes)
        {
            var normalizedKey = NormalizeClassKey(pair.Key);
            if (normalizedKey.Contains(normalizedClassName, StringComparison.Ordinal)
                || normalizedClassName.Contains(normalizedKey, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveSecondClasses(CharacterDraft? draft)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in draft?.MultiClassLevels ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value <= 0)
                continue;

            var key = (pair.Key ?? string.Empty).Trim();
            if (key.Length > 0)
                set.Add(key);
        }

        return set.ToList();
    }

    private static IReadOnlyList<string> ResolveSecondClassBrackets(
        IReadOnlyList<string> secondClasses,
        IReadOnlyDictionary<string, CharacterClassRecord> classes)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var secondClass in secondClasses)
        {
            var record = ResolveClassRecord(classes, secondClass);
            if (record?.Brackets != null)
            {
                foreach (var bracket in ExpandClassBrackets(record.Brackets))
                    set.Add(bracket);

                continue;
            }

            foreach (var inferred in InferBracketsFromClassName(secondClass))
                set.Add(inferred);
        }

        return set.ToList();
    }

    private static IReadOnlyList<string> InferBracketsFromClassName(string? className)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = NormalizeClassKey(className);
        if (normalized.Length == 0)
            return set.ToList();

        if (normalized.Contains("wizard", StringComparison.Ordinal)
            || normalized.Contains("warlock", StringComparison.Ordinal)
            || normalized.Contains("mage", StringComparison.Ordinal)
            || normalized.Contains("mancer", StringComparison.Ordinal)
            || normalized.Contains("sorcer", StringComparison.Ordinal))
        {
            AddBracketToken(set, WizardBracket);
        }

        if (normalized.Contains("priest", StringComparison.Ordinal)
            || normalized.Contains("shaman", StringComparison.Ordinal)
            || normalized.Contains("witchdoctor", StringComparison.Ordinal)
            || normalized.Contains("spiritual", StringComparison.Ordinal)
            || normalized.Contains("friar", StringComparison.Ordinal)
            || normalized.Contains("hospitaller", StringComparison.Ordinal)
            || normalized.Contains("vivomancer", StringComparison.Ordinal))
        {
            AddBracketToken(set, PriestBracket);
        }

        if (normalized.Contains("druid", StringComparison.Ordinal)
            || normalized.Contains("forestguardian", StringComparison.Ordinal))
        {
            AddBracketToken(set, DruidBracket);
        }

        if (normalized.Contains("warrior", StringComparison.Ordinal)
            || normalized.Contains("paladin", StringComparison.Ordinal)
            || normalized.Contains("cavalier", StringComparison.Ordinal)
            || normalized.Contains("berserker", StringComparison.Ordinal)
            || normalized.Contains("battlemaster", StringComparison.Ordinal)
            || normalized.Contains("armour", StringComparison.Ordinal)
            || normalized.Contains("smith", StringComparison.Ordinal)
            || normalized.Contains("powerhouse", StringComparison.Ordinal)
            || normalized.Contains("titan", StringComparison.Ordinal)
            || normalized.Contains("valdstayn", StringComparison.Ordinal)
            || normalized.Contains("vochstelen", StringComparison.Ordinal)
            || normalized.Contains("rovfug", StringComparison.Ordinal))
        {
            AddBracketToken(set, WarriorBracket);
        }

        if (normalized.Contains("rogue", StringComparison.Ordinal)
            || normalized.Contains("scout", StringComparison.Ordinal)
            || normalized.Contains("pathfinder", StringComparison.Ordinal)
            || normalized.Contains("assassin", StringComparison.Ordinal)
            || normalized.Contains("thug", StringComparison.Ordinal)
            || normalized.Contains("archer", StringComparison.Ordinal)
            || normalized.Contains("duelist", StringComparison.Ordinal)
            || normalized.Contains("duellist", StringComparison.Ordinal)
            || normalized.Contains("spy", StringComparison.Ordinal)
            || normalized.Contains("stalker", StringComparison.Ordinal)
            || normalized.Contains("beggar", StringComparison.Ordinal)
            || normalized.Contains("wayfinder", StringComparison.Ordinal)
            || normalized.Contains("leywalker", StringComparison.Ordinal)
            || normalized.Contains("ranger", StringComparison.Ordinal)
            || normalized.Contains("winddancer", StringComparison.Ordinal)
            || normalized.Contains("wardancer", StringComparison.Ordinal))
        {
            AddBracketToken(set, ScoutBracket);
        }

        if (normalized.Contains("psi", StringComparison.Ordinal)
            || normalized.Contains("telekin", StringComparison.Ordinal)
            || normalized.Contains("mind", StringComparison.Ordinal)
            || normalized.Contains("psycher", StringComparison.Ordinal)
            || normalized.Contains("seer", StringComparison.Ordinal)
            || normalized.Contains("adept", StringComparison.Ordinal)
            || normalized.Contains("neur", StringComparison.Ordinal)
            || normalized.Contains("silverwarden", StringComparison.Ordinal)
            || normalized.Contains("slayer", StringComparison.Ordinal))
        {
            AddBracketToken(set, NeuroBracket);
        }

        return set.ToList();
    }

    private static IReadOnlyList<string> ExpandClassBrackets(IEnumerable<string>? brackets)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bracket in brackets ?? Enumerable.Empty<string>())
            AddBracketToken(set, bracket);

        return set.ToList();
    }

    private static void AddBracketToken(HashSet<string> set, string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return;

        var canonical = CanonicalBracketLabel(trimmed);
        if (canonical.Length > 0)
            set.Add(canonical);

        set.Add(trimmed);

        var normalized = NormalizeRuleField(trimmed);
        if (normalized.Length > 0)
            set.Add(normalized);

        if (canonical.Length > 0)
        {
            var normalizedCanonical = NormalizeRuleField(canonical);
            if (normalizedCanonical.Length > 0)
                set.Add(normalizedCanonical);
        }
    }

    private static string CanonicalBracketLabel(string raw)
    {
        var normalized = NormalizeRuleField(raw);
        return normalized switch
        {
            "warrior" => WarriorBracket,
            "scout" => ScoutBracket,
            "priest" => PriestBracket,
            "wizard" => WizardBracket,
            "druid" => DruidBracket,
            "neuro" => NeuroBracket,
            _ => raw.Trim()
        };
    }

    private static IReadOnlyList<string> ResolvePeopleTypesForRace(
        IReadOnlyDictionary<string, PeopleRecord> races,
        string raceName,
        CharacterDraft? draft)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (raceName.Length == 0 || races.Count == 0)
            return ApplySubtypePeopleType(ApplyBarbarianPeopleType(resolved, draft), draft).ToList();

        if (races.TryGetValue(raceName, out var record))
        {
            foreach (var type in NormalizePeopleTypes(record))
                resolved.Add(type);

            return ApplySubtypePeopleType(ApplyBarbarianPeopleType(resolved, draft), draft).ToList();
        }

        var match = races.FirstOrDefault(pair =>
            string.Equals((pair.Key ?? string.Empty).Trim(), raceName, StringComparison.OrdinalIgnoreCase));

        if (match.Value != null)
        {
            foreach (var type in NormalizePeopleTypes(match.Value))
                resolved.Add(type);
        }

        return ApplySubtypePeopleType(ApplyBarbarianPeopleType(resolved, draft), draft).ToList();
    }

    private static IReadOnlyList<string> ResolveRaceTagsForRace(
        IReadOnlyDictionary<string, PeopleRecord> races,
        string raceName)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (raceName.Length == 0 || races.Count == 0)
            return resolved.ToList();

        PeopleRecord? record = null;
        if (races.TryGetValue(raceName, out var direct))
        {
            record = direct;
        }
        else
        {
            var match = races.FirstOrDefault(pair =>
                string.Equals((pair.Key ?? string.Empty).Trim(), raceName, StringComparison.OrdinalIgnoreCase));
            record = match.Value;
        }

        foreach (var tag in record?.Tags ?? new List<string>())
        {
            var trimmed = (tag ?? string.Empty).Trim();
            if (trimmed.Length > 0)
                resolved.Add(trimmed);
        }

        return resolved.ToList();
    }

    private static IReadOnlyList<string> ResolveSpecialisationTokens(CharacterDraft? draft)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in draft?.SpecialisationSelections ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            var key = (pair.Key ?? string.Empty).Trim();
            if (key.Length > 0)
                set.Add(key);

            var value = (pair.Value ?? string.Empty).Trim();
            if (value.Length > 0)
                set.Add(value);
        }

        foreach (var colour in draft?.ColourChoiceOverride ?? Enumerable.Empty<string>())
        {
            var value = (colour ?? string.Empty).Trim();
            if (value.Length > 0)
                set.Add(value);
        }

        return set.ToList();
    }

    private static IReadOnlyList<string> ResolveSpecialisationTokensByKey(
        CharacterDraft? draft,
        string? specialisationKey,
        IReadOnlyList<string> fallbackTokens)
    {
        var normalizedKey = NormalizeRuleField(specialisationKey);
        if (normalizedKey.Length == 0)
            return fallbackTokens;

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in draft?.SpecialisationSelections ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        {
            if (!MatchesSpecialisationKey(pair.Key, normalizedKey))
                continue;

            var value = (pair.Value ?? string.Empty).Trim();
            if (value.Length > 0)
                set.Add(value);
        }

        if (normalizedKey is "wizardcolour" or "wizardcolours" or "magiccolour" or "magiccolours")
        {
            foreach (var colour in draft?.ColourChoiceOverride ?? Enumerable.Empty<string>())
            {
                var value = (colour ?? string.Empty).Trim();
                if (value.Length > 0)
                    set.Add(value);
            }
        }

        if (normalizedKey is "racesubtype" or "subtype")
        {
            var subtype = (draft?.RaceSubtypeValue ?? string.Empty).Trim();
            if (subtype.Length > 0)
                set.Add(subtype);
        }

        return set.ToList();
    }

    private static bool MatchesSpecialisationKey(string? rawSelectionKey, string normalizedWanted)
    {
        var key = (rawSelectionKey ?? string.Empty).Trim();
        if (key.Length == 0 || normalizedWanted.Length == 0)
            return false;

        var normalizedKey = NormalizeRuleField(key);
        if (normalizedKey.Equals(normalizedWanted, StringComparison.OrdinalIgnoreCase))
            return true;

        var suffix = ExtractSelectionKeySuffix(key);
        var normalizedSuffix = NormalizeRuleField(suffix);
        return normalizedSuffix.Equals(normalizedWanted, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractSelectionKeySuffix(string key)
    {
        var raw = (key ?? string.Empty).Trim();
        if (raw.Length == 0)
            return string.Empty;

        var separator = raw.LastIndexOf("::", StringComparison.Ordinal);
        if (separator < 0 || separator + 2 >= raw.Length)
            return raw;

        return raw[(separator + 2)..].Trim();
    }

    private static IReadOnlyList<string> ResolveManaColours(CharacterDraft? draft)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tokens = new List<string>();
        if (draft?.SpecialisationSelections != null)
        {
            tokens.AddRange(draft.SpecialisationSelections.Keys);
            tokens.AddRange(draft.SpecialisationSelections.Values);
        }

        if (draft?.ColourChoiceOverride != null)
            tokens.AddRange(draft.ColourChoiceOverride);

        if (!string.IsNullOrWhiteSpace(draft?.RaceSubtypeValue))
            tokens.Add(draft.RaceSubtypeValue);

        foreach (var token in tokens)
        {
            var raw = (token ?? string.Empty).Trim();
            if (raw.Length == 0)
                continue;

            foreach (var part in raw.Split(new[] { ',', ';', '|', '/', '\\', '(', ')', ':' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<MagicColours>(part, true, out var colour))
                    set.Add(colour.ToString());
            }
        }

        return set.ToList();
    }

    private static IReadOnlyList<string> ResolvePriestStyles(
        string firstClass,
        CharacterClassRecord? firstClassRecord,
        IReadOnlyList<string> secondClasses,
        IReadOnlyDictionary<string, CharacterClassRecord> classes)
    {
        var styles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (HasPriestBracket(firstClassRecord?.Brackets))
            styles.Add(IsNonWeaponPriestClass(firstClass) ? "NonWeapon" : "Weapon");

        foreach (var secondClass in secondClasses)
        {
            var secondRecord = ResolveClassRecord(classes, secondClass);
            if (!HasPriestBracket(secondRecord?.Brackets))
                continue;

            styles.Add(IsNonWeaponPriestClass(secondClass) ? "NonWeapon" : "Weapon");
        }

        return styles.ToList();
    }

    private static bool HasPriestBracket(IEnumerable<string>? brackets)
        => ExpandClassBrackets(brackets)
            .Any(entry => NormalizeRuleField(entry).Equals("priest", StringComparison.OrdinalIgnoreCase));

    private static bool IsNonWeaponPriestClass(string? className)
    {
        var normalized = NormalizeClassKey(className);
        if (normalized.Length == 0)
            return false;

        return normalized.Equals("armouredpriest", StringComparison.Ordinal)
               || normalized.Equals("armoredpriest", StringComparison.Ordinal)
               || normalized.Equals("shaman", StringComparison.Ordinal)
               || normalized.Equals("purepriest", StringComparison.Ordinal)
               || normalized.Equals("hospitaller", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ResolveLegacyStatuses(
        string className,
        string raceName,
        IReadOnlyCollection<string> anyClasses,
        IReadOnlyCollection<string> firstClassBrackets,
        IReadOnlyCollection<string> secondClassBrackets,
        IReadOnlyCollection<string> anyClassBrackets,
        IReadOnlyCollection<string> raceTags,
        IReadOnlyCollection<string> manaColours,
        IReadOnlyCollection<string> priestStyles,
        IReadOnlyCollection<string> specialisationTokens)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in anyClasses)
            set.Add(value);

        foreach (var value in specialisationTokens)
            set.Add(value);

        if (anyClasses.Any(value => NormalizeClassKey(value).Equals("devoutbeliever", StringComparison.Ordinal)))
            set.Add("Devout Believer");

        if (ContainsBracket(firstClassBrackets, WarriorBracket))
            set.Add("1st Class Warrior Subclass");

        if (ContainsBracket(secondClassBrackets, WarriorBracket))
            set.Add("2nd Class Warrior Subclass");

        if (ContainsBracket(anyClassBrackets, WizardBracket))
            set.Add("Mana User");

        if (manaColours.Any(c => c.Equals("Red", StringComparison.OrdinalIgnoreCase)))
            set.Add("Red Mana User");

        if (manaColours.Any(c => c.Equals("Black", StringComparison.OrdinalIgnoreCase)))
            set.Add("Black Mana User");

        if (priestStyles.Any(style => style.Equals("Weapon", StringComparison.OrdinalIgnoreCase)))
            set.Add("Weapon Using Priest");

        if (priestStyles.Any(style => style.Equals("NonWeapon", StringComparison.OrdinalIgnoreCase)))
            set.Add("Non-Weapon Using Priest");

        var isFirstClassWizard = NormalizeClassKey(className) is "wizard" or "highwizard";
        var isHalfElf = NormalizeRaceName(raceName) is "halfelf";
        var isElf = NormalizeRaceName(raceName) is "elf";
        var isSpiritless = raceTags.Any(tag => NormalizeRuleField(tag).Equals("spiritless", StringComparison.OrdinalIgnoreCase));

        if (isFirstClassWizard && (isElf || isSpiritless))
            set.Add("Elf/Spiritless 1st Class Wizard/High Wizard");

        if (isSpiritless && !isFirstClassWizard)
            set.Add("Other Spiritless");

        if (isHalfElf && isFirstClassWizard)
            set.Add("Half-Elf 1st Class Wizard/High Wizard");

        if (isHalfElf && ContainsBracket(anyClassBrackets, WizardBracket))
            set.Add("Half-Elf Wizard Bracket");

        return set.ToList();
    }

    private static bool ContainsBracket(IEnumerable<string> brackets, string wanted)
    {
        var normalizedWanted = NormalizeRuleField(wanted);
        return brackets.Any(value => NormalizeRuleField(value).Equals(normalizedWanted, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeRaceName(string? raw)
        => NormalizeRuleField(raw);

    private static IReadOnlyList<string> NormalizePeopleTypes(PeopleRecord record)
    {
        return (record?.PeopleType ?? new List<string>())
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> ApplyBarbarianPeopleType(HashSet<string> peopleTypes, CharacterDraft? draft)
    {
        if (HasBarbarianPeopleType(draft))
            peopleTypes.Add("Tribal");

        return peopleTypes;
    }

    private static HashSet<string> ApplySubtypePeopleType(HashSet<string> peopleTypes, CharacterDraft? draft)
    {
        var subtype = (draft?.RaceSubtypeValue ?? draft?.RaceSubtype ?? string.Empty).Trim();
        if (subtype.Equals("Verdant Heart", StringComparison.OrdinalIgnoreCase))
            peopleTypes.Add("Tribal");

        return peopleTypes;
    }

    private static bool HasBarbarianPeopleType(CharacterDraft? draft)
    {
        if (IsBarbarianSelection(draft?.RaceSubtypeValue) || IsBarbarianSelection(draft?.RaceSubtype))
            return true;

        if (draft?.SpecialisationSelections == null || draft.SpecialisationSelections.Count == 0)
            return false;

        if (draft.SpecialisationSelections.TryGetValue("Barbarian", out var directSelection)
            && IsBarbarianSelection(directSelection))
        {
            return true;
        }

        return draft.SpecialisationSelections.Values.Any(IsBarbarianSelection);
    }

    private static bool IsBarbarianSelection(string? value)
    {
        var token = (value ?? string.Empty).Trim();
        return token.Length > 0
            && (token.Equals("Barbarian", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("Barbarian", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ToSingleValue(string value)
        => string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value.Trim() };

    private static string NormalizeClassKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return new string(raw.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static string NormalizeRuleField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Trim().Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
