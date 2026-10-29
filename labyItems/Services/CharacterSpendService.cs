using labyItems.Models.Characters;
using labyItems.Models.ViewModels;

namespace labyItems.Services;

public sealed class CharacterSpendService
{
    public const string DefaultSpendName = "Current";

    public CharacterSpend Create(CharacterDraft draft, string name)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var normalizedName = NormalizeName(name);
        draft.Spends ??= new Dictionary<string, CharacterSpend>(StringComparer.OrdinalIgnoreCase);
        if (draft.Spends.ContainsKey(normalizedName))
            throw new InvalidOperationException($"A spend named '{normalizedName}' already exists.");

        var spend = new CharacterSpend { Name = normalizedName };
        draft.Spends[normalizedName] = spend;
        draft.SelectedSpendName = normalizedName;
        SynchronizeCharacterCreationChoices(draft, spend);
        return spend;
    }

    public CharacterSpend GetOrCreateSelected(CharacterDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        draft.Spends ??= new Dictionary<string, CharacterSpend>(StringComparer.OrdinalIgnoreCase);

        var selected = (draft.SelectedSpendName ?? string.Empty).Trim();
        if (selected.Length > 0 && draft.Spends.TryGetValue(selected, out var existing))
        {
            SynchronizeCharacterCreationChoices(draft, existing);
            return existing;
        }

        var first = draft.Spends
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(first.Key) && first.Value != null)
        {
            draft.SelectedSpendName = first.Key;
            SynchronizeCharacterCreationChoices(draft, first.Value);
            return first.Value;
        }

        return Create(draft, DefaultSpendName);
    }

    public CharacterSpend Select(CharacterDraft draft, string name)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var normalizedName = NormalizeName(name);
        if (draft.Spends == null || !draft.Spends.TryGetValue(normalizedName, out var spend))
            throw new KeyNotFoundException($"Spend '{normalizedName}' was not found.");

        draft.SelectedSpendName = normalizedName;
        SynchronizeCharacterCreationChoices(draft, spend);
        return spend;
    }

    public void AddEntry(CharacterSpend spend, CharacterSpendEntry entry)
    {
        ArgumentNullException.ThrowIfNull(spend);
        ArgumentNullException.ThrowIfNull(entry);
        spend.Entries ??= new List<CharacterSpendEntry>();
        NormalizeEntry(entry);
        ValidateNextLevel(spend.Entries, entry);
        entry.Position = spend.Entries.Count + 1;
        spend.Entries.Add(entry);
    }

    public void RemoveEntries(CharacterSpend spend, IEnumerable<string> entryIds)
    {
        ArgumentNullException.ThrowIfNull(spend);
        var ids = BuildIdSet(entryIds);
        spend.Entries ??= new List<CharacterSpendEntry>();
        spend.Entries.RemoveAll(entry => ids.Contains(entry.Id));
        NormalizePositions(spend);
    }

    public void MoveToTop(CharacterSpend spend, IEnumerable<string> entryIds)
        => MoveToPosition(spend, entryIds, 1);

    public void MoveToBottom(CharacterSpend spend, IEnumerable<string> entryIds)
    {
        ArgumentNullException.ThrowIfNull(spend);
        MoveToPosition(spend, entryIds, Math.Max(1, spend.Entries?.Count ?? 0));
    }

    public void MoveToPosition(CharacterSpend spend, IEnumerable<string> entryIds, int position)
    {
        ArgumentNullException.ThrowIfNull(spend);
        spend.Entries ??= new List<CharacterSpendEntry>();
        var ids = BuildIdSet(entryIds);
        if (ids.Count == 0 || spend.Entries.Count == 0)
            return;

        var ordered = OrderEntries(spend.Entries);
        var selected = ordered.Where(entry => ids.Contains(entry.Id)).ToList();
        if (selected.Count == 0)
            return;

        var remainder = ordered.Where(entry => !ids.Contains(entry.Id)).ToList();
        var insertionIndex = Math.Clamp(position - 1, 0, remainder.Count);
        remainder.InsertRange(insertionIndex, selected);
        spend.Entries = remainder;
        SetPositionsInCurrentOrder(spend);
    }

    public IReadOnlyList<CharacterSpendRowVm> BuildRows(CharacterSpend spend)
    {
        ArgumentNullException.ThrowIfNull(spend);
        var runningTotal = 0;
        var rows = new List<CharacterSpendRowVm>();
        foreach (var entry in OrderEntries(spend.Entries ?? new List<CharacterSpendEntry>()))
        {
            runningTotal += Math.Max(0, entry.Cost);
            rows.Add(new CharacterSpendRowVm
            {
                Id = entry.Id,
                Position = entry.Position,
                Type = entry.Type,
                Name = entry.Name,
                Cost = Math.Max(0, entry.Cost),
                RunningTotal = runningTotal,
                CharacterLevel = entry.IsCharacterCreationChoice ? entry.CharacterLevel : null,
                PurchaseLevel = entry.PurchaseLevel
            });
        }

        return rows;
    }

    public void SynchronizeCharacterCreationChoices(CharacterDraft draft, CharacterSpend spend)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(spend);
        spend.Entries ??= new List<CharacterSpendEntry>();

        var existingByKey = spend.Entries
            .Where(entry => entry.IsCharacterCreationChoice)
            .GroupBy(entry => BuildCreationChoiceKey(entry.Key, entry.CharacterLevel), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var desiredByKey = new Dictionary<string, CharacterSpendEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in draft.Abilities ?? new List<AbilityDraft>())
        {
            var source = (ability?.Source ?? string.Empty).Trim();
            var name = (ability?.Name ?? string.Empty).Trim();
            var level = ability?.LevelGained;
            if (!source.StartsWith("Specialisation:", StringComparison.OrdinalIgnoreCase)
                || name.Length == 0
                || level is not (>= 1 and <= 8))
            {
                continue;
            }

            var key = (ability?.AbilityKey ?? string.Empty).Trim();
            if (key.Length == 0)
                key = name;
            var identity = BuildCreationChoiceKey(key, level);
            if (!existingByKey.TryGetValue(identity, out var entry))
            {
                entry = new CharacterSpendEntry
                {
                    Type = CharacterSpendEntryType.Ability,
                    Key = key,
                    Cost = 0,
                    CharacterLevel = level,
                    IsCharacterCreationChoice = true
                };
            }

            entry.Name = name;
            desiredByKey[identity] = entry;
        }

        var synchronized = new List<CharacterSpendEntry>();
        foreach (var entry in spend.Entries.OrderBy(entry => entry.Position))
        {
            if (!entry.IsCharacterCreationChoice)
            {
                synchronized.Add(entry);
                continue;
            }

            var identity = BuildCreationChoiceKey(entry.Key, entry.CharacterLevel);
            if (!desiredByKey.Remove(identity, out var desired))
                continue;
            synchronized.Add(desired);
        }

        synchronized.AddRange(desiredByKey.Values
            .OrderBy(entry => entry.CharacterLevel)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase));
        spend.Entries = synchronized;
        SetPositionsInCurrentOrder(spend);
    }

    private static void ValidateNextLevel(IEnumerable<CharacterSpendEntry> existing, CharacterSpendEntry candidate)
    {
        if (candidate.Type == CharacterSpendEntryType.Ability)
            return;

        var level = candidate.PurchaseLevel.GetValueOrDefault();
        if (level <= 0)
            throw new InvalidOperationException("Multi-class and multi-race entries require a positive purchase level.");

        var previousLevel = existing
            .Where(entry => entry.Type == candidate.Type
                            && string.Equals(entry.Key, candidate.Key, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.PurchaseLevel.GetValueOrDefault())
            .DefaultIfEmpty(0)
            .Max();
        if (level != previousLevel + 1)
            throw new InvalidOperationException($"{candidate.Type} levels must be purchased sequentially; expected level {previousLevel + 1}.");
    }

    private static void NormalizeEntry(CharacterSpendEntry entry)
    {
        entry.Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id.Trim();
        entry.Key = (entry.Key ?? string.Empty).Trim();
        entry.Name = (entry.Name ?? string.Empty).Trim();
        entry.Cost = Math.Max(0, entry.Cost);
        entry.ChoiceSelections ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (entry.Name.Length == 0)
            throw new InvalidOperationException("Spend entries require a display name.");
        if (entry.Key.Length == 0)
            entry.Key = entry.Name;
    }

    private static void NormalizePositions(CharacterSpend spend)
    {
        var ordered = OrderEntries(spend.Entries ?? new List<CharacterSpendEntry>());
        spend.Entries = ordered;
        SetPositionsInCurrentOrder(spend);
    }

    private static void SetPositionsInCurrentOrder(CharacterSpend spend)
    {
        for (var index = 0; index < spend.Entries.Count; index++)
            spend.Entries[index].Position = index + 1;
    }

    private static List<CharacterSpendEntry> OrderEntries(IEnumerable<CharacterSpendEntry> entries)
        => entries
            .Where(entry => entry != null)
            .OrderBy(entry => entry.Position <= 0 ? int.MaxValue : entry.Position)
            .ToList();

    private static HashSet<string> BuildIdSet(IEnumerable<string> entryIds)
        => new(entryIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim())
               ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

    private static string BuildCreationChoiceKey(string key, int? level)
        => $"{key.Trim()}::{level.GetValueOrDefault()}";

    private static string NormalizeName(string name)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (normalized.Length == 0)
            throw new InvalidOperationException("A spend name is required.");
        return normalized;
    }
}
