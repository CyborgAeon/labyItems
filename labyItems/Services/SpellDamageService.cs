using System.Text.Json;
using System.Linq;
using Microsoft.Maui.Storage;

namespace labyItems.Services;

public static class SpellDamageService
{
    private static List<DamageSpell>? _cache;

    public static async Task<List<DamageSpell>> GetDamagingSpellsAsync()
    {
        if (_cache != null)
            return _cache;

        await using var s = await FileSystem.OpenAppPackageFileAsync("grimoire/allSpells.json");
        using var r = new StreamReader(s);
        var json = await r.ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var list = new List<DamageSpell>();

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("damage", out var damageEl))
                continue;

            var name = GetString(el, "name");
            var level = GetInt(el, "level");
            var damageList = ReadIntArrayList(damageEl);
            if (damageList.Count == 0)
                continue;

            var types = el.TryGetProperty("damType", out var typeEl)
                ? ReadStringList(typeEl)
                : new List<string>();
            var macApplies = el.TryGetProperty("MACApplies", out var macEl)
                ? ReadIntArrayList(macEl)
                : new List<int[]>();
            var pacDam = el.TryGetProperty("PACDam", out var pacEl)
                ? ReadIntList(pacEl)
                : new List<int>();

            var parts = new List<DamagePart>();
            for (var i = 0; i < damageList.Count; i++)
            {
                var dmg = damageList[i];
                var tblp = dmg.Length > 0 ? dmg[0] : 0;
                var loc = dmg.Length > 1 ? dmg[1] : 0;

                var type = types.Count > i ? types[i] : (types.Count > 0 ? types[^1] : "Missile");
                var mac = macApplies.Count > i ? macApplies[i] : new[] { 0, 0 };
                var macTblp = mac.Length > 0 ? mac[0] : 0;
                var macLoc = mac.Length > 1 ? mac[1] : 0;
                var pac = pacDam.Count > i ? pacDam[i] : 0;

                parts.Add(new DamagePart(tblp, loc, type, macTblp, macLoc, pac));
            }

            if (parts.Count > 0)
                list.Add(new DamageSpell(name, level, parts));
        }

        _cache = list;
        return list;
    }

    private static string GetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? string.Empty : string.Empty;

    private static int GetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return 0;

        return p.ValueKind switch
        {
            JsonValueKind.Number => p.GetInt32(),
            JsonValueKind.String => int.TryParse(p.GetString(), out var v) ? v : 0,
            _ => 0
        };
    }

    private static List<string> ReadStringList(JsonElement el)
    {
        var list = new List<string>();
        if (el.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    list.Add(value.Trim());
            }
        }

        return list;
    }

    private static List<int[]> ReadIntArrayList(JsonElement el)
    {
        var list = new List<int[]>();

        if (el.ValueKind != JsonValueKind.Array)
            return list;

        if (el.GetArrayLength() == 0)
            return list;

        if (el[0].ValueKind != JsonValueKind.Array)
        {
            var single = new List<int>();
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                    single.Add(item.GetInt32());
                else if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out var v))
                    single.Add(v);
            }

            if (single.Count > 0)
                list.Add(single.ToArray());

            return list;
        }

        foreach (var arr in el.EnumerateArray())
        {
            if (arr.ValueKind != JsonValueKind.Array)
                continue;

            var values = new List<int>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                    values.Add(item.GetInt32());
                else if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out var v))
                    values.Add(v);
            }

            if (values.Count > 0)
                list.Add(values.ToArray());
        }

        return list;
    }

    private static List<int> ReadIntList(JsonElement el)
    {
        var list = new List<int>();
        if (el.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number)
                list.Add(item.GetInt32());
            else if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out var v))
                list.Add(v);
        }

        return list;
    }
}

public sealed record DamageSpell(string Name, int Level, IReadOnlyList<DamagePart> Parts)
{
    public string Summary
        => string.Join("; ", Parts.Select(p => $"{p.DamType}:{p.Tblp}/{p.Loc}"));
}

public sealed record DamagePart(int Tblp, int Loc, string DamType, int MacTblp, int MacLoc, int PacDam);
