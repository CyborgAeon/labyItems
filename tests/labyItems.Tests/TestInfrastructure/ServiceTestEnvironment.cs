using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using labyItems.Services;
using SQLite;

namespace labyItems.Tests;

internal static class ServiceTestEnvironment
{
    private static int _initialized;

    public static string RepoRoot { get; private set; } = string.Empty;
    public static string PackageRoot { get; private set; } = string.Empty;
    public static string AppDataDirectory { get; private set; } = string.Empty;
    public static string CacheDirectory { get; private set; } = string.Empty;
    public static string DbPath => Path.Combine(AppDataDirectory, ServiceHelper.DbFileName);

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        RepoRoot = FindRepoRoot();
        PackageRoot = Path.Combine(RepoRoot, "labyItems", "Resources", "Raw");

        var root = Path.Combine(Path.GetTempPath(), "labyitems-tests", Guid.NewGuid().ToString("N"));
        AppDataDirectory = Path.Combine(root, "appdata");
        CacheDirectory = Path.Combine(root, "cache");
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(CacheDirectory);

        FileSystem.Configure(AppDataDirectory, CacheDirectory, PackageRoot);
        FileSystem.ClearPackageOverrides();

        SeedDatabase(DbPath);
        ServiceCacheResetter.ResetAll();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var sln = Path.Combine(dir.FullName, "labyItems.sln");
            if (File.Exists(sln))
                return dir.FullName;

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static void SeedDatabase(string dbPath)
    {
        File.WriteAllText(dbPath, "seed");
        SQLiteTestStore.Reset();

        var seedSpell = new SpellService.SpellRaw
        {
            name = "Seed Spell",
            level = 2,
            colour = "Red",
            description = "Test spell",
            range = "Touch",
            duration = "Instant",
            gesture = "Wave",
            verbal = "Seed",
            notes = "",
            isAdvanced = false
        };
        SQLiteTestStore.AddSpell(JsonSerializer.Serialize(seedSpell));

        var seedMiracle = new MiracleService.MiracRaw
        {
            name = "Seed Miracle",
            power = 3,
            alignment = "Neutral",
            sphere = "General",
            description = "Test miracle",
            range = "Self",
            duration = "Instant",
            gesture = "",
            verbal = "",
            level = "1",
            isAdvanced = false
        };
        SQLiteTestStore.AddMiracle(
            seedMiracle.name,
            seedMiracle.power,
            JsonSerializer.Serialize(seedMiracle));

        InsertEvolution(
            idx: "AA1",
            description: "Arcane Attunement",
            cost: 3,
            tableId: 1,
            available: "Any",
            canBuyMultiple: false,
            preReqsJson: "[\"Ability:Focus\"]",
            dataJson: "{\"maxAvailable\":2}");

        InsertEvolution(
            idx: "Immunity! Fire",
            description: "Grants fire immunity",
            cost: 8,
            tableId: 2,
            available: "Any",
            canBuyMultiple: false,
            preReqsJson: "[]",
            dataJson: "{}");

        SQLiteTestStore.AddLifeScale("Human", "Wizard", 1, 10, 5);
    }

    private static void InsertEvolution(
        string idx,
        string description,
        int cost,
        int tableId,
        string available,
        bool canBuyMultiple,
        string preReqsJson,
        string dataJson)
    {
        var id = SQLiteTestStore.AddEvolution(
            idx,
            description,
            cost,
            tableId,
            available,
            canBuyMultiple ? 1 : 0,
            preReqsJson,
            dataJson);

        var searchable = ServiceHelper.NormalizeForNgrams($"{idx} {description}".ToLowerInvariant());
        var ngrams = ServiceHelper.GenerateNGrams(searchable, 3).Distinct(StringComparer.Ordinal).ToList();
        foreach (var token in ngrams)
            SQLiteTestStore.AddEvolutionNgram(id, token);
    }
}
