using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace SQLite;

[Flags]
public enum SQLiteOpenFlags
{
    ReadOnly = 1,
    ReadWrite = 2,
    Create = 4
}

public static class SQLiteTestStore
{
    private sealed record MiracleRow(string Name, int Power, string DataJson);
    private sealed record EvolutionRow(
        int Id,
        string Index,
        string Description,
        int Cost,
        int TableId,
        string Available,
        int CanBuyMultiple,
        string PreReqsJson,
        string DataJson);
    private sealed record NgramRow(int EvolutionId, string Token);
    private sealed record LifeScaleRow(string Race, string Class, int Index, int Body, int Loc);

    private static readonly List<string> SpellRows = new();
    private static readonly List<MiracleRow> MiracleRows = new();
    private static readonly List<EvolutionRow> EvolutionRows = new();
    private static readonly List<NgramRow> EvolutionNgrams = new();
    private static readonly List<LifeScaleRow> LifeScaleRows = new();
    private static int _nextEvolutionId = 1;

    public static void Reset()
    {
        SpellRows.Clear();
        MiracleRows.Clear();
        EvolutionRows.Clear();
        EvolutionNgrams.Clear();
        LifeScaleRows.Clear();
        _nextEvolutionId = 1;
    }

    public static void AddSpell(string dataJson)
        => SpellRows.Add(dataJson ?? string.Empty);

    public static void AddMiracle(string name, int power, string dataJson)
        => MiracleRows.Add(new MiracleRow(name ?? string.Empty, power, dataJson ?? string.Empty));

    public static int AddEvolution(
        string index,
        string description,
        int cost,
        int tableId,
        string available,
        int canBuyMultiple,
        string preReqsJson,
        string dataJson)
    {
        var id = _nextEvolutionId++;
        EvolutionRows.Add(new EvolutionRow(
            id,
            index ?? string.Empty,
            description ?? string.Empty,
            cost,
            tableId,
            available ?? string.Empty,
            canBuyMultiple,
            preReqsJson ?? string.Empty,
            dataJson ?? string.Empty));
        return id;
    }

    public static void AddEvolutionNgram(int evolutionId, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        EvolutionNgrams.Add(new NgramRow(evolutionId, token));
    }

    public static void AddLifeScale(string race, string @class, int index, int body, int loc)
        => LifeScaleRows.Add(new LifeScaleRow(race ?? string.Empty, @class ?? string.Empty, index, body, loc));

    internal static List<T> Query<T>(string sql, object[] args) where T : new()
    {
        if (sql.Contains("FROM spells", StringComparison.OrdinalIgnoreCase))
        {
            return SpellRows
                .Select(json => Materialize<T>(new Dictionary<string, object?>
                {
                    ["data_json"] = json
                }))
                .ToList();
        }

        if (sql.Contains("FROM miracles", StringComparison.OrdinalIgnoreCase))
        {
            return MiracleRows
                .OrderBy(r => r.Power)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(row => Materialize<T>(new Dictionary<string, object?>
                {
                    ["name"] = row.Name,
                    ["power"] = row.Power,
                    ["data_json"] = row.DataJson
                }))
                .ToList();
        }

        if (sql.Contains("FROM lifescales", StringComparison.OrdinalIgnoreCase))
        {
            return LifeScaleRows
                .OrderBy(r => r.Race, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Class, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Index)
                .Select(row => Materialize<T>(new Dictionary<string, object?>
                {
                    ["race"] = row.Race,
                    ["class"] = row.Class,
                    ["idx"] = row.Index,
                    ["body"] = row.Body,
                    ["loc"] = row.Loc
                }))
                .ToList();
        }

        if (sql.Contains("FROM evolution e JOIN", StringComparison.OrdinalIgnoreCase))
        {
            var tokens = (args ?? Array.Empty<object>())
                .Select(a => (a ?? string.Empty).ToString() ?? string.Empty)
                .Where(t => t.Length > 0)
                .ToHashSet(StringComparer.Ordinal);

            var matched = EvolutionNgrams
                .Where(n => tokens.Contains(n.Token))
                .GroupBy(n => n.EvolutionId)
                .OrderByDescending(g => g.Count())
                .Take(50)
                .Select(g => g.Key)
                .ToHashSet();

            var rows = EvolutionRows.Where(r => matched.Contains(r.Id)).ToList();
            return MaterializeEvolutionRows<T>(rows, includeAbilityColumns: sql.Contains("available", StringComparison.OrdinalIgnoreCase));
        }

        if (sql.Contains("FROM evolution WHERE idx LIKE", StringComparison.OrdinalIgnoreCase))
        {
            var like = (args ?? Array.Empty<object>()).FirstOrDefault()?.ToString() ?? string.Empty;
            var needle = like.Trim('%');

            var rows = EvolutionRows
                .Where(r => r.Index.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.TableId)
                .ThenBy(r => r.Index, StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList();

            return MaterializeEvolutionRows<T>(rows, includeAbilityColumns: true);
        }

        if (sql.Contains("FROM evolution", StringComparison.OrdinalIgnoreCase))
        {
            var rows = EvolutionRows
                .OrderBy(r => r.TableId)
                .ThenBy(r => r.Index, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return MaterializeEvolutionRows<T>(rows, includeAbilityColumns: sql.Contains("available", StringComparison.OrdinalIgnoreCase));
        }

        return new List<T>();
    }

    private static List<T> MaterializeEvolutionRows<T>(IReadOnlyList<EvolutionRow> rows, bool includeAbilityColumns) where T : new()
    {
        var list = new List<T>(rows.Count);
        foreach (var row in rows)
        {
            var values = new Dictionary<string, object?>
            {
                ["id"] = row.Id,
                ["idx"] = row.Index,
                ["description"] = row.Description,
                ["cost"] = row.Cost,
                ["table_id"] = row.TableId
            };

            if (includeAbilityColumns)
            {
                values["available"] = row.Available;
                values["can_buy_multiple"] = row.CanBuyMultiple;
                values["prereqs_json"] = row.PreReqsJson;
                values["data_json"] = row.DataJson;
            }

            list.Add(Materialize<T>(values));
        }

        return list;
    }

    private static T Materialize<T>(Dictionary<string, object?> values) where T : new()
    {
        var instance = new T();
        var props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var prop in props)
        {
            if (!prop.CanWrite)
                continue;

            var key = values.Keys.FirstOrDefault(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase));
            if (key == null)
                continue;

            var raw = values[key];
            if (raw == null)
            {
                prop.SetValue(instance, null);
                continue;
            }

            var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            object converted;
            if (target.IsEnum)
                converted = Enum.Parse(target, raw.ToString() ?? string.Empty, ignoreCase: true);
            else if (target == typeof(string))
                converted = raw.ToString() ?? string.Empty;
            else
                converted = Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);

            prop.SetValue(instance, converted);
        }

        return instance;
    }
}

public sealed class SQLiteConnection : IDisposable
{
    public SQLiteConnection(string databasePath, SQLiteOpenFlags openFlags = SQLiteOpenFlags.ReadWrite, bool storeDateTimeAsTicks = true)
    {
        DatabasePath = databasePath;
        OpenFlags = openFlags;
    }

    public string DatabasePath { get; }

    public SQLiteOpenFlags OpenFlags { get; }

    public List<T> Query<T>(string sql, params object[] args) where T : new()
        => SQLiteTestStore.Query<T>(sql, args ?? Array.Empty<object>());

    public void Dispose()
    {
    }
}
