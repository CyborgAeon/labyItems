using System.Collections.Generic;
using System.Threading.Tasks;
using labyItems.Models;
using labyItems.Models.Characters;
using ServiceCharacterClassRecord = labyItems.Services.CharacterClassRecord;

namespace labyItems.Services;

public interface ICharacterCreationDataService
{
    Task<Dictionary<string, PeopleRecord>> GetPeopleAsync();
    Task<Dictionary<string, ServiceCharacterClassRecord>> GetClassesAsync();
    Task<Dictionary<string, GuildRecord>> GetGuildsAsync();
    Task<IReadOnlyList<string>> GetGuildTypesAsync();
    Task<IReadOnlyList<LifeScalePoint>> GetLifeScaleAsync(string raceName, string className);
    Task<IReadOnlyList<string>> GetClassesForRaceAsync(string raceName);
    Task<IReadOnlyList<string>> GetRacesForClassAsync(string className);
    AlignmentRule? GetGuildAlignmentRule(GuildRecord? record);
    string NormalizeLifeScaleKey(string? key);
    bool TryGetByName<T>(IReadOnlyDictionary<string, T> map, string key, out T? value);
}
