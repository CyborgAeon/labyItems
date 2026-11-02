using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class AccountStorageCoordinator
{
    private static bool _initialized;
    public static async Task InitializeAsync()
    {
        if (_initialized) return;
        var root = Path.Combine(FileSystem.AppDataDirectory, "account-vaults");
        Directory.CreateDirectory(root);
        var publicDb = Path.Combine(root, "public.db");
        // Always seed from the packaged catalogue, never another person's modified database.
        if (!File.Exists(publicDb))
        {
            await using (var source = await FileSystem.OpenAppPackageFileAsync("laby.db"))
            await using (var target = File.Create(publicDb + ".tmp"))
                await source.CopyToAsync(target);
            File.Move(publicDb + ".tmp", publicDb, overwrite: true);
        }
        // Populate only from trusted packaged data; never copy an installed shared
        // database back into this public reference catalogue.
        LightweightMigrator.ApplyInitialSchema(publicDb, null);
        await (ServiceHelper.ResolveService<IEvolutionDataSynchronizer>() ?? throw new InvalidOperationException("Reference synchronizer unavailable.")).EnsureCurrentAsync(publicDb);
        await (ServiceHelper.ResolveService<IAbilityDefinitionDataSynchronizer>() ?? throw new InvalidOperationException("Reference synchronizer unavailable.")).EnsureCurrentAsync(publicDb);
        await (ServiceHelper.ResolveService<ICharacterReferenceDataSynchronizer>() ?? throw new InvalidOperationException("Reference synchronizer unavailable.")).EnsureCurrentAsync(publicDb);
        await (ServiceHelper.ResolveService<IReferenceCatalogueDataSynchronizer>() ?? throw new InvalidOperationException("Reference synchronizer unavailable.")).EnsureCurrentAsync(publicDb);
        // Capture the unowned legacy database and calendar data in an encrypted archive.
        var legacy = Path.Combine(root, "legacy.vault");
        if (!File.Exists(legacy))
        {
            var staging = Path.Combine(FileSystem.CacheDirectory, "legacy-staging");
            Directory.CreateDirectory(staging);
            var oldDb = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
            if (File.Exists(oldDb))
            {
                using var original = new SqliteConnection($"Data Source={oldDb};Pooling=False");
                using var backup = new SqliteConnection($"Data Source={Path.Combine(staging, "laby.db")};Pooling=False");
                original.Open(); backup.Open(); original.BackupDatabase(backup);
            }
            var oldCalendar = Path.Combine(FileSystem.AppDataDirectory, "calendar-cache");
            if (Directory.Exists(oldCalendar))
            {
                var target = Path.Combine(staging, "calendar");
                Directory.CreateDirectory(target);
                foreach (var file in Directory.EnumerateFiles(oldCalendar)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
            var bytes = AccountWorkspace.SealDirectory(staging, await ReadKeyAsync("legacy"));
            await File.WriteAllBytesAsync(legacy + ".tmp", bytes);
            File.Move(legacy + ".tmp", legacy);
            Directory.Delete(staging, true);
            if (Directory.Exists(oldCalendar)) Directory.Delete(oldCalendar, true);
        }
        var isolationMarker = Path.Combine(root, "legacy-isolated.v1");
        if (!File.Exists(isolationMarker))
        {
            // Complete this step even if a previous process stopped just after
            // publishing the encrypted legacy archive.
            var oldDb = Path.Combine(FileSystem.AppDataDirectory, "laby.db");
            SqliteConnection.ClearAllPools();
            File.Copy(publicDb, oldDb + ".public.tmp", true);
            File.Move(oldDb + ".public.tmp", oldDb, overwrite: true);
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(oldDb + suffix)) File.Delete(oldDb + suffix);
            var oldCalendar = Path.Combine(FileSystem.AppDataDirectory, "calendar-cache");
            if (Directory.Exists(oldCalendar)) Directory.Delete(oldCalendar, true);
            await File.WriteAllTextAsync(isolationMarker, "Legacy data archived; public catalogue contains packaged data only.");
        }
        var working = Path.Combine(FileSystem.CacheDirectory, "account-workspaces");
        // Recover unsaved workspace files after an interrupted shutdown before
        // deleting plaintext. No recovered archive is opened by the login screen.
        if (Directory.Exists(working))
        {
            foreach (var directory in Directory.EnumerateDirectories(working))
            {
                var name = Path.GetFileName(directory);
                if (name.Length == 64 && name.All(Uri.IsHexDigit))
                {
                    var recovered = AccountWorkspace.SealDirectory(directory, await ReadKeyAsync(name));
                    var path = Path.Combine(root, name + ".vault");
                    await File.WriteAllBytesAsync(path + ".tmp", recovered);
                    File.Move(path + ".tmp", path, true);
                }
                Directory.Delete(directory, true);
            }
        }
        var workspace = new AccountWorkspace(LabyrintheSession.Shared, root, working, publicDb, ReadKeyAsync);
        AccountDataAccess.Workspace = workspace;
        AccountDataAccess.CacheDirectoryProvider = () =>
        {
            var path = Path.Combine(workspace.RequireDirectory(), "exports");
            Directory.CreateDirectory(path);
            return path;
        };
        ServiceHelper.RuntimeDatabasePathProvider = () =>
        {
            try { return workspace.RequireDatabase(); }
            catch (InvalidOperationException) { return publicDb; }
        };
        LiteDbService.PersonalDatabasePathProvider = workspace.RequireDatabase;
        CalendarEventStore.Shared.Cache = new WorkspaceCalendarEventCache(workspace);
        // A device-wide biometric cannot establish which member is holding a shared phone.
        new DeviceSavedLoginStorage().Remove();
        _initialized = true;
    }

    private static async Task<byte[]> ReadKeyAsync(string account)
    {
        var installation = Preferences.Default.Get("labyrinthe.vault-installation", "");
        if (installation.Length == 0)
        {
            installation = Guid.NewGuid().ToString("N");
            Preferences.Default.Set("labyrinthe.vault-installation", installation);
        }
        var name = "labyrinthe.vault-key." + installation + "." + account;
        var saved = await SecureStorage.Default.GetAsync(name);
        if (saved != null) return Convert.FromBase64String(saved);
        var key = RandomNumberGenerator.GetBytes(32);
        await SecureStorage.Default.SetAsync(name, Convert.ToBase64String(key));
        return key;
    }

    public static void ResetCaches()
    {
        SpellService.InvalidateCache(); MiracleService.InvalidateCache();
        SpellDamageService.InvalidateCache(); MiracleDamageService.InvalidateCache();
        EvocationDamageService.InvalidateCache(); MiracleHealingService.InvalidateCache();
        MultiClassService.InvalidateCache(); MultiRaceService.InvalidateCache();
        SystemRulesService.InvalidateCache();
        DruidEvocationService.InvalidateCache(); NeuronicService.InvalidateCache();
        ClassService.InvalidateCache(); PeopleService.InvalidateCache();
        LifeScalesService.InvalidateCache(); EvolutionService.InvalidateCache();
        AbilityDetailsLookupService.InvalidateCache();
        AbilityDefinitionLookupService.InvalidateCache();
        GuildsService.InvalidateCache(); SpecialisationService.InvalidateCache();
        Specialisations.SpecialisationDefinitionRepository.InvalidateCache();
        labyItems.Pages.Characters.ViewModels.AdvanceAbilitySearchVm.InvalidateAccountCache();
        labyItems.Pages.Calculator.MpCalculatorPageBase.InvalidateAccountCache();
    }
}
