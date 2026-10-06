using labyItems.Pages.Search;
using labyItems.Services;

namespace labyItems.Pages.Calculator;

// Opens the shared search page as a multi-select picker limited to one content type.
public static class MpCatalogSearch
{
    public static Task<IReadOnlyList<EvolutionService.AbilityResult>> PickAbilitiesAsync(
        INavigation navigation,
        IEnumerable<EvolutionService.AbilityResult> abilities)
        => PickAsync(
            navigation,
            GlobalSearchKind.Ability,
            "Choose abilities",
            abilities.Where(a => !string.IsNullOrWhiteSpace(a?.Index)).ToList(),
            AbilityName,
            (_, _) => true);

    public static async Task<EvolutionService.AbilityResult?> PickAbilityAsync(
        INavigation navigation,
        IEnumerable<EvolutionService.AbilityResult> abilities)
    {
        var scope = abilities.Where(a => !string.IsNullOrWhiteSpace(a?.Index)).ToList();
        var picked = await PickAsync(navigation, GlobalSearchKind.Ability, "Choose an immunity", scope,
            AbilityName, (_, _) => true, confirmText: "Apply", singleSelection: true);
        return picked.FirstOrDefault();
    }

    public static async Task<EvolutionService.AbilityResult?> PickAbilityTemplateAsync(
        INavigation navigation,
        IEnumerable<EvolutionService.AbilityResult> abilities)
    {
        var scope = abilities.Where(a => !string.IsNullOrWhiteSpace(a?.Index)).ToList();
        var picked = await PickAsync(navigation, GlobalSearchKind.Ability, "Choose an ability", scope,
            AbilityName, (_, _) => true, confirmText: "Next", singleSelection: true);
        return picked.FirstOrDefault();
    }

