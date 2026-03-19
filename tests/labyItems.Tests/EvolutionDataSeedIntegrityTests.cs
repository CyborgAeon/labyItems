using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace labyItems.Tests;

public sealed class EvolutionDataSeedIntegrityTests : ServiceTestBase
{
    [Fact]
    public void MergedEvolutionSeed_IsValidAndContainsCanonicalResistanceAbility()
    {
        var path = Path.Combine(
            ServiceTestEnvironment.PackageRoot,
            "evolution_classes",
            "merged.json");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() > 0);

        var hasCanonicalResistance = doc.RootElement
            .EnumerateArray()
            .Any(entry =>
                entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty("index", out var indexElement)
                && indexElement.ValueKind == JsonValueKind.String
                && string.Equals(
                    indexElement.GetString(),
                    "9th Level Resistance to Magic and Spirits",
                    StringComparison.OrdinalIgnoreCase));

        Assert.True(hasCanonicalResistance);
    }

    [Fact]
    public void MergedEvolutionSeed_ContainsMultipleSourceBooks()
    {
        var path = Path.Combine(
            ServiceTestEnvironment.PackageRoot,
            "evolution_classes",
            "merged.json");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        var sourceBooks = doc.RootElement
            .EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.Object)
            .Select(entry =>
                entry.TryGetProperty("sourceBook", out var sourceBookElement)
                && sourceBookElement.ValueKind == JsonValueKind.String
                    ? (sourceBookElement.GetString() ?? string.Empty).Trim()
                    : string.Empty)
            .Where(sourceBook => sourceBook.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(sourceBooks.Count > 1);
    }
}
