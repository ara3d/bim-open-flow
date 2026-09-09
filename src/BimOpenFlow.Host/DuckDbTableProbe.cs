using Ara3D.BimOpenSchema.DuckDb;
using BimOpenFlow.Contracts;
using BimOpenFlow.Nodes.DuckDb;

namespace BimOpenFlow.Host;

/// <summary>One column of a described DuckDB table.</summary>
public sealed record DuckColumn(string Name, string Type);

/// <summary>One table of a described DuckDB database: its columns and row count.</summary>
public sealed record DuckTable(string Name, long RowCount, IReadOnlyList<DuckColumn> Columns);

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
