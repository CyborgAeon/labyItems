using Microsoft.Maui.Storage;

namespace labyItems.Tests;

public abstract class ServiceTestBase
{
    protected ServiceTestBase()
    {
        ServiceTestEnvironment.EnsureInitialized();
        FileSystem.ClearPackageOverrides();
        ServiceCacheResetter.ResetAll();
    }
}