    public static async Task<IReadOnlyList<string>> PickGlobalPrerequisitesAsync(
        INavigation navigation,
        IEnumerable<string>? initialSelection = null)
    {
        var page = new GlobalSearchPage(new GlobalSearchPickerOptions
        {
            Kind = GlobalSearchKind.Ability,
            Title = "Choose prerequisites",
            AllowAllKinds = true,
            ConfirmText = "Apply",
            InitialSelectedNames = new HashSet<string>(initialSelection ?? [], StringComparer.OrdinalIgnoreCase)
        });
        var picked = await page.PickAsync(navigation);
        return picked.Select(result => result.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static Task<IReadOnlyList<SpellService.SpellRaw>> PickSpellsAsync(
        INavigation navigation,
        IEnumerable<SpellService.SpellRaw> spells)
        => PickAsync(
            navigation,
            GlobalSearchKind.Spell,
            "Add spells",
            spells.Where(s => !string.IsNullOrWhiteSpace(s?.name)).ToList(),
            s => s.name,
            SpellMatches);

    public static async Task<SpellService.SpellRaw?> PickSpellTemplateAsync(
        INavigation navigation,
        IEnumerable<SpellService.SpellRaw> spells)
    {
        var picked = await PickAsync(
            navigation,
            GlobalSearchKind.Spell,
            "Choose a spell",
            spells.Where(s => !string.IsNullOrWhiteSpace(s?.name)).ToList(),
            s => s.name,
            SpellMatches,
            confirmText: "Next",
            singleSelection: true);
        return picked.FirstOrDefault();
    }

    public static Task<IReadOnlyList<MiracleService.MiracRaw>> PickMiraclesAsync(
        INavigation navigation,
        IEnumerable<MiracleService.MiracRaw> miracles)
        => PickAsync(
            navigation,
            GlobalSearchKind.Miracle,
            "Add miracles",
            miracles.Where(m => !string.IsNullOrWhiteSpace(m?.name)).ToList(),
            m => m.name,
            MiracleMatches);

    public static async Task<MiracleService.MiracRaw?> PickMiracleTemplateAsync(
        INavigation navigation,
        IEnumerable<MiracleService.MiracRaw> miracles)
    {
        var picked = await PickAsync(navigation, GlobalSearchKind.Miracle, "Choose a miracle",
            miracles.Where(m => !string.IsNullOrWhiteSpace(m?.name)).ToList(), m => m.name,
            MiracleMatches, confirmText: "Next", singleSelection: true);
        return picked.FirstOrDefault();
    }

    public static Task<IReadOnlyList<DruidEvocationService.EvocRaw>> PickEvocationsAsync(
        INavigation navigation,
        IEnumerable<DruidEvocationService.EvocRaw> evocations)
        => PickAsync(
            navigation,
            GlobalSearchKind.Evocation,
            "Add evocations",
            evocations.Where(e => !string.IsNullOrWhiteSpace(e?.name)).ToList(),
            e => e.name,
            EvocationMatches);

    public static async Task<DruidEvocationService.EvocRaw?> PickEvocationTemplateAsync(
        INavigation navigation,
        IEnumerable<DruidEvocationService.EvocRaw> evocations)
    {
        var picked = await PickAsync(
            navigation,
            GlobalSearchKind.Evocation,
            "Choose an evocation",
            evocations.Where(e => !string.IsNullOrWhiteSpace(e?.name)).ToList(),
            e => e.name,
            EvocationMatches,
            confirmText: "Next",
            singleSelection: true);
        return picked.FirstOrDefault();
    }

    public static Task<IReadOnlyList<NeuronicService.NeuronicRaw>> PickNeuronicsAsync(
        INavigation navigation,
        IEnumerable<NeuronicService.NeuronicRaw> neuronics)
        => PickAsync(
            navigation,
            GlobalSearchKind.Neuronic,
            "Add neuronics",
            neuronics.Where(n => !string.IsNullOrWhiteSpace(n?.name)).ToList(),
            n => n.name,
            NeuronicMatches);

    public static async Task<NeuronicService.NeuronicRaw?> PickNeuronicTemplateAsync(
        INavigation navigation, IEnumerable<NeuronicService.NeuronicRaw> neuronics)
    {
        var picked = await PickAsync(navigation, GlobalSearchKind.Neuronic, "Choose a neuronic",
            neuronics.Where(n => !string.IsNullOrWhiteSpace(n?.name)).ToList(), n => n.name,
            NeuronicMatches, confirmText: "Next", singleSelection: true);
        return picked.FirstOrDefault();
    }

    private static async Task<IReadOnlyList<T>> PickAsync<T>(
        INavigation navigation,
        GlobalSearchKind kind,
        string title,
        IReadOnlyList<T> scope,
        Func<T, string> getName,
        Func<T, string, bool> matchesFilter,
        string confirmText = "Save",
        bool singleSelection = false)
        where T : class
    {
        var byName = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in scope)
            byName.TryAdd(getName(item).Trim(), item);

        var page = new GlobalSearchPage(new GlobalSearchPickerOptions
        {
            Kind = kind,
            Title = title,
            AllowedNames = new HashSet<string>(byName.Keys, StringComparer.OrdinalIgnoreCase),
            IsFilterAvailable = key => scope.Any(item => matchesFilter(item, key)),
            ConfirmText = confirmText,
            SingleSelection = singleSelection
        });

        var picked = await page.PickAsync(navigation);
        var result = new List<T>();
        foreach (var r in picked)
        {
            if (byName.TryGetValue((r.Name ?? string.Empty).Trim(), out var item))
                result.Add(item);
        }

        return result;
    }

    private static bool SpellMatches(SpellService.SpellRaw spell, string key)
    {
        var (prefix, token) = Split(key);
        switch (prefix)
        {
            case "spell-tier":
                return TierMatches(token, spell.isAdvanced ?? false);
            case "spell-colour":
                var colour = Normalize(spell.colour);
                return token == "sorcorial" ? colour.Contains("sorc") : colour.Contains(token);
            default:
                return true;
        }
    }

    private static string AbilityName(EvolutionService.AbilityResult ability)
        => string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.Index : ability.DisplayName;

    private static bool MiracleMatches(MiracleService.MiracRaw miracle, string key)
    {
        var (prefix, token) = Split(key);
        switch (prefix)
        {
            case "miracle-tier":
                return TierMatches(token, miracle.isAdvanced);
            case "miracle-sphere":
                var sphere = Normalize(StripMajorMinor(miracle.sphere));
                return sphere == "universal" || sphere == token;
            default:
                return true;
        }
    }

    private static bool EvocationMatches(DruidEvocationService.EvocRaw evocation, string key)
    {
        var (prefix, token) = Split(key);
        switch (prefix)
        {
            case "evocation-tier":
                return TierMatches(token, evocation.isAdvanced);
            case "evocation-field":
                // Tolerates singular/plural differences (e.g. "forest" vs "forests").
                return (evocation.fields ?? new List<string>())
                    .Select(Normalize)
                    .Any(field => field.Length > 0 && (field.StartsWith(token) || token.StartsWith(field)));
            default:
                return true;
        }
    }

    private static bool NeuronicMatches(NeuronicService.NeuronicRaw neuronic, string key)
    {
        var (prefix, token) = Split(key);
        return prefix != "neuro-type"
               || Normalize(NeuronicService.FormatType(neuronic.Type)) == token;
    }

    private static bool TierMatches(string token, bool isAdvanced)
        => token == "advanced" ? isAdvanced : token != "handbook" || !isAdvanced;

    private static (string Prefix, string Token) Split(string key)
    {
        var index = key.IndexOf(':');
        return index < 0 ? (key, string.Empty) : (key[..index], key[(index + 1)..]);
    }

    private static string Normalize(string? value)
        => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string StripMajorMinor(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.StartsWith("Major", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Minor", StringComparison.OrdinalIgnoreCase))
        {
            return text.Length <= 5 ? string.Empty : text[5..].TrimStart(' ', ':', '-').Trim();
        }

        return text;
    }
}
