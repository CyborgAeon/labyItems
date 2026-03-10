using System.Runtime.CompilerServices;

namespace labyItems.Tests;

internal static class ModuleInit
{
    [ModuleInitializer]
    public static void Initialize()
    {
        ServiceTestEnvironment.EnsureInitialized();
    }
}
