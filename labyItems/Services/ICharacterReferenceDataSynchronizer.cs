namespace labyItems.Services;

public interface ICharacterReferenceDataSynchronizer
{
    Task EnsureCurrentAsync(string dbPath, CancellationToken cancellationToken = default);
}

