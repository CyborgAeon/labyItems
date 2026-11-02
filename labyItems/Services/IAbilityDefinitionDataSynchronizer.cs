namespace labyItems.Services;

public interface IAbilityDefinitionDataSynchronizer
{
    Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default);
}
