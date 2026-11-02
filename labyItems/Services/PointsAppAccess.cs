using System.Text.Json;

namespace labyItems.Services;

/// <summary>Points apps are member-owned content, including their persisted document references.</summary>
public static class PointsAppAccess
{
    public static string? CurrentOwnerKey
    {
        get
        {
            try { return AccountDataAccess.Workspace?.RequireMemberKey(); }
            catch (InvalidOperationException) { return null; }
        }
    }
    public static string RequireOwner(string? expectedOwner)
    {
        var current = CurrentOwnerKey;
        if (current == null || !string.Equals(current, expectedOwner, StringComparison.Ordinal))
            throw new InvalidOperationException("Sign in to the member account that owns this points app.");
        return current;
    }
    public static string RequireDatabase()
        => AccountDataAccess.Workspace?.RequireMemberDatabase()
            ?? throw new InvalidOperationException("Sign in with a member account to access points apps.");

    public static bool CanSeeDefinition(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson)) return true;
        try
        {
            using var document = JsonDocument.Parse(dataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return true;
            string? owner = null;
            var nonStandard = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals("ownerAccountKey", StringComparison.OrdinalIgnoreCase))
                    owner = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : "";
                if (property.Name.Equals("nonStandard", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Equals("isNonStandard", StringComparison.OrdinalIgnoreCase))
                    nonStandard |= property.Value.ValueKind == JsonValueKind.True
                        || property.Value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase)
                        || property.Value.ToString() == "1";
            }
            if (!nonStandard && owner == null) return true;
            var current = CurrentOwnerKey;
            // Pre-existing rows without a stamp belong to their already-isolated member database.
            return current != null && (owner == null || string.Equals(owner, current, StringComparison.Ordinal));
        }
        catch (JsonException) { return false; }
    }
}
