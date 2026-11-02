namespace labyItems.Services;

public interface IEvolutionDataSynchronizer
{
    Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default);
}
