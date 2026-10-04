using Ara3D.BimOpenSchema.DuckDb;
using BimOpenFlow.Contracts;
using BimOpenFlow.Nodes.DuckDb;

namespace BimOpenFlow.Host;

/// <summary>One column of a described DuckDB table.</summary>
public sealed record DuckColumn(string Name, string Type);

/// <summary>One table of a described DuckDB database: its columns and row count.</summary>
public sealed record DuckTable(string Name, long RowCount, IReadOnlyList<DuckColumn> Columns);

/// <summary>One column's value profile: NULL count, distinct count, a few sample
/// values as text, and the range for numeric columns.</summary>
public sealed record DuckColumnProfile(string Name, string Type, long NullCount, long DistinctCount,
    IReadOnlyList<string> Samples, string? Min, string? Max);

/// <summary>A .duckdb file found under a model root.</summary>
public sealed record DuckDatabase(string Name, string Path, long SizeBytes);

/// <summary>DuckDB-backed FileTableProbe for TablesInFile suggestions, plus the
/// schema description the MCP server offers agents. Every call opens the file
/// read-only, so nothing here can modify a database.</summary>
public static class DuckDbTableProbe
{
    public const string Extension = ".duckdb";

    public static IReadOnlyList<Suggestion> Tables(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}", path);
        using var conn = DuckDbOps.OpenReadOnly(path);
        var names = conn.Query(
            "SELECT table_name FROM information_schema.tables "
            + "WHERE table_schema = 'main' ORDER BY table_name");
        var tables = new List<Suggestion>(names.Rows.Count);
        for (var row = 0; row < names.Rows.Count; row++)
            tables.Add(new((string)names[0, row]!, null));
        return tables;
    }

    /// <summary>Every table in the file (or just one) with its columns, types, and
    /// row count: what an agent reads before writing SQL against a database it was
    /// handed. An unknown table name is an error, not an empty list.</summary>
    public static IReadOnlyList<DuckTable> Describe(string path, string? table = null)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}", path);
        using var conn = DuckDbOps.OpenReadOnly(path);
        return conn.GetTableInfo(string.IsNullOrWhiteSpace(table) ? null : table.Trim())
            .Select(t => new DuckTable(t.Table, t.RowCount,
                t.Columns.Select(c => new DuckColumn(c.Name, c.Type)).ToList()))
            .ToList();
    }

    /// <summary>What the values of one table look like, column by column: how many
    /// are NULL, how many distinct values there are, a few sample values, and the
    /// range for numeric columns. This is the vocabulary an agent needs to write a
    /// filter (which reason codes exist, what storey names look like) without a
    /// round trip per question.</summary>
    public static IReadOnlyList<DuckColumnProfile> Profile(string path, string table, int samples = 5)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}", path);
        using var conn = DuckDbOps.OpenReadOnly(path);
        var info = conn.GetTableInfo(table).Single();
        var t = Quote(info.Table);
        var result = new List<DuckColumnProfile>(info.Columns.Count);
        foreach (var column in info.Columns)
        {
            var c = Quote(column.Name);
            var counts = conn.Query($"SELECT count(*) - count({c}), count(DISTINCT {c}) FROM {t}");
            var nulls = Convert.ToInt64(counts[0, 0]);
            var distinct = Convert.ToInt64(counts[1, 0]);
            var values = new List<string>();
            string? min = null, max = null;
            if (distinct > 0)
            {
                var sampled = conn.Query(
                    $"SELECT DISTINCT CAST({c} AS VARCHAR) FROM {t} WHERE {c} IS NOT NULL ORDER BY 1 LIMIT {samples}");
                for (var row = 0; row < sampled.Rows.Count; row++)
                    values.Add(Clip(sampled[0, row]?.ToString() ?? ""));
                if (IsNumeric(column.Type))
                {
                    var range = conn.Query($"SELECT CAST(min({c}) AS VARCHAR), CAST(max({c}) AS VARCHAR) FROM {t}");
                    min = range[0, 0]?.ToString();
                    max = range[1, 0]?.ToString();
                }
            }
            result.Add(new DuckColumnProfile(column.Name, column.Type, nulls, distinct, values, min, max));
        }
        return result;
    }

    private static bool IsNumeric(string type)
        => type is "INTEGER" or "BIGINT" or "SMALLINT" or "TINYINT" or "HUGEINT" or "DOUBLE" or "FLOAT" or "DECIMAL"
            || type.StartsWith("DECIMAL", StringComparison.Ordinal) || type.StartsWith("U", StringComparison.Ordinal) && type.EndsWith("INT", StringComparison.Ordinal);

    private static string Quote(string identifier)
        => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string Clip(string text)
        => text.Length <= 60 ? text : text[..60] + "…";

    /// <summary>The .duckdb files directly under each root, with forward-slash
    /// paths ready to paste into a duck.source node.</summary>
    public static IReadOnlyList<DuckDatabase> ListDatabases(IEnumerable<string> roots)
        => roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*" + Extension, SearchOption.TopDirectoryOnly))
            .Select(file => new FileInfo(file))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new DuckDatabase(f.Name, f.FullName.Replace('\\', '/'), f.Length))
            .ToList();
}
