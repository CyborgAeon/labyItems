using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using labyItems.Models;
using labyItems.Models.Characters;
using ServiceCharacterClassRecord = labyItems.Services.CharacterClassRecord;

namespace labyItems.Services;

public sealed class CharacterCreationDataService : ICharacterCreationDataService
{
    public Task<Dictionary<string, PeopleRecord>> GetPeopleAsync()
        => PeopleService.GetAllAsync();

    public Task<Dictionary<string, ServiceCharacterClassRecord>> GetClassesAsync()
        => ClassService.GetAllAsync();

    public Task<Dictionary<string, GuildRecord>> GetGuildsAsync()
        => GuildsService.GetAllAsync();

    public Task<Dictionary<string, GuildRecord>> GetGuildsForMiracleSearchAsync()
        => GuildsService.GetMiracleSearchAsync();

    public Task<IReadOnlyList<string>> GetGuildTypesAsync()
        => GuildsService.GetTypesAsync();

    public Task<IReadOnlyList<LifeScalePoint>> GetLifeScaleAsync(string raceName, string className)
        => LifeScalesService.GetLifeScaleAsync(raceName, className);

    public Task<IReadOnlyList<string>> GetClassesForRaceAsync(string raceName)
        => LifeScalesService.GetClassesForRaceAsync(raceName);

    public Task<IReadOnlyList<string>> GetRacesForClassAsync(string className)
        => LifeScalesService.GetRacesForClassAsync(className);

    public AlignmentRule? GetGuildAlignmentRule(GuildRecord? record)
        => GuildsService.GetAlignmentRule(record);

    public string NormalizeLifeScaleKey(string? key)
        => LifeScalesService.NormalizeKey(key ?? string.Empty);

    public bool TryGetByName<T>(IReadOnlyDictionary<string, T> map, string key, out T? value)
    {
        value = default;
        if (map == null || string.IsNullOrWhiteSpace(key))
            return false;

        if (map.TryGetValue(key, out var direct))
        {
            value = direct;
            return true;
        }

        foreach (var kvp in map)
        {
            if (!string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                continue;

            value = kvp.Value;
            return true;
        }

        return false;
    }
}
